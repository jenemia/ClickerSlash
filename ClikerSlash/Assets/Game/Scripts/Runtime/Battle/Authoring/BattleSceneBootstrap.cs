using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    public sealed class BattleSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleConfigAuthoring battleConfigAuthoring;
        [SerializeField] private PlayerAuthoring playerAuthoring;
        [SerializeField] private EnemyAuthoring enemyAuthoring;
        [SerializeField] private LaneLayoutAuthoring laneLayoutAuthoring;

        private void Awake()
        {
            Bootstrap();
        }

        private void OnEnable()
        {
            Bootstrap();
        }

        private void Bootstrap()
        {
            Application.runInBackground = true;

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
            DestroyExistingSingletons(entityManager, typeof(BattleConfig));
            DestroyExistingSingletons(entityManager, typeof(PlayerConfig));
            DestroyExistingSingletons(entityManager, typeof(EnemyConfig));
            DestroyExistingSingletons(entityManager, typeof(LaneLayout));

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

        private static void DestroyExistingSingletons(EntityManager entityManager, ComponentType componentType)
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
