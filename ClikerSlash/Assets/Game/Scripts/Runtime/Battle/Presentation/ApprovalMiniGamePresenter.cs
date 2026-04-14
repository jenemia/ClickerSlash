using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 승인 구역이 포커스일 때 현재 박스의 스티커와 저울 정보를 화면 우측에 표시합니다.
    /// </summary>
    public sealed class ApprovalMiniGamePresenter : MonoBehaviour
    {
        private GUIStyle _labelStyle;

        /// <summary>
        /// 승인 카메라를 보고 있을 때만 현재 승인 대상 박스 정보를 그립니다.
        /// </summary>
        private void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            if (PrototypeSessionRuntime.GetFocusedMiniGameArea() != BattleMiniGameArea.Approval)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoMiniGamePhase>(),
                ComponentType.ReadOnly<CargoKind>(),
                ComponentType.ReadOnly<CargoWeight>(),
                ComponentType.ReadOnly<LocalTransform>());
            if (cargoQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var phases = cargoQuery.ToComponentDataArray<CargoMiniGamePhase>(Allocator.Temp);
            using var kinds = cargoQuery.ToComponentDataArray<CargoKind>(Allocator.Temp);
            using var weights = cargoQuery.ToComponentDataArray<CargoWeight>(Allocator.Temp);
            using var transforms = cargoQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (var index = 0; index < phases.Length; index += 1)
            {
                if (phases[index].Value != BattleMiniGamePhase.Approval)
                {
                    continue;
                }

                EnsureStyles();
                GUILayout.BeginArea(new Rect(Screen.width - 400f, 24f, 360f, 220f), GUI.skin.box);
                GUILayout.Label("APPROVAL", _labelStyle);
                GUILayout.Label($"Sticker: {DescribeCargoKind(kinds[index].Value)}", _labelStyle);
                GUILayout.Label($"Scale: {weights[index].Value}kg", _labelStyle);
                GUILayout.Label($"Shipping Cutoff: {PrototypeSessionRuntime.DefaultDeliveryLaneMaxWeight}kg", _labelStyle);
                GUILayout.Label($"Lane Z: {transforms[index].Position.z:0.00}", _labelStyle);
                GUILayout.Label("Input: Z Reject / X Approve", _labelStyle);
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
        /// 승인 패널 전용 IMGUI 스타일을 지연 생성합니다.
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
