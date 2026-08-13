# Changelog

## [Unreleased]

- Added recursive `[ValidateReferenceSet]` discovery inside serialized structs, classes, arrays, and lists.
- Added polymorphic nested validation through `[SerializeReference]` fields and collections.
- Added recursive Timeline `ExposedReference<T>` validation through `PlayableDirector`.
- Validation issues for nested references now include their complete Unity serialized property path.

## [1.2.1] - 2026-06-08

- Added class-based settings for `BuildTypeValidator` validators.
- Added configurable settings for the built-in `TimelineReferencesValidator`.

## [1.2.0] - 2026-06-08

- Added external `BuildTypeValidator` validators with Project Settings enable/disable controls.
- Added built-in `TimelineReferencesValidator` for Timeline bindings and marked `ExposedReference<T>` fields when `com.unity.timeline` is installed.
- Added multi-issue reporting through `BuildTypeValidationContext`.

## [1.1.1] - 2026-06-05

- Fixed `ExposedReference<T>` validation messages to include the resolver source.

## [1.1.0] - 2026-06-05

- Added `ValidateInvoke`.
- Added `ExposedReference<T>` validation through `IExposedPropertyTable` resolver contexts.

## [1.0.0] - 2026-06-05

- Added the first package feature: `ValidateReferenceSet`.
