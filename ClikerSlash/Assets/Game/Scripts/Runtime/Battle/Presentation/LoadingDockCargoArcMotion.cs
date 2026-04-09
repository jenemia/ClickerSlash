using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 화물이 트럭 쪽으로 날아갈 때 쓰는 간단한 포물선 보간 유틸리티입니다.
    /// </summary>
    public static class LoadingDockCargoArcMotion
    {
        public static Vector3 Evaluate(Vector3 start, Vector3 end, float arcHeight, float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            var position = Vector3.Lerp(start, end, t);
            position.y += Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, arcHeight);
            return position;
        }
    }
}
