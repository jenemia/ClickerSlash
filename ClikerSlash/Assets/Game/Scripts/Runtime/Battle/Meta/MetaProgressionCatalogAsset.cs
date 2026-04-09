using System;
using System.Collections.Generic;
using UnityEngine;

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
        AutomationUnlockFlag = 9
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
        public List<UnlockedSkillNodeState> unlockedNodeStates = new List<UnlockedSkillNodeState>();
        public List<string> selectedAutomationFlags = new List<string>();
        public int resolvedLoadoutVersion;
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
    /// 스킬 트리 브랜치 표시 메타데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillBranchDefinition
    {
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
                unlockedNodeStates = new List<UnlockedSkillNodeState>(),
                selectedAutomationFlags = new List<string>(),
                resolvedLoadoutVersion = 0
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
        public const int DefaultSchemaVersion = 1;
        public const string DefaultResourcePath = "MetaProgression/DefaultMetaProgressionCatalog";
        public const string StarterVitalityNodeId = "vitality.basic_stamina_training";
        public const string LaneExpansionNodeIdTier1 = "management.lane_expansion_i";
        public const string LaneExpansionNodeIdTier2 = "management.lane_expansion_ii";
        public const string LaneExpansionNodeIdTier3 = "management.lane_expansion_iii";
        public const string WeightPreviewFlag = "automation.weight_preview";
        public const string AssistArmFlag = "automation.assist_arm";

        [Min(1)] public int schemaVersion = DefaultSchemaVersion;
        public WorkerBaseStatsDefinition workerBaseStats = WorkerBaseStatsDefinition.CreateDefault();
        public List<SkillBranchDefinition> skillBranches = new List<SkillBranchDefinition>();
        public List<SkillNodeDefinition> skillNodes = new List<SkillNodeDefinition>();
        public StartingProgressionDefinition startingProgression = StartingProgressionDefinition.CreateDefault();

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
            foreach (var node in skillNodes)
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
            foreach (var branch in skillBranches)
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
        /// 비어 있는 필드를 기획 기준 기본 묶음으로 채웁니다.
        /// </summary>
        public void EnsureDefaults()
        {
            schemaVersion = Mathf.Max(1, schemaVersion);
            workerBaseStats ??= WorkerBaseStatsDefinition.CreateDefault();
            skillBranches ??= CreateDefaultBranches();
            skillNodes ??= CreateDefaultNodes();
            startingProgression ??= StartingProgressionDefinition.CreateDefault();

            if (skillBranches.Count == 0)
            {
                skillBranches = CreateDefaultBranches();
            }

            if (skillNodes.Count == 0)
            {
                skillNodes = CreateDefaultNodes();
            }
        }

        /// <summary>
        /// 카탈로그 전체를 물류센터 기본 스키마로 되돌립니다.
        /// </summary>
        [ContextMenu("Reset To Logistics Defaults")]
        public void ResetToLogisticsDefaults()
        {
            schemaVersion = DefaultSchemaVersion;
            workerBaseStats = WorkerBaseStatsDefinition.CreateDefault();
            skillBranches = CreateDefaultBranches();
            skillNodes = CreateDefaultNodes();
            startingProgression = StartingProgressionDefinition.CreateDefault();
        }

        private static List<SkillBranchDefinition> CreateDefaultBranches()
        {
            return new List<SkillBranchDefinition>
            {
                new SkillBranchDefinition { branchId = SkillBranchId.Vitality, displayName = "체력", sortOrder = 0 },
                new SkillBranchDefinition { branchId = SkillBranchId.Strength, displayName = "근력", sortOrder = 1 },
                new SkillBranchDefinition { branchId = SkillBranchId.Mobility, displayName = "이동", sortOrder = 2 },
                new SkillBranchDefinition { branchId = SkillBranchId.Mastery, displayName = "숙련", sortOrder = 3 },
                new SkillBranchDefinition { branchId = SkillBranchId.Management, displayName = "경영", sortOrder = 4 },
                new SkillBranchDefinition { branchId = SkillBranchId.Automation, displayName = "자동화", sortOrder = 5 }
            };
        }

        private static List<SkillNodeDefinition> CreateDefaultNodes()
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
    }
}
