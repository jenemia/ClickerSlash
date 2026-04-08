using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerLaneMoveSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<EnemyConfig>();
            state.RequireForUpdate<LaneLayout>();
            state.RequireForUpdate<SpawnTimerState>();
            state.RequireForUpdate<StageProgressState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingleton<StageProgressState>();
            if (stageProgress.IsFinished != 0)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var enemyConfig = SystemAPI.GetSingleton<EnemyConfig>();
            var laneLayout = SystemAPI.GetSingleton<LaneLayout>();
            var laneEntity = SystemAPI.GetSingletonEntity<LaneLayout>();
            var laneXs = state.EntityManager.GetBuffer<LaneWorldXElement>(laneEntity);
            var spawnTimer = SystemAPI.GetSingletonRW<SpawnTimerState>();

            spawnTimer.ValueRW.Remaining -= deltaTime;
            if (spawnTimer.ValueRO.Remaining > 0f)
            {
                return;
            }

            spawnTimer.ValueRW.Remaining += battleConfig.SpawnInterval;
            var laneIndex = spawnTimer.ValueRW.Random.NextInt(0, laneLayout.LaneCount);
            var laneX = BattleLaneUtility.GetLaneX(laneXs, laneIndex);

            var enemyEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(enemyEntity, new EnemyTag());
            state.EntityManager.AddComponentData(enemyEntity, new LaneIndex { Value = laneIndex });
            state.EntityManager.AddComponentData(enemyEntity, new VerticalPosition { Value = battleConfig.EnemySpawnZ });
            state.EntityManager.AddComponentData(enemyEntity, new MoveSpeed { Value = enemyConfig.MoveSpeed });
            state.EntityManager.AddComponentData(enemyEntity, new EnemyHealth { Value = enemyConfig.Health });
            state.EntityManager.AddComponentData(enemyEntity, LocalTransform.FromPositionRotationScale(
                new float3(laneX, enemyConfig.Y, battleConfig.EnemySpawnZ),
                quaternion.identity,
                1f));
        }
    }
}
