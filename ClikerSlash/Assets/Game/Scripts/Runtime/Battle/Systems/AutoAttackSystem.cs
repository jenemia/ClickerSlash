using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 플레이어가 현재 레인에서 처리 가능한 물류를 감지해 자동 처리 이벤트를 발행합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoMoveSystem))]
    public partial struct CargoHandleSystem : ISystem
    {
        /// <summary>
        /// 플레이어와 세션 설정이 준비된 뒤에만 처리 판정을 수행합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 같은 레인에 정지해 있고 아직 바쁘지 않을 때만 물류 자동 처리를 시도합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var entityManager = state.EntityManager;
            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var now = SystemAPI.Time.ElapsedTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var laneIndex = SystemAPI.GetComponent<LaneIndex>(playerEntity);
            var moveState = SystemAPI.GetComponent<LaneMoveState>(playerEntity);
            var handleState = SystemAPI.GetComponentRW<HandleState>(playerEntity);
            var maxHandleWeight = SystemAPI.GetComponent<MaxHandleWeight>(playerEntity);

            if (moveState.IsMoving != 0 || handleState.ValueRO.BusyUntilTime > now)
            {
                return;
            }

            var selectedCargo = Entity.Null;
            var selectedReward = 0;
            var selectedPenalty = 0;
            var selectedWeight = 0;
            var bestDistance = float.MaxValue;

            foreach (var (cargoLane, cargoWeight, cargoReward, cargoPenalty, cargoTransform, cargoEntity) in SystemAPI
                         .Query<RefRO<LaneIndex>, RefRO<CargoWeight>, RefRO<CargoReward>, RefRO<CargoPenalty>, RefRO<LocalTransform>>()
                         .WithAll<CargoTag>()
                         .WithEntityAccess())
            {
                if (cargoLane.ValueRO.Value != laneIndex.Value)
                {
                    continue;
                }

                var distance = math.abs(cargoTransform.ValueRO.Position.z - battleConfig.JudgmentLineZ);
                if (distance > battleConfig.HandleWindowHalfDepth || distance >= bestDistance)
                {
                    continue;
                }

                selectedCargo = cargoEntity;
                selectedReward = cargoReward.ValueRO.Value;
                selectedPenalty = cargoPenalty.ValueRO.Value;
                selectedWeight = cargoWeight.ValueRO.Value;
                bestDistance = distance;
            }

            if (selectedCargo == Entity.Null)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            if (selectedWeight > maxHandleWeight.Value)
            {
                var missedEvent = ecb.CreateEntity();
                ecb.AddComponent(missedEvent, new CargoMissedEvent { Penalty = selectedPenalty });
            }
            else
            {
                var handledEvent = ecb.CreateEntity();
                ecb.AddComponent(handledEvent, new CargoHandledEvent { Reward = selectedReward });
                handleState.ValueRW.BusyUntilTime = now + battleConfig.HandleDurationSeconds;
            }

            ecb.DestroyEntity(selectedCargo);
            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
