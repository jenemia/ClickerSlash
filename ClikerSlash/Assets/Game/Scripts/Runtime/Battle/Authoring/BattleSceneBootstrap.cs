using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 전투 씬이 로드될 때 씬 설정 오브젝트로부터 런타임 ECS 싱글턴을 구성합니다.
    /// </summary>
    public sealed class BattleSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleConfigAuthoring battleConfigAuthoring;
        [SerializeField] private PlayerAuthoring playerAuthoring;
        [SerializeField] private EnemyAuthoring enemyAuthoring;
        [SerializeField] private LaneLayoutAuthoring laneLayoutAuthoring;

        /// <summary>
        /// 씬 인스턴스가 만들어지자마자 가능한 빨리 런타임 초기화를 시작합니다.
        /// </summary>
        private void Awake()
        {
            Bootstrap();
        }

        /// <summary>
        /// 에디터에서 다시 활성화되거나 씬이 재로딩됐을 때 부트스트랩을 다시 수행합니다.
        /// </summary>
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

            // 이 플래그는 허브에서 이 씬으로 넘어오는 순간에만 의미가 있으므로 즉시 소비합니다.
            PrototypeSessionRuntime.ConsumeBattleEntryRequest();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            battleConfigAuthoring ??= FindFirstObjectByType<BattleConfigAuthoring>();
            playerAuthoring ??= FindFirstObjectByType<PlayerAuthoring>();
            enemyAuthoring ??= FindFirstObjectByType<EnemyAuthoring>();
            laneLayoutAuthoring ??= FindFirstObjectByType<LaneLayoutAuthoring>();

            if (battleConfigAuthoring == null || playerAuthoring == null || enemyAuthoring == null || laneLayoutAuthoring == null)
            {
                Debug.LogError("BattleSceneBootstrap could not find all required authoring objects.");
                return;
            }

            var entityManager = world.EntityManager;
            // 허브에서 다시 전투로 들어왔을 때 깨끗한 월드에서 시작하도록 남아 있던 싱글턴과 런타임 엔티티를 지웁니다.
            DestroyExistingSingletons(entityManager, typeof(BattleConfig));
            DestroyExistingSingletons(entityManager, typeof(PlayerConfig));
            DestroyExistingSingletons(entityManager, typeof(EnemyConfig));
            DestroyExistingSingletons(entityManager, typeof(LaneLayout));
            DestroyEntities(entityManager, typeof(PlayerTag));
            DestroyEntities(entityManager, typeof(EnemyTag));
            DestroyEntities(entityManager, typeof(AttackHitEvent));

            var battleEntity = entityManager.CreateEntity(typeof(BattleConfig));
            entityManager.SetComponentData(battleEntity, new BattleConfig
            {
                BattleDurationSeconds = battleConfigAuthoring.BattleDurationSeconds,
                StartingLives = battleConfigAuthoring.StartingLives,
                PlayerMoveDuration = battleConfigAuthoring.PlayerMoveDuration,
                AttackInterval = battleConfigAuthoring.AttackInterval,
                SpawnInterval = battleConfigAuthoring.SpawnInterval,
                EnemySpawnZ = battleConfigAuthoring.EnemySpawnZ,
                DefenseLineZ = battleConfigAuthoring.DefenseLineZ
            });

            var playerEntity = entityManager.CreateEntity(typeof(PlayerConfig));
            entityManager.SetComponentData(playerEntity, new PlayerConfig
            {
                InitialLane = playerAuthoring.InitialLane,
                Y = playerAuthoring.Y,
                Z = playerAuthoring.Z
            });

            var enemyEntity = entityManager.CreateEntity(typeof(EnemyConfig));
            entityManager.SetComponentData(enemyEntity, new EnemyConfig
            {
                Health = enemyAuthoring.Health,
                Y = enemyAuthoring.Y,
                MoveSpeed = enemyAuthoring.MoveSpeed
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

        /// <summary>
        /// 새 싱글턴 상태를 쓰기 전에 이전 싱글턴 보관 엔티티를 제거합니다.
        /// </summary>
        private static void DestroyExistingSingletons(EntityManager entityManager, ComponentType componentType)
        {
            using var entities = entityManager.CreateEntityQuery(componentType).ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
            {
                return;
            }

            entityManager.DestroyEntity(entities);
        }

        /// <summary>
        /// 특정 게임플레이 태그나 이벤트 컴포넌트를 가진 런타임 엔티티를 모두 제거합니다.
        /// </summary>
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
