using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 버퍼에 쌓인 이동 명령을 한 번에 하나씩 꺼내 실제 레인 전환 상태로 바꿉니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerInputCollectSystem))]
    public partial struct PlayerLaneQueueSystem : ISystem
    {
        /// <summary>
        /// 플레이어 이동 큐, 레인 레이아웃, 스테이지 상태가 준비될 때까지 대기합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<LaneLayout>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 플레이어가 정지 상태일 때만 다음 큐 명령으로 새 레인 이동을 시작합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var isMovementLocked = PrototypeSessionRuntime.IsLaneMovementLocked();

            var laneLayout = SystemAPI.GetSingleton<LaneLayout>();
            var activeLaneStartIndex = 0;
            var activeLaneCount = laneLayout.LaneCount;
            if (SystemAPI.HasSingleton<SessionRuleState>())
            {
                var sessionRules = SystemAPI.GetSingleton<SessionRuleState>();
                activeLaneStartIndex = sessionRules.ActiveLaneStartIndex;
                activeLaneCount = Unity.Mathematics.math.min(
                    laneLayout.LaneCount,
                    Unity.Mathematics.math.max(1, sessionRules.ActiveLaneCount));
            }

            foreach (var (moveState, laneIndex, moveCommands) in SystemAPI
                         .Query<RefRW<LaneMoveState>, RefRW<LaneIndex>, DynamicBuffer<LaneMoveCommandBufferElement>>()
                         .WithAll<PlayerTag>())
            {
                if (isMovementLocked)
                {
                    moveCommands.Clear();
                    continue;
                }

                // 이전 이동이 아직 끝나지 않았다면 새 큐 명령을 시작하지 않습니다.
                if (moveState.ValueRO.IsMoving != 0 || moveCommands.Length == 0)
                {
                    continue;
                }

                var nextCommand = moveCommands[0];
                moveCommands.RemoveAt(0);

                var targetLane = BattleLaneUtility.ClampLaneToActiveRange(
                    laneIndex.ValueRO.Value + nextCommand.Direction,
                    activeLaneStartIndex,
                    activeLaneCount,
                    laneLayout.LaneCount);
                if (targetLane == laneIndex.ValueRO.Value)
                {
                    // 레인 범위를 벗어나는 명령은 소비만 하고 무시합니다.
                    continue;
                }

                // 현재 레인에서 다음 목표 레인으로 향하는 새 보간 이동을 초기화합니다.
                moveState.ValueRW.StartLane = laneIndex.ValueRO.Value;
                moveState.ValueRW.TargetLane = targetLane;
                moveState.ValueRW.Progress = 0f;
                moveState.ValueRW.IsMoving = 1;
            }
        }
    }
}
