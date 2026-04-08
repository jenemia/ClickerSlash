using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyAdvanceSystem))]
    public partial struct TargetSelectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<EnemyTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (laneIndex, targetState, moveState) in SystemAPI
                         .Query<RefRO<LaneIndex>, RefRW<TargetSelectionState>, RefRO<LaneMoveState>>()
                         .WithAll<PlayerTag>())
            {
                if (moveState.ValueRO.IsMoving != 0)
                {
                    targetState.ValueRW.Target = Entity.Null;
                    continue;
                }

                var selectedEnemy = Entity.Null;
                var selectedZ = float.MaxValue;

                foreach (var (enemyLane, enemyTransform, enemyEntity) in SystemAPI
                             .Query<RefRO<LaneIndex>, RefRO<LocalTransform>>()
                             .WithAll<EnemyTag>()
                             .WithEntityAccess())
                {
                    if (enemyLane.ValueRO.Value != laneIndex.ValueRO.Value)
                    {
                        continue;
                    }

                    var enemyZ = enemyTransform.ValueRO.Position.z;
                    if (enemyZ >= selectedZ)
                    {
                        continue;
                    }

                    selectedEnemy = enemyEntity;
                    selectedZ = enemyZ;
                }

                targetState.ValueRW.Target = selectedEnemy;
            }
        }
    }
}
