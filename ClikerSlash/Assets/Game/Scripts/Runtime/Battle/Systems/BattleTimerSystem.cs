using Unity.Entities;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LifeResolveSystem))]
    public partial struct BattleTimerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingletonRW<StageProgressState>();
            if (stageProgress.ValueRO.IsFinished != 0)
            {
                return;
            }

            stageProgress.ValueRW.RemainingTime = Unity.Mathematics.math.max(0f, stageProgress.ValueRO.RemainingTime - SystemAPI.Time.DeltaTime);
        }
    }
}
