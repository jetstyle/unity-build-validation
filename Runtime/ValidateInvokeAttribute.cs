// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;

namespace JetXR.Unity.BuildValidation
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ValidateInvokeAttribute : Attribute
    {
        public ValidateInvokeAttribute()
            : this(ValidationScope.All)
        {
        }

        public ValidateInvokeAttribute(ValidationScope scope)
        {
            Scope = scope;
        }

        public ValidationScope Scope { get; }
    }
}
