using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
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
            PrototypeSessionRuntime.IncreaseHealthLevel();

            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();
            var stage = entityManager.CreateEntityQuery(typeof(StageProgressState)).GetSingleton<StageProgressState>();
            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var expectedDuration = battleConfig.BaseWorkDurationSeconds + battleConfig.HealthDurationBonusSeconds;

            Assert.That(stage.RemainingWorkTime, Is.EqualTo(expectedDuration).Within(0.01f));
            Assert.That(stats.ResolvedWorkDurationSeconds, Is.EqualTo(expectedDuration).Within(0.01f));
            Assert.That(PrototypeSessionRuntime.ResolvedWorkDurationSeconds, Is.EqualTo(expectedDuration).Within(0.01f));
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
            Assert.That(stats.ProcessedCargoCount, Is.EqualTo(1));
            Assert.That(stats.TotalMoney, Is.EqualTo(75));
            Assert.That(comboState.Current, Is.EqualTo(1));
            Assert.That(comboState.Max, Is.EqualTo(1));

            yield return new WaitForSeconds(battleConfig.HandleDurationSeconds + 0.05f);

            SpawnCargo(entityManager, laneIndex, battleConfig.JudgmentLineZ, maxHandleWeight + 2, 90, 40, 0f);
            yield return null;
            yield return null;

            comboState = entityManager.GetComponentData<ComboState>(playerEntity);
            stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            Assert.That(stats.MissedCargoCount, Is.EqualTo(1));
            Assert.That(stats.TotalMoney, Is.EqualTo(35));
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

            PrototypeSessionRuntime.IncreaseHealthLevel();
            hubPresenter.LoadPrototypeBattle();
            yield return WaitForSceneAndWorld(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();
            var stage = entityManager.CreateEntityQuery(typeof(StageProgressState)).GetSingleton<StageProgressState>();
            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var expectedDuration = battleConfig.BaseWorkDurationSeconds + battleConfig.HealthDurationBonusSeconds;

            Assert.That(stage.RemainingWorkTime, Is.EqualTo(expectedDuration).Within(0.01f));
            Assert.That(stats.TotalMoney, Is.EqualTo(0));
            Assert.That(stats.ProcessedCargoCount, Is.EqualTo(0));
            Assert.That(stats.MissedCargoCount, Is.EqualTo(0));
            Assert.That(stats.CurrentCombo, Is.EqualTo(0));
            Assert.That(PrototypeSessionRuntime.HealthLevel, Is.EqualTo(2));
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

        private static void SpawnCargo(EntityManager entityManager, int laneIndex, float zPosition, int weight, int reward, int penalty, float moveSpeed)
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
            entityManager.AddComponentData(cargoEntity, LocalTransform.FromPositionRotationScale(
                new float3(laneX, cargoConfig.Y, zPosition),
                quaternion.identity,
                1f));
        }
    }
}
