// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System.Linq;
using JetXR.Unity.BuildValidation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Tests.Editor
{
    public sealed class ValidationScopeTests
    {
        const string Root = "Assets/ValidationScopeTests";
        EditorBuildSettingsScene[] previousScenes;
        Scene scene;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "ValidationScopeTests");
            AssetDatabase.CreateFolder(Root, "Resources");
            previousScenes = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            EditorBuildSettings.scenes = previousScenes;
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.DeleteAsset(Root);
        }

        GameObject CreatePrefab()
        {
            var root = new GameObject("Source");
            SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("InactiveChild");
            child.transform.SetParent(root.transform);
            child.AddComponent<ValidatorInstanceScopeBehaviour>();
            child.SetActive(false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Resources/Source.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PrefabAssetSkipsInstanceRulesButKeepsDefaultRules(bool variant)
        {
            GameObject prefab = CreatePrefab();
            string path = AssetDatabase.GetAssetPath(prefab);
            if (variant)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                path = Root + "/Resources/Variant.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
            }
            AssertIssues(path, false, true);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SceneRulesDependOnPrefabInstanceAndAssignedReference(bool isInstance, bool assigned)
        {
            GameObject source = CreatePrefab();
            GameObject target;
            if (isInstance)
                target = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            else
            {
                target = new GameObject("OrdinaryObject");
                SceneManager.MoveGameObjectToScene(target, scene);
                target.AddComponent<ValidatorInstanceScopeBehaviour>();
            }
            if (assigned)
                target.GetComponentInChildren<ValidatorInstanceScopeBehaviour>(true).instanceTarget = target.transform;
            string path = Root + "/Scene.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            AssertIssues(path, isInstance && !assigned, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedPrefabInstanceIsCheckedInsideAnotherPrefab(bool assigned)
        {
            GameObject source = CreatePrefab();
            var container = new GameObject("Container");
            SceneManager.MoveGameObjectToScene(container, scene);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            instance.transform.SetParent(container.transform);
            if (assigned)
            {
                var behaviour = instance.GetComponentInChildren<ValidatorInstanceScopeBehaviour>(true);
                behaviour.instanceTarget = container.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
            }
            string path = Root + "/Resources/Container.prefab";
            PrefabUtility.SaveAsPrefabAsset(container, path);
            Object.DestroyImmediate(container);
            AssertIssues(path, !assigned, true);
        }

        [Test]
        public void TimelineScopeUsesDirectorInstanceInsteadOfPlayableAsset()
        {
            var playable = ScriptableObject.CreateInstance<InstanceScopePlayableAsset>();
            playable.target.exposedName = "target";
            AssetDatabase.CreateAsset(playable, Root + "/Scoped.playable");
            var directorObject = new GameObject("Director");
            SceneManager.MoveGameObjectToScene(directorObject, scene);
            directorObject.AddComponent<PlayableDirector>().playableAsset = playable;
            string prefabPath = Root + "/Resources/Director.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(directorObject, prefabPath);
            Object.DestroyImmediate(directorObject);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            string scenePath = Root + "/DirectorScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            var issues = BuildReferenceValidator.ValidateBuildContent(false).Issues;
            Assert.That(issues.Any(issue => issue.SourcePath == prefabPath), Is.False);
            Assert.That(issues.Any(issue => issue.SourcePath == scenePath && issue.Message.Contains("Timeline exposed reference")), Is.True);
        }

        static void AssertIssues(string path, bool instanceIssue, bool defaultIssue)
        {
            var issues = BuildReferenceValidator.ValidateBuildContent(false).Issues.Where(issue => issue.SourcePath == path).ToArray();
            Assert.That(issues.Any(issue => issue.FieldName == "instanceTarget"), Is.EqualTo(instanceIssue));
            Assert.That(issues.Any(issue => issue.MethodName == "ValidateInstance"), Is.EqualTo(instanceIssue));
            Assert.That(issues.Any(issue => issue.FieldName == "alwaysTarget"), Is.EqualTo(defaultIssue));
        }
    }

    public sealed class InstanceScopePlayableAsset : PlayableAsset
    {
        [ValidateReferenceSet(ValidationScope.PrefabInstancesOnly)]
        public ExposedReference<Transform> target;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Null;
    }
}
