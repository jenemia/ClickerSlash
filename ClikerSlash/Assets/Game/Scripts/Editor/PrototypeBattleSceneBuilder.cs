using System.Collections.Generic;
using ClikerSlash.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClikerSlash.Editor
{
    /// <summary>
    /// 한 번의 메뉴 실행으로 프로토타입 전투 씬과 허브 씬을 다시 생성하는 빌더입니다.
    /// </summary>
    public static class PrototypeBattleSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeBattle.unity";
        private const string HubScenePath = "Assets/Game/Scenes/PrototypeHub.unity";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/PlayerCube.prefab";
        private const string EnemyPrefabPath = "Assets/Game/Prefabs/EnemyCube.prefab";
        private const string PlayerMaterialPath = "Assets/Game/Materials/PlayerCube.mat";
        private const string EnemyMaterialPath = "Assets/Game/Materials/EnemyCube.mat";
        private const string LaneMaterialPath = "Assets/Game/Materials/LaneStrip.mat";
        private const string AccentMaterialPath = "Assets/Game/Materials/LineAccent.mat";

        [MenuItem("Tools/ClikerSlash/Build Prototype Battle Scene")]
        public static void BuildPrototypeBattleScene()
        {
            // 생성 산출물이 항상 같은 프로젝트 구조에 배치되도록 필요한 폴더를 먼저 보장합니다.
            EnsureFolder("Assets/Game");
            EnsureFolder("Assets/Game/Scenes");
            EnsureFolder("Assets/Game/Prefabs");
            EnsureFolder("Assets/Game/Materials");
            EnsureFolder("Assets/Game/UI");

            var playerMaterial = GetOrCreateMaterial(PlayerMaterialPath, new Color(0.20f, 0.90f, 1.00f));
            var enemyMaterial = GetOrCreateMaterial(EnemyMaterialPath, new Color(1.00f, 0.35f, 0.35f));
            var laneMaterial = GetOrCreateMaterial(LaneMaterialPath, new Color(0.10f, 0.14f, 0.22f));
            var accentMaterial = GetOrCreateMaterial(AccentMaterialPath, new Color(0.95f, 0.75f, 0.10f));

            var playerPrefab = GetOrCreateCubePrefab(PlayerPrefabPath, playerMaterial, new Vector3(1.1f, 1.1f, 1.1f));
            var enemyPrefab = GetOrCreateCubePrefab(EnemyPrefabPath, enemyMaterial, new Vector3(0.9f, 0.9f, 0.9f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PrototypeBattle";

            // 반복 실행 시에도 결과가 흔들리지 않도록 전투 씬 전체를 처음부터 다시 만듭니다.
            CreateCamera();
            CreateLight();
            var laneRoot = CreateLaneVisualRoot(laneMaterial, accentMaterial);
            CreateConfigRoots(laneRoot);
            CreatePresentationRoot(playerPrefab, enemyPrefab);
            CreateHudRoot();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            CreateHubScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(HubScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prototype battle scene generated at {ScenePath}");
        }

        /// <summary>
        /// 전투 씬과 경량 허브 씬에서 공통으로 사용하는 고정 카메라를 만듭니다.
        /// </summary>
        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.08f);
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            cameraObject.transform.position = new Vector3(0f, 13f, -11.5f);
            cameraObject.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
        }

        /// <summary>
        /// 별도 라이팅 세팅 없이도 프로토타입이 읽히도록 단일 방향광을 추가합니다.
        /// </summary>
        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.92f);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        /// <summary>
        /// 스폰 지점과 방어선 경계를 읽을 수 있도록 레인 바닥과 상하 라인을 생성합니다.
        /// </summary>
        private static GameObject CreateLaneVisualRoot(Material laneMaterial, Material accentMaterial)
        {
            var root = new GameObject("LaneVisualRoot");
            var laneAuthoring = root.AddComponent<LaneLayoutAuthoring>();
            laneAuthoring.LaneWorldXs = new List<float> { -4.5f, -1.5f, 1.5f, 4.5f };

            for (var i = 0; i < laneAuthoring.LaneWorldXs.Count; i++)
            {
                var laneStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                laneStrip.name = $"Lane_{i + 1}";
                laneStrip.transform.SetParent(root.transform);
                laneStrip.transform.position = new Vector3(laneAuthoring.LaneWorldXs[i], 0f, 1.6f);
                laneStrip.transform.localScale = new Vector3(2.2f, 0.05f, 12f);
                ApplyMaterial(laneStrip, laneMaterial);
                Object.DestroyImmediate(laneStrip.GetComponent<Collider>());
            }

            CreateLineVisual(root.transform, accentMaterial, "SpawnLine", new Vector3(0f, 0.07f, 8.5f), new Vector3(11.5f, 0.05f, 0.3f));
            CreateLineVisual(root.transform, accentMaterial, "DefenseLine", new Vector3(0f, 0.07f, -3.5f), new Vector3(11.5f, 0.05f, 0.3f));
            return root;
        }

        /// <summary>
        /// 스폰선이나 방어선처럼 직선 경계를 표현하는 단순 큐브 기반 마커를 만듭니다.
        /// </summary>
        private static void CreateLineVisual(Transform parent, Material material, string name, Vector3 position, Vector3 scale)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.SetParent(parent);
            line.transform.position = position;
            line.transform.localScale = scale;
            ApplyMaterial(line, material);
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        /// <summary>
        /// ECS 부트스트랩이 읽을 초기 전투 설정용 씬 구성 오브젝트를 생성합니다.
        /// </summary>
        private static void CreateConfigRoots(GameObject laneRoot)
        {
            var battleConfigRoot = new GameObject("BattleConfig");
            var battleConfig = battleConfigRoot.AddComponent<BattleConfigAuthoring>();
            battleConfigRoot.AddComponent<BattleSceneBootstrap>();
            battleConfig.BattleDurationSeconds = 60f;
            battleConfig.StartingLives = 3;
            battleConfig.PlayerMoveDuration = 0.22f;
            battleConfig.AttackInterval = 0.4f;
            battleConfig.SpawnInterval = 0.9f;
            battleConfig.EnemySpawnZ = 8.5f;
            battleConfig.DefenseLineZ = -3.5f;

            var playerRoot = new GameObject("PlayerSpawn");
            var playerAuthoring = playerRoot.AddComponent<PlayerAuthoring>();
            playerAuthoring.InitialLane = 1;
            playerAuthoring.Y = 0.6f;
            playerAuthoring.Z = -2.4f;

            var enemyRoot = new GameObject("EnemyPrefabReference");
            var enemyAuthoring = enemyRoot.AddComponent<EnemyAuthoring>();
            enemyAuthoring.Health = 1;
            enemyAuthoring.Y = 0.6f;
            enemyAuthoring.MoveSpeed = 2.4f;

            laneRoot.transform.position = Vector3.zero;
        }

        /// <summary>
        /// 생성한 플레이어/적 프리팹을 프레젠테이션 브리지에 연결합니다.
        /// </summary>
        private static void CreatePresentationRoot(GameObject playerPrefab, GameObject enemyPrefab)
        {
            var presentationRoot = new GameObject("BattlePresentationRoot");
            var bridge = presentationRoot.AddComponent<BattlePresentationBridge>();

            var playerField = typeof(BattlePresentationBridge).GetField("playerViewPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enemyField = typeof(BattlePresentationBridge).GetField("enemyViewPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            playerField?.SetValue(bridge, playerPrefab);
            enemyField?.SetValue(bridge, enemyPrefab);
            EditorUtility.SetDirty(bridge);
        }

        /// <summary>
        /// 프로토타입 전투 씬에서 사용하는 IMGUI HUD 프레젠터를 추가합니다.
        /// </summary>
        private static void CreateHudRoot()
        {
            var hudRoot = new GameObject("HUDRoot");
            hudRoot.AddComponent<BattleHudPresenter>();
        }

        /// <summary>
        /// 마지막 전투 결과와 재진입 버튼만 보여주는 최소 허브 씬을 생성합니다.
        /// </summary>
        private static void CreateHubScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = PrototypeSessionRuntime.HubSceneName;

            CreateCamera();
            CreateLight();

            var hubRoot = new GameObject("PrototypeHubRoot");
            hubRoot.AddComponent<PrototypeHubPresenter>();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), HubScenePath);
        }

        /// <summary>
        /// 기존 머티리얼이 있으면 재사용하고, 없으면 요청한 기본 색으로 새로 만듭니다.
        /// </summary>
        private static Material GetOrCreateMaterial(string assetPath, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                material.color = color;
                EditorUtility.SetDirty(material);
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader)
            {
                color = color
            };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        /// <summary>
        /// 기존 큐브 프리팹이 있으면 반환하고, 없으면 프로토타입 액터 비주얼용으로 생성합니다.
        /// </summary>
        private static GameObject GetOrCreateCubePrefab(string assetPath, Material material, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                return prefab;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            cube.transform.localScale = scale;
            ApplyMaterial(cube, material);
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            prefab = PrefabUtility.SaveAsPrefabAsset(cube, assetPath);
            Object.DestroyImmediate(cube);
            return prefab;
        }

        /// <summary>
        /// 생성한 프리미티브에 렌더러가 있으면 공용 머티리얼을 적용합니다.
        /// </summary>
        private static void ApplyMaterial(GameObject target, Material material)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// 생성한 씬이나 프리팹을 저장하기 전에 대상 에셋 폴더가 존재하도록 보장합니다.
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parentPath = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            var folderName = System.IO.Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath))
            {
                EnsureFolder(parentPath);
            }

            AssetDatabase.CreateFolder(parentPath ?? "Assets", folderName);
        }
    }
}
