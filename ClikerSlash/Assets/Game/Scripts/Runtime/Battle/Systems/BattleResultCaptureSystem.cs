using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 전투가 끝난 뒤 최종 요약 정보를 캡처해 씬 전환용 데이터로 저장합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleEndCheckSystem))]
    public partial struct BattleResultCaptureSystem : ISystem
    {
        /// <summary>
        /// 전투 결과 상태와 세션 통계 상태가 모두 준비된 뒤에만 동작합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleOutcomeState>();
            state.RequireForUpdate<BattleSessionStatsState>();
            state.RequireForUpdate<PlayerTag>();
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
                // 허브가 항상 안정적인 결과를 보도록 스냅샷은 한 번만 캡처합니다.
                return;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var comboState = SystemAPI.GetComponent<ComboState>(playerEntity);
            var lifeState = SystemAPI.GetComponent<LifeState>(playerEntity);
            var survivalTime = battleConfig.BattleDurationSeconds - stageProgress.RemainingTime;
            var killCount = sessionStats.ValueRO.KillCount;
            var currentCombo = comboState.Current;
            var maxCombo = comboState.Max;
            var remainingLives = lifeState.Value;

            sessionStats.ValueRW.CurrentCombo = currentCombo;
            sessionStats.ValueRW.MaxCombo = maxCombo;
            sessionStats.ValueRW.RemainingLives = remainingLives;
            sessionStats.ValueRW.SurvivalTimeSeconds = survivalTime;
            sessionStats.ValueRW.HasSnapshot = 1;
            sessionStats.ValueRW.IsVictory = outcome.IsVictory;

            // 씬 전환 뒤에도 읽을 수 있도록 ECS 요약을 관리형 런타임 오브젝트로 복제합니다.
            PrototypeSessionRuntime.StoreBattleResult(new BattleResultSnapshot
            {
                IsVictory = outcome.IsVictory,
                KillCount = killCount,
                CurrentCombo = currentCombo,
                MaxCombo = maxCombo,
                SurvivalTimeSeconds = survivalTime,
                RemainingLives = remainingLives
            });
        }
    }
}
