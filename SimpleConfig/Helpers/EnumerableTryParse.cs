using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace SimpleConfig.Helpers
{
    /// <summary>
    /// This class gives us the equivalent of <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/>, minus the original compile-time requirement that <see cref="TEnum"/> is a struct type. 
    /// Removing that restriction allows us to include this function in a single collection of generic TryParse delegates. 
    /// Enum-type enforcement is already handled internally with a runtime check; we simply defer to that. 
    /// This approach is preferred over wrapping <see cref="Enum.Parse(Type, string)"/>, primarily so we aren't forced to create and swallow an exception on failure. 
    /// All material has been copied from the .NET source, except where noted.  Altered lines have been commented out, and their replacements inserted directly below. 
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static class EnumerableTryParse
    {
        #region public functions

        //public static bool TryParse<TEnum>(String value, out TEnum result) where TEnum : struct
        public static bool TryParse<TEnum>(string value, out TEnum result)
        {
            return TryParse(value, false, out result);
        }

        //public static bool TryParse<TEnum>(String value, bool ignoreCase, out TEnum result) where TEnum : struct
        public static bool TryParse<TEnum>(string value, bool ignoreCase, out TEnum result)
        {
            result = default(TEnum);
            EnumResult parseResult = new EnumResult();
            parseResult.Init(false);
            bool retValue;

            if (retValue = TryParseEnum(typeof(TEnum), value, ignoreCase, ref parseResult))
                result = (TEnum)parseResult.parsedEnum;
            return retValue;
        }

        #endregion

        #region private/internal functions and support members, copied from source

        private static readonly char[] enumSeperatorCharArray = new char[] { ',' };

        private enum ParseFailureKind
        {
            None = 0,
            Argument = 1,
            ArgumentNull = 2,
            ArgumentWithParameter = 3,
            UnhandledException = 4
        }

        private struct EnumResult
        {
            internal object parsedEnum;
            internal bool canThrow;
            internal ParseFailureKind m_failure;
            internal string m_failureMessageID;
            internal string m_failureParameter;
            internal object m_failureMessageFormatArgument;
            internal Exception m_innerException;

            internal void Init(bool canMethodThrow)
            {
                parsedEnum = 0;
                canThrow = canMethodThrow;
            }
            internal void SetFailure(Exception unhandledException)
            {
                m_failure = ParseFailureKind.UnhandledException;
                m_innerException = unhandledException;
            }
            internal void SetFailure(ParseFailureKind failure, string failureParameter)
            {
                m_failure = failure;
                m_failureParameter = failureParameter;
                if (canThrow)
                    throw GetEnumParseException();
            }
            internal void SetFailure(ParseFailureKind failure, string failureMessageID, object failureMessageFormatArgument)
            {
                m_failure = failure;
                m_failureMessageID = failureMessageID;
                m_failureMessageFormatArgument = failureMessageFormatArgument;
                if (canThrow)
                    throw GetEnumParseException();
            }
            internal Exception GetEnumParseException()
            {
                switch (m_failure)
                {
                    case ParseFailureKind.Argument:
                        //return new ArgumentException(Environment.GetResourceString(m_failureMessageID));
                        return new ArgumentException(GetResourceString(m_failureMessageID));

                    case ParseFailureKind.ArgumentNull:
                        return new ArgumentNullException(m_failureParameter);

                    case ParseFailureKind.ArgumentWithParameter:
                        //return new ArgumentException(Environment.GetResourceString(m_failureMessageID, m_failureMessageFormatArgument));
                        return new ArgumentException(GetResourceString(m_failureMessageID, m_failureMessageFormatArgument));

                    case ParseFailureKind.UnhandledException:
                        return m_innerException;

                    default:
                        Contract.Assert(false, "Unknown EnumParseFailure: " + m_failure);
                        //return new ArgumentException(Environment.GetResourceString("Arg_EnumValueNotFound"));
                        return new ArgumentException(GetResourceString("Arg_EnumValueNotFound"));
                }
            }
        }

        private static bool TryParseEnum(Type enumType, string value, bool ignoreCase, ref EnumResult parseResult)
        {
            if (enumType == null)
                throw new ArgumentNullException("enumType");
            Contract.EndContractBlock();

            //RuntimeType rtType = enumType as RuntimeType;
            //if (rtType == null)
            //    throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "enumType");

            if (!enumType.IsEnum)
                //throw new ArgumentException(Environment.GetResourceString("Arg_MustBeEnum"), "enumType");
                throw new ArgumentException(GetResourceString("Arg_MustBeEnum"), "enumType");

            if (value == null)
            {
                parseResult.SetFailure(ParseFailureKind.ArgumentNull, "value");
                return false;
            }

            value = value.Trim();
            if (value.Length == 0)
            {
                parseResult.SetFailure(ParseFailureKind.Argument, "Arg_MustContainEnumInfo", null);
                return false;
            }

            // We have 2 code paths here. One if they are values else if they are Strings.
            // values will have the first character as as number or a sign.
            ulong result = 0;

            if (Char.IsDigit(value[0]) || value[0] == '-' || value[0] == '+')
            {
                //Type underlyingType = GetUnderlyingType(enumType);
                Type underlyingType = Enum.GetUnderlyingType(enumType);
                Object temp;

                try
                {
                    temp = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
                    //parseResult.parsedEnum = ToObject(enumType, temp);
                    parseResult.parsedEnum = Enum.ToObject(enumType, temp);
                    return true;
                }
                catch (FormatException)
                { // We need to Parse this as a String instead. There are cases
                  // when you tlbimp enums that can have values of the form "3D".
                  // Don't fix this code.
                }
                catch (Exception ex)
                {
                    if (parseResult.canThrow)
                        throw;
                    else
                    {
                        parseResult.SetFailure(ex);
                        return false;
                    }
                }
            }

            string[] values = value.Split(enumSeperatorCharArray);

            // Find the field.Lets assume that these are always static classes because the class is
            //  an enum.
            //ValuesAndNames entry = GetCachedValuesAndNames(rtType, true);
            EnumNameAndValueCache entry = GetCachedNamesAndValues(enumType);

            string[] enumNames = entry.Names;
            ulong[] enumValues = entry.Values;

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = values[i].Trim(); // We need to remove whitespace characters

                bool success = false;

                for (int j = 0; j < enumNames.Length; j++)
                {
                    if (ignoreCase)
                    {
                        if (string.Compare(enumNames[j], values[i], StringComparison.OrdinalIgnoreCase) != 0)
                            continue;
                    }
                    else
                    {
                        if (!enumNames[j].Equals(values[i]))
                            continue;
                    }

                    ulong item = enumValues[j];

                    result |= item;
                    success = true;
                    break;
                }

                if (!success)
                {
                    // Not found, throw an argument exception.
                    parseResult.SetFailure(ParseFailureKind.ArgumentWithParameter, "Arg_EnumValueNotFound", value);
                    return false;
                }
            }

            try
            {
                //parseResult.parsedEnum = ToObject(enumType, result);
                parseResult.parsedEnum = Enum.ToObject(enumType, result);
                return true;
            }
            catch (Exception ex)
            {
                if (parseResult.canThrow)
                    throw;
                else
                {
                    parseResult.SetFailure(ex);
                    return false;
                }
            }
        }

        #endregion

        #region private/internal functions and support members, reimplemented from scratch

        // Environment.GetResourceString() is used in several places; this gives us access
        internal delegate string GetResourceStringDelegate(string key, params object[] values);
        private static readonly MethodInfo _GetResourceStringMethodInfo =
            typeof(Environment).GetMethod("GetResourceString", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(object[]) }, null);
        internal static readonly GetResourceStringDelegate GetResourceString =
            (GetResourceStringDelegate)Delegate.CreateDelegate(typeof(GetResourceStringDelegate), _GetResourceStringMethodInfo);

        // The original enum name/value cache was a ValuesAndNames object, which led into a morass of non-public code; 
        // reimplementing the functionality here was far easier than attempting to gain access to all the necessary .NET source
        internal class EnumNameAndValueCache
        {
            public string[] Names { get; private set; }
            public ulong[] Values { get; private set; }

            public EnumNameAndValueCache(Type enumType)
            {
                Debug.Assert(enumType.IsEnum);

                Names = Enum.GetNames(enumType);
                Values = Enum.GetValues(enumType).Cast<Enum>().Select(e => Convert.ToUInt64(e, CultureInfo.InvariantCulture)).ToArray();
            }
        }

        private static ConcurrentDictionary<Type, EnumNameAndValueCache> _NameValueArrayCache = new ConcurrentDictionary<Type, EnumNameAndValueCache>();

        internal static EnumNameAndValueCache GetCachedNamesAndValues(Type enumType)
        {
            Debug.Assert(enumType.IsEnum);
            return _NameValueArrayCache.GetOrAdd(enumType, (key) => new EnumNameAndValueCache(key));
        }

        internal static void Clear()
        {
            _NameValueArrayCache.Clear();
        }

        #endregion

    }
}
