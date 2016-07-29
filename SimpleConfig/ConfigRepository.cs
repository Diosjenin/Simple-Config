using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

// TODO: Discuss case-insensitivity
namespace SimpleConfig
{
    /// <summary>
    /// Stores one or more sets of key-value pairs (<see cref="IDictionary"/>, <see cref="NameValueCollection"/>, etc.) to be read by <see cref="ConfigParameter{T}"/>s.
    /// </summary>
    public class ConfigRepository
    {
        #region private members and public properties

        private static CultureInfo _exceptionStringCulture = CultureInfo.CurrentCulture;

        private StringComparer _deepCopyKeyComparer = StringComparer.OrdinalIgnoreCase;
        private List<ConfigWrapper> _configs = new List<ConfigWrapper>();
        private List<ReadOnlyDictionary<string, string>> _deepCopies = new List<ReadOnlyDictionary<string, string>>();

        // We need an event-like system to track and interact with params for RefreshAll and ClearOverrideAll. 
        // Traditional events would force params to implement IDisposable for proper removal of old references from the list of subscribers, 
        // and WeakEvents would force us to reference the WindowsBase assembly, which is WPF-specific and not present in .NET Core. 
        // Rolling our own avoids both of these problems, and lets us completely sidestep some truly *nasty* threading difficulties. 
        // It also pulls double duty by letting us easily expose a collection of all params (for logging, etc.).
        // TODO: Consider expanding on this implementation and turning it into a separate project.
        private ConcurrentDictionary<string, WeakReference<ConfigParameterBase>> _params = new ConcurrentDictionary<string, WeakReference<ConfigParameterBase>>();


        /// <summary>
        /// The non-generic interfaces of all <see cref="ConfigParameter{T}"/>s constructed with this <see cref="ConfigRepository"/>.
        /// </summary>
        public IEnumerable<ConfigParameterBase> Parameters
        {
            get
            {
                foreach (var r in _params)
                {
                    ConfigParameterBase param;
                    if (r.Value.TryGetTarget(out param))
                    {
                        yield return param;
                    }
                }
            }
        }

        #endregion


        #region constructors

        /// <summary>
        /// Initializes a <see cref="ConfigRepository"/> with one or more <see cref="IDictionary"/> objects.
        /// </summary>
        /// <param name="primary">The first <see cref="IDictionary"/> object.</param>
        /// <param name="secondaries">One or more additional <see cref="IDictionary"/> objects. Optional.</param>
        /// <exception cref="ArgumentNullException">One or more of the given <see cref="IDictionary"/> objects were null.</exception>
        public ConfigRepository(IDictionary primary, params IDictionary[] secondaries)
        {
            NullCheck(primary, secondaries);
            Initialize(null, new DictionaryWrapper(primary), secondaries?.Select(s => new DictionaryWrapper(s)));
        }

        /// <summary>
        /// Initializes a <see cref="ConfigRepository"/> with one or more <see cref="IDictionary"/> objects.
        /// </summary>
        /// <param name="deepCopyKeyComparer">The <see cref="StringComparer"/> used to compare keys within the given key-value collections. Default value is <see cref="StringComparer.OrdinalIgnoreCase"/>.</param>
        /// <param name="primary">The first <see cref="IDictionary"/> object.</param>
        /// <param name="secondaries">One or more additional <see cref="IDictionary"/> objects. Optional.</param>
        /// <exception cref="ArgumentNullException">One or more of the given <see cref="IDictionary"/> objects were null.</exception>
        public ConfigRepository(StringComparer deepCopyKeyComparer, IDictionary primary, params IDictionary[] secondaries)
        {
            NullCheck(primary, secondaries);
            Initialize(deepCopyKeyComparer, new DictionaryWrapper(primary), secondaries?.Select(s => new DictionaryWrapper(s)));
        }

