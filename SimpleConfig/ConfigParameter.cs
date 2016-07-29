using SimpleConfig.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimpleConfig
{
    // TODO: Decide on property state post-exception/post-default
    /// <summary>
    /// Non-generic read-only interface for a <see cref="ConfigWrapper{T}"/>. 
    /// Custom implementations should inherit from <see cref="ConfigWrapper{T}"/> instead of this class.
    /// </summary>
    public abstract class ConfigParameterBase
    {
        /// <summary>
        /// True if a default value was not specified and <see cref="ConfigParameter{T}.Value"/> is equal to default(<see cref="T"/>), 
        /// or if a default value was specified and <see cref="ConfigParameter{T}.Value"/> is equal to it, 
        /// regardless of whether the default was applied or whether a successful parse happened to result in the default value; false otherwise.
        /// </summary>
        public abstract bool ValueEqualsDefault { get; }
        /// <summary>
        /// True if <see cref="ConfigParameter{T}.Value"/> was the result of a successful parse; 
        /// false if it was the result of a given default or override value being used instead.
        /// </summary>
        public abstract bool ValueWasParsed { get; }
        /// <summary>
        /// The string value found in the given <see cref="ConfigRepository"/> corresponding to the given key <see cref="Name"/>, or null if no string value was found.
        /// </summary>
        public abstract string ValueString { get; }
        /// <summary>
        /// True if <see cref="ConfigParameter{T}.Value"/> is being overridden through <see cref="ConfigParameter{T}.Override(T)"/>; false otherwise.
        /// </summary>
        public abstract bool ValueIsOverridden { get; }
        /// <summary>
        /// The key of the desired key-value pair. Retrieval within the source key-value collection(s) will conform to the <see cref="StringComparer"/>, 
        /// if any, specified for the given <see cref="ConfigRepository"/>.
        /// </summary>
        public abstract string Name { get; }
        internal abstract void RefreshInternal();
        internal abstract void ClearOverrideInternal();
    }

    /// <summary>
    /// Parses the value of a single key-value pair into the specified concrete type <see cref="T"/>.
    /// </summary>
    /// <typeparam name="T">The concrete type to parse the value into.</typeparam>
    public class ConfigParameter<T> : ConfigParameterBase
    {
        #region private members

        private static CultureInfo _exceptionStringCulture = CultureInfo.CurrentCulture;

        private string _referenceKey = Guid.NewGuid().ToString();
        private object _lockValue = new object();

        private ConfigRepository _configRepository;
        private Func<T, bool> _constraintFunction = (t) => true;
        private TryParseFunction<T> _customTryParseFunction;

        private string _name;
        private string _valueString;
        private bool _valueWasParsed = false;
        private bool _valueSet = false;
        private bool _defaultValueSet = false;
        private bool _overrideValueSet = false;
        private T _value = default(T);
        private T _defaultValue = default(T);
        private T _overrideValue = default(T);

        #endregion


        #region public properties

        /// <summary>
        /// True if a default value was not specified and <see cref="Value"/> is equal to default(<see cref="T"/>), 
        /// or if a default value was specified and <see cref="Value"/> is equal to it, 
        /// regardless of whether the default was applied or whether a successful parse happened to result in the default value; false otherwise.
        /// </summary>
        public override sealed bool ValueEqualsDefault { get { return Value.Equals(_defaultValue); } }

        /// <summary>
        /// True if <see cref="Value"/> was the result of a successful parse; 
        /// false if it was the result of a given default or override value being used instead.
        /// </summary>
        public override sealed bool ValueWasParsed { get { lock (_lockValue) { return !_overrideValueSet && _valueWasParsed; } } }

        /// <summary>
        /// The string value found in the given <see cref="ConfigRepository"/> corresponding to the given key <see cref="Name"/>, or null if no string value was found.
        /// </summary>
        // TODO: Test for parse failure. If a parse fails after a refresh, this could reflect new value while Value still reflects old one
        public override sealed string ValueString { get { return _valueString; } }

        /// <summary>
        /// True if <see cref="Value"/> is being overridden through <see cref="Override(T)"/>; false otherwise.
        /// </summary>
        public override sealed bool ValueIsOverridden { get { lock (_lockValue) { return _overrideValueSet; } } }


        /// <summary>
        /// The key of the desired key-value pair. Retrieval within the source key-value collection(s) will conform to the <see cref="StringComparer"/>, 
        /// if any, specified for the given <see cref="ConfigRepository"/>.
        /// </summary>
        public override sealed string Name { get { return _name; } }

        /// <summary>
        /// The concrete object parsed from the string value of the desired key-value pair. 
        /// If the string value cannot be found or for any reason cannot be parsed into the concrete type <see cref="T"/>, 
        /// and a default value was specified, that default value will be returned. 
        /// If no default value was specified, an exception will be thrown.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The key <see cref="Name"/> was not found, and no default value was specified.</exception>
        /// <exception cref="FormatException">The value found could not be parsed, and no default value was specified.</exception>
        /// <exception cref="InvalidOperationException">The concrete object failed the constraint function, if any, and no default value was specified.</exception>
        public T Value
        {
            get
            {
                lock (_lockValue)
                {
                    if (_overrideValueSet)
                    {
                        return _overrideValue;
                    }
                    if (!_valueSet)
                    {
                        _valueString = null;

                        // ensure key exists
                        string stringValue;
                        if (!TryGetValue(out stringValue))
                        {
                            return SetDefaultAsValueOrThrow<KeyNotFoundException>("A key-value pair with key {0} was not found in the given {1}", Name, nameof(ConfigRepository));
                        }
                        _valueString = stringValue;

                        // ensure string value is parsable
                        T value;
                        if (!_customTryParseFunction(stringValue, out value))
                        {
                            return SetDefaultAsValueOrThrow<FormatException>("Value {0} for parameter {1} could not be parsed into an object of type {2}", stringValue, Name, typeof(T));
                        }

                        // ensure T value fits constraint
                        if (!_constraintFunction(value))
                        {
                            return SetDefaultAsValueOrThrow<InvalidOperationException>("Value {0} for parameter {1} was disallowed by the constraint function", stringValue, Name);
                        }

                        // store T value and update
                        _value = value;
                        _valueSet = true;
                        _valueWasParsed = true;
                    }
                    return _value;
                }
            }
        }

        private T SetDefaultAsValueOrThrow<TException>(string baseMessage, params object[] messageParams)
            where TException : Exception
        {
            if (_defaultValueSet)
            {
                _value = _defaultValue;
                _valueSet = true;
                _valueWasParsed = false;
                return _value;
            }
            else
            {
                var ex = Activator.CreateInstance(typeof(TException), string.Format(_exceptionStringCulture, baseMessage, messageParams));
                throw (TException)ex;
            }
        }

        #endregion


        #region trygetvalue

        /// <summary>
        /// Attempts to retrieve the string value associated with the key <see cref="Name"/> from the given <see cref="ConfigRepository"/>. 
        /// This function is called when resolving the property <see cref="Value"/>. 
        /// By default, it returns <see cref="TryGetValueFromConfigRepository(string, out string)"/>, called with <see cref="Name"/>. 
        /// Override this function if non-standard value retrieval behavior is desired.
        /// </summary>
        /// <param name="value">The value associated with the key <see cref="Name"/>, if it was found; null otherwise.</param>
        /// <returns>True if the key was present in at least one key-value collection in the given <see cref="ConfigRepository"/>; false otherwise.</returns>
        protected virtual bool TryGetValue(out string value)
        {
            return TryGetValueFromConfigRepository(Name, out value);
        }

        /// <summary>
        /// Attempts to retrieve the string value associated with the key from the first possible key-value collection.
        /// </summary>
        /// <param name="name">The key whose value will be retrieved.</param>
        /// <param name="value">The value associated with the key, if it was found; null otherwise.</param>
        /// <returns>True if the key was present in at least one key-value collection; false otherwise.</returns>
        protected bool TryGetValueFromConfigRepository(string name, out string value)
        {
            return _configRepository.TryGetValue(name, out value);
        }

        /// <summary>
        /// Attempts to retrieve the string value associated with the key from the key-value collection at the specified index. 
        /// </summary>
        /// <param name="collectionIndex">The zero-based index of the key-value collection to attempt to retrieve the value from.</param>
        /// <param name="name">The key whose value will be retrieved.</param>
        /// <param name="value">The value associated with the key, if it was found; null otherwise.</param>
        /// <returns>True if the key was present in the specified key-value collection; false otherwise.</returns>
        /// <exception cref="ArgumentOutOfRangeException">collectionIndex was less than zero, or greater than the zero-indexed total number of key-value collections.</exception>
        protected bool TryGetValueFromConfigRepository(int collectionIndex, string name, out string value)
        {
            return _configRepository.TryGetValue(collectionIndex, name, out value);
        }

        #endregion


        // TODO: Cover both classes/ref types and structs/val types
        #region constructors

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/> and key.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        public ConfigParameter(ConfigRepository configRepository, string name)
        {
            Initialize(configRepository, name, null, null);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/>, key, and constraint function.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <param name="constraintFunction">A function that specifies a boolean condition that a successfully parsed concrete value must adhere to (for example, return false if an integer timeout is negative).</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        public ConfigParameter(ConfigRepository configRepository, string name, Func<T, bool> constraintFunction)
        {
            Initialize(configRepository, name, constraintFunction, null);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/>, key, and custom TryParse() function.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <param name="customTryParseFunction">A function that converts a string value to the concrete value type. Use when a particular non-default TryParse() behavior is desired, or when converting to a concrete type that lacks a native TryParse() function (except string, which is handled automatically).</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        public ConfigParameter(ConfigRepository configRepository, string name, TryParseFunction<T> customTryParseFunction)
        {
            Initialize(configRepository, name, null, customTryParseFunction);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/>, key, constraint function, and custom TryParse() function.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <param name="constraintFunction">A function that specifies a boolean condition that a successfully parsed concrete value must adhere to (for example, return false if an integer timeout is negative).</param>
        /// <param name="customTryParseFunction">A function that converts a string value to the concrete value type. Use when a particular non-default TryParse() behavior is desired, or when converting to a concrete type that lacks a native TryParse() function (except string, which is handled automatically).</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        public ConfigParameter(ConfigRepository configRepository, string name, Func<T, bool> constraintFunction, TryParseFunction<T> customTryParseFunction)
        {
            Initialize(configRepository, name, constraintFunction, customTryParseFunction);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/>, key, and default value.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <param name="defaultValue">A backup value, used as <see cref="Value"/> if parsing fails for any reason.</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        public ConfigParameter(ConfigRepository configRepository, string name, T defaultValue)
        {
            InitializeWithDefault(configRepository, name, defaultValue, null, null);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/>, key, default value, and constraint function.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <param name="defaultValue">A backup value, used as <see cref="Value"/> if parsing fails for any reason.</param>
        /// <param name="constraintFunction">A function that specifies a boolean condition that a successfully parsed concrete value must adhere to (for example, return false if an integer timeout is negative).</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        /// <exception cref="ArgumentException">The given <paramref name="defaultValue"/> failed the given <paramref name="constraintFunction"/>.</exception>
        // TODO: Split the ArgumentExceptions?
        public ConfigParameter(ConfigRepository configRepository, string name, T defaultValue, Func<T, bool> constraintFunction)
        {
            InitializeWithDefault(configRepository, name, defaultValue, constraintFunction, null);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/>, key, default value, and custom TryParse() function.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <param name="defaultValue">A backup value, used as <see cref="Value"/> if parsing fails for any reason.</param>
        /// <param name="customTryParseFunction">A function that converts a string value to the concrete value type. Use when a particular non-default TryParse() behavior is desired, or when converting to a concrete type that lacks a native TryParse() function (except string, which is handled automatically).</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        public ConfigParameter(ConfigRepository configRepository, string name, T defaultValue, TryParseFunction<T> customTryParseFunction)
        {
            InitializeWithDefault(configRepository, name, defaultValue, null, customTryParseFunction);
        }

        /// <summary>
        /// Initializes a <see cref="ConfigParameter{T}"/> with a <see cref="ConfigRepository"/>, key, default value, constraint function, and custom TryParse() function.
        /// </summary>
        /// <param name="configRepository">The <see cref="ConfigRepository"/> containing the key-value collection(s) that should specify this parameter's name and value as strings.</param>
        /// <param name="name">The key of the desired key-value pair.</param>
        /// <param name="defaultValue">A backup value, used as <see cref="Value"/> if parsing fails for any reason.</param>
        /// <param name="constraintFunction">A function that specifies a boolean condition that a successfully parsed concrete value must adhere to (for example, return false if an integer timeout is negative).</param>
        /// <param name="customTryParseFunction">A function that converts a string value to the concrete value type. Use when a particular non-default TryParse() behavior is desired, or when converting to a concrete type that lacks a native TryParse() function (except string, which is handled automatically).</param>
        /// <exception cref="ArgumentNullException">The given <paramref name="configRepository"/> was null, or the given <paramref name="name"/> was null or white space.</exception>
        /// <exception cref="ArgumentException">No TryParse() function could be found to convert a string value to the concrete value type.</exception>
        /// <exception cref="ArgumentException">The given <paramref name="defaultValue"/> failed the given <paramref name="constraintFunction"/>.</exception>
        public ConfigParameter(ConfigRepository configRepository, string name, T defaultValue, Func<T, bool> constraintFunction, TryParseFunction<T> customTryParseFunction)
        {
            InitializeWithDefault(configRepository, name, defaultValue, constraintFunction, customTryParseFunction);
        }


        private void InitializeWithDefault(ConfigRepository configRepository, string name, T defaultValue, Func<T, bool> constraintFunction, TryParseFunction<T> customTryParseFunction)
        {
            Initialize(configRepository, name, constraintFunction, customTryParseFunction);
            if (!_constraintFunction(defaultValue))
            {
                throw new ArgumentException(string.Format(_exceptionStringCulture, "The given {0} was disallowed by the given {1}", nameof(defaultValue), nameof(constraintFunction)));
            }
            _defaultValue = defaultValue;
            _defaultValueSet = true;
        }

        private void Initialize(ConfigRepository configRepository, string name, Func<T, bool> constraintFunction, TryParseFunction<T> customTryParseFunction)
        {
            if (configRepository == null)
            {
                throw new ArgumentNullException(nameof(configRepository));
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (customTryParseFunction == null)
            {
                customTryParseFunction = TryParseFunctionCache.GetTryParseFunction<T>();
                if (customTryParseFunction == null)
                {
                    throw new ArgumentException(string.Format(_exceptionStringCulture, "No TryParse() method for type {0} was passed in or found natively as a public static function", typeof(T)));
                }
            }

            _configRepository = configRepository;
            _configRepository.Register(_referenceKey, this);
            _name = name;
            if (constraintFunction != null)
            {
                _constraintFunction = constraintFunction;
            }
            _customTryParseFunction = customTryParseFunction;
        }

        ~ConfigParameter()
        {
            // _configRepository can be null if Initialize() throws an exception
            if (_configRepository != null)
            {
                _configRepository.Deregister(_referenceKey);
            }
        }

        #endregion


        #region refreshes and overrides

        /// <summary>
        /// Temporarily replace <see cref="Value"/> with the given value.
        /// </summary>
        /// <param name="value">The value to override <see cref="Value"/> with.</param>
        /// <exception cref="InvalidOperationException">The given <paramref name="value"/> failed the constraint function, if any.</exception>
        public void Override(T value)
        {
            if (!_constraintFunction(value))
            {
                throw new InvalidOperationException(string.Format(_exceptionStringCulture, "Override value {0} for parameter {1} was disallowed by the constraint function", value, Name));
            }
            lock (_lockValue)
            {
                _overrideValue = value;
                _overrideValueSet = true;
            }
        }

        /// <summary>
        /// Removes the replacement value specified with <see cref="Override(T)"/>, if any.
        /// </summary>
        public void ClearOverride()
        {
            ClearOverrideInternal();
        }

        internal override sealed void ClearOverrideInternal()
        {
            lock (_lockValue)
            {
                _overrideValue = default(T);
                _overrideValueSet = false;
            }
        }

        internal override sealed void RefreshInternal()
        {
            lock (_lockValue)
            {
                _value = default(T);
                _valueSet = false;
            }
        }

        #endregion


    }
}
