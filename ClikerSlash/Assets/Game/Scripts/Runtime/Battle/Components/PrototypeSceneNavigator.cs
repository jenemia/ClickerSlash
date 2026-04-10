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
}
