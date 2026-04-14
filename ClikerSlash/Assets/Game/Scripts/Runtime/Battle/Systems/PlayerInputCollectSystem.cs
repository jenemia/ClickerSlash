using Unity.Entities;
using UnityEngine.InputSystem;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 공통 카메라 전환 입력과 포커스 구역 전용 판정 입력을 분리해서 수집합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerInputCollectSystem : ISystem
    {
        /// <summary>
        /// 입력을 읽기 전에 플레이어, 전투 설정, 스테이지 상태가 존재하도록 요구합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// Q/E는 항상 카메라 포커스를 바꾸고, 나머지 키는 현재 구역에만 전달합니다.
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

            // 카메라 전환은 어느 구역을 보고 있든 공통으로 처리합니다.
            if (keyboard.qKey.wasPressedThisFrame)
            {
                PrototypeSessionRuntime.FocusPreviousMiniGameArea();
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                PrototypeSessionRuntime.FocusNextMiniGameArea();
            }

            var focusedArea = PrototypeSessionRuntime.GetFocusedMiniGameArea();
            if (!HasJudgableCargoInFocusedArea(ref state, focusedArea))
            {
                return;
            }

            if (focusedArea == BattleMiniGameArea.Approval)
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

            if (focusedArea != BattleMiniGameArea.RouteSelection)
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

        /// <summary>
        /// 현재 포커스 구역에 판정선 근처까지 도달한 물류가 있을 때만 입력을 허용합니다.
        /// </summary>
        private bool HasJudgableCargoInFocusedArea(ref SystemState state, BattleMiniGameArea focusedArea)
        {
            if (focusedArea == BattleMiniGameArea.LoadingDock)
            {
                return false;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var expectedPhase = focusedArea == BattleMiniGameArea.Approval
                ? BattleMiniGamePhase.Approval
                : BattleMiniGamePhase.RouteSelection;

            foreach (var (cargoPhase, cargoTransform) in SystemAPI
                         .Query<RefRO<CargoMiniGamePhase>, RefRO<LocalTransform>>()
                         .WithAll<CargoTag>())
            {
                if (cargoPhase.ValueRO.Value != expectedPhase)
                {
                    continue;
                }

                // 판정선에 아직 들어오지 않은 선입력은 버퍼에 남기지 않고 무시합니다.
                var distance = Unity.Mathematics.math.abs(cargoTransform.ValueRO.Position.z - battleConfig.JudgmentLineZ);
                if (distance <= battleConfig.HandleWindowHalfDepth)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
