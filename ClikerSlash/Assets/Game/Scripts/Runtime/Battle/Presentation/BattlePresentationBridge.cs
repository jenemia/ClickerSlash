using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// ECS 런타임 상태를 씬 뷰 오브젝트와 3구역 카메라 포커스에 반영합니다.
    /// </summary>
    public sealed class BattlePresentationBridge : MonoBehaviour
    {
        [Header("뷰 프리팹")]
        [Tooltip("작업자와 보조 오브젝트에 사용할 기본 뷰 프리팹입니다.")]
        [SerializeField] private GameObject playerViewPrefab;
        [Tooltip("물류 종류별 시각 프리팹 세트입니다.")]
        [SerializeField] private CargoVisualPrefabSet cargoVisualPrefabs;

        [Header("씬 참조")]
        [Tooltip("시네머신 브레인이 붙은 메인 카메라입니다.")]
        [SerializeField] private Camera sceneCamera;
        [Tooltip("전투 레인과 메인 무대 배치 정보를 담는 authoring입니다.")]
        [SerializeField] private BattleViewAuthoring battleView;
        [Tooltip("상하차 구역 앵커를 담는 authoring입니다.")]
        [SerializeField] private LoadingDockEnvironmentAuthoring loadingDockEnvironment;

        [Header("가상 카메라")]
        [Tooltip("승인 구역을 비추는 virtual camera입니다.")]
        [SerializeField] private CinemachineCamera approvalVirtualCamera;
        [Tooltip("레인선택 구역을 비추는 virtual camera입니다.")]
        [SerializeField] private CinemachineCamera laneVirtualCamera;
        [Tooltip("상하차 구역을 비추는 virtual camera입니다.")]
        [SerializeField] private CinemachineCamera loadingDockVirtualCamera;

        [Header("카메라 전환")]
        [Tooltip("구역 카메라 전환 시 시네머신 블렌드 시간입니다.")]
        [SerializeField] [Min(0.05f)] private float loadingDockTransitionDuration = PrototypeSessionRuntime.DefaultLoadingDockTransitionDurationSeconds;
        [Tooltip("비활성 카메라에 줄 우선순위입니다.")]
        [SerializeField] [Min(0)] private int standbyCameraPriority = 10;
        [Tooltip("현재 포커스 카메라에 줄 우선순위입니다.")]
        [SerializeField] [Min(1)] private int liveCameraPriority = 20;

        private World _cachedWorld;
        private EntityQuery _playerQuery;
        private EntityQuery _cargoQuery;
        private EntityQuery _laneRobotQuery;
        private GameObject _playerInstance;
        private GameObject _laneRobotInstance;
        private GameObject _dockRobotInstance;
        private readonly Dictionary<Entity, GameObject> _cargoInstances = new();
        private bool _cameraRigInitialized;
        private BattleMiniGameArea _currentLiveArea = BattleMiniGameArea.Approval;

        /// <summary>
        /// 매 프레임 카메라 포커스와 ECS 뷰 동기화를 갱신합니다.
        /// </summary>
        private void Update()
        {
            if (PrototypeSessionRuntime.IsPauseMenuOpen)
            {
                return;
            }

            PrototypeSessionRuntime.AdvanceLoadingDockTransition(Mathf.Max(Time.unscaledDeltaTime, 1f / 60f));
            SyncFocusedCamera();

            if (!TryPrepareQueries(out var entityManager))
            {
                CleanupAllViews();
                return;
            }

            SyncPlayer(entityManager);
            SyncLaneRobot(entityManager);
            SyncCargo(entityManager);
            SyncDockRobot();
        }

        /// <summary>
        /// 테스트나 씬 빌더가 카메라/뷰 참조를 명시적으로 연결할 때 사용합니다.
        /// </summary>
        public void BindSceneReferences(
            Camera targetCamera,
            BattleViewAuthoring targetBattleView,
            LoadingDockEnvironmentAuthoring targetLoadingDockEnvironment = null,
            CinemachineCamera targetApprovalVirtualCamera = null,
            CinemachineCamera targetLaneVirtualCamera = null,
            CinemachineCamera targetLoadingDockVirtualCamera = null)
        {
            sceneCamera = targetCamera;
            battleView = targetBattleView;
            loadingDockEnvironment = targetLoadingDockEnvironment;
            approvalVirtualCamera = targetApprovalVirtualCamera;
            laneVirtualCamera = targetLaneVirtualCamera;
            loadingDockVirtualCamera = targetLoadingDockVirtualCamera;
            _cameraRigInitialized = false;
            _currentLiveArea = BattleMiniGameArea.Approval;
            EnsureCameraRigInitialized();
        }

        /// <summary>
        /// 레인과 상하차가 공유하는 물류 프리팹 세트를 명시적으로 연결합니다.
        /// </summary>
        public void BindVisualPrefabs(GameObject targetPlayerViewPrefab, CargoVisualPrefabSet targetCargoVisualPrefabs)
        {
            playerViewPrefab = targetPlayerViewPrefab;
            cargoVisualPrefabs = targetCargoVisualPrefabs;
        }

        private bool TryPrepareQueries(out EntityManager entityManager)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            if (_cachedWorld == world)
            {
                return true;
            }

            _cachedWorld = world;
            _playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoKind>(),
                ComponentType.ReadOnly<LocalTransform>());
            _laneRobotQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<LaneRobotTag>(),
                ComponentType.ReadOnly<LaneRobotState>(),
                ComponentType.ReadOnly<LocalTransform>());
            return true;
        }

        private void SyncPlayer(EntityManager entityManager)
        {
            if (playerViewPrefab == null || _playerQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (_playerInstance == null)
            {
                _playerInstance = Instantiate(playerViewPrefab, transform);
                _playerInstance.name = "WorkerView";
            }

            _playerInstance.transform.position = transforms[0].Position;
        }

        private void SyncLaneRobot(EntityManager entityManager)
        {
            if (playerViewPrefab == null || _laneRobotQuery.IsEmptyIgnoreFilter)
            {
                if (_laneRobotInstance != null)
                {
                    _laneRobotInstance.SetActive(false);
                }

                return;
            }

            using var laneRobotStates = _laneRobotQuery.ToComponentDataArray<LaneRobotState>(Allocator.Temp);
            using var transforms = _laneRobotQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (laneRobotStates.Length == 0)
            {
                return;
            }

            if (_laneRobotInstance == null)
            {
                _laneRobotInstance = CreateRobotInstance("LaneRobotView", new Color(0.92f, 0.55f, 0.18f), 0.9f);
            }

            var laneRobotState = laneRobotStates[0];
            var shouldShow = laneRobotState.IsAssigned != 0;
            _laneRobotInstance.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            _laneRobotInstance.transform.position = transforms[0].Position;
        }

        /// <summary>
        /// 현재 포커스된 미니게임 구역 하나만 live priority를 가지도록 카메라를 갱신합니다.
        /// </summary>
        private void SyncFocusedCamera()
        {
            if (!EnsureCameraRigInitialized())
            {
                return;
            }

            var focusedArea = PrototypeSessionRuntime.GetFocusedMiniGameArea();
            if (_currentLiveArea == focusedArea)
            {
                return;
            }

            _currentLiveArea = focusedArea;
            approvalVirtualCamera.Priority = focusedArea == BattleMiniGameArea.Approval ? liveCameraPriority : standbyCameraPriority;
            laneVirtualCamera.Priority = focusedArea == BattleMiniGameArea.RouteSelection ? liveCameraPriority : standbyCameraPriority;
            loadingDockVirtualCamera.Priority = focusedArea == BattleMiniGameArea.LoadingDock ? liveCameraPriority : standbyCameraPriority;
        }

        private bool EnsureCameraRigInitialized()
        {
            if (_cameraRigInitialized &&
                sceneCamera != null &&
                approvalVirtualCamera != null &&
                laneVirtualCamera != null &&
                loadingDockVirtualCamera != null)
            {
                return true;
            }

            sceneCamera ??= Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            approvalVirtualCamera ??= FindVirtualCamera("ApprovalVirtualCamera");
            laneVirtualCamera ??= FindVirtualCamera("LaneVirtualCamera");
            loadingDockVirtualCamera ??= FindVirtualCamera("LoadingDockVirtualCamera");

            if (sceneCamera == null ||
                approvalVirtualCamera == null ||
                laneVirtualCamera == null ||
                loadingDockVirtualCamera == null)
            {
                return false;
            }

            var brain = sceneCamera.GetComponent<CinemachineBrain>() ?? sceneCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                Mathf.Max(0.05f, loadingDockTransitionDuration));

            _cameraRigInitialized = true;
            _currentLiveArea = PrototypeSessionRuntime.GetFocusedMiniGameArea();
            approvalVirtualCamera.Priority = _currentLiveArea == BattleMiniGameArea.Approval ? liveCameraPriority : standbyCameraPriority;
            laneVirtualCamera.Priority = _currentLiveArea == BattleMiniGameArea.RouteSelection ? liveCameraPriority : standbyCameraPriority;
            loadingDockVirtualCamera.Priority = standbyCameraPriority;
            if (_currentLiveArea == BattleMiniGameArea.LoadingDock)
            {
                loadingDockVirtualCamera.Priority = liveCameraPriority;
            }
            return true;
        }

        /// <summary>
        /// 현재 작업 영역 상태가 적재구역 시점을 메인 카메라로 써야 하는 상황인지 판정합니다.
        /// </summary>
        internal static bool ShouldUseLoadingDockCamera(LoadingDockRuntimeState dockState)
        {
            return dockState.CurrentArea == WorkAreaType.LoadingDock;
        }

        /// <summary>
        /// 씬 빌더와 프리뷰 프레젠터가 이름으로 가상 카메라를 재연결할 수 있게 도와줍니다.
        /// </summary>
        internal static CinemachineCamera FindVirtualCamera(string cameraName)
        {
            var cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                if (camera != null && string.Equals(camera.name, cameraName, System.StringComparison.Ordinal))
                {
                    return camera;
                }
            }

            return null;
        }

        private void SyncCargo(EntityManager entityManager)
        {
            if (cargoVisualPrefabs == null || !cargoVisualPrefabs.IsComplete)
            {
                return;
            }

            using var entities = _cargoQuery.ToEntityArray(Allocator.Temp);
            using var kinds = _cargoQuery.ToComponentDataArray<CargoKind>(Allocator.Temp);
            using var transforms = _cargoQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var alive = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++)
            {
                var cargoEntity = entities[i];
                alive.Add(cargoEntity);
                var prefab = cargoVisualPrefabs.Resolve(kinds[i].Value);
                if (prefab == null)
                {
                    continue;
                }

                if (!_cargoInstances.TryGetValue(cargoEntity, out var cargoView) || cargoView == null)
                {
                    cargoView = Instantiate(prefab, transform);
                    cargoView.name = $"CargoView_{cargoEntity.Index}";
                    _cargoInstances[cargoEntity] = cargoView;
                }

                cargoView.transform.position = transforms[i].Position;
            }

            if (_cargoInstances.Count == 0)
            {
                return;
            }

            var staleEntities = ListPool<Entity>.Get();
            foreach (var pair in _cargoInstances)
            {
                if (alive.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }

                staleEntities.Add(pair.Key);
            }

            foreach (var staleEntity in staleEntities)
            {
                _cargoInstances.Remove(staleEntity);
            }
            ListPool<Entity>.Release(staleEntities);
        }

        private void SyncDockRobot()
        {
            if (playerViewPrefab == null || loadingDockEnvironment == null)
            {
                if (_dockRobotInstance != null)
                {
                    _dockRobotInstance.SetActive(false);
                }

                return;
            }

            var shouldShow = PrototypeSessionRuntime.GetFocusedMiniGameArea() == BattleMiniGameArea.LoadingDock;

            if (_dockRobotInstance == null)
            {
                _dockRobotInstance = CreateRobotInstance("DockRobotView", new Color(0.28f, 0.86f, 0.64f), 0.82f);
            }

            _dockRobotInstance.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var anchor = loadingDockEnvironment.dockRobotAnchor != null
                ? loadingDockEnvironment.dockRobotAnchor
                : loadingDockEnvironment.truckDropZone;
            if (anchor != null)
            {
                _dockRobotInstance.transform.position = anchor.position;
            }
        }

        private GameObject CreateRobotInstance(string name, Color color, float scale)
        {
            var robotInstance = Instantiate(playerViewPrefab, transform);
            robotInstance.name = name;
            robotInstance.transform.localScale = Vector3.one * scale;

            foreach (var renderer in robotInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.material.color = color;
            }

            return robotInstance;
        }

        private void CleanupAllViews()
        {
            if (_playerInstance != null)
            {
                Destroy(_playerInstance);
                _playerInstance = null;
            }

            foreach (var pair in _cargoInstances)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }
            _cargoInstances.Clear();

            if (_laneRobotInstance != null)
            {
                Destroy(_laneRobotInstance);
                _laneRobotInstance = null;
            }

            if (_dockRobotInstance != null)
            {
                Destroy(_dockRobotInstance);
                _dockRobotInstance = null;
            }
        }

        private void OnDisable()
        {
            CleanupAllViews();
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
