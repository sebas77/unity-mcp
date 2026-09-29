using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OpenCode.UnityUI.Editor
{
    public sealed class GameViewStateSnapshot
    {
        public int SelectedSizeIndex { get; set; }
        public int TargetDisplay { get; set; }
        public List<string> OwnedSizeLabels { get; set; } = new List<string>();
    }

    public sealed class GameViewResolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Label { get; set; }
    }

    public sealed class GameViewSizeController
    {
        public GameViewStateSnapshot CaptureState()
        {
            object gameView = GetGameView();
            return new GameViewStateSnapshot
            {
                SelectedSizeIndex = GetProperty<int>(gameView, "selectedSizeIndex"),
                TargetDisplay = GetProperty<int>(gameView, "targetDisplay")
            };
        }

        public int EnsureResolution(GameViewStateSnapshot snapshot, GameViewResolution resolution, string ownerId)
        {
            object group = GetCurrentGroup();
            int existingIndex = FindSizeIndex(group, resolution.Width, resolution.Height);
            if (existingIndex >= 0)
                return existingIndex;

            string label = $"OpenCodeUnityUI:{ownerId}:{resolution.Label}";
            object sizeType = Enum.Parse(_gameViewSizeType, "FixedResolution");
            object size = Activator.CreateInstance(_gameViewSize, sizeType, resolution.Width, resolution.Height, label);
            Invoke(group, "AddCustomSize", size);
            int addedIndex = FindSizeIndex(group, resolution.Width, resolution.Height, label);
            if (addedIndex < 0)
                throw new InvalidOperationException($"Unity did not add Game view size {resolution.Width}x{resolution.Height}.");
            snapshot.OwnedSizeLabels.Add(label);
            return addedIndex;
        }

        public void SelectSize(int totalIndex)
        {
            object gameView = GetGameView();
            SetProperty(gameView, "selectedSizeIndex", totalIndex);
            ((EditorWindow)gameView).Repaint();
        }

        public void Restore(GameViewStateSnapshot snapshot)
        {
            object group = GetCurrentGroup();
            for (int totalIndex = GetTotalCount(group) - 1; totalIndex >= 0; totalIndex--)
            {
                object size = Invoke(group, "GetGameViewSize", totalIndex);
                string label = GetProperty<string>(size, "baseText");
                if (snapshot.OwnedSizeLabels.Contains(label) == false)
                    continue;
                Invoke(group, "RemoveCustomSize", totalIndex);
            }

            object gameView = GetGameView();
            int totalCount = GetTotalCount(group);
            if (snapshot.SelectedSizeIndex >= 0 && snapshot.SelectedSizeIndex < totalCount)
                SetProperty(gameView, "selectedSizeIndex", snapshot.SelectedSizeIndex);
            SetProperty(gameView, "targetDisplay", snapshot.TargetDisplay);
            ((EditorWindow)gameView).Repaint();
        }

        public int FindSizeIndex(int width, int height)
        {
            return FindSizeIndex(GetCurrentGroup(), width, height);
        }

        int FindSizeIndex(object group, int width, int height, string requiredLabel = null)
        {
            int totalCount = GetTotalCount(group);
            for (int index = 0; index < totalCount; index++)
            {
                object size = Invoke(group, "GetGameViewSize", index);
                if (GetProperty<int>(size, "width") != width || GetProperty<int>(size, "height") != height)
                    continue;
                if (requiredLabel != null && string.Equals(GetProperty<string>(size, "baseText"), requiredLabel, StringComparison.Ordinal) == false)
                    continue;
                return index;
            }
            return -1;
        }

        object GetGameView()
        {
            EditorWindow focused = EditorWindow.focusedWindow;
            if (focused != null && _gameView.IsInstanceOfType(focused))
                return focused;
            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(_gameView);
            if (views.Length == 0)
                throw new InvalidOperationException("No Game view is open. Open a Game view before using ugui_capture_matrix.");
            return views[0];
        }

        object GetCurrentGroup()
        {
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(_gameViewSizes);
            PropertyInfo instanceProperty = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            object sizes = instanceProperty?.GetValue(null);
            if (sizes == null)
                throw new InvalidOperationException("Unity GameViewSizes singleton is unavailable.");
            return GetProperty<object>(sizes, "currentGroup");
        }

        static int GetTotalCount(object group)
        {
            return (int)Invoke(group, "GetTotalCount");
        }

        static T GetProperty<T>(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, InstanceBindings);
            if (property == null)
                throw new MissingMemberException(instance.GetType().FullName, name);
            return (T)property.GetValue(instance);
        }

        static void SetProperty(object instance, string name, object value)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, InstanceBindings);
            if (property == null || property.CanWrite == false)
                throw new MissingMemberException(instance.GetType().FullName, name);
            property.SetValue(instance, value);
        }

        static object Invoke(object instance, string name, params object[] arguments)
        {
            Type[] argumentTypes = arguments.Select(argument => argument.GetType()).ToArray();
            MethodInfo method = instance.GetType().GetMethod(name, InstanceBindings, null, argumentTypes, null);
            if (method == null)
            {
                method = instance.GetType().GetMethods(InstanceBindings)
                    .SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
            }
            if (method == null)
                throw new MissingMethodException(instance.GetType().FullName, name);
            return method.Invoke(instance, arguments);
        }

        static Type RequireType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName);
            if (type == null)
                throw new NotSupportedException($"Unity internal type '{assemblyQualifiedName}' is unavailable in this Editor version.");
            return type;
        }

        const BindingFlags InstanceBindings = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly Type _gameView = RequireType("UnityEditor.GameView,UnityEditor");
        readonly Type _gameViewSizes = RequireType("UnityEditor.GameViewSizes,UnityEditor");
        readonly Type _gameViewSize = RequireType("UnityEditor.GameViewSize,UnityEditor");
        readonly Type _gameViewSizeType = RequireType("UnityEditor.GameViewSizeType,UnityEditor");
    }
}
