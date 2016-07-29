using System;
using System.Diagnostics.CodeAnalysis;

namespace SimpleConfig.Tests.TestHelpers
{
    public enum TestEnumDefault // int
    {
        OptionZero,
        OptionOne
    }

    public enum TestEnumInt : int
    {
        OptionZero = 0,
        OptionOne = int.MinValue
    }

    [SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")]
    public enum TestEnumUInt : uint
    {
        OptionZero = 0,
        OptionOne = uint.MaxValue
    }

    [SuppressMessage("Microsoft.Design", "CA1027:MarkEnumsWithFlags")]
    [SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")]
    public enum TestEnumLong : long
    {
        OptionZero = 0,
        OptionOne = long.MinValue
    }

    [SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")]
    public enum TestEnumULong : ulong
    {
        OptionZero = 0,
        OptionOne = ulong.MaxValue
    }

    [Flags]
    [SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags")]
    [SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")]
    public enum TestEnumFlags : ulong
    {
        OptionZero = 1ul,
        OptionOne = 1ul << 31,
        OptionTwo = 1ul << 63
        // TODO: File issue/bug report to the .NET Core/Framework teams.
        // Setting the 64th bit breaks the TypeConverter class, because EnumConverter tries parsing to long instead of ulong.
        // Among other things, this breaks the official Microsoft.Extensions.Configuration in ASP.NET Core.
    }


    [ExcludeFromCodeCoverage]
    public class TryParseTestClass
    {
        public string First { get; private set; }
        public string Second { get; private set; }

        public TryParseTestClass(string input)
        {
            if (!ValidateInputPresent(input))
            {
                throw new ArgumentNullException(nameof(input), nameof(input) + " cannot be null");
            }
            if (!ValidateInputFormatting(input))
            {
                throw new ArgumentException(nameof(input) + " cannot be null", nameof(input));
            }
            var split = input.Split(',');
            First = split[0];
            Second = split[1];
        }
        private static bool ValidateInputPresent(string input)
        {
            return !string.IsNullOrEmpty(input);
        }
        private static bool ValidateInputFormatting(string input)
        {
            return (input.Split(',')).Length == 2;
        }

        public override string ToString()
        {
            return string.Join(",", new[] { First, Second });
        }

        // should be automatically recognized and used for ConfigParameter<TryParseTestClass> instances
        public static bool TryParse(string value, out TryParseTestClass output)
        {
            if (ValidateInputPresent(value) && ValidateInputFormatting(value))
            {
                output = new TryParseTestClass(value);
                return true;
            }
            output = default(TryParseTestClass);
            return false;
        }

        public bool Equals(TryParseTestClass other)
        {
            return !ReferenceEquals(other, null)
                && First == other.First
                && Second == other.Second;
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as TryParseTestClass);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }


}
