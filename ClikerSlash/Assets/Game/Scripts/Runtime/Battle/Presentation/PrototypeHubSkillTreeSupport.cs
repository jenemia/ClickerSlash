using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 허브 스킬트리에서 노드를 어떤 시각 상태로 보여줄지 정의합니다.
    /// </summary>
    public enum SkillTreeNodeVisualState
    {
        Locked = 0,
        Available = 1,
        Selected = 2,
        Maxed = 3
    }

    /// <summary>
    /// 스킬트리 노드 뷰가 바로 그릴 수 있도록 정리한 표시 모델입니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeNodeViewModel
    {
        public string nodeId;
        public string displayName;
        public string glyph;
        public string levelText;
        public string costText;
        public string prerequisiteText;
        public SkillTreeNodeVisualState visualState;
    }

    /// <summary>
    /// 스킬트리 노드의 로컬 레이아웃 좌표입니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeNodeLayout
    {
        public string nodeId;
        public SkillBranchId branchId;
        public int depth;
        public float lateral;
        public Vector2 localPosition;
    }

    /// <summary>
    /// 스킬트리 연결선의 부모-자식 관계와 선분 좌표를 정의합니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeEdgeLayout
    {
        public string parentNodeId;
        public string childNodeId;
        public Vector2 from;
        public Vector2 to;
    }

    /// <summary>
    /// 하나의 브랜치가 어떤 각도로 회전하고 어떤 노드 묶음을 가지는지 나타냅니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeBranchLayout
    {
        public SkillTreeTabId tabId;
        public SkillBranchId branchId;
        public string displayName;
        public int sortOrder;
        public float angleDegrees;
        public Vector2 titlePosition;
        public readonly List<PrototypeHubSkillTreeNodeLayout> nodes = new List<PrototypeHubSkillTreeNodeLayout>();
        public readonly List<PrototypeHubSkillTreeEdgeLayout> edges = new List<PrototypeHubSkillTreeEdgeLayout>();
    }

    /// <summary>
    /// 허브 전체 그래프의 콘텐츠 크기와 브랜치 배치를 담습니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeLayout
    {
        public Vector2 contentSize;
        public readonly List<PrototypeHubSkillTreeBranchLayout> branches = new List<PrototypeHubSkillTreeBranchLayout>();
    }

    /// <summary>
    /// 카탈로그 정의를 허브 스킬트리 레이아웃으로 변환합니다.
    /// </summary>
    public static class PrototypeHubSkillTreeLayoutBuilder
    {
        private const float RootDistance = 360f;
        private const float DepthSpacing = 280f;
        private const float LateralSpacing = 180f;
        private const float TitleDistance = 160f;
        private const float Padding = 360f;
        private const float MinimumLaneGap = 1.2f;

        /// <summary>
        /// 기본 카탈로그를 중심 허브 기준의 방사형 트리 레이아웃으로 변환합니다.
        /// </summary>
        public static PrototypeHubSkillTreeLayout Build(MetaProgressionCatalogAsset catalog)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            catalog.EnsureDefaults();

            var layout = new PrototypeHubSkillTreeLayout();
            var branches = new List<SkillBranchDefinition>(catalog.skillBranches.Count);
            foreach (var branch in catalog.skillBranches)
            {
                if (branch != null)
                {
                    branches.Add(branch);
                }
            }

            branches.Sort((left, right) => left.sortOrder.CompareTo(right.sortOrder));
            var branchCount = Mathf.Max(1, branches.Count);

            var maxAbsX = 0f;
            var maxAbsY = 0f;

            for (var index = 0; index < branches.Count; index += 1)
            {
                var branch = branches[index];
                var branchLayout = BuildBranchLayout(catalog, branch, index, branchCount);
                layout.branches.Add(branchLayout);

                var branchRotation = Quaternion.Euler(0f, 0f, branchLayout.angleDegrees);
                var rotatedTitlePosition = branchRotation * new Vector3(branchLayout.titlePosition.x, branchLayout.titlePosition.y, 0f);
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(rotatedTitlePosition.x));
                maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(rotatedTitlePosition.y));

                foreach (var nodeLayout in branchLayout.nodes)
                {
                    var rotatedPosition = branchRotation * new Vector3(nodeLayout.localPosition.x, nodeLayout.localPosition.y, 0f);
                    maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(rotatedPosition.x));
                    maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(rotatedPosition.y));
                }
            }

            layout.contentSize = new Vector2(
                maxAbsX * 2f + Padding * 2f,
                maxAbsY * 2f + Padding * 2f);
            return layout;
        }

        /// <summary>
        /// 선택 상태와 메타 상태 DTO를 합쳐 노드 뷰 모델로 만듭니다.
        /// </summary>
        public static PrototypeHubSkillTreeNodeViewModel BuildNodeViewModel(
            MetaProgressionNodeStatus status,
            bool isSelected)
        {
            var visualState = ResolveVisualState(status, isSelected);
            return new PrototypeHubSkillTreeNodeViewModel
            {
                nodeId = status.nodeId,
                displayName = status.displayName,
                glyph = BuildGlyph(status.displayName),
                levelText = $"Lv {status.currentLevel}/{status.maxLevel}",
                costText = $"Cost {status.cost}",
                prerequisiteText = status.prerequisiteSummary,
                visualState = visualState
            };
        }

        /// <summary>
        /// 선택 정보와 잠금 여부를 하나의 열거값으로 정규화합니다.
        /// </summary>
        public static SkillTreeNodeVisualState ResolveVisualState(MetaProgressionNodeStatus status, bool isSelected)
        {
            if (isSelected)
            {
                return SkillTreeNodeVisualState.Selected;
            }

            if (status.isMaxed)
            {
                return SkillTreeNodeVisualState.Maxed;
            }

            if (status.isLocked)
            {
                return SkillTreeNodeVisualState.Locked;
            }

            return SkillTreeNodeVisualState.Available;
        }

        private static PrototypeHubSkillTreeBranchLayout BuildBranchLayout(
            MetaProgressionCatalogAsset catalog,
            SkillBranchDefinition branchDefinition,
            int branchIndex,
            int branchCount)
        {
            var nodesForBranch = CollectNodesForBranch(catalog, branchDefinition.branchId);
            var depthByNodeId = BuildDepthMap(catalog, nodesForBranch);
            var parentsByNodeId = BuildParentsMap(catalog, nodesForBranch);
            var lateralByNodeId = ResolveLaterals(nodesForBranch, depthByNodeId, parentsByNodeId);
            var branchLayout = new PrototypeHubSkillTreeBranchLayout
            {
                tabId = branchDefinition.tabId,
                branchId = branchDefinition.branchId,
                displayName = branchDefinition.displayName,
                sortOrder = branchDefinition.sortOrder,
                angleDegrees = 150f - branchIndex * (360f / branchCount),
                titlePosition = new Vector2(TitleDistance, -140f)
            };

            var localPositionByNodeId = new Dictionary<string, Vector2>();
            foreach (var node in nodesForBranch)
            {
                var depth = depthByNodeId.TryGetValue(node.nodeId, out var resolvedDepth) ? resolvedDepth : 0;
                var lateral = lateralByNodeId.TryGetValue(node.nodeId, out var resolvedLateral) ? resolvedLateral : 0f;
                var localPosition = new Vector2(
                    RootDistance + depth * DepthSpacing,
                    lateral * LateralSpacing);

                branchLayout.nodes.Add(new PrototypeHubSkillTreeNodeLayout
                {
                    nodeId = node.nodeId,
                    branchId = node.branchId,
                    depth = depth,
                    lateral = lateral,
                    localPosition = localPosition
                });
                localPositionByNodeId[node.nodeId] = localPosition;
            }

            foreach (var node in nodesForBranch)
            {
                var childPosition = localPositionByNodeId[node.nodeId];
                var parentNodeIds = parentsByNodeId[node.nodeId];
                if (parentNodeIds.Count == 0)
                {
                    branchLayout.edges.Add(new PrototypeHubSkillTreeEdgeLayout
                    {
                        childNodeId = node.nodeId,
                        from = Vector2.zero,
                        to = childPosition
                    });
                    continue;
                }

                foreach (var parentNodeId in parentNodeIds)
                {
                    if (!localPositionByNodeId.TryGetValue(parentNodeId, out var parentPosition))
                    {
                        continue;
                    }

                    branchLayout.edges.Add(new PrototypeHubSkillTreeEdgeLayout
                    {
                        parentNodeId = parentNodeId,
                        childNodeId = node.nodeId,
                        from = parentPosition,
                        to = childPosition
                    });
                }
            }

            return branchLayout;
        }

        private static List<SkillNodeDefinition> CollectNodesForBranch(
            MetaProgressionCatalogAsset catalog,
            SkillBranchId branchId)
        {
            var nodes = new List<SkillNodeDefinition>();
            foreach (var node in catalog.skillNodes)
            {
                if (node != null && node.branchId == branchId)
                {
                    nodes.Add(node);
                }
            }

            nodes.Sort((left, right) =>
            {
                var tierCompare = left.tier.CompareTo(right.tier);
                if (tierCompare != 0)
                {
                    return tierCompare;
                }

                return string.Compare(left.displayName, right.displayName, StringComparison.Ordinal);
            });
            return nodes;
        }

        private static Dictionary<string, int> BuildDepthMap(
            MetaProgressionCatalogAsset catalog,
            List<SkillNodeDefinition> branchNodes)
        {
            var branchNodeIds = new HashSet<string>();
            foreach (var node in branchNodes)
            {
                branchNodeIds.Add(node.nodeId);
            }

            var depthByNodeId = new Dictionary<string, int>(branchNodes.Count);
            foreach (var node in branchNodes)
            {
                depthByNodeId[node.nodeId] = ResolveDepth(node, catalog, branchNodeIds, depthByNodeId, new HashSet<string>());
            }

            return depthByNodeId;
        }

        private static Dictionary<string, List<string>> BuildParentsMap(
            MetaProgressionCatalogAsset catalog,
            List<SkillNodeDefinition> branchNodes)
        {
            var branchNodeIds = new HashSet<string>();
            foreach (var node in branchNodes)
            {
                branchNodeIds.Add(node.nodeId);
            }

            var parentsByNodeId = new Dictionary<string, List<string>>(branchNodes.Count);
            foreach (var node in branchNodes)
            {
                var parents = new List<string>();
                if (node.prerequisiteNodeIds != null)
                {
                    foreach (var prerequisiteNodeId in node.prerequisiteNodeIds)
                    {
                        if (branchNodeIds.Contains(prerequisiteNodeId))
                        {
                            parents.Add(prerequisiteNodeId);
                        }
                    }
                }

                parentsByNodeId[node.nodeId] = parents;
            }

            return parentsByNodeId;
        }

        private static Dictionary<string, float> ResolveLaterals(
            List<SkillNodeDefinition> branchNodes,
            Dictionary<string, int> depthByNodeId,
            Dictionary<string, List<string>> parentsByNodeId)
        {
            var lateralByNodeId = new Dictionary<string, float>(branchNodes.Count);
            var maxDepth = 0;
            foreach (var pair in depthByNodeId)
            {
                maxDepth = Mathf.Max(maxDepth, pair.Value);
            }

            for (var depth = 0; depth <= maxDepth; depth += 1)
            {
                var nodesAtDepth = new List<SkillNodeDefinition>();
                foreach (var node in branchNodes)
                {
                    if (depthByNodeId[node.nodeId] == depth)
                    {
                        nodesAtDepth.Add(node);
                    }
                }

                if (nodesAtDepth.Count == 0)
                {
                    continue;
                }

                if (depth == 0)
                {
                    CenterRootNodes(nodesAtDepth, lateralByNodeId);
                    continue;
                }

                nodesAtDepth.Sort((left, right) =>
                {
                    var leftDesired = ResolveDesiredLateral(left.nodeId, parentsByNodeId, lateralByNodeId);
                    var rightDesired = ResolveDesiredLateral(right.nodeId, parentsByNodeId, lateralByNodeId);
                    var desiredCompare = leftDesired.CompareTo(rightDesired);
                    if (desiredCompare != 0)
                    {
                        return desiredCompare;
                    }

                    return string.Compare(left.displayName, right.displayName, StringComparison.Ordinal);
                });

                var placedLaterals = new List<float>(nodesAtDepth.Count);
                for (var index = 0; index < nodesAtDepth.Count; index += 1)
                {
                    var node = nodesAtDepth[index];
                    var desiredLateral = ResolveDesiredLateral(node.nodeId, parentsByNodeId, lateralByNodeId);
                    var placedLateral = desiredLateral;
                    if (placedLaterals.Count > 0)
                    {
                        placedLateral = Mathf.Max(placedLateral, placedLaterals[placedLaterals.Count - 1] + MinimumLaneGap);
                    }

                    placedLaterals.Add(placedLateral);
                    lateralByNodeId[node.nodeId] = placedLateral;
                }

                var centerOffset = (placedLaterals[0] + placedLaterals[placedLaterals.Count - 1]) * 0.5f;
                foreach (var node in nodesAtDepth)
                {
                    lateralByNodeId[node.nodeId] -= centerOffset;
                }
            }

            return lateralByNodeId;
        }

        private static void CenterRootNodes(
            List<SkillNodeDefinition> rootNodes,
            Dictionary<string, float> lateralByNodeId)
        {
            if (rootNodes.Count == 1)
            {
                lateralByNodeId[rootNodes[0].nodeId] = 0f;
                return;
            }

            var start = -(rootNodes.Count - 1) * 0.5f;
            for (var index = 0; index < rootNodes.Count; index += 1)
            {
                lateralByNodeId[rootNodes[index].nodeId] = start + index;
            }
        }

        private static float ResolveDesiredLateral(
            string nodeId,
            Dictionary<string, List<string>> parentsByNodeId,
            Dictionary<string, float> lateralByNodeId)
        {
            if (!parentsByNodeId.TryGetValue(nodeId, out var parents) || parents.Count == 0)
            {
                return 0f;
            }

            var lateralSum = 0f;
            var counted = 0;
            foreach (var parentNodeId in parents)
            {
                if (!lateralByNodeId.TryGetValue(parentNodeId, out var lateral))
                {
                    continue;
                }

                lateralSum += lateral;
                counted += 1;
            }

            return counted == 0 ? 0f : lateralSum / counted;
        }

        private static int ResolveDepth(
            SkillNodeDefinition nodeDefinition,
            MetaProgressionCatalogAsset catalog,
            HashSet<string> branchNodeIds,
            Dictionary<string, int> cache,
            HashSet<string> visiting)
        {
            if (cache.TryGetValue(nodeDefinition.nodeId, out var cachedDepth))
            {
                return cachedDepth;
            }

            if (!visiting.Add(nodeDefinition.nodeId))
            {
                return 0;
            }

            var maxParentDepth = -1;
            if (nodeDefinition.prerequisiteNodeIds != null)
            {
                foreach (var prerequisiteNodeId in nodeDefinition.prerequisiteNodeIds)
                {
                    if (!branchNodeIds.Contains(prerequisiteNodeId) ||
                        !catalog.TryGetNodeDefinition(prerequisiteNodeId, out var prerequisiteDefinition) ||
                        prerequisiteDefinition == null)
                    {
                        continue;
                    }

                    maxParentDepth = Mathf.Max(
                        maxParentDepth,
                        ResolveDepth(prerequisiteDefinition, catalog, branchNodeIds, cache, visiting));
                }
            }

            visiting.Remove(nodeDefinition.nodeId);
            var resolvedDepth = maxParentDepth + 1;
            cache[nodeDefinition.nodeId] = resolvedDepth;
            return resolvedDepth;
        }

        private static string BuildGlyph(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            return displayName.Substring(0, 1);
        }
    }
}
