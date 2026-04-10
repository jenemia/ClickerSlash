using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 구역 활성화 시 프로토타입 화물과 입력 규칙을 화면에 반영합니다.
    /// </summary>
    public sealed class LoadingDockMiniGamePresenter : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private LoadingDockEnvironmentAuthoring environment;
        [SerializeField] [Min(0.1f)] private float cargoSpacing = 1.9f;
        [SerializeField] [Min(0.1f)] private float fragileDragDistance = 7f;
        [SerializeField] [Min(0.05f)] private float cargoFlightDuration = 0.45f;
        [SerializeField] [Min(0f)] private float cargoArcHeight = 2.1f;
        [SerializeField] [Min(0f)] private float completionReturnDelay = 0.8f;

        private readonly Dictionary<string, GameObject> _cargoViews = new();
        private readonly Dictionary<GameObject, string> _cargoIdsByView = new();
        private readonly Dictionary<string, Vector3> _homePositions = new();
        private readonly Dictionary<string, LoadingDockCargoVisualFlight> _cargoFlights = new();
        private readonly HashSet<string> _completedCargoFlights = new();
        private LoadingDockMiniGameRuntimeState _runtimeState;
        private string _draggingCargoId;
        private bool _wasLoadingDockActive;
        private bool _completionQueued;
        private float _completionCountdown;

        public void BindSceneReferences(Camera targetCamera, LoadingDockEnvironmentAuthoring targetEnvironment)
        {
            sceneCamera = targetCamera;
            environment = targetEnvironment;
        }

        private void Update()
        {
            sceneCamera ??= Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            environment ??= FindFirstObjectByType<LoadingDockEnvironmentAuthoring>();
            if (sceneCamera == null || environment == null)
            {
                ClearCargoViews();
                return;
            }

            var loadingDockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            var isLoadingDockActive = loadingDockState.CurrentArea == WorkAreaType.LoadingDock &&
                                      loadingDockState.TransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock;
            if (!isLoadingDockActive)
            {
                _wasLoadingDockActive = false;
                _draggingCargoId = null;
                ClearCargoViews();
                return;
            }

            if (!_wasLoadingDockActive || _runtimeState == null)
            {
                PrototypeSessionRuntime.ClearLastLoadingDockResult();
                _runtimeState = LoadingDockMiniGameRuntime.CreatePrototypeRound();
                BuildCargoViews();
            }

            _wasLoadingDockActive = true;
            if (PrototypeSessionRuntime.IsPauseMenuOpen)
            {
                return;
            }

            HandlePointerInput();
            RefreshCargoViews();
            TryQueueRoundCompletion();
        }

        private void OnGUI()
        {
            if (!_wasLoadingDockActive || _runtimeState == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(Screen.width - 380f, 24f, 340f, 200f), GUI.skin.box);
            GUILayout.Label("Loading Dock");
            foreach (var cargo in _runtimeState.cargos)
            {
                if (cargo == null)
                {
                    continue;
                }

                GUILayout.Label(BuildCargoSummary(cargo));
            }

            if (_runtimeState.IsCompleted)
            {
                GUILayout.Space(8f);
                GUILayout.Label(_completionQueued
                    ? "모든 화물 적재 완료 - 레인 복귀 중"
                    : "모든 화물 입력 완료");
            }

            GUILayout.EndArea();
        }

        private void BuildCargoViews()
        {
            ClearCargoViews();
            if (_runtimeState?.cargos == null || environment.cargoBayRoot == null)
            {
                return;
            }

            for (var index = 0; index < _runtimeState.cargos.Count; index += 1)
            {
                var cargo = _runtimeState.cargos[index];
                if (cargo == null)
                {
                    continue;
                }

                var cargoView = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cargoView.name = cargo.displayName;
                cargoView.transform.SetParent(transform, false);
                cargoView.transform.position = ResolveHomePosition(index);
                cargoView.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

                var renderer = cargoView.GetComponent<Renderer>();
                renderer.material.color = ResolveCargoColor(cargo);

                _cargoViews[cargo.cargoId] = cargoView;
                _cargoIdsByView[cargoView] = cargo.cargoId;
                _homePositions[cargo.cargoId] = cargoView.transform.position;
            }
        }

        private void HandlePointerInput()
        {
            if (_runtimeState == null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && TryRaycastCargo(out var pointerDownCargoId))
            {
                var cargo = LoadingDockMiniGameRuntime.GetCargo(_runtimeState, pointerDownCargoId);
                if (cargo != null && cargo.interactionType == LoadingDockCargoInteractionType.FragileDrag)
                {
                    if (LoadingDockMiniGameRuntime.BeginFragileDrag(_runtimeState, pointerDownCargoId))
                    {
                        _draggingCargoId = pointerDownCargoId;
                    }
                }
                else
                {
                    LoadingDockMiniGameRuntime.RegisterClick(_runtimeState, pointerDownCargoId);
                }
            }

            if (!string.IsNullOrEmpty(_draggingCargoId) && Input.GetMouseButton(0))
            {
                if (_cargoViews.TryGetValue(_draggingCargoId, out var cargoView) && cargoView != null)
                {
                    var progress = ResolveDragProgress(cargoView.transform.position);
                    LoadingDockMiniGameRuntime.UpdateFragileDrag(_runtimeState, _draggingCargoId, progress);
                    cargoView.transform.position = ResolveDragWorldPosition(cargoView.transform.position.y);
                }
            }

            if (!string.IsNullOrEmpty(_draggingCargoId) && Input.GetMouseButtonUp(0))
            {
                LoadingDockMiniGameRuntime.EndFragileDrag(_runtimeState, _draggingCargoId);
                _draggingCargoId = null;
            }
        }

        private void RefreshCargoViews()
        {
            if (_runtimeState?.cargos == null || environment.truckDropZone == null)
            {
                return;
            }

            var deliveredIndex = 0;
            foreach (var cargo in _runtimeState.cargos)
            {
                if (cargo == null || !_cargoViews.TryGetValue(cargo.cargoId, out var cargoView) || cargoView == null)
                {
                    continue;
                }

                var renderer = cargoView.GetComponent<Renderer>();
                renderer.material.color = ResolveCargoColor(cargo);

                if (cargo.deliveryState == LoadingDockCargoDeliveryState.Delivered)
                {
                    var deliveryTarget = environment.truckDropZone.position + new Vector3(0f, 0f, deliveredIndex * 1.25f);
                    RefreshCargoFlight(cargo.cargoId, cargoView, deliveryTarget);
                    cargoView.GetComponent<Collider>().enabled = false;
                    deliveredIndex += 1;
                    continue;
                }

                cargoView.GetComponent<Collider>().enabled = true;
                if (cargo.cargoId == _draggingCargoId)
                {
                    continue;
                }

                cargoView.transform.position = _homePositions[cargo.cargoId];
            }
        }

        private bool TryRaycastCargo(out string cargoId)
        {
            cargoId = null;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 200f))
            {
                return false;
            }

            return _cargoIdsByView.TryGetValue(hit.collider.gameObject, out cargoId);
        }

        private Vector3 ResolveHomePosition(int index)
        {
            var row = index / 2;
            var column = index % 2;
            return environment.cargoBayRoot.position +
                   new Vector3(column * cargoSpacing, 1.1f, row * cargoSpacing);
        }

        private Vector3 ResolveDragWorldPosition(float currentY)
        {
            var ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            var dragPlane = new Plane(Vector3.up, new Vector3(0f, currentY, 0f));
            if (!dragPlane.Raycast(ray, out var distance))
            {
                return environment.cargoBayRoot.position + new Vector3(0f, currentY, 0f);
            }

            var hitPoint = ray.GetPoint(distance);
            var clampedX = Mathf.Clamp(hitPoint.x, environment.cargoBayRoot.position.x - 2f, environment.truckDropZone.position.x + 2f);
            var clampedZ = Mathf.Clamp(hitPoint.z, environment.cargoBayRoot.position.z - 2f, environment.truckDropZone.position.z + 2f);
            return new Vector3(clampedX, currentY, clampedZ);
        }

        private float ResolveDragProgress(Vector3 currentPosition)
        {
            if (environment.cargoThrowOrigin == null || environment.truckDropZone == null)
            {
                return 0f;
            }

            var totalDistance = Mathf.Max(0.01f, Vector3.Distance(environment.cargoThrowOrigin.position, environment.truckDropZone.position));
            var currentDistance = Vector3.Distance(environment.cargoThrowOrigin.position, currentPosition);
            return Mathf.Clamp01(currentDistance / Mathf.Max(totalDistance, fragileDragDistance));
        }

        private static Color ResolveCargoColor(LoadingDockCargoRuntimeState cargo)
        {
            if (cargo.deliveryState == LoadingDockCargoDeliveryState.Delivered)
            {
                return new Color(0.3f, 0.9f, 0.5f);
            }

            return cargo.interactionType switch
            {
                LoadingDockCargoInteractionType.FragileDrag => new Color(0.4f, 0.85f, 1f),
                LoadingDockCargoInteractionType.HeavyClick => new Color(0.72f, 0.72f, 0.72f),
                _ => new Color(1f, 0.6f, 0.2f)
            };
        }

        private static string BuildCargoSummary(LoadingDockCargoRuntimeState cargo)
        {
            return cargo.interactionType switch
            {
                LoadingDockCargoInteractionType.HeavyClick => $"{cargo.displayName}: 클릭 {cargo.remainingClicks}회 남음",
                LoadingDockCargoInteractionType.FragileDrag => $"{cargo.displayName}: 드래그 {cargo.dragProgressNormalized * 100f:0}% / 상태 {cargo.deliveryState}",
                _ => $"{cargo.displayName}: 상태 {cargo.deliveryState}"
            };
        }

        private void ClearCargoViews()
        {
            foreach (var cargoView in _cargoViews.Values)
            {
                if (cargoView != null)
                {
                    Destroy(cargoView);
                }
            }

            _cargoViews.Clear();
            _cargoIdsByView.Clear();
            _homePositions.Clear();
            _cargoFlights.Clear();
            _completedCargoFlights.Clear();
            _completionQueued = false;
            _completionCountdown = 0f;
            _runtimeState = null;
        }

        private void RefreshCargoFlight(string cargoId, GameObject cargoView, Vector3 deliveryTarget)
        {
            if (_completedCargoFlights.Contains(cargoId))
            {
                cargoView.transform.position = deliveryTarget;
                return;
            }

            if (!_cargoFlights.TryGetValue(cargoId, out var flight))
            {
                flight = new LoadingDockCargoVisualFlight
                {
                    startPosition = cargoView.transform.position,
                    targetPosition = deliveryTarget,
                    elapsedTime = 0f
                };
                _cargoFlights[cargoId] = flight;
            }

            flight.targetPosition = deliveryTarget;
            flight.elapsedTime += Mathf.Max(Time.deltaTime, 1f / 60f);
            var duration = Mathf.Max(0.05f, cargoFlightDuration);
            var normalizedTime = Mathf.Clamp01(flight.elapsedTime / duration);
            cargoView.transform.position = LoadingDockCargoArcMotion.Evaluate(
                flight.startPosition,
                flight.targetPosition,
                cargoArcHeight,
                normalizedTime);

            if (normalizedTime >= 1f)
            {
                cargoView.transform.position = flight.targetPosition;
                _cargoFlights.Remove(cargoId);
                _completedCargoFlights.Add(cargoId);
            }
        }

        private sealed class LoadingDockCargoVisualFlight
        {
            public Vector3 startPosition;
            public Vector3 targetPosition;
            public float elapsedTime;
        }

        private void TryQueueRoundCompletion()
        {
            if (_runtimeState == null)
            {
                return;
            }

            if (!_completionQueued)
            {
                if (!LoadingDockMiniGameRuntime.TryCreateCompletionResult(
                        _runtimeState,
                        _cargoFlights.Count,
                        out var completionResult))
                {
                    return;
                }

                PrototypeSessionRuntime.StoreLoadingDockResult(completionResult);
                _completionQueued = true;
                _completionCountdown = completionReturnDelay;
            }

            if (_completionCountdown > 0f)
            {
                _completionCountdown -= Mathf.Max(Time.deltaTime, 1f / 60f);
                return;
            }

            PrototypeSessionRuntime.TryRequestLoadingDockReturn();
        }
    }
}
