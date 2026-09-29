using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OpenCode.UnityUI.Editor.Tests
{
    public sealed class UGUIPreviewTests
    {
        [SetUp]
        public void SetUp()
        {
            UGUIPreviewTransactionStore.RestoreAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Apply_RefusesEditMode()
        {
            object response = PreviewUGUILayout.HandleCommand(JObject.FromObject(new
            {
                action = "apply",
                operations = new[] { new { target = "Missing", operation = "set_active", active = false } }
            }));

            Assert.That(response, Is.TypeOf<ErrorResponse>());
            Assert.That(((ErrorResponse)response).Error, Does.Contain("Play-mode-only"));
        }

        [Test]
        public void TransactionStore_RestoresHierarchyRectAndLayoutElement()
        {
            RectTransform canvas = CreateRect("Canvas", null);
            RectTransform originalParent = CreateRect("OriginalParent", canvas);
            RectTransform previewParent = CreateRect("PreviewParent", canvas);
            RectTransform card = CreateRect("Card", originalParent);
            card.anchoredPosition = new Vector2(37f, 19f);
            card.sizeDelta = new Vector2(200f, 100f);
            var layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 200f;
            var transaction = new UGUIPreviewTransactionStore.Transaction
            {
                Id = "preview-test",
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                Objects =
                {
                    SnapshotObject(card.gameObject)
                },
                Components =
                {
                    SnapshotComponent(card),
                    SnapshotComponent(layout)
                }
            };
            UGUIPreviewTransactionStore.Add(transaction);
            card.SetParent(previewParent, false);
            UGUILayoutSerializer.ApplyProperties(card, JObject.FromObject(new { anchored_position = new[] { 0f, 0f }, size_delta = new[] { 300f, 120f } }));
            UGUILayoutSerializer.ApplyProperties(layout, JObject.FromObject(new { preferred_width = 300f }));

            Assert.That(card.parent, Is.EqualTo(previewParent));
            Assert.That(card.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(300f, 120f)));
            Assert.That(card.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(300f));

            Assert.That(UGUIPreviewTransactionStore.Restore("preview-test", out var errors), Is.True, string.Join("; ", errors));
            Assert.That(card.parent, Is.EqualTo(originalParent));
            Assert.That(card.anchoredPosition, Is.EqualTo(new Vector2(37f, 19f)));
            Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(200f, 100f)));
            Assert.That(card.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(200f));

        }

        [Test]
        public void PropertyPreflightFailureLeavesObjectUnchanged()
        {
            RectTransform canvas = CreateRect("Canvas", null);
            RectTransform card = CreateRect("Card", canvas);
            card.sizeDelta = new Vector2(200f, 100f);

            Assert.Throws<ArgumentException>(() => UGUILayoutSerializer.ValidateProperties(
                card,
                JObject.FromObject(new { invented_property = 1 })));
            Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(200f, 100f)));
            Assert.That(UGUIPreviewTransactionStore.GetTransactions(), Is.Empty);
        }

        static UGUIPreviewTransactionStore.GameObjectSnapshot SnapshotObject(GameObject gameObject)
        {
            Transform parent = gameObject.transform.parent;
            return new UGUIPreviewTransactionStore.GameObjectSnapshot
            {
                EntityId = UGUIObjectId.GetSessionId(gameObject),
                Path = UGUIObjectResolver.GetHierarchyPath(gameObject.transform),
                ParentEntityId = parent == null ? null : UGUIObjectId.GetSessionId(parent.gameObject),
                ParentPath = parent == null ? null : UGUIObjectResolver.GetHierarchyPath(parent),
                SiblingIndex = gameObject.transform.GetSiblingIndex(),
                ActiveSelf = gameObject.activeSelf,
                Depth = 2
            };
        }

        static UGUIPreviewTransactionStore.ComponentSnapshot SnapshotComponent(Component component)
        {
            return new UGUIPreviewTransactionStore.ComponentSnapshot
            {
                GameObjectEntityId = UGUIObjectId.GetSessionId(component.gameObject),
                TypeName = component.GetType().AssemblyQualifiedName,
                ComponentIndex = Array.IndexOf(component.gameObject.GetComponents(component.GetType()), component),
                Json = EditorJsonUtility.ToJson(component, false)
            };
        }

        static RectTransform CreateRect(string name, RectTransform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (parent != null)
                rect.SetParent(parent, false);
            return rect;
        }
    }
}
