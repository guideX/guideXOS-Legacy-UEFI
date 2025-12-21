namespace System.Collections.Generic {
    /// <summary>
    /// Dictionary
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class Dictionary<TKey, TValue> {
        /// <summary>
        /// This
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue this[TKey key] {
            get {
                return Values[Keys.IndexOf(key)];
            }
            set {
                Values[Keys.IndexOf(key)] = value;
            }
        }
        /// <summary>
        /// Count
        /// </summary>
        public int Count { get { return Values.Count; } }
        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="key"></param>
        public void Remove(TKey key) {
            Values.Remove(Values[Keys.IndexOf(key)]);
            Keys.Remove(key);
        }
        /// <summary>
        /// Dictionary
        /// </summary>
        public Dictionary() {
            Keys = new List<TKey>();
            Values = new List<TValue>();
        }
        /// <summary>
        /// Contains Key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(TKey key) {
            return Keys.IndexOf(key) != -1;
        }
        /// <summary>
        /// Contains Value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ContainsValue(TValue value) {
            return Values.IndexOf(value) != -1;
        }
        /// <summary>
        /// Add
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(TKey key, TValue value) {
            Keys.Add(key);
            Values.Add(value);
        }
        /// <summary>
        /// Clear
        /// </summary>
        public void Clear() {
            Keys.Clear();
            Values.Clear();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public override void Dispose() {
            Keys.Clear();
            Values.Clear();
            Values.Dispose();
            Keys.Dispose();
            base.Dispose();
        }
        /// <summary>
        /// Keys
        /// </summary>
        public List<TKey> Keys;
        /// <summary>
        /// Values
        /// </summary>
        public List<TValue> Values;
    }
}