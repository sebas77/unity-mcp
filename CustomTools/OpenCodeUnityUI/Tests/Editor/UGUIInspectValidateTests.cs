using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Runtime.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenCode.UnityUI.Editor.Tests
{
    public sealed class UGUIInspectValidateTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Inspect_ReturnsDeterministicPagedHierarchyData()
        {
            RectTransform canvas = CreateCanvas();
            RectTransform first = CreateImage("First", canvas, new Vector2(-100f, 0f));
            CreateImage("Second", canvas, new Vector2(100f, 0f));
            Canvas.ForceUpdateCanvases();

            object response = InspectUGUI.HandleCommand(JObject.FromObject(new
            {
                root = canvas.gameObject.GetInstanceIDCompat().ToString(),
                max_depth = 1,
                page_size = 2,
                cursor = 1
            }));

            Assert.That(response, Is.TypeOf<SuccessResponse>());
            var result = (UGUIInspectionResult)((SuccessResponse)response).Data;
            Assert.That(result.Total, Is.EqualTo(3));
            Assert.That(result.Nodes.Count, Is.EqualTo(2));
            Assert.That(result.Nodes[0].Path, Does.EndWith("/First"));
            Assert.That(result.Nodes[0].InstanceId, Is.EqualTo(first.gameObject.GetInstanceIDCompat()));
            Assert.That(result.NextCursor, Is.Null);
            Assert.That(result.Nodes.All(node => node.ScreenBounds.Valid), Is.True);
        }

        [Test]
        public void Resolver_RejectsAmbiguousNames()
        {
            RectTransform canvas = CreateCanvas();
            CreateImage("Duplicate", canvas, Vector2.zero);
            RectTransform group = CreateRect("Group", canvas);
            CreateImage("Duplicate", group, Vector2.zero);

            UGUIResolutionException exception = Assert.Throws<UGUIResolutionException>(
                () => UGUIObjectResolver.ResolveOne("Duplicate", null, true));
            Assert.That(exception.Message, Does.Contain("ambiguous"));
            Assert.That(exception.Message, Does.Contain("Canvas/Group/Duplicate"));
        }

        [Test]
        public void Validate_ReportsOffScreenZeroSizeAndMissingEventSystem()
        {
            RectTransform canvas = CreateCanvas();
            RectTransform offScreen = CreateImage("OffScreen", canvas, new Vector2(100000f, 0f));
            offScreen.sizeDelta = Vector2.zero;
            Canvas.ForceUpdateCanvases();

            object response = ValidateUGUI.HandleCommand(JObject.FromObject(new
            {
                root = canvas.gameObject.GetInstanceIDCompat().ToString(),
                checks = new[] { "off_screen", "zero_size", "event_system_count" }
            }));

            Assert.That(response, Is.TypeOf<SuccessResponse>());
            var result = (UGUIValidationResult)((SuccessResponse)response).Data;
            Assert.That(result.Findings.Any(finding => finding.Code == "off_screen" && finding.Path.EndsWith("/OffScreen")), Is.True);
            Assert.That(result.Findings.Any(finding => finding.Code == "zero_size" && finding.Path.EndsWith("/OffScreen")), Is.True);
            Assert.That(result.Findings.Any(finding => finding.Code == "event_system_count"), Is.True);
        }

        [Test]
        public void Validate_ButtonReadinessReportsConcreteIssuesOnly()
        {
            RectTransform canvas = CreateCanvas();
            RectTransform buttonRect = CreateImage("Button", canvas, Vector2.zero);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonRect.GetComponent<Image>();
            button.targetGraphic.raycastTarget = false;
            new GameObject("EventSystem", typeof(EventSystem));

            object response = ValidateUGUI.HandleCommand(JObject.FromObject(new
            {
                root = canvas.gameObject.GetInstanceIDCompat().ToString(),
                checks = new[] { "button_readiness" }
            }));

            var result = (UGUIValidationResult)((SuccessResponse)response).Data;
            Assert.That(result.Findings.Any(finding => finding.Code == "button_target_not_raycastable"), Is.True);
            Assert.That(result.Findings.Any(finding => finding.Code == "button_no_persistent_listener" && finding.Severity == "info"), Is.True);
        }

        static RectTransform CreateCanvas()
        {
            var gameObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1920f, 1080f);
            return rect;
        }

        static RectTransform CreateImage(string name, RectTransform parent, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(100f, 100f);
            return rect;
        }

        static RectTransform CreateRect(string name, RectTransform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(500f, 500f);
            return rect;
        }
    }
}
