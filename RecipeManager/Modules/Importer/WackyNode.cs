using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RecipeManager.Importer
{
    /// <summary>
    /// One mapping read from a WackysDatabase file, read loosely: keys are WackysDatabase's own field names, values
    /// are parsed as they are asked for, and a value that does not parse is reported rather than failing the file.
    /// Every key that is read is marked as used, so whatever is left over at the end is exactly what the import
    /// could not carry across.
    /// </summary>
    internal class WackyNode
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();
        private readonly HashSet<string> used = new HashSet<string>();
        private readonly Dictionary<string, string> dropped = new Dictionary<string, string>();
        private readonly List<WackyNode> children = new List<WackyNode>();
        private readonly string path;

        /// <summary>Keys and what they became, as "key -> rmKey".</summary>
        public readonly List<string> Converted = new List<string>();
        /// <summary>Values that were there but could not be read.</summary>
        public readonly List<string> Invalid = new List<string>();

        public WackyNode(object parsed, string path = "")
        {
            this.path = path;
            if (parsed is IDictionary<object, object> mapping)
            {
                foreach (KeyValuePair<object, object> pair in mapping)
                {
                    if (pair.Key != null) { values[pair.Key.ToString()] = pair.Value; }
                }
            }
        }

        /// <summary>The key as written in a report, with the blocks it sits in.</summary>
        public string Where(string key)
        {
            return path + key;
        }

        /// <summary>This block's own name as written in a report, empty for a whole file.</summary>
        public string Name => path.TrimEnd('.');

        /// <summary>Whether the key has a value. WackysDatabase dumps write every field, an unset one with no value.</summary>
        public bool Has(string key)
        {
            return values.TryGetValue(key, out object value) && value != null;
        }

        /// <summary>Whether the key is written at all, even with no value.</summary>
        public bool Contains(string key)
        {
            return values.ContainsKey(key);
        }

        private object Take(string key, string rmKey)
        {
            if (!values.TryGetValue(key, out object value) || value == null) { return null; }
            used.Add(key);
            if (rmKey != null) { Converted.Add($"{Where(key)} -> {rmKey}"); }
            return value;
        }

        /// <summary>Marks a key as handled without carrying it across, with the reason it is left out.</summary>
        public void Drop(string key, string reason)
        {
            if (!Has(key)) { return; }
            used.Add(key);
            dropped[Where(key)] = reason;
        }

        /// <summary>Marks a key as handled when it needs no setting of its own, such as one already matching the game.</summary>
        public void Use(string key, string note = null)
        {
            if (!Has(key)) { return; }
            used.Add(key);
            if (note != null) { Converted.Add($"{Where(key)}: {note}"); }
        }

        /// <param name="rmKey">What the value becomes, for the report. Null when the caller reports it itself.</param>
        public string Str(string key, string rmKey = null)
        {
            object value = Take(key, rmKey);
            return value == null ? null : ScalarText(value);
        }

        public float? Float(string key, string rmKey = null)
        {
            string text = Str(key, null);
            if (text == null) { return null; }
            if (float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                if (rmKey != null) { Converted.Add($"{Where(key)} -> {rmKey}"); }
                return result;
            }
            Invalid.Add($"{Where(key)}: '{text}' is not a number");
            return null;
        }

        public int? Int(string key, string rmKey = null)
        {
            float? number = Float(key, null);
            if (number == null) { return null; }
            if (rmKey != null) { Converted.Add($"{Where(key)} -> {rmKey}"); }
            return (int)Math.Round(number.Value);
        }

        public bool? Bool(string key, string rmKey = null)
        {
            string text = Str(key, null);
            if (text == null) { return null; }
            if (bool.TryParse(text.Trim(), out bool result))
            {
                if (rmKey != null) { Converted.Add($"{Where(key)} -> {rmKey}"); }
                return result;
            }
            Invalid.Add($"{Where(key)}: '{text}' is not true or false");
            return null;
        }

        /// <summary>An enum by name, case insensitive. Flags can be listed with commas, as WackysDatabase writes them.</summary>
        public T? Enum<T>(string key, string rmKey = null) where T : struct
        {
            string text = Str(key, null);
            if (text == null) { return null; }
            if (System.Enum.TryParse(text.Trim(), true, out T result))
            {
                if (rmKey != null) { Converted.Add($"{Where(key)} -> {rmKey}"); }
                return result;
            }
            Invalid.Add($"{Where(key)}: '{text}' is not one of {string.Join(", ", System.Enum.GetNames(typeof(T)))}");
            return null;
        }

        /// <summary>A list of plain values. Null when the key is not set, empty for an empty list.</summary>
        public List<string> StrList(string key)
        {
            if (!values.TryGetValue(key, out object value) || value == null) { return null; }
            used.Add(key);
            if (value is IList<object> list)
            {
                return list.Select(entry => entry == null ? null : ScalarText(entry)).ToList();
            }
            // A single value where a list was expected
            return new List<string> { ScalarText(value) };
        }

        /// <summary>A nested block. Its keys are reported with this one's name in front.</summary>
        public WackyNode Child(string key)
        {
            if (!values.TryGetValue(key, out object value) || !(value is IDictionary<object, object>)) { return null; }
            used.Add(key);
            WackyNode child = new WackyNode(value, Where(key) + ".");
            children.Add(child);
            return child;
        }

        /// <summary>A list of nested blocks, reported as key[0], key[1] and so on.</summary>
        public List<WackyNode> ChildList(string key)
        {
            if (!values.TryGetValue(key, out object value) || !(value is IList<object> list)) { return null; }
            used.Add(key);
            List<WackyNode> nodes = new List<WackyNode>();
            for (int i = 0; i < list.Count; i++)
            {
                WackyNode child = new WackyNode(list[i], $"{Where(key)}[{i}].");
                children.Add(child);
                nodes.Add(child);
            }
            return nodes;
        }

        /// <summary>
        /// Every key with a value that was neither read nor dropped, this block's and its children's: the key as
        /// reported, the key on its own, and its value.
        /// </summary>
        public IEnumerable<(string key, string leaf, object value)> Unread()
        {
            foreach (KeyValuePair<string, object> pair in values)
            {
                if (pair.Value != null && !used.Contains(pair.Key)) { yield return (Where(pair.Key), pair.Key, pair.Value); }
            }
            foreach (WackyNode child in children)
            {
                foreach ((string key, string leaf, object value) entry in child.Unread()) { yield return entry; }
            }
        }

        /// <summary>A number, without marking the key as read. Null when it is not set or not a number.</summary>
        public float? Peek(string key)
        {
            if (!values.TryGetValue(key, out object value) || value == null) { return null; }
            if (float.TryParse(ScalarText(value).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
            return null;
        }

        /// <summary>Every key dropped with a reason, this block's and its children's.</summary>
        public IEnumerable<KeyValuePair<string, string>> Dropped()
        {
            foreach (KeyValuePair<string, string> pair in dropped) { yield return pair; }
            foreach (WackyNode child in children)
            {
                foreach (KeyValuePair<string, string> pair in child.Dropped()) { yield return pair; }
            }
        }

        public IEnumerable<string> AllConverted()
        {
            foreach (string entry in Converted) { yield return entry; }
            foreach (WackyNode child in children)
            {
                foreach (string entry in child.AllConverted()) { yield return entry; }
            }
        }

        public IEnumerable<string> AllInvalid()
        {
            foreach (string entry in Invalid) { yield return entry; }
            foreach (WackyNode child in children)
            {
                foreach (string entry in child.AllInvalid()) { yield return entry; }
            }
        }

        /// <summary>The keys this block has a value for.</summary>
        public IEnumerable<string> Keys()
        {
            return values.Where(pair => pair.Value != null).Select(pair => pair.Key);
        }

        /// <summary>The raw value of a key, for comparing it with the game.</summary>
        public object Raw(string key)
        {
            return values.TryGetValue(key, out object value) ? value : null;
        }

        private static string ScalarText(object value)
        {
            if (value is IDictionary<object, object>) { return "{...}"; }
            if (value is IList<object>) { return "[...]"; }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
