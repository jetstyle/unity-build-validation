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
            : this(ValidationScope.All, severity, failMessage)
        {
        }

        public ValidateReferenceSetAttribute(ValidationScope scope)
            : this(scope, ReferenceValidationSeverity.Fatal, null)
        {
        }

        public ValidateReferenceSetAttribute(ValidationScope scope, string failMessage)
            : this(scope, ReferenceValidationSeverity.Fatal, failMessage)
        {
        }

        public ValidateReferenceSetAttribute(ValidationScope scope, ReferenceValidationSeverity severity, string failMessage = null)
        {
            Scope = scope;
            Severity = severity;
            FailMessage = failMessage;
        }

        public ValidationScope Scope { get; }
        public ReferenceValidationSeverity Severity { get; }
        public string FailMessage { get; }
    }
}
