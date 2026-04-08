using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 레인 인덱스를 안전한 런타임 값으로 변환할 때 쓰는 공용 유틸리티입니다.
    /// </summary>
    public static class BattleLaneUtility
    {
        /// <summary>
        /// 요청된 레인 인덱스를 현재 사용 가능한 레인 범위 안으로 보정합니다.
        /// </summary>
        public static int ClampLane(int lane, int laneCount)
        {
            return math.clamp(lane, 0, math.max(0, laneCount - 1));
        }

        /// <summary>
        /// 런타임과 동일한 보정 규칙을 적용한 뒤 해당 레인의 월드 X 위치를 반환합니다.
        /// </summary>
        public static float GetLaneX(DynamicBuffer<LaneWorldXElement> laneXs, int lane)
        {
            var clampedLane = ClampLane(lane, laneXs.Length);
            return laneXs[clampedLane].Value;
        }
    }
}
