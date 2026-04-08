using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 매 프레임 전투 ECS 상태를 읽어 가로형 HUD와 결과 패널을 함께 갱신합니다.
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
                ComponentType.ReadOnly<BattleOutcomeState>());
            using var playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LaneIndex>(),
                ComponentType.ReadOnly<LifeState>(),
                ComponentType.ReadOnly<LocalTransform>());

            if (battleQuery.IsEmptyIgnoreFilter || playerQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var stages = battleQuery.ToComponentDataArray<StageProgressState>(Allocator.Temp);
            using var outcomes = battleQuery.ToComponentDataArray<BattleOutcomeState>(Allocator.Temp);
            using var lanes = playerQuery.ToComponentDataArray<LaneIndex>(Allocator.Temp);
            using var lives = playerQuery.ToComponentDataArray<LifeState>(Allocator.Temp);
            using var playerEntities = playerQuery.ToEntityArray(Allocator.Temp);

            var comboText = string.Empty;
            var playerEntity = playerEntities[0];
            if (entityManager.HasComponent<ComboState>(playerEntity))
            {
                var combo = entityManager.GetComponentData<ComboState>(playerEntity);
                comboText = $"\nCombo {combo.Current}";
            }

            infoText.text = $"Time {stages[0].RemainingTime:0.0}\nLives {lives[0].Value}{comboText}";
            laneText.text = $"Lane {lanes[0].Value + 1} / 4";

            if (outcomes[0].HasOutcome == 0)
            {
                resultText.gameObject.SetActive(false);
                return;
            }

            resultText.text = outcomes[0].IsVictory != 0 ? "VICTORY" : "DEFEAT";
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
                ComponentType.ReadOnly<StageProgressState>(),
                ComponentType.ReadOnly<BattleOutcomeState>());
            if (battleQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var outcomes = battleQuery.ToComponentDataArray<BattleOutcomeState>(Allocator.Temp);
            if (outcomes[0].HasOutcome == 0)
            {
                return;
            }

            if (battleQuery.GetSingletonEntity() is var battleEntity &&
                entityManager.HasComponent<BattleSessionStatsState>(battleEntity))
            {
                var stats = entityManager.GetComponentData<BattleSessionStatsState>(battleEntity);
                EnsureStyles();

                GUILayout.BeginArea(new Rect(20f, 280f, 360f, 220f), GUI.skin.box);
                GUILayout.Label($"Kills: {stats.KillCount}", _labelStyle);
                GUILayout.Label($"Max Combo: {stats.MaxCombo}", _labelStyle);
                GUILayout.Label($"Survival: {stats.SurvivalTimeSeconds:0.0}s", _labelStyle);
                GUILayout.Label($"Lives Left: {stats.RemainingLives}", _labelStyle);

                GUILayout.Space(12f);
                if (GUILayout.Button("Go To Hub", _buttonStyle, GUILayout.Height(42f)))
                {
                    PrototypeSceneNavigator.LoadHubScene();
                }

                GUILayout.EndArea();
            }
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
        }
    }
}
