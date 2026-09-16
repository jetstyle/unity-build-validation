// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
    internal static class ValidationScopeUtility
    {
        internal static bool AppliesTo(ValidationScope scope, Object target)
        {
            if (scope != ValidationScope.PrefabInstancesOnly)
                return true;

            GameObject gameObject = target is Component component ? component.gameObject : target as GameObject;
            if (gameObject == null || !PrefabUtility.IsPartOfPrefabInstance(gameObject))
                return false;

            // A variant's root represents the asset itself, not a nested placement.
            return !PrefabUtility.IsPartOfPrefabAsset(gameObject) ||
                   PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) != gameObject.transform.root.gameObject;
        }
    }
}
