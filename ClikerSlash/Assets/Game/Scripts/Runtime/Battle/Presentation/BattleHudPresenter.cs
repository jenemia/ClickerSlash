using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        private GUIStyle _labelStyle;
        private GUIStyle _resultStyle;

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
                ComponentType.ReadOnly<BattleOutcomeState>());
            var playerQuery = entityManager.CreateEntityQuery(
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

            EnsureStyles();
            GUILayout.BeginArea(new Rect(20f, 20f, 320f, 180f), GUI.skin.box);
            GUILayout.Label($"Time: {stages[0].RemainingTime:0.0}", _labelStyle);
            GUILayout.Label($"Lives: {lives[0].Value}", _labelStyle);
            GUILayout.Label($"Lane: {lanes[0].Value + 1}", _labelStyle);
            GUILayout.Label("Controls: A/D or Left/Right", _labelStyle);
            GUILayout.EndArea();

            if (outcomes[0].HasOutcome == 0)
            {
                return;
            }

            var resultText = outcomes[0].IsVictory != 0 ? "VICTORY" : "DEFEAT";
            GUI.Label(new Rect(20f, 220f, 320f, 60f), resultText, _resultStyle);
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

            _resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.25f) }
            };
        }
    }
}
