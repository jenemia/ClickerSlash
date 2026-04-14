using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 현재 포커스된 컨베이어 구역만 플레이어 입력을 소비해 물류 판정을 확정합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoMoveSystem))]
    public partial struct CargoHandleSystem : ISystem
    {
        /// <summary>
        /// 판정 전에 전투 설정, 세션 통계, 구역 상태가 모두 준비되도록 요구합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<BattleSessionStatsState>();
            state.RequireForUpdate<RhythmPhaseState>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 포커스된 구역의 판정선 안에 들어온 물류 하나만 처리하고 다음 프레임으로 넘깁니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var focusedArea = PrototypeSessionRuntime.GetFocusedMiniGameArea();
            if (focusedArea == BattleMiniGameArea.LoadingDock)
            {
                return;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var stats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();
            var rhythmState = SystemAPI.GetSingletonRW<RhythmPhaseState>();
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (cargoPhase, cargoKind, cargoWeight, cargoReward, cargoPenalty, cargoApprovalDecision, cargoTransform, cargoEntity) in SystemAPI
                         .Query<RefRO<CargoMiniGamePhase>, RefRO<CargoKind>, RefRO<CargoWeight>, RefRO<CargoReward>, RefRO<CargoPenalty>, RefRO<CargoApprovalDecision>, RefRO<LocalTransform>>()
                         .WithAll<CargoTag>()
                         .WithEntityAccess())
            {
                if (!IsFocusedCargo(focusedArea, cargoPhase.ValueRO.Value))
                {
                    continue;
                }

                var distance = math.abs(cargoTransform.ValueRO.Position.z - battleConfig.JudgmentLineZ);
                if (distance > battleConfig.HandleWindowHalfDepth)
                {
                    continue;
                }

                var cargoEntryId = state.EntityManager.GetComponentData<CargoEntryId>(cargoEntity).Value;
                if (focusedArea == BattleMiniGameArea.Approval)
                {
                    if (!PrototypeSessionRuntime.TryConsumeApprovalInput(out var approvalDecision))
                    {
                        continue;
                    }

                    // 승인 결과는 즉시 레인선택 큐로 넘겨 다음 구역 스폰을 준비합니다.
                    PrototypeSessionRuntime.EnqueueRouteSelectionCargo(new ApprovalCargoSnapshot
                    {
                        EntryId = cargoEntryId,
                        Kind = cargoKind.ValueRO.Value,
                        Weight = cargoWeight.ValueRO.Value,
                        Reward = cargoReward.ValueRO.Value,
                        Penalty = cargoPenalty.ValueRO.Value
                    }, approvalDecision);

                    if (approvalDecision == ApprovalDecision.Approve)
                    {
                        stats.ValueRW.ApprovedCargoCount += 1;
                    }
                    else
                    {
                        stats.ValueRW.RejectedCargoCount += 1;
                    }

                    ecb.DestroyEntity(cargoEntity);
                    PrototypeSessionRuntime.NotifyAreaCargoResolved(BattleMiniGameArea.Approval);
                    UpdateRhythmPhaseState(ref rhythmState.ValueRW);
                    break;
                }

                if (!PrototypeSessionRuntime.TryConsumeRouteInput(out var selectedRoute))
                {
                    continue;
                }

                var routeCargo = new RouteSelectionCargoSnapshot
                {
                    EntryId = cargoEntryId,
                    Kind = cargoKind.ValueRO.Value,
                    Weight = cargoWeight.ValueRO.Value,
                    Reward = cargoReward.ValueRO.Value,
                    Penalty = cargoPenalty.ValueRO.Value,
                    ApprovalDecision = cargoApprovalDecision.ValueRO.Value,
                    IsDeliverable = PrototypeSessionRuntime.IsCargoDeliverable(cargoWeight.ValueRO.Value)
                };

                PrototypeSessionRuntime.ResolveRouteOutcome(
                    routeCargo,
                    selectedRoute,
                    out var incomeDelta,
                    out var countsAsCorrectRoute,
                    out var countsAsReturn,
                    out var countsAsMisroute);

                // 반송이 아닌 출력 레인으로 나간 박스는 상하차 구역 큐로 이어서 넘깁니다.
                if (selectedRoute != CargoRouteLane.Return)
                {
                    PrototypeSessionRuntime.EnqueueLoadingDockCargo(
                        cargoEntryId,
                        cargoKind.ValueRO.Value,
                        cargoWeight.ValueRO.Value);
                }

                stats.ValueRW.TotalMoney += incomeDelta;
                stats.ValueRW.ProcessedCargoCount += countsAsCorrectRoute ? 1 : 0;
                stats.ValueRW.CorrectRouteCount += countsAsCorrectRoute ? 1 : 0;
                stats.ValueRW.ReturnCount += countsAsReturn ? 1 : 0;
                stats.ValueRW.MisrouteCount += countsAsMisroute ? 1 : 0;
                stats.ValueRW.CurrentCombo = countsAsCorrectRoute ? stats.ValueRO.CurrentCombo + 1 : 0;
                stats.ValueRW.MaxCombo = math.max(stats.ValueRO.MaxCombo, stats.ValueRW.CurrentCombo);

                ecb.DestroyEntity(cargoEntity);
                PrototypeSessionRuntime.NotifyAreaCargoResolved(BattleMiniGameArea.RouteSelection);
                UpdateRhythmPhaseState(ref rhythmState.ValueRW);
                break;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// 현재 포커스 구역과 물류 엔티티의 구역 값이 일치하는지 판정합니다.
        /// </summary>
        private static bool IsFocusedCargo(BattleMiniGameArea focusedArea, BattleMiniGamePhase cargoPhase)
        {
            return focusedArea switch
            {
                BattleMiniGameArea.Approval => cargoPhase == BattleMiniGamePhase.Approval,
                BattleMiniGameArea.RouteSelection => cargoPhase == BattleMiniGamePhase.RouteSelection,
                _ => false
            };
        }

        /// <summary>
        /// 판정 결과가 반영된 뒤 HUD용 리듬 상태 싱글턴을 최신 스냅샷으로 덮어씁니다.
        /// </summary>
        private static void UpdateRhythmPhaseState(ref RhythmPhaseState rhythmPhaseState)
        {
            var runtimeSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            rhythmPhaseState.CurrentPhase = runtimeSnapshot.CurrentPhase;
            rhythmPhaseState.FocusedArea = runtimeSnapshot.FocusedArea;
            rhythmPhaseState.PendingApprovalCount = runtimeSnapshot.PendingApprovalCount;
            rhythmPhaseState.PendingRouteCount = runtimeSnapshot.PendingRouteCount;
            rhythmPhaseState.PendingLoadingDockCount = runtimeSnapshot.PendingLoadingDockCount;
            rhythmPhaseState.HasActiveCargo = runtimeSnapshot.HasActiveCargo ? (byte)1 : (byte)0;
            rhythmPhaseState.HasActiveApprovalCargo = runtimeSnapshot.HasApprovalCargo ? (byte)1 : (byte)0;
            rhythmPhaseState.HasActiveRouteCargo = runtimeSnapshot.HasRouteCargo ? (byte)1 : (byte)0;
        }
    }

    /// <summary>
    /// 레거시 로봇 처리 시스템은 새 3구역 수동 판정 구조에서 비활성 더미로 유지합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoHandleSystem))]
    public partial struct LaneRobotHandleSystem : ISystem
    {
        /// <summary>
        /// 현재는 추후 자동 판정 봇 확장 포인트만 남겨 둡니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
        }

        /// <summary>
        /// 봇 판정 기능이 들어오기 전까지는 아무 동작도 하지 않습니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
