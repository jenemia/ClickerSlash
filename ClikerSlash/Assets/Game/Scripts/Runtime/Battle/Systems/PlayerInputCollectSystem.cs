using Unity.Entities;
using UnityEngine.InputSystem;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 키보드 입력을 현재 리듬 phase의 승인/레인선택 명령으로 변환합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerInputCollectSystem : ISystem
    {
        /// <summary>
        /// 입력을 읽기 전에 플레이어와 스테이지 상태가 존재하도록 요구합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 좌우 입력 한 번을 읽어 플레이어 이동 큐에 추가합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0 || PrototypeSessionRuntime.IsPauseMenuOpen)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var phase = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().CurrentPhase;
            if (phase == BattleMiniGamePhase.Approval)
            {
                if (keyboard.zKey.wasPressedThisFrame)
                {
                    PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Reject);
                }
                else if (keyboard.xKey.wasPressedThisFrame)
                {
                    PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
                }

                return;
            }

            if (phase != BattleMiniGamePhase.RouteSelection)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Air);
                return;
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Sea);
                return;
            }

            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Rail);
                return;
            }

            if (keyboard.digit4Key.wasPressedThisFrame)
            {
                PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Truck);
                return;
            }

            if (keyboard.digit5Key.wasPressedThisFrame)
            {
                PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Return);
            }
        }
    }
}
