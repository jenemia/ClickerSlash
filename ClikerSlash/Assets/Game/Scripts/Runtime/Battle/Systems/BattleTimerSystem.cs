using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 남은 작업 시간을 감소시키고 경과 작업 시간을 세션 통계에 기록합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerLaneMoveSystem))]
    public partial struct BattleTimerSystem : ISystem
    {
        /// <summary>
        /// 경과 시간을 일관되게 계산할 수 있도록 스테이지 타이머와 세션 통계가 모두 필요합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleSessionStatsState>();
            state.RequireForUpdate<RhythmPhaseState>();
        }

        /// <summary>
        /// 세션이 진행 중일 때 작업시간 카운트다운과 파생 작업 시간을 동기화합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingletonRW<StageProgressState>();
            if (stageProgress.ValueRO.IsFinished != 0)
            {
                return;
            }

            var stats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();
            var phaseState = SystemAPI.GetSingletonRW<RhythmPhaseState>();
            var elapsedWorkTime = stageProgress.ValueRO.ElapsedWorkTime + SystemAPI.Time.DeltaTime;
            var remainingWorkTime = Unity.Mathematics.math.max(0f, stageProgress.ValueRO.RemainingWorkTime - SystemAPI.Time.DeltaTime);

            stageProgress.ValueRW.ElapsedWorkTime = elapsedWorkTime;
            stageProgress.ValueRW.RemainingWorkTime = remainingWorkTime;
            stats.ValueRW.WorkedTimeSeconds = elapsedWorkTime;
            var runtimeSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            phaseState.ValueRW.CurrentPhase = runtimeSnapshot.CurrentPhase;
            phaseState.ValueRW.FocusedArea = runtimeSnapshot.FocusedArea;
            phaseState.ValueRW.PendingApprovalCount = runtimeSnapshot.PendingApprovalCount;
            phaseState.ValueRW.PendingRouteCount = runtimeSnapshot.PendingRouteCount;
            phaseState.ValueRW.PendingLoadingDockCount = runtimeSnapshot.PendingLoadingDockCount;
            phaseState.ValueRW.HasActiveCargo = runtimeSnapshot.HasActiveCargo ? (byte)1 : (byte)0;
            phaseState.ValueRW.HasActiveApprovalCargo = runtimeSnapshot.HasApprovalCargo ? (byte)1 : (byte)0;
            phaseState.ValueRW.HasActiveRouteCargo = runtimeSnapshot.HasRouteCargo ? (byte)1 : (byte)0;
        }
    }
}
