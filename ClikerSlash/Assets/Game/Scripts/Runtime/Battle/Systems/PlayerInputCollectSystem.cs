using Unity.Entities;
using UnityEngine.InputSystem;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 키보드 입력을 프로토타입 플레이어의 레인 이동 큐 명령으로 변환합니다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerInputCollectSystem : ISystem
    {
        /// <summary>
        /// 입력을 읽기 전에 플레이어와 스테이지 상태가 존재하도록 요구합니다.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        /// <summary>
        /// 좌우 입력 한 번을 읽어 플레이어 이동 큐에 추가합니다.
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0 ||
                PrototypeSessionRuntime.IsLaneMovementLocked())
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // 한 프레임에 한 방향만 큐에 넣어 반대 방향 동시 입력이 애매한 명령으로 남지 않게 합니다.
            var direction = 0;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                direction = -1;
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                direction = 1;
            }

            if (direction == 0)
            {
                return;
            }

            // 플레이어 큐는 버퍼로 유지되어 여러 번 누른 입력을 이후 이동 업데이트에서 순차 처리할 수 있습니다.
            foreach (var moveBuffer in SystemAPI.Query<DynamicBuffer<LaneMoveCommandBufferElement>>().WithAll<PlayerTag>())
            {
                moveBuffer.Add(new LaneMoveCommandBufferElement { Direction = direction });
            }
        }
    }
}
