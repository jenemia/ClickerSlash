using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClikerSlash.Tests.PlayMode
{
    /// <summary>
    /// 메타 카탈로그 기본값과 DTO 계약 왕복을 검증하는 플레이 모드 테스트입니다.
    /// </summary>
    public class MetaProgressionPlayModeTests
    {
        /// <summary>
        /// 기본 카탈로그가 문서 기준 시작값을 그대로 복원하는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator DefaultCatalogResolvesExpectedBaseValues()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);
            var resolved = MetaProgressionCalculator.Resolve(snapshot, catalog, 5);

            Assert.That(snapshot.currency, Is.Not.Null);
            Assert.That(snapshot.currency.currentBalance, Is.Zero);
            Assert.That(snapshot.currency.totalBattleEarned, Is.Zero);
            Assert.That(snapshot.currency.totalSkillSpent, Is.Zero);
            Assert.That(catalog.centerTree, Is.Not.Null);
            Assert.That(catalog.humanTree, Is.Not.Null);
            Assert.That(catalog.robotTree, Is.Not.Null);
            Assert.That(catalog.skillTabs.Count, Is.EqualTo(3));
            Assert.That(
                catalog.skillBranches.Count,
                Is.EqualTo(catalog.centerTree.branches.Count + catalog.humanTree.branches.Count + catalog.robotTree.branches.Count));
            Assert.That(
                catalog.skillNodes.Count,
                Is.EqualTo(catalog.centerTree.nodes.Count + catalog.humanTree.nodes.Count + catalog.robotTree.nodes.Count));
            Assert.That(resolved.ActiveLaneCount, Is.EqualTo(catalog.workerBaseStats.startingUnlockedLaneCount));
            Assert.That(resolved.MaxHandleWeight, Is.EqualTo(catalog.workerBaseStats.baseMaxHandleWeight));
            Assert.That(resolved.LaneMoveDurationSeconds, Is.EqualTo(catalog.workerBaseStats.baseLaneMoveDurationSeconds).Within(0.001f));
            Assert.That(resolved.SessionDurationSeconds, Is.EqualTo(catalog.workerBaseStats.baseSessionDurationSeconds).Within(0.001f));
            Assert.That(resolved.HasLoadingDockAccess, Is.True);
            yield return null;
        }

        /// <summary>
        /// 기존 브랜치가 3탭 래퍼 아래로 매핑되고 상하차 오픈 노드가 센터 탭에 배치되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator BranchesKeepIdsWhileTabsWrapTheCatalog()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();

            Assert.That(catalog.TryGetTabDefinition(SkillTreeTabId.Center, out var centerTab), Is.True);
            Assert.That(catalog.TryGetTabDefinition(SkillTreeTabId.Human, out var humanTab), Is.True);
            Assert.That(catalog.TryGetTabDefinition(SkillTreeTabId.Robot, out var robotTab), Is.True);
            Assert.That(centerTab.displayName, Is.EqualTo("물류 센터 성능"));
            Assert.That(humanTab.displayName, Is.EqualTo("사람 성능"));
            Assert.That(robotTab.displayName, Is.EqualTo("로봇 성능"));

            Assert.That(catalog.TryGetBranchDefinition(SkillBranchId.Management, out var managementBranch), Is.True);
            Assert.That(catalog.TryGetBranchDefinition(SkillBranchId.Automation, out var automationBranch), Is.True);
            Assert.That(catalog.TryGetBranchDefinition(SkillBranchId.Vitality, out var vitalityBranch), Is.True);
            Assert.That(managementBranch.tabId, Is.EqualTo(SkillTreeTabId.Center));
            Assert.That(automationBranch.tabId, Is.EqualTo(SkillTreeTabId.Robot));
            Assert.That(vitalityBranch.tabId, Is.EqualTo(SkillTreeTabId.Human));

            Assert.That(catalog.TryGetNodeDefinition(MetaProgressionCatalogAsset.LoadingDockUnlockNodeId, out var loadingDockNode), Is.True);
            Assert.That(loadingDockNode.branchId, Is.EqualTo(SkillBranchId.Management));
            Assert.That(loadingDockNode.prerequisiteNodeIds, Does.Contain("management.performance_contract"));

            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);
            var resolved = MetaProgressionCalculator.Resolve(snapshot, catalog, 5);
            var status = MetaProgressionCalculator.DescribeNode(snapshot, catalog, MetaProgressionCatalogAsset.LoadingDockUnlockNodeId);

            Assert.That(resolved.HasLoadingDockAccess, Is.True);
            Assert.That(status.tabId, Is.EqualTo(SkillTreeTabId.Center));
            Assert.That(status.tabDisplayName, Is.EqualTo("물류 센터 성능"));
            Assert.That(status.isUnlocked, Is.True);
            Assert.That(status.isLocked, Is.False);
            yield return null;
        }

        /// <summary>
        /// 경영 라인 해금 노드가 3, 4, 5레인으로 단계적으로 집계되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LaneExpansionNodesClampResolvedLaneCount()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);
            for (var level = 0; level < 5; level += 1)
            {
                Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, "management.performance_contract"), Is.True);
            }

            Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, MetaProgressionCatalogAsset.LaneExpansionNodeIdTier1), Is.True);
            Assert.That(MetaProgressionCalculator.Resolve(snapshot, catalog, 5).ActiveLaneCount, Is.EqualTo(3));

            Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, MetaProgressionCatalogAsset.LaneExpansionNodeIdTier2), Is.True);
            Assert.That(MetaProgressionCalculator.Resolve(snapshot, catalog, 5).ActiveLaneCount, Is.EqualTo(4));

            Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, MetaProgressionCatalogAsset.LaneExpansionNodeIdTier3), Is.True);
            Assert.That(MetaProgressionCalculator.Resolve(snapshot, catalog, 5).ActiveLaneCount, Is.EqualTo(5));
            yield return null;
        }

        /// <summary>
        /// 계약 DTO가 런타임 상태로 갔다가 다시 돌아와도 핵심 식별자가 유지되는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator ProtoContractRoundTripPreservesNodeIdsAndVersions()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);
            Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, MetaProgressionCatalogAsset.StarterVitalityNodeId), Is.True);
            Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, "automation.weight_scanner"), Is.True);
            snapshot.currency.currentBalance = 14;
            snapshot.currency.totalBattleEarned = 21;
            snapshot.currency.totalSkillSpent = 7;

            var runtimeState = MetaProgressionProtoContractMapper.FromContract(snapshot, catalog, 4);
            var roundTrip = MetaProgressionProtoContractMapper.ToContract(runtimeState);

            Assert.That(roundTrip.schemaVersion, Is.EqualTo(snapshot.schemaVersion));
            Assert.That(roundTrip.resolvedLoadoutVersion, Is.EqualTo(snapshot.resolvedLoadoutVersion));
            Assert.That(roundTrip.currency.currentBalance, Is.EqualTo(snapshot.currency.currentBalance));
            Assert.That(roundTrip.currency.totalBattleEarned, Is.EqualTo(snapshot.currency.totalBattleEarned));
            Assert.That(roundTrip.currency.totalSkillSpent, Is.EqualTo(snapshot.currency.totalSkillSpent));
            Assert.That(roundTrip.unlockedNodeStates.Count, Is.EqualTo(snapshot.unlockedNodeStates.Count));
            Assert.That(roundTrip.unlockedNodeStates[0].nodeId, Is.EqualTo(snapshot.unlockedNodeStates[0].nodeId));
            Assert.That(roundTrip.unlockedNodeStates[0].level, Is.EqualTo(snapshot.unlockedNodeStates[0].level));
            yield return null;
        }

        /// <summary>
        /// 이전 계약에 재화 필드가 없어도 0원 기본값으로 보정되어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator NullCurrencyContractsAreNormalizedToZeroValues()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);
            snapshot.currency = null;

            var runtimeState = MetaProgressionProtoContractMapper.FromContract(snapshot, catalog, 4);
            var roundTrip = MetaProgressionProtoContractMapper.ToContract(runtimeState);

            Assert.That(roundTrip.currency, Is.Not.Null);
            Assert.That(roundTrip.currency.currentBalance, Is.Zero);
            Assert.That(roundTrip.currency.totalBattleEarned, Is.Zero);
            Assert.That(roundTrip.currency.totalSkillSpent, Is.Zero);
            yield return null;
        }

        /// <summary>
        /// 허브 노드 상태가 현재 재화 기준으로 구매 가능 여부를 함께 계산해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator NodeStatusReflectsAffordabilityFromCurrencyBalance()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);
            snapshot.currency.currentBalance = 0;

            var status = MetaProgressionCalculator.DescribeNode(snapshot, catalog, MetaProgressionCatalogAsset.StarterVitalityNodeId);
            Assert.That(status.isAffordable, Is.False);
            Assert.That(status.canUpgrade, Is.False);
            Assert.That(status.affordabilitySummary, Is.EqualTo("재화 부족 (보유 0 / 필요 1)"));

            snapshot.currency.currentBalance = 1;
            status = MetaProgressionCalculator.DescribeNode(snapshot, catalog, MetaProgressionCatalogAsset.StarterVitalityNodeId);
            Assert.That(status.isAffordable, Is.True);
            Assert.That(status.canUpgrade, Is.True);
            Assert.That(status.affordabilitySummary, Is.EqualTo("구매 가능 (보유 1)"));
            yield return null;
        }

        /// <summary>
        /// 다음 노드는 이전 노드가 최대 레벨에 도달하기 전까지 잠겨 있어야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator ChildNodesRequireParentMaxLevelBeforeUpgrade()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);

            Assert.That(
                MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, MetaProgressionCatalogAsset.LaneExpansionNodeIdTier1),
                Is.False);

            for (var level = 0; level < 5; level += 1)
            {
                Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, "management.performance_contract"), Is.True);
            }

            Assert.That(
                MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, MetaProgressionCatalogAsset.LaneExpansionNodeIdTier1),
                Is.True);
            Assert.That(
                MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, "automation.return_belt"),
                Is.False);

            Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, "automation.weight_scanner"), Is.True);
            Assert.That(MetaProgressionCalculator.TryUpgradeNode(snapshot, catalog, "automation.return_belt"), Is.True);
            yield return null;
        }

        /// <summary>
        /// 허브 레이아웃 빌더가 카탈로그의 모든 브랜치와 노드를 안정적으로 배치하는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator SkillTreeLayoutBuildsBranchesAndNodePositions()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var layout = PrototypeHubSkillTreeLayoutBuilder.Build(catalog);

            Assert.That(layout.branches.Count, Is.EqualTo(catalog.skillBranches.Count));

            var totalNodeCount = 0;
            foreach (var branch in layout.branches)
            {
                totalNodeCount += branch.nodes.Count;
                foreach (var node in branch.nodes)
                {
                    Assert.That(node.localPosition.x, Is.GreaterThan(0f));
                }
            }

            Assert.That(totalNodeCount, Is.EqualTo(catalog.skillNodes.Count));
            Assert.That(layout.contentSize.x, Is.GreaterThan(0f));
            Assert.That(layout.contentSize.y, Is.GreaterThan(0f));
            yield return null;
        }

        /// <summary>
        /// 스킬트리 뷰가 3탭 셸을 만들고 탭 전환 시 브랜치 가시성을 바꾸는지 검증합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator SkillTreeViewBuildsTabsAndSwitchesVisibleBranches()
        {
            var catalog = MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(catalog);

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(PrototypeHubPanZoomController), typeof(PrototypeHubSkillTreeView));
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
            viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.sizeDelta = new Vector2(1280f, 720f);

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(3200f, 2200f);

            var panZoomController = viewportObject.GetComponent<PrototypeHubPanZoomController>();
            var treeView = viewportObject.GetComponent<PrototypeHubSkillTreeView>();
            treeView.Bind(viewportRect, contentRect, panZoomController);
            treeView.Build(catalog);
            treeView.Refresh(snapshot, catalog, MetaProgressionCatalogAsset.StarterVitalityNodeId);

            Assert.That(treeView.TabCount, Is.EqualTo(3));
            Assert.That(treeView.ActiveTabId, Is.EqualTo(SkillTreeTabId.Human));
            Assert.That(treeView.VisibleBranchCount, Is.EqualTo(4));

            treeView.SelectTab(SkillTreeTabId.Center);
            treeView.Refresh(snapshot, catalog, MetaProgressionCatalogAsset.StarterVitalityNodeId);
            Assert.That(treeView.ActiveTabId, Is.EqualTo(SkillTreeTabId.Center));
            Assert.That(treeView.VisibleBranchCount, Is.EqualTo(1));

            treeView.SelectTab(SkillTreeTabId.Robot);
            treeView.Refresh(snapshot, catalog, MetaProgressionCatalogAsset.StarterVitalityNodeId);
            Assert.That(treeView.ActiveTabId, Is.EqualTo(SkillTreeTabId.Robot));
            Assert.That(treeView.VisibleBranchCount, Is.EqualTo(1));

            Object.Destroy(viewportObject);
            yield return null;
        }

        /// <summary>
        /// 탭/브랜치 필드가 없던 레거시 카탈로그도 3탭 구조와 상하차 오픈 노드를 복원해야 합니다.
        /// </summary>
        [UnityTest]
        public IEnumerator LegacyCatalogsNormalizeIntoThreeTabRuntimeFlow()
        {
            var legacyCatalog = ScriptableObject.CreateInstance<MetaProgressionCatalogAsset>();
            legacyCatalog.schemaVersion = 1;
            legacyCatalog.skillTabs = new System.Collections.Generic.List<SkillTreeTabDefinition>
            {
                new SkillTreeTabDefinition { tabId = SkillTreeTabId.Center, displayName = string.Empty, sortOrder = 99 }
            };
            legacyCatalog.skillBranches = new System.Collections.Generic.List<SkillBranchDefinition>
            {
                new SkillBranchDefinition { branchId = SkillBranchId.Vitality, displayName = "체력", sortOrder = 99 },
                new SkillBranchDefinition { branchId = SkillBranchId.Strength, displayName = "근력", sortOrder = 99 },
                new SkillBranchDefinition { branchId = SkillBranchId.Mobility, displayName = "이동", sortOrder = 99 },
                new SkillBranchDefinition { branchId = SkillBranchId.Mastery, displayName = "숙련", sortOrder = 99 },
                new SkillBranchDefinition { branchId = SkillBranchId.Management, displayName = "경영", sortOrder = 99 },
                new SkillBranchDefinition { branchId = SkillBranchId.Automation, displayName = "자동화", sortOrder = 99 }
            };
            legacyCatalog.skillNodes = new System.Collections.Generic.List<SkillNodeDefinition>
            {
                new SkillNodeDefinition
                {
                    nodeId = "management.performance_contract",
                    branchId = SkillBranchId.Management,
                    displayName = "성과급 계약",
                    tier = 1,
                    maxLevel = 5,
                    cost = 1,
                    prerequisiteNodeIds = null,
                    effects = null
                },
                new SkillNodeDefinition
                {
                    nodeId = MetaProgressionCatalogAsset.StarterVitalityNodeId,
                    branchId = SkillBranchId.Vitality,
                    displayName = "기초 체력 단련",
                    tier = 1,
                    maxLevel = 10,
                    cost = 1,
                    prerequisiteNodeIds = null,
                    effects = null
                }
            };
            legacyCatalog.startingProgression = new StartingProgressionDefinition
            {
                unlockedNodeStates = null,
                selectedAutomationFlags = null,
                resolvedLoadoutVersion = 0
            };

            legacyCatalog.EnsureDefaults();

            Assert.That(legacyCatalog.skillTabs.Count, Is.EqualTo(3));
            Assert.That(legacyCatalog.skillBranches.Count, Is.EqualTo(6));
            Assert.That(legacyCatalog.centerTree, Is.Null);
            Assert.That(legacyCatalog.humanTree, Is.Null);
            Assert.That(legacyCatalog.robotTree, Is.Null);
            Assert.That(legacyCatalog.TryGetBranchDefinition(SkillBranchId.Vitality, out var vitalityBranch), Is.True);
            Assert.That(legacyCatalog.TryGetBranchDefinition(SkillBranchId.Management, out var managementBranch), Is.True);
            Assert.That(vitalityBranch.tabId, Is.EqualTo(SkillTreeTabId.Human));
            Assert.That(managementBranch.tabId, Is.EqualTo(SkillTreeTabId.Center));
            Assert.That(vitalityBranch.sortOrder, Is.EqualTo(0));
            Assert.That(managementBranch.sortOrder, Is.EqualTo(4));
            Assert.That(legacyCatalog.TryGetNodeDefinition(MetaProgressionCatalogAsset.LoadingDockUnlockNodeId, out var loadingDockNode), Is.True);
            Assert.That(loadingDockNode.effects, Is.Not.Empty);

            var snapshot = MetaProgressionCalculator.CreateDefaultSnapshot(legacyCatalog);
            var loadingDockStatus = MetaProgressionCalculator.DescribeNode(snapshot, legacyCatalog, MetaProgressionCatalogAsset.LoadingDockUnlockNodeId);
            var vitalityStatus = MetaProgressionCalculator.DescribeNode(snapshot, legacyCatalog, MetaProgressionCatalogAsset.StarterVitalityNodeId);
            var resolved = MetaProgressionCalculator.Resolve(snapshot, legacyCatalog, 5);

            Assert.That(vitalityStatus.tabId, Is.EqualTo(SkillTreeTabId.Human));
            Assert.That(loadingDockStatus.tabId, Is.EqualTo(SkillTreeTabId.Center));
            Assert.That(loadingDockStatus.tabDisplayName, Is.EqualTo("물류 센터 성능"));
            Assert.That(loadingDockStatus.isUnlocked, Is.True);
            Assert.That(loadingDockStatus.isLocked, Is.False);
            Assert.That(resolved.HasLoadingDockAccess, Is.True);

            Object.Destroy(legacyCatalog);
            yield return null;
        }
    }
}
