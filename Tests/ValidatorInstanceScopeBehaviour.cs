// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using UnityEngine;

namespace JetXR.Unity.BuildValidation.Tests.Editor
{
    public sealed class ValidatorInstanceScopeBehaviour : MonoBehaviour
    {
        [ValidateReferenceSet(ValidationScope.PrefabInstancesOnly)]
        public Transform instanceTarget;

        [ValidateReferenceSet]
        public Transform alwaysTarget;

        [ValidateInvoke(ValidationScope.PrefabInstancesOnly)]
        public BuildValidationResult ValidateInstance()
        {
            return instanceTarget == null ? BuildValidationResult.Fatal("Instance target missing.") : BuildValidationResult.Pass();
        }
    }
}
