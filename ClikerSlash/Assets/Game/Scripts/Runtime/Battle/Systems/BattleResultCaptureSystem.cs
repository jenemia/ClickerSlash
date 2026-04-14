using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 세션이 끝난 뒤 최종 요약 정보를 캡처해 씬 전환용 데이터로 저장합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleEndCheckSystem))]
    public partial struct BattleResultCaptureSystem : ISystem
    {
        /// <summary>
        /// 세션 결과 상태와 통계 상태가 모두 준비된 뒤에만 동작합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleOutcomeState>();
            state.RequireForUpdate<BattleSessionStatsState>();
        }

        /// <summary>
        /// 최종 런타임 값을 세션 통계 싱글턴과 씬 간 공유용 런타임 스냅샷에 복사합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingleton<StageProgressState>();
            var outcome = SystemAPI.GetSingleton<BattleOutcomeState>();
            if (stageProgress.IsFinished == 0 || outcome.HasOutcome == 0)
            {
                return;
            }

            var sessionStats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();
            if (sessionStats.ValueRO.HasSnapshot != 0)
            {
                return;
            }

            sessionStats.ValueRW.WorkedTimeSeconds = stageProgress.ElapsedWorkTime;
            sessionStats.ValueRW.HasSnapshot = 1;

            PrototypeSessionRuntime.StoreBattleResult(new BattleResultSnapshot
            {
                TotalMoney = sessionStats.ValueRO.TotalMoney,
                ProcessedCargoCount = sessionStats.ValueRO.ProcessedCargoCount,
                MissedCargoCount = sessionStats.ValueRO.MissedCargoCount,
                CurrentCombo = sessionStats.ValueRO.CurrentCombo,
                MaxCombo = sessionStats.ValueRO.MaxCombo,
                WorkedTimeSeconds = stageProgress.ElapsedWorkTime,
                ApprovedCargoCount = sessionStats.ValueRO.ApprovedCargoCount,
                RejectedCargoCount = sessionStats.ValueRO.RejectedCargoCount,
                CorrectRouteCount = sessionStats.ValueRO.CorrectRouteCount,
                MisrouteCount = sessionStats.ValueRO.MisrouteCount,
                ReturnCount = sessionStats.ValueRO.ReturnCount
            });
        }
    }
}
