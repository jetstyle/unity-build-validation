// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using UnityEngine;

namespace JetXR.Unity.BuildValidation
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class ValidateReferenceSetAttribute : PropertyAttribute
    {
        public ValidateReferenceSetAttribute()
            : this(ReferenceValidationSeverity.Fatal, null)
        {
        }

        public ValidateReferenceSetAttribute(ReferenceValidationSeverity severity)
            : this(severity, null)
        {
        }

        public ValidateReferenceSetAttribute(string failMessage)
            : this(ReferenceValidationSeverity.Fatal, failMessage)
        {
        }

        public ValidateReferenceSetAttribute(ReferenceValidationSeverity severity, string failMessage)
        {
            Severity = severity;
            FailMessage = failMessage;
        }

        public ReferenceValidationSeverity Severity { get; }
        public string FailMessage { get; }
    }
}
