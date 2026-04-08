using System.Collections.Generic;
using ClikerSlash.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ClikerSlash.Editor
{
    /// <summary>
    /// 한 번의 메뉴 실행으로 프로토타입 물류 씬과 허브 씬을 다시 생성하는 빌더입니다.
    /// </summary>
    public static class PrototypeBattleSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeBattle.unity";
        private const string HubScenePath = "Assets/Game/Scenes/PrototypeHub.unity";
        private const string WorkerPrefabPath = "Assets/Game/Prefabs/PlayerCube.prefab";
        private const string CargoPrefabPath = "Assets/Game/Prefabs/CargoCube.prefab";
        private const string WorkerMaterialPath = "Assets/Game/Materials/PlayerCube.mat";
        private const string CargoMaterialPath = "Assets/Game/Materials/CargoCube.mat";
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

            var workerMaterial = GetOrCreateMaterial(WorkerMaterialPath, new Color(0.20f, 0.90f, 1.00f));
            var cargoMaterial = GetOrCreateMaterial(CargoMaterialPath, new Color(1.00f, 0.55f, 0.20f));
            var laneMaterial = GetOrCreateMaterial(LaneMaterialPath, new Color(0.10f, 0.14f, 0.22f));
            var accentMaterial = GetOrCreateMaterial(AccentMaterialPath, new Color(0.95f, 0.75f, 0.10f));

            var workerPrefab = GetOrCreateCubePrefab(WorkerPrefabPath, workerMaterial, new Vector3(1.1f, 1.1f, 1.1f));
            var cargoPrefab = GetOrCreateCubePrefab(CargoPrefabPath, cargoMaterial, new Vector3(0.9f, 0.9f, 0.9f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PrototypeBattle";

            var battleView = CreateBattleViewRoot();
            CreateCamera(battleView);
            CreateLight();
            var laneRoot = CreateLaneVisualRoot(battleView, laneMaterial, accentMaterial);
            CreateConfigRoots(battleView, laneRoot);
            CreatePresentationRoot(workerPrefab, cargoPrefab);
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
            Debug.Log($"Prototype logistics scene generated at {ScenePath}");
        }

        private static BattleViewAuthoring CreateBattleViewRoot()
        {
            var battleViewRoot = new GameObject("BattleView");
            var battleView = battleViewRoot.AddComponent<BattleViewAuthoring>();
            battleView.CameraPosition = new Vector3(0f, 10.6f, -16.8f);
            battleView.CameraRotation = new Vector3(31f, 0f, 0f);
            battleView.CameraFieldOfView = 34f;
            battleView.LaneWorldXs = new List<float> { -6f, -2f, 2f, 6f };
            battleView.LaneWidth = 3f;
            battleView.LaneLength = 15f;
            battleView.LaneCenterZ = 3f;
            battleView.LineVisualWidth = 16f;
            battleView.CargoSpawnZ = 10.5f;
            battleView.JudgmentLineZ = -2.8f;
            battleView.FailLineZ = -3.8f;
            battleView.PlayerZ = -3f;
            return battleView;
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

        private static void CreateCamera(BattleViewAuthoring battleView)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.08f);
            camera.fieldOfView = battleView.CameraFieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            cameraObject.transform.position = battleView.CameraPosition;
            cameraObject.transform.rotation = Quaternion.Euler(battleView.CameraRotation);
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

        private static GameObject CreateLaneVisualRoot(BattleViewAuthoring battleView, Material laneMaterial, Material accentMaterial)
        {
            var root = new GameObject("LaneVisualRoot");
            var laneAuthoring = root.AddComponent<LaneLayoutAuthoring>();
            laneAuthoring.LaneWorldXs = new List<float>(battleView.LaneWorldXs);

            for (var i = 0; i < laneAuthoring.LaneWorldXs.Count; i++)
            {
                var laneStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                laneStrip.name = $"Lane_{i + 1}";
                laneStrip.transform.SetParent(root.transform);
                laneStrip.transform.position = new Vector3(laneAuthoring.LaneWorldXs[i], 0f, battleView.LaneCenterZ);
                laneStrip.transform.localScale = new Vector3(battleView.LaneWidth, 0.05f, battleView.LaneLength);
                ApplyMaterial(laneStrip, laneMaterial);
                Object.DestroyImmediate(laneStrip.GetComponent<Collider>());
            }

            CreateLineVisual(root.transform, accentMaterial, "CargoSpawnLine", new Vector3(0f, 0.07f, battleView.CargoSpawnZ), new Vector3(battleView.LineVisualWidth, 0.05f, 0.3f));
            CreateLineVisual(root.transform, accentMaterial, "JudgmentLine", new Vector3(0f, 0.07f, battleView.JudgmentLineZ), new Vector3(battleView.LineVisualWidth, 0.05f, 0.3f));
            CreateLineVisual(root.transform, accentMaterial, "FailLine", new Vector3(0f, 0.07f, battleView.FailLineZ), new Vector3(battleView.LineVisualWidth, 0.05f, 0.3f));
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

        private static void CreateConfigRoots(BattleViewAuthoring battleView, GameObject laneRoot)
        {
            var battleConfigRoot = new GameObject("BattleConfig");
            var battleConfig = battleConfigRoot.AddComponent<BattleConfigAuthoring>();
            battleConfigRoot.AddComponent<BattleSceneBootstrap>();
            battleConfig.BaseWorkDurationSeconds = PrototypeSessionRuntime.DefaultBaseWorkDurationSeconds;
            battleConfig.HealthDurationBonusSeconds = PrototypeSessionRuntime.DefaultHealthDurationBonusSeconds;
            battleConfig.PlayerMoveDuration = 0.22f;
            battleConfig.HandleDurationSeconds = 0.4f;
            battleConfig.SpawnInterval = 0.9f;
            battleConfig.CargoSpawnZ = battleView.CargoSpawnZ;
            battleConfig.JudgmentLineZ = battleView.JudgmentLineZ;
            battleConfig.FailLineZ = battleView.FailLineZ;
            battleConfig.HandleWindowHalfDepth = 0.45f;
            battleConfig.StartingMaxHandleWeight = 10;

            var playerRoot = new GameObject("WorkerSpawn");
            var playerAuthoring = playerRoot.AddComponent<PlayerAuthoring>();
            playerAuthoring.InitialLane = 1;
            playerAuthoring.Y = 0.6f;
            playerAuthoring.Z = battleView.PlayerZ;

            var cargoRoot = new GameObject("CargoPrototype");
            var cargoAuthoring = cargoRoot.AddComponent<CargoAuthoring>();
            cargoAuthoring.Weight = 6;
            cargoAuthoring.Reward = 60;
            cargoAuthoring.Penalty = 35;
            cargoAuthoring.Y = 0.6f;
            cargoAuthoring.MoveSpeed = 2.4f;

            laneRoot.transform.position = Vector3.zero;
        }

        private static void CreatePresentationRoot(GameObject workerPrefab, GameObject cargoPrefab)
        {
            var presentationRoot = new GameObject("BattlePresentationRoot");
            var bridge = presentationRoot.AddComponent<BattlePresentationBridge>();

            var workerField = typeof(BattlePresentationBridge).GetField("playerViewPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cargoField = typeof(BattlePresentationBridge).GetField("cargoViewPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            workerField?.SetValue(bridge, workerPrefab);
            cargoField?.SetValue(bridge, cargoPrefab);
            EditorUtility.SetDirty(bridge);
        }

        private static void CreateHudRoot()
        {
            var hudRoot = new GameObject("HUDRoot");
            var canvas = hudRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudRoot.AddComponent<GraphicRaycaster>();

            var scaler = hudRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var presenter = hudRoot.AddComponent<BattleHudPresenter>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var infoText = CreateHudText("InfoText", hudRoot.transform, font, 28, TextAnchor.UpperLeft);
            SetRect(infoText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -26f), new Vector2(360f, 160f));
            infoText.text = "Work 30.0s\nMoney 0\nCombo 0";

            var laneText = CreateHudText("LaneText", hudRoot.transform, font, 30, TextAnchor.UpperCenter);
            SetRect(laneText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(320f, 110f));
            laneText.text = "Lane 2 / 4\nMax Weight 10";

            var controlsText = CreateHudText("ControlsText", hudRoot.transform, font, 24, TextAnchor.LowerCenter);
            SetRect(controlsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(500f, 60f));
            controlsText.text = "Controls: A / D or Left / Right";

            var resultText = CreateHudText("ResultText", hudRoot.transform, font, 56, TextAnchor.MiddleCenter);
            SetRect(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(720f, 100f));
            resultText.fontStyle = FontStyle.Bold;
            resultText.color = new Color(1f, 0.85f, 0.25f);
            resultText.text = "SHIFT COMPLETE";
            resultText.gameObject.SetActive(false);

            presenter.Bind(infoText, laneText, resultText, controlsText);
        }

        private static Text CreateHudText(string name, Transform parent, Font font, int fontSize, TextAnchor anchor)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

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

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
