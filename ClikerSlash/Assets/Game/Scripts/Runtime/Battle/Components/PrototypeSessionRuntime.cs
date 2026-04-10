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

        // 참이면 허브가 이전 작업에서 캡처한 결과 스냅샷을 표시해야 합니다.
        public static bool HasLastBattleResult { get; private set; }
        public static BattleResultSnapshot LastBattleResult { get; private set; }
        public static bool HasLastLoadingDockResult { get; private set; }
        public static LoadingDockResultSnapshot LastLoadingDockResult { get; private set; }
        public static float ResolvedWorkDurationSeconds { get; private set; }
        // 참이면 허브에서 작업 현장으로 넘어가는 중이며, 전투 씬이 아직 요청을 소비하지 않은 상태입니다.
        public static bool HasPendingBattleEntryRequest { get; private set; }
        private static WorkAreaType _currentWorkArea = WorkAreaType.Lane;
        private static WorkAreaTransitionPhase _workAreaTransitionPhase = WorkAreaTransitionPhase.None;
        private static bool _hasPendingLoadingDockEntryRequest;
        private static bool _hasPendingLoadingDockReturnRequest;
        private static float _loadingDockTransitionElapsed;

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
        /// 상하차 라운드 결과를 현재 세션 요약으로 저장합니다.
        /// </summary>
        public static void StoreLoadingDockResult(LoadingDockResultSnapshot snapshot)
        {
            HasLastLoadingDockResult = true;
            LastLoadingDockResult = snapshot;
        }

        /// <summary>
        /// 이전 상하차 라운드 결과를 지웁니다.
        /// </summary>
        public static void ClearLastLoadingDockResult()
        {
            HasLastLoadingDockResult = false;
            LastLoadingDockResult = default;
        }

        /// <summary>
        /// 허브 메타와 마지막 결과를 포함한 프로토타입 런타임 상태를 초기값으로 되돌립니다.
        /// </summary>
        public static void ResetPrototypeState()
        {
            HasLastBattleResult = false;
            LastBattleResult = default;
            HasLastLoadingDockResult = false;
            LastLoadingDockResult = default;
            ResolvedWorkDurationSeconds = 0f;
            HasPendingBattleEntryRequest = false;
            _currentWorkArea = WorkAreaType.Lane;
            _workAreaTransitionPhase = WorkAreaTransitionPhase.None;
            _hasPendingLoadingDockEntryRequest = false;
            _hasPendingLoadingDockReturnRequest = false;
            _loadingDockTransitionElapsed = 0f;
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
            if (!_metaProgressionRuntimeState.resolvedProgression.HasLoadingDockAccess ||
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
            if (_currentWorkArea != WorkAreaType.LoadingDock ||
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
