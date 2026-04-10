using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ClikerSlash.Tests.PlayMode
{
    /// <summary>
    /// 프로토타입 물류 세션 루프와 허브 메타 연동의 핵심 경로를 검증하는 플레이 모드 테스트입니다.
    /// </summary>
    public class PrototypeSessionLoopPlayModeTests
    {
        /// <summary>
        /// 허브 체력 레벨이 전투 진입 시 실제 작업시간으로 반영되는지 검증합니다.
        /// </summary>
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

        /// <summary>
        /// 성공 처리와 무게 초과 실패가 돈, 콤보, 세션 통계에 올바르게 반영되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator CargoHandlingAndOverweightMissUpdateMoneyAndCombo()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            FreezeRandomSpawns(entityManager);

            var playerEntity = entityManager.CreateEntityQuery(typeof(PlayerTag)).GetSingletonEntity();
            var laneIndex = entityManager.GetComponentData<LaneIndex>(playerEntity).Value;
            var maxHandleWeight = entityManager.GetComponentData<MaxHandleWeight>(playerEntity).Value;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();

            SpawnCargo(entityManager, laneIndex, battleConfig.JudgmentLineZ, maxHandleWeight, 75, 20, 0f);
            yield return null;
            yield return null;

            var comboState = entityManager.GetComponentData<ComboState>(playerEntity);
            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var economyModifier = entityManager.CreateEntityQuery(typeof(EconomyModifier)).GetSingleton<EconomyModifier>();
            Assert.That(stats.ProcessedCargoCount, Is.EqualTo(1));
            Assert.That(stats.TotalMoney, Is.EqualTo(Mathf.RoundToInt(75 * economyModifier.RewardMultiplier)));
            Assert.That(comboState.Current, Is.EqualTo(1));
            Assert.That(comboState.Max, Is.EqualTo(1));

            yield return new WaitForSeconds(battleConfig.HandleDurationSeconds + 0.05f);

            SpawnCargo(entityManager, laneIndex, battleConfig.JudgmentLineZ, maxHandleWeight + 2, 90, 40, 0f);
            yield return null;
            yield return null;

            comboState = entityManager.GetComponentData<ComboState>(playerEntity);
            stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var expectedMoney = Mathf.RoundToInt(75 * economyModifier.RewardMultiplier)
                - Mathf.RoundToInt(40 * economyModifier.PenaltyMultiplier);
            Assert.That(stats.MissedCargoCount, Is.EqualTo(1));
            Assert.That(stats.TotalMoney, Is.EqualTo(expectedMoney));
            Assert.That(comboState.Current, Is.EqualTo(0));
            Assert.That(comboState.Max, Is.EqualTo(1));
        }

        /// <summary>
        /// 작업시간 종료 후 결과 스냅샷이 허브까지 전달되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator WorkTimeEndsAndSnapshotTransfersToHub()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            FreezeRandomSpawns(entityManager);

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

        /// <summary>
        /// 허브에서 체력을 올린 뒤 재진입하면 다음 세션 작업시간이 늘고 런타임 통계가 초기화되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator HubHealthIncreaseAffectsReentryAndRuntimeResets()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.HubSceneName);

            var hubPresenter = Object.FindFirstObjectByType<PrototypeHubPresenter>();
            Assert.That(hubPresenter, Is.Not.Null);

            SeedRuntimeCurrency(1);
            Assert.That(PrototypeSessionRuntime.IncreaseHealthLevel(), Is.True);
            hubPresenter.LoadPrototypeBattle();
            yield return WaitForSceneAndWorld(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var stage = entityManager.CreateEntityQuery(typeof(StageProgressState)).GetSingleton<StageProgressState>();
            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var sessionRules = entityManager.CreateEntityQuery(typeof(SessionRuleState)).GetSingleton<SessionRuleState>();
            var expectedDuration = PrototypeSessionRuntime.GetResolvedMetaProgression().SessionDurationSeconds;
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();

            Assert.That(stage.RemainingWorkTime, Is.EqualTo(expectedDuration).Within(0.05f));
            Assert.That(stats.TotalMoney, Is.EqualTo(0));
            Assert.That(stats.ProcessedCargoCount, Is.EqualTo(0));
            Assert.That(stats.MissedCargoCount, Is.EqualTo(0));
            Assert.That(stats.CurrentCombo, Is.EqualTo(0));
            Assert.That(PrototypeSessionRuntime.HealthLevel, Is.EqualTo(2));
            Assert.That(PrototypeSessionRuntime.GetCurrencySnapshot().currentBalance, Is.EqualTo(0));
            Assert.That(PrototypeSessionRuntime.GetCurrencySnapshot().totalSkillSpent, Is.EqualTo(1));
            Assert.That(sessionRules.ActiveLaneCount, Is.EqualTo(catalog.workerBaseStats.startingUnlockedLaneCount));
        }

        /// <summary>
        /// 양수 전투 결과는 플레이어 잔액과 누적 획득량으로 정산되어야 합니다.
        /// </summary>
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

        /// <summary>
        /// 음수 전투 결과는 최근 결과에는 남더라도 플레이어 잔액은 깎지 않아야 합니다.
        /// </summary>
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

        /// <summary>
        /// 허브 구매는 재화가 충분할 때만 진행되고 사용 금액이 누적되어야 합니다.
        /// </summary>
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

        /// <summary>
        /// 상하차 해금 전에는 진입할 수 없고, 해금 후에는 진입/복귀 상태 계약이 순서대로 흘러야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockEntryAndReturnFollowRuntimeContract()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();

            var dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.HasLoadingDockAccess, Is.True);
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.Lane));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.None));
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);

            dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.Lane));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.EnteringLoadingDock));
            Assert.That(dockState.HasPendingEntryRequest, Is.True);

            PrototypeSessionRuntime.ConsumeLoadingDockEntryRequest();
            dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.LoadingDock));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.ActiveInLoadingDock));
            Assert.That(dockState.HasPendingEntryRequest, Is.False);
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.False);

            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockReturn(), Is.True);
            dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.HasPendingReturnRequest, Is.True);
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.ReturningToLane));

            PrototypeSessionRuntime.ConsumeLoadingDockReturnRequest();
            dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.Lane));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.None));
            Assert.That(dockState.HasPendingReturnRequest, Is.False);
            yield return null;
        }

        /// <summary>
        /// Q 토글용 런타임 API는 레인과 상하차 구역 사이를 왕복하고 전환 중 재요청은 막아야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockToggleSwitchesBetweenAreasAndIgnoresTransitions()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();

            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.True);
            var dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.EnteringLoadingDock));
            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.False);

            PrototypeSessionRuntime.ConsumeLoadingDockEntryRequest();
            dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.LoadingDock));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.ActiveInLoadingDock));

            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.True);
            dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.ReturningToLane));
            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.False);

            PrototypeSessionRuntime.ConsumeLoadingDockReturnRequest();
            dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.Lane));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.None));
            yield return null;
        }

        /// <summary>
        /// 일시정지 팝업 상태는 시간 흐름을 멈추고 상하차 토글 요청도 차단해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator PauseMenuControlsTimeScaleAndBlocksDockToggle()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();

            PrototypeSessionRuntime.OpenPauseMenu();
            Assert.That(PrototypeSessionRuntime.IsPauseMenuOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.False);

            PrototypeSessionRuntime.ClosePauseMenu();
            Assert.That(PrototypeSessionRuntime.IsPauseMenuOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.True);
            PrototypeSessionRuntime.ConsumeLoadingDockEntryRequest();

            var dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.LoadingDock));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.ActiveInLoadingDock));

            PrototypeSessionRuntime.OpenPauseMenu();
            Assert.That(PrototypeSessionRuntime.IsPauseMenuOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.False);

            PrototypeSessionRuntime.ClosePauseMenu();
            Assert.That(PrototypeSessionRuntime.TryToggleLoadingDock(catalog), Is.True);
            PrototypeSessionRuntime.ConsumeLoadingDockReturnRequest();
            yield return null;
        }

        /// <summary>
        /// 상하차 세션 큐는 최대 5개 활성 슬롯을 먼저 채우고 초과분은 backlog에 FIFO로 쌓아야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockQueueSnapshotSeparatesActiveSlotsAndBacklog()
        {
            PrototypeSessionRuntime.ResetPrototypeState();

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);

            var snapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();
            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            var backlogEntries = PrototypeSessionRuntime.GetLoadingDockBacklogCargoEntries();

            Assert.That(snapshot.ActiveSlotCount, Is.EqualTo(5));
            Assert.That(snapshot.BacklogCount, Is.EqualTo(2));
            Assert.That(snapshot.TotalCount, Is.EqualTo(7));
            Assert.That(snapshot.MaxActiveSlotCount, Is.EqualTo(PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount));
            Assert.That(activeEntries.Length, Is.EqualTo(5));
            Assert.That(backlogEntries.Length, Is.EqualTo(2));
            Assert.That(activeEntries[0].SlotIndex, Is.EqualTo(0));
            Assert.That(activeEntries[0].EntryId, Is.EqualTo(1));
            Assert.That(activeEntries[4].SlotIndex, Is.EqualTo(4));
            Assert.That(activeEntries[4].EntryId, Is.EqualTo(5));
            Assert.That(backlogEntries[0].EntryId, Is.EqualTo(6));
            Assert.That(backlogEntries[1].EntryId, Is.EqualTo(7));
            yield return null;
        }

        /// <summary>
        /// 레인 성공 처리 이벤트만 상하차 세션 큐에 적재되고 미스 이벤트는 제외되어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator HandledCargoEventsEnqueueLoadingDockCargoKinds()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            FreezeRandomSpawns(entityManager);

            var handledEvent = entityManager.CreateEntity();
            entityManager.AddComponentData(handledEvent, new CargoHandledEvent
            {
                Reward = 25,
                Kind = LoadingDockCargoKind.Fragile
            });

            var missedEvent = entityManager.CreateEntity();
            entityManager.AddComponentData(missedEvent, new CargoMissedEvent { Penalty = 10 });

            yield return null;
            yield return null;

            var snapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();
            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();

            Assert.That(snapshot.TotalCount, Is.EqualTo(1));
            Assert.That(snapshot.ActiveSlotCount, Is.EqualTo(1));
            Assert.That(snapshot.BacklogCount, Is.Zero);
            Assert.That(activeEntries.Length, Is.EqualTo(1));
            Assert.That(activeEntries[0].SlotIndex, Is.EqualTo(0));
            Assert.That(activeEntries[0].Kind, Is.EqualTo(LoadingDockCargoKind.Fragile));
        }

        /// <summary>
        /// 상하차 진입이 시작되면 대기 중 명령을 비우고 진행 중 레인 이동도 현재 확정 레인으로 즉시 취소해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockEntryClearsQueuedLaneMovesAndCancelsInterpolation()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var playerEntity = entityManager.CreateEntityQuery(typeof(PlayerTag)).GetSingletonEntity();
            var laneEntity = entityManager.CreateEntityQuery(typeof(LaneLayout)).GetSingletonEntity();
            var laneXs = entityManager.GetBuffer<LaneWorldXElement>(laneEntity);
            var currentLane = entityManager.GetComponentData<LaneIndex>(playerEntity).Value;
            var currentLaneX = BattleLaneUtility.GetLaneX(laneXs, currentLane);

            entityManager.SetComponentData(playerEntity, new LaneMoveState
            {
                StartLane = currentLane,
                TargetLane = math.min(currentLane + 1, laneXs.Length - 1),
                Progress = 0.5f,
                IsMoving = 1
            });
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            transform.Position.x = currentLaneX + 1.6f;
            entityManager.SetComponentData(playerEntity, transform);

            var moveCommands = entityManager.GetBuffer<LaneMoveCommandBufferElement>(playerEntity);
            moveCommands.Add(new LaneMoveCommandBufferElement { Direction = 1 });
            moveCommands.Add(new LaneMoveCommandBufferElement { Direction = -1 });

            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            yield return null;

            moveCommands = entityManager.GetBuffer<LaneMoveCommandBufferElement>(playerEntity);
            var moveState = entityManager.GetComponentData<LaneMoveState>(playerEntity);
            transform = entityManager.GetComponentData<LocalTransform>(playerEntity);

            Assert.That(moveCommands.Length, Is.Zero);
            Assert.That(moveState.IsMoving, Is.Zero);
            Assert.That(moveState.StartLane, Is.EqualTo(currentLane));
            Assert.That(moveState.TargetLane, Is.EqualTo(currentLane));
            Assert.That(moveState.Progress, Is.Zero);
            Assert.That(transform.Position.x, Is.EqualTo(currentLaneX).Within(0.001f));
        }

        /// <summary>
        /// 허브로 전환할 때 일시정지 상태와 timeScale이 항상 정리되어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadHubSceneClosesPauseMenuState()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.OpenPauseMenu();
            Assert.That(PrototypeSessionRuntime.IsPauseMenuOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            PrototypeSceneNavigator.LoadHubScene();
            yield return WaitForSceneAndWorld(PrototypeSessionRuntime.HubSceneName);

            Assert.That(PrototypeSessionRuntime.IsPauseMenuOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PrototypeSessionRuntime.GetLoadingDockQueueSnapshot().TotalCount, Is.Zero);
        }

        /// <summary>
        /// 상하차 진입/복귀 요청이 도크 카메라 앵커와 레인 카메라 사이를 Cinemachine으로 전환하는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockCameraTransitionTargetsVirtualCameraAndReturns()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();

            var cameraObject = new GameObject("LoadingDockCamera", typeof(Camera), typeof(CinemachineBrain));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10.6f, -16.8f), Quaternion.Euler(31f, 0f, 0f));
            var brain = cameraObject.GetComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                PrototypeSessionRuntime.DefaultLoadingDockTransitionDurationSeconds);

            var battleViewObject = new GameObject("BattleView");
            var battleView = battleViewObject.AddComponent<BattleViewAuthoring>();
            battleView.CameraPosition = camera.transform.position;
            battleView.CameraRotation = new Vector3(31f, 0f, 0f);
            battleView.CameraFieldOfView = 34f;

            var loadingDockEnvironmentObject = new GameObject("LoadingDockEnvironment");
            var loadingDockEnvironment = loadingDockEnvironmentObject.AddComponent<LoadingDockEnvironmentAuthoring>();
            var cargoBayRoot = new GameObject("CargoBayRoot").transform;
            cargoBayRoot.SetParent(loadingDockEnvironmentObject.transform, false);
            cargoBayRoot.localPosition = new Vector3(-5f, 0f, -1.8f);
            loadingDockEnvironment.cargoBayRoot = cargoBayRoot;

            var truckBayRoot = new GameObject("TruckBayRoot").transform;
            truckBayRoot.SetParent(loadingDockEnvironmentObject.transform, false);
            truckBayRoot.localPosition = new Vector3(5f, 0f, 1.8f);
            loadingDockEnvironment.truckBayRoot = truckBayRoot;

            var truckDropZone = new GameObject("TruckDropZone").transform;
            truckDropZone.SetParent(truckBayRoot, false);
            loadingDockEnvironment.truckDropZone = truckDropZone;

            var laneVirtualCamera = CreatePassiveVirtualCamera(
                "LaneVirtualCamera",
                battleView.CameraPosition,
                Quaternion.Euler(battleView.CameraRotation),
                battleView.CameraFieldOfView,
                20);
            var loadingDockVirtualCamera = CreatePassiveVirtualCamera(
                "LoadingDockVirtualCamera",
                new Vector3(13.64f, 11.22f, -9.6f),
                Quaternion.Euler(39.583f, 18f, 0f),
                battleView.CameraFieldOfView,
                10);

            var bridgeObject = new GameObject("BattlePresentationRoot", typeof(BattlePresentationBridge));
            var bridge = bridgeObject.GetComponent<BattlePresentationBridge>();
            bridge.BindSceneReferences(camera, battleView, loadingDockEnvironment, laneVirtualCamera, loadingDockVirtualCamera);

            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 60);
            yield return WaitForCameraPose(
                camera,
                loadingDockVirtualCamera.transform.position,
                loadingDockVirtualCamera.transform.rotation,
                60,
                1.5f,
                2.5f);
            Assert.That(brain.ActiveVirtualCamera, Is.SameAs(loadingDockVirtualCamera));

            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockReturn(), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.Lane, WorkAreaTransitionPhase.None, 60);
            yield return WaitForCameraPose(
                camera,
                battleView.CameraPosition,
                Quaternion.Euler(battleView.CameraRotation),
                60,
                1.5f,
                2.5f);
            Assert.That(brain.ActiveVirtualCamera, Is.SameAs(laneVirtualCamera));

            Object.Destroy(cameraObject);
            Object.Destroy(battleViewObject);
            Object.Destroy(loadingDockEnvironmentObject);
            Object.Destroy(laneVirtualCamera.gameObject);
            Object.Destroy(loadingDockVirtualCamera.gameObject);
            Object.Destroy(bridgeObject);
        }

        /// <summary>
        /// 같은 kind의 물류는 레인과 상하차 구역에서 같은 프리팹 외형을 공유해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LaneAndLoadingDockCargoUseSameVisualPrefabForKind()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            FreezeRandomSpawns(entityManager);

            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();
            SpawnCargo(entityManager, 1, battleConfig.JudgmentLineZ + 1.25f, 6, 50, 20, 0f, LoadingDockCargoKind.Fragile);
            yield return null;
            yield return null;

            var laneCargoView = FindLaneCargoView();
            Assert.That(laneCargoView, Is.Not.Null);
            var laneRenderer = laneCargoView.GetComponent<Renderer>();
            Assert.That(laneRenderer, Is.Not.Null);

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            yield return null;
            yield return null;

            var presenter = Object.FindFirstObjectByType<LoadingDockMiniGamePresenter>();
            var cargoViewRoot = presenter != null ? presenter.transform.Find("LoadingDockCargoViewRoot") : null;
            Assert.That(cargoViewRoot, Is.Not.Null);
            Assert.That(cargoViewRoot.childCount, Is.GreaterThan(0));

            var dockCargoView = cargoViewRoot.GetChild(0).GetComponent<Renderer>();
            Assert.That(dockCargoView, Is.Not.Null);
            Assert.That(dockCargoView.sharedMaterial, Is.SameAs(laneRenderer.sharedMaterial));
            Assert.That(cargoViewRoot.GetChild(0).localScale, Is.EqualTo(laneCargoView.transform.localScale));
        }

        /// <summary>
        /// 전투 씬은 적재장과 차량 구역이 분리된 상하차 블록아웃 루트를 포함해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator BattleSceneContainsLoadingDockEnvironmentBlockout()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var loadingDockEnvironment = Object.FindFirstObjectByType<LoadingDockEnvironmentAuthoring>();
            Assert.That(loadingDockEnvironment, Is.Not.Null);
            Assert.That(loadingDockEnvironment.cargoBayRoot, Is.Not.Null);
            Assert.That(loadingDockEnvironment.truckBayRoot, Is.Not.Null);
            Assert.That(loadingDockEnvironment.cargoThrowOrigin, Is.Not.Null);
            Assert.That(loadingDockEnvironment.truckDropZone, Is.Not.Null);
            Assert.That(loadingDockEnvironment.cargoSlotAnchors, Is.Not.Null);
            Assert.That(loadingDockEnvironment.cargoSlotAnchors.Length, Is.EqualTo(PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount));
            foreach (var slotAnchor in loadingDockEnvironment.cargoSlotAnchors)
            {
                Assert.That(slotAnchor, Is.Not.Null);
            }
            Assert.That(
                Vector3.Distance(
                    loadingDockEnvironment.cargoBayRoot.position,
                    loadingDockEnvironment.truckBayRoot.position),
                Is.GreaterThan(6f));
        }

        /// <summary>
        /// 실제 배틀 씬에서도 상하차 진입 요청이 프레젠테이션 누락 없이 활성 상태까지 완료되어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator BattleSceneLoadingDockEntryCompletesFromRuntimeRequest()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var initialState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(initialState.HasLoadingDockAccess, Is.True);

            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            var mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null);

            var brain = mainCamera.GetComponent<CinemachineBrain>();
            Assert.That(brain, Is.Not.Null);

            var loadingDockCamera = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            CinemachineCamera activeDockCamera = null;
            foreach (var virtualCamera in loadingDockCamera)
            {
                if (virtualCamera != null && virtualCamera.name == "LoadingDockVirtualCamera")
                {
                    activeDockCamera = virtualCamera;
                    break;
                }
            }

            var environment = Object.FindFirstObjectByType<LoadingDockEnvironmentAuthoring>();
            Assert.That(activeDockCamera, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            yield return WaitForCameraPose(
                mainCamera,
                activeDockCamera.transform.position,
                activeDockCamera.transform.rotation,
                120);
            Assert.That(brain.ActiveVirtualCamera, Is.SameAs(activeDockCamera));

            var cargoViewport = mainCamera.WorldToViewportPoint(environment.cargoBayRoot.position);
            var truckViewport = mainCamera.WorldToViewportPoint(environment.truckBayRoot.position);
            Assert.That(IsInsideViewport(cargoViewport), Is.True);
            Assert.That(IsInsideViewport(truckViewport), Is.True);

            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockReturn(), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.Lane, WorkAreaTransitionPhase.None, 120);
        }

        /// <summary>
        /// 상하차 presenter는 세션 큐를 기준으로 최대 5개의 슬롯만 표시해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockPresenterShowsOnlyActiveQueueSlots()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);

            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            yield return null;
            yield return null;

            var presenter = Object.FindFirstObjectByType<LoadingDockMiniGamePresenter>();
            var environment = Object.FindFirstObjectByType<LoadingDockEnvironmentAuthoring>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            var cargoViewRoot = presenter.transform.Find("LoadingDockCargoViewRoot");
            Assert.That(cargoViewRoot, Is.Not.Null);
            Assert.That(cargoViewRoot.childCount, Is.EqualTo(PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount));

            var snapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();
            Assert.That(snapshot.ActiveSlotCount, Is.EqualTo(PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount));
            Assert.That(snapshot.BacklogCount, Is.EqualTo(2));
        }

        /// <summary>
        /// 레인 상태에서도 상하차 적재장 뷰는 세션 큐와 동기화된 채 유지되어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockPresenterShowsQueuedCargoBeforeDockEntry()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            yield return null;
            yield return null;

            var presenter = Object.FindFirstObjectByType<LoadingDockMiniGamePresenter>();
            var cargoViewRoot = presenter != null ? presenter.transform.Find("LoadingDockCargoViewRoot") : null;
            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            var runtimeState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();

            Assert.That(runtimeState.CurrentArea, Is.EqualTo(WorkAreaType.Lane));
            Assert.That(runtimeState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.None));
            Assert.That(activeEntries.Length, Is.EqualTo(3));
            Assert.That(cargoViewRoot, Is.Not.Null);
            Assert.That(cargoViewRoot.childCount, Is.EqualTo(3));
        }

        /// <summary>
        /// delivered 처리 시 활성 슬롯은 즉시 보충되고 backlog는 FIFO로 감소해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockQueueRefillsSlotAfterDelivery()
        {
            PrototypeSessionRuntime.ResetPrototypeState();

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);

            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            Assert.That(PrototypeSessionRuntime.TryDeliverLoadingDockCargo(activeEntries[2].EntryId, out var deliveredEntry), Is.True);
            Assert.That(deliveredEntry.EntryId, Is.EqualTo(3));

            var snapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();
            activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();

            Assert.That(snapshot.ActiveSlotCount, Is.EqualTo(PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount));
            Assert.That(snapshot.BacklogCount, Is.EqualTo(1));
            Assert.That(activeEntries[0].EntryId, Is.EqualTo(1));
            Assert.That(activeEntries[1].EntryId, Is.EqualTo(2));
            Assert.That(activeEntries[2].SlotIndex, Is.EqualTo(2));
            Assert.That(activeEntries[2].EntryId, Is.EqualTo(6));
            Assert.That(activeEntries[3].EntryId, Is.EqualTo(4));
            Assert.That(activeEntries[4].EntryId, Is.EqualTo(5));
            yield return null;
        }

        /// <summary>
        /// presenter delivery 훅은 슬롯 cube를 release하고 다음 backlog 물류로 즉시 채워야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockPresenterDeliveryRefillsVisibleSlot()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);

            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            yield return null;

            var presenter = Object.FindFirstObjectByType<LoadingDockMiniGamePresenter>();
            Assert.That(presenter, Is.Not.Null);

            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            Assert.That(presenter.TryDeliverCargoEntry(activeEntries[0].EntryId), Is.True);
            yield return null;

            var snapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();
            var cargoViewRoot = presenter.transform.Find("LoadingDockCargoViewRoot");
            activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();

            Assert.That(snapshot.ActiveSlotCount, Is.EqualTo(PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount));
            Assert.That(snapshot.BacklogCount, Is.Zero);
            Assert.That(activeEntries[0].SlotIndex, Is.EqualTo(0));
            Assert.That(activeEntries[0].EntryId, Is.EqualTo(6));
            Assert.That(cargoViewRoot, Is.Not.Null);
            Assert.That(cargoViewRoot.childCount, Is.EqualTo(PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount));
        }

        /// <summary>
        /// 큐가 비어 있는 상태로 상하차에 진입해도 오류 없이 empty state만 보여야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockPresenterSupportsEmptyQueueState()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            yield return null;

            var presenter = Object.FindFirstObjectByType<LoadingDockMiniGamePresenter>();
            var cargoViewRoot = presenter != null ? presenter.transform.Find("LoadingDockCargoViewRoot") : null;
            var snapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();

            Assert.That(presenter, Is.Not.Null);
            Assert.That(snapshot.TotalCount, Is.Zero);
            Assert.That(cargoViewRoot == null || cargoViewRoot.childCount == 0, Is.True);
        }

        /// <summary>
        /// 상하차 구역을 나갔다 다시 들어와도 세션 큐와 표시 슬롯이 복원되어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadingDockQueuePersistsAcrossDockReentry()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Standard);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Fragile);
            PrototypeSessionRuntime.EnqueueLoadingDockCargo(LoadingDockCargoKind.Heavy);

            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            yield return null;

            var firstActiveEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockReturn(), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.Lane, WorkAreaTransitionPhase.None, 120);

            Assert.That(PrototypeSessionRuntime.TryRequestLoadingDockEntry(catalog), Is.True);
            yield return WaitForDockPhase(catalog, WorkAreaType.LoadingDock, WorkAreaTransitionPhase.ActiveInLoadingDock, 120);
            yield return null;

            var presenter = Object.FindFirstObjectByType<LoadingDockMiniGamePresenter>();
            var cargoViewRoot = presenter != null ? presenter.transform.Find("LoadingDockCargoViewRoot") : null;
            var reenteredEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();

            Assert.That(reenteredEntries.Length, Is.EqualTo(3));
            Assert.That(reenteredEntries[0].SlotIndex, Is.EqualTo(firstActiveEntries[0].SlotIndex));
            Assert.That(reenteredEntries[0].EntryId, Is.EqualTo(firstActiveEntries[0].EntryId));
            Assert.That(reenteredEntries[1].SlotIndex, Is.EqualTo(firstActiveEntries[1].SlotIndex));
            Assert.That(reenteredEntries[1].EntryId, Is.EqualTo(firstActiveEntries[1].EntryId));
            Assert.That(reenteredEntries[2].SlotIndex, Is.EqualTo(firstActiveEntries[2].SlotIndex));
            Assert.That(reenteredEntries[2].EntryId, Is.EqualTo(firstActiveEntries[2].EntryId));
            Assert.That(cargoViewRoot, Is.Not.Null);
            Assert.That(cargoViewRoot.childCount, Is.EqualTo(3));
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

        private static void FreezeRandomSpawns(EntityManager entityManager)
        {
            var spawnEntity = entityManager.CreateEntityQuery(typeof(SpawnTimerState)).GetSingletonEntity();
            var spawnTimer = entityManager.GetComponentData<SpawnTimerState>(spawnEntity);
            spawnTimer.Remaining = 999f;
            entityManager.SetComponentData(spawnEntity, spawnTimer);
        }

        private static void SpawnCargo(
            EntityManager entityManager,
            int laneIndex,
            float zPosition,
            int weight,
            int reward,
            int penalty,
            float moveSpeed,
            LoadingDockCargoKind kind = LoadingDockCargoKind.Standard)
        {
            var laneEntity = entityManager.CreateEntityQuery(typeof(LaneLayout)).GetSingletonEntity();
            var laneXs = entityManager.GetBuffer<LaneWorldXElement>(laneEntity);
            var cargoConfig = entityManager.CreateEntityQuery(typeof(CargoConfig)).GetSingleton<CargoConfig>();
            var laneX = BattleLaneUtility.GetLaneX(laneXs, laneIndex);
            var cargoEntity = entityManager.CreateEntity();

            entityManager.AddComponentData(cargoEntity, new CargoTag());
            entityManager.AddComponentData(cargoEntity, new LaneIndex { Value = laneIndex });
            entityManager.AddComponentData(cargoEntity, new VerticalPosition { Value = zPosition });
            entityManager.AddComponentData(cargoEntity, new MoveSpeed { Value = moveSpeed });
            entityManager.AddComponentData(cargoEntity, new CargoWeight { Value = weight });
            entityManager.AddComponentData(cargoEntity, new CargoReward { Value = reward });
            entityManager.AddComponentData(cargoEntity, new CargoPenalty { Value = penalty });
            entityManager.AddComponentData(cargoEntity, new CargoKind { Value = kind });
            entityManager.AddComponentData(cargoEntity, LocalTransform.FromPositionRotationScale(
                new float3(laneX, cargoConfig.Y, zPosition),
                quaternion.identity,
                1f));
        }

        private static GameObject FindLaneCargoView()
        {
            var bridge = Object.FindFirstObjectByType<BattlePresentationBridge>();
            if (bridge == null)
            {
                return null;
            }

            for (var index = 0; index < bridge.transform.childCount; index += 1)
            {
                var child = bridge.transform.GetChild(index);
                if (child.name.StartsWith("CargoView_", System.StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }

            return null;
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

        private static IEnumerator WaitForDockPhase(
            MetaProgressionCatalogAsset catalog,
            WorkAreaType expectedArea,
            WorkAreaTransitionPhase expectedPhase,
            int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame += 1)
            {
                var state = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
                if (state.CurrentArea == expectedArea && state.TransitionPhase == expectedPhase)
                {
                    yield break;
                }

                yield return null;
            }

            var finalState = PrototypeSessionRuntime.GetLoadingDockRuntimeState(catalog);
            Assert.That(finalState.CurrentArea, Is.EqualTo(expectedArea));
            Assert.That(finalState.TransitionPhase, Is.EqualTo(expectedPhase));
        }

        private static IEnumerator WaitForCameraPose(
            Camera camera,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            int maxFrames,
            float positionTolerance = 0.4f,
            float rotationTolerance = 1.5f)
        {
            for (var frame = 0; frame < maxFrames; frame += 1)
            {
                if (Vector3.Distance(camera.transform.position, expectedPosition) <= positionTolerance &&
                    Quaternion.Angle(camera.transform.rotation, expectedRotation) <= rotationTolerance)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.That(Vector3.Distance(camera.transform.position, expectedPosition), Is.LessThanOrEqualTo(positionTolerance));
            Assert.That(Quaternion.Angle(camera.transform.rotation, expectedRotation), Is.LessThanOrEqualTo(rotationTolerance));
        }

        private static CinemachineCamera CreatePassiveVirtualCamera(
            string name,
            Vector3 position,
            Quaternion rotation,
            float fieldOfView,
            int priority)
        {
            var cameraObject = new GameObject(name, typeof(CinemachineCamera));
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            var virtualCamera = cameraObject.GetComponent<CinemachineCamera>();
            var lens = virtualCamera.Lens;
            lens.FieldOfView = fieldOfView;
            virtualCamera.Lens = lens;
            virtualCamera.Priority = priority;
            return virtualCamera;
        }

        private static bool IsInsideViewport(Vector3 viewportPoint)
        {
            return viewportPoint.z > 0f &&
                   viewportPoint.x is >= 0f and <= 1f &&
                   viewportPoint.y is >= 0f and <= 1f;
        }
    }
}