        /// <summary>
        /// Initializes a <see cref="ConfigRepository"/> with one or more <see cref="NameValueCollection"/> objects.
        /// </summary>
        /// <param name="primary">The first <see cref="NameValueCollection"/> object.</param>
        /// <param name="secondaries">One or more additional <see cref="NameValueCollection"/> objects. Optional.</param>
        /// <exception cref="ArgumentNullException">One or more of the given <see cref="NameValueCollection"/> objects were null.</exception>
        public ConfigRepository(NameValueCollection primary, params NameValueCollection[] secondaries)
        {
            NullCheck(primary, secondaries);
            Initialize(null, new NameValueCollectionWrapper(primary), secondaries?.Select(s => new NameValueCollectionWrapper(s)));
        }

        /// <summary>
        /// Initializes a <see cref="ConfigRepository"/> with one or more NameValueCollection objects.
        /// </summary>
        /// <param name="deepCopyKeyComparer">The <see cref="StringComparer"/> used to compare keys within the given key-value collections. Default value is <see cref="StringComparer.OrdinalIgnoreCase"/>.</param>
        /// <param name="primary">The first <see cref="NameValueCollection"/> object.</param>
        /// <param name="secondaries">One or more additional <see cref="NameValueCollection"/> objects. Optional.</param>
        /// <exception cref="ArgumentNullException">One or more of the given <see cref="NameValueCollection"/> objects were null.</exception>
        public ConfigRepository(StringComparer deepCopyKeyComparer, NameValueCollection primary, params NameValueCollection[] secondaries)
        {
            NullCheck(primary, secondaries);
            Initialize(deepCopyKeyComparer, new NameValueCollectionWrapper(primary), secondaries?.Select(s => new NameValueCollectionWrapper(s)));
        }

