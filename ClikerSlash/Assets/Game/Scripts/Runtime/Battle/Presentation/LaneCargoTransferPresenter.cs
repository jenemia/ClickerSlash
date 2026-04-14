using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 팔레트에서 레일 진입점까지 상자를 옮기는 handoff 연출을 담당합니다.
    /// </summary>
    public sealed class LaneCargoTransferPresenter : MonoBehaviour
    {
        public const float DefaultTransferDurationSeconds = 0.42f;

        private readonly Dictionary<Entity, TransferVisualState> _transferStateByEntity = new();
        private LoadingDockEnvironmentAuthoring _environment;
        private BattlePresentationBridge _bridge;
        private LaneCargoPalletStackPresenter _stackPresenter;
        private BattleViewAuthoring _battleView;
        private World _cachedWorld;
        private EntityQuery _cargoQuery;

        /// <summary>
        /// authoritative 환경과 handoff 연출에 필요한 프레젠터 참조를 함께 바인딩합니다.
        /// </summary>
        public void BindSceneReferences(
            LoadingDockEnvironmentAuthoring targetEnvironment,
            BattlePresentationBridge targetBridge,
            LaneCargoPalletStackPresenter targetStackPresenter,
            BattleViewAuthoring targetBattleView)
        {
            _environment = targetEnvironment;
            _bridge = targetBridge;
            _stackPresenter = targetStackPresenter;
            _battleView = targetBattleView;
        }

        /// <summary>
        /// reveal delay가 걸린 cargo entity를 transient 박스로 추적해 팔레트에서 레일까지 보간합니다.
        /// </summary>
        private void Update()
        {
            if (_environment == null || _bridge == null || _stackPresenter == null || _battleView == null)
            {
                return;
            }

            if (!TryPrepareQuery(out var entityManager))
            {
                CleanupTransfers();
                return;
            }

            using var entities = _cargoQuery.ToEntityArray(Allocator.Temp);
            using var kinds = _cargoQuery.ToComponentDataArray<CargoKind>(Allocator.Temp);
            using var variants = _cargoQuery.ToComponentDataArray<CargoPrefabVariant>(Allocator.Temp);
            using var revealDelays = _cargoQuery.ToComponentDataArray<CargoRevealDelay>(Allocator.Temp);
            using var spawnSequenceIds = _cargoQuery.ToComponentDataArray<CargoSpawnSequenceId>(Allocator.Temp);
            using var laneIndices = _cargoQuery.ToComponentDataArray<LaneIndex>(Allocator.Temp);

            var alive = new HashSet<Entity>();
            for (var index = 0; index < entities.Length; index += 1)
            {
                var entity = entities[index];
                alive.Add(entity);
                var revealDelay = revealDelays[index];
                if (revealDelay.TotalSeconds <= 0f || revealDelay.RemainingSeconds <= 0f)
                {
                    // reveal이 끝난 뒤에도 transient 박스가 남지 않도록 즉시 정리합니다.
                    ReleaseTransfer(entity);
                    continue;
                }

                if (!_transferStateByEntity.TryGetValue(entity, out var transferState))
                {
                    var profile = _environment.GetLaneCargoPrefabProfile();
                    var prefab = profile != null
                        ? profile.ResolvePrefab(kinds[index].Value, variants[index].Value)
                        : CargoTypePrefabProfile.ResolveDefaultPrefab(kinds[index].Value, variants[index].Value);
                    if (prefab == null)
                    {
                        continue;
                    }

                    // 팔레트에서 대응되는 박스를 숨기고, 같은 prefab으로 이동용 transient 오브젝트를 만듭니다.
                    _stackPresenter.TryHideStackEntry(spawnSequenceIds[index].Value, out var startPosition);
                    var transientParent = _environment.transientCargoRoot != null
                        ? _environment.transientCargoRoot
                        : _environment.transform;
                    var transientObject = Instantiate(prefab, transientParent);
                    transientObject.name = $"LaneCargoTransfer_{spawnSequenceIds[index].Value}";
                    transferState = new TransferVisualState(
                        transientObject,
                        startPosition);
                    _transferStateByEntity[entity] = transferState;
                }

                var progress = 1f - (revealDelay.RemainingSeconds / Mathf.Max(0.0001f, revealDelay.TotalSeconds));
                var currentEndPosition = ResolveLaneEntryPosition(laneIndices[index].Value, kinds[index].Value);
                transferState.GameObject.transform.position = LoadingDockCargoArcMotion.Evaluate(
                    transferState.StartPosition,
                    currentEndPosition,
                    0.9f,
                    progress);
            }

            CleanupStaleTransfers(alive);
        }

        /// <summary>
        /// 현재 기본 World에서 cargo 추적용 EntityQuery를 준비하고 캐시합니다.
        /// </summary>
        private bool TryPrepareQuery(out EntityManager entityManager)
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
            _cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoKind>(),
                ComponentType.ReadOnly<CargoPrefabVariant>(),
                ComponentType.ReadOnly<CargoRevealDelay>(),
                ComponentType.ReadOnly<CargoSpawnSequenceId>(),
                ComponentType.ReadOnly<LaneIndex>());
            return true;
        }

        /// <summary>
        /// Env의 레인 진입 앵커를 그대로 사용해 pallet->lane handoff 도착점을 계산합니다.
        /// </summary>
        private Vector3 ResolveLaneEntryPosition(int laneIndex, LoadingDockCargoKind kind)
        {
            return _environment.laneEntryAnchors[laneIndex].position + _bridge.GetLaneCargoOffset(kind);
        }

        /// <summary>
        /// 이번 프레임에 더 이상 살아 있지 않은 entity의 transient 박스를 정리합니다.
        /// </summary>
        private void CleanupStaleTransfers(HashSet<Entity> aliveEntities)
        {
            var staleEntities = new List<Entity>();
            foreach (var pair in _transferStateByEntity)
            {
                if (aliveEntities.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value.GameObject != null)
                {
                    Destroy(pair.Value.GameObject);
                }

                staleEntities.Add(pair.Key);
            }

            foreach (var entity in staleEntities)
            {
                _transferStateByEntity.Remove(entity);
            }
        }

        /// <summary>
        /// 현재 관리 중인 모든 transient 박스를 해제합니다.
        /// </summary>
        private void CleanupTransfers()
        {
            var entities = new List<Entity>(_transferStateByEntity.Keys);
            foreach (var entity in entities)
            {
                ReleaseTransfer(entity);
            }
        }

        /// <summary>
        /// 프레젠터 비활성화 시 이동 중이던 transient 박스를 모두 정리합니다.
        /// </summary>
        private void OnDisable()
        {
            CleanupTransfers();
        }

        /// <summary>
        /// 특정 entity에 대응하는 transient 박스를 제거하고 추적 테이블에서도 뺍니다.
        /// </summary>
        private void ReleaseTransfer(Entity entity)
        {
            if (!_transferStateByEntity.TryGetValue(entity, out var transferState))
            {
                return;
            }

            if (transferState.GameObject != null)
            {
                Destroy(transferState.GameObject);
            }

            _transferStateByEntity.Remove(entity);
        }

        private readonly struct TransferVisualState
        {
            /// <summary>
            /// transient 박스와 시작 위치를 묶어 현재 handoff 진행 상태를 표현합니다.
            /// </summary>
            public TransferVisualState(GameObject gameObject, Vector3 startPosition)
            {
                GameObject = gameObject;
                StartPosition = startPosition;
            }

            public GameObject GameObject { get; }
            public Vector3 StartPosition { get; }
        }
    }
}
