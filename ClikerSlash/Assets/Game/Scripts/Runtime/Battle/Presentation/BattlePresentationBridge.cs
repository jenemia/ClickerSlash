using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// ECS의 플레이어와 적 위치를 프로토타입 씬의 단순 게임 오브젝트 뷰에 반영합니다.
    /// </summary>
    public sealed class BattlePresentationBridge : MonoBehaviour
    {
        [SerializeField] private GameObject playerViewPrefab;
        [SerializeField] private GameObject enemyViewPrefab;

        private World _cachedWorld;
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private GameObject _playerInstance;
        private readonly Dictionary<Entity, GameObject> _enemyInstances = new();

        /// <summary>
        /// 활성 ECS 월드 기준으로 매 프레임 뷰 오브젝트를 갱신합니다.
        /// </summary>
        private void Update()
        {
            if (!TryPrepareQueries(out var entityManager))
            {
                CleanupAllViews();
                return;
            }

            SyncPlayer(entityManager);
            SyncEnemies(entityManager);
        }

        /// <summary>
        /// 현재 월드용 ECS 쿼리를 캐시하고 프레젠테이션 동기화를 진행할 수 있는지 반환합니다.
        /// </summary>
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
            _enemyQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            return true;
        }

        /// <summary>
        /// 플레이어 엔티티를 따라가도록 단일 플레이어 뷰를 생성하거나 이동시킵니다.
        /// </summary>
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
                _playerInstance.name = "PlayerView";
            }

            _playerInstance.transform.position = transforms[0].Position;
        }

        /// <summary>
        /// 현재 적 엔티티 집합에 맞춰 적 뷰를 생성, 재사용, 제거합니다.
        /// </summary>
        private void SyncEnemies(EntityManager entityManager)
        {
            if (enemyViewPrefab == null)
            {
                return;
            }

            using var entities = _enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var alive = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++)
            {
                var enemyEntity = entities[i];
                alive.Add(enemyEntity);

                if (!_enemyInstances.TryGetValue(enemyEntity, out var enemyView) || enemyView == null)
                {
                    enemyView = Instantiate(enemyViewPrefab, transform);
                    enemyView.name = $"EnemyView_{enemyEntity.Index}";
                    _enemyInstances[enemyEntity] = enemyView;
                }

                enemyView.transform.position = transforms[i].Position;
            }

            if (_enemyInstances.Count == 0)
            {
                return;
            }

            // 이번 프레임에 제거된 적 엔티티에 대응하는 뷰도 같이 정리합니다.
            var staleEntities = ListPool<Entity>.Get();
            foreach (var pair in _enemyInstances)
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
                _enemyInstances.Remove(staleEntity);
            }
            ListPool<Entity>.Release(staleEntities);
        }

        /// <summary>
        /// ECS 월드를 쓸 수 없을 때 생성해 둔 모든 프레젠테이션 오브젝트를 제거합니다.
        /// </summary>
        private void CleanupAllViews()
        {
            if (_playerInstance != null)
            {
                Destroy(_playerInstance);
                _playerInstance = null;
            }

            foreach (var pair in _enemyInstances)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }
            _enemyInstances.Clear();
        }

        /// <summary>
        /// 이 프레젠터가 비활성화될 때 풀링된 뷰가 남지 않도록 정리합니다.
        /// </summary>
        private void OnDisable()
        {
            CleanupAllViews();
        }

        /// <summary>
        /// 매 프레임 오래된 엔티티 목록을 새로 할당하지 않기 위한 작은 리스트 풀입니다.
        /// </summary>
        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            /// <summary>
            /// 풀에서 빈 리스트를 가져오고, 없으면 새로 생성합니다.
            /// </summary>
            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            /// <summary>
            /// 리스트를 비운 뒤 재사용할 수 있도록 풀에 반환합니다.
            /// </summary>
            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
