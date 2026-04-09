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
                HasAssistArm = ContainsFlag(snapshot.selectedAutomationFlags, MetaProgressionCatalogAsset.AssistArmFlag)
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

            if (!PrerequisitesSatisfied(snapshot, nodeDefinition))
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
                }
            }
        }

        private static bool PrerequisitesSatisfied(PlayerMetaProgressionSnapshot snapshot, SkillNodeDefinition nodeDefinition)
        {
            if (nodeDefinition.prerequisiteNodeIds == null || nodeDefinition.prerequisiteNodeIds.Count == 0)
            {
                return true;
            }

            foreach (var prerequisiteNodeId in nodeDefinition.prerequisiteNodeIds)
            {
                if (GetNodeLevel(snapshot, prerequisiteNodeId) <= 0)
                {
                    return false;
                }
            }

            return true;
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
