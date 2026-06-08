# BuildValidation

BuildValidation is a JetXR Unity package for checking project-specific validation rules before a player build is created.

The package adds attributes that mark serialized Unity object references as required and validation methods as build checks. During a build, BuildValidation scans build content and reports every issue it finds. Fatal validation issues stop the build after the scan is complete.

## Installation

Install the package through Unity Package Manager from the Git repository:

```text
https://github.com/jetstyle/unity-build-validation.git
```

In Unity:

1. Open `Window > Package Manager`.
2. Click `+`.
3. Select `Add package from git URL...`.
4. Enter the repository URL.

The package name is:

```text
style.jetxr.buildvalidation
```

## Usage

Add the namespace to a runtime script:

```csharp
using JetXR.Unity.BuildValidation;
using UnityEngine;

public sealed class ExampleMapSettings : MonoBehaviour
{
    [ValidateReferenceSet]
    public Texture2D requiredMap;
}
```

When no severity is provided, the attribute uses `ReferenceValidationSeverity.Fatal`.

The attribute can be used on serialized fields that store Unity object references:

```csharp
[ValidateReferenceSet(ReferenceValidationSeverity.Fatal)]
[SerializeField] Material requiredMaterial;

[ValidateReferenceSet("Main audio clip is required for this asset.")]
public AudioClip mainClip;

[ValidateReferenceSet(ReferenceValidationSeverity.Info)]
public AudioClip[] clips;

[ValidateReferenceSet(ReferenceValidationSeverity.Warning)]
public List<GameObject> prefabs;
```

Fields must be serialized by Unity. Public fields and private fields with `[SerializeField]` are supported. Static fields and non-serialized fields are ignored.

`ExposedReference<T>` fields are supported when `T` is a Unity object type. BuildValidation resolves them through an `IExposedPropertyTable` context. In scenes this commonly comes from `PlayableDirector`. Standalone assets without a resolver context are not failed for unresolved exposed references.

Use `[ValidateInvoke]` when validation logic needs code:

```csharp
using JetXR.Unity.BuildValidation;
using UnityEngine;

public sealed class ExampleMapSettings : MonoBehaviour
{
    public Texture2D requiredMap;

    [ValidateInvoke]
    private BuildValidationResult OnBuildValidate()
    {
        if (requiredMap == null)
            return BuildValidationResult.Fatal("Required map is missing.");

        return BuildValidationResult.Pass();
    }
}
```

Methods marked with `[ValidateInvoke]` must be instance methods without parameters and must return `BuildValidationResult`. They can be public, protected, or private. BuildValidation calls these methods explicitly before the build; Unity runtime lifecycle methods such as `Awake`, `Start`, and `OnEnable` are not part of the validation contract.

Use `BuildTypeValidator` when validation logic should live in an editor-only script and apply to every object of a specific Unity type:

```csharp
using JetXR.Unity.BuildValidation;
using JetXR.Unity.BuildValidation.Editor;
using UnityEngine;

[BuildTypeValidator(typeof(ExampleMapSettings))]
public sealed class ExampleMapSettingsBuildTypeValidator : BuildTypeValidator<ExampleMapSettings>
{
    protected override BuildValidationResult Validate(ExampleMapSettings target, BuildTypeValidationContext context)
    {
        if (target.requiredMap == null)
            return BuildValidationResult.Fatal("Required map is missing.");

        return BuildValidationResult.Pass();
    }
}
```

`BuildTypeValidator` classes must be in editor assemblies. The attribute binds a validator to the target type. Passing `useForChildren: true` also applies the validator to derived Unity object types.

Multiple validators can target the same type. The object is valid only when all enabled validators pass.

Build type validators can be enabled or disabled in:

```text
Project Settings > Build Validation
```

## Manual Validation

Run validation without starting a build from:

```text
Tools/Validation/Validate Required References For Build
```

The same validation path is used for manual validation and build validation.

## Build Behavior

BuildValidation checks:

- enabled scenes from Build Settings;
- MonoBehaviour components in those scenes;
- prefabs included through build dependencies;
- ScriptableObject assets included through build dependencies;
- other serialized Unity object assets included through build dependencies;
- assets under `Resources`, even when they are not referenced by a scene.
- `ExposedReference<T>` fields in playable assets used by `PlayableDirector` components in checked scenes.
- enabled `BuildTypeValidator` validators for every checked Unity object with a matching type.

For each issue, the Unity Console receives a message with the severity, asset or scene path, object path, component or asset type, and the field or method that reported the issue. Console messages use the relevant Unity object as context, so selecting the log entry can ping or select the related asset or object.

If a custom fail message is passed to the attribute, that message is written as the first line of the Console entry. The default validation details are written after it.

Severity controls the build result:

- `Info` logs a regular Console message and does not appear in the final validation summary.
- `Warning` logs a warning and does not fail the build.
- `Fatal` logs an error and fails the build after all validation issues have been reported.

Array and `List<T>` fields are checked per element. Empty arrays and lists are valid. Null elements are reported with their element index.

Invalid `[ValidateInvoke]` method signatures and exceptions thrown by validation methods are reported as `Fatal`.

Invalid `BuildTypeValidator` definitions and exceptions thrown by build type validators are reported as `Fatal`.
