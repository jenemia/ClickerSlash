using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 승인 phase에서 현재 박스의 스티커와 저울 정보를 화면 우측에 표시합니다.
    /// </summary>
    public sealed class ApprovalMiniGamePresenter : MonoBehaviour
    {
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            if (PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().CurrentPhase != BattleMiniGamePhase.Approval)
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

        private static string DescribeCargoKind(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => "Fragile",
                LoadingDockCargoKind.Frozen => "Frozen",
                _ => "General"
            };
        }

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
