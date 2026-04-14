using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 레인선택 구역이 포커스일 때 현재 박스의 승인 결과와 출력 라인 키 가이드를 보여줍니다.
    /// </summary>
    public sealed class RouteLaneSelectionPresenter : MonoBehaviour
    {
        private GUIStyle _labelStyle;

        /// <summary>
        /// 레인선택 카메라를 보고 있을 때만 현재 라우팅 대상 박스 정보를 그립니다.
        /// </summary>
        private void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            if (PrototypeSessionRuntime.GetFocusedMiniGameArea() != BattleMiniGameArea.RouteSelection)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoMiniGamePhase>(),
                ComponentType.ReadOnly<CargoKind>(),
                ComponentType.ReadOnly<CargoWeight>(),
                ComponentType.ReadOnly<CargoApprovalDecision>(),
                ComponentType.ReadOnly<LocalTransform>());
            if (cargoQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var phases = cargoQuery.ToComponentDataArray<CargoMiniGamePhase>(Allocator.Temp);
            using var kinds = cargoQuery.ToComponentDataArray<CargoKind>(Allocator.Temp);
            using var weights = cargoQuery.ToComponentDataArray<CargoWeight>(Allocator.Temp);
            using var decisions = cargoQuery.ToComponentDataArray<CargoApprovalDecision>(Allocator.Temp);
            using var transforms = cargoQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (var index = 0; index < phases.Length; index += 1)
            {
                if (phases[index].Value != BattleMiniGamePhase.RouteSelection)
                {
                    continue;
                }

                EnsureStyles();
                GUILayout.BeginArea(new Rect(Screen.width - 430f, 24f, 390f, 260f), GUI.skin.box);
                GUILayout.Label("ROUTE SELECTION", _labelStyle);
                GUILayout.Label($"Cargo: {DescribeCargoKind(kinds[index].Value)} / {weights[index].Value}kg", _labelStyle);
                GUILayout.Label($"Approval: {decisions[index].Value}", _labelStyle);
                GUILayout.Label($"Lane Z: {transforms[index].Position.z:0.00}", _labelStyle);
                GUILayout.Label("1 Air / 2 Sea / 3 Rail / 4 Truck / 5 Return", _labelStyle);
                GUILayout.Label($"Delivery lanes max {PrototypeSessionRuntime.DefaultDeliveryLaneMaxWeight}kg", _labelStyle);
                GUILayout.EndArea();
                break;
            }
        }

        /// <summary>
        /// 물류 분류 enum을 HUD에 노출할 문자열로 바꿉니다.
        /// </summary>
        private static string DescribeCargoKind(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => "Fragile",
                LoadingDockCargoKind.Frozen => "Frozen",
                _ => "General"
            };
        }

        /// <summary>
        /// 라우팅 패널 전용 IMGUI 스타일을 지연 생성합니다.
        /// </summary>
        private void EnsureStyles()
        {
            _labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                wordWrap = true
            };
        }
    }
}
