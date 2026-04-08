using Unity.Entities;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerInputCollectSystem))]
    public partial struct PlayerLaneQueueSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<LaneLayout>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var laneCount = SystemAPI.GetSingleton<LaneLayout>().LaneCount;

            foreach (var (moveState, laneIndex, moveCommands) in SystemAPI
                         .Query<RefRW<LaneMoveState>, RefRW<LaneIndex>, DynamicBuffer<LaneMoveCommandBufferElement>>()
                         .WithAll<PlayerTag>())
            {
                if (moveState.ValueRO.IsMoving != 0 || moveCommands.Length == 0)
                {
                    continue;
                }

                var nextCommand = moveCommands[0];
                moveCommands.RemoveAt(0);

                var targetLane = BattleLaneUtility.ClampLane(laneIndex.ValueRO.Value + nextCommand.Direction, laneCount);
                if (targetLane == laneIndex.ValueRO.Value)
                {
                    continue;
                }

                moveState.ValueRW.StartLane = laneIndex.ValueRO.Value;
                moveState.ValueRW.TargetLane = targetLane;
                moveState.ValueRW.Progress = 0f;
                moveState.ValueRW.IsMoving = 1;
            }
        }
    }
}
