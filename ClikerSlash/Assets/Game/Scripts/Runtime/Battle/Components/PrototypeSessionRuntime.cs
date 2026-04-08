using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 프로토타입 전투 결과 화면으로 넘기는 직렬화 가능한 런타임 스냅샷입니다.
    /// </summary>
    public struct BattleResultSnapshot
    {
        // 0은 이번 전투가 패배로 끝났음을, 1은 생존 성공으로 끝났음을 뜻합니다.
        public byte IsVictory;
        public int KillCount;
        public int CurrentCombo;
        public int MaxCombo;
        public float SurvivalTimeSeconds;
        public int RemainingLives;
    }

    /// <summary>
    /// 저장하지 않는 프로토타입 세션 데이터를 씬 전환 사이에서 유지합니다.
    /// </summary>
    public static class PrototypeSessionRuntime
    {
        public const string BattleSceneName = "PrototypeBattle";
        public const string HubSceneName = "PrototypeHub";

        // 참이면 허브가 이전 전투에서 캡처한 결과 스냅샷을 표시해야 합니다.
        public static bool HasLastBattleResult { get; private set; }
        public static BattleResultSnapshot LastBattleResult { get; private set; }
        // 참이면 허브에서 전투로 넘어가는 중이며, 전투 씬이 아직 요청을 소비하지 않은 상태입니다.
        public static bool HasPendingBattleEntryRequest { get; private set; }

        /// <summary>
        /// 다음 씬이 저장 데이터 없이도 읽을 수 있도록 마지막 전투 결과를 저장합니다.
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
        /// 사용자가 전투 씬 밖에서 새 전투 진입을 요청했음을 표시합니다.
        /// </summary>
        public static void RequestBattleEntry()
        {
            HasPendingBattleEntryRequest = true;
        }

        /// <summary>
        /// 전투 씬이 진입 요청을 인지한 뒤 대기 중인 진입 플래그를 지웁니다.
        /// </summary>
        public static void ConsumeBattleEntryRequest()
        {
            HasPendingBattleEntryRequest = false;
        }

        /// <summary>
        /// 엔진이 플레이어 도메인을 다시 로드할 때 정적 세션 상태를 초기화합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            HasLastBattleResult = false;
            LastBattleResult = default;
            HasPendingBattleEntryRequest = false;
        }
    }
}
