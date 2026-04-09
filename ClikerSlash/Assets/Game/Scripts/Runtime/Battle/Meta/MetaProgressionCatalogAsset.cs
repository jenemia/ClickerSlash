using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 메타 성장의 6개 큰 줄기 식별자입니다.
    /// </summary>
    public enum SkillBranchId
    {
        Vitality = 0,
        Strength = 1,
        Mobility = 2,
        Mastery = 3,
        Management = 4,
        Automation = 5
    }

    /// <summary>
    /// 메타 허브에서 브랜치를 묶어 보여주는 상위 탭 식별자입니다.
    /// </summary>
    public enum SkillTreeTabId
    {
        Center = 0,
        Human = 1,
        Robot = 2
    }

    /// <summary>
    /// 개별 스킬 노드가 런타임 집계에 주는 효과 종류입니다.
    /// </summary>
    public enum SkillEffectType
    {
        SessionDurationAddSeconds = 0,
        MaxHandleWeightAdd = 1,
        LaneMoveDurationMultiplier = 2,
        PerfectWindowAddSeconds = 3,
        RewardMultiplierAdd = 4,
        PenaltyMultiplierAdd = 5,
        UnlockedLaneCountOverride = 6,
        PreviewCargoCountAdd = 7,
        ReturnBeltChanceAdd = 8,
        AutomationUnlockFlag = 9,
        CenterUnlockFlag = 10
    }

    /// <summary>
    /// 스킬 효과가 값을 누적하거나 덮어쓸 때 사용하는 연산 종류입니다.
    /// </summary>
    public enum SkillEffectOperation
    {
        Add = 0,
        Multiply = 1,
        Override = 2,
        Unlock = 3
    }

    /// <summary>
    /// protobuf로 옮기기 쉬운 작업자 기본 스탯 스냅샷입니다.
    /// </summary>
    [Serializable]
    public sealed class WorkerStatsSnapshot
    {
        public float baseSessionDurationSeconds;
        public int baseMaxHandleWeight;
        public float baseLaneMoveDurationSeconds;
        public float baseTimingWindowHalfDepth;
        public float baseRewardMultiplier;
        public float basePenaltyMultiplier;
        public int startingUnlockedLaneCount;

        /// <summary>
        /// 기본 정의를 저장 계약용 스냅샷으로 복제합니다.
        /// </summary>
        public static WorkerStatsSnapshot FromDefinition(WorkerBaseStatsDefinition definition)
        {
            if (definition == null)
            {
                definition = WorkerBaseStatsDefinition.CreateDefault();
            }

            return new WorkerStatsSnapshot
            {
                baseSessionDurationSeconds = definition.baseSessionDurationSeconds,
                baseMaxHandleWeight = definition.baseMaxHandleWeight,
                baseLaneMoveDurationSeconds = definition.baseLaneMoveDurationSeconds,
                baseTimingWindowHalfDepth = definition.baseTimingWindowHalfDepth,
                baseRewardMultiplier = definition.baseRewardMultiplier,
                basePenaltyMultiplier = definition.basePenaltyMultiplier,
                startingUnlockedLaneCount = definition.startingUnlockedLaneCount
            };
        }
    }

    /// <summary>
    /// protobuf로 옮기기 쉬운 노드 해금 상태 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class UnlockedSkillNodeState
    {
        public string nodeId;
        public int level;
        public bool isUnlocked;
    }

    /// <summary>
    /// protobuf 대응용 메타 진행 루트 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class PlayerMetaProgressionSnapshot
    {
        public int schemaVersion;
        public WorkerStatsSnapshot workerStats;
        public PlayerCurrencySnapshot currency;
        public List<UnlockedSkillNodeState> unlockedNodeStates = new List<UnlockedSkillNodeState>();
        public List<string> selectedAutomationFlags = new List<string>();
        public int resolvedLoadoutVersion;
    }

    /// <summary>
    /// 플레이어가 벌고 사용한 재화를 세션 범위에서 추적하는 DTO입니다.
    /// </summary>
    [Serializable]
    public sealed class PlayerCurrencySnapshot
    {
        public int currentBalance;
        public int totalBattleEarned;
        public int totalSkillSpent;

        /// <summary>
        /// 0원 기준 기본 재화 상태를 생성합니다.
        /// </summary>
        public static PlayerCurrencySnapshot CreateDefault()
        {
            return new PlayerCurrencySnapshot
            {
                currentBalance = 0,
                totalBattleEarned = 0,
                totalSkillSpent = 0
            };
        }
    }

    /// <summary>
    /// 카탈로그가 제공하는 작업자 기본 스탯 정의입니다.
    /// </summary>
    [Serializable]
    public sealed class WorkerBaseStatsDefinition
    {
        [Min(1f)] public float baseSessionDurationSeconds = PrototypeSessionRuntime.DefaultBaseWorkDurationSeconds;
        [Min(1)] public int baseMaxHandleWeight = 10;
        [Min(0.01f)] public float baseLaneMoveDurationSeconds = 0.18f;
        [Min(0.01f)] public float baseTimingWindowHalfDepth = 0.45f;
        [Min(0.01f)] public float baseRewardMultiplier = 1f;
        [Min(0.01f)] public float basePenaltyMultiplier = 1f;
        [Min(1)] public int startingUnlockedLaneCount = 2;

        /// <summary>
        /// 물류센터 기획서 기준 기본 스탯 묶음을 생성합니다.
        /// </summary>
        public static WorkerBaseStatsDefinition CreateDefault()
        {
            return new WorkerBaseStatsDefinition
            {
                baseSessionDurationSeconds = PrototypeSessionRuntime.DefaultBaseWorkDurationSeconds,
                baseMaxHandleWeight = 10,
                baseLaneMoveDurationSeconds = 0.18f,
                baseTimingWindowHalfDepth = 0.45f,
                baseRewardMultiplier = 1f,
                basePenaltyMultiplier = 1f,
                startingUnlockedLaneCount = 2
            };
        }
    }

    /// <summary>
    /// 스킬 트리 상위 탭 표시 메타데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillTreeTabDefinition
    {
        public SkillTreeTabId tabId;
        public string displayName;
        public int sortOrder;
    }

    /// <summary>
    /// 스킬 트리 브랜치 표시 메타데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillBranchDefinition
    {
        public SkillTreeTabId tabId;
        public SkillBranchId branchId;
        public string displayName;
        public int sortOrder;
    }

    /// <summary>
    /// 개별 노드 효과 정의입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillEffectDefinition
    {
        public SkillEffectType effectType;
        public SkillEffectOperation operation;
        public int intValue;
        public float floatValue;
        public string targetKey;
    }

    /// <summary>
    /// 단일 스킬 노드 정의입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillNodeDefinition
    {
        public string nodeId;
        public SkillBranchId branchId;
        public string displayName;
        public int tier;
        public int maxLevel = 1;
        public int cost = 1;
        public List<string> prerequisiteNodeIds = new List<string>();
        public List<SkillEffectDefinition> effects = new List<SkillEffectDefinition>();
    }

    /// <summary>
    /// 새 세이브 시작 시 적용할 메타 진행 기본값입니다.
    /// </summary>
    [Serializable]
    public sealed class StartingProgressionDefinition
    {
        public List<UnlockedSkillNodeState> unlockedNodeStates = new List<UnlockedSkillNodeState>();
        public List<string> selectedAutomationFlags = new List<string>();
        public int resolvedLoadoutVersion;

        /// <summary>
        /// 비어 있는 시작 진행 상태를 생성합니다.
        /// </summary>
        public static StartingProgressionDefinition CreateDefault()
        {
            return new StartingProgressionDefinition
            {
                unlockedNodeStates = new List<UnlockedSkillNodeState>
                {
                    new UnlockedSkillNodeState
                    {
                        nodeId = MetaProgressionCatalogAsset.LoadingDockUnlockNodeId,
                        level = 1,
                        isUnlocked = true
                    },
                },
                selectedAutomationFlags = new List<string>(),
                resolvedLoadoutVersion = 1
            };
        }
    }

    /// <summary>
    /// 물류센터 메타 성장의 ScriptableObject 루트 카탈로그입니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DefaultMetaProgressionCatalog",
        menuName = "ClikerSlash/Meta/Meta Progression Catalog")]
    public sealed class MetaProgressionCatalogAsset : ScriptableObject
    {
        public const int DefaultSchemaVersion = 3;
        public const string DefaultResourcePath = "MetaProgression/DefaultMetaProgressionCatalog";
        public const string StarterVitalityNodeId = "vitality.basic_stamina_training";
        public const string LaneExpansionNodeIdTier1 = "management.lane_expansion_i";
        public const string LaneExpansionNodeIdTier2 = "management.lane_expansion_ii";
        public const string LaneExpansionNodeIdTier3 = "management.lane_expansion_iii";
        public const string LoadingDockUnlockNodeId = "management.loading_dock_unlock";
        public const string WeightPreviewFlag = "automation.weight_preview";
        public const string AssistArmFlag = "automation.assist_arm";
        public const string LoadingDockUnlockFlag = "center.loading_dock_access";

        [Min(1)] public int schemaVersion = DefaultSchemaVersion;
        public WorkerBaseStatsDefinition workerBaseStats = WorkerBaseStatsDefinition.CreateDefault();
        public MetaProgressionTreeAsset centerTree;
        public MetaProgressionTreeAsset humanTree;
        public MetaProgressionTreeAsset robotTree;
        public StartingProgressionDefinition startingProgression = StartingProgressionDefinition.CreateDefault();

        [FormerlySerializedAs("skillTabs")]
        [SerializeField] private List<SkillTreeTabDefinition> legacySkillTabs = new List<SkillTreeTabDefinition>();

        [FormerlySerializedAs("skillBranches")]
        [SerializeField] private List<SkillBranchDefinition> legacySkillBranches = new List<SkillBranchDefinition>();

        [FormerlySerializedAs("skillNodes")]
        [SerializeField] private List<SkillNodeDefinition> legacySkillNodes = new List<SkillNodeDefinition>();

        [NonSerialized] private readonly List<SkillTreeTabDefinition> mergedSkillTabs = new List<SkillTreeTabDefinition>();
        [NonSerialized] private readonly List<SkillBranchDefinition> mergedSkillBranches = new List<SkillBranchDefinition>();
        [NonSerialized] private readonly List<SkillNodeDefinition> mergedSkillNodes = new List<SkillNodeDefinition>();
        [NonSerialized] private bool mergedCachesDirty = true;

        public List<SkillTreeTabDefinition> skillTabs
        {
            get
            {
                EnsureDefaults();
                return mergedSkillTabs;
            }
            set
            {
                legacySkillTabs = value ?? new List<SkillTreeTabDefinition>();
                InvalidateMergedCaches();
            }
        }

        public List<SkillBranchDefinition> skillBranches
        {
            get
            {
                EnsureDefaults();
                return mergedSkillBranches;
            }
            set
            {
                legacySkillBranches = value ?? new List<SkillBranchDefinition>();
                InvalidateMergedCaches();
            }
        }

        public List<SkillNodeDefinition> skillNodes
        {
            get
            {
                EnsureDefaults();
                return mergedSkillNodes;
            }
            set
            {
                legacySkillNodes = value ?? new List<SkillNodeDefinition>();
                InvalidateMergedCaches();
            }
        }

        /// <summary>
        /// 리소스 기본 카탈로그를 읽고, 없으면 메모리 기본값 인스턴스를 생성합니다.
        /// </summary>
        public static MetaProgressionCatalogAsset LoadDefaultCatalog()
        {
            var catalog = Resources.Load<MetaProgressionCatalogAsset>(DefaultResourcePath);
            if (catalog != null)
            {
                catalog.EnsureDefaults();
                return catalog;
            }

            var runtimeDefault = CreateInstance<MetaProgressionCatalogAsset>();
            runtimeDefault.name = "RuntimeDefaultMetaProgressionCatalog";
            runtimeDefault.ResetToLogisticsDefaults();
            return runtimeDefault;
        }

        /// <summary>
        /// 인스펙터에서 값이 비었을 때 물류센터 기준 기본값을 복원합니다.
        /// </summary>
        public void OnValidate()
        {
            EnsureDefaults();
        }

        /// <summary>
        /// 카탈로그 안에서 노드 id로 정의를 검색합니다.
        /// </summary>
        public bool TryGetNodeDefinition(string nodeId, out SkillNodeDefinition nodeDefinition)
        {
            EnsureDefaults();
            foreach (var node in mergedSkillNodes)
            {
                if (node != null && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal))
                {
                    nodeDefinition = node;
                    return true;
                }
            }

            nodeDefinition = null;
            return false;
        }

        /// <summary>
        /// 브랜치 식별자로 표시용 메타데이터를 검색합니다.
        /// </summary>
        public bool TryGetBranchDefinition(SkillBranchId branchId, out SkillBranchDefinition branchDefinition)
        {
            EnsureDefaults();
            foreach (var branch in mergedSkillBranches)
            {
                if (branch != null && branch.branchId == branchId)
                {
                    branchDefinition = branch;
                    return true;
                }
            }

            branchDefinition = null;
            return false;
        }

        /// <summary>
        /// 탭 식별자로 표시용 메타데이터를 검색합니다.
        /// </summary>
        public bool TryGetTabDefinition(SkillTreeTabId tabId, out SkillTreeTabDefinition tabDefinition)
        {
            EnsureDefaults();
            foreach (var tab in mergedSkillTabs)
            {
                if (tab != null && tab.tabId == tabId)
                {
                    tabDefinition = tab;
                    return true;
                }
            }

            tabDefinition = null;
            return false;
        }

        /// <summary>
        /// 비어 있는 필드를 기획 기준 기본 묶음으로 채웁니다.
        /// </summary>
        public void EnsureDefaults()
        {
            schemaVersion = Mathf.Max(DefaultSchemaVersion, schemaVersion);
            workerBaseStats ??= WorkerBaseStatsDefinition.CreateDefault();
            legacySkillTabs ??= new List<SkillTreeTabDefinition>();
            legacySkillBranches ??= new List<SkillBranchDefinition>();
            legacySkillNodes ??= new List<SkillNodeDefinition>();
            startingProgression ??= StartingProgressionDefinition.CreateDefault();
            startingProgression.unlockedNodeStates ??= new List<UnlockedSkillNodeState>();
            startingProgression.selectedAutomationFlags ??= new List<string>();
            NormalizeStartingProgression(startingProgression);
            if (!mergedCachesDirty && mergedSkillTabs.Count > 0 && mergedSkillBranches.Count > 0 && mergedSkillNodes.Count > 0)
            {
                return;
            }

            RebuildMergedCaches();
        }

        /// <summary>
        /// 카탈로그 전체를 물류센터 기본 스키마로 되돌립니다.
        /// </summary>
        [ContextMenu("Reset To Logistics Defaults")]
        public void ResetToLogisticsDefaults()
        {
            schemaVersion = DefaultSchemaVersion;
            workerBaseStats = WorkerBaseStatsDefinition.CreateDefault();
            startingProgression = StartingProgressionDefinition.CreateDefault();
            legacySkillTabs = new List<SkillTreeTabDefinition>();
            legacySkillBranches = new List<SkillBranchDefinition>();
            legacySkillNodes = new List<SkillNodeDefinition>();
            centerTree = ResetOrCreateTree(centerTree, SkillTreeTabId.Center);
            humanTree = ResetOrCreateTree(humanTree, SkillTreeTabId.Human);
            robotTree = ResetOrCreateTree(robotTree, SkillTreeTabId.Robot);
            RebuildMergedCaches();
        }

        internal static SkillTreeTabDefinition CreateDefaultTabDefinition(SkillTreeTabId tabId)
        {
            foreach (var tab in CreateDefaultTabs())
            {
                if (tab.tabId == tabId)
                {
                    return CloneTabDefinition(tab);
                }
            }

            return new SkillTreeTabDefinition
            {
                tabId = tabId,
                displayName = tabId.ToString(),
                sortOrder = 0
            };
        }

        internal static List<SkillTreeTabDefinition> CreateDefaultTabs()
        {
            return new List<SkillTreeTabDefinition>
            {
                new SkillTreeTabDefinition { tabId = SkillTreeTabId.Center, displayName = "물류 센터 성능", sortOrder = 0 },
                new SkillTreeTabDefinition { tabId = SkillTreeTabId.Human, displayName = "사람 성능", sortOrder = 1 },
                new SkillTreeTabDefinition { tabId = SkillTreeTabId.Robot, displayName = "로봇 성능", sortOrder = 2 }
            };
        }

        private IEnumerable<MetaProgressionTreeAsset> ResolveActiveTrees()
        {
            if (centerTree != null || humanTree != null || robotTree != null)
            {
                centerTree ??= CreateRuntimeTreeAssetFromLegacy(SkillTreeTabId.Center);
                humanTree ??= CreateRuntimeTreeAssetFromLegacy(SkillTreeTabId.Human);
                robotTree ??= CreateRuntimeTreeAssetFromLegacy(SkillTreeTabId.Robot);

                yield return centerTree;
                yield return humanTree;
                yield return robotTree;
                yield break;
            }

            yield return CreateRuntimeTreeAssetFromLegacy(SkillTreeTabId.Center);
            yield return CreateRuntimeTreeAssetFromLegacy(SkillTreeTabId.Human);
            yield return CreateRuntimeTreeAssetFromLegacy(SkillTreeTabId.Robot);
        }

        private MetaProgressionTreeAsset CreateRuntimeTreeAssetFromLegacy(SkillTreeTabId tabId)
        {
            var tree = CreateRuntimeTreeAsset(tabId);
            var legacyTab = FindTabDefinition(legacySkillTabs, tabId);
            if (legacyTab != null)
            {
                tree.displayName = string.IsNullOrWhiteSpace(legacyTab.displayName)
                    ? tree.displayName
                    : legacyTab.displayName;
                tree.sortOrder = legacyTab.sortOrder;
            }

            tree.branches = ExtractLegacyBranchesForTab(tabId);
            tree.nodes = ExtractLegacyNodesForTab(tabId);
            tree.EnsureDefaults();
            return tree;
        }

        private List<SkillBranchDefinition> ExtractLegacyBranchesForTab(SkillTreeTabId tabId)
        {
            var branches = new List<SkillBranchDefinition>();
            foreach (var branch in legacySkillBranches)
            {
                if (branch == null)
                {
                    continue;
                }

                if (ResolveTabIdForBranch(branch.branchId) != tabId)
                {
                    continue;
                }

                branches.Add(CloneBranchDefinition(branch));
            }

            return branches;
        }

        private List<SkillNodeDefinition> ExtractLegacyNodesForTab(SkillTreeTabId tabId)
        {
            var allowedBranchIds = new HashSet<SkillBranchId>();
            foreach (var branch in CreateDefaultBranchesForTab(tabId))
            {
                allowedBranchIds.Add(branch.branchId);
            }

            var nodes = new List<SkillNodeDefinition>();
            foreach (var node in legacySkillNodes)
            {
                if (node != null && allowedBranchIds.Contains(node.branchId))
                {
                    nodes.Add(CloneNodeDefinition(node));
                }
            }

            return nodes;
        }

        private static MetaProgressionTreeAsset CreateRuntimeTreeAsset(SkillTreeTabId tabId)
        {
            var tree = CreateInstance<MetaProgressionTreeAsset>();
            tree.name = tabId + "MetaProgressionTree(Runtime)";
            tree.tabId = tabId;
            tree.ResetToLogisticsDefaults();
            return tree;
        }

        private static MetaProgressionTreeAsset ResetOrCreateTree(MetaProgressionTreeAsset tree, SkillTreeTabId tabId)
        {
            tree ??= CreateRuntimeTreeAsset(tabId);
            tree.tabId = tabId;
            tree.ResetToLogisticsDefaults();
            return tree;
        }

        private void RebuildMergedCaches()
        {
            mergedSkillTabs.Clear();
            mergedSkillBranches.Clear();
            mergedSkillNodes.Clear();

            var tabs = new List<SkillTreeTabDefinition>();
            foreach (var tree in ResolveActiveTrees())
            {
                if (tree == null)
                {
                    continue;
                }

                tree.EnsureDefaults();
                tabs.Add(new SkillTreeTabDefinition
                {
                    tabId = tree.tabId,
                    displayName = tree.displayName,
                    sortOrder = tree.sortOrder
                });

                foreach (var branch in tree.branches)
                {
                    if (branch != null)
                    {
                        mergedSkillBranches.Add(CloneBranchDefinition(branch));
                    }
                }

                foreach (var node in tree.nodes)
                {
                    if (node != null)
                    {
                        mergedSkillNodes.Add(CloneNodeDefinition(node));
                    }
                }
            }

            NormalizeTabs(tabs);
            mergedSkillTabs.AddRange(tabs);
            mergedSkillBranches.Sort((left, right) => left.sortOrder.CompareTo(right.sortOrder));
            mergedSkillNodes.Sort(CompareNodes);
            mergedCachesDirty = false;
        }

        private void InvalidateMergedCaches()
        {
            mergedSkillTabs.Clear();
            mergedSkillBranches.Clear();
            mergedSkillNodes.Clear();
            mergedCachesDirty = true;
        }

        private static void NormalizeStartingProgression(StartingProgressionDefinition startingProgression)
        {
            if (startingProgression == null)
            {
                return;
            }

            if (FindUnlockedNodeState(startingProgression.unlockedNodeStates, LoadingDockUnlockNodeId) == null)
            {
                startingProgression.unlockedNodeStates.Add(new UnlockedSkillNodeState
                {
                    nodeId = LoadingDockUnlockNodeId,
                    level = 1,
                    isUnlocked = true
                });
            }

            startingProgression.resolvedLoadoutVersion = Mathf.Max(1, startingProgression.resolvedLoadoutVersion);
        }

        private static SkillTreeTabId ResolveTabIdForBranch(SkillBranchId branchId)
        {
            return branchId switch
            {
                SkillBranchId.Management => SkillTreeTabId.Center,
                SkillBranchId.Automation => SkillTreeTabId.Robot,
                _ => SkillTreeTabId.Human
            };
        }

        internal static List<SkillBranchDefinition> CreateDefaultBranches()
        {
            return new List<SkillBranchDefinition>
            {
                new SkillBranchDefinition { tabId = SkillTreeTabId.Human, branchId = SkillBranchId.Vitality, displayName = "체력", sortOrder = 0 },
                new SkillBranchDefinition { tabId = SkillTreeTabId.Human, branchId = SkillBranchId.Strength, displayName = "근력", sortOrder = 1 },
                new SkillBranchDefinition { tabId = SkillTreeTabId.Human, branchId = SkillBranchId.Mobility, displayName = "이동", sortOrder = 2 },
                new SkillBranchDefinition { tabId = SkillTreeTabId.Human, branchId = SkillBranchId.Mastery, displayName = "숙련", sortOrder = 3 },
                new SkillBranchDefinition { tabId = SkillTreeTabId.Center, branchId = SkillBranchId.Management, displayName = "경영", sortOrder = 4 },
                new SkillBranchDefinition { tabId = SkillTreeTabId.Robot, branchId = SkillBranchId.Automation, displayName = "자동화", sortOrder = 5 }
            };
        }

        internal static List<SkillBranchDefinition> CreateDefaultBranchesForTab(SkillTreeTabId tabId)
        {
            var branchesForTab = new List<SkillBranchDefinition>();
            foreach (var branch in CreateDefaultBranches())
            {
                if (branch.tabId == tabId)
                {
                    branchesForTab.Add(CloneBranchDefinition(branch));
                }
            }

            return branchesForTab;
        }

        internal static List<SkillNodeDefinition> CreateDefaultNodes()
        {
            return new List<SkillNodeDefinition>
            {
                CreateNode(
                    StarterVitalityNodeId,
                    SkillBranchId.Vitality,
                    "기초 체력 단련",
                    1,
                    10,
                    1,
                    new List<string>(),
                    AddFloatEffect(SkillEffectType.SessionDurationAddSeconds, 10f)),
                CreateNode(
                    "strength.basic_strength_training",
                    SkillBranchId.Strength,
                    "기초 근력 훈련",
                    1,
                    10,
                    1,
                    new List<string>(),
                    AddIntEffect(SkillEffectType.MaxHandleWeightAdd, 2)),
                CreateNode(
                    "mobility.footwork_training",
                    SkillBranchId.Mobility,
                    "발놀림 강화",
                    1,
                    5,
                    1,
                    new List<string>(),
                    MultiplyFloatEffect(SkillEffectType.LaneMoveDurationMultiplier, 0.9f)),
                CreateNode(
                    "mastery.work_sense_training",
                    SkillBranchId.Mastery,
                    "작업 감각 향상",
                    1,
                    5,
                    1,
                    new List<string>(),
                    AddFloatEffect(SkillEffectType.PerfectWindowAddSeconds, 0.05f)),
                CreateNode(
                    "management.performance_contract",
                    SkillBranchId.Management,
                    "성과급 계약",
                    1,
                    5,
                    1,
                    new List<string>(),
                    AddFloatEffect(SkillEffectType.RewardMultiplierAdd, 0.05f)),
                CreateNode(
                    LaneExpansionNodeIdTier1,
                    SkillBranchId.Management,
                    "라인 증설 I",
                    2,
                    1,
                    2,
                    new List<string> { "management.performance_contract" },
                    OverrideIntEffect(SkillEffectType.UnlockedLaneCountOverride, 3)),
                CreateNode(
                    LoadingDockUnlockNodeId,
                    SkillBranchId.Management,
                    "상하차 오픈",
                    2,
                    1,
                    2,
                    new List<string> { "management.performance_contract" },
                    UnlockCenterFlagEffect(LoadingDockUnlockFlag)),
                CreateNode(
                    LaneExpansionNodeIdTier2,
                    SkillBranchId.Management,
                    "라인 증설 II",
                    3,
                    1,
                    3,
                    new List<string> { LaneExpansionNodeIdTier1 },
                    OverrideIntEffect(SkillEffectType.UnlockedLaneCountOverride, 4)),
                CreateNode(
                    LaneExpansionNodeIdTier3,
                    SkillBranchId.Management,
                    "라인 증설 III",
                    4,
                    1,
                    4,
                    new List<string> { LaneExpansionNodeIdTier2 },
                    OverrideIntEffect(SkillEffectType.UnlockedLaneCountOverride, 5)),
                CreateNode(
                    "automation.weight_scanner",
                    SkillBranchId.Automation,
                    "자동 스캐너",
                    1,
                    1,
                    1,
                    new List<string>(),
                    UnlockFlagEffect(WeightPreviewFlag)),
                CreateNode(
                    "automation.return_belt",
                    SkillBranchId.Automation,
                    "리턴 벨트",
                    2,
                    3,
                    2,
                    new List<string> { "automation.weight_scanner" },
                    AddFloatEffect(SkillEffectType.ReturnBeltChanceAdd, 0.15f)),
                CreateNode(
                    "automation.assist_arm",
                    SkillBranchId.Automation,
                    "보조 암 설치",
                    3,
                    1,
                    3,
                    new List<string> { "automation.return_belt" },
                    UnlockFlagEffect(AssistArmFlag))
            };
        }

        internal static List<SkillNodeDefinition> CreateDefaultNodesForTab(SkillTreeTabId tabId)
        {
            var branchIdsForTab = new HashSet<SkillBranchId>();
            foreach (var branch in CreateDefaultBranchesForTab(tabId))
            {
                branchIdsForTab.Add(branch.branchId);
            }

            var nodesForTab = new List<SkillNodeDefinition>();
            foreach (var node in CreateDefaultNodes())
            {
                if (branchIdsForTab.Contains(node.branchId))
                {
                    nodesForTab.Add(CloneNodeDefinition(node));
                }
            }

            return nodesForTab;
        }

        private static void NormalizeTabs(List<SkillTreeTabDefinition> tabs)
        {
            if (tabs == null)
            {
                return;
            }

            var defaults = CreateDefaultTabs();
            var normalizedTabs = new List<SkillTreeTabDefinition>(defaults.Count);
            foreach (var defaultTab in defaults)
            {
                var existingTab = FindTabDefinition(tabs, defaultTab.tabId);
                if (existingTab == null)
                {
                    normalizedTabs.Add(CloneTabDefinition(defaultTab));
                    continue;
                }

                existingTab.sortOrder = defaultTab.sortOrder;
                if (string.IsNullOrWhiteSpace(existingTab.displayName))
                {
                    existingTab.displayName = defaultTab.displayName;
                }

                normalizedTabs.Add(existingTab);
            }

            tabs.Clear();
            tabs.AddRange(normalizedTabs);
        }

        internal static void NormalizeTreeBranches(
            SkillTreeTabId tabId,
            List<SkillBranchDefinition> branches,
            List<SkillBranchDefinition> defaults)
        {
            if (branches == null)
            {
                return;
            }

            var normalizedBranches = new List<SkillBranchDefinition>(defaults.Count);
            foreach (var defaultBranch in defaults)
            {
                var existingBranch = FindBranchDefinition(branches, defaultBranch.branchId);
                if (existingBranch == null)
                {
                    normalizedBranches.Add(CloneBranchDefinition(defaultBranch));
                    continue;
                }

                existingBranch.tabId = defaultBranch.tabId;
                existingBranch.sortOrder = defaultBranch.sortOrder;
                if (string.IsNullOrWhiteSpace(existingBranch.displayName))
                {
                    existingBranch.displayName = defaultBranch.displayName;
                }

                normalizedBranches.Add(existingBranch);
            }

            branches.Clear();
            branches.AddRange(normalizedBranches);
        }

        internal static void NormalizeTreeNodes(List<SkillNodeDefinition> nodes, List<SkillNodeDefinition> defaults)
        {
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                node.prerequisiteNodeIds ??= new List<string>();
                node.effects ??= new List<SkillEffectDefinition>();
                if (string.IsNullOrWhiteSpace(node.displayName))
                {
                    node.displayName = node.nodeId;
                }
            }

            foreach (var defaultNode in defaults)
            {
                if (FindNodeDefinition(nodes, defaultNode.nodeId) != null)
                {
                    continue;
                }

                nodes.Add(CloneNodeDefinition(defaultNode));
            }
        }

        private static SkillTreeTabDefinition FindTabDefinition(List<SkillTreeTabDefinition> tabs, SkillTreeTabId tabId)
        {
            if (tabs == null)
            {
                return null;
            }

            foreach (var tab in tabs)
            {
                if (tab != null && tab.tabId == tabId)
                {
                    return tab;
                }
            }

            return null;
        }

        private static SkillBranchDefinition FindBranchDefinition(List<SkillBranchDefinition> branches, SkillBranchId branchId)
        {
            if (branches == null)
            {
                return null;
            }

            foreach (var branch in branches)
            {
                if (branch != null && branch.branchId == branchId)
                {
                    return branch;
                }
            }

            return null;
        }

        private static SkillNodeDefinition FindNodeDefinition(List<SkillNodeDefinition> nodes, string nodeId)
        {
            if (nodes == null)
            {
                return null;
            }

            foreach (var node in nodes)
            {
                if (node != null && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        private static UnlockedSkillNodeState FindUnlockedNodeState(List<UnlockedSkillNodeState> unlockedStates, string nodeId)
        {
            if (unlockedStates == null)
            {
                return null;
            }

            foreach (var unlockedState in unlockedStates)
            {
                if (unlockedState != null && string.Equals(unlockedState.nodeId, nodeId, StringComparison.Ordinal))
                {
                    return unlockedState;
                }
            }

            return null;
        }

        private static int CompareNodes(SkillNodeDefinition left, SkillNodeDefinition right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            if (left.branchId != right.branchId)
            {
                if (FindBranchDefinition(CreateDefaultBranches(), left.branchId) is { } leftBranch &&
                    FindBranchDefinition(CreateDefaultBranches(), right.branchId) is { } rightBranch)
                {
                    var branchCompare = leftBranch.sortOrder.CompareTo(rightBranch.sortOrder);
                    if (branchCompare != 0)
                    {
                        return branchCompare;
                    }
                }

                return left.branchId.CompareTo(right.branchId);
            }

            var tierCompare = left.tier.CompareTo(right.tier);
            if (tierCompare != 0)
            {
                return tierCompare;
            }

            return string.Compare(left.displayName, right.displayName, StringComparison.Ordinal);
        }

        private static SkillTreeTabDefinition CloneTabDefinition(SkillTreeTabDefinition source)
        {
            return new SkillTreeTabDefinition
            {
                tabId = source.tabId,
                displayName = source.displayName,
                sortOrder = source.sortOrder
            };
        }

        private static SkillBranchDefinition CloneBranchDefinition(SkillBranchDefinition source)
        {
            return new SkillBranchDefinition
            {
                tabId = source.tabId,
                branchId = source.branchId,
                displayName = source.displayName,
                sortOrder = source.sortOrder
            };
        }

        private static SkillNodeDefinition CloneNodeDefinition(SkillNodeDefinition source)
        {
            var clone = new SkillNodeDefinition
            {
                nodeId = source.nodeId,
                branchId = source.branchId,
                displayName = source.displayName,
                tier = source.tier,
                maxLevel = source.maxLevel,
                cost = source.cost,
                prerequisiteNodeIds = new List<string>(source.prerequisiteNodeIds ?? new List<string>()),
                effects = new List<SkillEffectDefinition>()
            };

            if (source.effects == null)
            {
                return clone;
            }

            foreach (var effect in source.effects)
            {
                if (effect == null)
                {
                    continue;
                }

                clone.effects.Add(new SkillEffectDefinition
                {
                    effectType = effect.effectType,
                    operation = effect.operation,
                    intValue = effect.intValue,
                    floatValue = effect.floatValue,
                    targetKey = effect.targetKey
                });
            }

            return clone;
        }

        private static SkillNodeDefinition CreateNode(
            string nodeId,
            SkillBranchId branchId,
            string displayName,
            int tier,
            int maxLevel,
            int cost,
            List<string> prerequisiteNodeIds,
            params SkillEffectDefinition[] effects)
        {
            return new SkillNodeDefinition
            {
                nodeId = nodeId,
                branchId = branchId,
                displayName = displayName,
                tier = tier,
                maxLevel = maxLevel,
                cost = cost,
                prerequisiteNodeIds = prerequisiteNodeIds ?? new List<string>(),
                effects = new List<SkillEffectDefinition>(effects ?? Array.Empty<SkillEffectDefinition>())
            };
        }

        private static SkillEffectDefinition AddIntEffect(SkillEffectType effectType, int value)
        {
            return new SkillEffectDefinition
            {
                effectType = effectType,
                operation = SkillEffectOperation.Add,
                intValue = value,
                floatValue = value
            };
        }

        private static SkillEffectDefinition AddFloatEffect(SkillEffectType effectType, float value)
        {
            return new SkillEffectDefinition
            {
                effectType = effectType,
                operation = SkillEffectOperation.Add,
                intValue = Mathf.RoundToInt(value),
                floatValue = value
            };
        }

        private static SkillEffectDefinition MultiplyFloatEffect(SkillEffectType effectType, float value)
        {
            return new SkillEffectDefinition
            {
                effectType = effectType,
                operation = SkillEffectOperation.Multiply,
                intValue = Mathf.RoundToInt(value),
                floatValue = value
            };
        }

        private static SkillEffectDefinition OverrideIntEffect(SkillEffectType effectType, int value)
        {
            return new SkillEffectDefinition
            {
                effectType = effectType,
                operation = SkillEffectOperation.Override,
                intValue = value,
                floatValue = value
            };
        }

        private static SkillEffectDefinition UnlockFlagEffect(string targetKey)
        {
            return new SkillEffectDefinition
            {
                effectType = SkillEffectType.AutomationUnlockFlag,
                operation = SkillEffectOperation.Unlock,
                targetKey = targetKey
            };
        }

        private static SkillEffectDefinition UnlockCenterFlagEffect(string targetKey)
        {
            return new SkillEffectDefinition
            {
                effectType = SkillEffectType.CenterUnlockFlag,
                operation = SkillEffectOperation.Unlock,
                targetKey = targetKey
            };
        }
    }
}
