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

        // 참이면 허브가 이전 작업에서 캡처한 결과 스냅샷을 표시해야 합니다.
        public static bool HasLastBattleResult { get; private set; }
        public static BattleResultSnapshot LastBattleResult { get; private set; }
        public static float ResolvedWorkDurationSeconds { get; private set; }
        // 참이면 허브에서 작업 현장으로 넘어가는 중이며, 전투 씬이 아직 요청을 소비하지 않은 상태입니다.
        public static bool HasPendingBattleEntryRequest { get; private set; }

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
        /// 다음 씬이 저장 데이터 없이도 읽을 수 있도록 마지막 작업 결과를 저장합니다.
        /// </summary>
        public static void StoreBattleResult(BattleResultSnapshot snapshot)
        {
            LastBattleResult = snapshot;
            HasLastBattleResult = true;
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
            HasLastBattleResult = false;
            LastBattleResult = default;
            ResolvedWorkDurationSeconds = 0f;
            HasPendingBattleEntryRequest = false;
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
        public static void IncreaseHealthLevel()
        {
            TryUpgradeNode(MetaProgressionCatalogAsset.StarterVitalityNodeId, MetaProgressionCatalogAsset.LoadDefaultCatalog());
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
            if (!MetaProgressionCalculator.TryUpgradeNode(_metaProgressionRuntimeState.snapshot, catalog, nodeId))
            {
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
