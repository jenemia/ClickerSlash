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

            Assert.That(resolved.ActiveLaneCount, Is.EqualTo(2));
            Assert.That(resolved.MaxHandleWeight, Is.EqualTo(10));
            Assert.That(resolved.LaneMoveDurationSeconds, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(resolved.SessionDurationSeconds, Is.EqualTo(PrototypeSessionRuntime.DefaultBaseWorkDurationSeconds).Within(0.001f));
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

            var runtimeState = MetaProgressionProtoContractMapper.FromContract(snapshot, catalog, 4);
            var roundTrip = MetaProgressionProtoContractMapper.ToContract(runtimeState);

            Assert.That(roundTrip.schemaVersion, Is.EqualTo(snapshot.schemaVersion));
            Assert.That(roundTrip.resolvedLoadoutVersion, Is.EqualTo(snapshot.resolvedLoadoutVersion));
            Assert.That(roundTrip.unlockedNodeStates.Count, Is.EqualTo(snapshot.unlockedNodeStates.Count));
            Assert.That(roundTrip.unlockedNodeStates[0].nodeId, Is.EqualTo(snapshot.unlockedNodeStates[0].nodeId));
            Assert.That(roundTrip.unlockedNodeStates[0].level, Is.EqualTo(snapshot.unlockedNodeStates[0].level));
            yield return null;
        }
    }
}
