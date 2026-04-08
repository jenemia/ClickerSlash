using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySpawnSystem))]
    public partial struct EnemyAdvanceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<StageProgressState>();
        }

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
                verticalPosition.ValueRW.Value -= moveSpeed.ValueRO.Value * deltaTime;
                var position = transform.ValueRO.Position;
                position.z = verticalPosition.ValueRO.Value;
                transform.ValueRW.Position = position;
            }
        }
    }
}
