using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 작업 결과 화면으로 넘기는 직렬화 가능한 런타임 스냅샷입니다.
    /// </summary>
    public struct BattleResultSnapshot
    {
        public int TotalMoney;
        public int ProcessedCargoCount;
        public int MissedCargoCount;
        public int CurrentCombo;
        public int MaxCombo;
        public float WorkedTimeSeconds;
        public int ApprovedCargoCount;
        public int RejectedCargoCount;
        public int CorrectRouteCount;
        public int MisrouteCount;
        public int ReturnCount;
    }

    internal enum PendingPhaseInput
    {
        None = 0,
        ApprovalReject = 1,
        ApprovalApprove = 2,
        RouteAir = 3,
        RouteSea = 4,
        RouteRail = 5,
        RouteTruck = 6,
        RouteReturn = 7
    }

    /// <summary>
    /// 저장하지 않는 프로토타입 세션 데이터를 씬 전환 사이에서 유지합니다.
    /// </summary>
    public static class PrototypeSessionRuntime
    {
        public const string BattleSceneName = "PrototypeBattle";
        public const string HubSceneName = "PrototypeHub";
        public const int MinimumHealthLevel = 1;
        public const float DefaultBaseWorkDurationSeconds = 30f;
        public const float DefaultHealthDurationBonusSeconds = 10f;
        public const float DefaultLoadingDockTransitionDurationSeconds = 0.35f;
        public const int MaxLoadingDockActiveSlotCount = 5;
        public const int FixedRouteLaneCount = 5;
        public const int DefaultDeliveryLaneMaxWeight = 5;

        public static bool HasLastBattleResult { get; private set; }
        public static BattleResultSnapshot LastBattleResult { get; private set; }
        public static float ResolvedWorkDurationSeconds { get; private set; }
        public static bool IsPauseMenuOpen { get; private set; }
        public static bool HasPendingBattleEntryRequest { get; private set; }

        private static readonly Queue<ApprovalCargoSnapshot> _pendingApprovalQueue = new();
        private static readonly Queue<RouteSelectionCargoSnapshot> _pendingRouteQueue = new();
        private static int _nextCargoEntryId = 1;
        private static BattleMiniGamePhase _currentMiniGamePhase = BattleMiniGamePhase.Approval;
        private static bool _hasActiveCargo;
        private static PendingPhaseInput _pendingPhaseInput;
        private static MetaProgressionRuntimeState _metaProgressionRuntimeState;

        public static int HealthLevel
        {
            get
            {
                var nodeLevel = MetaProgressionCalculator.GetNodeLevel(
                    _metaProgressionRuntimeState?.snapshot,
                    MetaProgressionCatalogAsset.StarterVitalityNodeId);
                return MinimumHealthLevel + nodeLevel;
            }
        }

        public static MetaProgressionRuntimeState GetMetaProgressionRuntimeState()
        {
            return _metaProgressionRuntimeState;
        }

        public static PlayerMetaProgressionSnapshot GetMetaProgressionSnapshot()
        {
            return MetaProgressionProtoContractMapper.ToContract(_metaProgressionRuntimeState);
        }

        public static PlayerCurrencySnapshot GetCurrencySnapshot()
        {
            EnsureMetaProgressionInitialized(MetaProgressionCatalogAsset.LoadDefaultCatalog());
            var currency = _metaProgressionRuntimeState?.snapshot?.currency;
            return currency == null
                ? PlayerCurrencySnapshot.CreateDefault()
                : new PlayerCurrencySnapshot
                {
                    currentBalance = currency.currentBalance,
                    totalBattleEarned = currency.totalBattleEarned,
                    totalSkillSpent = currency.totalSkillSpent
                };
        }

        public static ResolvedMetaProgression GetResolvedMetaProgression()
        {
            if (_metaProgressionRuntimeState == null)
            {
                EnsureMetaProgressionInitialized(MetaProgressionCatalogAsset.LoadDefaultCatalog());
            }

            return _metaProgressionRuntimeState.resolvedProgression;
        }

        public static BattleMiniGamePhaseSnapshot GetBattleMiniGamePhaseSnapshot()
        {
            return new BattleMiniGamePhaseSnapshot
            {
                CurrentPhase = _currentMiniGamePhase,
                HasActiveCargo = _hasActiveCargo,
                PendingApprovalCount = _pendingApprovalQueue.Count,
                PendingRouteCount = _pendingRouteQueue.Count,
                DeliveryLaneMaxWeight = DefaultDeliveryLaneMaxWeight
            };
        }

        public static ApprovalCargoSnapshot[] GetPendingApprovalCargoEntries()
        {
            return _pendingApprovalQueue.ToArray();
        }

        public static RouteSelectionCargoSnapshot[] GetPendingRouteCargoEntries()
        {
            return _pendingRouteQueue.ToArray();
        }

        public static int GetDeliveryLaneMaxWeight(CargoRouteLane routeLane)
        {
            return routeLane == CargoRouteLane.Return ? int.MaxValue : DefaultDeliveryLaneMaxWeight;
        }

        public static bool IsCargoDeliverable(int weight)
        {
            return weight <= DefaultDeliveryLaneMaxWeight;
        }

        public static void InitializeRhythmCargoPlan(float resolvedWorkDurationSeconds, float spawnIntervalSeconds, CargoConfig cargoConfig, uint seed)
        {
            ClearRhythmQueues();
            _currentMiniGamePhase = BattleMiniGamePhase.Approval;
            _pendingPhaseInput = PendingPhaseInput.None;
            _hasActiveCargo = false;

            var safeInterval = Mathf.Max(0.25f, spawnIntervalSeconds * 2f);
            var totalCargoCount = Mathf.Max(6, Mathf.CeilToInt(resolvedWorkDurationSeconds / safeInterval));
            var randomSeed = seed == 0u ? 1u : seed;
            var random = new System.Random(unchecked((int)randomSeed));

            for (var index = 0; index < totalCargoCount; index += 1)
            {
                var kind = (LoadingDockCargoKind)random.Next(0, 3);
                var weight = ResolvePlannedWeight(kind, cargoConfig, random);
                var reward = cargoConfig.Reward + Mathf.Max(0, weight - 3) * 6;
                var penalty = cargoConfig.Penalty + Mathf.Max(0, weight - DefaultDeliveryLaneMaxWeight) * 5;
                _pendingApprovalQueue.Enqueue(new ApprovalCargoSnapshot
                {
                    EntryId = _nextCargoEntryId,
                    Kind = kind,
                    Weight = weight,
                    Reward = reward,
                    Penalty = penalty
                });
                _nextCargoEntryId += 1;
            }
        }

        public static void InitializeRhythmCargoPlan(IReadOnlyList<ApprovalCargoSnapshot> plannedCargoEntries)
        {
            ClearRhythmQueues();
            _currentMiniGamePhase = BattleMiniGamePhase.Approval;
            _pendingPhaseInput = PendingPhaseInput.None;
            _hasActiveCargo = false;

            var nextEntryId = 1;
            if (plannedCargoEntries == null)
            {
                _nextCargoEntryId = nextEntryId;
                return;
            }

            for (var index = 0; index < plannedCargoEntries.Count; index += 1)
            {
                var cargo = plannedCargoEntries[index];
                if (cargo.EntryId <= 0)
                {
                    cargo.EntryId = nextEntryId;
                }

                _pendingApprovalQueue.Enqueue(cargo);
                nextEntryId = Mathf.Max(nextEntryId, cargo.EntryId + 1);
            }

            _nextCargoEntryId = nextEntryId;
        }

        public static bool TryDequeueNextPhaseCargo(
            out BattleMiniGamePhase phase,
            out ApprovalCargoSnapshot approvalCargo,
            out RouteSelectionCargoSnapshot routeCargo)
        {
            phase = _currentMiniGamePhase;
            approvalCargo = default;
            routeCargo = default;

            if (_hasActiveCargo)
            {
                return false;
            }

            if (_currentMiniGamePhase == BattleMiniGamePhase.Approval)
            {
                if (_pendingApprovalQueue.Count > 0)
                {
                    approvalCargo = _pendingApprovalQueue.Dequeue();
                    _hasActiveCargo = true;
                    return true;
                }

                _currentMiniGamePhase = _pendingRouteQueue.Count > 0
                    ? BattleMiniGamePhase.RouteSelection
                    : BattleMiniGamePhase.Completed;
                phase = _currentMiniGamePhase;
            }

            if (_currentMiniGamePhase == BattleMiniGamePhase.RouteSelection)
            {
                if (_pendingRouteQueue.Count > 0)
                {
                    routeCargo = _pendingRouteQueue.Dequeue();
                    _hasActiveCargo = true;
                    return true;
                }

                _currentMiniGamePhase = BattleMiniGamePhase.Completed;
                phase = _currentMiniGamePhase;
            }

            return false;
        }

        public static void NotifyActiveCargoResolved()
        {
            _hasActiveCargo = false;
            if (_currentMiniGamePhase == BattleMiniGamePhase.Approval && _pendingApprovalQueue.Count == 0)
            {
                _currentMiniGamePhase = _pendingRouteQueue.Count > 0
                    ? BattleMiniGamePhase.RouteSelection
                    : BattleMiniGamePhase.Completed;
                return;
            }

            if (_currentMiniGamePhase == BattleMiniGamePhase.RouteSelection && _pendingRouteQueue.Count == 0)
            {
                _currentMiniGamePhase = BattleMiniGamePhase.Completed;
            }
        }

        public static bool IsMiniGameLoopFinished()
        {
            return _currentMiniGamePhase == BattleMiniGamePhase.Completed && !_hasActiveCargo;
        }

        public static void QueueApprovalInput(ApprovalDecision decision)
        {
            _pendingPhaseInput = decision switch
            {
                ApprovalDecision.Approve => PendingPhaseInput.ApprovalApprove,
                ApprovalDecision.Reject => PendingPhaseInput.ApprovalReject,
                _ => PendingPhaseInput.None
            };
        }

        public static void QueueRouteInput(CargoRouteLane routeLane)
        {
            _pendingPhaseInput = routeLane switch
            {
                CargoRouteLane.Air => PendingPhaseInput.RouteAir,
                CargoRouteLane.Sea => PendingPhaseInput.RouteSea,
                CargoRouteLane.Rail => PendingPhaseInput.RouteRail,
                CargoRouteLane.Truck => PendingPhaseInput.RouteTruck,
                CargoRouteLane.Return => PendingPhaseInput.RouteReturn,
                _ => PendingPhaseInput.None
            };
        }

        public static bool TryConsumeApprovalInput(out ApprovalDecision decision)
        {
            decision = _pendingPhaseInput switch
            {
                PendingPhaseInput.ApprovalApprove => ApprovalDecision.Approve,
                PendingPhaseInput.ApprovalReject => ApprovalDecision.Reject,
                _ => ApprovalDecision.None
            };

            if (decision == ApprovalDecision.None)
            {
                return false;
            }

            _pendingPhaseInput = PendingPhaseInput.None;
            return true;
        }

        public static bool TryConsumeRouteInput(out CargoRouteLane routeLane)
        {
            routeLane = _pendingPhaseInput switch
            {
                PendingPhaseInput.RouteAir => CargoRouteLane.Air,
                PendingPhaseInput.RouteSea => CargoRouteLane.Sea,
                PendingPhaseInput.RouteRail => CargoRouteLane.Rail,
                PendingPhaseInput.RouteTruck => CargoRouteLane.Truck,
                PendingPhaseInput.RouteReturn => CargoRouteLane.Return,
                _ => CargoRouteLane.Air
            };

            if (_pendingPhaseInput is < PendingPhaseInput.RouteAir or > PendingPhaseInput.RouteReturn)
            {
                return false;
            }

            _pendingPhaseInput = PendingPhaseInput.None;
            return true;
        }

        public static void EnqueueRouteSelectionCargo(ApprovalCargoSnapshot cargo, ApprovalDecision decision)
        {
            if (decision == ApprovalDecision.None)
            {
                return;
            }

            _pendingRouteQueue.Enqueue(new RouteSelectionCargoSnapshot
            {
                EntryId = cargo.EntryId,
                Kind = cargo.Kind,
                Weight = cargo.Weight,
                Reward = cargo.Reward,
                Penalty = cargo.Penalty,
                ApprovalDecision = decision,
                IsDeliverable = IsCargoDeliverable(cargo.Weight)
            });
        }

        public static void ResolveRouteOutcome(
            RouteSelectionCargoSnapshot cargo,
            CargoRouteLane selectedRoute,
            out int incomeDelta,
            out bool countsAsCorrectRoute,
            out bool countsAsReturn,
            out bool countsAsMisroute)
        {
            incomeDelta = 0;
            countsAsCorrectRoute = false;
            countsAsReturn = false;
            countsAsMisroute = false;

            if (selectedRoute == CargoRouteLane.Return)
            {
                countsAsReturn = true;
                if (cargo.ApprovalDecision == ApprovalDecision.Reject)
                {
                    return;
                }

                countsAsMisroute = true;
                incomeDelta = -cargo.Penalty;
                return;
            }

            if (cargo.ApprovalDecision == ApprovalDecision.Approve && cargo.Weight <= GetDeliveryLaneMaxWeight(selectedRoute))
            {
                countsAsCorrectRoute = true;
                incomeDelta = cargo.Reward;
                return;
            }

            countsAsMisroute = true;
            incomeDelta = -cargo.Penalty;
        }

        public static void StoreBattleResult(BattleResultSnapshot snapshot)
        {
            EnsureMetaProgressionInitialized(MetaProgressionCatalogAsset.LoadDefaultCatalog());
            LastBattleResult = snapshot;
            HasLastBattleResult = true;

            if (snapshot.TotalMoney <= 0)
            {
                return;
            }

            EnsureCurrencyInitialized();
            _metaProgressionRuntimeState.snapshot.currency.currentBalance += snapshot.TotalMoney;
            _metaProgressionRuntimeState.snapshot.currency.totalBattleEarned += snapshot.TotalMoney;
        }

        public static void ClearLastBattleResult()
        {
            HasLastBattleResult = false;
            LastBattleResult = default;
        }

        public static void ResetPrototypeState()
        {
            ClosePauseMenu();
            HasLastBattleResult = false;
            LastBattleResult = default;
            ClearRhythmQueues();
            ResolvedWorkDurationSeconds = 0f;
            HasPendingBattleEntryRequest = false;
            _currentMiniGamePhase = BattleMiniGamePhase.Approval;
            _pendingPhaseInput = PendingPhaseInput.None;
            _hasActiveCargo = false;
            _metaProgressionRuntimeState = null;
        }

        public static void EnsureMetaProgressionInitialized(
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            _metaProgressionRuntimeState ??= MetaProgressionProtoContractMapper.FromContract(
                MetaProgressionCalculator.CreateDefaultSnapshot(catalog),
                catalog,
                physicalLaneCount);
            RebuildResolvedMetaProgression(catalog, physicalLaneCount);
        }

        public static void SetMetaProgressionSnapshot(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            _metaProgressionRuntimeState = MetaProgressionProtoContractMapper.FromContract(
                snapshot,
                catalog,
                physicalLaneCount);
        }

        public static bool IncreaseHealthLevel()
        {
            return TryUpgradeNode(MetaProgressionCatalogAsset.StarterVitalityNodeId, MetaProgressionCatalogAsset.LoadDefaultCatalog());
        }

        public static bool TryUpgradeNode(
            string nodeId,
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            if (!catalog.TryGetNodeDefinition(nodeId, out var nodeDefinition) || nodeDefinition == null)
            {
                return false;
            }

            EnsureCurrencyInitialized();
            var nodeStatus = MetaProgressionCalculator.DescribeNode(_metaProgressionRuntimeState.snapshot, catalog, nodeId);
            if (nodeStatus == null || !nodeStatus.canUpgrade)
            {
                return false;
            }

            var cost = Mathf.Max(0, nodeDefinition.cost);
            _metaProgressionRuntimeState.snapshot.currency.currentBalance -= cost;
            _metaProgressionRuntimeState.snapshot.currency.totalSkillSpent += cost;

            if (!MetaProgressionCalculator.TryUpgradeNode(_metaProgressionRuntimeState.snapshot, catalog, nodeId))
            {
                _metaProgressionRuntimeState.snapshot.currency.currentBalance += cost;
                _metaProgressionRuntimeState.snapshot.currency.totalSkillSpent -= cost;
                return false;
            }

            RebuildResolvedMetaProgression(catalog, physicalLaneCount);
            return true;
        }

        public static float PreviewResolvedWorkDuration()
        {
            EnsureMetaProgressionInitialized(MetaProgressionCatalogAsset.LoadDefaultCatalog());
            return _metaProgressionRuntimeState.resolvedProgression.SessionDurationSeconds;
        }

        public static float ResolveWorkDuration(float baseWorkDurationSeconds, float healthDurationBonusSeconds)
        {
            if (_metaProgressionRuntimeState != null)
            {
                ResolvedWorkDurationSeconds = _metaProgressionRuntimeState.resolvedProgression.SessionDurationSeconds;
                return ResolvedWorkDurationSeconds;
            }

            ResolvedWorkDurationSeconds = CalculateWorkDuration(HealthLevel, baseWorkDurationSeconds, healthDurationBonusSeconds);
            return ResolvedWorkDurationSeconds;
        }

        public static void RequestBattleEntry()
        {
            ClosePauseMenu();
            ClearRhythmQueues();
            HasPendingBattleEntryRequest = true;
        }

        public static void ConsumeBattleEntryRequest()
        {
            HasPendingBattleEntryRequest = false;
        }

        public static LoadingDockRuntimeState GetLoadingDockRuntimeState(
            MetaProgressionCatalogAsset catalog = null,
            int physicalLaneCount = int.MaxValue)
        {
            EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            var approvalActive = _currentMiniGamePhase == BattleMiniGamePhase.Approval;
            return new LoadingDockRuntimeState
            {
                HasLoadingDockAccess = approvalActive,
                CurrentArea = approvalActive ? WorkAreaType.LoadingDock : WorkAreaType.Lane,
                TransitionPhase = approvalActive ? WorkAreaTransitionPhase.ActiveInLoadingDock : WorkAreaTransitionPhase.None,
                HasPendingEntryRequest = false,
                HasPendingReturnRequest = false
            };
        }

        public static LoadingDockQueueSnapshot GetLoadingDockQueueSnapshot()
        {
            return new LoadingDockQueueSnapshot
            {
                BacklogCount = _pendingRouteQueue.Count,
                ActiveSlotCount = _pendingApprovalQueue.Count,
                MaxActiveSlotCount = MaxLoadingDockActiveSlotCount,
                TotalCount = _pendingApprovalQueue.Count + _pendingRouteQueue.Count
            };
        }

        public static LoadingDockActiveCargoSlotSnapshot[] GetLoadingDockActiveCargoEntries()
        {
            var snapshots = new List<LoadingDockActiveCargoSlotSnapshot>();
            var slotIndex = 0;
            foreach (var approvalCargo in _pendingApprovalQueue)
            {
                snapshots.Add(new LoadingDockActiveCargoSlotSnapshot
                {
                    SlotIndex = slotIndex,
                    EntryId = approvalCargo.EntryId,
                    Kind = approvalCargo.Kind,
                    Weight = approvalCargo.Weight
                });
                slotIndex += 1;
                if (slotIndex >= MaxLoadingDockActiveSlotCount)
                {
                    break;
                }
            }

            return snapshots.ToArray();
        }

        public static LoadingDockCargoQueueEntry[] GetLoadingDockBacklogCargoEntries()
        {
            var entries = new List<LoadingDockCargoQueueEntry>();
            foreach (var routeCargo in _pendingRouteQueue)
            {
                entries.Add(new LoadingDockCargoQueueEntry
                {
                    EntryId = routeCargo.EntryId,
                    Kind = routeCargo.Kind,
                    Weight = routeCargo.Weight
                });
            }

            return entries.ToArray();
        }

        public static void EnqueueLoadingDockCargo(LoadingDockCargoKind kind)
        {
            EnqueueLoadingDockCargo(kind, DefaultDeliveryLaneMaxWeight);
        }

        public static void EnqueueLoadingDockCargo(LoadingDockCargoKind kind, int weight)
        {
            _pendingRouteQueue.Enqueue(new RouteSelectionCargoSnapshot
            {
                EntryId = _nextCargoEntryId++,
                Kind = kind,
                Weight = weight,
                Reward = 60,
                Penalty = 35,
                ApprovalDecision = ApprovalDecision.Approve,
                IsDeliverable = IsCargoDeliverable(weight)
            });
        }

        public static bool TryDeliverLoadingDockCargo(int entryId, out LoadingDockCargoQueueEntry deliveredEntry)
        {
            deliveredEntry = default;
            return false;
        }

        public static void ClearLoadingDockQueue()
        {
            ClearRhythmQueues();
        }

        public static bool TryRequestLoadingDockEntry(MetaProgressionCatalogAsset catalog, int physicalLaneCount = int.MaxValue)
        {
            return false;
        }

        public static void ConsumeLoadingDockEntryRequest()
        {
        }

        public static bool TryRequestLoadingDockReturn()
        {
            return false;
        }

        public static void ConsumeLoadingDockReturnRequest()
        {
        }

        public static void AdvanceLoadingDockTransition(float deltaTime, float transitionDuration = DefaultLoadingDockTransitionDurationSeconds)
        {
        }

        public static bool TryToggleLoadingDock(MetaProgressionCatalogAsset catalog, int physicalLaneCount = int.MaxValue)
        {
            return false;
        }

        public static bool IsLaneMovementLocked()
        {
            return _currentMiniGamePhase == BattleMiniGamePhase.Approval;
        }

        public static void OpenPauseMenu()
        {
            ApplyPauseState(true);
        }

        public static void ClosePauseMenu()
        {
            ApplyPauseState(false);
        }

        public static void TogglePauseMenu()
        {
            ApplyPauseState(!IsPauseMenuOpen);
        }

        private static int ResolvePlannedWeight(LoadingDockCargoKind kind, CargoConfig cargoConfig, System.Random random)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => random.Next(Mathf.Max(2, cargoConfig.FragileWeight - 2), cargoConfig.FragileWeight + 2),
                LoadingDockCargoKind.Frozen => random.Next(Mathf.Max(4, cargoConfig.HeavyWeight - 4), cargoConfig.HeavyWeight + 1),
                _ => random.Next(Mathf.Max(3, cargoConfig.StandardWeight - 2), cargoConfig.StandardWeight + 2)
            };
        }

        private static void ClearRhythmQueues()
        {
            _pendingApprovalQueue.Clear();
            _pendingRouteQueue.Clear();
            _nextCargoEntryId = 1;
        }

        private static float CalculateWorkDuration(int healthLevel, float baseWorkDurationSeconds, float healthDurationBonusSeconds)
        {
            var normalizedHealthLevel = Mathf.Max(MinimumHealthLevel, healthLevel);
            return baseWorkDurationSeconds + (normalizedHealthLevel - MinimumHealthLevel) * healthDurationBonusSeconds;
        }

        private static void RebuildResolvedMetaProgression(
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            catalog = catalog != null ? catalog : MetaProgressionCatalogAsset.LoadDefaultCatalog();
            _metaProgressionRuntimeState = MetaProgressionProtoContractMapper.FromContract(
                _metaProgressionRuntimeState?.snapshot,
                catalog,
                physicalLaneCount);
        }

        private static void EnsureCurrencyInitialized()
        {
            _metaProgressionRuntimeState ??= new MetaProgressionRuntimeState();
            _metaProgressionRuntimeState.snapshot ??= MetaProgressionCalculator.CreateDefaultSnapshot(MetaProgressionCatalogAsset.LoadDefaultCatalog());
            _metaProgressionRuntimeState.snapshot.currency ??= PlayerCurrencySnapshot.CreateDefault();
        }

        private static void ApplyPauseState(bool isPaused)
        {
            IsPauseMenuOpen = isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ResetPrototypeState();
        }
    }
}
