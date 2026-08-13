// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using JetXR.Unity.BuildValidation;
using JetXR.Unity.BuildValidation.Editor;
using JetXR.Unity.BuildValidation.Timeline.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Tests.Editor
{
    public sealed class BuildReferenceValidatorTests
    {
        const string TestRoot = "Assets/Temp/BuildReferenceValidatorTests";
        const string ResourcesRoot = TestRoot + "/Resources";

        [SetUp]
        public void SetUp()
        {
            CreateFolderIfMissing("Assets", "Temp");
            CreateFolderIfMissing("Assets/Temp", "BuildReferenceValidatorTests");
            CreateFolderIfMissing(TestRoot, "Resources");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        public void ScriptableObjectWithFatalNullReferenceReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorTestScriptableObject>();
            string assetPath = $"{ResourcesRoot}/FatalObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorTestScriptableObject.requiredTexture)), Is.True);
        }

        [Test]
        public void PrefabMonoBehaviourWithFatalNullReferenceReportsFatal()
        {
            var gameObject = new GameObject("ValidationPrefab");
            gameObject.AddComponent<ValidatorTestBehaviour>();
            string prefabPath = $"{ResourcesRoot}/ValidationPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
            Object.DestroyImmediate(gameObject);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, prefabPath, nameof(ValidatorTestBehaviour.requiredTexture)), Is.True);
        }

        [Test]
        public void BuildSceneMonoBehaviourWithFatalNullReferenceReportsFatal()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameObject = new GameObject("SceneValidationObject");
            gameObject.AddComponent<ValidatorTestBehaviour>();
            string scenePath = $"{TestRoot}/ValidationScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsIssue(report, scenePath, nameof(ValidatorTestBehaviour.requiredTexture)), Is.True);
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void WarningReferenceDoesNotIncrementFatalCount()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorWarningScriptableObject>();
            string assetPath = $"{ResourcesRoot}/WarningObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorWarningScriptableObject.optionalTexture)), Is.True);
        }

        [Test]
        public void InfoReferenceDoesNotIncrementSummaryOrFatalCount()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInfoScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InfoObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(report.SummaryIssueCount, Is.Zero);
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorInfoScriptableObject.infoTexture)), Is.True);
        }

        [Test]
        public void ListReferenceReportsNullElementIndex()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorListScriptableObject>();
            asset.textures = new List<Texture2D> { null };
            string assetPath = $"{ResourcesRoot}/ListObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorListScriptableObject.textures), collectionIndex: 0), Is.True);
        }

        [Test]
        public void ReferenceInsideStructListReportsFullPropertyPath()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorNestedStructListScriptableObject>();
            asset.entries = new List<ValidatorNestedStructEntry>
            {
                new ValidatorNestedStructEntry(),
                new ValidatorNestedStructEntry()
            };
            string assetPath = $"{ResourcesRoot}/NestedStructList.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(CountFieldIssues(report, nameof(ValidatorNestedStructEntry.requiredTexture)), Is.EqualTo(2));
            Assert.That(ContainsPropertyIssue(report, assetPath, "entries.Array.data[0].requiredTexture"), Is.True);
            Assert.That(ContainsPropertyIssue(report, assetPath, "entries.Array.data[1].requiredTexture"), Is.True);
        }

        [Test]
        public void ReferenceInsideStructArrayReportsFullPropertyPath()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorNestedStructArrayScriptableObject>();
            asset.entries = new[] { new ValidatorNestedStructEntry() };
            string assetPath = $"{ResourcesRoot}/NestedStructArray.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsPropertyIssue(report, assetPath, "entries.Array.data[0].requiredTexture"), Is.True);
        }

        [Test]
        public void ReferenceCollectionInsideNestedStructReportsElement()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorNestedReferenceListScriptableObject>();
            asset.entry = new ValidatorNestedReferenceListEntry
            {
                requiredTextures = new List<Texture2D> { null }
            };
            string assetPath = $"{ResourcesRoot}/NestedReferenceList.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindIssue(report, assetPath, nameof(ValidatorNestedReferenceListEntry.requiredTextures), collectionIndex: 0);
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.PropertyPath, Is.EqualTo("entry.requiredTextures.Array.data[0]"));
        }

        [Test]
        public void NullNestedClassIsNotAnIssueWithoutContainerAttribute()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorNestedClassScriptableObject>();
            asset.entry = null;
            string assetPath = $"{ResourcesRoot}/NullNestedClass.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorNestedClassEntry.requiredTexture)), Is.False);
        }

        [Test]
        public void SerializeReferenceListUsesRuntimeTypeAndSkipsNullElements()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorManagedReferenceListScriptableObject>();
            asset.entries = new List<ValidatorManagedReferenceBase>
            {
                null,
                new ValidatorManagedReferenceEntry()
            };
            string assetPath = $"{ResourcesRoot}/ManagedReferenceList.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(CountFieldIssues(report, nameof(ValidatorManagedReferenceEntry.requiredTexture)), Is.EqualTo(1));
            Assert.That(ContainsPropertyIssue(report, assetPath, "entries.Array.data[1].requiredTexture"), Is.True);
        }

        [Test]
        public void FilledReferencesPass()
        {
            var texture = new Texture2D(1, 1);
            string texturePath = $"{ResourcesRoot}/Texture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);

            var asset = ScriptableObject.CreateInstance<ValidatorTestScriptableObject>();
            asset.requiredTexture = texture;
            string assetPath = $"{ResourcesRoot}/FilledObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorTestScriptableObject.requiredTexture)), Is.False);
        }

        [Test]
        public void AttributeWithoutArgumentsDefaultsToFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorDefaultSeverityScriptableObject>();
            string assetPath = $"{ResourcesRoot}/DefaultSeverityObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorDefaultSeverityScriptableObject.requiredTexture)), Is.True);
        }

        [Test]
        public void FailMessageIsFirstLogLine()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorFailMessageScriptableObject>();
            string assetPath = $"{ResourcesRoot}/FailMessageObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindIssue(report, assetPath, nameof(ValidatorFailMessageScriptableObject.requiredTexture));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.ToString(), Does.StartWith(ValidatorFailMessageScriptableObject.Message + "\n"));
        }

        [Test]
        public void ValidateInvokePassDoesNotCreateIssue()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokePassScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokePassObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokePassScriptableObject.OnBuildValidate)), Is.False);
        }

        [Test]
        public void ValidateInvokeInfoDoesNotIncrementSummaryOrFatalCount()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeInfoScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeInfoObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(report.SummaryIssueCount, Is.Zero);
            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokeInfoScriptableObject.OnBuildValidate)), Is.True);
        }

        [Test]
        public void ValidateInvokeWarningIncrementsSummaryOnly()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeWarningScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeWarningObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.Zero);
            Assert.That(report.SummaryIssueCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokeWarningScriptableObject.OnBuildValidate)), Is.True);
        }

        [Test]
        public void ValidateInvokeFatalIncrementsSummaryAndFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeFatalScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeFatalObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(report.SummaryIssueCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(ContainsMethodIssue(report, assetPath, nameof(ValidatorInvokeFatalScriptableObject.OnBuildValidate)), Is.True);
        }

        [Test]
        public void ValidateInvokePrivateMethodIsCalled()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokePrivateScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokePrivateObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsMethodIssue(report, assetPath, "OnBuildValidate"), Is.True);
        }

        [Test]
        public void ValidateInvokeWrongSignatureReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeWrongSignatureScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeWrongSignatureObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindMethodIssue(report, assetPath, nameof(ValidatorInvokeWrongSignatureScriptableObject.OnBuildValidate));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
        }

        [Test]
        public void ValidateInvokeExceptionReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorInvokeExceptionScriptableObject>();
            string assetPath = $"{ResourcesRoot}/InvokeExceptionObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindMethodIssue(report, assetPath, nameof(ValidatorInvokeExceptionScriptableObject.OnBuildValidate));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
        }

        [Test]
        public void UnsupportedReferenceFieldTypeReportsFatal()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorUnsupportedFieldScriptableObject>();
            string assetPath = $"{ResourcesRoot}/UnsupportedFieldObject.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            BuildReferenceValidator.ValidationIssue issue = FindIssue(report, assetPath, nameof(ValidatorUnsupportedFieldScriptableObject.unsupportedField));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
        }

        [Test]
        public void UnsupportedReferenceFieldTypeReportsOncePerField()
        {
            var firstAsset = ScriptableObject.CreateInstance<ValidatorUnsupportedFieldScriptableObject>();
            var secondAsset = ScriptableObject.CreateInstance<ValidatorUnsupportedFieldScriptableObject>();
            AssetDatabase.CreateAsset(firstAsset, $"{ResourcesRoot}/UnsupportedFieldFirst.asset");
            AssetDatabase.CreateAsset(secondAsset, $"{ResourcesRoot}/UnsupportedFieldSecond.asset");

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(CountFieldIssues(report, nameof(ValidatorUnsupportedFieldScriptableObject.unsupportedField)), Is.EqualTo(1));
        }

        [Test]
        public void ValidateInvokeWrongSignatureReportsOncePerMethod()
        {
            var firstAsset = ScriptableObject.CreateInstance<ValidatorInvokeWrongSignatureScriptableObject>();
            var secondAsset = ScriptableObject.CreateInstance<ValidatorInvokeWrongSignatureScriptableObject>();
            AssetDatabase.CreateAsset(firstAsset, $"{ResourcesRoot}/InvokeWrongSignatureFirst.asset");
            AssetDatabase.CreateAsset(secondAsset, $"{ResourcesRoot}/InvokeWrongSignatureSecond.asset");

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(CountMethodIssues(report, nameof(ValidatorInvokeWrongSignatureScriptableObject.OnBuildValidate)), Is.EqualTo(1));
        }

        [Test]
        public void StandaloneExposedReferenceDoesNotReportMissingReference()
        {
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{ResourcesRoot}/StandaloneExposedReference.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

            Assert.That(ContainsIssue(report, assetPath, nameof(ValidatorExposedReferencePlayableAsset.targetTransform)), Is.False);
        }

        [Test]
        public void PlayableDirectorMissingExposedReferenceReportsIssue()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{TestRoot}/DirectorMissingExposedReference.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            director.playableAsset = asset;
            string scenePath = $"{TestRoot}/DirectorMissingExposedReference.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                BuildReferenceValidator.ValidationIssue issue = FindTimelineValidatorIssue(report, scenePath);
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Context, Is.EqualTo(director));
                Assert.That(issue.Message, Does.Contain(assetPath));
                Assert.That(issue.Message, Does.Contain(nameof(ValidatorExposedReferencePlayableAsset.targetTransform)));
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void PlayableDirectorResolvedExposedReferencePasses()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            Transform target = new GameObject("Target").transform;
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{TestRoot}/DirectorResolvedExposedReference.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            director.playableAsset = asset;
            director.SetReferenceValue(asset.targetTransform.exposedName, target);
            string scenePath = $"{TestRoot}/DirectorResolvedExposedReference.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsTimelineValidatorIssue(report, scenePath), Is.False);
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void PlayableDirectorValidatesExposedReferenceInsideStructList()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            var asset = ScriptableObject.CreateInstance<ValidatorNestedExposedReferencePlayableAsset>();
            asset.entries = new List<ValidatorNestedExposedReferenceEntry>
            {
                new ValidatorNestedExposedReferenceEntry
                {
                    targetTransform = new ExposedReference<Transform> { exposedName = "nestedTarget" }
                }
            };
            string assetPath = $"{TestRoot}/NestedExposedReference.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            director.playableAsset = asset;
            string scenePath = $"{TestRoot}/NestedExposedReference.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                BuildReferenceValidator.ValidationIssue issue = FindTimelineValidatorIssue(report, scenePath);
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Message, Does.Contain("entries.Array.data[0].targetTransform"));
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void PlayableDirectorResolvesExposedReferenceInsideStructList()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            Transform target = new GameObject("Target").transform;
            var asset = ScriptableObject.CreateInstance<ValidatorNestedExposedReferencePlayableAsset>();
            asset.entries = new List<ValidatorNestedExposedReferenceEntry>
            {
                new ValidatorNestedExposedReferenceEntry
                {
                    targetTransform = new ExposedReference<Transform> { exposedName = "nestedTarget" }
                }
            };
            string assetPath = $"{TestRoot}/ResolvedNestedExposedReference.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            director.playableAsset = asset;
            director.SetReferenceValue(asset.entries[0].targetTransform.exposedName, target);
            string scenePath = $"{TestRoot}/ResolvedNestedExposedReference.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsTimelineValidatorIssue(report, scenePath), Is.False);
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void SamePlayableAssetIsValidatedPerPlayableDirectorResolver()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var firstDirectorObject = new GameObject("FirstDirector");
            var secondDirectorObject = new GameObject("SecondDirector");
            PlayableDirector firstDirector = firstDirectorObject.AddComponent<PlayableDirector>();
            PlayableDirector secondDirector = secondDirectorObject.AddComponent<PlayableDirector>();
            Transform target = new GameObject("Target").transform;
            var asset = ScriptableObject.CreateInstance<ValidatorExposedReferencePlayableAsset>();
            asset.targetTransform.exposedName = "target";
            string assetPath = $"{TestRoot}/SharedPlayableAsset.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            firstDirector.playableAsset = asset;
            secondDirector.playableAsset = asset;
            firstDirector.SetReferenceValue(asset.targetTransform.exposedName, target);
            string scenePath = $"{TestRoot}/SharedPlayableAsset.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(CountTimelineValidatorIssues(report), Is.EqualTo(1));
                BuildReferenceValidator.ValidationIssue issue = FindTimelineValidatorIssue(report, scenePath);
                Assert.That(issue.Context, Is.EqualTo(secondDirector));
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void TimelineReferencesValidatorReportsMultipleMissingExposedReferences()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            var asset = ScriptableObject.CreateInstance<ValidatorMultipleExposedReferencesPlayableAsset>();
            asset.firstTarget.exposedName = "firstTarget";
            asset.secondTarget.exposedName = "secondTarget";
            string assetPath = $"{TestRoot}/MultipleExposedReferences.playable";
            AssetDatabase.CreateAsset(asset, assetPath);
            director.playableAsset = asset;
            string scenePath = $"{TestRoot}/MultipleExposedReferences.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(CountTimelineValidatorIssues(report), Is.EqualTo(2));
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void PlayableDirectorMissingTimelineBindingReportsFatal()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.CreateTrack<AnimationTrack>(null, "Animation Track");
            string assetPath = $"{TestRoot}/MissingTimelineBinding.playable";
            AssetDatabase.CreateAsset(timeline, assetPath);
            director.playableAsset = timeline;
            string scenePath = $"{TestRoot}/MissingTimelineBinding.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                BuildReferenceValidator.ValidationIssue issue = FindTimelineValidatorIssue(report, scenePath);
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
                Assert.That(issue.Message, Does.Contain(assetPath));
                Assert.That(issue.Message, Does.Contain("Animation Track"));
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void PlayableDirectorFilledTimelineBindingPasses()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var directorObject = new GameObject("Director");
            PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
            Animator animator = new GameObject("Animator").AddComponent<Animator>();
            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            TrackAsset track = timeline.CreateTrack<AnimationTrack>(null, "Animation Track");
            string assetPath = $"{TestRoot}/FilledTimelineBinding.playable";
            AssetDatabase.CreateAsset(timeline, assetPath);
            director.playableAsset = timeline;
            director.SetGenericBinding(track, animator);
            string scenePath = $"{TestRoot}/FilledTimelineBinding.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, enabled: true) };

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsTimelineValidatorIssue(report, scenePath), Is.False);
            }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
            }
        }

        [Test]
        public void BuildTypeValidatorForExactTypeRuns()
        {
            Type validatorType = typeof(ExactBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeExactTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeExactTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.True);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void MultipleBuildTypeValidatorsForSameTypeAllRun()
        {
            Type firstValidatorType = typeof(FirstMultipleBuildTypeValidator);
            Type secondValidatorType = typeof(SecondMultipleBuildTypeValidator);
            bool firstWasEnabled = SetBuildTypeValidatorEnabled(firstValidatorType, enabled: true);
            bool secondWasEnabled = SetBuildTypeValidatorEnabled(secondValidatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeMultipleTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeMultipleTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsValidatorIssue(report, assetPath, firstValidatorType), Is.True);
                Assert.That(ContainsValidatorIssue(report, assetPath, secondValidatorType), Is.True);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(firstValidatorType, firstWasEnabled);
                SetBuildTypeValidatorEnabled(secondValidatorType, secondWasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorPassDoesNotCreateIssue()
        {
            Type validatorType = typeof(PassBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypePassTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypePassTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.False);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorInfoDoesNotIncrementSummaryOrFatalCount()
        {
            Type validatorType = typeof(InfoBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeInfoTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeInfoTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(report.FatalCount, Is.Zero);
                Assert.That(report.SummaryIssueCount, Is.Zero);
                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.True);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorWarningIncrementsSummaryOnly()
        {
            Type validatorType = typeof(WarningBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeWarningTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeWarningTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(report.FatalCount, Is.Zero);
                Assert.That(report.SummaryIssueCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.True);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorFatalIncrementsSummaryAndFatal()
        {
            Type validatorType = typeof(FatalBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeFatalTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeFatalTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(report.FatalCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(report.SummaryIssueCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.True);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void DisabledBuildTypeValidatorDoesNotRun()
        {
            Type validatorType = typeof(DisabledBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: false);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeDisabledTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeDisabledTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.False);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorUseForChildrenAppliesToDerivedType()
        {
            Type validatorType = typeof(ChildrenBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeChildrenDerivedScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeChildrenDerived.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.True);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorUseForChildrenFalseDoesNotApplyToDerivedType()
        {
            Type validatorType = typeof(NoChildrenBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeNoChildrenDerivedScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeNoChildrenDerived.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                Assert.That(ContainsValidatorIssue(report, assetPath, validatorType), Is.False);
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorExceptionReportsFatal()
        {
            Type validatorType = typeof(ExceptionBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                var asset = ScriptableObject.CreateInstance<BuildTypeExceptionTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeExceptionTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                BuildReferenceValidator.ValidationIssue issue = FindValidatorIssue(report, assetPath, validatorType);
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Fatal));
            }
            finally
            {
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        [Test]
        public void BuildTypeValidatorReceivesStoredSettings()
        {
            Type validatorType = typeof(SettingsBuildTypeValidator);
            bool wasEnabled = SetBuildTypeValidatorEnabled(validatorType, enabled: true);
            try
            {
                BuildTypeValidatorDescriptor descriptor = FindBuildTypeValidatorDescriptor(validatorType);
                var settings = (SettingsBuildTypeValidatorSettings)BuildValidationSettings.instance.GetOrCreateBuildTypeValidatorSettings(descriptor);
                settings.shouldReport = true;
                settings.message = "Configured validator message.";
                settings.severity = ReferenceValidationSeverity.Warning;
                BuildValidationSettings.instance.SaveSettings();

                var asset = ScriptableObject.CreateInstance<BuildTypeSettingsTargetScriptableObject>();
                string assetPath = $"{ResourcesRoot}/BuildTypeSettingsTarget.asset";
                AssetDatabase.CreateAsset(asset, assetPath);

                BuildReferenceValidator.ValidationReport report = BuildReferenceValidator.ValidateBuildContent(logResults: false);

                BuildReferenceValidator.ValidationIssue issue = FindValidatorIssue(report, assetPath, validatorType);
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Severity, Is.EqualTo(ReferenceValidationSeverity.Warning));
                Assert.That(issue.Message, Is.EqualTo(settings.message));
            }
            finally
            {
                BuildTypeValidatorDescriptor descriptor = FindBuildTypeValidatorDescriptor(validatorType);
                var settings = (SettingsBuildTypeValidatorSettings)BuildValidationSettings.instance.GetOrCreateBuildTypeValidatorSettings(descriptor);
                settings.shouldReport = false;
                settings.message = "Default validator message.";
                settings.severity = ReferenceValidationSeverity.Fatal;
                BuildValidationSettings.instance.SaveSettings();
                SetBuildTypeValidatorEnabled(validatorType, wasEnabled);
            }
        }

        static bool ContainsIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string fieldName, int? collectionIndex = null)
        {
            return FindIssue(report, sourcePath, fieldName, collectionIndex) != null;
        }

        static bool ContainsMethodIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string methodName)
        {
            return FindMethodIssue(report, sourcePath, methodName) != null;
        }

        static bool ContainsPropertyIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string propertyPath)
        {
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.SourcePath == sourcePath && issue.PropertyPath == propertyPath)
                    return true;
            }

            return false;
        }

        static BuildReferenceValidator.ValidationIssue FindIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string fieldName, int? collectionIndex = null)
        {
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.SourcePath == sourcePath && issue.FieldName == fieldName && issue.CollectionIndex == collectionIndex)
                    return issue;
            }

            return null;
        }

        static BuildReferenceValidator.ValidationIssue FindMethodIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, string methodName)
        {
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.SourcePath == sourcePath && issue.MethodName == methodName)
                    return issue;
            }

            return null;
        }

        static int CountFieldIssues(BuildReferenceValidator.ValidationReport report, string fieldName)
        {
            int count = 0;
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.FieldName == fieldName)
                    count++;
            }

            return count;
        }

        static int CountMethodIssues(BuildReferenceValidator.ValidationReport report, string methodName)
        {
            int count = 0;
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.MethodName == methodName)
                    count++;
            }

            return count;
        }

        static bool ContainsValidatorIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, Type validatorType)
        {
            return FindValidatorIssue(report, sourcePath, validatorType) != null;
        }

        static bool ContainsTimelineValidatorIssue(BuildReferenceValidator.ValidationReport report, string sourcePath)
        {
            return FindTimelineValidatorIssue(report, sourcePath) != null;
        }

        static BuildReferenceValidator.ValidationIssue FindTimelineValidatorIssue(BuildReferenceValidator.ValidationReport report, string sourcePath)
        {
            return FindValidatorIssue(report, sourcePath, typeof(TimelineReferencesValidator));
        }

        static BuildReferenceValidator.ValidationIssue FindValidatorIssue(BuildReferenceValidator.ValidationReport report, string sourcePath, Type validatorType)
        {
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.SourcePath == sourcePath && issue.ValidatorName == validatorType.FullName)
                    return issue;
            }

            return null;
        }

        static int CountTimelineValidatorIssues(BuildReferenceValidator.ValidationReport report)
        {
            int count = 0;
            foreach (BuildReferenceValidator.ValidationIssue issue in report.Issues)
            {
                if (issue.ValidatorName == typeof(TimelineReferencesValidator).FullName)
                    count++;
            }

            return count;
        }

        static bool SetBuildTypeValidatorEnabled(Type validatorType, bool enabled)
        {
            BuildTypeValidatorDescriptor descriptor = FindBuildTypeValidatorDescriptor(validatorType);
            bool wasEnabled = BuildValidationSettings.instance.IsBuildTypeValidatorEnabled(descriptor.Id);
            BuildValidationSettings.instance.SetBuildTypeValidatorEnabled(descriptor.Id, enabled);
            return wasEnabled;
        }

        static BuildTypeValidatorDescriptor FindBuildTypeValidatorDescriptor(Type validatorType)
        {
            BuildTypeValidatorRegistry.Refresh();
            foreach (BuildTypeValidatorDescriptor descriptor in BuildTypeValidatorRegistry.Descriptors)
            {
                if (descriptor.ValidatorType == validatorType)
                    return descriptor;
            }

            Assert.Fail($"Build type validator descriptor was not found for {validatorType.FullName}.");
            return null;
        }

        static void CreateFolderIfMissing(string parentFolder, string folderName)
        {
            string path = $"{parentFolder}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parentFolder, folderName);
        }
    }

    public sealed class ValidatorTestScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorWarningScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Warning)]
        public Texture2D optionalTexture;
    }

    public sealed class ValidatorInfoScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Info)]
        public Texture2D infoTexture;
    }

    public sealed class ValidatorListScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
        public List<Texture2D> textures;
    }

    [Serializable]
    public struct ValidatorNestedStructEntry
    {
        [ValidateReferenceSet]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorNestedStructListScriptableObject : ScriptableObject
    {
        public List<ValidatorNestedStructEntry> entries;
    }

    public sealed class ValidatorNestedStructArrayScriptableObject : ScriptableObject
    {
        public ValidatorNestedStructEntry[] entries;
    }

    [Serializable]
    public struct ValidatorNestedReferenceListEntry
    {
        [ValidateReferenceSet]
        public List<Texture2D> requiredTextures;
    }

    public sealed class ValidatorNestedReferenceListScriptableObject : ScriptableObject
    {
        public ValidatorNestedReferenceListEntry entry;
    }

    [Serializable]
    public sealed class ValidatorNestedClassEntry
    {
        [ValidateReferenceSet]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorNestedClassScriptableObject : ScriptableObject
    {
        [SerializeReference]
        public ValidatorNestedClassEntry entry;
    }

    [Serializable]
    public abstract class ValidatorManagedReferenceBase
    {
    }

    [Serializable]
    public sealed class ValidatorManagedReferenceEntry : ValidatorManagedReferenceBase
    {
        [ValidateReferenceSet]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorManagedReferenceListScriptableObject : ScriptableObject
    {
        [SerializeReference]
        public List<ValidatorManagedReferenceBase> entries;
    }

    public sealed class ValidatorDefaultSeverityScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorFailMessageScriptableObject : ScriptableObject
    {
        public const string Message = "Texture must be assigned before building.";

        [ValidateReferenceSet(Message)]
        public Texture2D requiredTexture;
    }

    public sealed class ValidatorInvokePassScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Pass();
        }
    }

    public sealed class ValidatorInvokeInfoScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Info("Informational validation message.");
        }
    }

    public sealed class ValidatorInvokeWarningScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Warning("Warning validation message.");
        }
    }

    public sealed class ValidatorInvokeFatalScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Fatal("Fatal validation message.");
        }
    }

    public sealed class ValidatorInvokePrivateScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        BuildValidationResult OnBuildValidate()
        {
            return BuildValidationResult.Warning("Private validation method was called.");
        }
    }

    public sealed class ValidatorInvokeWrongSignatureScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public void OnBuildValidate()
        {
        }
    }

    public sealed class ValidatorInvokeExceptionScriptableObject : ScriptableObject
    {
        [ValidateInvoke]
        public BuildValidationResult OnBuildValidate()
        {
            throw new System.InvalidOperationException("Validation exception.");
        }
    }

    public sealed class ValidatorUnsupportedFieldScriptableObject : ScriptableObject
    {
        [ValidateReferenceSet]
        public string unsupportedField;
    }

    public sealed class ValidatorExposedReferencePlayableAsset : PlayableAsset
    {
        [ValidateReferenceSet]
        public ExposedReference<Transform> targetTransform;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Null;
        }
    }

    public sealed class ValidatorMultipleExposedReferencesPlayableAsset : PlayableAsset
    {
        [ValidateReferenceSet]
        public ExposedReference<Transform> firstTarget;

        [ValidateReferenceSet]
        public ExposedReference<Transform> secondTarget;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Null;
        }
    }

    [Serializable]
    public struct ValidatorNestedExposedReferenceEntry
    {
        [ValidateReferenceSet]
        public ExposedReference<Transform> targetTransform;
    }

    public sealed class ValidatorNestedExposedReferencePlayableAsset : PlayableAsset
    {
        public List<ValidatorNestedExposedReferenceEntry> entries;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Null;
        }
    }

    public sealed class BuildTypeExactTargetScriptableObject : ScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeExactTargetScriptableObject))]
    public sealed class ExactBuildTypeValidator : BuildTypeValidator<BuildTypeExactTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeExactTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Warning("Exact build type validator ran.");
        }
    }

    public sealed class BuildTypeMultipleTargetScriptableObject : ScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeMultipleTargetScriptableObject))]
    public sealed class FirstMultipleBuildTypeValidator : BuildTypeValidator<BuildTypeMultipleTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeMultipleTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Warning("First build type validator ran.");
        }
    }

    [BuildTypeValidator(typeof(BuildTypeMultipleTargetScriptableObject))]
    public sealed class SecondMultipleBuildTypeValidator : BuildTypeValidator<BuildTypeMultipleTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeMultipleTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Warning("Second build type validator ran.");
        }
    }

    public sealed class BuildTypeInfoTargetScriptableObject : ScriptableObject
    {
    }

    public sealed class BuildTypePassTargetScriptableObject : ScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypePassTargetScriptableObject))]
    public sealed class PassBuildTypeValidator : BuildTypeValidator<BuildTypePassTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypePassTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Pass();
        }
    }

    [BuildTypeValidator(typeof(BuildTypeInfoTargetScriptableObject))]
    public sealed class InfoBuildTypeValidator : BuildTypeValidator<BuildTypeInfoTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeInfoTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Info("Info build type validator ran.");
        }
    }

    public sealed class BuildTypeWarningTargetScriptableObject : ScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeWarningTargetScriptableObject))]
    public sealed class WarningBuildTypeValidator : BuildTypeValidator<BuildTypeWarningTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeWarningTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Warning("Warning build type validator ran.");
        }
    }

    public sealed class BuildTypeFatalTargetScriptableObject : ScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeFatalTargetScriptableObject))]
    public sealed class FatalBuildTypeValidator : BuildTypeValidator<BuildTypeFatalTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeFatalTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Fatal("Fatal build type validator ran.");
        }
    }

    public sealed class BuildTypeDisabledTargetScriptableObject : ScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeDisabledTargetScriptableObject))]
    public sealed class DisabledBuildTypeValidator : BuildTypeValidator<BuildTypeDisabledTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeDisabledTargetScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Fatal("Disabled build type validator should not run.");
        }
    }

    public class BuildTypeChildrenBaseScriptableObject : ScriptableObject
    {
    }

    public sealed class BuildTypeChildrenDerivedScriptableObject : BuildTypeChildrenBaseScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeChildrenBaseScriptableObject), useForChildren: true)]
    public sealed class ChildrenBuildTypeValidator : BuildTypeValidator<BuildTypeChildrenBaseScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeChildrenBaseScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Warning("Children build type validator ran.");
        }
    }

    public class BuildTypeNoChildrenBaseScriptableObject : ScriptableObject
    {
    }

    public sealed class BuildTypeNoChildrenDerivedScriptableObject : BuildTypeNoChildrenBaseScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeNoChildrenBaseScriptableObject))]
    public sealed class NoChildrenBuildTypeValidator : BuildTypeValidator<BuildTypeNoChildrenBaseScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeNoChildrenBaseScriptableObject target, BuildTypeValidationContext context)
        {
            return BuildValidationResult.Warning("No-children build type validator should not run for derived type.");
        }
    }

    public sealed class BuildTypeExceptionTargetScriptableObject : ScriptableObject
    {
    }

    [BuildTypeValidator(typeof(BuildTypeExceptionTargetScriptableObject))]
    public sealed class ExceptionBuildTypeValidator : BuildTypeValidator<BuildTypeExceptionTargetScriptableObject>
    {
        protected override BuildValidationResult Validate(BuildTypeExceptionTargetScriptableObject target, BuildTypeValidationContext context)
        {
            throw new InvalidOperationException("Build type validator exception.");
        }
    }

    public sealed class BuildTypeSettingsTargetScriptableObject : ScriptableObject
    {
    }

    [Serializable]
    public sealed class SettingsBuildTypeValidatorSettings : BuildTypeValidatorSettings
    {
        public bool shouldReport;
        public string message = "Default validator message.";
        public ReferenceValidationSeverity severity = ReferenceValidationSeverity.Fatal;
    }

    [BuildTypeValidator(typeof(BuildTypeSettingsTargetScriptableObject))]
    public sealed class SettingsBuildTypeValidator : BuildTypeValidator<BuildTypeSettingsTargetScriptableObject, SettingsBuildTypeValidatorSettings>
    {
        protected override BuildValidationResult Validate(BuildTypeSettingsTargetScriptableObject target, SettingsBuildTypeValidatorSettings settings, BuildTypeValidationContext context)
        {
            return settings.shouldReport ? BuildValidationResult.Issue(settings.severity, settings.message) : BuildValidationResult.Pass();
        }
    }
}
