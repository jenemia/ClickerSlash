using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 플레이어를 레인 사이로 보간 이동시키고, 정지 시에는 레인 기준점에 다시 맞춰 둡니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerLaneQueueSystem))]
    public partial struct PlayerLaneMoveSystem : ISystem
    {
        /// <summary>
        /// 플레이어 설정, 레인 레이아웃, 스테이지 상태가 준비된 뒤에만 동작합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerConfig>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<LaneLayout>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 현재 진행 중인 레인 전환을 진행시키거나, 정지 상태라면 플레이어를 현재 레인 위치에 맞춥니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var isMovementLocked = PrototypeSessionRuntime.IsLaneMovementLocked();
            var deltaTime = SystemAPI.Time.DeltaTime;
            var playerConfig = SystemAPI.GetSingleton<PlayerConfig>();
            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var laneEntity = SystemAPI.GetSingletonEntity<LaneLayout>();
            var laneXs = state.EntityManager.GetBuffer<LaneWorldXElement>(laneEntity);

            foreach (var (laneIndex, moveState, transform) in SystemAPI
                         .Query<RefRW<LaneIndex>, RefRW<LaneMoveState>, RefRW<LocalTransform>>()
                         .WithAll<PlayerTag>())
            {
                if (isMovementLocked)
                {
                    var currentLaneX = BattleLaneUtility.GetLaneX(laneXs, laneIndex.ValueRO.Value);
                    moveState.ValueRW.StartLane = laneIndex.ValueRO.Value;
                    moveState.ValueRW.TargetLane = laneIndex.ValueRO.Value;
                    moveState.ValueRW.Progress = 0f;
                    moveState.ValueRW.IsMoving = 0;
                    transform.ValueRW.Position = new float3(currentLaneX, playerConfig.Y, playerConfig.Z);
                    continue;
                }

                if (moveState.ValueRO.IsMoving == 0)
                {
                    // 정지 프레임에도 기준 레인 위치에 다시 맞춰 두어 드리프트를 막습니다.
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

                // 선형 보간을 사용해 고정 이동 시간을 유지하면서도 읽기 쉬운 레인 이동을 만듭니다.
                transform.ValueRW.Position = new float3(
                    math.lerp(startX, targetX, progress),
                    playerConfig.Y,
                    playerConfig.Z);

                if (progress < 1f)
                {
                    continue;
                }

                // 보간이 끝나면 새 레인 인덱스를 확정하고 이동 중 플래그를 내립니다.
                laneIndex.ValueRW.Value = moveState.ValueRO.TargetLane;
                moveState.ValueRW.Progress = 0f;
                moveState.ValueRW.IsMoving = 0;
                transform.ValueRW.Position = new float3(targetX, playerConfig.Y, playerConfig.Z);
            }
        }
    }
}
