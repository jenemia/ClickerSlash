using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 마지막 프로토타입 전투 결과를 보여주고 다시 전투에 들어가게 하는 경량 허브 화면을 그립니다.
    /// </summary>
    public sealed class PrototypeHubPresenter : MonoBehaviour
    {
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;

        /// <summary>
        /// 허브의 액션 버튼을 통해 프로토타입 전투 씬을 로드합니다.
        /// </summary>
        public void LoadPrototypeBattle()
        {
            PrototypeSceneNavigator.LoadBattleScene();
        }

        /// <summary>
        /// 마지막 전투 요약과 전투 진입 버튼을 화면에 그립니다.
        /// </summary>
        private void OnGUI()
        {
            EnsureStyles();

            GUILayout.BeginArea(new Rect(20f, 20f, 420f, 320f), GUI.skin.box);
            GUILayout.Label("PROTOTYPE HUB", _titleStyle);
            GUILayout.Space(12f);

            if (PrototypeSessionRuntime.HasLastBattleResult)
            {
                // 전투 직후에는 ECS를 직접 조회하지 않고 씬 전환 전에 저장한 핸드오프 스냅샷을 사용합니다.
                var snapshot = PrototypeSessionRuntime.LastBattleResult;
                GUILayout.Label(snapshot.IsVictory != 0 ? "Last Result: Victory" : "Last Result: Defeat", _bodyStyle);
                GUILayout.Label($"Kills: {snapshot.KillCount}", _bodyStyle);
                GUILayout.Label($"Max Combo: {snapshot.MaxCombo}", _bodyStyle);
                GUILayout.Label($"Survival: {snapshot.SurvivalTimeSeconds:0.0}s", _bodyStyle);
                GUILayout.Label($"Lives Left: {snapshot.RemainingLives}", _bodyStyle);
            }
            else
            {
                GUILayout.Label("No battle result captured yet.", _bodyStyle);
            }

            GUILayout.Space(18f);
            if (GUILayout.Button("Enter Prototype Battle", _buttonStyle, GUILayout.Height(48f)))
            {
                LoadPrototypeBattle();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// 허브 패널에서 사용하는 IMGUI 스타일을 지연 생성합니다.
        /// </summary>
        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
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
