using System.Collections.Generic;
using ClikerSlash.Battle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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

        [MenuItem("Tools/ClikerSlash/Build Prototype Battle Scene")]
        public static void BuildPrototypeBattleScene()
        {
            var assetLocator = BattleAssetEditorLocator.Instance;

            EnsureFolder("Assets/Game");
            EnsureFolder("Assets/Game/Scenes");
            EnsureFolder(BattleAssetEditorLocator.RemoteRootPath);
            EnsureFolder(BattleAssetEditorLocator.PrefabDirectoryPath);
            EnsureFolder(BattleAssetEditorLocator.MaterialDirectoryPath);
            EnsureFolder("Assets/Game/UI");

            var workerMaterial = GetOrCreateMaterial(assetLocator, BattleAssetKeys.PlayerMaterial, new Color(0.20f, 0.90f, 1.00f));
            var cargoMaterial = GetOrCreateMaterial(assetLocator, BattleAssetKeys.CargoMaterial, new Color(1.00f, 0.55f, 0.20f));
            var laneMaterial = GetOrCreateMaterial(assetLocator, BattleAssetKeys.LaneMaterial, new Color(0.10f, 0.14f, 0.22f));
            var accentMaterial = GetOrCreateMaterial(assetLocator, BattleAssetKeys.AccentMaterial, new Color(0.95f, 0.75f, 0.10f));

            var workerPrefab = GetOrCreateCubePrefab(assetLocator, BattleAssetKeys.PlayerView, workerMaterial, new Vector3(1.1f, 1.1f, 1.1f));
            var cargoPrefab = GetOrCreateCubePrefab(assetLocator, BattleAssetKeys.CargoView, cargoMaterial, new Vector3(0.9f, 0.9f, 0.9f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PrototypeBattle";

            var battleView = CreateBattleViewRoot();
            CreateCamera(battleView);
            CreateLight();
            var laneRoot = CreateLaneVisualRoot(battleView, laneMaterial, accentMaterial);
            CreateLoadingDockEnvironment(battleView, laneMaterial, accentMaterial, cargoMaterial, workerMaterial);
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

        private static void CreateLoadingDockEnvironment(
            BattleViewAuthoring battleView,
            Material floorMaterial,
            Material accentMaterial,
            Material cargoMaterial,
            Material workerMaterial)
        {
            var root = new GameObject("LoadingDockEnvironmentRoot");
            root.transform.position = new Vector3(18f, 0f, battleView.LaneCenterZ + 1.5f);
            var authoring = root.AddComponent<LoadingDockEnvironmentAuthoring>();

            var dockFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dockFloor.name = "DockFloor";
            dockFloor.transform.SetParent(root.transform, false);
            dockFloor.transform.localPosition = Vector3.zero;
            dockFloor.transform.localScale = new Vector3(18f, 0.08f, 12f);
            ApplyMaterial(dockFloor, floorMaterial);
            Object.DestroyImmediate(dockFloor.GetComponent<Collider>());

            var divider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            divider.name = "ZoneDivider";
            divider.transform.SetParent(root.transform, false);
            divider.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            divider.transform.localScale = new Vector3(0.28f, 0.06f, 11f);
            ApplyMaterial(divider, accentMaterial);
            Object.DestroyImmediate(divider.GetComponent<Collider>());

            var cargoBayRoot = new GameObject("CargoBayRoot");
            cargoBayRoot.transform.SetParent(root.transform, false);
            cargoBayRoot.transform.localPosition = new Vector3(-5f, 0f, -1.8f);
            authoring.cargoBayRoot = cargoBayRoot.transform;

            var cargoPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargoPad.name = "CargoBayPad";
            cargoPad.transform.SetParent(cargoBayRoot.transform, false);
            cargoPad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            cargoPad.transform.localScale = new Vector3(7f, 0.1f, 7f);
            ApplyMaterial(cargoPad, accentMaterial);
            Object.DestroyImmediate(cargoPad.GetComponent<Collider>());

            for (var stackIndex = 0; stackIndex < 6; stackIndex += 1)
            {
                var stackCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stackCube.name = $"CargoStack_{stackIndex + 1}";
                stackCube.transform.SetParent(cargoBayRoot.transform, false);
                stackCube.transform.localPosition = new Vector3(-2f + (stackIndex % 3) * 2f, 0.55f + (stackIndex / 3) * 1.05f, -1.2f + (stackIndex % 2) * 2.4f);
                stackCube.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
                ApplyMaterial(stackCube, cargoMaterial);
                Object.DestroyImmediate(stackCube.GetComponent<Collider>());
            }

            var cargoThrowOrigin = new GameObject("CargoThrowOrigin");
            cargoThrowOrigin.transform.SetParent(cargoBayRoot.transform, false);
            cargoThrowOrigin.transform.localPosition = new Vector3(2.4f, 1f, 2.2f);
            authoring.cargoThrowOrigin = cargoThrowOrigin.transform;

            var truckBayRoot = new GameObject("TruckBayRoot");
            truckBayRoot.transform.SetParent(root.transform, false);
            truckBayRoot.transform.localPosition = new Vector3(5f, 0f, 1.8f);
            authoring.truckBayRoot = truckBayRoot.transform;

            var truckPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            truckPad.name = "TruckPad";
            truckPad.transform.SetParent(truckBayRoot.transform, false);
            truckPad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            truckPad.transform.localScale = new Vector3(7.5f, 0.1f, 7f);
            ApplyMaterial(truckPad, accentMaterial);
            Object.DestroyImmediate(truckPad.GetComponent<Collider>());

            var truckBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            truckBody.name = "TruckBody";
            truckBody.transform.SetParent(truckBayRoot.transform, false);
            truckBody.transform.localPosition = new Vector3(0f, 1.1f, 0.2f);
            truckBody.transform.localScale = new Vector3(5.5f, 2f, 3.2f);
            ApplyMaterial(truckBody, workerMaterial);
            Object.DestroyImmediate(truckBody.GetComponent<Collider>());

            var truckCab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            truckCab.name = "TruckCab";
            truckCab.transform.SetParent(truckBayRoot.transform, false);
            truckCab.transform.localPosition = new Vector3(2.5f, 0.95f, -1.1f);
            truckCab.transform.localScale = new Vector3(2f, 1.7f, 1.5f);
            ApplyMaterial(truckCab, workerMaterial);
            Object.DestroyImmediate(truckCab.GetComponent<Collider>());

            var truckDropZone = new GameObject("TruckDropZone");
            truckDropZone.transform.SetParent(truckBayRoot.transform, false);
            truckDropZone.transform.localPosition = new Vector3(-1.4f, 1.2f, 0.8f);
            authoring.truckDropZone = truckDropZone.transform;
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
            presentationRoot.AddComponent<LoadingDockMiniGamePresenter>();

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
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

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
            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var hubRoot = new GameObject("PrototypeHubRoot");
            var presenter = hubRoot.AddComponent<PrototypeHubPresenter>();

            var canvasRoot = new GameObject("PrototypeHubCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreatePanel("Background", canvasRoot.transform, uiSprite, new Color(0.05f, 0.07f, 0.09f, 1f));
            SetRect(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var viewport = CreatePanel("TreeViewport", canvasRoot.transform, uiSprite, new Color(0.08f, 0.11f, 0.10f, 0.92f));
            viewport.AddComponent<RectMask2D>();
            var viewportRect = viewport.GetComponent<RectTransform>();
            SetRect(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var treeContent = new GameObject("TreeContent", typeof(RectTransform));
            treeContent.transform.SetParent(viewport.transform, false);
            var treeContentRect = treeContent.GetComponent<RectTransform>();
            treeContentRect.anchorMin = new Vector2(0.5f, 0.5f);
            treeContentRect.anchorMax = new Vector2(0.5f, 0.5f);
            treeContentRect.pivot = new Vector2(0.5f, 0.5f);
            treeContentRect.anchoredPosition = Vector2.zero;
            treeContentRect.sizeDelta = new Vector2(3200f, 2200f);

            var panZoomController = viewport.AddComponent<PrototypeHubPanZoomController>();
            var treeView = viewport.AddComponent<PrototypeHubSkillTreeView>();
            treeView.Bind(viewportRect, treeContentRect, panZoomController);

            var overviewPanel = CreatePanel("OverviewPanel", canvasRoot.transform, uiSprite, new Color(0.11f, 0.14f, 0.15f, 0.9f));
            SetRect(overviewPanel.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(360f, 378f));
            var title = CreateWrapHudText("TitleLabel", overviewPanel.transform, 34, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -18f), new Vector2(-44f, 42f));
            title.fontStyle = FontStyles.Bold;

            var health = CreateWrapHudText("HealthLabel", overviewPanel.transform, 24, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(health.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -74f), new Vector2(-44f, 34f));
            var duration = CreateWrapHudText("DurationLabel", overviewPanel.transform, 24, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(duration.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -110f), new Vector2(-44f, 34f));
            var weight = CreateWrapHudText("WeightLabel", overviewPanel.transform, 24, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(weight.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -146f), new Vector2(-44f, 34f));
            var lane = CreateWrapHudText("LaneLabel", overviewPanel.transform, 24, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(lane.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -182f), new Vector2(-44f, 34f));
            var balance = CreateWrapHudText("BalanceLabel", overviewPanel.transform, 24, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(balance.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -218f), new Vector2(-44f, 34f));
            var earned = CreateWrapHudText("EarnedLabel", overviewPanel.transform, 24, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(earned.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -254f), new Vector2(-44f, 34f));
            var spent = CreateWrapHudText("SpentLabel", overviewPanel.transform, 24, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(spent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -290f), new Vector2(-44f, 34f));
            var controls = CreateWrapHudText("ControlsLabel", overviewPanel.transform, 20, TextAlignmentOptions.TopLeft, new Color(0.79f, 0.88f, 0.83f));
            SetRect(controls.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 18f), new Vector2(-44f, 54f));

            var selectionPanel = CreatePanel("SelectionPanel", canvasRoot.transform, uiSprite, new Color(0.11f, 0.13f, 0.14f, 0.92f));
            SetRect(selectionPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(360f, 294f));
            var selectionTitle = CreateWrapHudText("SelectionTitleLabel", selectionPanel.transform, 28, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(selectionTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -20f), new Vector2(-44f, 38f));
            selectionTitle.fontStyle = FontStyles.Bold;
            var selectionBody = CreateWrapHudText("SelectionBodyLabel", selectionPanel.transform, 20, TextAlignmentOptions.TopLeft, new Color(0.86f, 0.92f, 0.86f));
            SetRect(selectionBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 20f), new Vector2(-44f, -70f));

            var resultPanel = CreatePanel("ResultPanel", canvasRoot.transform, uiSprite, new Color(0.13f, 0.13f, 0.11f, 0.92f));
            SetRect(resultPanel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(320f, 236f));
            var result = CreateWrapHudText("ResultLabel", resultPanel.transform, 22, TextAlignmentOptions.TopLeft, new Color(0.98f, 0.90f, 0.68f));
            SetRect(result.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 20f), new Vector2(-44f, -40f));

            var startButton = CreateButton("StartButton", canvasRoot.transform, uiSprite, "Start Prototype Shift");
            SetRect(startButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(320f, 68f));

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetSiblingIndex(canvasRoot.transform.GetSiblingIndex() + 1);

            presenter.Bind(
                treeView,
                title,
                health,
                duration,
                weight,
                lane,
                balance,
                earned,
                spent,
                controls,
                result,
                selectionTitle,
                selectionBody,
                startButton.GetComponent<Button>());

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), HubScenePath);
        }

        private static GameObject CreatePanel(string name, Transform parent, Sprite sprite, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(WrapImage));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<WrapImage>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return panel;
        }

        private static WrapLabel CreateWrapHudText(
            string name,
            Transform parent,
            int fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(WrapLabel));
            labelObject.transform.SetParent(parent, false);
            var text = labelObject.GetComponent<WrapLabel>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            return labelObject.GetComponent<WrapLabel>();
        }

        private static GameObject CreateButton(
            string name,
            Transform parent,
            Sprite sprite,
            string labelText)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(WrapImage), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<WrapImage>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(0.24f, 0.46f, 0.31f, 0.98f);

            var label = CreateWrapHudText("Label", buttonObject.transform, 24, TextAlignmentOptions.Center, Color.white);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = labelText;
            label.fontStyle = FontStyles.Bold;
            return buttonObject;
        }

        private static Material GetOrCreateMaterial(BattleAssetEditorLocator assetLocator, string assetKey, Color color)
        {
            var assetPath = assetLocator.GetEditorAssetPath(assetKey);
            var material = assetLocator.LoadAsset<Material>(assetKey);
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

        private static GameObject GetOrCreateCubePrefab(
            BattleAssetEditorLocator assetLocator,
            string assetKey,
            Material material,
            Vector3 scale)
        {
            var assetPath = assetLocator.GetEditorAssetPath(assetKey);
            var prefab = assetLocator.LoadAsset<GameObject>(assetKey);
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
