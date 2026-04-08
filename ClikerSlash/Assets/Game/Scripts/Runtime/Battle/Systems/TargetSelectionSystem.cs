using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 플레이어 현재 레인에서 가장 가까운 적을 골라 자동 공격의 단일 기준 타깃으로 삼습니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyAdvanceSystem))]
    public partial struct TargetSelectionSystem : ISystem
    {
        /// <summary>
        /// 플레이어와 적 엔티티가 모두 있고 스테이지가 진행 중일 때만 타깃 선택을 수행합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 이동 중에는 타깃을 비우고, 그 외에는 방어선 기준 가장 앞선 적을 선택합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            foreach (var (laneIndex, targetState, moveState) in SystemAPI
                         .Query<RefRO<LaneIndex>, RefRW<TargetSelectionState>, RefRO<LaneMoveState>>()
                         .WithAll<PlayerTag>())
            {
                if (moveState.ValueRO.IsMoving != 0)
                {
                    // 이동 중 공격을 막기 위해 이동 상태에서는 타깃을 의도적으로 비웁니다.
                    targetState.ValueRW.Target = Entity.Null;
                    continue;
                }

                var selectedEnemy = Entity.Null;
                var selectedZ = float.MaxValue;

                // 적은 양수 Z에서 방어선 방향으로 내려오므로, 가장 작은 Z 값이 가장 앞선 적입니다.
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
