using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        [SerializeField] private Text infoText;
        [SerializeField] private Text laneText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text controlsText;

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

            infoText.text = $"Time {stages[0].RemainingTime:0.0}\nLives {lives[0].Value}";
            laneText.text = $"Lane {lanes[0].Value + 1} / 4";

            if (outcomes[0].HasOutcome == 0)
            {
                resultText.gameObject.SetActive(false);
                return;
            }

            var outcomeText = outcomes[0].IsVictory != 0 ? "VICTORY" : "DEFEAT";
            resultText.text = outcomeText;
            this.resultText.gameObject.SetActive(true);
        }
    }
}
