using Unity.Entities;
using UnityEngine.InputSystem;

namespace ClikerSlash.Battle
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerInputCollectSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<StageProgressState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<StageProgressState>().IsFinished != 0)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

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

            foreach (var moveBuffer in SystemAPI.Query<DynamicBuffer<LaneMoveCommandBufferElement>>().WithAll<PlayerTag>())
            {
                moveBuffer.Add(new LaneMoveCommandBufferElement { Direction = direction });
            }
        }
    }
}
