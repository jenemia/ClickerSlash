using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ClikerSlash.Tests.PlayMode
{
    /// <summary>
    /// 프로토타입 세션 루프와 전투-허브 전환 핵심 경로를 검증하는 플레이 모드 테스트입니다.
    /// </summary>
    public class PrototypeSessionLoopPlayModeTests
    {
        /// <summary>
        /// 먼저 적을 처치해 콤보를 만든 뒤, 방어선 돌파가 발생하면 콤보가 초기화되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator ComboResetsWhenDefenseLineIsBroken()
        {
            PrototypeSessionRuntime.ClearLastBattleResult();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            FreezeRandomSpawns(entityManager);

            var playerEntity = entityManager.CreateEntityQuery(typeof(PlayerTag)).GetSingletonEntity();
            var laneIndex = entityManager.GetComponentData<LaneIndex>(playerEntity).Value;
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();

            // 먼저 플레이어 레인에 안전한 처치 대상을 만들어 콤보를 0이 아닌 값으로 올립니다.
            SpawnEnemy(entityManager, laneIndex, -1.2f, 0f);
            yield return new WaitForSeconds(0.6f);

            var comboState = entityManager.GetComponentData<ComboState>(playerEntity);
            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            Assert.That(comboState.Current, Is.EqualTo(1));
            Assert.That(stats.KillCount, Is.EqualTo(1));

            var laneLayout = entityManager.CreateEntityQuery(typeof(LaneLayout)).GetSingleton<LaneLayout>();
            var breachLane = (laneIndex + 1) % laneLayout.LaneCount;
            // 그다음 다른 레인에서 강제로 돌파를 일으켜 타기팅 시스템이 처치하기 전에 라이프 감소 처리 시스템이 보게 합니다.
            SpawnEnemy(entityManager, breachLane, battleConfig.DefenseLineZ - 0.2f, 0f);
            yield return null;

            var lifeState = entityManager.GetComponentData<LifeState>(playerEntity);
            comboState = entityManager.GetComponentData<ComboState>(playerEntity);
            stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();

            Assert.That(lifeState.Value, Is.EqualTo(battleConfig.StartingLives - 1));
            Assert.That(comboState.Current, Is.EqualTo(0));
            Assert.That(stats.CurrentCombo, Is.EqualTo(0));
        }

        /// <summary>
        /// 승리 결과가 허브까지 전달되고, 다시 전투에 들어오면 런타임 상태가 초기화되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator VictoryTransfersToHubAndBattleReentryResetsRuntimeState()
        {
            PrototypeSessionRuntime.ClearLastBattleResult();
            yield return LoadSceneAndWait(PrototypeSessionRuntime.BattleSceneName);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            FreezeRandomSpawns(entityManager);

            var stageEntity = entityManager.CreateEntityQuery(typeof(StageProgressState)).GetSingletonEntity();
            var stageProgress = entityManager.GetComponentData<StageProgressState>(stageEntity);
            // 전체 전투 시간을 기다리지 않고 승리 경로를 빠르게 통과시키기 위해 남은 시간을 거의 0으로 줄입니다.
            stageProgress.RemainingTime = 0.01f;
            entityManager.SetComponentData(stageEntity, stageProgress);

            yield return new WaitUntil(() =>
            {
                var outcome = entityManager.CreateEntityQuery(typeof(BattleOutcomeState)).GetSingleton<BattleOutcomeState>();
                return outcome.HasOutcome != 0;
            });

            var resultSnapshot = PrototypeSessionRuntime.LastBattleResult;
            Assert.That(PrototypeSessionRuntime.HasLastBattleResult, Is.True);
            Assert.That(resultSnapshot.IsVictory, Is.EqualTo(1));

            yield return LoadSceneAndWait(PrototypeSessionRuntime.HubSceneName);
            var hubPresenter = Object.FindFirstObjectByType<PrototypeHubPresenter>();
            Assert.That(hubPresenter, Is.Not.Null);

            // 실제 플레이어가 누르는 허브 버튼과 같은 경로로 전투 재진입을 수행합니다.
            hubPresenter.LoadPrototypeBattle();
            yield return WaitForSceneAndWorld(PrototypeSessionRuntime.BattleSceneName);

            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var playerEntity = entityManager.CreateEntityQuery(typeof(PlayerTag)).GetSingletonEntity();
            var comboState = entityManager.GetComponentData<ComboState>(playerEntity);
            var lifeState = entityManager.GetComponentData<LifeState>(playerEntity);
            var battleConfig = entityManager.CreateEntityQuery(typeof(BattleConfig)).GetSingleton<BattleConfig>();
            var stats = entityManager.CreateEntityQuery(typeof(BattleSessionStatsState)).GetSingleton<BattleSessionStatsState>();
            var outcomeState = entityManager.CreateEntityQuery(typeof(BattleOutcomeState)).GetSingleton<BattleOutcomeState>();

            Assert.That(comboState.Current, Is.EqualTo(0));
            Assert.That(comboState.Max, Is.EqualTo(0));
            Assert.That(lifeState.Value, Is.EqualTo(battleConfig.StartingLives));
            Assert.That(stats.KillCount, Is.EqualTo(0));
            Assert.That(outcomeState.HasOutcome, Is.EqualTo(0));
        }

        /// <summary>
        /// 씬을 로드하고, 테스트가 상호작용할 수 있을 만큼 씬과 ECS 월드가 준비될 때까지 기다립니다.
        /// </summary>
        private static IEnumerator LoadSceneAndWait(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
            yield return WaitForSceneAndWorld(sceneName);
        }

        /// <summary>
        /// 엔진이 요청한 씬을 활성화하고 기본 ECS 월드까지 만든 시점까지 대기합니다.
        /// </summary>
        private static IEnumerator WaitForSceneAndWorld(string sceneName)
        {
            yield return null;
            yield return null;
            yield return new WaitUntil(() =>
                SceneManager.GetActiveScene().name == sceneName &&
                World.DefaultGameObjectInjectionWorld != null &&
                World.DefaultGameObjectInjectionWorld.IsCreated);
        }

        /// <summary>
        /// 테스트가 적 구성을 완전히 통제할 수 있도록 스폰 타이머를 충분히 먼 미래로 밀어 둡니다.
        /// </summary>
        private static void FreezeRandomSpawns(EntityManager entityManager)
        {
            var spawnEntity = entityManager.CreateEntityQuery(typeof(SpawnTimerState)).GetSingletonEntity();
            var spawnTimer = entityManager.GetComponentData<SpawnTimerState>(spawnEntity);
            spawnTimer.Remaining = 999f;
            entityManager.SetComponentData(spawnEntity, spawnTimer);
        }

        /// <summary>
        /// 결정론적 테스트를 위해 요청한 레인, 위치, 속도로 최소 구성의 적 엔티티를 만듭니다.
        /// </summary>
        private static void SpawnEnemy(EntityManager entityManager, int laneIndex, float zPosition, float moveSpeed)
        {
            var laneEntity = entityManager.CreateEntityQuery(typeof(LaneLayout)).GetSingletonEntity();
            var laneXs = entityManager.GetBuffer<LaneWorldXElement>(laneEntity);
            var enemyConfig = entityManager.CreateEntityQuery(typeof(EnemyConfig)).GetSingleton<EnemyConfig>();
            var laneX = BattleLaneUtility.GetLaneX(laneXs, laneIndex);
            var enemyEntity = entityManager.CreateEntity();

            // 현재 테스트 대상 전투 시스템이 읽는 최소 데이터만 적 엔티티에 넣습니다.
            entityManager.AddComponentData(enemyEntity, new EnemyTag());
            entityManager.AddComponentData(enemyEntity, new LaneIndex { Value = laneIndex });
            entityManager.AddComponentData(enemyEntity, new VerticalPosition { Value = zPosition });
            entityManager.AddComponentData(enemyEntity, new MoveSpeed { Value = moveSpeed });
            entityManager.AddComponentData(enemyEntity, new EnemyHealth { Value = 1 });
            entityManager.AddComponentData(enemyEntity, LocalTransform.FromPositionRotationScale(
                new float3(laneX, enemyConfig.Y, zPosition),
                quaternion.identity,
                1f));
        }
    }
}
