using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 현재 2단계 리듬 물류 루프의 phase, 통계, 입력 가이드를 HUD로 표시합니다.
    /// </summary>
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        [SerializeField] private Text infoText;
        [SerializeField] private Text laneText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text controlsText;

        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _popupTitleStyle;

        public void Bind(Text info, Text lane, Text result, Text controls)
        {
            infoText = info;
            laneText = lane;
            resultText = result;
            controlsText = controls;
        }

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
                $"Work {stage.RemainingWorkTime:0.0}s\nIncome {stats.TotalMoney}\nApproved {stats.ApprovedCargoCount}\nRejected {stats.RejectedCargoCount}";
            laneText.text =
                $"Phase {DescribePhase(phaseState.CurrentPhase)}\nPending Approval {phaseState.PendingApprovalCount}\nPending Route {phaseState.PendingRouteCount}\nCorrect {stats.CorrectRouteCount} / Misroute {stats.MisrouteCount} / Return {stats.ReturnCount}";
            if (controlsText != null)
            {
                controlsText.text = BuildControlsText(phaseState.CurrentPhase);
            }

            if (outcome.HasOutcome == 0)
            {
                resultText.gameObject.SetActive(false);
                return;
            }

            resultText.text = "SHIFT COMPLETE";
            resultText.gameObject.SetActive(true);
        }

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

        private static string BuildControlsText(BattleMiniGamePhase phase)
        {
            return phase switch
            {
                BattleMiniGamePhase.Approval => "Approval: Z = Reject / X = Approve / Esc = Pause",
                BattleMiniGamePhase.RouteSelection => "Route: 1 Air / 2 Sea / 3 Rail / 4 Truck / 5 Return / Esc = Pause",
                _ => "Esc = Pause"
            };
        }

        private static string DescribePhase(BattleMiniGamePhase phase)
        {
            return phase switch
            {
                BattleMiniGamePhase.Approval => "Approval",
                BattleMiniGamePhase.RouteSelection => "Route Selection",
                _ => "Completed"
            };
        }

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
