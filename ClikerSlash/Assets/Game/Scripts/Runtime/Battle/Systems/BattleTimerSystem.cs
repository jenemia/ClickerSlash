using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 전투 시간을 감소시키고 경과한 생존 시간을 세션 통계에 기록합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifeResolveSystem))]
    public partial struct BattleTimerSystem : ISystem
    {
        /// <summary>
        /// 경과 시간을 일관되게 계산할 수 있도록 스테이지 타이머와 전투 설정이 모두 필요합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<BattleSessionStatsState>();
        }

        /// <summary>
        /// 전투가 진행 중일 때 카운트다운을 갱신하고 파생 생존 시간을 동기화합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingletonRW<StageProgressState>();
            if (stageProgress.ValueRO.IsFinished != 0)
            {
                return;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var stats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();
            var remainingTime = Unity.Mathematics.math.max(0f, stageProgress.ValueRO.RemainingTime - SystemAPI.Time.DeltaTime);
            stageProgress.ValueRW.RemainingTime = remainingTime;
            // 결과 화면에는 남은 시간보다 경과 생존 시간이 더 직접적으로 쓰이므로 경과 시간 값으로 저장합니다.
            stats.ValueRW.SurvivalTimeSeconds = battleConfig.BattleDurationSeconds - remainingTime;
        }
    }
}
