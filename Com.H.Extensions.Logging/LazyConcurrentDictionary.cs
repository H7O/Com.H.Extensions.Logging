using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Extensions.Logging
{
    internal class InvalidLazyConcurrentUpdateException : Exception
    {

    }
    internal class LazyConcurrentDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue?> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, Lazy<TValue?>> _dic = new();
        public LazyConcurrentDictionary()
        { }

        public LazyConcurrentDictionary(IEnumerable<KeyValuePair<TKey, TValue?>> keyValuePairs)
        => this._dic = new ConcurrentDictionary<TKey, Lazy<TValue?>>(
            keyValuePairs.Select(x =>
                    new KeyValuePair<TKey, Lazy<TValue?>>(x.Key, new Lazy<TValue?>(x.Value))
            ));

        public TValue? this[TKey key]
        {
            get
            {
                if (key is null
                    || !this._dic.ContainsKey(key)
                    ) return default;
                this._dic.TryGetValue(key, out var lv);
                return lv is null ? default : lv.Value;
            }
            set
            {
                if (key is null) return;
                _ = this.AddOrUpdate(key, value, (_, oldItem) => value);
            }
        }

        public IEnumerable<TKey> Keys => this._dic.Keys;

        public IEnumerable<TValue?> Values => this._dic.Values.Select(x => x.Value);

        public int Count => this._dic.Count;

        public TValue? AddOrUpdate(TKey key, TValue? value, Func<TKey, TValue?, TValue?> updateValueFactory)
            
            => this._dic.AddOrUpdate(
                key,
                new Lazy<TValue?>(value),
                (k, oldItem) => new Lazy<TValue?>(() => updateValueFactory(k, oldItem.Value)))
                .Value;

        public TValue? AddOrUpdate(TKey key,
            Func<TKey, TValue?> addValueFactory,
            Func<TKey, TValue?, TValue?> updateValueFactory)
            => this._dic.AddOrUpdate(
                key,
                new Lazy<TValue?>(() => addValueFactory(key)),
                (k, oldItem) => new Lazy<TValue?>(() => updateValueFactory(k, oldItem.Value)))
                .Value;

        public TValue? AddOrUpdate<TArg>(TKey key,
            Func<TKey, TArg?, TValue?> addValueFactory,
            Func<TKey, TValue?, TArg?, TValue?> updateValueFactory,
            TArg factoryArgument
            ) =>
            this._dic.AddOrUpdate(
                key,
                new Lazy<TValue?>(() => addValueFactory(key, factoryArgument)),
                (k, oldItem) => new Lazy<TValue?>(() => updateValueFactory(k, oldItem.Value, factoryArgument)))
                .Value;
        
        /// <summary>
        /// Thread-safe updates the dictionary. If a key doesn't exist, no update is done and the method returns default TValue.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="updateValueFactory"></param>
        /// <returns></returns>
        public TValue? Update(TKey key,
            Func<TKey, TValue?, TValue?> updateValueFactory)
        {
            try
            {
                var value = this.AddOrUpdate(key, 
                    _ => throw new InvalidLazyConcurrentUpdateException(),
                    (k, oldValue) => updateValueFactory(k, oldValue));
                return value;
            }
            catch (InvalidLazyConcurrentUpdateException)
            {
                return default;
            }
        }

        /// <summary>
        /// Thread-safe adds an item to the dictionary. If a key does exist, no add is done and the method returns default TValue.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="updateValueFactory"></param>
        /// <returns></returns>
        public TValue? Add(TKey key,
            Func<TKey, TValue?> addValueFactory)
        {
            try
            {
                var value = this.AddOrUpdate(key,
                    k => addValueFactory(k),
                    (_, _) => throw new InvalidLazyConcurrentUpdateException());
                return value;
            }
            catch (InvalidLazyConcurrentUpdateException)
            {
                return default;
            }
        }

        public bool ContainsKey(TKey key)
            =>
            this._dic.ContainsKey(key);

        public IEnumerator<KeyValuePair<TKey, TValue?>> GetEnumerator()
            =>
            this._dic.Select(x => new KeyValuePair<TKey, TValue?>(x.Key, x.Value.Value))
                .GetEnumerator();


        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue? value)
        {
            if (this._dic.TryGetValue(key, out Lazy<TValue?>? outValue))
            {
                value = outValue == null ? default : outValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue? value)
        {
            if (this._dic.TryRemove(key, out Lazy<TValue?>? outValue))
            {
                value = outValue == null ? default : outValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool TryAdd(TKey key, TValue? value)
            => this._dic.TryAdd(key, new Lazy<TValue?>(value));


        IEnumerator IEnumerable.GetEnumerator()
            =>
            this._dic.Select(x => new KeyValuePair<TKey, TValue?>(x.Key, x.Value.Value))
                .GetEnumerator();

        public void Clear()
            => this._dic.Clear();
    }
}
