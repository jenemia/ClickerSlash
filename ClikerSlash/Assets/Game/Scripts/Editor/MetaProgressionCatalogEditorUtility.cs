using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 기본 메타 카탈로그 에셋을 생성하거나 갱신하는 에디터 유틸리티입니다.
    /// </summary>
    public static class MetaProgressionCatalogEditorUtility
    {
        private const string AssetDirectoryPath = "Assets/Game/Resources/MetaProgression";
        private const string AssetPath = AssetDirectoryPath + "/DefaultMetaProgressionCatalog.asset";
        private const string TreeDirectoryPath = AssetDirectoryPath + "/Trees";
        private const string CenterTreePath = TreeDirectoryPath + "/CenterMetaProgressionTree.asset";
        private const string HumanTreePath = TreeDirectoryPath + "/HumanMetaProgressionTree.asset";
        private const string RobotTreePath = TreeDirectoryPath + "/RobotMetaProgressionTree.asset";

        /// <summary>
        /// 메뉴에서 기본 카탈로그 에셋을 생성하거나 물류센터 기본값으로 갱신합니다.
        /// </summary>
        [MenuItem("Tools/ClikerSlash/Meta/Ensure Default Meta Progression Catalog")]
        public static void EnsureDefaultCatalogAsset()
        {
            EnsureFoldersExist();

            var catalog = AssetDatabase.LoadAssetAtPath<MetaProgressionCatalogAsset>(AssetPath);
            var preservedWorkerBaseStats = CloneWorkerBaseStats(catalog != null ? catalog.workerBaseStats : null);
            var preservedStartingProgression = CloneStartingProgression(catalog != null ? catalog.startingProgression : null);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MetaProgressionCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            catalog.centerTree = LoadOrCreateTreeAsset(CenterTreePath, SkillTreeTabId.Center);
            catalog.humanTree = LoadOrCreateTreeAsset(HumanTreePath, SkillTreeTabId.Human);
            catalog.robotTree = LoadOrCreateTreeAsset(RobotTreePath, SkillTreeTabId.Robot);
            catalog.ResetToLogisticsDefaults();
            if (preservedWorkerBaseStats != null)
            {
                catalog.workerBaseStats = preservedWorkerBaseStats;
            }

            if (preservedStartingProgression != null)
            {
                catalog.startingProgression = preservedStartingProgression;
            }

            catalog.EnsureDefaults();

            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(catalog.centerTree);
            EditorUtility.SetDirty(catalog.humanTree);
            EditorUtility.SetDirty(catalog.robotTree);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static MetaProgressionTreeAsset LoadOrCreateTreeAsset(string assetPath, SkillTreeTabId tabId)
        {
            var tree = AssetDatabase.LoadAssetAtPath<MetaProgressionTreeAsset>(assetPath);
            if (tree == null)
            {
                tree = ScriptableObject.CreateInstance<MetaProgressionTreeAsset>();
                tree.tabId = tabId;
                tree.ResetToLogisticsDefaults();
                AssetDatabase.CreateAsset(tree, assetPath);
                return tree;
            }

            tree.tabId = tabId;
            tree.ResetToLogisticsDefaults();
            return tree;
        }

        private static WorkerBaseStatsDefinition CloneWorkerBaseStats(WorkerBaseStatsDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            return new WorkerBaseStatsDefinition
            {
                baseSessionDurationSeconds = source.baseSessionDurationSeconds,
                baseMaxHandleWeight = source.baseMaxHandleWeight,
                baseLaneMoveDurationSeconds = source.baseLaneMoveDurationSeconds,
                baseTimingWindowHalfDepth = source.baseTimingWindowHalfDepth,
                baseRewardMultiplier = source.baseRewardMultiplier,
                basePenaltyMultiplier = source.basePenaltyMultiplier,
                startingUnlockedLaneCount = source.startingUnlockedLaneCount
            };
        }

        private static StartingProgressionDefinition CloneStartingProgression(StartingProgressionDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new StartingProgressionDefinition
            {
                selectedAutomationFlags = source.selectedAutomationFlags != null
                    ? new System.Collections.Generic.List<string>(source.selectedAutomationFlags)
                    : new System.Collections.Generic.List<string>(),
                unlockedNodeStates = new System.Collections.Generic.List<UnlockedSkillNodeState>(),
                resolvedLoadoutVersion = source.resolvedLoadoutVersion
            };

            if (source.unlockedNodeStates == null)
            {
                return clone;
            }

            foreach (var unlockedNodeState in source.unlockedNodeStates)
            {
                if (unlockedNodeState == null)
                {
                    continue;
                }

                clone.unlockedNodeStates.Add(new UnlockedSkillNodeState
                {
                    nodeId = unlockedNodeState.nodeId,
                    level = unlockedNodeState.level,
                    isUnlocked = unlockedNodeState.isUnlocked
                });
            }

            return clone;
        }

        private static void EnsureFoldersExist()
        {
            if (AssetDatabase.IsValidFolder(AssetDirectoryPath))
            {
                if (!AssetDatabase.IsValidFolder(TreeDirectoryPath))
                {
                    AssetDatabase.CreateFolder(AssetDirectoryPath, "Trees");
                }

                return;
            }

            var segments = AssetDirectoryPath.Split('/');
            var currentPath = segments[0];
            for (var index = 1; index < segments.Length; index += 1)
            {
                var nextPath = currentPath + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }

            if (!Directory.Exists(AssetDirectoryPath))
            {
                Directory.CreateDirectory(AssetDirectoryPath);
            }

            if (!Directory.Exists(TreeDirectoryPath))
            {
                Directory.CreateDirectory(TreeDirectoryPath);
            }
        }
    }
}
