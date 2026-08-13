// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using JetXR.Unity.BuildValidation;
using UnityEngine;

namespace JetXR.Unity.BuildValidation.Tests.Editor
{
    public sealed class ValidatorTestBehaviour : MonoBehaviour
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
        public Texture2D requiredTexture;
    }
}
