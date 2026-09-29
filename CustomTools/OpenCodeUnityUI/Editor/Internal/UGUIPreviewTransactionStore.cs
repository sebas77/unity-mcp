using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace OpenCode.UnityUI.Editor
{
    [InitializeOnLoad]
    public static class UGUIPreviewTransactionStore
    {
        public sealed class Transaction
        {
            public string Id { get; set; }
            public string CreatedUtc { get; set; }
            public List<GameObjectSnapshot> Objects { get; set; } = new List<GameObjectSnapshot>();
            public List<ComponentSnapshot> Components { get; set; } = new List<ComponentSnapshot>();
        }

        public sealed class GameObjectSnapshot
        {
            public string EntityId { get; set; }
            public string Path { get; set; }
            public string ParentEntityId { get; set; }
            public string ParentPath { get; set; }
            public int SiblingIndex { get; set; }
            public bool ActiveSelf { get; set; }
            public int Depth { get; set; }
        }

        public sealed class ComponentSnapshot
        {
            public string GameObjectEntityId { get; set; }
            public string TypeName { get; set; }
            public int ComponentIndex { get; set; }
            public string Json { get; set; }
        }

        static UGUIPreviewTransactionStore()
        {
            Load();
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAllBeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += RestoreAllBeforeReload;
        }

        public static bool Contains(string transactionId)
        {
            return _transactions.ContainsKey(transactionId);
        }

        public static IReadOnlyList<Transaction> GetTransactions()
        {
            return _transactions.Values.OrderBy(transaction => transaction.Id, StringComparer.Ordinal).ToArray();
        }

        public static void Add(Transaction transaction)
        {
            _transactions.Add(transaction.Id, transaction);
            Save();
        }

        public static bool Restore(string transactionId, out IReadOnlyList<string> errors)
        {
            if (_transactions.TryGetValue(transactionId, out Transaction transaction) == false)
            {
                errors = new[] { $"Preview transaction '{transactionId}' does not exist." };
                return false;
            }

            var restoreErrors = new List<string>();
            foreach (GameObjectSnapshot snapshot in transaction.Objects.OrderBy(snapshot => snapshot.Depth))
            {
                GameObject gameObject = FindGameObject(snapshot.EntityId);
                if (gameObject == null)
                {
                    restoreErrors.Add($"Object '{snapshot.Path}' no longer exists.");
                    continue;
                }

                Transform parent = string.IsNullOrEmpty(snapshot.ParentEntityId)
                    ? null
                    : FindGameObject(snapshot.ParentEntityId)?.transform;
                if (string.IsNullOrEmpty(snapshot.ParentEntityId) == false && parent == null)
                {
                    restoreErrors.Add($"Parent '{snapshot.ParentPath}' for '{snapshot.Path}' no longer exists.");
                    continue;
                }
                gameObject.transform.SetParent(parent, false);
                gameObject.SetActive(snapshot.ActiveSelf);
            }

            foreach (ComponentSnapshot snapshot in transaction.Components)
            {
                GameObject gameObject = FindGameObject(snapshot.GameObjectEntityId);
                Type componentType = Type.GetType(snapshot.TypeName);
                Component component = gameObject == null || componentType == null
                    ? null
                    : gameObject.GetComponents(componentType).ElementAtOrDefault(snapshot.ComponentIndex);
                if (component == null)
                {
                    restoreErrors.Add($"Component '{snapshot.TypeName}' on object {snapshot.GameObjectEntityId} no longer exists.");
                    continue;
                }
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(snapshot.Json, component);
                }
                catch (Exception exception)
                {
                    restoreErrors.Add($"Could not restore {snapshot.TypeName}: {exception.Message}");
                }
            }

            foreach (IGrouping<string, GameObjectSnapshot> group in transaction.Objects
                         .Where(snapshot => string.IsNullOrEmpty(snapshot.ParentEntityId) == false)
                         .GroupBy(snapshot => snapshot.ParentEntityId))
            {
                foreach (GameObjectSnapshot snapshot in group.OrderBy(snapshot => snapshot.SiblingIndex))
                {
                    GameObject gameObject = FindGameObject(snapshot.EntityId);
                    if (gameObject != null)
                        gameObject.transform.SetSiblingIndex(snapshot.SiblingIndex);
                }
            }

            if (restoreErrors.Count == 0)
            {
                _transactions.Remove(transactionId);
                Save();
            }
            errors = restoreErrors;
            return restoreErrors.Count == 0;
        }

        public static IReadOnlyList<string> RestoreAll()
        {
            var errors = new List<string>();
            foreach (string transactionId in _transactions.Keys.ToArray())
            {
                if (Restore(transactionId, out IReadOnlyList<string> transactionErrors) == false)
                    errors.AddRange(transactionErrors);
            }
            return errors;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                RestoreAll();
        }

        static void RestoreAllBeforeReload()
        {
            RestoreAll();
        }

        static void Load()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;
            try
            {
                List<Transaction> transactions = JsonConvert.DeserializeObject<List<Transaction>>(json);
                if (transactions != null)
                {
                    foreach (Transaction transaction in transactions)
                        _transactions[transaction.Id] = transaction;
                }
            }
            catch (JsonException)
            {
                SessionState.EraseString(SessionKey);
            }
        }

        static void Save()
        {
            if (_transactions.Count == 0)
            {
                SessionState.EraseString(SessionKey);
                return;
            }
            SessionState.SetString(SessionKey, JsonConvert.SerializeObject(_transactions.Values));
        }

        static GameObject FindGameObject(string entityId)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(gameObject => gameObject.scene.IsValid() && gameObject.scene.isLoaded &&
                                              UGUIObjectId.GetSessionId(gameObject) == entityId);
        }

        const string SessionKey = "OpenCode.UnityUI.PreviewTransactions.v2";
        static readonly Dictionary<string, Transaction> _transactions = new Dictionary<string, Transaction>(StringComparer.Ordinal);
    }
}
