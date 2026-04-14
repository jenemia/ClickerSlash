using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 모든 물류를 실패선 방향으로 이동시키며 논리 위치와 시각 위치를 함께 갱신합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoSpawnSystem))]
    public partial struct CargoMoveSystem : ISystem
    {
        /// <summary>
        /// 세션 진행 상태가 준비된 뒤에만 이동 업데이트를 수행합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 세션이 아직 끝나지 않았을 때만 물류 이동을 진행합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (transform, moveSpeed, verticalPosition, revealDelay) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<MoveSpeed>, RefRW<VerticalPosition>, RefRO<CargoRevealDelay>>()
                         .WithAll<CargoTag>())
            {
                if (revealDelay.ValueRO.RemainingSeconds > 0f)
                {
                    // 팔레트에서 레일로 옮기는 reveal 연출 동안에는 논리 이동도 잠시 멈춥니다.
                    continue;
                }

                verticalPosition.ValueRW.Value -= moveSpeed.ValueRO.Value * deltaTime;
                var position = transform.ValueRO.Position;
                position.z = verticalPosition.ValueRO.Value;
                transform.ValueRW.Position = position;
            }
        }
    }
}
