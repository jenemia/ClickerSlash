using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 현재 활성 물류가 판단 구간에 들어왔을 때 승인/레인선택 입력을 소비합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoMoveSystem))]
    public partial struct CargoHandleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<BattleSessionStatsState>();
            state.RequireForUpdate<RhythmPhaseState>();
            state.RequireForUpdate<StageProgressState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var rhythmPhase = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().CurrentPhase;
            var stats = SystemAPI.GetSingletonRW<BattleSessionStatsState>();
            var rhythmState = SystemAPI.GetSingletonRW<RhythmPhaseState>();
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (cargoPhase, cargoKind, cargoWeight, cargoReward, cargoPenalty, cargoApprovalDecision, cargoTransform, cargoEntity) in SystemAPI
                         .Query<RefRO<CargoMiniGamePhase>, RefRO<CargoKind>, RefRO<CargoWeight>, RefRO<CargoReward>, RefRO<CargoPenalty>, RefRO<CargoApprovalDecision>, RefRO<LocalTransform>>()
                         .WithAll<CargoTag>()
                         .WithEntityAccess())
            {
                var cargoEntryId = state.EntityManager.GetComponentData<CargoEntryId>(cargoEntity).Value;

                if (cargoPhase.ValueRO.Value != rhythmPhase)
                {
                    continue;
                }

                var distance = math.abs(cargoTransform.ValueRO.Position.z - battleConfig.JudgmentLineZ);
                if (distance > battleConfig.HandleWindowHalfDepth)
                {
                    continue;
                }

                if (rhythmPhase == BattleMiniGamePhase.Approval)
                {
                    if (!PrototypeSessionRuntime.TryConsumeApprovalInput(out var approvalDecision))
                    {
                        continue;
                    }

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
                    PrototypeSessionRuntime.NotifyActiveCargoResolved();
                    UpdateRhythmPhaseState(ref rhythmState.ValueRW);
                    break;
                }

                if (!PrototypeSessionRuntime.TryConsumeRouteInput(out var selectedRoute))
                {
                    continue;
                }

                PrototypeSessionRuntime.ResolveRouteOutcome(new RouteSelectionCargoSnapshot
                {
                    EntryId = cargoEntryId,
                    Kind = cargoKind.ValueRO.Value,
                    Weight = cargoWeight.ValueRO.Value,
                    Reward = cargoReward.ValueRO.Value,
                    Penalty = cargoPenalty.ValueRO.Value,
                    ApprovalDecision = cargoApprovalDecision.ValueRO.Value,
                    IsDeliverable = PrototypeSessionRuntime.IsCargoDeliverable(cargoWeight.ValueRO.Value)
                }, selectedRoute, out var incomeDelta, out var countsAsCorrectRoute, out var countsAsReturn, out var countsAsMisroute);

                stats.ValueRW.TotalMoney += incomeDelta;
                stats.ValueRW.ProcessedCargoCount += countsAsCorrectRoute ? 1 : 0;
                stats.ValueRW.CorrectRouteCount += countsAsCorrectRoute ? 1 : 0;
                stats.ValueRW.ReturnCount += countsAsReturn ? 1 : 0;
                stats.ValueRW.MisrouteCount += countsAsMisroute ? 1 : 0;
                stats.ValueRW.CurrentCombo = countsAsCorrectRoute ? stats.ValueRO.CurrentCombo + 1 : 0;
                stats.ValueRW.MaxCombo = math.max(stats.ValueRO.MaxCombo, stats.ValueRW.CurrentCombo);

                ecb.DestroyEntity(cargoEntity);
                PrototypeSessionRuntime.NotifyActiveCargoResolved();
                UpdateRhythmPhaseState(ref rhythmState.ValueRW);
                break;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private static void UpdateRhythmPhaseState(ref RhythmPhaseState rhythmPhaseState)
        {
            var runtimeSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            rhythmPhaseState.CurrentPhase = runtimeSnapshot.CurrentPhase;
            rhythmPhaseState.PendingApprovalCount = runtimeSnapshot.PendingApprovalCount;
            rhythmPhaseState.PendingRouteCount = runtimeSnapshot.PendingRouteCount;
            rhythmPhaseState.HasActiveCargo = runtimeSnapshot.HasActiveCargo ? (byte)1 : (byte)0;
        }
    }

    /// <summary>
    /// 레거시 로봇 처리 시스템은 새 2단계 리듬 루프에서 사용하지 않습니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoHandleSystem))]
    public partial struct LaneRobotHandleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
