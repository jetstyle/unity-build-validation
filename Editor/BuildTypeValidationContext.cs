// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
    public readonly struct BuildTypeValidationContext
    {
        public BuildTypeValidationContext(string sourcePath, string hierarchyPath, string resolverSource, IExposedPropertyTable resolver, Object contextObject)
        {
            SourcePath = sourcePath;
            HierarchyPath = hierarchyPath;
            ResolverSource = resolverSource;
            Resolver = resolver;
            ContextObject = contextObject;
        }

        public string SourcePath { get; }
        public string HierarchyPath { get; }
        public string ResolverSource { get; }
        public IExposedPropertyTable Resolver { get; }
        public Object ContextObject { get; }
    }
}
