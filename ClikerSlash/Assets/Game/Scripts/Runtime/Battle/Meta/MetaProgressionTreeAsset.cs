using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 단일 상위 탭에 속한 스킬 브랜치/노드 데이터를 담는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MetaProgressionTree",
        menuName = "ClikerSlash/Meta/Meta Progression Tree")]
    public sealed class MetaProgressionTreeAsset : ScriptableObject
    {
        public SkillTreeTabId tabId;
        public string displayName;
        public int sortOrder;
        public List<SkillBranchDefinition> branches = new List<SkillBranchDefinition>();
        public List<SkillNodeDefinition> nodes = new List<SkillNodeDefinition>();

        /// <summary>
        /// 이 탭 자산을 기본 물류센터 트리 구조로 맞춥니다.
        /// </summary>
        public void ResetToLogisticsDefaults()
        {
            var tabDefinition = MetaProgressionCatalogAsset.CreateDefaultTabDefinition(tabId);
            displayName = tabDefinition.displayName;
            sortOrder = tabDefinition.sortOrder;
            branches = MetaProgressionCatalogAsset.CreateDefaultBranchesForTab(tabId);
            nodes = MetaProgressionCatalogAsset.CreateDefaultNodesForTab(tabId);
        }

        /// <summary>
        /// 탭 메타데이터와 하위 브랜치/노드를 현재 스키마 기준으로 보정합니다.
        /// </summary>
        public void EnsureDefaults()
        {
            var tabDefinition = MetaProgressionCatalogAsset.CreateDefaultTabDefinition(tabId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? tabDefinition.displayName : displayName;
            sortOrder = tabDefinition.sortOrder;

            branches ??= new List<SkillBranchDefinition>();
            nodes ??= new List<SkillNodeDefinition>();

            MetaProgressionCatalogAsset.NormalizeTreeBranches(
                tabId,
                branches,
                MetaProgressionCatalogAsset.CreateDefaultBranchesForTab(tabId));
            MetaProgressionCatalogAsset.NormalizeTreeNodes(
                nodes,
                MetaProgressionCatalogAsset.CreateDefaultNodesForTab(tabId));
        }
    }
}
