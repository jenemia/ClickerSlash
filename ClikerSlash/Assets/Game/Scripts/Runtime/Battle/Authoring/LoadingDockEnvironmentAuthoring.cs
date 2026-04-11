using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 프로토타입 구역의 블록아웃 루트와 핵심 기준점을 보관합니다.
    /// </summary>
    public sealed class LoadingDockEnvironmentAuthoring : MonoBehaviour
    {
        public Transform cargoBayRoot;
        public Transform truckBayRoot;
        public Transform cargoThrowOrigin;
        public Transform truckDropZone;
        public Transform dockRobotAnchor;
        public Transform[] cargoSlotAnchors;
        public Renderer[] conveyorBeltRenderers = Array.Empty<Renderer>();
        public string conveyorTexturePropertyName = "_BaseMap";
        public float conveyorUvSpeedY = 0.485f;
        public Vector3 globalCargoOffset;
        public Vector3 standardCargoOffset;
        public Vector3 fragileCargoOffset;
        public Vector3 heavyCargoOffset;

        /// <summary>
        /// inspector 참조가 비어 있으면 레인 루트의 컨베이어 벨트 메쉬를 자동으로 찾아 반환합니다.
        /// </summary>
        public Renderer[] GetConveyorBeltRenderers()
        {
            if (conveyorBeltRenderers != null && conveyorBeltRenderers.Length > 0)
            {
                return conveyorBeltRenderers;
            }

            var discoveredRenderers = new List<Renderer>();
            var laneLayout = FindFirstObjectByType<LaneLayoutAuthoring>();
            if (laneLayout != null)
            {
                foreach (var renderer in laneLayout.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !string.Equals(renderer.name, "PaletArrow.010", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    discoveredRenderers.Add(renderer);
                }
            }

            conveyorBeltRenderers = discoveredRenderers.ToArray();
            return conveyorBeltRenderers;
        }

        /// <summary>
        /// 물류 종류별 보정값을 공통 오프셋과 합쳐 벨트 위 최종 배치 보정량으로 반환합니다.
        /// </summary>
        public Vector3 GetCargoOffset(LoadingDockCargoKind kind)
        {
            return globalCargoOffset + (kind switch
            {
                LoadingDockCargoKind.Fragile => fragileCargoOffset,
                LoadingDockCargoKind.Heavy => heavyCargoOffset,
                _ => standardCargoOffset
            });
        }
    }
}
