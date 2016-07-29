using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SimpleConfig.Helpers
{
    public delegate bool TryParseFunction<T>(string input, out T output);

    internal static class TryParseFunctionCache
    {
        private static bool _tryParseString(string input, out string output)
        {
            output = input;
            return true;
        }

        private static ConcurrentDictionary<Type, Delegate> _genericTryParseFunctions = new ConcurrentDictionary<Type, Delegate>() { };

        public static TryParseFunction<T> GetTryParseFunction<T>()
        {
            var type = typeof(T);
            if (!_genericTryParseFunctions.ContainsKey(type))
            {
                if (type.IsEnum)
                {
                    return _genericTryParseFunctions.GetOrAdd(type, (t) => (TryParseFunction<T>)EnumerableTryParse.TryParse) as TryParseFunction<T>;
                }
                else if (type == typeof(string))
                {
                    return _genericTryParseFunctions.GetOrAdd(type, (t) => (TryParseFunction<string>)_tryParseString) as TryParseFunction<T>;
                }
                else
                {
                    var method = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), type.MakeByRefType() }, null);
                    if (method != null)
                    {
                        return _genericTryParseFunctions.GetOrAdd(type, (t) => Delegate.CreateDelegate(typeof(TryParseFunction<T>), method)) as TryParseFunction<T>;
                    }
                }
            }

            Delegate d;
            if (_genericTryParseFunctions.TryGetValue(type, out d))
            {
                return d as TryParseFunction<T>;
            }
            return null;
        }

        internal static void Clear()
        {
            _genericTryParseFunctions.Clear();
        }

    }
}
