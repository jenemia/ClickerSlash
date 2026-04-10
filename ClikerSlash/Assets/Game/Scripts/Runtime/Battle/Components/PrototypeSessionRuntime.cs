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

        // 참이면 허브가 이전 작업에서 캡처한 결과 스냅샷을 표시해야 합니다.
        public static bool HasLastBattleResult { get; private set; }
        public static BattleResultSnapshot LastBattleResult { get; private set; }
        public static float ResolvedWorkDurationSeconds { get; private set; }
        public static bool IsPauseMenuOpen { get; private set; }
        // 참이면 허브에서 작업 현장으로 넘어가는 중이며, 전투 씬이 아직 요청을 소비하지 않은 상태입니다.
        public static bool HasPendingBattleEntryRequest { get; private set; }
        private static WorkAreaType _currentWorkArea = WorkAreaType.Lane;
        private static WorkAreaTransitionPhase _workAreaTransitionPhase = WorkAreaTransitionPhase.None;
        private static bool _hasPendingLoadingDockEntryRequest;
        private static bool _hasPendingLoadingDockReturnRequest;
        private static float _loadingDockTransitionElapsed;
        private static bool _hasInstalledDockRobot;
        private static readonly Queue<LoadingDockCargoQueueEntry> _loadingDockBacklogQueue = new();
        private static readonly LoadingDockCargoQueueEntry?[] _loadingDockActiveSlots = new LoadingDockCargoQueueEntry?[MaxLoadingDockActiveSlotCount];
        private static int _nextLoadingDockCargoEntryId = 1;

        private static MetaProgressionRuntimeState _metaProgressionRuntimeState;

        /// <summary>
        /// 기존 허브 UI 호환을 위해 시작 체력 노드 레벨을 1부터 환산해 노출합니다.
        /// </summary>
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

        /// <summary>
        /// 현재 메타 진행 런타임 상태를 외부 브리지와 테스트에서 읽을 수 있게 노출합니다.
        /// </summary>
        public static MetaProgressionRuntimeState GetMetaProgressionRuntimeState()
        {
            return _metaProgressionRuntimeState;
        }

        /// <summary>
        /// 현재 해금 상태를 직렬화 계약 형태로 깊은 복제해 반환합니다.
        /// </summary>
        public static PlayerMetaProgressionSnapshot GetMetaProgressionSnapshot()
        {
            return MetaProgressionProtoContractMapper.ToContract(_metaProgressionRuntimeState);
        }

        /// <summary>
        /// 현재 세션 플레이어 재화 상태를 읽기 전용 복사본으로 반환합니다.
        /// </summary>
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

        /// <summary>
        /// 현재 세션 시작에 쓰일 메타 집계 결과를 반환합니다.
        /// </summary>
        public static ResolvedMetaProgression GetResolvedMetaProgression()
        {
            if (_metaProgressionRuntimeState == null)
            {
                EnsureMetaProgressionInitialized(MetaProgressionCatalogAsset.LoadDefaultCatalog());
            }

            return _metaProgressionRuntimeState.resolvedProgression;
        }

        /// <summary>
        /// 현재 세션에서 Dock 로봇이 실제로 설치된 상태인지 반환합니다.
        /// </summary>
        public static bool HasInstalledDockRobot()
        {
            return _hasInstalledDockRobot;
        }

        /// <summary>
        /// Dock 로봇 해금 여부를 확인한 뒤 세션 설치 상태를 변경합니다.
        /// </summary>
        public static bool SetDockRobotInstalled(bool isInstalled, MetaProgressionCatalogAsset catalog = null, int physicalLaneCount = int.MaxValue)
        {
            if (!isInstalled)
            {
                _hasInstalledDockRobot = false;
                return true;
            }

            EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            if (!_metaProgressionRuntimeState.resolvedProgression.HasDockRobotAccess)
            {
                return false;
            }

            _hasInstalledDockRobot = true;
            return true;
        }

        /// <summary>
        /// 현재 상하차 진입/복귀 계약 상태를 읽기 전용 스냅샷으로 반환합니다.
        /// </summary>
        public static LoadingDockRuntimeState GetLoadingDockRuntimeState(
            MetaProgressionCatalogAsset catalog = null,
            int physicalLaneCount = int.MaxValue)
        {
            EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            return new LoadingDockRuntimeState
            {
                HasLoadingDockAccess = _metaProgressionRuntimeState.resolvedProgression.HasLoadingDockAccess,
                CurrentArea = _currentWorkArea,
                TransitionPhase = _workAreaTransitionPhase,
                HasPendingEntryRequest = _hasPendingLoadingDockEntryRequest,
                HasPendingReturnRequest = _hasPendingLoadingDockReturnRequest
            };
        }

        /// <summary>
        /// 현재 상하차 큐의 backlog/활성 슬롯 요약을 반환합니다.
        /// </summary>
        public static LoadingDockQueueSnapshot GetLoadingDockQueueSnapshot()
        {
            var activeSlotCount = 0;
            for (var slotIndex = 0; slotIndex < _loadingDockActiveSlots.Length; slotIndex += 1)
            {
                if (_loadingDockActiveSlots[slotIndex].HasValue)
                {
                    activeSlotCount += 1;
                }
            }

            return new LoadingDockQueueSnapshot
            {
                BacklogCount = _loadingDockBacklogQueue.Count,
                ActiveSlotCount = activeSlotCount,
                MaxActiveSlotCount = MaxLoadingDockActiveSlotCount,
                TotalCount = activeSlotCount + _loadingDockBacklogQueue.Count
            };
        }

        /// <summary>
        /// 현재 활성 슬롯에 배치된 상하차 물류 엔트리 복사본을 반환합니다.
        /// </summary>
        public static LoadingDockActiveCargoSlotSnapshot[] GetLoadingDockActiveCargoEntries()
        {
            var activeEntries = new List<LoadingDockActiveCargoSlotSnapshot>(MaxLoadingDockActiveSlotCount);
            for (var slotIndex = 0; slotIndex < _loadingDockActiveSlots.Length; slotIndex += 1)
            {
                if (!_loadingDockActiveSlots[slotIndex].HasValue)
                {
                    continue;
                }

                var entry = _loadingDockActiveSlots[slotIndex].Value;
                activeEntries.Add(new LoadingDockActiveCargoSlotSnapshot
                {
                    SlotIndex = slotIndex,
                    EntryId = entry.EntryId,
                    Kind = entry.Kind,
                    Weight = entry.Weight
                });
            }

            return activeEntries.ToArray();
        }

        /// <summary>
        /// 현재 backlog 대기열에 쌓인 상하차 물류 엔트리 복사본을 반환합니다.
        /// </summary>
        public static LoadingDockCargoQueueEntry[] GetLoadingDockBacklogCargoEntries()
        {
            return _loadingDockBacklogQueue.ToArray();
        }

        /// <summary>
        /// 레인에서 성공 처리된 물류를 상하차 세션 큐에 적재합니다.
        /// </summary>
        public static void EnqueueLoadingDockCargo(LoadingDockCargoKind kind)
        {
            var defaultWeight = kind switch
            {
                LoadingDockCargoKind.Heavy => 12,
                _ => 6
            };
            EnqueueLoadingDockCargo(kind, defaultWeight);
        }

        /// <summary>
        /// 레인에서 성공 처리된 물류를 무게 정보와 함께 상하차 세션 큐에 적재합니다.
        /// </summary>
        public static void EnqueueLoadingDockCargo(LoadingDockCargoKind kind, int weight)
        {
            var queueEntry = new LoadingDockCargoQueueEntry
            {
                EntryId = _nextLoadingDockCargoEntryId,
                Kind = kind,
                Weight = weight
            };
            _nextLoadingDockCargoEntryId += 1;

            var emptySlotIndex = FindFirstEmptyLoadingDockSlotIndex();
            if (emptySlotIndex >= 0)
            {
                _loadingDockActiveSlots[emptySlotIndex] = queueEntry;
                return;
            }

            _loadingDockBacklogQueue.Enqueue(queueEntry);
        }

        /// <summary>
        /// 활성 슬롯에 표시된 상하차 물류를 delivered 처리하고 backlog를 즉시 보충합니다.
        /// </summary>
        public static bool TryDeliverLoadingDockCargo(int entryId, out LoadingDockCargoQueueEntry deliveredEntry)
        {
            deliveredEntry = default;
            for (var slotIndex = 0; slotIndex < _loadingDockActiveSlots.Length; slotIndex += 1)
            {
                if (!_loadingDockActiveSlots[slotIndex].HasValue)
                {
                    continue;
                }

                var activeEntry = _loadingDockActiveSlots[slotIndex].Value;
                if (activeEntry.EntryId != entryId)
                {
                    continue;
                }

                deliveredEntry = activeEntry;
                _loadingDockActiveSlots[slotIndex] = null;
                if (_loadingDockBacklogQueue.Count > 0)
                {
                    _loadingDockActiveSlots[slotIndex] = _loadingDockBacklogQueue.Dequeue();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 상하차 세션 큐와 슬롯 상태를 모두 비웁니다.
        /// </summary>
        public static void ClearLoadingDockQueue()
        {
            _loadingDockBacklogQueue.Clear();
            for (var slotIndex = 0; slotIndex < _loadingDockActiveSlots.Length; slotIndex += 1)
            {
                _loadingDockActiveSlots[slotIndex] = null;
            }

            _nextLoadingDockCargoEntryId = 1;
        }

        private static int FindFirstEmptyLoadingDockSlotIndex()
        {
            for (var slotIndex = 0; slotIndex < _loadingDockActiveSlots.Length; slotIndex += 1)
            {
                if (!_loadingDockActiveSlots[slotIndex].HasValue)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// 다음 씬이 저장 데이터 없이도 읽을 수 있도록 마지막 작업 결과를 저장합니다.
        /// </summary>
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
        /// 이전에 저장된 결과 스냅샷을 비웁니다.
        /// </summary>
        public static void ClearLastBattleResult()
        {
            HasLastBattleResult = false;
            LastBattleResult = default;
        }

        /// <summary>
        /// 허브 메타와 마지막 결과를 포함한 프로토타입 런타임 상태를 초기값으로 되돌립니다.
        /// </summary>
        public static void ResetPrototypeState()
        {
            ClosePauseMenu();
            HasLastBattleResult = false;
            LastBattleResult = default;
            ClearLoadingDockQueue();
            ResolvedWorkDurationSeconds = 0f;
            HasPendingBattleEntryRequest = false;
            _currentWorkArea = WorkAreaType.Lane;
            _workAreaTransitionPhase = WorkAreaTransitionPhase.None;
            _hasPendingLoadingDockEntryRequest = false;
            _hasPendingLoadingDockReturnRequest = false;
            _loadingDockTransitionElapsed = 0f;
            _hasInstalledDockRobot = false;
            _metaProgressionRuntimeState = null;
        }

        /// <summary>
        /// 카탈로그 기본값을 기반으로 메타 진행 상태가 최소 1회 초기화되도록 합니다.
        /// </summary>
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

        /// <summary>
        /// 외부에서 구성한 메타 진행 스냅샷으로 현재 런타임 상태를 교체합니다.
        /// </summary>
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

        /// <summary>
        /// 허브 임시 메타 상태에서 시작 체력 노드를 한 단계 올립니다.
        /// </summary>
        public static bool IncreaseHealthLevel()
        {
            return TryUpgradeNode(MetaProgressionCatalogAsset.StarterVitalityNodeId, MetaProgressionCatalogAsset.LoadDefaultCatalog());
        }

        /// <summary>
        /// 특정 메타 노드를 한 단계 올리고 집계 결과를 다시 계산합니다.
        /// </summary>
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

        /// <summary>
        /// 현재 메타 집계 기준으로 다음 세션 예상 작업시간을 계산합니다.
        /// </summary>
        public static float PreviewResolvedWorkDuration()
        {
            EnsureMetaProgressionInitialized(MetaProgressionCatalogAsset.LoadDefaultCatalog());
            return _metaProgressionRuntimeState.resolvedProgression.SessionDurationSeconds;
        }

        /// <summary>
        /// 실제 세션 설정값 또는 메타 집계를 기준으로 이번 진입의 작업시간을 계산하고 캐시합니다.
        /// </summary>
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

        /// <summary>
        /// 사용자가 작업 씬 밖에서 새 작업 진입을 요청했음을 표시합니다.
        /// </summary>
        public static void RequestBattleEntry()
        {
            ClosePauseMenu();
            ClearLoadingDockQueue();
            HasPendingBattleEntryRequest = true;
        }

        /// <summary>
        /// 작업 씬이 진입 요청을 인지한 뒤 대기 중인 진입 플래그를 지웁니다.
        /// </summary>
        public static void ConsumeBattleEntryRequest()
        {
            HasPendingBattleEntryRequest = false;
        }

        /// <summary>
        /// 상하차 오픈이 해금된 상태에서만 상하차 구역 진입 연출을 요청합니다.
        /// </summary>
        public static bool TryRequestLoadingDockEntry(
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            if (IsPauseMenuOpen ||
                !_metaProgressionRuntimeState.resolvedProgression.HasLoadingDockAccess ||
                _currentWorkArea != WorkAreaType.Lane ||
                _workAreaTransitionPhase != WorkAreaTransitionPhase.None)
            {
                return false;
            }

            _hasPendingLoadingDockEntryRequest = true;
            _hasPendingLoadingDockReturnRequest = false;
            _workAreaTransitionPhase = WorkAreaTransitionPhase.EnteringLoadingDock;
            _loadingDockTransitionElapsed = 0f;
            return true;
        }

        /// <summary>
        /// 상하차 진입 연출이 끝났을 때 호출해 현재 작업 구역을 상하차로 확정합니다.
        /// </summary>
        public static void ConsumeLoadingDockEntryRequest()
        {
            if (!_hasPendingLoadingDockEntryRequest)
            {
                return;
            }

            _hasPendingLoadingDockEntryRequest = false;
            _currentWorkArea = WorkAreaType.LoadingDock;
            _workAreaTransitionPhase = WorkAreaTransitionPhase.ActiveInLoadingDock;
            _loadingDockTransitionElapsed = 0f;
        }

        /// <summary>
        /// 상하차 작업이 끝나면 레인으로 복귀하는 전환을 요청합니다.
        /// </summary>
        public static bool TryRequestLoadingDockReturn()
        {
            if (IsPauseMenuOpen ||
                _currentWorkArea != WorkAreaType.LoadingDock ||
                _workAreaTransitionPhase != WorkAreaTransitionPhase.ActiveInLoadingDock)
            {
                return false;
            }

            _hasPendingLoadingDockReturnRequest = true;
            _workAreaTransitionPhase = WorkAreaTransitionPhase.ReturningToLane;
            _loadingDockTransitionElapsed = 0f;
            return true;
        }

        /// <summary>
        /// 레인 복귀 연출이 끝났을 때 호출해 기본 작업 구역으로 상태를 정리합니다.
        /// </summary>
        public static void ConsumeLoadingDockReturnRequest()
        {
            if (!_hasPendingLoadingDockReturnRequest)
            {
                return;
            }

            _hasPendingLoadingDockReturnRequest = false;
            _currentWorkArea = WorkAreaType.Lane;
            _workAreaTransitionPhase = WorkAreaTransitionPhase.None;
            _loadingDockTransitionElapsed = 0f;
        }

        /// <summary>
        /// 프레젠테이션 브리지 호출이 빠져도 상하차 전환 상태가 영구 대기하지 않도록 시간을 기준으로 확정합니다.
        /// </summary>
        public static void AdvanceLoadingDockTransition(float deltaTime, float transitionDuration = DefaultLoadingDockTransitionDurationSeconds)
        {
            if (IsPauseMenuOpen)
            {
                return;
            }

            if (_workAreaTransitionPhase != WorkAreaTransitionPhase.EnteringLoadingDock &&
                _workAreaTransitionPhase != WorkAreaTransitionPhase.ReturningToLane)
            {
                return;
            }

            _loadingDockTransitionElapsed += Mathf.Max(0f, deltaTime);
            if (_loadingDockTransitionElapsed < Mathf.Max(0.01f, transitionDuration))
            {
                return;
            }

            if (_workAreaTransitionPhase == WorkAreaTransitionPhase.EnteringLoadingDock)
            {
                ConsumeLoadingDockEntryRequest();
                return;
            }

            ConsumeLoadingDockReturnRequest();
        }

        /// <summary>
        /// 현재 구역 기준으로 레인과 상하차 구역 사이 전환 요청을 토글합니다.
        /// </summary>
        public static bool TryToggleLoadingDock(
            MetaProgressionCatalogAsset catalog,
            int physicalLaneCount = int.MaxValue)
        {
            EnsureMetaProgressionInitialized(catalog, physicalLaneCount);
            if (IsPauseMenuOpen)
            {
                return false;
            }

            if (_currentWorkArea == WorkAreaType.Lane)
            {
                return _workAreaTransitionPhase == WorkAreaTransitionPhase.None &&
                       TryRequestLoadingDockEntry(catalog, physicalLaneCount);
            }

            return _workAreaTransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock &&
                   TryRequestLoadingDockReturn();
        }

        /// <summary>
        /// 상하차 구역 진입/체류/복귀 중에는 레인 이동 입력과 보간 이동을 모두 잠급니다.
        /// </summary>
        public static bool IsLaneMovementLocked()
        {
            return _workAreaTransitionPhase == WorkAreaTransitionPhase.EnteringLoadingDock ||
                   _workAreaTransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock ||
                   _workAreaTransitionPhase == WorkAreaTransitionPhase.ReturningToLane;
        }

        /// <summary>
        /// 전역 일시정지 팝업을 열고 시간을 멈춥니다.
        /// </summary>
        public static void OpenPauseMenu()
        {
            ApplyPauseState(true);
        }

        /// <summary>
        /// 전역 일시정지 팝업을 닫고 시간을 다시 흐르게 합니다.
        /// </summary>
        public static void ClosePauseMenu()
        {
            ApplyPauseState(false);
        }

        /// <summary>
        /// 전역 일시정지 팝업의 열림 상태를 반전합니다.
        /// </summary>
        public static void TogglePauseMenu()
        {
            ApplyPauseState(!IsPauseMenuOpen);
        }

        private static float CalculateWorkDuration(int healthLevel, float baseWorkDurationSeconds, float healthDurationBonusSeconds)
        {
            var normalizedHealthLevel = Mathf.Max(MinimumHealthLevel, healthLevel);
            return baseWorkDurationSeconds + (normalizedHealthLevel - MinimumHealthLevel) * healthDurationBonusSeconds;
        }

        /// <summary>
        /// 현재 스냅샷을 기준으로 메타 집계 결과를 다시 계산합니다.
        /// </summary>
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

        /// <summary>
        /// 엔진이 플레이어 도메인을 다시 로드할 때 정적 세션 상태를 초기화합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ResetPrototypeState();
        }
    }
}
