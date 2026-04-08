using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 플레이어 라이프가 모두 소진되거나 타이머가 0이 되면 전투 결과를 확정합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleTimerSystem))]
    public partial struct BattleEndCheckSystem : ISystem
    {
        /// <summary>
        /// 전투 종료 조건을 평가하기 전에 전역 타이머, 결과 상태, 플레이어 엔티티가 존재하도록 요구합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleOutcomeState>();
            state.RequireForUpdate<PlayerTag>();
        }

        /// <summary>
        /// 시간 종료와 라이프 소진이 동시에 일어날 수 있으므로 승리보다 패배를 먼저 검사합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingletonRW<StageProgressState>();
            if (stageProgress.ValueRO.IsFinished != 0)
            {
                return;
            }

            var outcome = SystemAPI.GetSingletonRW<BattleOutcomeState>();
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var life = SystemAPI.GetComponent<LifeState>(playerEntity).Value;

            if (life <= 0)
            {
                // 방어선 돌파로 라이프가 모두 사라지면 즉시 패배로 확정합니다.
                stageProgress.ValueRW.IsFinished = 1;
                outcome.ValueRW.HasOutcome = 1;
                outcome.ValueRW.IsVictory = 0;
                return;
            }

            if (stageProgress.ValueRO.RemainingTime > 0f)
            {
                return;
            }

            // 타이머가 끝날 때까지 생존하면 프로토타입 기준 승리입니다.
            stageProgress.ValueRW.IsFinished = 1;
            outcome.ValueRW.HasOutcome = 1;
            outcome.ValueRW.IsVictory = 1;
        }
    }
}
