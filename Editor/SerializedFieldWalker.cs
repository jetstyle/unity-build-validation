// Copyright (c) 2026 JetXR
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JetXR.Unity.BuildValidation.Editor
{
    internal static class SerializedFieldWalker
    {
        const int MaxDepth = 64;

        static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        public static void Walk(Object target, Action<SerializedField> visitor)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));

            var serializedObject = new SerializedObject(target);
            var activeObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
            WalkObject(target, target.GetType(), serializedObject, null, visitor, activeObjects, 0);
        }

        static void WalkObject(object owner, Type ownerType, SerializedObject serializedObject, SerializedProperty parentProperty, Action<SerializedField> visitor, HashSet<object> activeObjects, int depth)
        {
            if (owner == null || ownerType == null || depth > MaxDepth)
                return;

            bool trackObject = !ownerType.IsValueType;
            if (trackObject && !activeObjects.Add(owner))
                return;

            try
            {
                foreach (FieldInfo field in GetUnitySerializedFields(ownerType))
                {
                    SerializedProperty property = parentProperty == null
                        ? serializedObject.FindProperty(field.Name)
                        : parentProperty.FindPropertyRelative(field.Name);
                    object value = field.GetValue(owner);
                    visitor(new SerializedField(owner, field, property));

                    if (property != null)
                        WalkValue(value, field.FieldType, property, serializedObject, visitor, activeObjects, depth + 1);
                }
            }
            finally
            {
                if (trackObject)
                    activeObjects.Remove(owner);
            }
        }

        static void WalkValue(object value, Type declaredType, SerializedProperty property, SerializedObject serializedObject, Action<SerializedField> visitor, HashSet<object> activeObjects, int depth)
        {
            if (value == null || depth > MaxDepth || typeof(Object).IsAssignableFrom(declaredType) || IsExposedReference(declaredType))
                return;

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                if (!(value is IList list))
                    return;

                int count = Math.Min(property.arraySize, list.Count);
                for (int i = 0; i < count; i++)
                {
                    object element = list[i];
                    if (element == null)
                        continue;

                    SerializedProperty elementProperty = property.GetArrayElementAtIndex(i);
                    Type runtimeElementType = element.GetType();
                    if (ShouldWalkObject(runtimeElementType, elementProperty))
                        WalkObject(element, runtimeElementType, serializedObject, elementProperty, visitor, activeObjects, depth);
                }

                return;
            }

            Type runtimeType = value.GetType();
            if (ShouldWalkObject(runtimeType, property))
                WalkObject(value, runtimeType, serializedObject, property, visitor, activeObjects, depth);
        }

        static bool ShouldWalkObject(Type type, SerializedProperty property)
        {
            if (type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(Object).IsAssignableFrom(type) || IsExposedReference(type))
                return false;

            return property.propertyType == SerializedPropertyType.Generic ||
                   property.propertyType == SerializedPropertyType.ManagedReference;
        }

        static FieldInfo[] GetUnitySerializedFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out FieldInfo[] cachedFields))
                return cachedFields;

            var fields = new List<FieldInfo>();
            for (Type currentType = type; ShouldInspectType(currentType); currentType = currentType.BaseType)
            {
                foreach (FieldInfo field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (IsUnitySerializedField(field))
                        fields.Add(field);
                }
            }

            cachedFields = fields.ToArray();
            FieldCache[type] = cachedFields;
            return cachedFields;
        }

        static bool ShouldInspectType(Type type)
        {
            return type != null &&
                   type != typeof(object) &&
                   type != typeof(MonoBehaviour) &&
                   type != typeof(ScriptableObject);
        }

        static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral || field.IsNotSerialized)
                return false;

            return field.IsPublic ||
                   field.IsDefined(typeof(SerializeField), inherit: true) ||
                   field.IsDefined(typeof(SerializeReference), inherit: true);
        }

        static bool IsExposedReference(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition().FullName == "UnityEngine.ExposedReference`1";
        }

        internal readonly struct SerializedField
        {
            public SerializedField(object owner, FieldInfo field, SerializedProperty property)
            {
                Owner = owner;
                Field = field;
                Property = property;
            }

            public object Owner { get; }
            public FieldInfo Field { get; }
            public SerializedProperty Property { get; }
        }

        sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
