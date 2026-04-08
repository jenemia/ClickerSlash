using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 모든 적을 방어선 방향으로 이동시키며 논리 위치와 시각 위치를 함께 갱신합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySpawnSystem))]
    public partial struct EnemyAdvanceSystem : ISystem
    {
        /// <summary>
        /// 전투 진행 상태와 적 엔티티가 준비된 뒤에만 이동 업데이트를 수행합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 전투가 아직 끝나지 않았을 때만 적의 전진을 진행합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (transform, moveSpeed, verticalPosition) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<MoveSpeed>, RefRW<VerticalPosition>>()
                         .WithAll<EnemyTag>())
            {
                // 논리 진행도와 실제 지역 변환값이 항상 같은 Z 값을 가리키도록 맞춰 줍니다.
                verticalPosition.ValueRW.Value -= moveSpeed.ValueRO.Value * deltaTime;
                var position = transform.ValueRO.Position;
                position.z = verticalPosition.ValueRO.Value;
                transform.ValueRW.Position = position;
            }
        }
    }
}
