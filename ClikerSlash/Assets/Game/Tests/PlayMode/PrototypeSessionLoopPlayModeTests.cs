using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ClikerSlash.Tests.PlayMode
{
    /// <summary>
    /// 3구역 동시 진행 물류 루프의 포커스, handoff, 정지 규칙을 검증합니다.
    /// </summary>
    public class PrototypeSessionLoopPlayModeTests
    {
        [UnityTest]
        public IEnumerator BattleSceneStartsWithApprovalFocusAndThreeCameras()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var snapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            Assert.That(snapshot.FocusedArea, Is.EqualTo(BattleMiniGameArea.Approval));
            Assert.That(snapshot.PendingApprovalCount, Is.GreaterThan(0));
            Assert.That(Object.FindFirstObjectByType<ApprovalMiniGamePresenter>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<RouteLaneSelectionPresenter>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<LoadingDockMiniGamePresenter>(), Is.Not.Null);
            Assert.That(GameObject.Find("ApprovalVirtualCamera"), Is.Not.Null);
            Assert.That(GameObject.Find("LaneVirtualCamera"), Is.Not.Null);
            Assert.That(GameObject.Find("LoadingDockVirtualCamera"), Is.Not.Null);
        }

        [Test]
        public void FocusCycleWrapsAcrossThreeMiniGameAreas()
        {
            PrototypeSessionRuntime.ResetPrototypeState();

            Assert.That(PrototypeSessionRuntime.GetFocusedMiniGameArea(), Is.EqualTo(BattleMiniGameArea.Approval));
            PrototypeSessionRuntime.FocusNextMiniGameArea();
            Assert.That(PrototypeSessionRuntime.GetFocusedMiniGameArea(), Is.EqualTo(BattleMiniGameArea.RouteSelection));
            PrototypeSessionRuntime.FocusNextMiniGameArea();
            Assert.That(PrototypeSessionRuntime.GetFocusedMiniGameArea(), Is.EqualTo(BattleMiniGameArea.LoadingDock));
            PrototypeSessionRuntime.FocusNextMiniGameArea();
            Assert.That(PrototypeSessionRuntime.GetFocusedMiniGameArea(), Is.EqualTo(BattleMiniGameArea.Approval));

            PrototypeSessionRuntime.FocusPreviousMiniGameArea();
            Assert.That(PrototypeSessionRuntime.GetFocusedMiniGameArea(), Is.EqualTo(BattleMiniGameArea.LoadingDock));
        }

        [UnityTest]
        public IEnumerator ApprovalCargoDoesNotResolveWhileAnotherAreaIsFocused()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 101,
                Kind = LoadingDockCargoKind.General,
                Weight = 4,
                Reward = 80,
                Penalty = 30
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = GetBattleConfig(entityManager);
            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            Assert.That(approvalCargo, Is.Not.EqualTo(Entity.Null));

            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.FocusNextMiniGameArea();
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
            yield return null;
            yield return null;

            Assert.That(FindActiveCargo(entityManager, BattleMiniGamePhase.Approval), Is.Not.EqualTo(Entity.Null));
            Assert.That(PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().PendingRouteCount, Is.Zero);

            PrototypeSessionRuntime.FocusPreviousMiniGameArea();
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
            yield return null;

            Assert.That(PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().PendingRouteCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnfocusedApprovalCargoKeepsMovingUntilJudgmentThenStops()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 111,
                Kind = LoadingDockCargoKind.Fragile,
                Weight = 4,
                Reward = 75,
                Penalty = 30
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = GetBattleConfig(entityManager);
            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            Assert.That(approvalCargo, Is.Not.EqualTo(Entity.Null));

            entityManager.SetComponentData(approvalCargo, new MoveSpeed { Value = 6f });
            SetCargoPosition(entityManager, approvalCargo, battleConfig.JudgmentLineZ + 0.1f);
            PrototypeSessionRuntime.FocusNextMiniGameArea();

            yield return null;

            var transform = entityManager.GetComponentData<LocalTransform>(approvalCargo);
            Assert.That(transform.Position.z, Is.EqualTo(battleConfig.JudgmentLineZ).Within(0.001f));
            Assert.That(FindActiveCargo(entityManager, BattleMiniGamePhase.Approval), Is.Not.EqualTo(Entity.Null));
        }

        [UnityTest]
        public IEnumerator ApprovalAndRouteCargoCanExistAtTheSameTime()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(
                new ApprovalCargoSnapshot
                {
                    EntryId = 121,
                    Kind = LoadingDockCargoKind.General,
                    Weight = 4,
                    Reward = 80,
                    Penalty = 30
                },
                new ApprovalCargoSnapshot
                {
                    EntryId = 122,
                    Kind = LoadingDockCargoKind.Frozen,
                    Weight = 5,
                    Reward = 95,
                    Penalty = 40
                });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = GetBattleConfig(entityManager);
            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);

            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
            yield return null;

            ForceImmediateSpawn(entityManager);
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection, 20);
            yield return WaitForApprovalRespawn(entityManager, battleConfig, 30);

            Assert.That(FindActiveCargo(entityManager, BattleMiniGamePhase.Approval), Is.Not.EqualTo(Entity.Null));
            Assert.That(FindActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection), Is.Not.EqualTo(Entity.Null));
        }

        [UnityTest]
        public IEnumerator RouteSelectionHandsOffPhysicalRouteCargoToLoadingDockQueue()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return PrepareBattleSceneWithPlan(new ApprovalCargoSnapshot
            {
                EntryId = 131,
                Kind = LoadingDockCargoKind.General,
                Weight = 4,
                Reward = 90,
                Penalty = 30
            });

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = GetBattleConfig(entityManager);

            var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
            SetCargoPositionAndStop(entityManager, approvalCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueApprovalInput(ApprovalDecision.Approve);
            yield return null;

            ForceImmediateSpawn(entityManager);
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection, 20);

            PrototypeSessionRuntime.FocusNextMiniGameArea();
            var routeCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.RouteSelection);
            SetCargoPositionAndStop(entityManager, routeCargo, battleConfig.JudgmentLineZ);
            PrototypeSessionRuntime.QueueRouteInput(CargoRouteLane.Air);
            yield return null;

            var loadingDockSnapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();
            Assert.That(loadingDockSnapshot.TotalCount, Is.EqualTo(1));
            Assert.That(loadingDockSnapshot.ActiveSlotCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LoadingDockFocusUsesLoadingDockRuntimeState()
        {
            PrototypeSessionRuntime.ResetPrototypeState();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            PrototypeSessionRuntime.FocusNextMiniGameArea();
            PrototypeSessionRuntime.FocusNextMiniGameArea();

            var dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            Assert.That(dockState.CurrentArea, Is.EqualTo(WorkAreaType.LoadingDock));
            Assert.That(dockState.TransitionPhase, Is.EqualTo(WorkAreaTransitionPhase.ActiveInLoadingDock));
        }

        /// <summary>
        /// 테스트용 계획을 주입한 뒤 첫 승인 물류가 스폰될 때까지 씬을 준비합니다.
        /// </summary>
        private static IEnumerator PrepareBattleSceneWithPlan(params ApprovalCargoSnapshot[] plannedCargoEntries)
        {
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            ClearAllCargo(entityManager);
            PrototypeSessionRuntime.InitializeRhythmCargoPlan(plannedCargoEntries);
            SyncRhythmPhaseState(entityManager);
            ForceImmediateSpawn(entityManager);

            yield return null;
            yield return WaitForActiveCargo(entityManager, BattleMiniGamePhase.Approval, 20);
        }

        /// <summary>
        /// 대상 씬이 활성화되고 기본 월드가 살아날 때까지 대기합니다.
        /// </summary>
        private static IEnumerator LoadSceneAndWait(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return null;
            yield return null;
            yield return new WaitUntil(() =>
                SceneManager.GetActiveScene().name == sceneName &&
                World.DefaultGameObjectInjectionWorld != null &&
                World.DefaultGameObjectInjectionWorld.IsCreated);
        }

        /// <summary>
        /// 기존 컨베이어 물류 엔티티를 모두 지워 테스트 시작 상태를 고정합니다.
        /// </summary>
        private static void ClearAllCargo(EntityManager entityManager)
        {
            using var cargoQuery = entityManager.CreateEntityQuery(typeof(CargoTag));
            entityManager.DestroyEntity(cargoQuery);
        }

        /// <summary>
        /// 지정한 구역의 현재 활성 물류 엔티티 하나를 찾습니다.
        /// </summary>
        private static Entity FindActiveCargo(EntityManager entityManager, BattleMiniGamePhase phase)
        {
            using var cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoMiniGamePhase>());
            using var entities = cargoQuery.ToEntityArray(Allocator.Temp);
            using var phases = cargoQuery.ToComponentDataArray<CargoMiniGamePhase>(Allocator.Temp);

            for (var index = 0; index < entities.Length; index += 1)
            {
                if (phases[index].Value == phase)
                {
                    return entities[index];
                }
            }

            return Entity.Null;
        }

        /// <summary>
        /// 물류를 판정선에 바로 세워 놓기 위해 위치를 바꾸고 이동을 중지합니다.
        /// </summary>
        private static void SetCargoPositionAndStop(EntityManager entityManager, Entity cargoEntity, float zPosition)
        {
            entityManager.SetComponentData(cargoEntity, new MoveSpeed { Value = 0f });
            SetCargoPosition(entityManager, cargoEntity, zPosition);
        }

        /// <summary>
        /// 물류의 논리 위치와 시각 위치를 같은 Z 좌표로 강제로 맞춥니다.
        /// </summary>
        private static void SetCargoPosition(EntityManager entityManager, Entity cargoEntity, float zPosition)
        {
            entityManager.SetComponentData(cargoEntity, new VerticalPosition { Value = zPosition });
            var transform = entityManager.GetComponentData<LocalTransform>(cargoEntity);
            transform.Position.z = zPosition;
            entityManager.SetComponentData(cargoEntity, transform);
        }

        /// <summary>
        /// 승인/레인선택 두 컨베이어의 스폰 타이머를 모두 즉시 만료시킵니다.
        /// </summary>
        private static void ForceImmediateSpawn(EntityManager entityManager)
        {
            var spawnEntity = entityManager.CreateEntityQuery(typeof(SpawnTimerState)).GetSingletonEntity();
            var spawnTimer = entityManager.GetComponentData<SpawnTimerState>(spawnEntity);
            spawnTimer.ApprovalRemaining = 0f;
            spawnTimer.RouteRemaining = 0f;
            entityManager.SetComponentData(spawnEntity, spawnTimer);
        }

        /// <summary>
        /// 런타임 스냅샷을 ECS용 리듬 상태 싱글턴에 즉시 반영합니다.
        /// </summary>
        private static void SyncRhythmPhaseState(EntityManager entityManager)
        {
            var rhythmEntity = entityManager.CreateEntityQuery(typeof(RhythmPhaseState)).GetSingletonEntity();
            var snapshot = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot();
            entityManager.SetComponentData(rhythmEntity, new RhythmPhaseState
            {
                CurrentPhase = snapshot.CurrentPhase,
                FocusedArea = snapshot.FocusedArea,
                PendingApprovalCount = snapshot.PendingApprovalCount,
                PendingRouteCount = snapshot.PendingRouteCount,
                PendingLoadingDockCount = snapshot.PendingLoadingDockCount,
                HasActiveCargo = snapshot.HasActiveCargo ? (byte)1 : (byte)0,
                HasActiveApprovalCargo = snapshot.HasApprovalCargo ? (byte)1 : (byte)0,
                HasActiveRouteCargo = snapshot.HasRouteCargo ? (byte)1 : (byte)0
            });
        }

        /// <summary>
        /// 지정한 phase의 활성 물류가 나타날 때까지 최대 프레임 수만큼 대기합니다.
        /// </summary>
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

        /// <summary>
        /// 승인 물류 하나를 처리한 뒤 다음 승인 물류가 다시 스폰될 때까지 기다립니다.
        /// </summary>
        private static IEnumerator WaitForApprovalRespawn(EntityManager entityManager, BattleConfig battleConfig, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame += 1)
            {
                var approvalCargo = FindActiveCargo(entityManager, BattleMiniGamePhase.Approval);
                if (approvalCargo != Entity.Null)
                {
                    var transform = entityManager.GetComponentData<LocalTransform>(approvalCargo);
                    if (transform.Position.z > battleConfig.JudgmentLineZ)
                    {
                        yield break;
                    }
                }

                yield return null;
            }

            Assert.That(FindActiveCargo(entityManager, BattleMiniGamePhase.Approval), Is.Not.EqualTo(Entity.Null));
        }

        /// <summary>
        /// 현재 씬의 전투 설정 싱글턴을 읽어 반환합니다.
        /// </summary>
        private static BattleConfig GetBattleConfig(EntityManager entityManager)
        {
            return entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();
        }
    }
}
