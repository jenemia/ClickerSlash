using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 매 프레임 전투 ECS 상태를 읽어 프로토타입 전투 HUD와 결과 패널을 그립니다.
    /// </summary>
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        private GUIStyle _labelStyle;
        private GUIStyle _resultStyle;
        private GUIStyle _buttonStyle;

        /// <summary>
        /// 전투 중에는 상태 패널을, 결과가 생긴 뒤에는 결과 패널까지 확장해 그립니다.
        /// </summary>
        private void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            var battleQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<StageProgressState>(),
                ComponentType.ReadOnly<BattleOutcomeState>(),
                ComponentType.ReadOnly<BattleSessionStatsState>());
            var playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LaneIndex>(),
                ComponentType.ReadOnly<LifeState>(),
                ComponentType.ReadOnly<ComboState>(),
                ComponentType.ReadOnly<LocalTransform>());

            if (battleQuery.IsEmptyIgnoreFilter || playerQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var stages = battleQuery.ToComponentDataArray<StageProgressState>(Allocator.Temp);
            using var outcomes = battleQuery.ToComponentDataArray<BattleOutcomeState>(Allocator.Temp);
            using var sessionStats = battleQuery.ToComponentDataArray<BattleSessionStatsState>(Allocator.Temp);
            using var lanes = playerQuery.ToComponentDataArray<LaneIndex>(Allocator.Temp);
            using var lives = playerQuery.ToComponentDataArray<LifeState>(Allocator.Temp);
            using var combos = playerQuery.ToComponentDataArray<ComboState>(Allocator.Temp);

            EnsureStyles();
            // 상단 패널은 전투 내내 유지되어 시간, 라이프, 콤보 압박을 계속 읽을 수 있게 합니다.
            GUILayout.BeginArea(new Rect(20f, 20f, 320f, 220f), GUI.skin.box);
            GUILayout.Label($"Time: {stages[0].RemainingTime:0.0}", _labelStyle);
            GUILayout.Label($"Lives: {lives[0].Value}", _labelStyle);
            GUILayout.Label($"Combo: {combos[0].Current}", _labelStyle);
            GUILayout.Label($"Lane: {lanes[0].Value + 1}", _labelStyle);
            GUILayout.Label("Controls: A/D or Left/Right", _labelStyle);
            GUILayout.EndArea();

            if (outcomes[0].HasOutcome == 0)
            {
                return;
            }

            var resultText = outcomes[0].IsVictory != 0 ? "VICTORY" : "DEFEAT";
            GUI.Label(new Rect(20f, 220f, 360f, 60f), resultText, _resultStyle);

            // 결과 패널은 캡처된 세션 통계를 요약하고 허브로 넘어가는 액션을 제공합니다.
            GUILayout.BeginArea(new Rect(20f, 280f, 360f, 220f), GUI.skin.box);
            GUILayout.Label($"Kills: {sessionStats[0].KillCount}", _labelStyle);
            GUILayout.Label($"Max Combo: {sessionStats[0].MaxCombo}", _labelStyle);
            GUILayout.Label($"Survival: {sessionStats[0].SurvivalTimeSeconds:0.0}s", _labelStyle);
            GUILayout.Label($"Lives Left: {sessionStats[0].RemainingLives}", _labelStyle);

            GUILayout.Space(12f);
            if (GUILayout.Button("Go To Hub", _buttonStyle, GUILayout.Height(42f)))
            {
                PrototypeSceneNavigator.LoadHubScene();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// HUD에서 사용하는 IMGUI 스타일을 필요할 때 한 번만 생성합니다.
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
                normal = { textColor = Color.white }
            };

            _resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.25f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
