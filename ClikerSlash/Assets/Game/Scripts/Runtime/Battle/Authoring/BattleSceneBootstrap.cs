using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 물류 씬이 로드될 때 씬 설정 오브젝트로부터 런타임 ECS 싱글턴을 구성합니다.
    /// </summary>
    public sealed class BattleSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleConfigAuthoring battleConfigAuthoring;
        [SerializeField] private PlayerAuthoring playerAuthoring;
        [SerializeField] private CargoAuthoring cargoAuthoring;
        [SerializeField] private LaneLayoutAuthoring laneLayoutAuthoring;
        [SerializeField] private MetaProgressionCatalogAsset metaProgressionCatalog;

        private void Awake()
        {
            Bootstrap();
        }

        private void OnEnable()
        {
            Bootstrap();
        }

        /// <summary>
        /// 현재 씬의 설정 오브젝트를 기준으로 프로토타입 ECS 구성을 다시 만듭니다.
        /// </summary>
        private void Bootstrap()
        {
            Application.runInBackground = true;
            PrototypeSessionRuntime.ConsumeBattleEntryRequest();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            battleConfigAuthoring ??= FindFirstObjectByType<BattleConfigAuthoring>();
            playerAuthoring ??= FindFirstObjectByType<PlayerAuthoring>();
            cargoAuthoring ??= FindFirstObjectByType<CargoAuthoring>();
            laneLayoutAuthoring ??= FindFirstObjectByType<LaneLayoutAuthoring>();

            if (battleConfigAuthoring == null || playerAuthoring == null || cargoAuthoring == null || laneLayoutAuthoring == null)
            {
                Debug.LogError("BattleSceneBootstrap could not find all required authoring objects.");
                return;
            }

            metaProgressionCatalog = MetaProgressionBootstrapBridge.ResolveCatalog(metaProgressionCatalog);
            var runtimeState = MetaProgressionBootstrapBridge.EnsureRuntimeState(
                metaProgressionCatalog,
                laneLayoutAuthoring.LaneWorldXs.Count);

            var entityManager = world.EntityManager;
            DestroyExistingSingletons(entityManager, typeof(BattleConfig));
            DestroyExistingSingletons(entityManager, typeof(PlayerConfig));
            DestroyExistingSingletons(entityManager, typeof(CargoConfig));
            DestroyExistingSingletons(entityManager, typeof(LaneLayout));
            DestroyEntities(entityManager, typeof(PlayerTag));
            DestroyEntities(entityManager, typeof(CargoTag));
            DestroyEntities(entityManager, typeof(CargoHandledEvent));
            DestroyEntities(entityManager, typeof(CargoMissedEvent));

            var battleEntity = entityManager.CreateEntity(typeof(BattleConfig));
            var battleConfig = MetaProgressionBootstrapBridge.ApplyToBattleConfig(
                new BattleConfig
                {
                    BaseWorkDurationSeconds = battleConfigAuthoring.BaseWorkDurationSeconds,
                    HealthDurationBonusSeconds = battleConfigAuthoring.HealthDurationBonusSeconds,
                    PlayerMoveDuration = battleConfigAuthoring.PlayerMoveDuration,
                    HandleDurationSeconds = battleConfigAuthoring.HandleDurationSeconds,
                    SpawnInterval = battleConfigAuthoring.SpawnInterval,
                    CargoSpawnZ = battleConfigAuthoring.CargoSpawnZ,
                    JudgmentLineZ = battleConfigAuthoring.JudgmentLineZ,
                    FailLineZ = battleConfigAuthoring.FailLineZ,
                    HandleWindowHalfDepth = battleConfigAuthoring.HandleWindowHalfDepth,
                    StartingMaxHandleWeight = battleConfigAuthoring.StartingMaxHandleWeight
                },
                runtimeState.resolvedProgression);
            entityManager.SetComponentData(battleEntity, battleConfig);

            var playerEntity = entityManager.CreateEntity(typeof(PlayerConfig));
            entityManager.SetComponentData(playerEntity, new PlayerConfig
            {
                InitialLane = playerAuthoring.InitialLane,
                Y = playerAuthoring.Y,
                Z = playerAuthoring.Z
            });

            var cargoEntity = entityManager.CreateEntity(typeof(CargoConfig));
            entityManager.SetComponentData(cargoEntity, new CargoConfig
            {
                Weight = cargoAuthoring.Weight,
                Reward = cargoAuthoring.Reward,
                Penalty = cargoAuthoring.Penalty,
                Y = cargoAuthoring.Y,
                MoveSpeed = cargoAuthoring.MoveSpeed
            });

            var laneEntity = entityManager.CreateEntity(typeof(LaneLayout));
            entityManager.SetComponentData(laneEntity, new LaneLayout
            {
                LaneCount = laneLayoutAuthoring.LaneWorldXs.Count
            });

            var laneBuffer = entityManager.AddBuffer<LaneWorldXElement>(laneEntity);
            foreach (var laneX in laneLayoutAuthoring.LaneWorldXs)
            {
                laneBuffer.Add(new LaneWorldXElement { Value = laneX });
            }
        }

        private static void DestroyExistingSingletons(EntityManager entityManager, ComponentType componentType)
        {
            using var entities = entityManager.CreateEntityQuery(componentType).ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
            {
                return;
            }

            entityManager.DestroyEntity(entities);
        }

        private static void DestroyEntities(EntityManager entityManager, ComponentType componentType)
        {
            using var entities = entityManager.CreateEntityQuery(componentType).ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
            {
                return;
            }

            entityManager.DestroyEntity(entities);
        }
    }
}
