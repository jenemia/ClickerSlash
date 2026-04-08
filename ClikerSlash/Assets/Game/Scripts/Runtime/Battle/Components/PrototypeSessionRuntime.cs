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
        public static int HealthLevel { get; private set; } = MinimumHealthLevel;
        public static float ResolvedWorkDurationSeconds { get; private set; }
        // 참이면 허브에서 작업 현장으로 넘어가는 중이며, 전투 씬이 아직 요청을 소비하지 않은 상태입니다.
        public static bool HasPendingBattleEntryRequest { get; private set; }

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
            HealthLevel = MinimumHealthLevel;
            ResolvedWorkDurationSeconds = 0f;
            HasPendingBattleEntryRequest = false;
        }

        /// <summary>
        /// 허브 임시 메타 상태에서 체력 레벨을 1 올립니다.
        /// </summary>
        public static void IncreaseHealthLevel()
        {
            HealthLevel += 1;
        }

        /// <summary>
        /// 현재 체력 레벨을 기준으로 다음 세션 예상 작업시간을 계산합니다.
        /// </summary>
        public static float PreviewResolvedWorkDuration()
        {
            return CalculateWorkDuration(HealthLevel, DefaultBaseWorkDurationSeconds, DefaultHealthDurationBonusSeconds);
        }

        /// <summary>
        /// 실제 세션 설정값을 기준으로 이번 진입의 작업시간을 계산하고 캐시합니다.
        /// </summary>
        public static float ResolveWorkDuration(float baseWorkDurationSeconds, float healthDurationBonusSeconds)
        {
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
        /// 엔진이 플레이어 도메인을 다시 로드할 때 정적 세션 상태를 초기화합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ResetPrototypeState();
        }
    }
}
