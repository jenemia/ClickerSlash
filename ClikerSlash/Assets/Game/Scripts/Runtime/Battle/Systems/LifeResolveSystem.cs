using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 실패선을 지난 물류를 감지해 제거하고 실패 이벤트를 발행합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoHandleSystem))]
    public partial struct CargoMissSystem : ISystem
    {
        /// <summary>
        /// 실패 처리 전에 세션 상태와 설정이 준비되어야 합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 실패선을 통과한 모든 물류를 정리하고 돈 손실 이벤트를 남깁니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var failLineZ = SystemAPI.GetSingleton<BattleConfig>().FailLineZ;
            var economyModifier = SystemAPI.HasSingleton<EconomyModifier>()
                ? SystemAPI.GetSingleton<EconomyModifier>()
                : new EconomyModifier
                {
                    RewardMultiplier = 1f,
                    PenaltyMultiplier = 1f
                };
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (cargoPenalty, cargoTransform, cargoEntity) in SystemAPI
                         .Query<RefRO<CargoPenalty>, RefRO<LocalTransform>>()
                         .WithAll<CargoTag>()
                         .WithEntityAccess())
            {
                if (cargoTransform.ValueRO.Position.z > failLineZ)
                {
                    continue;
                }

                var missedEvent = ecb.CreateEntity();
                var adjustedPenalty = Unity.Mathematics.math.max(
                    0,
                    (int)Unity.Mathematics.math.round(cargoPenalty.ValueRO.Value * economyModifier.PenaltyMultiplier));
                ecb.AddComponent(missedEvent, new CargoMissedEvent { Penalty = adjustedPenalty });
                ecb.DestroyEntity(cargoEntity);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
