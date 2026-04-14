using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// ECS의 플레이어와 물류 위치를 프로토타입 씬의 단순 게임 오브젝트 뷰에 반영합니다.
    /// 상하차 진입 연출은 Cinemachine 가상 카메라 우선순위 전환으로 처리합니다.
    /// </summary>
    public sealed class BattlePresentationBridge : MonoBehaviour
    {
        [SerializeField] private GameObject playerViewPrefab;
        [SerializeField] private GameObject supportRobotViewPrefab;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private BattleViewAuthoring battleView;
        [SerializeField] private LoadingDockEnvironmentAuthoring loadingDockEnvironment;
        [SerializeField] private CinemachineCamera laneVirtualCamera;
        [SerializeField] private CinemachineCamera loadingDockVirtualCamera;
        [SerializeField] [Min(0.05f)] private float loadingDockTransitionDuration = PrototypeSessionRuntime.DefaultLoadingDockTransitionDurationSeconds;
        [SerializeField] [Min(0)] private int standbyCameraPriority = 10;
        [SerializeField] [Min(1)] private int liveCameraPriority = 20;
        [SerializeField] private Vector3 laneCargoGlobalOffset;
        [SerializeField] private Vector3 laneStandardCargoOffset;
        [SerializeField] private Vector3 laneFragileCargoOffset;
        [SerializeField] private Vector3 laneHeavyCargoOffset;

        private World _cachedWorld;
        private EntityQuery _playerQuery;
        private EntityQuery _cargoQuery;
        private EntityQuery _laneRobotQuery;
        private GameObject _playerInstance;
        private GameObject _laneRobotInstance;
        private GameObject _dockRobotInstance;
        private readonly Dictionary<Entity, GameObject> _cargoInstances = new();
        private readonly HashSet<int> _missingLaneCargoPrefabLogKeys = new();
        private bool _cameraRigInitialized;
        private bool _dockCameraIsLive;

        /// <summary>
        /// 현재 ECS 상태와 authoritative 환경 바인딩 결과를 읽어 레인/상하차 뷰를 매 프레임 동기화합니다.
        /// </summary>
        private void Update()
        {
            if (PrototypeSessionRuntime.IsPauseMenuOpen)
            {
                return;
            }

            // authoritative Env가 바뀌면 기존 직렬화 참조보다 런타임 바인딩 결과를 우선합니다.
            if (BattleEnvironmentBindingRuntime.CurrentEnvironment != null &&
                loadingDockEnvironment != BattleEnvironmentBindingRuntime.CurrentEnvironment)
            {
                loadingDockEnvironment = BattleEnvironmentBindingRuntime.CurrentEnvironment;
            }

            PrototypeSessionRuntime.AdvanceLoadingDockTransition(Mathf.Max(Time.unscaledDeltaTime, 1f / 60f));
            SyncLoadingDockCamera();

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
            CinemachineCamera targetLaneVirtualCamera = null,
            CinemachineCamera targetLoadingDockVirtualCamera = null)
        {
            sceneCamera = targetCamera;
            battleView = targetBattleView;
            loadingDockEnvironment = targetLoadingDockEnvironment;
            laneVirtualCamera = targetLaneVirtualCamera;
            loadingDockVirtualCamera = targetLoadingDockVirtualCamera;
            _cameraRigInitialized = false;
            _dockCameraIsLive = false;
            EnsureCameraRigInitialized();
        }

        /// <summary>
        /// additive Env 씬이 준비된 뒤 상하차 환경 참조만 다시 연결합니다.
        /// </summary>
        public void BindLoadingDockEnvironment(LoadingDockEnvironmentAuthoring targetLoadingDockEnvironment)
        {
            loadingDockEnvironment = targetLoadingDockEnvironment;
        }

        /// <summary>
        /// 레인/도크 actor 뷰 프리팹만 명시적으로 연결합니다.
        /// 레인 cargo 외형은 Env profile 또는 기본 Resources가 책임집니다.
        /// </summary>
        public void BindActorVisualPrefabs(
            GameObject targetPlayerViewPrefab,
            GameObject targetSupportRobotViewPrefab = null)
        {
            playerViewPrefab = targetPlayerViewPrefab;
            supportRobotViewPrefab = targetSupportRobotViewPrefab != null
                ? targetSupportRobotViewPrefab
                : targetPlayerViewPrefab;
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
                ComponentType.ReadOnly<LaneMoveState>(),
                ComponentType.ReadOnly<LocalTransform>());
            _cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoKind>(),
                ComponentType.ReadOnly<CargoPrefabVariant>(),
                ComponentType.ReadOnly<CargoRevealDelay>(),
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

            using var moveStates = _playerQuery.ToComponentDataArray<LaneMoveState>(Allocator.Temp);
            using var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (_playerInstance == null)
            {
                _playerInstance = Instantiate(playerViewPrefab, transform);
                _playerInstance.name = "WorkerView";
                ConfigurePlayerViewInstance(_playerInstance);
            }

            _playerInstance.transform.position = transforms[0].Position;
            var direction = moveStates[0].TargetLane - moveStates[0].StartLane;
            if (_playerInstance.TryGetComponent<BattleRobotKyleAnimatorDriver>(out var driver))
            {
                driver.ApplyPresentationState(moveStates[0].IsMoving != 0, Mathf.Sign(direction));
            }
        }

        private void SyncLaneRobot(EntityManager entityManager)
        {
            var supportViewPrefab = GetSupportRobotViewPrefab();
            if (supportViewPrefab == null || _laneRobotQuery.IsEmptyIgnoreFilter)
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

        private void SyncLoadingDockCamera()
        {
            if (!EnsureCameraRigInitialized())
            {
                return;
            }

            var dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            var shouldUseDockCamera = ShouldUseLoadingDockCamera(dockState);

            if (_dockCameraIsLive == shouldUseDockCamera)
            {
                return;
            }

            _dockCameraIsLive = shouldUseDockCamera;
            laneVirtualCamera.Priority = shouldUseDockCamera ? standbyCameraPriority : liveCameraPriority;
            loadingDockVirtualCamera.Priority = shouldUseDockCamera ? liveCameraPriority : standbyCameraPriority;
        }

        private bool EnsureCameraRigInitialized()
        {
            if (_cameraRigInitialized &&
                sceneCamera != null &&
                laneVirtualCamera != null &&
                loadingDockVirtualCamera != null)
            {
                return true;
            }

            sceneCamera ??= Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            laneVirtualCamera ??= FindVirtualCamera("LaneVirtualCamera");
            loadingDockVirtualCamera ??= FindVirtualCamera("LoadingDockVirtualCamera");

            if (sceneCamera == null ||
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
            _dockCameraIsLive = false;
            laneVirtualCamera.Priority = liveCameraPriority;
            loadingDockVirtualCamera.Priority = standbyCameraPriority;
            return true;
        }

        /// <summary>
        /// 현재 작업 영역 상태가 적재구역 시점을 메인 카메라로 써야 하는 상황인지 판정합니다.
        /// </summary>
        internal static bool ShouldUseLoadingDockCamera(LoadingDockRuntimeState dockState)
        {
            return dockState.TransitionPhase == WorkAreaTransitionPhase.EnteringLoadingDock ||
                   dockState.TransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock;
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

        /// <summary>
        /// 레인 물류 뷰의 높이와 피벗 차이를 맞추기 위한 종류별 시각 보정값을 반환합니다.
        /// </summary>
        public Vector3 GetLaneCargoOffset(LoadingDockCargoKind kind)
        {
            return laneCargoGlobalOffset + (kind switch
            {
                LoadingDockCargoKind.Fragile => laneFragileCargoOffset,
                LoadingDockCargoKind.Heavy => laneHeavyCargoOffset,
                _ => laneStandardCargoOffset
            });
        }

        /// <summary>
        /// Env 프로파일과 기본 Resources만 사용해 레인 cargo prefab을 해석합니다.
        /// </summary>
        private GameObject ResolveLaneCargoPrefab(LoadingDockCargoKind kind, int variantId)
        {
            var envProfile = loadingDockEnvironment != null ? loadingDockEnvironment.GetLaneCargoPrefabProfile() : null;
            if (envProfile != null)
            {
                var prefab = envProfile.ResolvePrefab(kind, variantId);
                if (prefab != null)
                {
                    return prefab;
                }
            }

            var defaultPrefab = CargoTypePrefabProfile.ResolveDefaultPrefab(kind, variantId);
            if (defaultPrefab != null)
            {
                return defaultPrefab;
            }

            LogMissingLaneCargoPrefabOnce(kind, variantId);
            return null;
        }

        /// <summary>
        /// reveal이 끝난 cargo만 실제 레인 뷰로 보이게 동기화합니다.
        /// </summary>
        private void SyncCargo(EntityManager entityManager)
        {
            using var entities = _cargoQuery.ToEntityArray(Allocator.Temp);
            using var kinds = _cargoQuery.ToComponentDataArray<CargoKind>(Allocator.Temp);
            using var variants = _cargoQuery.ToComponentDataArray<CargoPrefabVariant>(Allocator.Temp);
            using var revealDelays = _cargoQuery.ToComponentDataArray<CargoRevealDelay>(Allocator.Temp);
            using var transforms = _cargoQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var alive = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++)
            {
                var cargoEntity = entities[i];
                if (revealDelays[i].RemainingSeconds > 0f)
                {
                    // 팔레트 -> 레일 handoff가 끝나기 전에는 본 레인 뷰를 아직 보여주지 않습니다.
                    continue;
                }

                alive.Add(cargoEntity);
                var prefab = ResolveLaneCargoPrefab(kinds[i].Value, variants[i].Value);
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

                var visualPosition = (Vector3)transforms[i].Position;
                visualPosition.y = 0f;
                cargoView.transform.position = visualPosition + GetLaneCargoOffset(kinds[i].Value);
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

                // 더 이상 ECS에 존재하지 않거나 reveal 중으로 돌아간 뷰는 제거합니다.
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
            if (GetSupportRobotViewPrefab() == null || loadingDockEnvironment == null)
            {
                if (_dockRobotInstance != null)
                {
                    _dockRobotInstance.SetActive(false);
                }

                return;
            }

            var resolvedProgression = PrototypeSessionRuntime.GetResolvedMetaProgression();
            var loadingDockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            var shouldShow = resolvedProgression.HasDockRobotAccess &&
                             PrototypeSessionRuntime.HasInstalledDockRobot() &&
                             loadingDockState.CurrentArea == WorkAreaType.LoadingDock &&
                             loadingDockState.TransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock;

            if (_dockRobotInstance == null)
            {
                _dockRobotInstance = CreateRobotInstance("DockRobotView", new Color(0.28f, 0.86f, 0.64f), 0.82f);
            }

            _dockRobotInstance.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var anchor = loadingDockEnvironment.dockRobotAnchor;
            if (anchor != null)
            {
                _dockRobotInstance.transform.position = anchor.position;
            }
        }

        private GameObject CreateRobotInstance(string name, Color color, float scale)
        {
            var supportViewPrefab = GetSupportRobotViewPrefab();
            if (supportViewPrefab == null)
            {
                return null;
            }

            var robotInstance = Instantiate(supportViewPrefab, transform);
            robotInstance.name = name;
            robotInstance.transform.localScale = Vector3.one * scale;

            foreach (var renderer in robotInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.material.color = color;
            }

            return robotInstance;
        }

        private GameObject GetSupportRobotViewPrefab()
        {
            return supportRobotViewPrefab != null ? supportRobotViewPrefab : playerViewPrefab;
        }

        private static void ConfigurePlayerViewInstance(GameObject playerView)
        {
            DisableComponentsByTypeName(
                playerView,
                "CharacterController");
            DestroyComponentsByTypeName(
                playerView,
                "ThirdPersonController",
                "BasicRigidBodyPush",
                "PlayerInput",
                "StarterAssetsInputs");

            if (!playerView.TryGetComponent<BattleRobotKyleAnimatorDriver>(out _))
            {
                playerView.AddComponent<BattleRobotKyleAnimatorDriver>();
            }
        }

        private static void DisableComponentsByTypeName(GameObject root, params string[] typeNames)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                var componentTypeName = component.GetType().Name;
                for (var index = 0; index < typeNames.Length; index += 1)
                {
                    if (!string.Equals(componentTypeName, typeNames[index], System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var enabledProperty = component.GetType().GetProperty("enabled");
                    if (enabledProperty != null && enabledProperty.CanWrite)
                    {
                        enabledProperty.SetValue(component, false);
                    }

                    break;
                }
            }
        }

        private static void DestroyComponentsByTypeName(GameObject root, params string[] typeNames)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                var componentTypeName = component.GetType().Name;
                for (var index = 0; index < typeNames.Length; index += 1)
                {
                    if (!string.Equals(componentTypeName, typeNames[index], System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Destroy(component);
                    break;
                }
            }
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

            _missingLaneCargoPrefabLogKeys.Clear();
        }

        private void OnDisable()
        {
            CleanupAllViews();
        }

        /// <summary>
        /// Env profile과 기본 Resources 모두 비어 있을 때 같은 kind/variant 조합의 오류를 한 번만 기록합니다.
        /// </summary>
        private void LogMissingLaneCargoPrefabOnce(LoadingDockCargoKind kind, int variantId)
        {
            var logKey = ((int)kind << 16) ^ variantId;
            if (!_missingLaneCargoPrefabLogKeys.Add(logKey))
            {
                return;
            }

            Debug.LogError(
                $"BattlePresentationBridge: 레인 cargo prefab을 찾을 수 없습니다. kind={kind}, variantId={variantId}, envProfile={loadingDockEnvironment?.laneCargoPrefabProfile}",
                loadingDockEnvironment != null ? loadingDockEnvironment : this);
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
