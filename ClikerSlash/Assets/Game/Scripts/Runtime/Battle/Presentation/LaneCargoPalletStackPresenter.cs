using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 레인 스폰 계획 전체를 팔레트 그리드 스택으로 시각화하고, 소비된 상자를 숨깁니다.
    /// </summary>
    public sealed class LaneCargoPalletStackPresenter : MonoBehaviour
    {
        private const int GridColumns = 4;
        private const int GridRows = 4;
        private const float SmallCellSize = 0.82f;
        private const float SmallLayerHeight = 0.72f;

        private readonly Dictionary<int, GameObject> _stackObjectsBySequenceId = new();
        private LoadingDockEnvironmentAuthoring _environment;
        private Transform _stackRoot;
        private GameObject _palletInstance;
        // true면 현재 환경 기준 팔레트 적재 표현을 이미 한 번 구성한 상태입니다.
        private bool _isBuilt;

        /// <summary>
        /// authoritative 환경이 바뀌면 기존 적재 표현을 정리하고 새 환경을 다시 바라보게 합니다.
        /// </summary>
        public void BindEnvironment(LoadingDockEnvironmentAuthoring targetEnvironment)
        {
            if (_environment == targetEnvironment)
            {
                return;
            }

            ClearStack();
            _environment = targetEnvironment;
        }

        /// <summary>
        /// 레인으로 반출된 스폰 순번의 박스를 팔레트에서 숨기고 시작 위치를 돌려줍니다.
        /// </summary>
        public bool TryHideStackEntry(int sequenceId, out Vector3 worldPosition)
        {
            if (_stackObjectsBySequenceId.TryGetValue(sequenceId, out var stackObject) && stackObject != null)
            {
                worldPosition = stackObject.transform.position;
                stackObject.SetActive(false);
                _stackObjectsBySequenceId.Remove(sequenceId);
                return true;
            }

            worldPosition = _environment != null && _environment.palletStackAnchor != null
                ? _environment.palletStackAnchor.position
                : transform.position;
            return false;
        }

        /// <summary>
        /// 환경 앵커가 준비되면 해당 세션의 전체 스폰 계획을 한 번만 팔레트에 시각화합니다.
        /// </summary>
        private void Update()
        {
            if (_environment == null || _environment.palletStackAnchor == null || _isBuilt)
            {
                return;
            }

            BuildStack();
        }

        /// <summary>
        /// 프레젠터가 비활성화될 때 생성해 둔 팔레트 적재 오브젝트를 모두 정리합니다.
        /// </summary>
        private void OnDisable()
        {
            ClearStack();
        }

        /// <summary>
        /// 아직 소비되지 않은 전체 스폰 계획을 팔레트 위 grid stack으로 런타임 생성합니다.
        /// </summary>
        private void BuildStack()
        {
            var spawnPlan = PrototypeSessionRuntime.GetRemainingLaneCargoSpawnEntries();
            if (spawnPlan.Length == 0)
            {
                _isBuilt = true;
                return;
            }

            _stackRoot = new GameObject("LaneCargoPalletStackRoot").transform;
            _stackRoot.SetParent(_environment.palletStackAnchor, false);
            _stackRoot.localPosition = Vector3.zero;

            if (_environment.laneCargoPalletPrefab != null)
            {
                _palletInstance = Instantiate(_environment.laneCargoPalletPrefab, _environment.palletStackAnchor);
                _palletInstance.name = "LaneCargoPallet";
                _palletInstance.transform.localPosition = Vector3.zero;
            }

            var reversedEntries = new LaneCargoSpawnEntry[spawnPlan.Length];
            for (var index = 0; index < spawnPlan.Length; index += 1)
            {
                // 먼저 빠져야 할 상자가 위/바깥쪽에 오도록 역순으로 패킹합니다.
                reversedEntries[index] = spawnPlan[spawnPlan.Length - 1 - index];
            }

            var placements = BuildPlacements(reversedEntries);
            for (var index = 0; index < placements.Count; index += 1)
            {
                var placement = placements[index];
                var profile = _environment.GetLaneCargoPrefabProfile();
                var prefab = profile != null
                    ? profile.ResolvePrefab(placement.Entry.Kind, placement.Entry.PrefabVariantId)
                    : CargoTypePrefabProfile.ResolveDefaultPrefab(placement.Entry.Kind, placement.Entry.PrefabVariantId);
                if (prefab == null)
                {
                    continue;
                }

                var instance = Instantiate(prefab, _stackRoot);
                instance.name = $"LaneCargoStack_{placement.Entry.SpawnIndex}";
                instance.transform.localPosition = placement.LocalPosition;
                _stackObjectsBySequenceId[placement.Entry.SpawnIndex] = instance;
            }

            _isBuilt = true;
        }

        /// <summary>
        /// small=1x1, heavy=2x2 규칙으로 각 스폰 엔트리의 적재 위치를 계산합니다.
        /// </summary>
        private List<StackPlacement> BuildPlacements(IReadOnlyList<LaneCargoSpawnEntry> reversedEntries)
        {
            var placements = new List<StackPlacement>(reversedEntries.Count);
            var layers = new List<bool[,]>();
            for (var entryIndex = 0; entryIndex < reversedEntries.Count; entryIndex += 1)
            {
                var entry = reversedEntries[entryIndex];
                var footprintWidth = entry.Kind == LoadingDockCargoKind.Heavy ? 2 : 1;
                var footprintHeight = entry.Kind == LoadingDockCargoKind.Heavy ? 2 : 1;
                var placement = default(StackPlacement);
                var wasPlaced = false;

                for (var layerIndex = 0; layerIndex < layers.Count && !wasPlaced; layerIndex += 1)
                {
                    // 기존 레이어에 빈 공간이 있으면 위로 쌓기 전에 먼저 채워 밀도를 유지합니다.
                    if (!TryFindPlacement(layers[layerIndex], footprintWidth, footprintHeight, out var cellX, out var cellY))
                    {
                        continue;
                    }

                    Occupy(layers[layerIndex], cellX, cellY, footprintWidth, footprintHeight);
                    placement = new StackPlacement(entry, ResolveLocalPosition(layerIndex, cellX, cellY, footprintWidth, footprintHeight));
                    wasPlaced = true;
                }

                if (wasPlaced)
                {
                    placements.Add(placement);
                    continue;
                }

                var newLayer = new bool[GridColumns, GridRows];
                layers.Add(newLayer);
                TryFindPlacement(newLayer, footprintWidth, footprintHeight, out var newCellX, out var newCellY);
                Occupy(newLayer, newCellX, newCellY, footprintWidth, footprintHeight);
                placements.Add(new StackPlacement(
                    entry,
                    ResolveLocalPosition(layers.Count - 1, newCellX, newCellY, footprintWidth, footprintHeight)));
            }

            return placements;
        }

        /// <summary>
        /// grid cell 좌표와 레이어 인덱스를 실제 팔레트 로컬 좌표로 변환합니다.
        /// </summary>
        private Vector3 ResolveLocalPosition(int layerIndex, int cellX, int cellY, int footprintWidth, int footprintHeight)
        {
            var cellPitch = SmallCellSize + Mathf.Max(0f, _environment.stackGap);
            var totalWidth = GridColumns * cellPitch;
            var totalDepth = GridRows * cellPitch;
            var centerOffsetX = (-totalWidth * 0.5f) + (footprintWidth * cellPitch * 0.5f);
            var centerOffsetZ = (-totalDepth * 0.5f) + (footprintHeight * cellPitch * 0.5f);
            return new Vector3(
                centerOffsetX + (cellX * cellPitch),
                layerIndex * (SmallLayerHeight + Mathf.Max(0f, _environment.layerGap)),
                centerOffsetZ + (cellY * cellPitch));
        }

        /// <summary>
        /// 지정한 footprint가 현재 레이어의 어느 cell에 들어갈 수 있는지 탐색합니다.
        /// </summary>
        private static bool TryFindPlacement(bool[,] layer, int footprintWidth, int footprintHeight, out int cellX, out int cellY)
        {
            for (var row = 0; row <= GridRows - footprintHeight; row += 1)
            {
                for (var column = 0; column <= GridColumns - footprintWidth; column += 1)
                {
                    var isFree = true;
                    for (var x = 0; x < footprintWidth && isFree; x += 1)
                    {
                        for (var y = 0; y < footprintHeight; y += 1)
                        {
                            if (!layer[column + x, row + y])
                            {
                                continue;
                            }

                            isFree = false;
                            break;
                        }
                    }

                    if (!isFree)
                    {
                        continue;
                    }

                    cellX = column;
                    cellY = row;
                    return true;
                }
            }

            cellX = 0;
            cellY = 0;
            return false;
        }

        /// <summary>
        /// 배치가 확정된 footprint 영역을 사용 중으로 표시합니다.
        /// </summary>
        private static void Occupy(bool[,] layer, int cellX, int cellY, int footprintWidth, int footprintHeight)
        {
            for (var x = 0; x < footprintWidth; x += 1)
            {
                for (var y = 0; y < footprintHeight; y += 1)
                {
                    layer[cellX + x, cellY + y] = true;
                }
            }
        }

        /// <summary>
        /// 현재 환경에 생성된 팔레트와 박스 인스턴스를 모두 제거하고 상태를 초기화합니다.
        /// </summary>
        private void ClearStack()
        {
            foreach (var pair in _stackObjectsBySequenceId)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            _stackObjectsBySequenceId.Clear();
            if (_palletInstance != null)
            {
                Destroy(_palletInstance);
                _palletInstance = null;
            }

            if (_stackRoot != null)
            {
                Destroy(_stackRoot.gameObject);
                _stackRoot = null;
            }

            _isBuilt = false;
        }

        private readonly struct StackPlacement
        {
            /// <summary>
            /// 스폰 엔트리와 그 엔트리가 배치될 로컬 좌표를 함께 묶습니다.
            /// </summary>
            public StackPlacement(LaneCargoSpawnEntry entry, Vector3 localPosition)
            {
                Entry = entry;
                LocalPosition = localPosition;
            }

            public LaneCargoSpawnEntry Entry { get; }
            public Vector3 LocalPosition { get; }
        }
    }
}
