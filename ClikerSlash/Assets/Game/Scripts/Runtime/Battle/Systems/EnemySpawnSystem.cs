using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 세션이 진행 중일 때 주기적으로 임의 레인에 새 물류를 스폰합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleTimerSystem))]
    public partial struct CargoSpawnSystem : ISystem
    {
        /// <summary>
        /// 스폰을 시작하기 전에 세션 설정, 물류 설정, 레인 정보, 타이머 상태가 모두 필요합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<CargoConfig>();
            state.RequireForUpdate<LaneLayout>();
            state.RequireForUpdate<SpawnTimerState>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 스폰 타이머를 감소시키고 0이 되면 새 물류를 생성합니다.
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
            var cargoConfig = SystemAPI.GetSingleton<CargoConfig>();
            var laneLayout = SystemAPI.GetSingleton<LaneLayout>();
            var laneEntity = SystemAPI.GetSingletonEntity<LaneLayout>();
            var laneXs = state.EntityManager.GetBuffer<LaneWorldXElement>(laneEntity);
            var activeLaneCount = laneLayout.LaneCount;
            if (SystemAPI.HasSingleton<SessionRuleState>())
            {
                activeLaneCount = Unity.Mathematics.math.min(
                    activeLaneCount,
                    Unity.Mathematics.math.max(1, SystemAPI.GetSingleton<SessionRuleState>().ActiveLaneCount));
            }

            var spawnTimer = SystemAPI.GetSingletonRW<SpawnTimerState>();

            spawnTimer.ValueRW.Remaining -= deltaTime;
            if (spawnTimer.ValueRO.Remaining > 0f)
            {
                return;
            }

            spawnTimer.ValueRW.Remaining += battleConfig.SpawnInterval;
            var laneIndex = spawnTimer.ValueRW.Random.NextInt(0, activeLaneCount);
            var laneX = BattleLaneUtility.GetLaneX(laneXs, laneIndex);

            var cargoEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(cargoEntity, new CargoTag());
            state.EntityManager.AddComponentData(cargoEntity, new LaneIndex { Value = laneIndex });
            state.EntityManager.AddComponentData(cargoEntity, new VerticalPosition { Value = battleConfig.CargoSpawnZ });
            state.EntityManager.AddComponentData(cargoEntity, new MoveSpeed { Value = cargoConfig.MoveSpeed });
            state.EntityManager.AddComponentData(cargoEntity, new CargoWeight { Value = cargoConfig.Weight });
            state.EntityManager.AddComponentData(cargoEntity, new CargoReward { Value = cargoConfig.Reward });
            state.EntityManager.AddComponentData(cargoEntity, new CargoPenalty { Value = cargoConfig.Penalty });
            state.EntityManager.AddComponentData(cargoEntity, LocalTransform.FromPositionRotationScale(
                new float3(laneX, cargoConfig.Y, battleConfig.CargoSpawnZ),
                quaternion.identity,
                1f));
        }
    }
}
