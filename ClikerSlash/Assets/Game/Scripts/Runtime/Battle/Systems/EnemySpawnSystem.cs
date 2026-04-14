using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 승인 컨베이어와 레인선택 컨베이어에 각각 독립적인 활성 물류를 스폰합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleTimerSystem))]
    public partial struct CargoSpawnSystem : ISystem
    {
        /// <summary>
        /// 스폰 전에 세션 설정, 물류 설정, 타이머 상태가 모두 준비되도록 강제합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<CargoConfig>();
            state.RequireForUpdate<SpawnTimerState>();
            state.RequireForUpdate<StageProgressState>();
            state.RequireForUpdate<RhythmPhaseState>();
        }

        /// <summary>
        /// 두 컨베이어의 스폰 타이머를 동시에 갱신하고 빈 구역에만 새 물류를 배치합니다.
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
            var cargoConfig = SystemAPI.GetSingleton<CargoConfig>();
            var spawnTimer = SystemAPI.GetSingletonRW<SpawnTimerState>();

            spawnTimer.ValueRW.ApprovalRemaining -= deltaTime;
            spawnTimer.ValueRW.RouteRemaining -= deltaTime;

            TrySpawnForArea(
                ref state,
                ref spawnTimer.ValueRW.ApprovalRemaining,
                BattleMiniGameArea.Approval,
                battleConfig.ApprovalLaneX,
                battleConfig,
                cargoConfig.MoveSpeed);
            TrySpawnForArea(
                ref state,
                ref spawnTimer.ValueRW.RouteRemaining,
                BattleMiniGameArea.RouteSelection,
                battleConfig.RouteLaneX,
                battleConfig,
                cargoConfig.MoveSpeed);

            ApplyPhaseSnapshot(ref state);
        }

        /// <summary>
        /// 특정 구역의 타이머가 만료되고 빈 자리일 때만 새 활성 물류를 하나 생성합니다.
        /// </summary>
        private static void TrySpawnForArea(
            ref SystemState state,
            ref float remainingTime,
            BattleMiniGameArea area,
            float laneX,
            BattleConfig battleConfig,
            float moveSpeed)
        {
            if (remainingTime > 0f)
            {
                return;
            }

            if (!PrototypeSessionRuntime.TryDequeueCargoForArea(area, out var phase, out var approvalCargo, out var routeCargo))
            {
                return;
            }

            remainingTime += battleConfig.SpawnInterval;

            var cargoEntity = state.EntityManager.CreateEntity();
            var spawnedCargo = area == BattleMiniGameArea.Approval
                ? approvalCargo
                : new ApprovalCargoSnapshot
                {
                    EntryId = routeCargo.EntryId,
                    Kind = routeCargo.Kind,
                    Weight = routeCargo.Weight,
                    Reward = routeCargo.Reward,
                    Penalty = routeCargo.Penalty
                };

            // 승인/레인선택 물류는 같은 구조를 쓰되 승인 결과만 레인선택 단계에 전달합니다.
            state.EntityManager.AddComponentData(cargoEntity, new CargoTag());
            state.EntityManager.AddComponentData(cargoEntity, new CargoEntryId { Value = spawnedCargo.EntryId });
            state.EntityManager.AddComponentData(cargoEntity, new CargoKind { Value = spawnedCargo.Kind });
            state.EntityManager.AddComponentData(cargoEntity, new CargoMiniGamePhase { Value = phase });
            state.EntityManager.AddComponentData(cargoEntity, new CargoApprovalDecision
            {
                Value = area == BattleMiniGameArea.RouteSelection ? routeCargo.ApprovalDecision : ApprovalDecision.None
            });
            state.EntityManager.AddComponentData(cargoEntity, new LaneIndex { Value = 0 });
            state.EntityManager.AddComponentData(cargoEntity, new VerticalPosition { Value = battleConfig.CargoSpawnZ });
            state.EntityManager.AddComponentData(cargoEntity, new MoveSpeed { Value = moveSpeed });
            state.EntityManager.AddComponentData(cargoEntity, new CargoWeight { Value = spawnedCargo.Weight });
            state.EntityManager.AddComponentData(cargoEntity, new CargoReward { Value = spawnedCargo.Reward });
            state.EntityManager.AddComponentData(cargoEntity, new CargoPenalty { Value = spawnedCargo.Penalty });
            state.EntityManager.AddComponentData(cargoEntity, LocalTransform.FromPositionRotationScale(
                new float3(laneX, 0.6f, battleConfig.CargoSpawnZ),
                quaternion.identity,
                1f));
        }

        /// <summary>
        /// HUD와 디버그 프레젠터가 최신 큐 상태를 읽을 수 있도록 ECS 싱글턴을 동기화합니다.
        /// </summary>
        private static void ApplyPhaseSnapshot(ref SystemState state)
        {
            var phaseSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            var rhythmEntity = state.EntityManager.CreateEntityQuery(typeof(RhythmPhaseState)).GetSingletonEntity();
            state.EntityManager.SetComponentData(rhythmEntity, new RhythmPhaseState
            {
                CurrentPhase = phaseSnapshot.CurrentPhase,
                FocusedArea = phaseSnapshot.FocusedArea,
                PendingApprovalCount = phaseSnapshot.PendingApprovalCount,
                PendingRouteCount = phaseSnapshot.PendingRouteCount,
                PendingLoadingDockCount = phaseSnapshot.PendingLoadingDockCount,
                HasActiveCargo = phaseSnapshot.HasActiveCargo ? (byte)1 : (byte)0,
                HasActiveApprovalCargo = phaseSnapshot.HasApprovalCargo ? (byte)1 : (byte)0,
                HasActiveRouteCargo = phaseSnapshot.HasRouteCargo ? (byte)1 : (byte)0
            });
        }
    }
}
