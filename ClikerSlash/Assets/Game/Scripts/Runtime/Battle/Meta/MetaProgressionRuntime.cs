using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 메타 스냅샷을 전투 세션용 숫자 집계로 변환한 결과입니다.
    /// </summary>
    public struct ResolvedMetaProgression
    {
        public int SchemaVersion;
        public int ResolvedLoadoutVersion;
        public int UnlockedNodeCount;
        public float SessionDurationSeconds;
        public int MaxHandleWeight;
        public float LaneMoveDurationSeconds;
        public float TimingWindowHalfDepth;
        public float RewardMultiplier;
        public float PenaltyMultiplier;
        public int ActiveLaneCount;
        public int PreviewCargoCount;
        public float ReturnBeltChance;
        public bool HasWeightPreview;
        public bool HasAssistArm;
        public bool HasLoadingDockAccess;
    }

    /// <summary>
    /// 런타임이 들고 있는 메타 진행 상태와 집계 결과 묶음입니다.
    /// </summary>
    public sealed class MetaProgressionRuntimeState
    {
        public PlayerMetaProgressionSnapshot snapshot;
        public ResolvedMetaProgression resolvedProgression;
    }

    /// <summary>
    /// 허브 UI가 노드 상태를 표시할 때 사용하는 읽기 전용 스냅샷입니다.
    /// </summary>
    public sealed class MetaProgressionNodeStatus
    {
        public string nodeId;
        public string displayName;
        public string tabDisplayName;
        public SkillTreeTabId tabId;
        public string branchDisplayName;
        public SkillBranchId branchId;
        public int tier;
        public int cost;
        public int currentLevel;
        public int maxLevel;
        // true면 현재 레벨이 1 이상이라 이미 일부라도 해금된 상태입니다.
        public bool isUnlocked;
        // true면 선행 max 조건을 모두 만족하지 못해 아직 진입할 수 없습니다.
        public bool isLocked;
        // true면 추가 레벨을 올릴 수 있는 상태입니다.
        public bool canUpgrade;
        // true면 최대 레벨에 도달해 더 이상 클릭 업그레이드가 일어나지 않습니다.
        public bool isMaxed;
        public bool isAffordable;
        public int currentBalance;
        public List<string> prerequisiteNodeIds = new List<string>();
        public List<string> unmetPrerequisiteNodeIds = new List<string>();
        public List<string> unmetPrerequisiteNames = new List<string>();
        public string prerequisiteSummary;
        public string affordabilitySummary;
    }

    /// <summary>
    /// 카탈로그와 해금 상태를 합쳐 현재 세션용 메타 효과를 계산합니다.
    /// </summary>
    public static class MetaProgressionCalculator
    {
        /// <summary>
        /// 카탈로그 기본값을 그대로 담은 새 메타 진행 스냅샷을 만듭니다.
        /// </summary>
        public static PlayerMetaProgressionSnapshot CreateDefaultSnapshot(MetaProgressionCatalogAsset catalog)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            catalog.EnsureDefaults();

            return new PlayerMetaProgressionSnapshot
            {
                schemaVersion = catalog.schemaVersion,
                workerStats = WorkerStatsSnapshot.FromDefinition(catalog.workerBaseStats),
                currency = PlayerCurrencySnapshot.CreateDefault(),
                unlockedNodeStates = CloneUnlockedStates(catalog.startingProgression.unlockedNodeStates),
                selectedAutomationFlags = CloneStrings(catalog.startingProgression.selectedAutomationFlags),
                resolvedLoadoutVersion = catalog.startingProgression.resolvedLoadoutVersion
            };
        }

        /// <summary>
        /// 스냅샷과 카탈로그 정의를 합쳐 현재 세션용 메타 집계를 계산합니다.
        /// </summary>
        public static ResolvedMetaProgression Resolve(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            catalog.EnsureDefaults();
            snapshot ??= CreateDefaultSnapshot(catalog);
            snapshot.workerStats ??= WorkerStatsSnapshot.FromDefinition(catalog.workerBaseStats);
            snapshot.currency ??= PlayerCurrencySnapshot.CreateDefault();
            snapshot.unlockedNodeStates ??= new List<UnlockedSkillNodeState>();
            snapshot.selectedAutomationFlags ??= new List<string>();

            var resolved = new ResolvedMetaProgression
            {
                SchemaVersion = math.max(1, snapshot.schemaVersion),
                ResolvedLoadoutVersion = math.max(0, snapshot.resolvedLoadoutVersion),
                UnlockedNodeCount = 0,
                SessionDurationSeconds = math.max(1f, snapshot.workerStats.baseSessionDurationSeconds),
                MaxHandleWeight = math.max(1, snapshot.workerStats.baseMaxHandleWeight),
                LaneMoveDurationSeconds = math.max(0.01f, snapshot.workerStats.baseLaneMoveDurationSeconds),
                TimingWindowHalfDepth = math.max(0.01f, snapshot.workerStats.baseTimingWindowHalfDepth),
                RewardMultiplier = math.max(0.01f, snapshot.workerStats.baseRewardMultiplier),
                PenaltyMultiplier = math.max(0.01f, snapshot.workerStats.basePenaltyMultiplier),
                ActiveLaneCount = math.max(1, snapshot.workerStats.startingUnlockedLaneCount),
                PreviewCargoCount = 0,
                ReturnBeltChance = 0f,
                HasWeightPreview = ContainsFlag(snapshot.selectedAutomationFlags, MetaProgressionCatalogAsset.WeightPreviewFlag),
                HasAssistArm = ContainsFlag(snapshot.selectedAutomationFlags, MetaProgressionCatalogAsset.AssistArmFlag),
                HasLoadingDockAccess = false
            };

            foreach (var unlockedState in snapshot.unlockedNodeStates)
            {
                if (unlockedState == null || !unlockedState.isUnlocked || unlockedState.level <= 0)
                {
                    continue;
                }

                if (!catalog.TryGetNodeDefinition(unlockedState.nodeId, out var nodeDefinition) || nodeDefinition == null)
                {
                    continue;
                }

                resolved.UnlockedNodeCount += 1;

                for (var levelIndex = 0; levelIndex < unlockedState.level; levelIndex += 1)
                {
                    ApplyNodeEffects(nodeDefinition, ref resolved);
                }
            }

            resolved.ActiveLaneCount = math.max(1, resolved.ActiveLaneCount);
            if (physicalLaneCount != int.MaxValue)
            {
                resolved.ActiveLaneCount = math.clamp(resolved.ActiveLaneCount, 1, math.max(1, physicalLaneCount));
            }

            resolved.LaneMoveDurationSeconds = math.max(0.05f, resolved.LaneMoveDurationSeconds);
            resolved.TimingWindowHalfDepth = math.max(0.05f, resolved.TimingWindowHalfDepth);
            resolved.SessionDurationSeconds = math.max(1f, resolved.SessionDurationSeconds);
            resolved.RewardMultiplier = math.max(0.01f, resolved.RewardMultiplier);
            resolved.PenaltyMultiplier = math.max(0.01f, resolved.PenaltyMultiplier);
            resolved.ReturnBeltChance = math.clamp(resolved.ReturnBeltChance, 0f, 1f);
            return resolved;
        }

        /// <summary>
        /// 지정한 노드의 레벨을 한 단계 올릴 수 있으면 스냅샷을 갱신합니다.
        /// </summary>
        public static bool TryUpgradeNode(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            string nodeId)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            if (!catalog.TryGetNodeDefinition(nodeId, out var nodeDefinition) || nodeDefinition == null)
            {
                return false;
            }

            if (!PrerequisitesSatisfied(snapshot, catalog, nodeDefinition))
            {
                return false;
            }

            snapshot.unlockedNodeStates ??= new List<UnlockedSkillNodeState>();
            var unlockedState = FindUnlockedState(snapshot.unlockedNodeStates, nodeId);
            if (unlockedState == null)
            {
                unlockedState = new UnlockedSkillNodeState
                {
                    nodeId = nodeId,
                    level = 0,
                    isUnlocked = true
                };
                snapshot.unlockedNodeStates.Add(unlockedState);
            }

            if (unlockedState.level >= math.max(1, nodeDefinition.maxLevel))
            {
                return false;
            }

            unlockedState.isUnlocked = true;
            unlockedState.level += 1;
            snapshot.resolvedLoadoutVersion += 1;
            snapshot.schemaVersion = math.max(1, snapshot.schemaVersion);
            return true;
        }

        /// <summary>
        /// 현재 스냅샷에서 노드 레벨을 읽습니다.
        /// </summary>
        public static int GetNodeLevel(PlayerMetaProgressionSnapshot snapshot, string nodeId)
        {
            if (snapshot?.unlockedNodeStates == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return 0;
            }

            var unlockedState = FindUnlockedState(snapshot.unlockedNodeStates, nodeId);
            return unlockedState != null && unlockedState.isUnlocked ? math.max(0, unlockedState.level) : 0;
        }

        /// <summary>
        /// 단일 노드의 현재 레벨, 잠금 상태, 선행 max 조건 충족 여부를 계산합니다.
        /// </summary>
        public static MetaProgressionNodeStatus DescribeNode(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            string nodeId)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            snapshot ??= CreateDefaultSnapshot(catalog);
            snapshot.currency ??= PlayerCurrencySnapshot.CreateDefault();
            snapshot.unlockedNodeStates ??= new List<UnlockedSkillNodeState>();

            if (!catalog.TryGetNodeDefinition(nodeId, out var nodeDefinition) || nodeDefinition == null)
            {
                return null;
            }

            return DescribeNode(snapshot, catalog, nodeDefinition);
        }

        /// <summary>
        /// 카탈로그의 모든 노드를 UI 친화적인 상태 묶음으로 변환합니다.
        /// </summary>
        public static List<MetaProgressionNodeStatus> DescribeAllNodes(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            snapshot ??= CreateDefaultSnapshot(catalog);
            catalog.EnsureDefaults();

            var statuses = new List<MetaProgressionNodeStatus>(catalog.skillNodes.Count);
            foreach (var nodeDefinition in catalog.skillNodes)
            {
                if (nodeDefinition == null)
                {
                    continue;
                }

                statuses.Add(DescribeNode(snapshot, catalog, nodeDefinition));
            }

            return statuses;
        }

        private static void ApplyNodeEffects(SkillNodeDefinition nodeDefinition, ref ResolvedMetaProgression resolved)
        {
            if (nodeDefinition.effects == null)
            {
                return;
            }

            foreach (var effect in nodeDefinition.effects)
            {
                if (effect == null)
                {
                    continue;
                }

                switch (effect.effectType)
                {
                    case SkillEffectType.SessionDurationAddSeconds:
                        resolved.SessionDurationSeconds += effect.floatValue;
                        break;

                    case SkillEffectType.MaxHandleWeightAdd:
                        resolved.MaxHandleWeight += effect.intValue;
                        break;

                    case SkillEffectType.LaneMoveDurationMultiplier:
                        resolved.LaneMoveDurationSeconds *= effect.floatValue <= 0f ? 1f : effect.floatValue;
                        break;

                    case SkillEffectType.PerfectWindowAddSeconds:
                        resolved.TimingWindowHalfDepth += effect.floatValue;
                        break;

                    case SkillEffectType.RewardMultiplierAdd:
                        resolved.RewardMultiplier += effect.floatValue;
                        break;

                    case SkillEffectType.PenaltyMultiplierAdd:
                        resolved.PenaltyMultiplier += effect.floatValue;
                        break;

                    case SkillEffectType.UnlockedLaneCountOverride:
                        resolved.ActiveLaneCount = math.max(resolved.ActiveLaneCount, effect.intValue);
                        break;

                    case SkillEffectType.PreviewCargoCountAdd:
                        resolved.PreviewCargoCount += effect.intValue;
                        break;

                    case SkillEffectType.ReturnBeltChanceAdd:
                        resolved.ReturnBeltChance += effect.floatValue;
                        break;

                    case SkillEffectType.AutomationUnlockFlag:
                        if (string.Equals(effect.targetKey, MetaProgressionCatalogAsset.WeightPreviewFlag, StringComparison.Ordinal))
                        {
                            resolved.HasWeightPreview = true;
                        }
                        else if (string.Equals(effect.targetKey, MetaProgressionCatalogAsset.AssistArmFlag, StringComparison.Ordinal))
                        {
                            resolved.HasAssistArm = true;
                        }
                        break;

                    case SkillEffectType.CenterUnlockFlag:
                        if (string.Equals(effect.targetKey, MetaProgressionCatalogAsset.LoadingDockUnlockFlag, StringComparison.Ordinal))
                        {
                            resolved.HasLoadingDockAccess = true;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 선행 노드가 모두 최대 레벨까지 올라가야 다음 노드가 열리도록 확인합니다.
        /// </summary>
        private static bool PrerequisitesSatisfied(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            SkillNodeDefinition nodeDefinition)
        {
            if (nodeDefinition.prerequisiteNodeIds == null || nodeDefinition.prerequisiteNodeIds.Count == 0)
            {
                return true;
            }

            foreach (var prerequisiteNodeId in nodeDefinition.prerequisiteNodeIds)
            {
                if (!catalog.TryGetNodeDefinition(prerequisiteNodeId, out var prerequisiteDefinition) ||
                    prerequisiteDefinition == null)
                {
                    return false;
                }

                if (GetNodeLevel(snapshot, prerequisiteNodeId) < math.max(1, prerequisiteDefinition.maxLevel))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 내부 정의를 기준으로 노드 상태 DTO를 조립합니다.
        /// </summary>
        private static MetaProgressionNodeStatus DescribeNode(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            SkillNodeDefinition nodeDefinition)
        {
            var currentLevel = GetNodeLevel(snapshot, nodeDefinition.nodeId);
            var maxLevel = math.max(1, nodeDefinition.maxLevel);
            var currentBalance = math.max(0, snapshot.currency?.currentBalance ?? 0);
            var cost = math.max(0, nodeDefinition.cost);
            var status = new MetaProgressionNodeStatus
            {
                nodeId = nodeDefinition.nodeId,
                displayName = string.IsNullOrWhiteSpace(nodeDefinition.displayName)
                    ? nodeDefinition.nodeId
                    : nodeDefinition.displayName,
                tabId = ResolveTabId(catalog, nodeDefinition.branchId),
                tabDisplayName = ResolveTabDisplayName(catalog, nodeDefinition.branchId),
                branchId = nodeDefinition.branchId,
                branchDisplayName = ResolveBranchDisplayName(catalog, nodeDefinition.branchId),
                tier = math.max(1, nodeDefinition.tier),
                cost = cost,
                currentLevel = currentLevel,
                maxLevel = maxLevel,
                currentBalance = currentBalance,
                isUnlocked = currentLevel > 0,
                isMaxed = currentLevel >= maxLevel
            };

            if (nodeDefinition.prerequisiteNodeIds != null)
            {
                status.prerequisiteNodeIds.AddRange(nodeDefinition.prerequisiteNodeIds);
            }

            PopulateUnmetPrerequisites(snapshot, catalog, nodeDefinition, status);
            status.isLocked = currentLevel <= 0 && status.unmetPrerequisiteNodeIds.Count > 0;
            status.isAffordable = cost <= currentBalance;
            status.canUpgrade = !status.isLocked && !status.isMaxed && status.isAffordable;
            status.prerequisiteSummary = status.unmetPrerequisiteNames.Count == 0
                ? "선행 max 조건 충족"
                : $"필요: {string.Join(", ", status.unmetPrerequisiteNames)}";
            status.affordabilitySummary = BuildAffordabilitySummary(status);
            return status;
        }

        private static string BuildAffordabilitySummary(MetaProgressionNodeStatus status)
        {
            if (status.isLocked)
            {
                return "선행 스킬 조건 필요";
            }

            if (status.isMaxed)
            {
                return "최대 레벨 도달";
            }

            return status.isAffordable
                ? $"구매 가능 (보유 {status.currentBalance})"
                : $"재화 부족 (보유 {status.currentBalance} / 필요 {status.cost})";
        }

        /// <summary>
        /// 미충족 선행 노드를 이름 기준으로 정리해 UI가 바로 문자열로 쓸 수 있게 합니다.
        /// </summary>
        private static void PopulateUnmetPrerequisites(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            SkillNodeDefinition nodeDefinition,
            MetaProgressionNodeStatus status)
        {
            if (nodeDefinition.prerequisiteNodeIds == null)
            {
                return;
            }

            foreach (var prerequisiteNodeId in nodeDefinition.prerequisiteNodeIds)
            {
                if (!catalog.TryGetNodeDefinition(prerequisiteNodeId, out var prerequisiteDefinition) ||
                    prerequisiteDefinition == null)
                {
                    status.unmetPrerequisiteNodeIds.Add(prerequisiteNodeId);
                    status.unmetPrerequisiteNames.Add(prerequisiteNodeId);
                    continue;
                }

                if (GetNodeLevel(snapshot, prerequisiteNodeId) >= math.max(1, prerequisiteDefinition.maxLevel))
                {
                    continue;
                }

                status.unmetPrerequisiteNodeIds.Add(prerequisiteNodeId);
                status.unmetPrerequisiteNames.Add(prerequisiteDefinition.displayName);
            }
        }

        /// <summary>
        /// 브랜치명이 비어 있으면 enum 이름으로 폴백해 UI 표기를 유지합니다.
        /// </summary>
        private static string ResolveBranchDisplayName(MetaProgressionCatalogAsset catalog, SkillBranchId branchId)
        {
            if (catalog.TryGetBranchDefinition(branchId, out var branchDefinition) &&
                branchDefinition != null &&
                !string.IsNullOrWhiteSpace(branchDefinition.displayName))
            {
                return branchDefinition.displayName;
            }

            return branchId.ToString();
        }

        /// <summary>
        /// 브랜치 매핑이 비어 있으면 사람 탭으로 폴백해 기존 표시를 깨지 않게 유지합니다.
        /// </summary>
        private static SkillTreeTabId ResolveTabId(MetaProgressionCatalogAsset catalog, SkillBranchId branchId)
        {
            if (catalog.TryGetBranchDefinition(branchId, out var branchDefinition) && branchDefinition != null)
            {
                return branchDefinition.tabId;
            }

            return SkillTreeTabId.Human;
        }

        /// <summary>
        /// 상위 탭 이름을 읽어 허브가 탭-브랜치 관계를 함께 표시할 수 있게 합니다.
        /// </summary>
        private static string ResolveTabDisplayName(MetaProgressionCatalogAsset catalog, SkillBranchId branchId)
        {
            var tabId = ResolveTabId(catalog, branchId);
            if (catalog.TryGetTabDefinition(tabId, out var tabDefinition) &&
                tabDefinition != null &&
                !string.IsNullOrWhiteSpace(tabDefinition.displayName))
            {
                return tabDefinition.displayName;
            }

            return tabId.ToString();
        }

        private static bool ContainsFlag(List<string> flags, string targetFlag)
        {
            if (flags == null)
            {
                return false;
            }

            foreach (var flag in flags)
            {
                if (string.Equals(flag, targetFlag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static UnlockedSkillNodeState FindUnlockedState(List<UnlockedSkillNodeState> unlockedStates, string nodeId)
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

        private static List<UnlockedSkillNodeState> CloneUnlockedStates(List<UnlockedSkillNodeState> source)
        {
            var clone = new List<UnlockedSkillNodeState>();
            if (source == null)
            {
                return clone;
            }

            foreach (var unlockedState in source)
            {
                if (unlockedState == null)
                {
                    continue;
                }

                clone.Add(new UnlockedSkillNodeState
                {
                    nodeId = unlockedState.nodeId,
                    level = unlockedState.level,
                    isUnlocked = unlockedState.isUnlocked
                });
            }

            return clone;
        }

        private static List<string> CloneStrings(List<string> source)
        {
            return source == null ? new List<string>() : new List<string>(source);
        }
    }

    /// <summary>
    /// 런타임 상태와 protobuf-ready DTO 사이를 왕복 복제합니다.
    /// </summary>
    public static class MetaProgressionProtoContractMapper
    {
        /// <summary>
        /// 런타임 상태를 직렬화용 계약 객체로 깊은 복제합니다.
        /// </summary>
        public static PlayerMetaProgressionSnapshot ToContract(MetaProgressionRuntimeState runtimeState)
        {
            return Clone(runtimeState?.snapshot);
        }

        /// <summary>
        /// 직렬화 계약에서 런타임 상태와 집계 결과를 다시 만듭니다.
        /// </summary>
        public static MetaProgressionRuntimeState FromContract(
            PlayerMetaProgressionSnapshot contract,
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            var clonedSnapshot = Clone(contract) ?? MetaProgressionCalculator.CreateDefaultSnapshot(catalog);
            return new MetaProgressionRuntimeState
            {
                snapshot = clonedSnapshot,
                resolvedProgression = MetaProgressionCalculator.Resolve(clonedSnapshot, catalog, physicalLaneCount)
            };
        }

        /// <summary>
        /// 계약 객체를 protobuf 친화적인 형태 그대로 깊은 복제합니다.
        /// </summary>
        public static PlayerMetaProgressionSnapshot Clone(PlayerMetaProgressionSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            return new PlayerMetaProgressionSnapshot
            {
                schemaVersion = source.schemaVersion,
                workerStats = source.workerStats == null
                    ? null
                    : new WorkerStatsSnapshot
                    {
                        baseSessionDurationSeconds = source.workerStats.baseSessionDurationSeconds,
                        baseMaxHandleWeight = source.workerStats.baseMaxHandleWeight,
                        baseLaneMoveDurationSeconds = source.workerStats.baseLaneMoveDurationSeconds,
                        baseTimingWindowHalfDepth = source.workerStats.baseTimingWindowHalfDepth,
                        baseRewardMultiplier = source.workerStats.baseRewardMultiplier,
                        basePenaltyMultiplier = source.workerStats.basePenaltyMultiplier,
                        startingUnlockedLaneCount = source.workerStats.startingUnlockedLaneCount
                    },
                currency = source.currency == null
                    ? PlayerCurrencySnapshot.CreateDefault()
                    : new PlayerCurrencySnapshot
                    {
                        currentBalance = source.currency.currentBalance,
                        totalBattleEarned = source.currency.totalBattleEarned,
                        totalSkillSpent = source.currency.totalSkillSpent
                    },
                unlockedNodeStates = source.unlockedNodeStates == null
                    ? new List<UnlockedSkillNodeState>()
                    : CloneUnlockedStates(source.unlockedNodeStates),
                selectedAutomationFlags = source.selectedAutomationFlags == null
                    ? new List<string>()
                    : new List<string>(source.selectedAutomationFlags),
                resolvedLoadoutVersion = source.resolvedLoadoutVersion
            };
        }

        private static List<UnlockedSkillNodeState> CloneUnlockedStates(List<UnlockedSkillNodeState> source)
        {
            var clone = new List<UnlockedSkillNodeState>();
            if (source == null)
            {
                return clone;
            }

            foreach (var unlockedState in source)
            {
                if (unlockedState == null)
                {
                    continue;
                }

                clone.Add(new UnlockedSkillNodeState
                {
                    nodeId = unlockedState.nodeId,
                    level = unlockedState.level,
                    isUnlocked = unlockedState.isUnlocked
                });
            }

            return clone;
        }
    }

    /// <summary>
    /// 메타 카탈로그와 프로토타입 런타임 사이의 경계를 담당합니다.
    /// </summary>
    public static class MetaProgressionBootstrapBridge
    {
        /// <summary>
        /// 명시적인 카탈로그가 없으면 기본 리소스 카탈로그를 사용합니다.
        /// </summary>
        public static MetaProgressionCatalogAsset ResolveCatalog(MetaProgressionCatalogAsset catalog)
        {
            return catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
        }

        /// <summary>
        /// 프로토타입 런타임이 현재 씬 레인 수 기준으로 메타 집계를 다시 계산하게 합니다.
        /// </summary>
        public static MetaProgressionRuntimeState EnsureRuntimeState(
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount)
        {
            catalog = ResolveCatalog(catalog);
            PrototypeSessionRuntime.EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            return PrototypeSessionRuntime.GetMetaProgressionRuntimeState();
        }

        /// <summary>
        /// 메타 집계 결과를 현재 세션 BattleConfig에 반영합니다.
        /// </summary>
        public static BattleConfig ApplyToBattleConfig(BattleConfig battleConfig, ResolvedMetaProgression resolvedProgression)
        {
            battleConfig.PlayerMoveDuration = math.max(0.05f, resolvedProgression.LaneMoveDurationSeconds);
            battleConfig.StartingMaxHandleWeight = math.max(1, resolvedProgression.MaxHandleWeight);
            battleConfig.HandleWindowHalfDepth = math.max(0.05f, resolvedProgression.TimingWindowHalfDepth);
            return battleConfig;
        }

        /// <summary>
        /// 물리 레인 수와 메타 해금 레인 수를 합쳐 이번 세션 활성 레인 수를 결정합니다.
        /// </summary>
        public static int ResolveActiveLaneCount(ResolvedMetaProgression resolvedProgression, int physicalLaneCount)
        {
            return math.clamp(resolvedProgression.ActiveLaneCount, 1, math.max(1, physicalLaneCount));
        }
    }
}
