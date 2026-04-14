using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 현재 phase에 맞는 단일 활성 물류를 승인/레인선택 컨베이어 위에 스폰합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleTimerSystem))]
    public partial struct CargoSpawnSystem : ISystem
    {
        /// <summary>
        /// 스폰을 시작하기 전에 세션 설정, 물류 설정, 레인 정보, 타이머 상태가 모두 필요합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<SpawnTimerState>();
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<RhythmPhaseState>();
        }

        /// <summary>
        /// 스폰 타이머를 감소시키고 0이 되면 새 물류를 생성합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var stageProgress = SystemAPI.GetSingleton<StageProgressState>();
            if (stageProgress.IsFinished != 0)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var spawnTimer = SystemAPI.GetSingletonRW<SpawnTimerState>();

            spawnTimer.ValueRW.Remaining -= deltaTime;
            if (spawnTimer.ValueRO.Remaining > 0f)
            {
                ApplyPhaseSnapshot(ref state);
                return;
            }

            if (!state.EntityManager.CreateEntityQuery(typeof(CargoTag)).IsEmptyIgnoreFilter)
            {
                return;
            }

            spawnTimer.ValueRW.Remaining += battleConfig.SpawnInterval;
            if (!PrototypeSessionRuntime.TryDequeueNextPhaseCargo(out var phase, out var approvalCargo, out var routeCargo))
            {
                ApplyPhaseSnapshot(ref state);
                return;
            }

            var cargoEntity = state.EntityManager.CreateEntity();
            var spawnedCargo = phase == BattleMiniGamePhase.Approval
                ? approvalCargo
                : new ApprovalCargoSnapshot
                {
                    EntryId = routeCargo.EntryId,
                    Kind = routeCargo.Kind,
                    Weight = routeCargo.Weight,
                    Reward = routeCargo.Reward,
                    Penalty = routeCargo.Penalty
                };
            var laneX = phase == BattleMiniGamePhase.Approval
                ? battleConfig.ApprovalLaneX
                : battleConfig.RouteLaneX;

            state.EntityManager.AddComponentData(cargoEntity, new CargoTag());
            state.EntityManager.AddComponentData(cargoEntity, new CargoEntryId { Value = spawnedCargo.EntryId });
            state.EntityManager.AddComponentData(cargoEntity, new CargoKind { Value = spawnedCargo.Kind });
            state.EntityManager.AddComponentData(cargoEntity, new CargoMiniGamePhase { Value = phase });
            state.EntityManager.AddComponentData(cargoEntity, new CargoApprovalDecision
            {
                Value = phase == BattleMiniGamePhase.RouteSelection ? routeCargo.ApprovalDecision : ApprovalDecision.None
            });
            state.EntityManager.AddComponentData(cargoEntity, new LaneIndex { Value = 0 });
            state.EntityManager.AddComponentData(cargoEntity, new VerticalPosition { Value = battleConfig.CargoSpawnZ });
            state.EntityManager.AddComponentData(cargoEntity, new MoveSpeed { Value = 4.6f });
            state.EntityManager.AddComponentData(cargoEntity, new CargoWeight { Value = spawnedCargo.Weight });
            state.EntityManager.AddComponentData(cargoEntity, new CargoReward { Value = spawnedCargo.Reward });
            state.EntityManager.AddComponentData(cargoEntity, new CargoPenalty { Value = spawnedCargo.Penalty });
            state.EntityManager.AddComponentData(cargoEntity, LocalTransform.FromPositionRotationScale(
                new float3(laneX, 0.6f, battleConfig.CargoSpawnZ),
                quaternion.identity,
                1f));

            ApplyPhaseSnapshot(ref state);
        }

        private static void ApplyPhaseSnapshot(ref SystemState state)
        {
            var phaseSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            var rhythmEntity = state.EntityManager.CreateEntityQuery(typeof(RhythmPhaseState)).GetSingletonEntity();
            state.EntityManager.SetComponentData(rhythmEntity, new RhythmPhaseState
            {
                CurrentPhase = phaseSnapshot.CurrentPhase,
                PendingApprovalCount = phaseSnapshot.PendingApprovalCount,
                PendingRouteCount = phaseSnapshot.PendingRouteCount,
                HasActiveCargo = phaseSnapshot.HasActiveCargo ? (byte)1 : (byte)0
            });
        }
    }
}
