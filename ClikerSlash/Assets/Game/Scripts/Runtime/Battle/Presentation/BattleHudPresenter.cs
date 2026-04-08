using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 매 프레임 ECS 상태를 읽어 물류 HUD와 결과 패널을 함께 갱신합니다.
    /// </summary>
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        [SerializeField] private Text infoText;
        [SerializeField] private Text laneText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text controlsText;

        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public void Bind(Text info, Text lane, Text result, Text controls)
        {
            infoText = info;
            laneText = lane;
            resultText = result;
            controlsText = controls;
        }

        private void Awake()
        {
            if (controlsText != null)
            {
                controlsText.text = "Controls: A / D or Left / Right";
            }
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
                ComponentType.ReadOnly<BattleSessionStatsState>());
            using var playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LaneIndex>(),
                ComponentType.ReadOnly<MaxHandleWeight>(),
                ComponentType.ReadOnly<ComboState>());
            using var laneQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<LaneLayout>());

            if (battleQuery.IsEmptyIgnoreFilter || playerQuery.IsEmptyIgnoreFilter || laneQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var stage = battleQuery.GetSingleton<StageProgressState>();
            var outcome = battleQuery.GetSingleton<BattleOutcomeState>();
            var stats = battleQuery.GetSingleton<BattleSessionStatsState>();
            var playerEntity = playerQuery.GetSingletonEntity();
            var lane = entityManager.GetComponentData<LaneIndex>(playerEntity);
            var maxHandleWeight = entityManager.GetComponentData<MaxHandleWeight>(playerEntity);
            var combo = entityManager.GetComponentData<ComboState>(playerEntity);
            var laneLayout = laneQuery.GetSingleton<LaneLayout>();

            infoText.text =
                $"Work {stage.RemainingWorkTime:0.0}s\nMoney {stats.TotalMoney}\nCombo {combo.Current}";
            laneText.text =
                $"Lane {lane.Value + 1} / {laneLayout.LaneCount}\nMax Weight {maxHandleWeight.Value}";

            if (outcome.HasOutcome == 0)
            {
                resultText.gameObject.SetActive(false);
                return;
            }

            resultText.text = "SHIFT COMPLETE";
            resultText.gameObject.SetActive(true);
        }

        private void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var battleQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattleOutcomeState>(),
                ComponentType.ReadOnly<BattleSessionStatsState>());
            if (battleQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var outcome = battleQuery.GetSingleton<BattleOutcomeState>();
            if (outcome.HasOutcome == 0)
            {
                return;
            }

            var stats = battleQuery.GetSingleton<BattleSessionStatsState>();
            EnsureStyles();

            var panelWidth = Mathf.Clamp(Screen.width * 0.28f, 420f, 520f);
            var panelHeight = Mathf.Clamp(Screen.height * 0.36f, 300f, 360f);
            var panelRect = new Rect(
                20f,
                Screen.height - panelHeight - 20f,
                panelWidth,
                panelHeight);

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label($"Money: {stats.TotalMoney}", _labelStyle);
            GUILayout.Label($"Processed: {stats.ProcessedCargoCount}", _labelStyle);
            GUILayout.Label($"Missed: {stats.MissedCargoCount}", _labelStyle);
            GUILayout.Label($"Max Combo: {stats.MaxCombo}", _labelStyle);
            GUILayout.Label($"Worked: {stats.WorkedTimeSeconds:0.0}s", _labelStyle);

            GUILayout.FlexibleSpace();
            GUILayout.Space(12f);
            if (GUILayout.Button("Return To Hub", _buttonStyle, GUILayout.Height(42f)))
            {
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
                normal = { textColor = Color.white }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };

            _buttonStyle.margin = new RectOffset(0, 0, 0, 0);
        }
    }
}
