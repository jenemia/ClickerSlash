using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 레인선택 phase에서 현재 박스의 승인 결과와 출력 라인 키 가이드를 보여줍니다.
    /// </summary>
    public sealed class RouteLaneSelectionPresenter : MonoBehaviour
    {
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            if (PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().CurrentPhase != BattleMiniGamePhase.RouteSelection)
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
