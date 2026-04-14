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
            var economyModifier = SystemAPI.HasSingleton<EconomyModifier>()
                ? SystemAPI.GetSingleton<EconomyModifier>()
                : new EconomyModifier
                {
                    RewardMultiplier = 1f,
                    PenaltyMultiplier = 1f
                };

            if (moveState.IsMoving != 0 || handleState.ValueRO.BusyUntilTime > now)
            {
                return;
            }

            var selectedCargo = Entity.Null;
            var selectedReward = 0;
            var selectedPenalty = 0;
            var selectedWeight = 0;
            var bestDistance = float.MaxValue;

            var selectedKind = LoadingDockCargoKind.Standard;

            foreach (var (cargoLane, cargoWeight, cargoReward, cargoPenalty, cargoKind, cargoTransform, cargoEntity) in SystemAPI
                         .Query<RefRO<LaneIndex>, RefRO<CargoWeight>, RefRO<CargoReward>, RefRO<CargoPenalty>, RefRO<CargoKind>, RefRO<LocalTransform>>()
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
                selectedKind = cargoKind.ValueRO.Value;
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
                var adjustedPenalty = math.max(0, (int)math.round(selectedPenalty * economyModifier.PenaltyMultiplier));
                ecb.AddComponent(missedEvent, new CargoMissedEvent { Penalty = adjustedPenalty });
            }
            else
            {
                var handledEvent = ecb.CreateEntity();
                var adjustedReward = math.max(0, (int)math.round(selectedReward * economyModifier.RewardMultiplier));
                ecb.AddComponent(handledEvent, new CargoHandledEvent
                {
                    Reward = adjustedReward,
                    Kind = selectedKind,
                    Weight = selectedWeight
                });
                handleState.ValueRW.BusyUntilTime = now + battleConfig.HandleDurationSeconds;
            }

            ecb.DestroyEntity(selectedCargo);
            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// 배치된 레인 로봇이 플레이어보다 먼저 처리 가능한 물류를 자동 처리합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CargoMoveSystem))]
    [UpdateBefore(typeof(CargoHandleSystem))]
    public partial struct LaneRobotHandleSystem : ISystem
    {
        /// <summary>
        /// 레인 로봇과 세션 설정이 모두 준비된 뒤에만 자동 판정을 수행합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LaneRobotTag>();
            state.RequireForUpdate<BattleConfig>();
            state.RequireForUpdate<RobotProfile>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 현재 배치된 레인의 처리 가능 물류 중 가장 가까운 대상을 우선 처리합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var robotProfile = SystemAPI.GetSingleton<RobotProfile>();
            if (robotProfile.HasLaneRobotAccess == 0)
            {
                return;
            }

            var laneRobotEntity = SystemAPI.GetSingletonEntity<LaneRobotTag>();
            var laneRobotState = SystemAPI.GetComponent<LaneRobotState>(laneRobotEntity);
            if (laneRobotState.IsAssigned == 0)
            {
                return;
            }

            var handleState = SystemAPI.GetComponentRW<HandleState>(laneRobotEntity);
            var now = SystemAPI.Time.ElapsedTime;
            if (handleState.ValueRO.BusyUntilTime > now)
            {
                return;
            }

            var battleConfig = SystemAPI.GetSingleton<BattleConfig>();
            var economyModifier = SystemAPI.HasSingleton<EconomyModifier>()
                ? SystemAPI.GetSingleton<EconomyModifier>()
                : new EconomyModifier
                {
                    RewardMultiplier = 1f,
                    PenaltyMultiplier = 1f
                };

            var selectedCargo = Entity.Null;
            var selectedReward = 0;
            var selectedWeight = 0;
            var selectedKind = LoadingDockCargoKind.Standard;
            var bestDistance = float.MaxValue;

            foreach (var (cargoLane, cargoWeight, cargoReward, cargoKind, cargoTransform, cargoEntity) in SystemAPI
                         .Query<RefRO<LaneIndex>, RefRO<CargoWeight>, RefRO<CargoReward>, RefRO<CargoKind>, RefRO<LocalTransform>>()
                         .WithAll<CargoTag>()
                         .WithEntityAccess())
            {
                if (cargoLane.ValueRO.Value != laneRobotState.AssignedLane)
                {
                    continue;
                }

                if (!RobotHandlingRules.CanHandle(
                        robotProfile.MaxHandleWeight,
                        robotProfile.PrecisionTier,
                        cargoKind.ValueRO.Value,
                        cargoWeight.ValueRO.Value))
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
                selectedWeight = cargoWeight.ValueRO.Value;
                selectedKind = cargoKind.ValueRO.Value;
                bestDistance = distance;
            }

            if (selectedCargo == Entity.Null)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var handledEvent = ecb.CreateEntity();
            var adjustedReward = math.max(0, (int)math.round(selectedReward * economyModifier.RewardMultiplier));
            ecb.AddComponent(handledEvent, new CargoHandledEvent
            {
                Reward = adjustedReward,
                Kind = selectedKind,
                Weight = selectedWeight
            });
            ecb.DestroyEntity(selectedCargo);
            handleState.ValueRW.BusyUntilTime = now + battleConfig.HandleDurationSeconds;
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
