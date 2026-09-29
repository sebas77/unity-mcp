using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenCode.UnityUI.Editor
{
    public sealed class UGUIResolutionException : Exception
    {
        public UGUIResolutionException(string message)
            : base(message)
        {
        }
    }

    public static class UGUIObjectResolver
    {
        public static IReadOnlyList<RectTransform> Resolve(
            string rootSelector,
            IReadOnlyList<string> targetSelectors,
            bool includeInactive,
            int maxDepth)
        {
            RectTransform root = string.IsNullOrWhiteSpace(rootSelector)
                ? null
                : RequireRectTransform(ResolveOne(rootSelector, null, includeInactive), rootSelector);

            var results = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            if (targetSelectors != null && targetSelectors.Count > 0)
            {
                foreach (string selector in targetSelectors)
                {
                    GameObject target = ResolveOne(selector, root, includeInactive);
                    RectTransform rect = RequireRectTransform(target, selector);
                    AddTree(rect, maxDepth, includeInactive, results);
                }
            }
            else if (root != null)
            {
                AddTree(root, maxDepth, includeInactive, results);
            }
            else
            {
                foreach (RectTransform rect in GetSceneRectTransforms(includeInactive))
                {
                    if (GetRectTransformParent(rect) == null)
                        AddTree(rect, maxDepth, includeInactive, results);
                }
            }

            return results.Values
                .OrderBy(GetHierarchyPath, StringComparer.Ordinal)
                .ToArray();
        }

        public static GameObject ResolveOne(string selector, Transform root, bool includeInactive)
        {
            if (string.IsNullOrWhiteSpace(selector))
                throw new UGUIResolutionException("Target selector cannot be empty.");

            string trimmed = selector.Trim();
            if (ulong.TryParse(trimmed, out _) || int.TryParse(trimmed, out _))
            {
                GameObject[] idMatches = GetSceneGameObjects(true)
                    .Where(gameObject => UGUIObjectId.MatchesSelector(gameObject, trimmed))
                    .ToArray();
                if (idMatches.Length == 0)
                    throw new UGUIResolutionException($"No scene GameObject has session/instance ID {trimmed}.");
                if (idMatches.Length > 1)
                {
                    string paths = string.Join(", ", idMatches.Select(gameObject => GetHierarchyPath(gameObject.transform)));
                    throw new UGUIResolutionException($"Legacy instance ID '{trimmed}' is ambiguous. Use a full entity ID or path. Candidates: {paths}");
                }
                GameObject instanceObject = idMatches[0];
                if (root != null && IsDescendantOrSelf(instanceObject.transform, root) == false)
                    throw new UGUIResolutionException($"'{trimmed}' is outside root '{GetHierarchyPath(root)}'.");
                if (includeInactive == false && instanceObject.activeInHierarchy == false)
                    throw new UGUIResolutionException($"'{GetHierarchyPath(instanceObject.transform)}' is inactive.");
                return instanceObject;
            }

            GameObject[] candidates = GetSceneGameObjects(includeInactive)
                .Where(gameObject => root == null || IsDescendantOrSelf(gameObject.transform, root))
                .ToArray();

            bool isPath = trimmed.IndexOf('/') >= 0 || trimmed.IndexOf(':') >= 0;
            GameObject[] matches = isPath
                ? candidates.Where(gameObject => PathMatches(GetHierarchyPath(gameObject.transform), trimmed)).ToArray()
                : candidates.Where(gameObject => string.Equals(gameObject.name, trimmed, StringComparison.Ordinal)).ToArray();

            if (matches.Length == 0)
                throw new UGUIResolutionException($"No scene GameObject matches '{trimmed}'.");
            if (matches.Length > 1)
            {
                string paths = string.Join(", ", matches.Select(gameObject => GetHierarchyPath(gameObject.transform)));
                throw new UGUIResolutionException($"Selector '{trimmed}' is ambiguous. Candidates: {paths}");
            }

            return matches[0];
        }

        public static string GetHierarchyPath(Component component)
        {
            return component == null ? null : GetHierarchyPath(component.transform);
        }

        public static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            string sceneName = transform.gameObject.scene.IsValid()
                ? transform.gameObject.scene.name
                : "NoScene";
            return sceneName + ":/" + string.Join("/", names);
        }

        public static RectTransform GetRectTransformParent(RectTransform rect)
        {
            Transform parent = rect.parent;
            while (parent != null)
            {
                if (parent is RectTransform rectParent)
                    return rectParent;
                parent = parent.parent;
            }

            return null;
        }

        static RectTransform RequireRectTransform(GameObject gameObject, string selector)
        {
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (rect == null)
                throw new UGUIResolutionException($"'{selector}' resolves to '{GetHierarchyPath(gameObject.transform)}', which has no RectTransform.");
            return rect;
        }

        static void AddTree(
            RectTransform root,
            int maxDepth,
            bool includeInactive,
            IDictionary<string, RectTransform> results)
        {
            var pending = new Queue<KeyValuePair<RectTransform, int>>();
            pending.Enqueue(new KeyValuePair<RectTransform, int>(root, 0));
            while (pending.Count > 0)
            {
                KeyValuePair<RectTransform, int> entry = pending.Dequeue();
                RectTransform rect = entry.Key;
                int depth = entry.Value;
                if (includeInactive || rect.gameObject.activeInHierarchy)
                    results[UGUIObjectId.GetSessionId(rect)] = rect;
                if (depth >= maxDepth)
                    continue;

                for (int index = 0; index < rect.childCount; index++)
                {
                    if (rect.GetChild(index) is RectTransform child)
                        pending.Enqueue(new KeyValuePair<RectTransform, int>(child, depth + 1));
                }
            }
        }

        static IEnumerable<RectTransform> GetSceneRectTransforms(bool includeInactive)
        {
            return Resources.FindObjectsOfTypeAll<RectTransform>()
                .Where(rect => IsSceneObject(rect.gameObject))
                .Where(rect => includeInactive || rect.gameObject.activeInHierarchy);
        }

        static GameObject[] GetSceneGameObjects(bool includeInactive)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(IsSceneObject)
                .Where(gameObject => includeInactive || gameObject.activeInHierarchy)
                .ToArray();
        }

        static bool IsSceneObject(GameObject gameObject)
        {
            Scene scene = gameObject.scene;
            return EditorUtility.IsPersistent(gameObject) == false && scene.IsValid() && scene.isLoaded;
        }

        static bool IsDescendantOrSelf(Transform candidate, Transform root)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current == root)
                    return true;
                current = current.parent;
            }

            return false;
        }

        static bool PathMatches(string canonicalPath, string selector)
        {
            string normalized = selector.Replace('\\', '/').Trim();
            if (normalized.StartsWith("/", StringComparison.Ordinal))
                normalized = normalized.Substring(1);
            if (string.Equals(canonicalPath, normalized, StringComparison.Ordinal))
                return true;

            int marker = canonicalPath.IndexOf(":/", StringComparison.Ordinal);
            string sceneRelative = marker >= 0 ? canonicalPath.Substring(marker + 2) : canonicalPath;
            return string.Equals(sceneRelative, normalized, StringComparison.Ordinal);
        }
    }
}
