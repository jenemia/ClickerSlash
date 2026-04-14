using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// Battle 씬의 프레젠터들을 additive Env facade와 명시적으로 연결합니다.
    /// </summary>
    public sealed class BattleEnvironmentBinder : MonoBehaviour
    {
        private BattlePresentationBridge _bridge;
        private LoadingDockMiniGamePresenter _miniGamePresenter;
        private LoadingDockConveyorPresenter _conveyorPresenter;
        private LaneCargoPalletStackPresenter _palletStackPresenter;
        private LaneCargoTransferPresenter _transferPresenter;
        private BattleViewAuthoring _battleView;
        private LoadingDockEnvironmentAuthoring _boundEnvironment;
        private string _lastBindingFailureReason;

        /// <summary>
        /// 프레젠터 참조와 authoritative 환경을 감시하고, 바인딩 대상이 바뀌면 즉시 다시 연결합니다.
        /// </summary>
        private void Update()
        {
            if (!TryResolvePresenterReferences())
            {
                return;
            }

            var environment = ResolveAuthoritativeEnvironment();
            if (environment == null || environment == _boundEnvironment)
            {
                return;
            }

            if (!TryValidateEnvironment(environment))
            {
                return;
            }

            _bridge.BindLoadingDockEnvironment(environment);
            _miniGamePresenter.BindEnvironment(environment);
            _conveyorPresenter.BindSceneReferences(environment);
            _palletStackPresenter.BindEnvironment(environment);
            _transferPresenter.BindSceneReferences(environment, _bridge, _palletStackPresenter, _battleView);
            BattleEnvironmentBindingRuntime.Register(environment);
            _boundEnvironment = environment;
        }

        /// <summary>
        /// 바인더가 비활성화될 때 현재 등록된 authoritative 환경만 레지스트리에서 제거합니다.
        /// </summary>
        private void OnDisable()
        {
            BattleEnvironmentBindingRuntime.Clear(_boundEnvironment);
            _boundEnvironment = null;
            _lastBindingFailureReason = null;
        }

        /// <summary>
        /// 같은 GameObject에 배치된 프레젠터들과 전투 뷰 참조를 한 번만 찾아 캐시합니다.
        /// </summary>
        private bool TryResolvePresenterReferences()
        {
            _bridge ??= GetComponent<BattlePresentationBridge>();
            _miniGamePresenter ??= GetComponent<LoadingDockMiniGamePresenter>();
            _conveyorPresenter ??= GetComponent<LoadingDockConveyorPresenter>();
            _palletStackPresenter ??= GetComponent<LaneCargoPalletStackPresenter>();
            _transferPresenter ??= GetComponent<LaneCargoTransferPresenter>();
            _battleView ??= FindFirstObjectByType<BattleViewAuthoring>();

            return _bridge != null &&
                   _miniGamePresenter != null &&
                   _conveyorPresenter != null &&
                   _palletStackPresenter != null &&
                   _transferPresenter != null &&
                   _battleView != null;
        }

        /// <summary>
        /// additive Env 씬의 authoritative 환경 facade만 선택하고, 누락 시 조용히 생성하지 않습니다.
        /// </summary>
        private LoadingDockEnvironmentAuthoring ResolveAuthoritativeEnvironment()
        {
            var envScene = SceneManager.GetSceneByName(PrototypeSessionRuntime.BattleEnvironmentSceneName);
            if (!envScene.IsValid() || !envScene.isLoaded)
            {
                LogBindingFailureOnce("PrototypeEvn additive scene is not loaded.");
                return null;
            }

            var allEnvironments = FindObjectsByType<LoadingDockEnvironmentAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            LoadingDockEnvironmentAuthoring selected = null;
            var duplicateCount = 0;
            foreach (var candidate in allEnvironments)
            {
                if (candidate == null || candidate.gameObject.scene != envScene)
                {
                    continue;
                }

                duplicateCount += 1;
                if (selected == null)
                {
                    selected = candidate;
                }
            }

            if (selected == null)
            {
                LogBindingFailureOnce("PrototypeEvn scene does not contain LoadingDockEnvironmentAuthoring.");
                return null;
            }

            if (duplicateCount > 1)
            {
                LogBindingFailureOnce("PrototypeEvn scene contains multiple LoadingDockEnvironmentAuthoring components.");
            }

            return selected;
        }

        /// <summary>
        /// 바인딩 전 strict contract를 검사하고, 누락된 필드가 있으면 같은 이유를 한 번만 기록합니다.
        /// </summary>
        private bool TryValidateEnvironment(LoadingDockEnvironmentAuthoring environment)
        {
            if (_battleView == null)
            {
                LogBindingFailureOnce("BattleViewAuthoring is missing, so the environment contract cannot be validated.");
                return false;
            }

            if (environment.TryValidateStrictContract(_battleView.LaneWorldXs.Count, out var errorMessage))
            {
                _lastBindingFailureReason = null;
                return true;
            }

            LogBindingFailureOnce(errorMessage, environment);
            return false;
        }

        /// <summary>
        /// 같은 실패 이유를 매 프레임 반복하지 않도록 마지막 로그와 비교해 한 번만 출력합니다.
        /// </summary>
        private void LogBindingFailureOnce(string reason, Object context = null)
        {
            if (_lastBindingFailureReason == reason)
            {
                return;
            }

            _lastBindingFailureReason = reason;
            if (context != null)
            {
                Debug.LogError($"BattleEnvironmentBinder: {reason}", context);
                return;
            }

            Debug.LogError($"BattleEnvironmentBinder: {reason}", this);
        }
    }

    /// <summary>
    /// 기존 Battle 씬에도 새 binder/presenter 조합이 자동으로 붙도록 보정합니다.
    /// </summary>
    public static class BattleEnvironmentAutoSetup
    {
        /// <summary>
        /// 기존 전투 씬 자산에도 새 binder/presenter가 자동으로 붙도록 로드 직후 보정합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var battleScene = SceneManager.GetSceneByName(PrototypeSessionRuntime.BattleSceneName);
            if (!battleScene.IsValid() || !battleScene.isLoaded)
            {
                return;
            }

            var root = GameObject.Find("BattlePresentationRoot");
            if (root == null)
            {
                return;
            }

            GetOrAddComponent<BattleEnvironmentBinder>(root);
            GetOrAddComponent<LaneCargoPalletStackPresenter>(root);
            GetOrAddComponent<LaneCargoTransferPresenter>(root);
        }

        /// <summary>
        /// 대상 GameObject에 컴포넌트가 없을 때만 추가합니다.
        /// </summary>
        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent<T>(out var existing) ? existing : target.AddComponent<T>();
        }
    }
}
