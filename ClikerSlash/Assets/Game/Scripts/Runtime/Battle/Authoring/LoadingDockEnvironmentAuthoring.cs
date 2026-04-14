using System;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 프로토타입 구역의 블록아웃 루트와 핵심 기준점을 보관합니다.
    /// </summary>
    public sealed class LoadingDockEnvironmentAuthoring : MonoBehaviour
    {
        [Header("상하차쪽 / 구역 루트")]
        [Tooltip("트럭 상하차 구역의 기준 루트입니다. 트럭 쪽 카메라 구도와 도크 연출 기준점으로 사용됩니다.")]
        public Transform truckBayRoot;

        [Tooltip("도크 로봇이 서 있거나 작업하는 기준 위치입니다.")]
        public Transform dockRobotAnchor;

        [Header("상하차쪽 / 팔레트 적재와 슬롯")]
        [Tooltip("레인으로 출고될 박스를 팔레트 위에 쌓아둘 기준 앵커입니다.")]
        public Transform palletStackAnchor;

        [Tooltip("팔레트에서 레일로 옮기는 동안 잠시 생성되는 transient cargo 오브젝트의 부모 루트입니다.")]
        public Transform transientCargoRoot;

        [Tooltip("상하차 미니게임의 활성 cargo 슬롯 위치들입니다. 슬롯 수와 순서가 UI/로직과 맞아야 합니다.")]
        public Transform[] cargoSlotAnchors;

        [Header("레인쪽 / 진입과 레일")]
        [Tooltip("각 레인으로 cargo가 진입하는 시작 앵커입니다. Battle의 논리 레인 수와 동일한 개수여야 합니다.")]
        public Transform[] laneEntryAnchors = Array.Empty<Transform>();

        [Header("레인쪽 / 프리팹과 렌더링")]
        [Tooltip("컨베이어 UV 스크롤을 적용할 렌더러 목록입니다. strict contract 기준으로 직접 모두 연결해야 합니다.")]
        public Renderer[] conveyorBeltRenderers = Array.Empty<Renderer>();

        [Tooltip("레인 cargo 타입별 실제 박스 프리팹 묶음입니다. 비어 있으면 Resources 기본 박스를 사용합니다.")]
        public CargoTypePrefabProfile laneCargoPrefabProfile;

        [Tooltip("팔레트 자체를 시각적으로 보여줄 프리팹입니다. 비워두면 박스만 적재됩니다.")]
        public GameObject laneCargoPalletPrefab;

        [Tooltip("컨베이어 재질에서 UV 오프셋을 움직일 텍스처 프로퍼티 이름입니다. 보통 _BaseMap을 사용합니다.")]
        public string conveyorTexturePropertyName = "_BaseMap";

        [Tooltip("컨베이어가 흐르는 것처럼 보이게 할 UV Y축 스크롤 속도입니다.")]
        public float conveyorUvSpeedY = 0.388f;

        [Header("상하차쪽 / 적재 간격")]
        [Tooltip("팔레트 그리드에서 박스 사이에 둘 추가 간격입니다.")]
        public float stackGap = 0.08f;

        [Tooltip("레이어를 위로 쌓을 때 층 사이에 둘 추가 높이 간격입니다.")]
        public float layerGap = 0.05f;

        [Header("레인쪽 / 표시 보정")]
        [Tooltip("모든 cargo 타입에 공통으로 더해지는 월드/시각 보정값입니다.")]
        public Vector3 globalCargoOffset;

        [Tooltip("Standard cargo를 레인 위에 표시할 때 추가로 적용할 보정값입니다.")]
        public Vector3 standardCargoOffset;

        [Tooltip("Fragile cargo를 레인 위에 표시할 때 추가로 적용할 보정값입니다.")]
        public Vector3 fragileCargoOffset;

        [Tooltip("Heavy cargo를 레인 위에 표시할 때 추가로 적용할 보정값입니다.")]
        public Vector3 heavyCargoOffset;

        /// <summary>
        /// 현재 authoring이 strict contract를 만족하는지 검사하고, 누락된 필드가 있으면 이유를 함께 반환합니다.
        /// </summary>
        public bool TryValidateStrictContract(int expectedLaneCount, out string errorMessage)
        {
            if (truckBayRoot == null)
            {
                errorMessage = "truckBayRoot가 연결되지 않았습니다.";
                return false;
            }

            if (dockRobotAnchor == null)
            {
                errorMessage = "dockRobotAnchor가 연결되지 않았습니다.";
                return false;
            }

            if (palletStackAnchor == null)
            {
                errorMessage = "palletStackAnchor가 연결되지 않았습니다.";
                return false;
            }

            if (transientCargoRoot == null)
            {
                errorMessage = "transientCargoRoot가 연결되지 않았습니다.";
                return false;
            }

            if (!HasAllEntries(cargoSlotAnchors, PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount))
            {
                errorMessage =
                    $"cargoSlotAnchors는 {PrototypeSessionRuntime.MaxLoadingDockActiveSlotCount}개의 null 없는 앵커를 가져야 합니다.";
                return false;
            }

            if (!HasAllEntries(laneEntryAnchors, expectedLaneCount))
            {
                errorMessage = $"laneEntryAnchors는 {expectedLaneCount}개의 null 없는 앵커를 가져야 합니다.";
                return false;
            }

            if (conveyorBeltRenderers == null || conveyorBeltRenderers.Length == 0)
            {
                errorMessage = "conveyorBeltRenderers가 비어 있습니다.";
                return false;
            }

            for (var rendererIndex = 0; rendererIndex < conveyorBeltRenderers.Length; rendererIndex += 1)
            {
                if (conveyorBeltRenderers[rendererIndex] != null)
                {
                    continue;
                }

                errorMessage = "conveyorBeltRenderers에 null 항목이 있습니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
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

        /// <summary>
        /// 타입별 레인 상자 프리팹 프로파일을 반환합니다.
        /// </summary>
        public CargoTypePrefabProfile GetLaneCargoPrefabProfile()
        {
            return laneCargoPrefabProfile;
        }

        /// <summary>
        /// inspector 배열이 기대 개수만큼 채워져 있는지 공통 규칙으로 검사합니다.
        /// </summary>
        private static bool HasAllEntries<T>(T[] values, int expectedCount) where T : UnityEngine.Object
        {
            if (values == null || values.Length != expectedCount)
            {
                return false;
            }

            for (var index = 0; index < values.Length; index += 1)
            {
                if (values[index] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
