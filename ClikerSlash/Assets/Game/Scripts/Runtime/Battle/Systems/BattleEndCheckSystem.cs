using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 작업 시간이 모두 소진되면 세션 결과를 확정합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoRewardResolveSystem))]
    public partial struct BattleEndCheckSystem : ISystem
    {
        /// <summary>
        /// 세션 종료 조건을 평가하기 전에 전역 타이머와 결과 상태가 존재하도록 요구합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleOutcomeState>();
        }

        /// <summary>
        /// 남은 작업시간이 0이 되면 결과를 한 번만 확정합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingletonRW<StageProgressState>();
            if (stageProgress.ValueRO.IsFinished != 0)
            {
                return;
            }

            if (stageProgress.ValueRO.RemainingWorkTime > 0f)
            {
                return;
            }

            var outcome = SystemAPI.GetSingletonRW<BattleOutcomeState>();
            stageProgress.ValueRW.IsFinished = 1;
            outcome.ValueRW.HasOutcome = 1;
        }
    }
}
