// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;

namespace JetXR.Unity.BuildValidation
{
    public readonly struct BuildValidationResult
    {
        BuildValidationResult(bool passed, ReferenceValidationSeverity severity, string message)
        {
            Passed = passed;
            Severity = severity;
            Message = message;
        }

        public bool Passed { get; }
        public ReferenceValidationSeverity Severity { get; }
        public string Message { get; }

        public static BuildValidationResult Pass()
        {
            return new BuildValidationResult(true, ReferenceValidationSeverity.Info, null);
        }

        public static BuildValidationResult Info(string message)
        {
            return Issue(ReferenceValidationSeverity.Info, message);
        }

        public static BuildValidationResult Warning(string message)
        {
            return Issue(ReferenceValidationSeverity.Warning, message);
        }

        public static BuildValidationResult Fatal(string message)
        {
            return Issue(ReferenceValidationSeverity.Fatal, message);
        }

        public static BuildValidationResult Issue(ReferenceValidationSeverity severity, string message)
        {
            if (!Enum.IsDefined(typeof(ReferenceValidationSeverity), severity))
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported validation severity.");

            return new BuildValidationResult(false, severity, message);
        }
    }
}
