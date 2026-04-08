using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerLaneQueueSystem))]
    public partial struct PlayerLaneMoveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerConfig>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<LaneLayout>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var playerConfig = SystemAPI.GetSingleton<PlayerConfig>();
            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var laneEntity = SystemAPI.GetSingletonEntity<LaneLayout>();
            var laneXs = state.EntityManager.GetBuffer<LaneWorldXElement>(laneEntity);

            foreach (var (laneIndex, moveState, transform) in SystemAPI
                         .Query<RefRW<LaneIndex>, RefRW<LaneMoveState>, RefRW<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                if (moveState.ValueRO.IsMoving == 0)
                {
                    transform.ValueRW.Position = new float3(
                        BattleLaneUtility.GetLaneX(laneXs, laneIndex.ValueRO.Value),
                        playerConfig.Y,
                        playerConfig.Z);
                    continue;
                }

                moveState.ValueRW.Progress += deltaTime / math.max(0.01f, battleConfig.PlayerMoveDuration);
                var progress = math.saturate(moveState.ValueRO.Progress);
                var startX = BattleLaneUtility.GetLaneX(laneXs, moveState.ValueRO.StartLane);
                var targetX = BattleLaneUtility.GetLaneX(laneXs, moveState.ValueRO.TargetLane);

                transform.ValueRW.Position = new float3(
                    math.lerp(startX, targetX, progress),
                    playerConfig.Y,
                    playerConfig.Z);

                if (progress < 1f)
                {
                    continue;
                }

                laneIndex.ValueRW.Value = moveState.ValueRO.TargetLane;
                moveState.ValueRW.Progress = 0f;
                moveState.ValueRW.IsMoving = 0;
                transform.ValueRW.Position = new float3(targetX, playerConfig.Y, playerConfig.Z);
            }
        }
    }
}
