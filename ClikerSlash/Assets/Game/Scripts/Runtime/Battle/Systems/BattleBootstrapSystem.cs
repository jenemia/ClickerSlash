using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 씬 설정 기반 싱글턴이 준비되면 런타임 물류 세션 엔티티를 최초로 구성합니다.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BattleBootstrapSystem : ISystem
    {
        /// <summary>
        /// 베이크된 설정 싱글턴이 모두 존재할 때까지 세션 상태 초기화를 보류합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<PlayerConfig>();
            state.RequireForUpdate<CargoConfig>();
            state.RequireForUpdate<LaneLayout>();
        }

        /// <summary>
        /// 씬 부트스트랩당 한 번만 세션 싱글턴 상태와 플레이어 런타임 엔티티를 생성합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var configEntity = SystemAPI.GetSingletonEntity<BattleConfig>();
            if (state.EntityManager.HasComponent<BattleRuntimeInitializedTag>(configEntity))
            {
                return;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var playerConfig = SystemAPI.GetSingleton<PlayerConfig>();
            var laneEntity = SystemAPI.GetSingletonEntity<LaneLayout>();
            var laneXs = state.EntityManager.GetBuffer<LaneWorldXElement>(laneEntity);
            var resolvedProgression = PrototypeSessionRuntime.GetResolvedMetaProgression();
            var activeLaneCount = MetaProgressionBootstrapBridge.ResolveActiveLaneCount(resolvedProgression, laneXs.Length);
            var activeLaneStartIndex = BattleLaneUtility.ResolveCenteredActiveLaneStartIndex(activeLaneCount, laneXs.Length);
            var initialLane = BattleLaneUtility.ClampLaneToActiveRange(
                playerConfig.InitialLane,
                activeLaneStartIndex,
                activeLaneCount,
                laneXs.Length);
            var playerX = BattleLaneUtility.GetLaneX(laneXs, initialLane);
            var resolvedWorkDuration = PrototypeSessionRuntime.ResolveWorkDuration(
                battleConfig.BaseWorkDurationSeconds,
                battleConfig.HealthDurationBonusSeconds);
            var spawnPlanSeed = (uint)System.DateTime.UtcNow.Ticks;
            if (spawnPlanSeed == 0u)
            {
                spawnPlanSeed = 1u;
            }

            // 전투 시작 시점에 전체 레인 스폰 순서를 먼저 고정해 팔레트 재고와 실제 스폰이 어긋나지 않게 합니다.
            PrototypeSessionRuntime.InitializeLaneCargoSpawnPlan(
                resolvedWorkDuration,
                battleConfig.SpawnInterval,
                spawnPlanSeed);

            state.EntityManager.AddComponentData(configEntity, new StageProgressState
            {
                RemainingWorkTime = resolvedWorkDuration,
                ElapsedWorkTime = 0f,
                IsFinished = 0
            });
            state.EntityManager.AddComponentData(configEntity, new SpawnTimerState
            {
                Remaining = battleConfig.SpawnInterval,
                Random = Unity.Mathematics.Random.CreateFromIndex(spawnPlanSeed)
            });
            state.EntityManager.AddComponentData(configEntity, new BattleOutcomeState
            {
                HasOutcome = 0
            });
            state.EntityManager.AddComponentData(configEntity, new BattleSessionStatsState
            {
                TotalMoney = 0,
                ProcessedCargoCount = 0,
                MissedCargoCount = 0,
                CurrentCombo = 0,
                MaxCombo = 0,
                WorkedTimeSeconds = 0f,
                ResolvedWorkDurationSeconds = resolvedWorkDuration,
                HasSnapshot = 0
            });
            state.EntityManager.AddComponentData(configEntity, new WorkerProgressionStats
            {
                SessionDurationSeconds = resolvedWorkDuration,
                MaxHandleWeight = resolvedProgression.MaxHandleWeight,
                LaneMoveDurationSeconds = resolvedProgression.LaneMoveDurationSeconds,
                TimingWindowHalfDepth = resolvedProgression.TimingWindowHalfDepth
            });
            state.EntityManager.AddComponentData(configEntity, new SessionRuleState
            {
                ActiveLaneStartIndex = activeLaneStartIndex,
                ActiveLaneCount = activeLaneCount,
                PreviewCargoCount = resolvedProgression.PreviewCargoCount
            });
            state.EntityManager.AddComponentData(configEntity, new EconomyModifier
            {
                RewardMultiplier = resolvedProgression.RewardMultiplier,
                PenaltyMultiplier = resolvedProgression.PenaltyMultiplier
            });
            state.EntityManager.AddComponentData(configEntity, new AutomationProfile
            {
                ReturnBeltChance = resolvedProgression.ReturnBeltChance,
                HasWeightPreview = resolvedProgression.HasWeightPreview ? (byte)1 : (byte)0,
                HasAssistArm = resolvedProgression.HasAssistArm ? (byte)1 : (byte)0
            });
            state.EntityManager.AddComponentData(configEntity, new RobotProfile
            {
                HasLaneRobotAccess = resolvedProgression.HasLaneRobotAccess ? (byte)1 : (byte)0,
                HasDockRobotAccess = resolvedProgression.HasDockRobotAccess ? (byte)1 : (byte)0,
                MaxHandleWeight = resolvedProgression.RobotMaxHandleWeight,
                PrecisionTier = resolvedProgression.RobotPrecisionTier
            });
            state.EntityManager.AddComponentData(configEntity, new SkillLoadoutState
            {
                SchemaVersion = resolvedProgression.SchemaVersion,
                ResolvedLoadoutVersion = resolvedProgression.ResolvedLoadoutVersion,
                UnlockedNodeCount = resolvedProgression.UnlockedNodeCount
            });
            state.EntityManager.AddComponent<BattleRuntimeInitializedTag>(configEntity);

            var playerEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(playerEntity, new PlayerTag());
            state.EntityManager.AddComponentData(playerEntity, new LaneIndex { Value = initialLane });
            state.EntityManager.AddComponentData(playerEntity, new LaneMoveState
            {
                StartLane = initialLane,
                TargetLane = initialLane,
                Progress = 0f,
                IsMoving = 0
            });
            state.EntityManager.AddComponentData(playerEntity, new HandleState { BusyUntilTime = 0d });
            state.EntityManager.AddComponentData(playerEntity, new MaxHandleWeight { Value = resolvedProgression.MaxHandleWeight });
            state.EntityManager.AddComponentData(playerEntity, new ComboState { Current = 0, Max = 0 });
            state.EntityManager.AddComponentData(playerEntity, LocalTransform.FromPositionRotationScale(
                new float3(playerX, playerConfig.WorldPosition.y, playerConfig.WorldPosition.z),
                quaternion.identity,
                1f));
            state.EntityManager.AddBuffer<LaneMoveCommandBufferElement>(playerEntity);

            if (resolvedProgression.HasLaneRobotAccess)
            {
                var laneRobotEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(laneRobotEntity, new LaneRobotTag());
                state.EntityManager.AddComponentData(laneRobotEntity, new LaneRobotState
                {
                    AssignedLane = 0,
                    IsAssigned = 0
                });
                state.EntityManager.AddComponentData(laneRobotEntity, new HandleState { BusyUntilTime = 0d });
                state.EntityManager.AddComponentData(laneRobotEntity, LocalTransform.FromPositionRotationScale(
                    new float3(0f, playerConfig.WorldPosition.y, (battleConfig.JudgmentLineZ + battleConfig.FailLineZ) * 0.5f),
                    quaternion.identity,
                    0.95f));
            }
        }
    }
}
