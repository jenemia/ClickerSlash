using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// PrototypeHub의 스킬트리, 메타 요약, 최근 결과 패널을 UGUI로 갱신합니다.
    /// </summary>
    public sealed class PrototypeHubPresenter : MonoBehaviour
    {
        [SerializeField] private PrototypeHubSkillTreeView skillTreeView;
        [SerializeField] private WrapLabel titleLabel;
        [SerializeField] private WrapLabel healthLabel;
        [SerializeField] private WrapLabel durationLabel;
        [SerializeField] private WrapLabel weightLabel;
        [SerializeField] private WrapLabel laneLabel;
        [SerializeField] private WrapLabel balanceLabel;
        [SerializeField] private WrapLabel earnedLabel;
        [SerializeField] private WrapLabel spentLabel;
        [SerializeField] private WrapLabel controlsLabel;
        [SerializeField] private WrapLabel resultLabel;
        [SerializeField] private WrapLabel selectionTitleLabel;
        [SerializeField] private WrapLabel selectionBodyLabel;
        [SerializeField] private Button startButton;

        private MetaProgressionCatalogAsset _catalog;
        private string _selectedNodeId;
        private bool _treeBuilt;

        /// <summary>
        /// 씬 빌더가 생성한 참조를 한 번에 연결합니다.
        /// </summary>
        public void Bind(
            PrototypeHubSkillTreeView treeView,
            WrapLabel title,
            WrapLabel health,
            WrapLabel duration,
            WrapLabel weight,
            WrapLabel lane,
            WrapLabel balance,
            WrapLabel earned,
            WrapLabel spent,
            WrapLabel controls,
            WrapLabel result,
            WrapLabel selectionTitle,
            WrapLabel selectionBody,
            Button launchButton)
        {
            skillTreeView = treeView;
            titleLabel = title;
            healthLabel = health;
            durationLabel = duration;
            weightLabel = weight;
            laneLabel = lane;
            balanceLabel = balance;
            earnedLabel = earned;
            spentLabel = spent;
            controlsLabel = controls;
            resultLabel = result;
            selectionTitleLabel = selectionTitle;
            selectionBodyLabel = selectionBody;
            startButton = launchButton;
        }

        /// <summary>
        /// 전투 씬 진입 전에 메타 상태를 한 번 더 정리합니다.
        /// </summary>
        public void LoadPrototypeBattle()
        {
            EnsureCatalog();
            PrototypeSessionRuntime.EnsureMetaProgressionInitialized(_catalog);
            PrototypeSceneNavigator.LoadBattleScene();
        }

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(LoadPrototypeBattle);
            }

            if (skillTreeView != null)
            {
                skillTreeView.NodeClicked += HandleNodeClicked;
            }
        }

        private void Start()
        {
            RefreshHub();
        }

        private void OnEnable()
        {
            RefreshHub();
        }

        private void OnDestroy()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(LoadPrototypeBattle);
            }

            if (skillTreeView != null)
            {
                skillTreeView.NodeClicked -= HandleNodeClicked;
            }
        }

        /// <summary>
        /// 노드 선택을 먼저 기록하고, 업그레이드 가능할 때만 레벨을 한 단계 올립니다.
        /// </summary>
        private void HandleNodeClicked(string nodeId)
        {
            _selectedNodeId = nodeId;
            EnsureCatalog();
            PrototypeSessionRuntime.EnsureMetaProgressionInitialized(_catalog);

            var snapshot = PrototypeSessionRuntime.GetMetaProgressionSnapshot();
            var nodeStatus = MetaProgressionCalculator.DescribeNode(snapshot, _catalog, nodeId);
            if (nodeStatus != null && nodeStatus.canUpgrade)
            {
                PrototypeSessionRuntime.TryUpgradeNode(nodeId, _catalog);
            }

            RefreshHub();
        }

        /// <summary>
        /// 현재 메타 진행과 최근 결과를 읽어 허브 전체 UI를 다시 그립니다.
        /// </summary>
        private void RefreshHub()
        {
            EnsureCatalog();
            PrototypeSessionRuntime.EnsureMetaProgressionInitialized(_catalog);

            if (string.IsNullOrWhiteSpace(_selectedNodeId))
            {
                _selectedNodeId = ResolveDefaultSelectedNodeId();
            }

            if (!_treeBuilt && skillTreeView != null)
            {
                skillTreeView.Build(_catalog);
                _treeBuilt = true;
            }

            var snapshot = PrototypeSessionRuntime.GetMetaProgressionSnapshot();
            var resolvedProgression = PrototypeSessionRuntime.GetResolvedMetaProgression();
            var currency = PrototypeSessionRuntime.GetCurrencySnapshot();

            if (titleLabel != null) titleLabel.text = "PROTOTYPE HUB";
            if (healthLabel != null) healthLabel.text = $"Health Lv {PrototypeSessionRuntime.HealthLevel}";
            if (durationLabel != null) durationLabel.text = $"Next Work {PrototypeSessionRuntime.PreviewResolvedWorkDuration():0.0}s";
            if (weightLabel != null) weightLabel.text = $"Max Weight {resolvedProgression.MaxHandleWeight}";
            if (laneLabel != null) laneLabel.text = $"Active Lanes {resolvedProgression.ActiveLaneCount}";
            if (balanceLabel != null) balanceLabel.text = $"Balance {currency.currentBalance}";
            if (earnedLabel != null) earnedLabel.text = $"Earned {currency.totalBattleEarned}";
            if (spentLabel != null) spentLabel.text = $"Spent {currency.totalSkillSpent}";
            if (controlsLabel != null) controlsLabel.text = "상단 탭 전환 / 드래그 이동 / 휠 줌 / 클릭 업그레이드";
            if (resultLabel != null) resultLabel.text = BuildResultSummary();

            if (skillTreeView != null)
            {
                skillTreeView.Refresh(snapshot, _catalog, _selectedNodeId);
            }

            RefreshSelectionPanel(snapshot);
        }

        /// <summary>
        /// 선택된 노드의 현재 레벨, 선행 조건, 효과를 요약 패널에 표시합니다.
        /// </summary>
        private void RefreshSelectionPanel(PlayerMetaProgressionSnapshot snapshot)
        {
            var selectedNodeStatus = MetaProgressionCalculator.DescribeNode(snapshot, _catalog, _selectedNodeId);
            if (selectedNodeStatus == null)
            {
                if (selectionTitleLabel != null) selectionTitleLabel.text = "선택된 노드 없음";
                if (selectionBodyLabel != null) selectionBodyLabel.text = "중심 허브에서 줄기를 선택해 메타 성장을 진행하세요.";
                return;
            }

            _catalog.TryGetNodeDefinition(_selectedNodeId, out var nodeDefinition);
            if (selectionTitleLabel != null)
            {
                selectionTitleLabel.text = selectedNodeStatus.displayName;
            }
            if (selectionBodyLabel != null)
            {
                selectionBodyLabel.text =
                    $"탭: {selectedNodeStatus.tabDisplayName}\n" +
                    $"브랜치: {selectedNodeStatus.branchDisplayName}\n" +
                    $"티어: {selectedNodeStatus.tier}\n" +
                    $"레벨: {selectedNodeStatus.currentLevel}/{selectedNodeStatus.maxLevel}\n" +
                    $"코스트: {selectedNodeStatus.cost}\n" +
                    $"{selectedNodeStatus.affordabilitySummary}\n" +
                    $"{selectedNodeStatus.prerequisiteSummary}\n" +
                    $"효과: {BuildEffectSummary(nodeDefinition)}";
            }
        }

        /// <summary>
        /// 최근 전투 결과가 있으면 숫자를 묶어 보여주고, 없으면 안내 문구를 표시합니다.
        /// </summary>
        private string BuildResultSummary()
        {
            if (!PrototypeSessionRuntime.HasLastBattleResult)
            {
                return "최근 작업 기록 없음";
            }

            var snapshot = PrototypeSessionRuntime.LastBattleResult;
            return
                $"최근 결과\n" +
                $"Money {snapshot.TotalMoney}\n" +
                $"Processed {snapshot.ProcessedCargoCount}\n" +
                $"Missed {snapshot.MissedCargoCount}\n" +
                $"Max Combo {snapshot.MaxCombo}\n" +
                $"Worked {snapshot.WorkedTimeSeconds:0.0}s";
        }

        /// <summary>
        /// 효과 정의를 짧은 자연어로 요약합니다.
        /// </summary>
        private string BuildEffectSummary(SkillNodeDefinition nodeDefinition)
        {
            if (nodeDefinition?.effects == null || nodeDefinition.effects.Count == 0)
            {
                return "효과 없음";
            }

            var effectTexts = new string[nodeDefinition.effects.Count];
            for (var index = 0; index < nodeDefinition.effects.Count; index += 1)
            {
                effectTexts[index] = DescribeEffect(nodeDefinition.effects[index]);
            }

            return string.Join(", ", effectTexts);
        }

        /// <summary>
        /// 각 효과 타입을 허브 설명 문자열로 변환합니다.
        /// </summary>
        private string DescribeEffect(SkillEffectDefinition effect)
        {
            if (effect == null)
            {
                return "효과 없음";
            }

            switch (effect.effectType)
            {
                case SkillEffectType.SessionDurationAddSeconds:
                    return $"작업 시간 +{effect.floatValue:0.##}초";

                case SkillEffectType.MaxHandleWeightAdd:
                    return $"최대 중량 +{effect.intValue}";

                case SkillEffectType.LaneMoveDurationMultiplier:
                    return $"이동 시간 x{effect.floatValue:0.##}";

                case SkillEffectType.PerfectWindowAddSeconds:
                    return $"퍼펙트 판정 +{effect.floatValue:0.##}초";

                case SkillEffectType.RewardMultiplierAdd:
                    return $"보상 배율 +{effect.floatValue:0.##}";

                case SkillEffectType.PenaltyMultiplierAdd:
                    return $"패널티 배율 +{effect.floatValue:0.##}";

                case SkillEffectType.UnlockedLaneCountOverride:
                    return $"활성 라인 {effect.intValue}개";

                case SkillEffectType.PreviewCargoCountAdd:
                    return $"미리보기 화물 +{effect.intValue}";

                case SkillEffectType.ReturnBeltChanceAdd:
                    return $"리턴 벨트 확률 +{effect.floatValue * 100f:0.#}%";

                case SkillEffectType.AutomationUnlockFlag:
                    return $"{effect.targetKey} 해금";

                case SkillEffectType.CenterUnlockFlag:
                    return effect.targetKey == MetaProgressionCatalogAsset.LoadingDockUnlockFlag
                        ? "상하차 구역 해금"
                        : $"{effect.targetKey} 해금";

                case SkillEffectType.RobotUnlockFlag:
                    return effect.targetKey == MetaProgressionCatalogAsset.LaneRobotUnlockFlag
                        ? "레인 로봇 해금"
                        : effect.targetKey == MetaProgressionCatalogAsset.DockRobotUnlockFlag
                            ? "Dock 로봇 해금"
                            : $"{effect.targetKey} 해금";

                case SkillEffectType.RobotMaxHandleWeightAdd:
                    return $"로봇 중량 한도 +{effect.intValue}";

                case SkillEffectType.RobotPrecisionTierAdd:
                    return $"로봇 세밀함 +{effect.intValue}";

                default:
                    return effect.effectType.ToString();
            }
        }

        /// <summary>
        /// 초기 선택 노드는 정렬된 브랜치 중 가장 먼저 나오는 루트 노드로 잡습니다.
        /// </summary>
        private string ResolveDefaultSelectedNodeId()
        {
            _catalog.EnsureDefaults();
            SkillBranchDefinition selectedBranch = null;
            foreach (var branch in _catalog.skillBranches)
            {
                if (branch == null)
                {
                    continue;
                }

                if (selectedBranch == null || branch.sortOrder < selectedBranch.sortOrder)
                {
                    selectedBranch = branch;
                }
            }

            if (selectedBranch == null)
            {
                return string.Empty;
            }

            SkillNodeDefinition selectedNode = null;
            foreach (var node in _catalog.skillNodes)
            {
                if (node == null || node.branchId != selectedBranch.branchId)
                {
                    continue;
                }

                if (selectedNode == null ||
                    node.tier < selectedNode.tier ||
                    node.tier == selectedNode.tier && string.Compare(node.displayName, selectedNode.displayName, StringComparison.Ordinal) < 0)
                {
                    selectedNode = node;
                }
            }

            return selectedNode?.nodeId ?? string.Empty;
        }

        /// <summary>
        /// 허브에서 쓰는 카탈로그를 지연 초기화합니다.
        /// </summary>
        private void EnsureCatalog()
        {
            _catalog ??= MetaProgressionCatalogAsset.LoadDefaultCatalog();
        }
    }
}
