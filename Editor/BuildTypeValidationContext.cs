// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using JetXR.Unity.BuildValidation;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
    public readonly struct BuildTypeValidationContext
    {
        readonly Action<ReferenceValidationSeverity, string, string> reportIssue;

        public BuildTypeValidationContext(string sourcePath, string hierarchyPath, string resolverSource, IExposedPropertyTable resolver, Object contextObject)
            : this(sourcePath, hierarchyPath, resolverSource, resolver, contextObject, null)
        {
        }

        internal BuildTypeValidationContext(string sourcePath, string hierarchyPath, string resolverSource, IExposedPropertyTable resolver, Object contextObject, Action<ReferenceValidationSeverity, string, string> reportIssue)
        {
            SourcePath = sourcePath;
            HierarchyPath = hierarchyPath;
            ResolverSource = resolverSource;
            Resolver = resolver;
            ContextObject = contextObject;
            this.reportIssue = reportIssue;
        }

        public string SourcePath { get; }
        public string HierarchyPath { get; }
        public string ResolverSource { get; }
        public IExposedPropertyTable Resolver { get; }
        public Object ContextObject { get; }

        public void Report(ReferenceValidationSeverity severity, string message, string failMessage = null)
        {
            if (!Enum.IsDefined(typeof(ReferenceValidationSeverity), severity))
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported validation severity.");

            if (reportIssue == null)
                throw new InvalidOperationException("This BuildTypeValidationContext is not connected to a validation report.");

            reportIssue(severity, message, failMessage);
        }

        public void Info(string message)
        {
            Report(ReferenceValidationSeverity.Info, message);
        }

        public void Warning(string message)
        {
            Report(ReferenceValidationSeverity.Warning, message);
        }

        public void Fatal(string message)
        {
            Report(ReferenceValidationSeverity.Fatal, message);
        }
    }
}
