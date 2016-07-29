using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace SimpleConfig
{
    /*
     * bool TryGet(string key, out string value);
     * void Set(string key, string value);
     * IChangeToken GetReloadToken();
     * void Load();  // deep copy
     * 
     *  /// <summary>
     *  /// Returns the immediate descendant configuration keys for a given parent path based on this
     *  /// <see cref="IConfigurationProvider"/>'s data and the set of keys returned by all the preceding
     *  /// <see cref="IConfigurationProvider"/>s.
     *  /// </summary>
     *  /// <param name="earlierKeys">The child keys returned by the preceding providers for the same parent path.</param>
     *  /// <param name="parentPath">The parent path.</param>
     *  /// <returns>The child keys.</returns>
     *  IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath);
     */

    //public abstract class ConfigWrapper<T>
    //{
    //    private Dictionary<string, string> _deepCopy;
    //    private StringComparer _deepCopyKeyComparer = StringComparer.OrdinalIgnoreCase;

    //    public ConfigWrapper(T config, StringComparer deepCopyKeyComparer)
    //    {
    //        if (config == null)
    //        {
    //            throw new ArgumentNullException(nameof(config));
    //        }
    //        if (deepCopyKeyComparer != null)
    //        {
    //            _deepCopyKeyComparer = deepCopyKeyComparer;
    //        }
    //        ReloadFromConfig();
    //    }

    //    public void ReloadFromConfig()
    //    {
    //        _deepCopy = new Dictionary<string, string>(_deepCopyKeyComparer);
    //        CopyAllProperties();
    //    }

    //    protected abstract void CopyAllProperties();

    //    protected void CopyProperty(string key, string value)
    //    {
    //        _deepCopy.Add(key, value);
    //    }
    //}


    /// <summary>
    /// Provides access to a deep copy of a source key-value collection as a read-only dictionary, 
    /// as well as a means of recreating the deep copy on demand to reflect changes made to the source object. 
    /// </summary>
    public abstract class ConfigWrapper
    {
        internal abstract ReadOnlyDictionary<string, string> MakeDeepCopy(StringComparer deepCopyKeyComparer);
    }

    /// <summary>
    /// Provides access to a deep copy of a source key-value collection as a read-only dictionary, 
    /// as well as a means of recreating the deep copy on demand to reflect changes made to the source object. 
    /// All provided concrete implementations inherit from this class.
    /// </summary>
    /// <typeparam name="T">The type of the source key-value collection used to create the deep copy.</typeparam>
    public abstract class ConfigWrapper<T> : ConfigWrapper
    {
        private T _source;

        /// <summary>
        /// Base constructor. Automatically creates the deep copy of the source <see cref="T"/>.
        /// </summary>
        /// <param name="config">The source key-value collection of type <see cref="T"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> was null.</exception>
        protected ConfigWrapper(T config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            _source = config;
        }

        internal sealed override ReadOnlyDictionary<string, string> MakeDeepCopy(StringComparer deepCopyKeyComparer)
        {
            IDictionary<string, string> d = new Dictionary<string, string>(deepCopyKeyComparer);
            foreach (var kvp in DeepCopySource(_source))
            {
                d.Add(kvp);
            }
            return new ReadOnlyDictionary<string, string>(d);
        }

        /// <summary>
        /// Copies the source key-value collection of type <see cref="T"/> into a series of KeyValuePairs.
        /// </summary>
        /// <param name="source">The source key-value collection of type <see cref="T"/>.</param>
        /// <returns>A copy of all data from the source of type <see cref="T"/>.</returns>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        protected abstract IEnumerable<KeyValuePair<string, string>> DeepCopySource(T source);
    }



    /// <summary>
    /// Provides access to a deep copy of a source IDictionary as a read-only dictionary.
    /// </summary>
    public class DictionaryWrapper : ConfigWrapper<IDictionary>
    {
        /// <summary>
        /// Initializes a DictionaryWrapper. Automatically creates the deep copy of the source IDictionary.
        /// </summary>
        /// <param name="config">The source key-value collection of type IDictionary.</param>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> was null.</exception>
        public DictionaryWrapper(IDictionary config)
            : base(config)
        { }

        /// <summary>
        /// Copies the source IDictionary into a series of KeyValuePairs.
        /// </summary>
        /// <param name="source">The source key-value collection of type IDictionary.</param>
        /// <returns>A copy of all data from the source IDictionary.</returns>
        protected sealed override IEnumerable<KeyValuePair<string, string>> DeepCopySource(IDictionary source)
        {
            if (source is IDictionary<string, string>)
            {
                // small optimization: if the IDictionary's enumerator already returns KeyValuePair<string, string>, we can return those directly
                // (MakeDeepCopy() uses ICollection<T>.Add() on a Dictionary, which creates a new copy of the KeyValuePair internally, so this pattern is safe)
                foreach (var kvp in (source as IDictionary<string, string>))
                {
                    yield return kvp;
                }
            }
            else
            {
                foreach (DictionaryEntry kvp in source)
                {
                    yield return new KeyValuePair<string, string>(kvp.Key.ToString(), kvp.Value.ToString());
                }
            }
        }
    }

    /// <summary>
    /// Provides access to a deep copy of a source NameValueCollection as a read-only dictionary.
    /// </summary>
    public class NameValueCollectionWrapper : ConfigWrapper<NameValueCollection>
    {
        /// <summary>
        /// Initializes a NameValueCollectionWrapper. Automatically creates the deep copy of the source NameValueCollection.
        /// </summary>
        /// <param name="config">The source key-value collection of type NameValueCollection.</param>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> was null.</exception>
        public NameValueCollectionWrapper(NameValueCollection config)
            : base(config)
        { }

        /// <summary>
        /// Copies the source key-value collection into a series of KeyValuePairs.
        /// </summary>
        /// <param name="source">The source key-value collection of type NameValueCollection.</param>
        /// <returns>A copy of all data from the source NameValueCollection.</returns>
        protected sealed override IEnumerable<KeyValuePair<string, string>> DeepCopySource(NameValueCollection source)
        {
            foreach (string key in source.Keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    yield return new KeyValuePair<string, string>(key, source[key]);
                }
            }
        }
    }


}
