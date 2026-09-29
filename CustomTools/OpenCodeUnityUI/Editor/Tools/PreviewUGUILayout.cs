using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenCode.UnityUI.Editor
{
    [McpForUnityTool(
        "ugui_preview_layout",
        Description = "Apply, inspect, and restore declarative Play-mode-only uGUI layout preview transactions.",
        Group = "ui")]
    public static class PreviewUGUILayout
    {
        public sealed class Parameters
        {
            [ToolParameter("Action: apply, status, restore, or restore_all.")]
            public string action { get; set; }

            [ToolParameter("Transaction ID. Generated for apply when omitted; required for restore.", Required = false)]
            public string transaction_id { get; set; }

            [ToolParameter("Ordered declarative operations for apply.", Required = false)]
            public Operation[] operations { get; set; }
        }

        public sealed class Operation
        {
            public string target { get; set; }
            public string operation { get; set; }
            public string parent { get; set; }
            public int? sibling_index { get; set; }
            public bool? active { get; set; }
            public string component { get; set; }
            public JObject properties { get; set; }
        }

        sealed class PreparedOperation
        {
            public Operation Source { get; set; }
            public GameObject Target { get; set; }
            public Transform Parent { get; set; }
            public Component Component { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            try
            {
                Parameters parameters = @params == null ? new Parameters() : @params.ToObject<Parameters>();
                string action = (parameters.action ?? string.Empty).ToLowerInvariant();
                switch (action)
                {
                    case "apply": return Apply(parameters);
                    case "status": return Status();
                    case "restore": return Restore(parameters.transaction_id);
                    case "restore_all": return RestoreAll();
                    case "": return new ErrorResponse("action is required: apply, status, restore, or restore_all.");
                    default: return new ErrorResponse($"Unknown action '{parameters.action}'. Allowed: apply, status, restore, restore_all.");
                }
            }
            catch (UGUIResolutionException exception)
            {
                return new ErrorResponse(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return new ErrorResponse(exception.Message);
            }
            catch (Exception exception)
            {
                return new ErrorResponse($"uGUI preview failed: {exception.Message}");
            }
        }

        static object Apply(Parameters parameters)
        {
            if (Application.isPlaying == false)
                return new ErrorResponse("ugui_preview_layout apply is Play-mode-only.");
            if (parameters.operations == null || parameters.operations.Length == 0)
                return new ErrorResponse("apply requires at least one operation.");

            string transactionId = string.IsNullOrWhiteSpace(parameters.transaction_id)
                ? Guid.NewGuid().ToString("N")
                : parameters.transaction_id.Trim();
            if (transactionId.Length > 128)
                return new ErrorResponse("transaction_id cannot exceed 128 characters.");
            if (UGUIPreviewTransactionStore.Contains(transactionId))
                return new ErrorResponse($"Preview transaction '{transactionId}' already exists.");

            PreparedOperation[] prepared = parameters.operations.Select(Prepare).ToArray();
            UGUIPreviewTransactionStore.Transaction transaction = Snapshot(transactionId, prepared);
            UGUIPreviewTransactionStore.Add(transaction);
            try
            {
                foreach (PreparedOperation operation in prepared)
                    Execute(operation);
            }
            catch (Exception exception)
            {
                if (UGUIPreviewTransactionStore.Restore(transactionId, out IReadOnlyList<string> restoreErrors) == false)
                {
                    throw new InvalidOperationException(
                        $"Preview apply failed: {exception.Message}. Restoration also failed: {string.Join("; ", restoreErrors)}",
                        exception);
                }
                throw;
            }

            return new SuccessResponse($"Applied uGUI preview transaction '{transactionId}'.", new
            {
                transaction_id = transactionId,
                operation_count = prepared.Length,
                object_count = transaction.Objects.Count,
                component_count = transaction.Components.Count
            });
        }

        static object Status()
        {
            UGUIPreviewTransactionStore.Transaction[] transactions = UGUIPreviewTransactionStore.GetTransactions().ToArray();
            return new SuccessResponse($"Found {transactions.Length} active uGUI preview transaction(s).", new
            {
                count = transactions.Length,
                transactions = transactions.Select(transaction => new
                {
                    transaction_id = transaction.Id,
                    created_utc = transaction.CreatedUtc,
                    object_count = transaction.Objects.Count,
                    component_count = transaction.Components.Count
                }).ToArray()
            });
        }

        static object Restore(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                return new ErrorResponse("restore requires transaction_id.");
            if (UGUIPreviewTransactionStore.Restore(transactionId.Trim(), out IReadOnlyList<string> errors) == false)
                return new ErrorResponse($"Could not fully restore preview transaction '{transactionId}'.", new { errors });
            return new SuccessResponse($"Restored uGUI preview transaction '{transactionId}'.", new { transaction_id = transactionId });
        }

        static object RestoreAll()
        {
            IReadOnlyList<string> errors = UGUIPreviewTransactionStore.RestoreAll();
            if (errors.Count > 0)
                return new ErrorResponse("Could not fully restore all uGUI preview transactions.", new { errors });
            return new SuccessResponse("Restored all uGUI preview transactions.");
        }

        static PreparedOperation Prepare(Operation operation)
        {
            if (operation == null || string.IsNullOrWhiteSpace(operation.target))
                throw new ArgumentException("Every preview operation requires target.");
            if (string.IsNullOrWhiteSpace(operation.operation))
                throw new ArgumentException($"Preview operation for '{operation.target}' requires operation.");

            GameObject target = UGUIObjectResolver.ResolveOne(operation.target, null, true);
            string operationName = operation.operation.ToLowerInvariant();
            switch (operationName)
            {
                case "reparent":
                    if (string.IsNullOrWhiteSpace(operation.parent))
                        throw new ArgumentException($"reparent for '{operation.target}' requires parent.");
                    GameObject parent = UGUIObjectResolver.ResolveOne(operation.parent, null, true);
                    if (parent.transform == target.transform || parent.transform.IsChildOf(target.transform))
                        throw new ArgumentException($"reparent for '{operation.target}' would create a hierarchy cycle.");
                    if (operation.sibling_index < 0)
                        throw new ArgumentException("sibling_index cannot be negative.");
                    return new PreparedOperation { Source = operation, Target = target, Parent = parent.transform };
                case "set_active":
                    if (operation.active.HasValue == false)
                        throw new ArgumentException($"set_active for '{operation.target}' requires active.");
                    return new PreparedOperation { Source = operation, Target = target };
                case "set_properties":
                    if (string.IsNullOrWhiteSpace(operation.component))
                        throw new ArgumentException($"set_properties for '{operation.target}' requires component.");
                    Component component = UGUILayoutSerializer.ResolveSupportedComponent(target, operation.component);
                    if (component == null)
                        throw new ArgumentException($"'{UGUIObjectResolver.GetHierarchyPath(target.transform)}' has no {operation.component} component.");
                    UGUILayoutSerializer.ValidateProperties(component, operation.properties);
                    return new PreparedOperation { Source = operation, Target = target, Component = component };
                default:
                    throw new ArgumentException($"Unknown preview operation '{operation.operation}'. Allowed: reparent, set_active, set_properties.");
            }
        }

        static UGUIPreviewTransactionStore.Transaction Snapshot(string transactionId, IEnumerable<PreparedOperation> operations)
        {
            PreparedOperation[] prepared = operations.ToArray();
            var transaction = new UGUIPreviewTransactionStore.Transaction
            {
                Id = transactionId,
                CreatedUtc = DateTime.UtcNow.ToString("O")
            };
            foreach (GameObject target in prepared.Select(operation => operation.Target).Distinct())
            {
                Transform parent = target.transform.parent;
                transaction.Objects.Add(new UGUIPreviewTransactionStore.GameObjectSnapshot
                {
                    EntityId = UGUIObjectId.GetSessionId(target),
                    Path = UGUIObjectResolver.GetHierarchyPath(target.transform),
                    ParentEntityId = parent == null ? null : UGUIObjectId.GetSessionId(parent.gameObject),
                    ParentPath = parent == null ? null : UGUIObjectResolver.GetHierarchyPath(parent),
                    SiblingIndex = target.transform.GetSiblingIndex(),
                    ActiveSelf = target.activeSelf,
                    Depth = GetDepth(target.transform)
                });
            }

            IEnumerable<Component> touchedComponents = prepared
                .Where(operation => operation.Component != null)
                .Select(operation => operation.Component)
                .Concat(prepared
                    .Where(operation => string.Equals(operation.Source.operation, "reparent", StringComparison.OrdinalIgnoreCase))
                    .Select(operation => operation.Target.GetComponent<RectTransform>())
                    .Where(component => component != null));
            foreach (Component component in touchedComponents
                         .Distinct())
            {
                transaction.Components.Add(new UGUIPreviewTransactionStore.ComponentSnapshot
                {
                    GameObjectEntityId = UGUIObjectId.GetSessionId(component.gameObject),
                    TypeName = component.GetType().AssemblyQualifiedName,
                    ComponentIndex = Array.IndexOf(component.gameObject.GetComponents(component.GetType()), component),
                    Json = EditorJsonUtility.ToJson(component, false)
                });
            }
            return transaction;
        }

        static void Execute(PreparedOperation operation)
        {
            switch (operation.Source.operation.ToLowerInvariant())
            {
                case "reparent":
                    operation.Target.transform.SetParent(operation.Parent, false);
                    if (operation.Source.sibling_index.HasValue)
                        operation.Target.transform.SetSiblingIndex(operation.Source.sibling_index.Value);
                    break;
                case "set_active":
                    operation.Target.SetActive(operation.Source.active.Value);
                    break;
                case "set_properties":
                    UGUILayoutSerializer.ApplyProperties(operation.Component, operation.Source.properties);
                    break;
            }
        }

        static int GetDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform.parent;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }
    }
}
