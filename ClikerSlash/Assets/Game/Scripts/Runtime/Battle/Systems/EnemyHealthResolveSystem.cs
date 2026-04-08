using Unity.Collections;
using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 물류 처리 성공/실패 이벤트를 반영해 돈과 콤보 중심 세션 통계를 갱신합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoMissSystem))]
    public partial struct CargoRewardResolveSystem : ISystem
    {
        /// <summary>
        /// 이벤트와 플레이어/세션 상태가 준비된 뒤 결과 반영을 시작합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BattleSessionStatsState>();
        }

        /// <summary>
        /// 한 프레임 동안 발생한 처리 결과를 모아 돈, 콤보, 누적 통계를 갱신합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var comboState = SystemAPI.GetComponentRW<ComboState>(playerEntity);
            var sessionStats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();

            foreach (var (handledEvent, eventEntity) in SystemAPI.Query<RefRO<CargoHandledEvent>>().WithEntityAccess())
            {
                var nextCombo = comboState.ValueRO.Current + 1;
                var nextMaxCombo = Unity.Mathematics.math.max(comboState.ValueRO.Max, nextCombo);

                comboState.ValueRW.Current = nextCombo;
                comboState.ValueRW.Max = nextMaxCombo;
                sessionStats.ValueRW.TotalMoney += handledEvent.ValueRO.Reward;
                sessionStats.ValueRW.ProcessedCargoCount += 1;
                sessionStats.ValueRW.CurrentCombo = nextCombo;
                sessionStats.ValueRW.MaxCombo = nextMaxCombo;
                ecb.DestroyEntity(eventEntity);
            }

            foreach (var (missedEvent, eventEntity) in SystemAPI.Query<RefRO<CargoMissedEvent>>().WithEntityAccess())
            {
                comboState.ValueRW.Current = 0;
                sessionStats.ValueRW.TotalMoney -= missedEvent.ValueRO.Penalty;
                sessionStats.ValueRW.MissedCargoCount += 1;
                sessionStats.ValueRW.CurrentCombo = 0;
                ecb.DestroyEntity(eventEntity);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
