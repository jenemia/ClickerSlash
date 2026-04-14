using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 현재 전투 세션에서 authoritative한 Env facade를 정적 레지스트리로 공유합니다.
    /// </summary>
    public static class BattleEnvironmentBindingRuntime
    {
        /// <summary>
        /// 현재 전투 세션에서 authoritative 환경으로 선택된 facade를 반환합니다.
        /// </summary>
        public static LoadingDockEnvironmentAuthoring CurrentEnvironment { get; private set; }

        /// <summary>
        /// authoritative 환경이 이미 바인딩되어 있는지 빠르게 확인합니다.
        /// </summary>
        public static bool IsReady => CurrentEnvironment != null;

        /// <summary>
        /// 도메인 리로드나 플레이 모드 재시작 시 정적 레지스트리를 초기화합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            CurrentEnvironment = null;
        }

        /// <summary>
        /// 현재 프레임부터 사용할 authoritative 환경 facade를 등록합니다.
        /// </summary>
        public static void Register(LoadingDockEnvironmentAuthoring environment)
        {
            CurrentEnvironment = environment;
        }

        /// <summary>
        /// 비활성화되는 facade가 현재 등록본과 같을 때만 안전하게 레지스트리를 비웁니다.
        /// </summary>
        public static void Clear(LoadingDockEnvironmentAuthoring environment)
        {
            if (CurrentEnvironment == environment)
            {
                CurrentEnvironment = null;
            }
        }
    }
}
