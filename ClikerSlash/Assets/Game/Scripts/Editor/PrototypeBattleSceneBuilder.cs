using System.Collections.Generic;
using ClikerSlash.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClikerSlash.Editor
{
    public static class PrototypeBattleSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeBattle.unity";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/PlayerCube.prefab";
        private const string EnemyPrefabPath = "Assets/Game/Prefabs/EnemyCube.prefab";
        private const string PlayerMaterialPath = "Assets/Game/Materials/PlayerCube.mat";
        private const string EnemyMaterialPath = "Assets/Game/Materials/EnemyCube.mat";
        private const string LaneMaterialPath = "Assets/Game/Materials/LaneStrip.mat";
        private const string AccentMaterialPath = "Assets/Game/Materials/LineAccent.mat";

        [MenuItem("Tools/ClikerSlash/Build Prototype Battle Scene")]
        public static void BuildPrototypeBattleScene()
        {
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

            CreateCamera();
            CreateLight();
            var laneRoot = CreateLaneVisualRoot(laneMaterial, accentMaterial);
            CreateConfigRoots(laneRoot);
            CreatePresentationRoot(playerPrefab, enemyPrefab);
            CreateHudRoot();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prototype battle scene generated at {ScenePath}");
        }

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

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.92f);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

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

        private static void CreateHudRoot()
        {
            var hudRoot = new GameObject("HUDRoot");
            hudRoot.AddComponent<BattleHudPresenter>();
        }

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

        private static void ApplyMaterial(GameObject target, Material material)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

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
