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
        public const string BattleEnvironmentSceneName = "PrototypeEvn";
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
        private static readonly Queue<LoadingDockCargoQueueEntry> _loadingDockBacklogQueue = new();
        private static readonly List<LoadingDockCargoQueueEntry> _loadingDockActiveEntries = new();
        private static int _nextCargoEntryId = 1;
        private static BattleMiniGameArea _focusedMiniGameArea = BattleMiniGameArea.Approval;
        private static bool _hasActiveApprovalCargo;
        private static bool _hasActiveRouteCargo;
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

        /// <summary>
        /// HUD와 프레젠터가 읽을 수 있는 현재 세션의 구역 요약 정보를 반환합니다.
        /// </summary>
        public static BattleMiniGamePhaseSnapshot GetBattleMiniGamePhaseSnapshot()
        {
            return new BattleMiniGamePhaseSnapshot
            {
                CurrentPhase = ConvertAreaToPhase(_focusedMiniGameArea),
                FocusedArea = _focusedMiniGameArea,
                HasActiveCargo = HasActiveCargoInArea(_focusedMiniGameArea),
                HasApprovalCargo = _hasActiveApprovalCargo,
                HasRouteCargo = _hasActiveRouteCargo,
                PendingApprovalCount = _pendingApprovalQueue.Count,
                PendingRouteCount = _pendingRouteQueue.Count,
                PendingLoadingDockCount = _loadingDockActiveEntries.Count + _loadingDockBacklogQueue.Count,
                DeliveryLaneMaxWeight = DefaultDeliveryLaneMaxWeight
            };
        }

        /// <summary>
        /// 현재 플레이어가 조작 중인 미니게임 구역을 반환합니다.
        /// </summary>
        public static BattleMiniGameArea GetFocusedMiniGameArea()
        {
            return _focusedMiniGameArea;
        }

        /// <summary>
        /// 이전 순서의 미니게임 구역으로 카메라 포커스를 이동합니다.
        /// </summary>
        public static void FocusPreviousMiniGameArea()
        {
            _focusedMiniGameArea = _focusedMiniGameArea switch
            {
                BattleMiniGameArea.Approval => BattleMiniGameArea.LoadingDock,
                BattleMiniGameArea.RouteSelection => BattleMiniGameArea.Approval,
                _ => BattleMiniGameArea.RouteSelection
            };
        }

        /// <summary>
        /// 다음 순서의 미니게임 구역으로 카메라 포커스를 이동합니다.
        /// </summary>
        public static void FocusNextMiniGameArea()
        {
            _focusedMiniGameArea = _focusedMiniGameArea switch
            {
                BattleMiniGameArea.Approval => BattleMiniGameArea.RouteSelection,
                BattleMiniGameArea.RouteSelection => BattleMiniGameArea.LoadingDock,
                _ => BattleMiniGameArea.Approval
            };
        }

        /// <summary>
        /// 특정 구역이 현재 플레이어 포커스를 받고 있는지 반환합니다.
        /// </summary>
        public static bool IsMiniGameAreaFocused(BattleMiniGameArea area)
        {
            return _focusedMiniGameArea == area;
        }

        /// <summary>
        /// 비포커스 상태에서도 자동 판정을 허용할지 여부를 반환합니다.
        /// </summary>
        public static bool CanAutoResolveWhenUnfocused(BattleMiniGameArea area)
        {
            return false;
        }

        /// <summary>
        /// 해당 구역의 물류가 판정선에 도달했을 때 대기해야 하는지 여부를 반환합니다.
        /// </summary>
        public static bool ShouldHoldAtJudgment(BattleMiniGameArea area)
        {
            return area is BattleMiniGameArea.Approval or BattleMiniGameArea.RouteSelection;
        }

        /// <summary>
        /// 특정 판정 구역에 현재 활성 물류가 존재하는지 반환합니다.
        /// </summary>
        public static bool HasActiveCargoInArea(BattleMiniGameArea area)
        {
            return area switch
            {
                BattleMiniGameArea.Approval => _hasActiveApprovalCargo,
                BattleMiniGameArea.RouteSelection => _hasActiveRouteCargo,
                _ => _loadingDockActiveEntries.Count > 0
            };
        }

        /// <summary>
        /// 아직 승인 구역에 도달하지 않은 대기 물류 목록을 배열로 반환합니다.
        /// </summary>
        public static ApprovalCargoSnapshot[] GetPendingApprovalCargoEntries()
        {
            return _pendingApprovalQueue.ToArray();
        }

        /// <summary>
        /// 승인 결과가 반영되어 레인선택을 기다리는 물류 목록을 배열로 반환합니다.
        /// </summary>
        public static RouteSelectionCargoSnapshot[] GetPendingRouteCargoEntries()
        {
            return _pendingRouteQueue.ToArray();
        }

        /// <summary>
        /// 지정한 출력 레인이 허용하는 최대 무게를 반환합니다.
        /// </summary>
        public static int GetDeliveryLaneMaxWeight(CargoRouteLane routeLane)
        {
            return routeLane == CargoRouteLane.Return ? int.MaxValue : DefaultDeliveryLaneMaxWeight;
        }

        /// <summary>
        /// 기본 배송 레인 기준으로 현재 무게가 출고 가능한지 빠르게 판정합니다.
        /// </summary>
        public static bool IsCargoDeliverable(int weight)
        {
            return weight <= DefaultDeliveryLaneMaxWeight;
        }

        /// <summary>
        /// 현재 메타 진행 상태에서 상하차 로봇이 해금되었는지 반환합니다.
        /// </summary>
        public static bool HasInstalledDockRobot()
        {
            return GetResolvedMetaProgression().HasDockRobotAccess;
        }

        public static void InitializeRhythmCargoPlan(float resolvedWorkDurationSeconds, float spawnIntervalSeconds, CargoConfig cargoConfig, uint seed)
        {
            ClearRhythmQueues();
            _focusedMiniGameArea = BattleMiniGameArea.Approval;
            _pendingPhaseInput = PendingPhaseInput.None;
            _hasActiveApprovalCargo = false;
            _hasActiveRouteCargo = false;

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
            _focusedMiniGameArea = BattleMiniGameArea.Approval;
            _pendingPhaseInput = PendingPhaseInput.None;
            _hasActiveApprovalCargo = false;
            _hasActiveRouteCargo = false;

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

        /// <summary>
        /// 지정한 구역이 새 활성 물류를 시작할 수 있을 때 다음 엔트리를 꺼냅니다.
        /// </summary>
        public static bool TryDequeueCargoForArea(
            BattleMiniGameArea area,
            out BattleMiniGamePhase phase,
            out ApprovalCargoSnapshot approvalCargo,
            out RouteSelectionCargoSnapshot routeCargo)
        {
            phase = ConvertAreaToPhase(area);
            approvalCargo = default;
            routeCargo = default;

            if (area == BattleMiniGameArea.Approval)
            {
                if (_hasActiveApprovalCargo || _pendingApprovalQueue.Count == 0)
                {
                    return false;
                }

                approvalCargo = _pendingApprovalQueue.Dequeue();
                _hasActiveApprovalCargo = true;
                return true;
            }

            if (area == BattleMiniGameArea.RouteSelection)
            {
                if (_hasActiveRouteCargo || _pendingRouteQueue.Count == 0)
                {
                    return false;
                }

                routeCargo = _pendingRouteQueue.Dequeue();
                _hasActiveRouteCargo = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 지정한 구역의 활성 물류가 판정을 마쳤음을 런타임 상태에 반영합니다.
        /// </summary>
        public static void NotifyAreaCargoResolved(BattleMiniGameArea area)
        {
            switch (area)
            {
                case BattleMiniGameArea.Approval:
                    _hasActiveApprovalCargo = false;
                    break;
                case BattleMiniGameArea.RouteSelection:
                    _hasActiveRouteCargo = false;
                    break;
            }
        }

        /// <summary>
        /// 레거시 호출부와의 호환을 위해 현재 포커스 구역 기준으로 활성 물류 해제를 처리합니다.
        /// </summary>
        public static void NotifyActiveCargoResolved()
        {
            NotifyAreaCargoResolved(_focusedMiniGameArea);
        }

        public static bool IsMiniGameLoopFinished()
        {
            return _pendingApprovalQueue.Count == 0 &&
                   _pendingRouteQueue.Count == 0 &&
                   _loadingDockBacklogQueue.Count == 0 &&
                   _loadingDockActiveEntries.Count == 0 &&
                   !_hasActiveApprovalCargo &&
                   !_hasActiveRouteCargo;
        }

        /// <summary>
        /// 승인 구역에서 입력된 불/가 판정 하나를 런타임 버퍼에 저장합니다.
        /// </summary>
        public static void QueueApprovalInput(ApprovalDecision decision)
        {
            _pendingPhaseInput = decision switch
            {
                ApprovalDecision.Approve => PendingPhaseInput.ApprovalApprove,
                ApprovalDecision.Reject => PendingPhaseInput.ApprovalReject,
                _ => PendingPhaseInput.None
            };
        }

        /// <summary>
        /// 레인선택 구역에서 입력된 출력 레인 선택 하나를 런타임 버퍼에 저장합니다.
        /// </summary>
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

        /// <summary>
        /// 승인 구역이 사용할 수 있는 보류 입력이 있으면 한 번만 꺼내 반환합니다.
        /// </summary>
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

        /// <summary>
        /// 레인선택 구역이 사용할 수 있는 보류 입력이 있으면 한 번만 꺼내 반환합니다.
        /// </summary>
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

        /// <summary>
        /// 승인 판정을 마친 물류를 레인선택 대기열 뒤에 추가합니다.
        /// </summary>
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

        /// <summary>
        /// 레인선택 결과가 수익, 반송, 오배차 중 무엇으로 집계될지 계산합니다.
        /// </summary>
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

        /// <summary>
        /// 직전 전투 결과 스냅샷을 비워 허브 표시 상태를 초기화합니다.
        /// </summary>
        public static void ClearLastBattleResult()
        {
            HasLastBattleResult = false;
            LastBattleResult = default;
        }

        /// <summary>
        /// 전투 세션과 관련된 모든 정적 런타임 상태를 기본값으로 되돌립니다.
        /// </summary>
        public static void ResetPrototypeState()
        {
            ClosePauseMenu();
            HasLastBattleResult = false;
            LastBattleResult = default;
            ClearRhythmQueues();
            ResolvedWorkDurationSeconds = 0f;
            HasPendingBattleEntryRequest = false;
            _focusedMiniGameArea = BattleMiniGameArea.Approval;
            _pendingPhaseInput = PendingPhaseInput.None;
            _hasActiveApprovalCargo = false;
            _hasActiveRouteCargo = false;
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

        /// <summary>
        /// 포커스 상태를 기준으로 레거시 상하차 프레젠터가 읽을 런타임 스냅샷을 만듭니다.
        /// </summary>
        public static LoadingDockRuntimeState GetLoadingDockRuntimeState(
            MetaProgressionCatalogAsset catalog = null,
            int physicalLaneCount = int.MaxValue)
        {
            EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            var loadingDockFocused = _focusedMiniGameArea == BattleMiniGameArea.LoadingDock;
            return new LoadingDockRuntimeState
            {
                HasLoadingDockAccess = true,
                CurrentArea = loadingDockFocused ? WorkAreaType.LoadingDock : WorkAreaType.Lane,
                TransitionPhase = loadingDockFocused ? WorkAreaTransitionPhase.ActiveInLoadingDock : WorkAreaTransitionPhase.None,
                HasPendingEntryRequest = false,
                HasPendingReturnRequest = false
            };
        }

        /// <summary>
        /// 상하차 구역의 활성 슬롯과 backlog 개수를 한 번에 반환합니다.
        /// </summary>
        public static LoadingDockQueueSnapshot GetLoadingDockQueueSnapshot()
        {
            return new LoadingDockQueueSnapshot
            {
                BacklogCount = _loadingDockBacklogQueue.Count,
                ActiveSlotCount = _loadingDockActiveEntries.Count,
                MaxActiveSlotCount = MaxLoadingDockActiveSlotCount,
                TotalCount = _loadingDockActiveEntries.Count + _loadingDockBacklogQueue.Count
            };
        }

        /// <summary>
        /// 상하차 슬롯에 현재 노출 중인 물류 목록을 슬롯 번호와 함께 반환합니다.
        /// </summary>
        public static LoadingDockActiveCargoSlotSnapshot[] GetLoadingDockActiveCargoEntries()
        {
            var snapshots = new List<LoadingDockActiveCargoSlotSnapshot>();
            for (var slotIndex = 0; slotIndex < _loadingDockActiveEntries.Count; slotIndex += 1)
            {
                var approvalCargo = _loadingDockActiveEntries[slotIndex];
                snapshots.Add(new LoadingDockActiveCargoSlotSnapshot
                {
                    SlotIndex = slotIndex,
                    EntryId = approvalCargo.EntryId,
                    Kind = approvalCargo.Kind,
                    Weight = approvalCargo.Weight
                });
            }

            return snapshots.ToArray();
        }

        /// <summary>
        /// 활성 슬롯 뒤에서 대기 중인 상하차 backlog 물류 목록을 반환합니다.
        /// </summary>
        public static LoadingDockCargoQueueEntry[] GetLoadingDockBacklogCargoEntries()
        {
            var entries = new List<LoadingDockCargoQueueEntry>();
            foreach (var routeCargo in _loadingDockBacklogQueue)
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

        /// <summary>
        /// 새 상하차 물류를 기본 무게값으로 큐에 추가합니다.
        /// </summary>
        public static void EnqueueLoadingDockCargo(LoadingDockCargoKind kind)
        {
            EnqueueLoadingDockCargo(kind, DefaultDeliveryLaneMaxWeight);
        }

        public static void EnqueueLoadingDockCargo(LoadingDockCargoKind kind, int weight)
        {
            EnqueueLoadingDockCargo(_nextCargoEntryId++, kind, weight);
        }

        /// <summary>
        /// 레인선택을 마친 물류를 상하차 구역 큐로 전달하고 빈 슬롯을 즉시 채웁니다.
        /// </summary>
        public static void EnqueueLoadingDockCargo(int entryId, LoadingDockCargoKind kind, int weight)
        {
            _loadingDockBacklogQueue.Enqueue(new LoadingDockCargoQueueEntry
            {
                EntryId = entryId > 0 ? entryId : _nextCargoEntryId++,
                Kind = kind,
                Weight = weight
            });

            PromoteLoadingDockBacklog();
        }

        /// <summary>
        /// 클릭 또는 로봇 처리로 지정한 상하차 엔트리를 슬롯에서 제거합니다.
        /// </summary>
        public static bool TryDeliverLoadingDockCargo(int entryId, out LoadingDockCargoQueueEntry deliveredEntry)
        {
            deliveredEntry = default;
            for (var index = 0; index < _loadingDockActiveEntries.Count; index += 1)
            {
                if (_loadingDockActiveEntries[index].EntryId != entryId)
                {
                    continue;
                }

                deliveredEntry = _loadingDockActiveEntries[index];
                _loadingDockActiveEntries.RemoveAt(index);
                PromoteLoadingDockBacklog();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 상하차 구역의 활성 슬롯과 backlog를 모두 비웁니다.
        /// </summary>
        public static void ClearLoadingDockQueue()
        {
            _loadingDockActiveEntries.Clear();
            _loadingDockBacklogQueue.Clear();
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

        /// <summary>
        /// 현재 프로토타입에서는 레인 이동형 조작을 사용하지 않으므로 항상 잠금 상태를 반환합니다.
        /// </summary>
        public static bool IsLaneMovementLocked()
        {
            return true;
        }

        /// <summary>
        /// 일시정지 메뉴를 열고 게임 시간을 멈춥니다.
        /// </summary>
        public static void OpenPauseMenu()
        {
            ApplyPauseState(true);
        }

        /// <summary>
        /// 일시정지 메뉴를 닫고 게임 시간을 다시 흐르게 합니다.
        /// </summary>
        public static void ClosePauseMenu()
        {
            ApplyPauseState(false);
        }

        /// <summary>
        /// 현재 일시정지 상태를 토글합니다.
        /// </summary>
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
            _loadingDockBacklogQueue.Clear();
            _loadingDockActiveEntries.Clear();
            _nextCargoEntryId = 1;
        }

        /// <summary>
        /// 상하차 슬롯이 비어 있으면 backlog에서 앞 순서대로 채워 넣습니다.
        /// </summary>
        private static void PromoteLoadingDockBacklog()
        {
            while (_loadingDockActiveEntries.Count < MaxLoadingDockActiveSlotCount &&
                   _loadingDockBacklogQueue.Count > 0)
            {
                _loadingDockActiveEntries.Add(_loadingDockBacklogQueue.Dequeue());
            }
        }

        /// <summary>
        /// 포커스 구역 값을 레거시 phase 표현으로 변환합니다.
        /// </summary>
        private static BattleMiniGamePhase ConvertAreaToPhase(BattleMiniGameArea area)
        {
            return area switch
            {
                BattleMiniGameArea.Approval => BattleMiniGamePhase.Approval,
                BattleMiniGameArea.RouteSelection => BattleMiniGamePhase.RouteSelection,
                BattleMiniGameArea.LoadingDock => BattleMiniGamePhase.LoadingDock,
                _ => BattleMiniGamePhase.Completed
            };
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
