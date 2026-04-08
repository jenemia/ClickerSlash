using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 전투가 진행 중일 때 주기적으로 임의 레인에 새 적을 스폰합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerLaneMoveSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        /// <summary>
        /// 스폰을 시작하기 전에 전투 설정, 적 설정, 레인 정보, 타이머 상태가 모두 필요합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<EnemyConfig>();
            state.RequireForUpdate<LaneLayout>();
            state.RequireForUpdate<SpawnTimerState>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 스폰 타이머를 감소시키고 0이 되면 새 적을 생성합니다.
        /// </summary>
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

            // 늦은 프레임에서도 다음 스폰 주기가 밀리지 않도록 적 생성 전에 타이머를 먼저 되돌립니다.
            spawnTimer.ValueRW.Remaining += battleConfig.SpawnInterval;
            var laneIndex = spawnTimer.ValueRW.Random.NextInt(0, laneLayout.LaneCount);
            var laneX = BattleLaneUtility.GetLaneX(laneXs, laneIndex);

            // 생성된 적은 이동, 타기팅, 프레젠테이션 동기화에 필요한 최소 ECS 데이터만 가집니다.
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
