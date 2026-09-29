using System;
using MCPForUnity.Editor.Tools;
using NUnit.Framework;

namespace OpenCode.UnityUI.Editor.Tests
{
    public sealed class UGUICaptureAndDiscoveryTests
    {
        [Test]
        public void Tools_HaveExpectedDiscoveryContracts()
        {
            AssertTool(typeof(InspectUGUI), "ugui_inspect", false);
            AssertTool(typeof(ValidateUGUI), "ugui_validate", false);
            AssertTool(typeof(PreviewUGUILayout), "ugui_preview_layout", false);
            AssertTool(typeof(CaptureUGUIMatrix), "ugui_capture_matrix", true);
        }

        [Test]
        public void GameViewSizeController_AddsAndRemovesOnlyOwnedSize()
        {
            var controller = new GameViewSizeController();
            GameViewStateSnapshot snapshot = controller.CaptureState();
            int width = 733;
            int height = 411;
            while (controller.FindSizeIndex(width, height) >= 0)
                width++;

            int index = controller.EnsureResolution(snapshot, new GameViewResolution { Width = width, Height = height, Label = "test" }, "owner");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(controller.FindSizeIndex(width, height), Is.EqualTo(index));

            controller.Restore(snapshot);
            Assert.That(controller.FindSizeIndex(width, height), Is.EqualTo(-1));
        }

        static void AssertTool(Type type, string expectedName, bool requiresPolling)
        {
            var attribute = (McpForUnityToolAttribute)Attribute.GetCustomAttribute(type, typeof(McpForUnityToolAttribute));
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.Name, Is.EqualTo(expectedName));
            Assert.That(attribute.Group, Is.EqualTo("ui"));
            Assert.That(attribute.RequiresPolling, Is.EqualTo(requiresPolling));
            Assert.That(type.GetMethod("HandleCommand"), Is.Not.Null);
        }
    }
}
