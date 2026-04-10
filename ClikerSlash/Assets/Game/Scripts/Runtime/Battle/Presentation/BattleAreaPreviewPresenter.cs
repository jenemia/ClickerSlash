using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 메인 화면의 반대 작업 구역을 좌상단 모니터로 실시간 렌더링합니다.
    /// </summary>
    public sealed class BattleAreaPreviewPresenter : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private CinemachineCamera laneVirtualCamera;
        [SerializeField] private CinemachineCamera loadingDockVirtualCamera;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private Vector2Int previewTextureSize = new(640, 360);

        private Camera _previewCamera;
        private RenderTexture _previewTexture;

        /// <summary>
        /// 테스트와 씬 빌더가 카메라 참조를 명시적으로 연결할 때 사용합니다.
        /// </summary>
        public void BindSceneReferences(
            Camera targetSceneCamera,
            CinemachineCamera targetLaneVirtualCamera = null,
            CinemachineCamera targetLoadingDockVirtualCamera = null)
        {
            sceneCamera = targetSceneCamera;
            laneVirtualCamera = targetLaneVirtualCamera;
            loadingDockVirtualCamera = targetLoadingDockVirtualCamera;
        }

        /// <summary>
        /// HUD가 만든 preview 모니터를 명시적으로 연결합니다.
        /// </summary>
        public void BindPreviewImage(RawImage targetPreviewImage)
        {
            previewImage = targetPreviewImage;
            if (previewImage != null)
            {
                previewImage.raycastTarget = false;
                previewImage.texture = _previewTexture;
            }
        }

        /// <summary>
        /// 테스트가 생성된 preview 카메라와 텍스처를 읽을 수 있게 노출합니다.
        /// </summary>
        public Camera PreviewCamera => _previewCamera;

        /// <summary>
        /// 테스트가 생성된 preview 렌더 텍스처를 확인할 수 있게 노출합니다.
        /// </summary>
        public RenderTexture PreviewTexture => _previewTexture;

        /// <summary>
        /// preview 카메라가 준비되면 매 프레임 반대 구역의 시점을 복사합니다.
        /// </summary>
        private void Update()
        {
            if (!EnsurePreviewRigInitialized())
            {
                return;
            }

            SyncPreviewCamera();
        }

        /// <summary>
        /// 씬 참조가 비어 있어도 런타임에서 다시 찾아 preview 리그를 복구합니다.
        /// </summary>
        private bool EnsurePreviewRigInitialized()
        {
            sceneCamera ??= Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            laneVirtualCamera ??= BattlePresentationBridge.FindVirtualCamera("LaneVirtualCamera");
            loadingDockVirtualCamera ??= BattlePresentationBridge.FindVirtualCamera("LoadingDockVirtualCamera");
            previewImage ??= FindPreviewImage();

            if (sceneCamera == null ||
                laneVirtualCamera == null ||
                loadingDockVirtualCamera == null ||
                previewImage == null)
            {
                return false;
            }

            EnsurePreviewCamera();
            EnsurePreviewTexture();
            return _previewCamera != null && (SupportsPreviewRenderTexture() ? _previewTexture != null : true);
        }

        /// <summary>
        /// 메인 카메라 설정을 복사한 뒤 target texture만 분리한 전용 preview 카메라를 만듭니다.
        /// </summary>
        private void EnsurePreviewCamera()
        {
            if (_previewCamera == null)
            {
                var previewCameraObject = new GameObject("BattleAreaPreviewCamera");
                previewCameraObject.transform.SetParent(transform, false);
                _previewCamera = previewCameraObject.AddComponent<Camera>();
            }

            _previewCamera.clearFlags = sceneCamera.clearFlags;
            _previewCamera.backgroundColor = sceneCamera.backgroundColor;
            _previewCamera.cullingMask = sceneCamera.cullingMask;
            _previewCamera.nearClipPlane = sceneCamera.nearClipPlane;
            _previewCamera.farClipPlane = sceneCamera.farClipPlane;
            _previewCamera.allowHDR = sceneCamera.allowHDR;
            _previewCamera.allowMSAA = sceneCamera.allowMSAA;
            _previewCamera.useOcclusionCulling = sceneCamera.useOcclusionCulling;
            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.enabled = true;
        }

        /// <summary>
        /// 프로젝트 에셋을 더럽히지 않도록 preview 렌더 텍스처는 런타임에서만 관리합니다.
        /// </summary>
        private void EnsurePreviewTexture()
        {
            if (!SupportsPreviewRenderTexture())
            {
                if (previewImage != null && previewImage.texture == _previewTexture)
                {
                    previewImage.texture = null;
                }

                ReleasePreviewTexture();
                return;
            }

            var width = Mathf.Max(64, previewTextureSize.x);
            var height = Mathf.Max(64, previewTextureSize.y);
            if (_previewTexture != null &&
                _previewTexture.width == width &&
                _previewTexture.height == height)
            {
                if (!_previewTexture.IsCreated())
                {
                    _previewTexture.Create();
                }

                if (previewImage.texture != _previewTexture)
                {
                    previewImage.texture = _previewTexture;
                }

                if (_previewCamera != null)
                {
                    _previewCamera.targetTexture = _previewTexture;
                }

                return;
            }

            ReleasePreviewTexture();
            _previewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "BattleAreaPreviewTexture"
            };
            _previewTexture.Create();
            previewImage.texture = _previewTexture;
            if (_previewCamera != null)
            {
                _previewCamera.targetTexture = _previewTexture;
            }
        }

        /// <summary>
        /// 현재 메인 작업 구역의 반대편 가상 카메라 포즈를 preview 카메라에 복사합니다.
        /// </summary>
        private void SyncPreviewCamera()
        {
            var loadingDockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            var shouldUseDockCamera = BattlePresentationBridge.ShouldUseLoadingDockCamera(loadingDockState);
            var previewTarget = shouldUseDockCamera ? laneVirtualCamera : loadingDockVirtualCamera;
            if (previewTarget == null)
            {
                return;
            }

            _previewCamera.transform.position = previewTarget.transform.position;
            _previewCamera.transform.eulerAngles = previewTarget.transform.eulerAngles;
            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.enabled = true;

            var lens = previewTarget.Lens;
            _previewCamera.orthographic = lens.Orthographic;
            _previewCamera.fieldOfView = lens.FieldOfView;
            _previewCamera.orthographicSize = lens.OrthographicSize;
        }

        /// <summary>
        /// 헤드리스 배치모드처럼 GPU가 없는 환경에서는 preview 텍스처 생성을 건너뜁니다.
        /// </summary>
        private static bool SupportsPreviewRenderTexture()
        {
            return SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        }

        /// <summary>
        /// 빌더가 만든 HUD preview 패널을 이름으로 재연결합니다.
        /// </summary>
        private static RawImage FindPreviewImage()
        {
            var rawImages = FindObjectsByType<RawImage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var candidate in rawImages)
            {
                if (candidate != null &&
                    string.Equals(candidate.name, "AreaPreviewImage", System.StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// scene unload나 오브젝트 파괴 시 런타임 텍스처를 즉시 해제합니다.
        /// </summary>
        private void CleanupPreviewRig()
        {
            if (previewImage != null && previewImage.texture == _previewTexture)
            {
                previewImage.texture = null;
            }

            ReleasePreviewTexture();

            if (_previewCamera != null)
            {
                Destroy(_previewCamera.gameObject);
                _previewCamera = null;
            }
        }

        /// <summary>
        /// RenderTexture 수명을 한곳에서 정리해 중복 해제 실수를 막습니다.
        /// </summary>
        private void ReleasePreviewTexture()
        {
            if (_previewTexture == null)
            {
                return;
            }

            if (_previewTexture.IsCreated())
            {
                _previewTexture.Release();
            }

            Destroy(_previewTexture);
            _previewTexture = null;
        }

        /// <summary>
        /// 비활성화될 때도 preview 리소스를 정리해 orphan texture를 남기지 않습니다.
        /// </summary>
        private void OnDisable()
        {
            CleanupPreviewRig();
        }

        /// <summary>
        /// 파괴 경로에서도 동일한 정리를 한 번 더 보장합니다.
        /// </summary>
        private void OnDestroy()
        {
            CleanupPreviewRig();
        }
    }
}
