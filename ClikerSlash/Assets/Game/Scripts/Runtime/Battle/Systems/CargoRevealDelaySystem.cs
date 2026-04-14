using Unity.Entities;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 팔레트 반출 연출이 끝날 때까지 남은 표시 지연 시간을 감소시킵니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoSpawnSystem))]
    [UpdateBefore(typeof(CargoMoveSystem))]
    public partial struct CargoRevealDelaySystem : ISystem
    {
        /// <summary>
        /// 전투 진행 상태가 준비된 뒤에만 reveal 지연 시간을 갱신합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// reveal delay가 남아 있는 cargo들의 남은 시간을 프레임 단위로 감소시킵니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var revealDelay in SystemAPI.Query<RefRW<CargoRevealDelay>>().WithAll<CargoTag>())
            {
                revealDelay.ValueRW.RemainingSeconds = UnityEngine.Mathf.Max(0f, revealDelay.ValueRO.RemainingSeconds - deltaTime);
            }
        }
    }
}
