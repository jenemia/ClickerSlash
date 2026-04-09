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
            Assert.That(resolved.ActiveLaneCount, Is.EqualTo(catalog.workerBaseStats.startingUnlockedLaneCount));
            Assert.That(resolved.MaxHandleWeight, Is.EqualTo(catalog.workerBaseStats.baseMaxHandleWeight));
            Assert.That(resolved.LaneMoveDurationSeconds, Is.EqualTo(catalog.workerBaseStats.baseLaneMoveDurationSeconds).Within(0.001f));
            Assert.That(resolved.SessionDurationSeconds, Is.EqualTo(catalog.workerBaseStats.baseSessionDurationSeconds).Within(0.001f));
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
    }
}
