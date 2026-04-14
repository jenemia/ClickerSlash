using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ClikerSlash.Tests.PlayMode
{
    /// <summary>
    /// 2단계 리듬 물류 루프의 핵심 계약과 허브 연동을 검증합니다.
    /// </summary>
    public class PrototypeSessionLoopPlayModeTests
    {
        [UnityTest]
        public IEnumerator HealthLevelControlsResolvedWorkDurationOnBattleEntry()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            SeedRuntimeCurrency(1);
            Assert.That(PrototypeSessionRuntime.IncreaseHealthLevel(), Is.True);
            var expectedDuration = PrototypeSessionRuntime.GetResolvedMetaProgression().SessionDurationSeconds;

            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var stage = entityManager.CreateEntityQuery(typeof(StageProgressState)).GetSingleton<StageProgressState>();
            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var progressionStats = entityManager.CreateEntityQuery(typeof(WorkerProgressionStats)).GetSingleton<WorkerProgressionStats>();

            Assert.That(stage.RemainingWorkTime, Is.EqualTo(expectedDuration).Within(0.05f));
            Assert.That(stats.ResolvedWorkDurationSeconds, Is.EqualTo(expectedDuration).Within(0.01f));
            Assert.That(PrototypeSessionRuntime.ResolvedWorkDurationSeconds, Is.EqualTo(expectedDuration).Within(0.01f));
            Assert.That(progressionStats.SessionDurationSeconds, Is.EqualTo(expectedDuration).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator BattleSceneStartsInApprovalPhaseWithFiveRouteLanes()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var phaseSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            var sessionRules = entityManager.CreateEntityQuery(typeof(SessionRuleState)).GetSingleton<SessionRuleState>();
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();

            Assert.That(phaseSnapshot.CurrentPhase, Is.EqualTo(BattleMiniGamePhase.Approval));
            Assert.That(phaseSnapshot.PendingApprovalCount, Is.GreaterThan(0));
            Assert.That(phaseSnapshot.PendingRouteCount, Is.Zero);
            Assert.That(sessionRules.ActiveLaneCount, Is.EqualTo(PrototypeSessionRuntime.FixedRouteLaneCount));
            Assert.That(battleConfig.DeliveryLaneMaxWeight, Is.EqualTo(PrototypeSessionRuntime.DefaultDeliveryLaneMaxWeight));
        }

        [UnityTest]
        public IEnumerator ApprovalMissDoesNotEnqueueRouteCargo()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 11,
                Kind = LoadingDockCargoKind.General,
                Weight = 4,
                Reward = 70,
                Penalty = 25
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();
            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            Assert.That(approvalCargo, Is.Not.EqualTo(Entity.Null));

            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.FailLineZ - 0.25f);
            yield return null;
            yield return null;

            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var phaseSnapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();

            Assert.That(stats.MissedCargoCount, Is.EqualTo(1));
            Assert.That(phaseSnapshot.PendingRouteCount, Is.Zero);
            Assert.That(phaseSnapshot.CurrentPhase, Is.EqualTo(BattleMiniGamePhase.Completed));
        }

        [UnityTest]
        public IEnumerator ApprovalApproveHandsOffCargoToRouteSelection()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 21,
                Kind = LoadingDockCargoKind.Fragile,
                Weight = 4,
                Reward = 70,
                Penalty = 25
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();
            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            Assert.That(approvalCargo, Is.Not.EqualTo(Entity.Null));

            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
            yield return null;
            ForceImmediateSpawn(entityManager);
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection, 30);

            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var routeCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection);
            var approvalDecision = entityManager.GetComponentData<CargoApprovalDecision>(routeCargo);
            var cargoEntryId = entityManager.GetComponentData<CargoEntryId>(routeCargo);
            var cargoKind = entityManager.GetComponentData<CargoKind>(routeCargo);
            var cargoWeight = entityManager.GetComponentData<CargoWeight>(routeCargo);

            Assert.That(stats.ApprovedCargoCount, Is.EqualTo(1));
            Assert.That(cargoEntryId.Value, Is.EqualTo(21));
            Assert.That(approvalDecision.Value, Is.EqualTo(ApprovalDecision.Approve));
            Assert.That(cargoKind.Value, Is.EqualTo(LoadingDockCargoKind.Fragile));
            Assert.That(cargoWeight.Value, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator RouteSelectionCorrectDeliveryAddsIncome()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 31,
                Kind = LoadingDockCargoKind.General,
                Weight = 4,
                Reward = 90,
                Penalty = 35
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();

            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
            yield return null;

            ForceImmediateSpawn(entityManager);
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection, 30);

            var routeCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection);
            SetCargoPositionAndStop(entityManager, routeCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Air);
            yield return null;
            yield return null;

            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            Assert.That(stats.TotalMoney, Is.EqualTo(90));
            Assert.That(stats.CorrectRouteCount, Is.EqualTo(1));
            Assert.That(stats.MisrouteCount, Is.Zero);
            Assert.That(stats.ReturnCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OverweightApprovedCargoCreatesPenaltyWhenSentToDeliveryLane()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 41,
                Kind = LoadingDockCargoKind.Frozen,
                Weight = 8,
                Reward = 120,
                Penalty = 55
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();

            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
            yield return null;

            ForceImmediateSpawn(entityManager);
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection, 30);

            var routeCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection);
            SetCargoPositionAndStop(entityManager, routeCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Sea);
            yield return null;
            yield return null;

            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            Assert.That(stats.TotalMoney, Is.EqualTo(-55));
            Assert.That(stats.CorrectRouteCount, Is.Zero);
            Assert.That(stats.MisrouteCount, Is.EqualTo(1));
            Assert.That(stats.ReturnCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RejectedCargoReturnedCountsAsReturnWithoutPenalty()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 51,
                Kind = LoadingDockCargoKind.General,
                Weight = 4,
                Reward = 80,
                Penalty = 30
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();

            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Reject);
            yield return null;

            ForceImmediateSpawn(entityManager);
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection, 30);

            var routeCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection);
            SetCargoPositionAndStop(entityManager, routeCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Return);
            yield return null;
            yield return null;

            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            Assert.That(stats.RejectedCargoCount, Is.EqualTo(1));
            Assert.That(stats.ReturnCount, Is.EqualTo(1));
            Assert.That(stats.MisrouteCount, Is.Zero);
            Assert.That(stats.TotalMoney, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BattleSceneContainsApprovalAndRouteSelectionPresentation()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var approvalEnvironment = Object.FindFirstObjectByType<ApprovalEnvironmentAuthoring>();
            var approvalPresenter = Object.FindFirstObjectByType<ApprovalMiniGamePresenter>();
            var routePresenter = Object.FindFirstObjectByType<RouteLaneSelectionPresenter>();

            Assert.That(approvalEnvironment, Is.Not.Null);
            Assert.That(approvalEnvironment.cargoSpawnAnchor, Is.Not.Null);
            Assert.That(approvalEnvironment.judgmentAnchor, Is.Not.Null);
            Assert.That(approvalEnvironment.failAnchor, Is.Not.Null);
            Assert.That(approvalEnvironment.scaleAnchor, Is.Not.Null);
            Assert.That(approvalEnvironment.stickerAnchor, Is.Not.Null);
            Assert.That(approvalPresenter, Is.Not.Null);
            Assert.That(routePresenter, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator WorkTimeEndsAndSnapshotTransfersToHub()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var stageEntity = entityManager.CreateEntityQuery(typeof(StageProgressState)).GetSingletonEntity();
            var stage = entityManager.GetComponentData<StageProgressState>(stageEntity);
            stage.RemainingWorkTime = 0.01f;
            entityManager.SetComponentData(stageEntity, stage);

            yield return new WaitUntil(() =>
                PrototypeSessionRuntime.HasLastBattleResult &&
                entityManager.CreateEntityQuery(typeof(BattleOutcomeState)).GetSingleton<BattleOutcomeState>().HasOutcome != 0);

            var snapshot = PrototypeSessionRuntime.LastBattleResult;
            Assert.That(snapshot.WorkedTimeSeconds, Is.GreaterThan(0f));

            yield return LoadSceneAndWait(PrototypeSessionRuntime.HubSceneName);
            var hubPresenter = Object.FindFirstObjectByType<PrototypeHubPresenter>();
            Assert.That(hubPresenter, Is.Not.Null);
            Assert.That(PrototypeSessionRuntime.HasLastBattleResult, Is.True);
        }

        [UnityTest]
        public IEnumerator PositiveBattleResultsIncreasePlayerCurrency()
        {
            PrototypeSessionRuntime.ResetPrototypeState();

            PrototypeSessionRuntime.StoreBattleResult(new BattleResultSnapshot
            {
                TotalMoney = 120,
                WorkedTimeSeconds = 12f
            });

            var currency = PrototypeSessionRuntime.GetCurrencySnapshot();
            Assert.That(currency.currentBalance, Is.EqualTo(120));
            Assert.That(currency.totalBattleEarned, Is.EqualTo(120));
            Assert.That(currency.totalSkillSpent, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NegativeBattleResultsDoNotReducePlayerBalance()
        {
            PrototypeSessionRuntime.ResetPrototypeState();

            PrototypeSessionRuntime.StoreBattleResult(new BattleResultSnapshot
            {
                TotalMoney = 80,
                WorkedTimeSeconds = 10f
            });
            PrototypeSessionRuntime.StoreBattleResult(new BattleResultSnapshot
            {
                TotalMoney = -40,
                WorkedTimeSeconds = 5f
            });

            var currency = PrototypeSessionRuntime.GetCurrencySnapshot();
            Assert.That(currency.currentBalance, Is.EqualTo(80));
            Assert.That(currency.totalBattleEarned, Is.EqualTo(80));
            Assert.That(PrototypeSessionRuntime.LastBattleResult.TotalMoney, Is.EqualTo(-40));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeUpgradeConsumesCurrencyAndRejectsInsufficientFunds()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            SeedRuntimeCurrency(1);
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();

            Assert.That(PrototypeSessionRuntime.TryUpgradeNode(MetaProgressionCatalogAsset.StarterVitalityNodeId, catalog), Is.True);
            var currency = PrototypeSessionRuntime.GetCurrencySnapshot();
            Assert.That(currency.currentBalance, Is.Zero);
            Assert.That(currency.totalSkillSpent, Is.EqualTo(1));

            Assert.That(PrototypeSessionRuntime.TryUpgradeNode("strength.basic_strength_training", catalog), Is.False);
            currency = PrototypeSessionRuntime.GetCurrencySnapshot();
            Assert.That(currency.currentBalance, Is.Zero);
            Assert.That(currency.totalSkillSpent, Is.EqualTo(1));
            yield return null;
        }

        private static IEnumerator PrepareBattleSceneWithPlan(params ApprovalCargoSnapshot[] plannedCargoEntries)
        {
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            ClearAllCargo(entityManager);
            PrototypeSessionRuntime.InitializeRhythmCargoPlan(plannedCargoEntries);
            SyncRhythmPhaseState(entityManager);
            ForceImmediateSpawn(entityManager);

            yield return null;
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.Approval, 30);
        }

        private static IEnumerator LoadSceneAndWait(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return WaitForSceneAndWorld(sceneName);
        }

        private static IEnumerator WaitForSceneAndWorld(string sceneName)
        {
            yield return null;
            yield return null;
            yield return new WaitUntil(() =>
                SceneManager.GetActiveScene().name == sceneName &&
                World.DefaultGameObjectInjectionWorld != null &&
                World.DefaultGameObjectInjectionWorld.IsCreated);
        }

        private static void ClearAllCargo(EntityManager entityManager)
        {
            using var cargoQuery = entityManager.CreateEntityQuery(typeof(CargoTag));
            entityManager.DestroyEntity(cargoQuery);
        }

        private static Entity FindActiveCargo(EntityManager entityManager, BattleMiniGamePhase phase)
        {
            using var cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoMiniGamePhase>());
            using var entities = cargoQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var phases = cargoQuery.ToComponentDataArray<CargoMiniGamePhase>(Unity.Collections.Allocator.Temp);

            for (var index = 0; index < entities.Length; index += 1)
            {
                if (phases[index].Value == phase)
                {
                    return entities[index];
                }
            }

            return Entity.Null;
        }

        private static void SetCargoPositionAndStop(EntityManager entityManager, Entity cargoEntity, float zPosition)
        {
            entityManager.SetComponentData(cargoEntity, new MoveSpeed { Value = 0f });
            entityManager.SetComponentData(cargoEntity, new VerticalPosition { Value = zPosition });
            var transform = entityManager.GetComponentData<LocalTransform>(cargoEntity);
            transform.Position.z = zPosition;
            entityManager.SetComponentData(cargoEntity, transform);
        }

        private static void ForceImmediateSpawn(EntityManager entityManager)
        {
            var spawnEntity = entityManager.CreateEntityQuery(typeof(SpawnTimerState)).GetSingletonEntity();
            var spawnTimer = entityManager.GetComponentData<SpawnTimerState>(spawnEntity);
            spawnTimer.Remaining = 0f;
            entityManager.SetComponentData(spawnEntity, spawnTimer);
        }

        private static void SyncRhythmPhaseState(EntityManager entityManager)
        {
            var rhythmEntity = entityManager.CreateEntityQuery(typeof(RhythmPhaseState)).GetSingletonEntity();
            var snapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            entityManager.SetComponentData(rhythmEntity, new RhythmPhaseState
            {
                CurrentPhase = snapshot.CurrentPhase,
                PendingApprovalCount = snapshot.PendingApprovalCount,
                PendingRouteCount = snapshot.PendingRouteCount,
                HasActiveCargo = snapshot.HasActiveCargo ? (byte)1 : (byte)0
            });
        }

        private static IEnumerator WaitForActiveCargo(EntityManager entityManager, BattleMiniGamePhase phase, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame += 1)
            {
                if (FindActiveCargo(entityManager, phase) != Entity.Null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.That(FindActiveCargo(entityManager, phase), Is.Not.EqualTo(Entity.Null));
        }

        private static void SeedRuntimeCurrency(int amount)
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            PrototypeSessionRuntime.EnsureMetaProgressionInitialized(catalog);
            var snapshot = PrototypeSessionRuntime.GetMetaProgressionSnapshot();
            snapshot.currency ??= PlayerCurrencySnapshot.CreateDefault();
            snapshot.currency.currentBalance = amount;
            snapshot.currency.totalBattleEarned = amount;
            PrototypeSessionRuntime.SetMetaProgressionSnapshot(snapshot, catalog);
        }
    }
}
