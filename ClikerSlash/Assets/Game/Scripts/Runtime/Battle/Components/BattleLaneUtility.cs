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

        /// <summary>
        /// 활성 레인을 중앙 정렬 연속 구간으로 배치할 때 시작 인덱스를 계산합니다.
        /// </summary>
        public static int ResolveCenteredActiveLaneStartIndex(int activeLaneCount, int physicalLaneCount)
        {
            var resolvedPhysicalLaneCount = math.max(1, physicalLaneCount);
            var resolvedActiveLaneCount = math.clamp(activeLaneCount, 1, resolvedPhysicalLaneCount);
            return math.max(0, (resolvedPhysicalLaneCount - resolvedActiveLaneCount) / 2);
        }

        /// <summary>
        /// 활성 구간의 시작/개수 기준으로 요청된 레인을 허용 범위 안으로 보정합니다.
        /// </summary>
        public static int ClampLaneToActiveRange(int lane, int activeLaneStartIndex, int activeLaneCount, int physicalLaneCount)
        {
            var resolvedPhysicalLaneCount = math.max(1, physicalLaneCount);
            var resolvedActiveLaneCount = math.clamp(activeLaneCount, 1, resolvedPhysicalLaneCount);
            var resolvedStartIndex = math.clamp(
                activeLaneStartIndex,
                0,
                resolvedPhysicalLaneCount - resolvedActiveLaneCount);
            var resolvedEndIndex = resolvedStartIndex + resolvedActiveLaneCount - 1;
            return math.clamp(lane, resolvedStartIndex, resolvedEndIndex);
        }

        /// <summary>
        /// 지정한 물리 레인이 현재 활성 구간 안에 포함되는지 반환합니다.
        /// </summary>
        public static bool IsLaneActive(int lane, int activeLaneStartIndex, int activeLaneCount)
        {
            if (activeLaneCount <= 0)
            {
                return false;
            }

            return lane >= activeLaneStartIndex && lane < activeLaneStartIndex + activeLaneCount;
        }
    }
}
