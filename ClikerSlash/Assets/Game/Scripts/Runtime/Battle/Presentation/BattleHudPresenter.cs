using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 현재 3구역 동시 진행 물류 루프의 포커스, 통계, 입력 가이드를 HUD로 표시합니다.
    /// </summary>
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        [Header("HUD 참조")]
        [Tooltip("좌측 정보 패널 텍스트입니다.")]
        [SerializeField] private Text infoText;
        [Tooltip("상단 구역 상태 패널 텍스트입니다.")]
        [SerializeField] private Text laneText;
        [Tooltip("세션 종료 결과 텍스트입니다.")]
        [SerializeField] private Text resultText;
        [Tooltip("현재 구역 조작 키를 보여 주는 텍스트입니다.")]
        [SerializeField] private Text controlsText;

        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _popupTitleStyle;

        /// <summary>
        /// 씬 빌더가 생성한 HUD 텍스트 참조를 프레젠터에 연결합니다.
        /// </summary>
        public void Bind(Text info, Text lane, Text result, Text controls)
        {
            infoText = info;
            laneText = lane;
            resultText = result;
            controlsText = controls;
        }

        /// <summary>
        /// 매 프레임 전투 통계와 현재 포커스 구역을 읽어 HUD 문자열을 다시 그립니다.
        /// </summary>
        private void Update()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || infoText == null || laneText == null || resultText == null)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var battleQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<StageProgressState>(),
                ComponentType.ReadOnly<BattleOutcomeState>(),
                ComponentType.ReadOnly<BattleSessionStatsState>(),
                ComponentType.ReadOnly<RhythmPhaseState>());
            if (battleQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var stage = battleQuery.GetSingleton<StageProgressState>();
            var outcome = battleQuery.GetSingleton<BattleOutcomeState>();
            var stats = battleQuery.GetSingleton<BattleSessionStatsState>();
            var phaseState = battleQuery.GetSingleton<RhythmPhaseState>();

            HandlePauseInput(outcome);

            infoText.text =
                $"Work {stage.RemainingWorkTime:0.0}s\nIncome {stats.TotalMoney}\nApproved {stats.ApprovedCargoCount}\nRejected {stats.RejectedCargoCount}\nDock Queue {phaseState.PendingLoadingDockCount}";
            laneText.text =
                $"Focus {DescribeArea(phaseState.FocusedArea)}\nPending Approval {phaseState.PendingApprovalCount}\nPending Route {phaseState.PendingRouteCount}\nPending Dock {phaseState.PendingLoadingDockCount}\nCorrect {stats.CorrectRouteCount} / Misroute {stats.MisrouteCount} / Return {stats.ReturnCount}";
            if (controlsText != null)
            {
                controlsText.text = BuildControlsText(phaseState.FocusedArea);
            }

            if (outcome.HasOutcome == 0)
            {
                resultText.gameObject.SetActive(false);
                return;
            }

            resultText.text = "SHIFT COMPLETE";
            resultText.gameObject.SetActive(true);
        }

        /// <summary>
        /// 진행 중 세션에서만 ESC 일시정지 입력을 처리합니다.
        /// </summary>
        private static void HandlePauseInput(BattleOutcomeState outcome)
        {
            if (outcome.HasOutcome != 0)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            PrototypeSessionRuntime.TogglePauseMenu();
        }

        /// <summary>
        /// 현재 포커스된 구역에 맞는 키 가이드를 생성합니다.
        /// </summary>
        private static string BuildControlsText(BattleMiniGameArea area)
        {
            return area switch
            {
                BattleMiniGameArea.Approval => "Q/E Camera  Z Reject  X Approve  Esc Pause",
                BattleMiniGameArea.RouteSelection => "Q/E Camera  1 Air  2 Sea  3 Rail  4 Truck  5 Return  Esc Pause",
                BattleMiniGameArea.LoadingDock => "Q/E Camera  Mouse Deliver  Esc Pause",
                _ => "Q/E Camera  Esc Pause"
            };
        }

        /// <summary>
        /// 포커스 구역 이름을 HUD 친화 문자열로 변환합니다.
        /// </summary>
        private static string DescribeArea(BattleMiniGameArea area)
        {
            return area switch
            {
                BattleMiniGameArea.Approval => "Approval",
                BattleMiniGameArea.RouteSelection => "Route Selection",
                BattleMiniGameArea.LoadingDock => "Loading Dock",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// 세션 종료 이후에는 허브 복귀 팝업을 IMGUI로 표시합니다.
        /// </summary>
        private void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            using var battleQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattleOutcomeState>(),
                ComponentType.ReadOnly<BattleSessionStatsState>());
            if (battleQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var outcome = battleQuery.GetSingleton<BattleOutcomeState>();
            if (outcome.HasOutcome == 0 && PrototypeSessionRuntime.IsPauseMenuOpen)
            {
                DrawPausePopup();
                return;
            }

            if (outcome.HasOutcome == 0)
            {
                return;
            }

            var stats = battleQuery.GetSingleton<BattleSessionStatsState>();
            EnsureStyles();

            var panelWidth = Mathf.Clamp(Screen.width * 0.28f, 420f, 520f);
            var panelHeight = Mathf.Clamp(Screen.height * 0.40f, 320f, 420f);
            var panelRect = new Rect(20f, Screen.height - panelHeight - 20f, panelWidth, panelHeight);

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label($"Income: {stats.TotalMoney}", _labelStyle);
            GUILayout.Label($"Approved: {stats.ApprovedCargoCount}", _labelStyle);
            GUILayout.Label($"Rejected: {stats.RejectedCargoCount}", _labelStyle);
            GUILayout.Label($"Correct Routes: {stats.CorrectRouteCount}", _labelStyle);
            GUILayout.Label($"Misroutes: {stats.MisrouteCount}", _labelStyle);
            GUILayout.Label($"Returns: {stats.ReturnCount}", _labelStyle);
            GUILayout.Label($"Misses: {stats.MissedCargoCount}", _labelStyle);
            GUILayout.Label($"Worked: {stats.WorkedTimeSeconds:0.0}s", _labelStyle);

            GUILayout.FlexibleSpace();
            GUILayout.Space(12f);
            if (GUILayout.Button("Return To Hub", _buttonStyle, GUILayout.Height(42f)))
            {
                PrototypeSceneNavigator.LoadHubScene();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// 전투 중 ESC로 연 일시정지 팝업을 렌더링합니다.
        /// </summary>
        private void DrawPausePopup()
        {
            EnsureStyles();

            var popupWidth = Mathf.Min(Screen.width * 0.32f, 520f);
            var popupHeight = Mathf.Min(Screen.height * 0.24f, 260f);
            var popupRect = new Rect(
                (Screen.width - popupWidth) * 0.5f,
                (Screen.height - popupHeight) * 0.5f,
                popupWidth,
                popupHeight);

            GUILayout.BeginArea(popupRect, GUI.skin.window);
            GUILayout.Space(8f);
            GUILayout.Label("PAUSED", _popupTitleStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Resume the shift or return to the hub.", _labelStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Resume", _buttonStyle, GUILayout.Height(40f)))
            {
                PrototypeSessionRuntime.ClosePauseMenu();
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Return To Hub", _buttonStyle, GUILayout.Height(40f)))
            {
                PrototypeSessionRuntime.ClosePauseMenu();
                PrototypeSceneNavigator.LoadHubScene();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// IMGUI 스타일 객체를 한 번만 생성해 재사용합니다.
        /// </summary>
        private void EnsureStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                wordWrap = true
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22
            };
            _popupTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
