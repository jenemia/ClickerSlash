using Unity.Entities;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleTimerSystem))]
    public partial struct BattleEndCheckSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<BattleOutcomeState>();
            state.RequireForUpdate<PlayerTag>();
        }

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
                stageProgress.ValueRW.IsFinished = 1;
                outcome.ValueRW.HasOutcome = 1;
                outcome.ValueRW.IsVictory = 0;
                return;
            }

            if (stageProgress.ValueRO.RemainingTime > 0f)
            {
                return;
            }

            stageProgress.ValueRW.IsFinished = 1;
            outcome.ValueRW.HasOutcome = 1;
            outcome.ValueRW.IsVictory = 1;
        }
    }
}
