using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    public sealed class LaneLayoutAuthoring : MonoBehaviour
    {
        public List<float> LaneWorldXs = new() { -4.5f, -1.5f, 1.5f, 4.5f };
    }

    public sealed class LaneLayoutAuthoringBaker : Baker<LaneLayoutAuthoring>
    {
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
