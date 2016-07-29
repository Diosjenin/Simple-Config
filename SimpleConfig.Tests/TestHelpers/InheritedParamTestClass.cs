using SimpleConfig.Helpers;
using System;
using System.Diagnostics.CodeAnalysis;

namespace SimpleConfig.Tests.TestHelpers
{
    [ExcludeFromCodeCoverage]
    public class InheritedParameter<T> : ConfigParameter<T>
    {
        public string AltName { get; private set; }

        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated in base constructor")]
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Test class; don't care")]
        public InheritedParameter(ConfigRepository repo, string name, string altName, T defaultValue, Func<T, bool> constraintFunction = null, TryParseFunction<T> customTryParseFunction = null)
            : base(repo, name, defaultValue, constraintFunction, customTryParseFunction)
        {
            AltName = string.IsNullOrWhiteSpace(altName) ? string.Empty : altName;
        }

        protected override bool TryGetValue(out string value)
        {
            if (TryGetValueFromConfigRepository(0, Name, out value) || TryGetValueFromConfigRepository(0, AltName, out value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            if (TryGetValueFromConfigRepository(1, AltName, out value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            if (TryGetValueFromConfigRepository(2, Name, out value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            value = null;
            return false;
        }
    }


}
