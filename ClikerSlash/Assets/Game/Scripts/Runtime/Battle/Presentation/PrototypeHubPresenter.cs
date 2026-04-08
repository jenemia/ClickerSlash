using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 마지막 작업 결과와 임시 체력 메타 UI를 보여주는 경량 허브 화면을 그립니다.
    /// </summary>
    public sealed class PrototypeHubPresenter : MonoBehaviour
    {
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _buttonStyle;

        public void LoadPrototypeBattle()
        {
            PrototypeSceneNavigator.LoadBattleScene();
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUILayout.BeginArea(new Rect(20f, 20f, 480f, 420f), GUI.skin.box);
            GUILayout.Label("LOGISTICS HUB", _titleStyle);
            GUILayout.Space(12f);

            GUILayout.Label($"Health Lv: {PrototypeSessionRuntime.HealthLevel}", _bodyStyle);
            GUILayout.Label(
                $"Next Work Duration: {PrototypeSessionRuntime.PreviewResolvedWorkDuration():0.0}s",
                _bodyStyle);

            GUILayout.Space(10f);
            if (GUILayout.Button("Increase Health", _buttonStyle, GUILayout.Height(42f)))
            {
                PrototypeSessionRuntime.IncreaseHealthLevel();
            }

            GUILayout.Space(18f);
            if (PrototypeSessionRuntime.HasLastBattleResult)
            {
                var snapshot = PrototypeSessionRuntime.LastBattleResult;
                GUILayout.Label("Last Shift Result", _bodyStyle);
                GUILayout.Label($"Money: {snapshot.TotalMoney}", _bodyStyle);
                GUILayout.Label($"Processed: {snapshot.ProcessedCargoCount}", _bodyStyle);
                GUILayout.Label($"Missed: {snapshot.MissedCargoCount}", _bodyStyle);
                GUILayout.Label($"Max Combo: {snapshot.MaxCombo}", _bodyStyle);
                GUILayout.Label($"Worked: {snapshot.WorkedTimeSeconds:0.0}s", _bodyStyle);
            }
            else
            {
                GUILayout.Label("No shift result captured yet.", _bodyStyle);
            }

            GUILayout.Space(18f);
            if (GUILayout.Button("Start Prototype Shift", _buttonStyle, GUILayout.Height(48f)))
            {
                LoadPrototypeBattle();
            }

            GUILayout.EndArea();
        }

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
