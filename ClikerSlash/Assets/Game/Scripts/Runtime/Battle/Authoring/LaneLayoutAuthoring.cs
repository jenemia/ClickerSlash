using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 플레이 가능한 레인 위치를 정의하는 월드 X 좌표 목록을 저장합니다.
    /// </summary>
    public sealed class LaneLayoutAuthoring : MonoBehaviour
    {
        public List<float> LaneWorldXs = new() { -6.6666665f, -4f, -1.3333334f, 1.3333334f, 4f, 6.6666665f };
    }

    /// <summary>
    /// 이동과 스폰이 인덱스 기반 레인을 사용할 수 있도록 레인 위치를 ECS 데이터로 베이크합니다.
    /// </summary>
    public sealed class LaneLayoutAuthoringBaker : Baker<LaneLayoutAuthoring>
    {
        /// <summary>
        /// 레인 개수와 인덱스별 X 좌표를 함께 레인 엔티티에 기록합니다.
        /// </summary>
        public override void Bake(LaneLayoutAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new LaneLayout
            {
                LaneCount = authoring.LaneWorldXs.Count
            });

            var laneBuffer = AddBuffer<LaneWorldXElement>(entity);
            foreach (var laneX in authoring.LaneWorldXs)
            {
                laneBuffer.Add(new LaneWorldXElement { Value = laneX });
            }
        }
    }
}
