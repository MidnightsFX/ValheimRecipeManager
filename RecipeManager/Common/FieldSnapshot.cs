using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace RecipeManager.Common
{
    /// <summary>
    /// The values of the named fields of an object, to put back later. For game objects with many settings, where
    /// listing each field by hand in a snapshot class would only repeat the list of what can be configured. A field
    /// the object's type does not have is skipped, so one list serves related types. Lists and arrays are copied,
    /// so changes made to the object later cannot reach back into the snapshot.
    /// </summary>
    internal class FieldSnapshot
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<(FieldInfo field, object value)> values = new List<(FieldInfo field, object value)>();

        public static FieldSnapshot Of(object target, IEnumerable<string> fieldNames)
        {
            FieldSnapshot snapshot = new FieldSnapshot();
            if (target == null) { return snapshot; }
            foreach (string name in fieldNames)
            {
                FieldInfo field = FindField(target.GetType(), name);
                if (field == null) { continue; }
                snapshot.values.Add((field, Copy(field.GetValue(target))));
            }
            return snapshot;
        }

        public void WriteTo(object target)
        {
            if (target == null) { return; }
            foreach ((FieldInfo field, object value) in values)
            {
                if (!field.DeclaringType.IsInstanceOfType(target)) { continue; }
                field.SetValue(target, Copy(value));
            }
        }

        /// <summary>
        /// Adds each field whose value on <paramref name="target"/> is no longer the one in <paramref name="before"/>,
        /// with its value from before. A field already held keeps the value it was first recorded with. Used to record
        /// only what was actually changed, so putting it back leaves everything else as other mods have it.
        /// </summary>
        public void RecordChanges(FieldSnapshot before, object target)
        {
            if (target == null) { return; }
            foreach ((FieldInfo field, object value) in before.values)
            {
                if (!field.DeclaringType.IsInstanceOfType(target) || values.Exists(entry => entry.field == field)) { continue; }
                if (!Same(value, field.GetValue(target))) { values.Add((field, Copy(value))); }
            }
        }

        private static bool Same(object a, object b)
        {
            if (a is IList first && b is IList second)
            {
                if (first.Count != second.Count) { return false; }
                for (int i = 0; i < first.Count; i++)
                {
                    if (!Equals(first[i], second[i])) { return false; }
                }
                return true;
            }
            return Equals(a, b);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) { return field; }
            }
            return null;
        }

        /// <summary>A new list or array holding the same entries. Value types are copied by boxing already.</summary>
        private static object Copy(object value)
        {
            if (value is Array array) { return array.Clone(); }
            if (value is IList list && value.GetType().IsGenericType)
            {
                IList copy = (IList)Activator.CreateInstance(value.GetType());
                foreach (object entry in list) { copy.Add(entry); }
                return copy;
            }
            return value;
        }
    }
}
