using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 허브 스킬트리의 노드, 연결선, 브랜치 라벨을 런타임에 생성하고 갱신합니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private PrototypeHubPanZoomController panZoomController;

        private readonly Dictionary<string, PrototypeHubSkillTreeNodeView> _nodeViews = new Dictionary<string, PrototypeHubSkillTreeNodeView>();
        private readonly List<PrototypeHubSkillTreeEdgeRecord> _edgeViews = new List<PrototypeHubSkillTreeEdgeRecord>();
        private readonly List<GameObject> _runtimeObjects = new List<GameObject>();

        private Sprite _panelSprite;
        private PrototypeHubSkillTreeLayout _layout;

        /// <summary>
        /// 노드가 눌렸을 때 프레젠터가 선택/업그레이드 로직을 처리할 수 있도록 이벤트를 전달합니다.
        /// </summary>
        public event Action<string> NodeClicked;

        /// <summary>
        /// 뷰포트, 콘텐츠 루트, 팬/줌 컨트롤러를 한 번에 연결합니다.
        /// </summary>
        public void Bind(
            RectTransform viewportTransform,
            RectTransform contentTransform,
            PrototypeHubPanZoomController controller)
        {
            viewport = viewportTransform;
            contentRoot = contentTransform;
            panZoomController = controller;
        }

        /// <summary>
        /// 카탈로그 구조를 기준으로 노드와 연결선을 처음 생성합니다.
        /// </summary>
        public void Build(MetaProgressionCatalogAsset catalog)
        {
            EnsureResources();
            ClearRuntimeObjects();

            _layout = PrototypeHubSkillTreeLayoutBuilder.Build(catalog);
            contentRoot.sizeDelta = _layout.contentSize;

            CreateHubNode();

            foreach (var branchLayout in _layout.branches)
            {
                CreateBranch(branchLayout);
            }

            panZoomController.Bind(viewport, contentRoot);
            panZoomController.FrameContent();
        }

        /// <summary>
        /// 현재 메타 상태와 선택 노드를 반영해 색상과 텍스트를 갱신합니다.
        /// </summary>
        public void Refresh(
            PlayerMetaProgressionSnapshot snapshot,
            MetaProgressionCatalogAsset catalog,
            string selectedNodeId)
        {
            if (_layout == null)
            {
                Build(catalog);
            }

            var statuses = MetaProgressionCalculator.DescribeAllNodes(snapshot, catalog);
            var statusByNodeId = new Dictionary<string, MetaProgressionNodeStatus>(statuses.Count);
            foreach (var status in statuses)
            {
                statusByNodeId[status.nodeId] = status;
                if (!_nodeViews.TryGetValue(status.nodeId, out var nodeView))
                {
                    continue;
                }

                var viewModel = PrototypeHubSkillTreeLayoutBuilder.BuildNodeViewModel(
                    status,
                    string.Equals(status.nodeId, selectedNodeId, StringComparison.Ordinal));
                nodeView.Bind(viewModel, HandleNodeClicked);
            }

            foreach (var edgeRecord in _edgeViews)
            {
                if (!statusByNodeId.TryGetValue(edgeRecord.childNodeId, out var childStatus))
                {
                    continue;
                }

                edgeRecord.edgeView.SetColor(ResolveConnectorColor(childStatus));
            }
        }

        private void HandleNodeClicked(string nodeId)
        {
            NodeClicked?.Invoke(nodeId);
        }

        private void CreateHubNode()
        {
            var hubNode = CreateNodeView(contentRoot, "CenterHubNode");
            hubNode.RectTransform.anchoredPosition = Vector2.zero;
            hubNode.BindHub("HUB");
            _runtimeObjects.Add(hubNode.gameObject);
        }

        private void CreateBranch(PrototypeHubSkillTreeBranchLayout branchLayout)
        {
            var branchRoot = CreateRectTransform("Branch_" + branchLayout.displayName, contentRoot, Vector2.zero);
            branchRoot.anchorMin = new Vector2(0.5f, 0.5f);
            branchRoot.anchorMax = new Vector2(0.5f, 0.5f);
            branchRoot.pivot = new Vector2(0.5f, 0.5f);
            branchRoot.anchoredPosition = Vector2.zero;
            branchRoot.localRotation = Quaternion.Euler(0f, 0f, branchLayout.angleDegrees);
            _runtimeObjects.Add(branchRoot.gameObject);

            var edgeRoot = CreateRectTransform("Edges", branchRoot, Vector2.zero);
            edgeRoot.anchorMin = new Vector2(0.5f, 0.5f);
            edgeRoot.anchorMax = new Vector2(0.5f, 0.5f);
            edgeRoot.pivot = new Vector2(0.5f, 0.5f);
            edgeRoot.anchoredPosition = Vector2.zero;

            var nodeRoot = CreateRectTransform("Nodes", branchRoot, Vector2.zero);
            nodeRoot.anchorMin = new Vector2(0.5f, 0.5f);
            nodeRoot.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRoot.pivot = new Vector2(0.5f, 0.5f);
            nodeRoot.anchoredPosition = Vector2.zero;

            foreach (var edgeLayout in branchLayout.edges)
            {
                var edgeView = CreateEdgeView(edgeRoot, edgeLayout);
                _edgeViews.Add(new PrototypeHubSkillTreeEdgeRecord
                {
                    childNodeId = edgeLayout.childNodeId,
                    edgeView = edgeView
                });
            }

            foreach (var nodeLayout in branchLayout.nodes)
            {
                var nodeView = CreateNodeView(nodeRoot, nodeLayout.nodeId);
                nodeView.RectTransform.anchoredPosition = nodeLayout.localPosition;
                _nodeViews[nodeLayout.nodeId] = nodeView;
                _runtimeObjects.Add(nodeView.gameObject);
            }

            var label = CreateLabel(contentRoot, "BranchLabel_" + branchLayout.displayName, new Vector2(260f, 56f));
            label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var rotatedTitlePosition = Quaternion.Euler(0f, 0f, branchLayout.angleDegrees) *
                                       new Vector3(branchLayout.titlePosition.x, branchLayout.titlePosition.y, 0f);
            label.rectTransform.anchoredPosition = new Vector2(rotatedTitlePosition.x, rotatedTitlePosition.y);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 34;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.89f, 0.93f, 0.84f, 0.95f);
            label.text = branchLayout.displayName;
            _runtimeObjects.Add(label.gameObject);
        }

        private PrototypeHubSkillTreeNodeView CreateNodeView(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(160f, 168f);

            var nodeView = root.AddComponent<PrototypeHubSkillTreeNodeView>();

            var buttonObject = new GameObject("ButtonRoot", typeof(RectTransform), typeof(WrapImage), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -46f);
            buttonRect.sizeDelta = new Vector2(100f, 100f);

            var buttonImage = buttonObject.GetComponent<WrapImage>();
            buttonImage.sprite = _panelSprite;
            buttonImage.type = _panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            buttonImage.raycastTarget = true;

            var ringObject = new GameObject("Ring", typeof(RectTransform), typeof(WrapImage));
            ringObject.transform.SetParent(buttonObject.transform, false);
            var ringRect = ringObject.GetComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.pivot = new Vector2(0.5f, 0.5f);
            ringRect.anchoredPosition = Vector2.zero;
            ringRect.sizeDelta = new Vector2(112f, 112f);
            var ringImage = ringObject.GetComponent<WrapImage>();
            ringImage.sprite = _panelSprite;
            ringImage.type = _panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            ringImage.raycastTarget = false;

            var glyph = CreateLabel(buttonObject.transform, "Glyph", new Vector2(70f, 70f));
            glyph.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.anchoredPosition = Vector2.zero;
            glyph.fontSize = 36;
            glyph.fontStyle = FontStyles.Bold;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.color = Color.white;
            var title = CreateLabel(root.transform, "Title", new Vector2(150f, 34f));
            title.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            title.rectTransform.pivot = new Vector2(0.5f, 0f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            title.fontSize = 18;
            title.alignment = TextAlignmentOptions.TopGeoAligned;
            title.color = new Color(0.96f, 0.97f, 0.92f, 0.98f);
            var level = CreateLabel(root.transform, "Level", new Vector2(140f, 28f));
            level.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            level.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            level.rectTransform.pivot = new Vector2(0.5f, 0f);
            level.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            level.fontSize = 16;
            level.alignment = TextAlignmentOptions.TopGeoAligned;
            level.color = new Color(0.82f, 0.88f, 0.82f, 0.95f);
            var cost = CreateLabel(root.transform, "Cost", new Vector2(140f, 24f));
            cost.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            cost.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            cost.rectTransform.pivot = new Vector2(0.5f, 0f);
            cost.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            cost.fontSize = 14;
            cost.alignment = TextAlignmentOptions.TopGeoAligned;
            cost.color = new Color(0.71f, 0.80f, 0.72f, 0.92f);
            nodeView.BindReferences(
                rootRect,
                buttonObject.GetComponent<Button>(),
                buttonObject.GetComponent<WrapImage>(),
                ringObject.GetComponent<WrapImage>(),
                glyph,
                title,
                level,
                cost);
            return nodeView;
        }

        private PrototypeHubSkillTreeEdgeView CreateEdgeView(Transform parent, PrototypeHubSkillTreeEdgeLayout edgeLayout)
        {
            var edgeObject = new GameObject("Edge_" + edgeLayout.childNodeId, typeof(RectTransform));
            edgeObject.transform.SetParent(parent, false);
            var edgeView = edgeObject.AddComponent<PrototypeHubSkillTreeEdgeView>();

            var horizontal = CreateSegment(edgeObject.transform, "Horizontal");
            var vertical = CreateSegment(edgeObject.transform, "Vertical");
            edgeView.Bind(horizontal.GetComponent<WrapImage>(), vertical.GetComponent<WrapImage>());
            edgeView.SetPath(edgeLayout.from, edgeLayout.to);
            edgeView.SetColor(new Color(0.24f, 0.30f, 0.24f, 0.96f));
            _runtimeObjects.Add(edgeObject);
            return edgeView;
        }

        private GameObject CreateSegment(Transform parent, string name)
        {
            var segment = new GameObject(name, typeof(RectTransform), typeof(WrapImage));
            segment.transform.SetParent(parent, false);
            var image = segment.GetComponent<WrapImage>();
            image.sprite = _panelSprite;
            image.type = _panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return segment;
        }

        private WrapLabel CreateLabel(Transform parent, string name, Vector2 size)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(WrapLabel));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.GetComponent<WrapLabel>();
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = false;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        private RectTransform CreateRectTransform(string name, Transform parent, Vector2 sizeDelta)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = sizeDelta;
            return rectTransform;
        }

        private Color ResolveConnectorColor(MetaProgressionNodeStatus status)
        {
            if (status.isLocked)
            {
                return new Color(0.24f, 0.28f, 0.26f, 0.9f);
            }

            if (status.isMaxed)
            {
                return new Color(0.19f, 0.55f, 0.31f, 0.94f);
            }

            return new Color(0.39f, 0.71f, 0.46f, 0.94f);
        }

        private void EnsureResources()
        {
        }

        private void ClearRuntimeObjects()
        {
            _nodeViews.Clear();
            _edgeViews.Clear();

            foreach (var runtimeObject in _runtimeObjects)
            {
                if (runtimeObject != null)
                {
                    Destroy(runtimeObject);
                }
            }

            _runtimeObjects.Clear();
        }
    }

    /// <summary>
    /// 스킬트리 노드 하나의 배경과 텍스트를 상태에 맞게 갱신합니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeNodeView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Button button;
        [SerializeField] private WrapImage background;
        [SerializeField] private WrapImage ring;
        [SerializeField] private WrapLabel glyphLabel;
        [SerializeField] private WrapLabel titleLabel;
        [SerializeField] private WrapLabel levelLabel;
        [SerializeField] private WrapLabel costLabel;

        private string _boundNodeId;

        /// <summary>
        /// 외부에서 생성한 하위 UI 참조를 한 번에 연결합니다.
        /// </summary>
        public void BindReferences(
            RectTransform targetRectTransform,
            Button targetButton,
            WrapImage backgroundImage,
            WrapImage ringImage,
            WrapLabel glyph,
            WrapLabel title,
            WrapLabel level,
            WrapLabel cost)
        {
            rectTransform = targetRectTransform;
            button = targetButton;
            background = backgroundImage;
            ring = ringImage;
            glyphLabel = glyph;
            titleLabel = title;
            levelLabel = level;
            costLabel = cost;
        }

        /// <summary>
        /// 현재 노드의 RectTransform을 외부 레이아웃 코드가 직접 배치할 수 있게 노출합니다.
        /// </summary>
        public RectTransform RectTransform => rectTransform;

        /// <summary>
        /// 중앙 허브처럼 클릭 업그레이드가 없는 장식을 설정합니다.
        /// </summary>
        public void BindHub(string title)
        {
            _boundNodeId = string.Empty;
            titleLabel.text = title;
            glyphLabel.text = "+";
            levelLabel.text = "CENTER";
            costLabel.text = string.Empty;
            ApplyColors(
                new Color(0.21f, 0.29f, 0.41f, 0.98f),
                new Color(0.42f, 0.62f, 0.84f, 0.98f),
                Color.white,
                new Color(0.88f, 0.94f, 1f, 0.96f));

            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }

        /// <summary>
        /// 뷰 모델을 바탕으로 텍스트와 시각 상태를 적용합니다.
        /// </summary>
        public void Bind(PrototypeHubSkillTreeNodeViewModel viewModel, Action<string> onClicked)
        {
            _boundNodeId = viewModel.nodeId;
            glyphLabel.text = viewModel.glyph;
            titleLabel.text = viewModel.displayName;
            levelLabel.text = viewModel.levelText;
            costLabel.text = ResolveFooterText(viewModel);

            var palette = ResolvePalette(viewModel.visualState);
            ApplyColors(palette.fill, palette.ring, palette.glyph, palette.detail);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(_boundNodeId));
            button.interactable = true;
        }

        private string ResolveFooterText(PrototypeHubSkillTreeNodeViewModel viewModel)
        {
            switch (viewModel.visualState)
            {
                case SkillTreeNodeVisualState.Locked:
                    return "LOCKED";

                case SkillTreeNodeVisualState.Maxed:
                    return "MAX";

                default:
                    return viewModel.costText;
            }
        }

        private void ApplyColors(Color fillColor, Color ringColor, Color glyphColor, Color detailColor)
        {
            background.color = fillColor;
            ring.color = ringColor;
            glyphLabel.color = glyphColor;
            titleLabel.color = Color.white;
            levelLabel.color = detailColor;
            costLabel.color = detailColor;
        }

        private (Color fill, Color ring, Color glyph, Color detail) ResolvePalette(SkillTreeNodeVisualState visualState)
        {
            switch (visualState)
            {
                case SkillTreeNodeVisualState.Selected:
                    return (
                        new Color(0.73f, 0.56f, 0.20f, 0.98f),
                        new Color(0.97f, 0.86f, 0.41f, 0.98f),
                        new Color(0.18f, 0.14f, 0.06f, 1f),
                        new Color(1f, 0.95f, 0.78f, 0.97f));

                case SkillTreeNodeVisualState.Maxed:
                    return (
                        new Color(0.16f, 0.42f, 0.23f, 0.98f),
                        new Color(0.34f, 0.82f, 0.49f, 0.98f),
                        Color.white,
                        new Color(0.79f, 0.94f, 0.83f, 0.96f));

                case SkillTreeNodeVisualState.Locked:
                    return (
                        new Color(0.20f, 0.22f, 0.25f, 0.98f),
                        new Color(0.35f, 0.38f, 0.42f, 0.98f),
                        new Color(0.75f, 0.78f, 0.80f, 0.95f),
                        new Color(0.63f, 0.68f, 0.72f, 0.92f));

                default:
                    return (
                        new Color(0.23f, 0.47f, 0.30f, 0.98f),
                        new Color(0.45f, 0.84f, 0.57f, 0.98f),
                        Color.white,
                        new Color(0.86f, 0.95f, 0.88f, 0.95f));
            }
        }
    }

    /// <summary>
    /// 하나의 부모-자식 관계를 직교 선분 두 개로 렌더링합니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeEdgeView : MonoBehaviour
    {
        private const float Thickness = 16f;

        [SerializeField] private WrapImage horizontalSegment;
        [SerializeField] private WrapImage verticalSegment;

        /// <summary>
        /// 선분 두 조각을 외부 생성 코드와 연결합니다.
        /// </summary>
        public void Bind(WrapImage horizontal, WrapImage vertical)
        {
            horizontalSegment = horizontal;
            verticalSegment = vertical;
        }

        /// <summary>
        /// 부모에서 자식으로 가는 경로를 가로 한 번, 세로 한 번으로 배치합니다.
        /// </summary>
        public void SetPath(Vector2 from, Vector2 to)
        {
            var horizontalRect = horizontalSegment.rectTransform;
            horizontalRect.anchorMin = new Vector2(0.5f, 0.5f);
            horizontalRect.anchorMax = new Vector2(0.5f, 0.5f);
            horizontalRect.pivot = new Vector2(0.5f, 0.5f);
            horizontalRect.anchoredPosition = new Vector2((from.x + to.x) * 0.5f, from.y);
            horizontalRect.sizeDelta = new Vector2(Mathf.Abs(to.x - from.x), Thickness);

            var verticalRect = verticalSegment.rectTransform;
            verticalRect.anchorMin = new Vector2(0.5f, 0.5f);
            verticalRect.anchorMax = new Vector2(0.5f, 0.5f);
            verticalRect.pivot = new Vector2(0.5f, 0.5f);
            verticalRect.anchoredPosition = new Vector2(to.x, (from.y + to.y) * 0.5f);
            verticalRect.sizeDelta = new Vector2(Thickness, Mathf.Abs(to.y - from.y));
            verticalSegment.enabled = !Mathf.Approximately(from.y, to.y);
        }

        /// <summary>
        /// 선 색상을 갱신합니다.
        /// </summary>
        public void SetColor(Color color)
        {
            horizontalSegment.color = color;
            verticalSegment.color = color;
        }
    }

    /// <summary>
    /// 연결선 상태를 갱신할 때 자식 노드 기준으로 다시 찾을 수 있게 보관하는 레코드입니다.
    /// </summary>
    public sealed class PrototypeHubSkillTreeEdgeRecord
    {
        public string childNodeId;
        public PrototypeHubSkillTreeEdgeView edgeView;
    }
}
