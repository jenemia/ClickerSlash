using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 전투 루프에서 사용하는 씬 전환을 한곳에 모읍니다.
    /// </summary>
    public static class PrototypeSceneNavigator
    {
        /// <summary>
        /// 마지막 전투 결과를 보여주는 경량 허브 씬을 로드합니다.
        /// </summary>
        public static void LoadHubScene()
        {
            PrototypeSessionRuntime.ClosePauseMenu();
            PrototypeSessionRuntime.ClearLoadingDockQueue();
            SceneManager.LoadScene(PrototypeSessionRuntime.HubSceneName);
        }

        /// <summary>
        /// 새 전투 진입 요청을 기록한 뒤 프로토타입 전투 씬을 로드합니다.
        /// </summary>
        public static void LoadBattleScene()
        {
            // 이 런타임 플래그는 전투 씬이 올라오면서 부트스트랩 단계에서 소비됩니다.
            PrototypeSessionRuntime.ClosePauseMenu();
            PrototypeSessionRuntime.RequestBattleEntry();
            SceneManager.LoadScene(PrototypeSessionRuntime.BattleSceneName);
        }
    }

    /// <summary>
    /// 전투 씬이 열릴 때 패시브 환경 씬을 additive로 붙이고, 활성 씬 소유권을 전투 씬에 고정합니다.
    /// </summary>
    public static class PrototypeBattleEnvironmentSceneBootstrap
    {
        private static bool _hasWarnedMissingEnvironmentScene;

        /// <summary>
        /// 전투용 환경 씬이 현재 로드되어 있는지 반환합니다.
        /// </summary>
        public static bool IsBattleEnvironmentLoaded()
        {
            var environmentScene = SceneManager.GetSceneByName(PrototypeSessionRuntime.BattleEnvironmentSceneName);
            return environmentScene.IsValid() && environmentScene.isLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _hasWarnedMissingEnvironmentScene = false;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallHooks()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == PrototypeSessionRuntime.BattleSceneName)
            {
                EnsureBattleEnvironmentScene();
                return;
            }

            if (scene.name != PrototypeSessionRuntime.BattleEnvironmentSceneName)
            {
                return;
            }

            EnsureBattleSceneActive();
            if (LightmapSettings.lightProbes != null)
            {
                LightProbes.Tetrahedralize();
            }
        }

        private static void EnsureBattleEnvironmentScene()
        {
            if (IsBattleEnvironmentLoaded())
            {
                EnsureBattleSceneActive();
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(PrototypeSessionRuntime.BattleEnvironmentSceneName))
            {
                if (_hasWarnedMissingEnvironmentScene)
                {
                    return;
                }

                Debug.LogWarning(
                    $"Battle environment scene '{PrototypeSessionRuntime.BattleEnvironmentSceneName}' is not available in build settings. " +
                    "PrototypeBattle will continue without the additive environment scene.");
                _hasWarnedMissingEnvironmentScene = true;
                return;
            }

            SceneManager.LoadSceneAsync(PrototypeSessionRuntime.BattleEnvironmentSceneName, LoadSceneMode.Additive);
        }

        private static void EnsureBattleSceneActive()
        {
            var battleScene = SceneManager.GetSceneByName(PrototypeSessionRuntime.BattleSceneName);
            if (!battleScene.IsValid() || !battleScene.isLoaded)
            {
                return;
            }

            if (SceneManager.GetActiveScene() == battleScene)
            {
                return;
            }

            SceneManager.SetActiveScene(battleScene);
        }
    }
}