        /// <summary>
        /// Initializes a <see cref="ConfigRepository"/> with one or more key-value collections, each manually wrapped in an <see cref="ConfigWrapper"/>.
        /// </summary>
        /// <param name="primary">The first <see cref="ConfigWrapper"/> object.</param>
        /// <param name="secondaries">One or more additional <see cref="ConfigWrapper"/> objects. Optional.</param>
        /// <exception cref="ArgumentNullException">One or more of the given <see cref="ConfigWrapper"/> objects were null.</exception>
        public ConfigRepository(ConfigWrapper primary, params ConfigWrapper[] secondaries)
        {
            NullCheck(primary, secondaries);
            Initialize(null, primary, secondaries);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigRepository"/> with one or more key-value collections, each manually wrapped in an <see cref="ConfigWrapper"/>.
        /// </summary>
        /// <param name="deepCopyKeyComparer">The <see cref="StringComparer"/> used to compare keys within the given key-value collection. Default value is <see cref="StringComparer.OrdinalIgnoreCase"/>.</param>
        /// <param name="primary">The first <see cref="ConfigWrapper"/> object.</param>
        /// <param name="secondaries">One or more additional <see cref="ConfigWrapper"/> objects. Optional.</param>
        /// <exception cref="ArgumentNullException">One or more of the given <see cref="ConfigWrapper"/> objects were null.</exception>
        public ConfigRepository(StringComparer deepCopyKeyComparer, ConfigWrapper primary, params ConfigWrapper[] secondaries)
        {
            NullCheck(primary, secondaries);
            Initialize(deepCopyKeyComparer, primary, secondaries);
        }

        private static void NullCheck<T>(T primary, T[] secondaries)
        {
            if (primary == null)
            {
                throw new ArgumentNullException(nameof(primary));
            }
            if (secondaries != null)
            {
                // For simplicity, unit tests only deal with zero, one, and two key-value collections passed in. 
                // If a for loop were used here, the loop's "i++" wouldn't be covered (secondaries[] would need 2+ objects, for 3+ total).
                // So we're cheesing our way through a while loop instead. The things I do for perfect coverage.
                // TODO: change unit tests instead of cheesing this while loop?
                int i = 0;
                while (i < secondaries.Length)
                {
                    if (secondaries[i] == null)
                    {
                        throw new ArgumentNullException(string.Format(_exceptionStringCulture, "{0}[{1}]", nameof(secondaries), i));
                    }
                    i++;
                }
            }
        }

        private void Initialize(StringComparer deepCopyKeyComparer, ConfigWrapper primary, IEnumerable<ConfigWrapper> secondaries)
        {
            if (deepCopyKeyComparer != null)
            {
                _deepCopyKeyComparer = deepCopyKeyComparer;
            }

            Debug.Assert(primary != null);
            _configs.Add(primary);
            _deepCopies.Add(null);

            if (secondaries != null)
            {
                foreach (var s in secondaries)
                {
                    Debug.Assert(s != null);
                    _configs.Add(s);
                    _deepCopies.Add(null);
                }
            }

            RefreshAll();
        }

        #endregion


        #region trygetvalue

        /// <summary>
        /// Attempts to retrieve the string value associated with the key from the first possible key-value collection.
        /// </summary>
        /// <param name="name">The key whose value will be retrieved.</param>
        /// <param name="value">The value associated with the key, if it was found; null otherwise.</param>
        /// <returns>True if <paramref name="name"/> was present in at least one key-value collection; false otherwise.</returns>
        public bool TryGetValue(string name, out string value)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                // for loop grants thread safety for concurrent TryGetValue() calls
                for (int i = 0; i < _deepCopies.Count; i++)
                {
                    if (_deepCopies[i].TryGetValue(name, out value))
                    {
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Attempts to retrieve the string value associated with the key from the key-value collection at the specified index. 
        /// </summary>
        /// <param name="collectionIndex">The zero-based index of the key-value collection to attempt to retrieve the value from.</param>
        /// <param name="name">The key whose value will be retrieved.</param>
        /// <param name="value">The value associated with the key, if it was found; null otherwise.</param>
        /// <returns>True if the key <paramref name="name"/> was present in the specified key-value collection; false otherwise.</returns>
        /// <exception cref="ArgumentOutOfRangeException">collectionIndex was less than zero, or greater than the zero-indexed total number of key-value collections.</exception>
        public bool TryGetValue(int collectionIndex, string name, out string value)
        {
            if (collectionIndex < 0 || collectionIndex >= _configs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(collectionIndex));
            }
            if (!string.IsNullOrWhiteSpace(name) && _deepCopies[collectionIndex].TryGetValue(name, out value))
            {
                return true;
            }
            value = null;
            return false;
        }

        #endregion

        #region parameter registration

        internal void Register(string paramKey, ConfigParameterBase param)
        {
            Debug.Assert(param != null);
            var paramAdd = _params.TryAdd(paramKey, new WeakReference<ConfigParameterBase>(param));
            Debug.Assert(paramAdd);
        }

        internal void Deregister(string parameterKey)
        {
            WeakReference<ConfigParameterBase> wr;
            var paramRemove = _params.TryRemove(parameterKey, out wr);
            Debug.Assert(paramRemove);
        }

        #endregion

        #region refreshes and overrides

        /// <summary>
        /// Reflects changes made to the source key-value collections after the <see cref="ConfigRepository"/>'s construction.
        /// </summary>
        // TODO: Discuss thread safety?
        // TODO: Test Refresh under Override, for both Repo and Params
        public void RefreshAll()
        {
            // use a for loop here to grant thread safety for concurrent calls to RefreshAll()
            for (int i = 0; i < _configs.Count; i++)
            {
                _deepCopies[i] = _configs[i].MakeDeepCopy(_deepCopyKeyComparer);
            }
            foreach (var p in Parameters)
            {
                p.RefreshInternal();
            }
        }

        /// <summary>
        /// Removes the replacement value specified with <see cref="ConfigParameter{T}.Override(T)"/>, if any, 
        /// for all <see cref="ConfigParameter{T}"/> objects constructed using this <see cref="ConfigRepository"/>.
        /// </summary>
        public void ClearOverrideAll()
        {
            foreach (var p in Parameters)
            {
                p.ClearOverrideInternal();
            }
        }

        #endregion

    }
}
