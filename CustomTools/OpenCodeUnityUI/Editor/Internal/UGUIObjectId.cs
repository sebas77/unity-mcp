using System;
using MCPForUnity.Runtime.Helpers;
using UnityEngine;

namespace OpenCode.UnityUI.Editor
{
    public static class UGUIObjectId
    {
        public static int GetLegacyId(UnityEngine.Object value)
        {
            return value.GetInstanceIDCompat();
        }

        public static string GetSessionId(UnityEngine.Object value)
        {
#if UNITY_6000_5_OR_NEWER
            return EntityId.ToULong(value.GetEntityId()).ToString();
#else
            return value.GetInstanceID().ToString();
#endif
        }

        public static bool MatchesSelector(UnityEngine.Object value, string selector)
        {
            if (string.Equals(GetSessionId(value), selector, StringComparison.Ordinal))
                return true;
            return int.TryParse(selector, out int legacyId) && GetLegacyId(value) == legacyId;
        }
    }
}
