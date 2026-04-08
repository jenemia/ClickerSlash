using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ClikerSlash.Battle
{
    public static class BattleLaneUtility
    {
        public static int ClampLane(int lane, int laneCount)
        {
            return math.clamp(lane, 0, math.max(0, laneCount - 1));
        }

        public static float GetLaneX(DynamicBuffer<LaneWorldXElement> laneXs, int lane)
        {
            var clampedLane = ClampLane(lane, laneXs.Length);
            return laneXs[clampedLane].Value;
        }
    }
}
