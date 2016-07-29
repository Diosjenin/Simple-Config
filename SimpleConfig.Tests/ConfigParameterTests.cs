using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleConfig.Helpers;
using SimpleConfig.Tests.TestHelpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static SimpleConfig.Tests.TestHelpers.TestHelperMethods;

namespace SimpleConfig.Tests
{
    [TestClass, ExcludeFromCodeCoverage]
    public class ConfigParameterTests
    {
        #region test helpers

        private const string _testIntParameterName = "testInt";
        private const string _testIntParameterNotPresentName = "notPresent";
        private const int _testIntParameterValue = 3;
        private const int _testIntParameterValuePlusOne = _testIntParameterValue + 1;
        private static readonly ConfigRepository _testCr = new ConfigRepository(new Dictionary<string, string> {
            { _testIntParameterName, _testIntParameterValue.ToString() }
        });

        private bool _tryParseIntTimesTen(string value, out int output)
        {
            bool b = int.TryParse(value, out output);
            if (b)
            {
                output *= 10;
            }
            return b;
        }


        [Flags]
        private enum OverloadType
        {
            None = 1 << 0,
            Default = 1 << 1,
            Constraint = 1 << 2,
            TryParse = 1 << 3,
            All = 1 << 31
        }

        private delegate ConfigParameter<T> Overload<T>(ConfigRepository r, string n, T d, Func<T, bool> cf, TryParseFunction<T> tpf);

        private static IEnumerable<Overload<T>> GetOverloads<T>(OverloadType include = OverloadType.All, OverloadType exclude = OverloadType.None)
        {
            // not a dictionary; we want to maintain input order so manual debugging is a bit easier
            var dictionary = new List<KeyValuePair<OverloadType, Overload<T>>>()
            {
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n)),
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All | OverloadType.Default, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n, d)),
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All | OverloadType.Constraint, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n, cf)),
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All | OverloadType.TryParse, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n, tpf)),
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All | OverloadType.Default | OverloadType.Constraint, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n, d, cf)),
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All | OverloadType.Default | OverloadType.TryParse, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n, d, tpf)),
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All | OverloadType.Constraint | OverloadType.TryParse, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n, cf, tpf)),
                 new KeyValuePair<OverloadType, Overload<T>>(OverloadType.All | OverloadType.Default | OverloadType.Constraint | OverloadType.TryParse, (r, n, d, cf, tpf) => new ConfigParameter<T>(r, n, d, cf, tpf))
            };

            List<Overload<T>> lst = new List<Overload<T>>();
            foreach (var kvp in dictionary)
            {
                if (kvp.Key.HasFlag(include) && !kvp.Key.HasFlag(exclude))
                {
                    lst.Add(kvp.Value);
                }
            }

            // sanity check
            if (lst.Count == 0)
            {
                throw new ArgumentException("No overloads returned");
            }

            return lst;
        }

        #endregion


        #region bad args

        [TestMethod]
        public void ConfigParameterConstructorBadArgsConfigRepositoryNull()
        {
            ConstructorBadArgsHelper<int, ArgumentNullException>(null, _testIntParameterName);
        }

        [TestMethod]
        public void ConfigParameterConstructorBadArgsNameNull()
        {
            ConstructorBadArgsHelper<int, ArgumentNullException>(_testCr, null);
        }

        [TestMethod]
        public void ConfigParameterConstructorBadArgsNameEmpty()
        {
            ConstructorBadArgsHelper<int, ArgumentNullException>(_testCr, string.Empty);
        }

        [TestMethod]
        public void ConfigParameterConstructorBadArgsNameWhiteSpace()
        {
            ConstructorBadArgsHelper<int, ArgumentNullException>(_testCr, " ");
        }

        [TestMethod]
        public void ConfigParameterConstructorBadArgsTypeHasNoTryParse()
        {
            ConstructorBadArgsHelper<Type, ArgumentException>(_testCr, _testIntParameterName);
        }

        [TestMethod]
        public void ConfigParameterConstructorBadArgsDefaultValueFailsConstraint()
        {
            ConstructorBadArgsHelper<int, ArgumentException>(_testCr, _testIntParameterName, (i) => i != default(int));
        }

        private static void ConstructorBadArgsHelper<TType, TException>(ConfigRepository repo, string name, Func<TType, bool> cf = null)
            where TException : Exception
        {
            var include = cf == null ? OverloadType.All : OverloadType.Default | OverloadType.Constraint;
            foreach (var o in GetOverloads<TType>(include))
            {
                AssertThrows<TException>(() => o(repo, name, default(TType), cf, null));
            }
        }

        #endregion

        #region bad value

        [TestMethod]
        public void ConfigParameterConstructorBadValueKeyNotPresent()
        {
            // parse should fail if key cannot be found
            ConstructorBadValueHelper<int, KeyNotFoundException>(_testIntParameterNotPresentName);
        }

        [TestMethod]
        public void ConfigParameterConstructorBadValueValueNotParsableIntoType()
        {
            // parse should fail if value cannot be parsed into type T
            ConstructorBadValueHelper<DateTime, FormatException>(_testIntParameterName);
        }

        [TestMethod]
        public void ConfigParameterConstructorBadValueValueFailsConstraint()
        {
            // parse should fail if value fails constraint with default TryParse
            ConstructorBadValueHelper<int, InvalidOperationException>(_testIntParameterName, _testIntParameterValuePlusOne, (i) => i != _testIntParameterValue);
        }

        [TestMethod]
        public void ConfigParameterConstructorBadValueTryParseFailsConstraint()
        {
            // parse should fail if value fails constraint with custom TryParse
            ConstructorBadValueHelper<int, InvalidOperationException>(_testIntParameterName, _testIntParameterValue, (i) => i == _testIntParameterValue, _tryParseIntTimesTen);
        }

        private static void ConstructorBadValueHelper<TType, TException>(string name, TType d = default(TType), Func<TType, bool> cf = null, TryParseFunction<TType> tpf = null)
            where TException : Exception
        {
            var includeWithDefault = OverloadType.Default;
            var include = OverloadType.All;
            if (cf != null)
            {
                includeWithDefault |= OverloadType.Constraint;
                include |= OverloadType.Constraint;
            }
            if (tpf != null)
            {
                includeWithDefault |= OverloadType.TryParse;
                include |= OverloadType.TryParse;
            }

            // without default, throw exception
            foreach (var o in GetOverloads<TType>(include, OverloadType.Default))
            {
                var p = o(_testCr, name, d, cf, tpf);
                AssertThrows<TException>(() => p.Value.Equals(d));
            }

            // with default, apply default
            foreach (var o in GetOverloads<TType>(includeWithDefault))
            {
                var p = o(_testCr, name, d, cf, tpf);
                Assert.IsTrue(p.Value.Equals(d));
                Assert.IsTrue(p.ValueEqualsDefault);
                Assert.IsFalse(p.ValueWasParsed);
            }
        }

        #endregion

        #region constructors

        [TestMethod]
        public void ConfigParameterConstructorBase()
        {
            ConstructorHelper(_testIntParameterName, _testIntParameterValuePlusOne);
        }

        [TestMethod]
        public void ConfigParameterConstructorDefaultValue()
        {
            // on success, proceed as normal
            ConstructorHelper(_testIntParameterName, _testIntParameterValuePlusOne, OverloadType.Default);

            // on fail, apply default
            ConstructorHelper(_testIntParameterNotPresentName, _testIntParameterValuePlusOne, OverloadType.Default);
        }

        [TestMethod]
        public void ConfigParameterConstructorConstraint()
        {
            // without default, proceed as normal
            ConstructorHelper(_testIntParameterName, _testIntParameterValuePlusOne, OverloadType.Constraint, OverloadType.Default, (i) => i == _testIntParameterValue);

            // with default, apply default
            ConstructorHelper(_testIntParameterNotPresentName, _testIntParameterValuePlusOne, OverloadType.Constraint | OverloadType.Default, (i) => i == _testIntParameterValuePlusOne);
        }

        [TestMethod]
        public void ConfigParameterConstructorTryParse()
        {
            // custom TryParse should be applied
            ConstructorHelper(_testIntParameterName, _testIntParameterValuePlusOne, OverloadType.TryParse, null, _tryParseIntTimesTen);
        }

        [TestMethod]
        public void ConfigParameterConstructorConstraintAndTryParse()
        {
            // constraint should succeed with custom TryParse, where standard TryParse would fail
            ConstructorHelper(_testIntParameterName, _testIntParameterValuePlusOne, OverloadType.Constraint | OverloadType.TryParse, (i) => i != _testIntParameterValue, _tryParseIntTimesTen);
        }

        private static void ConstructorHelper(string name, int d, OverloadType include = OverloadType.All, Func<int, bool> cf = null, TryParseFunction<int> tpf = null)
        {
            ConstructorHelper(name, d, include, OverloadType.None, cf, tpf);
        }

        private static void ConstructorHelper(string name, int d, OverloadType include, OverloadType exclude, Func<int, bool> cf = null, TryParseFunction<int> tpf = null)
        {
            bool defaultShouldBeUsed = include.HasFlag(OverloadType.Default) && name != _testIntParameterName;
            foreach (var o in GetOverloads<int>(include, exclude))
            {
                var p = o(_testCr, name, d, cf, tpf);

                Assert.IsTrue(p.Name == name);
                Assert.IsTrue(p.ValueEqualsDefault == defaultShouldBeUsed);
                Assert.IsTrue(p.ValueWasParsed != defaultShouldBeUsed);
                Assert.IsFalse(p.ValueIsOverridden);

                // without default, assert successful parse
                if (!defaultShouldBeUsed)
                {
                    Assert.IsTrue(p.ValueString == _testIntParameterValue.ToString());

                    int output;
                    TryParseFunction<int> assertTpf = tpf ?? int.TryParse;
                    Assert.IsTrue(assertTpf(p.ValueString, out output));
                    Assert.IsTrue(p.Value == output);
                }

                // with default, assert unsuccessful parse
                else
                {
                    Assert.IsTrue(string.IsNullOrEmpty(p.ValueString));
                    Assert.IsTrue(p.Value == d);
                }
            }
        }

        #endregion

        #region refresh

        [TestMethod]
        public void ConfigParameterRefresh()
        {
            // without default, proceed as normal
            RefreshHelper(_testIntParameterName, _testIntParameterValuePlusOne, exclude: OverloadType.Default);

            // with default, apply default, refresh, assert default removed
            RefreshHelper(_testIntParameterNotPresentName, _testIntParameterValue, OverloadType.Default);
        }

        private static void RefreshHelper(string name, int defaultValue, OverloadType include = OverloadType.All, OverloadType exclude = OverloadType.None)
        {
            bool useDefault = include.HasFlag(OverloadType.Default);
            var sourceDictionary = new Dictionary<string, string> { { _testIntParameterName, _testIntParameterValue.ToString() } };

            foreach (var o in GetOverloads<int>(include, exclude))
            {
                var d = new ConcurrentDictionary<string, string>(sourceDictionary);
                ConfigRepository cr = new ConfigRepository(d);

                var p = o(cr, name, defaultValue, null, null);
                Assert.IsTrue(p.Value == _testIntParameterValue);
                Assert.IsTrue(p.ValueWasParsed != useDefault);
                Assert.IsTrue(p.ValueEqualsDefault == useDefault);

                d.AddOrUpdate(name, _testIntParameterValuePlusOne.ToString(), (k, v) => _testIntParameterValuePlusOne.ToString());
                Assert.IsTrue(p.Value == _testIntParameterValue);
                Assert.IsTrue(p.ValueWasParsed != useDefault);
                Assert.IsTrue(p.ValueEqualsDefault == useDefault);

                cr.RefreshAll();
                Assert.IsTrue(p.Value == _testIntParameterValuePlusOne);
                Assert.IsTrue(p.ValueWasParsed);
                Assert.IsFalse(p.ValueEqualsDefault);
            }
        }

        [TestMethod]
        public void ConfigParameterRefreshFailsConstraint()
        {
            // constraint should apply to new Value parsed after refresh
            RefreshFailsConstraintHelper(OverloadType.Constraint, OverloadType.Default);
            RefreshFailsConstraintHelper(OverloadType.Constraint | OverloadType.Default);
        }

        private static void RefreshFailsConstraintHelper(OverloadType include, OverloadType exclude = OverloadType.None)
        {
            bool useDefault = include.HasFlag(OverloadType.Default);
            var sourceDictionary = new Dictionary<string, string> { { _testIntParameterName, _testIntParameterValue.ToString() } };

            foreach (var o in GetOverloads<int>(include, exclude))
            {
                var d = new Dictionary<string, string>(sourceDictionary);
                var cr = new ConfigRepository(d);

                var p = o(cr, _testIntParameterName, _testIntParameterValuePlusOne, (i) => i >= 0, null);
                Assert.IsTrue(p.Value == _testIntParameterValue);

                d[_testIntParameterName] = "-1";
                Assert.IsTrue(p.Value == _testIntParameterValue);

                cr.RefreshAll();

                // without default, throw exception
                if (!useDefault)
                {
                    AssertThrows<InvalidOperationException>(() => p.Value.Equals(_testIntParameterValuePlusOne));
                }
                // with default, apply default
                else
                {
                    Assert.IsTrue(p.Value == _testIntParameterValuePlusOne);
                }
            }
        }

        #endregion

        #region override

        // TODO: Deal with failure sans default
        [TestMethod]
        public void ConfigParameterSingleOverride()
        {
            // without default, proceed as normal
            OverrideHelper(_testIntParameterName, (p) => p.ClearOverride());

            // with default, apply default
            OverrideHelper(_testIntParameterNotPresentName, (p) => p.ClearOverride(), OverloadType.Default);
        }

        [TestMethod]
        public void ConfigParameterMassOverride()
        {
            // without default, proceed as normal
            OverrideHelper(_testIntParameterName, (p) => _testCr.ClearOverrideAll());

            // with default, apply default
            OverrideHelper(_testIntParameterNotPresentName, (p) => _testCr.ClearOverrideAll(), OverloadType.Default);
        }

        private static void OverrideHelper(string name, Action<ConfigParameter<int>> clearAction, OverloadType include = OverloadType.All)
        {
            bool useDefault = include.HasFlag(OverloadType.Default);
            int intendedValue = useDefault ? _testIntParameterValuePlusOne : _testIntParameterValue;

            foreach (var o in GetOverloads<int>(include))
            {
                var p = o(_testCr, name, _testIntParameterValuePlusOne, null, null);

                Assert.IsFalse(p.ValueIsOverridden);
                Assert.IsFalse(p.ValueWasParsed);
                // don't test ValueEqualToDefault yet, as it will calculate Value

                Assert.IsTrue(p.Value == intendedValue);
                Assert.IsFalse(p.ValueIsOverridden);
                Assert.IsTrue(p.ValueWasParsed != useDefault);
                Assert.IsTrue(p.ValueEqualsDefault == useDefault);

                p.Override(p.Value - 1);
                Assert.IsTrue(p.Value == intendedValue - 1);
                Assert.IsTrue(p.ValueIsOverridden);
                Assert.IsFalse(p.ValueWasParsed);
                Assert.IsFalse(p.ValueEqualsDefault);

                clearAction(p);
                Assert.IsTrue(p.Value == intendedValue);
                Assert.IsFalse(p.ValueIsOverridden);
                Assert.IsTrue(p.ValueWasParsed != useDefault);
                Assert.IsTrue(p.ValueEqualsDefault == useDefault);
            }
        }

        [TestMethod]
        public void ConfigParameterOverrideFailsConstraint()
        {
            foreach (var o in GetOverloads<int>(OverloadType.Constraint))
            {
                ConfigParameter<int> param = o(_testCr, _testIntParameterName, _testIntParameterValuePlusOne, (i) => i == _testIntParameterValuePlusOne, null);
                AssertThrows<InvalidOperationException>(() => param.Override(_testIntParameterValue));
            }
        }

        #endregion

        #region atypical types

        [TestMethod]
        public void ConfigParameterAtypicalTypeString()
        {
            // string should work automatically despite having no TryParse
            AtypicalTypesHelper(_testIntParameterValue.ToString(), _testIntParameterName);
        }

        [TestMethod]
        public void ConfigParameterAtypicalTypeGenericEnum()
        {
            // enum TryParse input should not be restricted to structs
            AtypicalTypesHelper(TestEnumDefault.OptionOne);
        }

        [TestMethod]
        [SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags")]
        public void ConfigParameterAtypicalTypeFlagsEnum()
        {
            // enum TryParse input should correctly handle flags
            AtypicalTypesHelper(TestEnumFlags.OptionOne | TestEnumFlags.OptionTwo);
        }

        [TestMethod]
        public void ConfigParameterAtypicalTypeCustomClass()
        {
            // custom TryParse should be discoverable at runtime
            AtypicalTypesHelper(new TryParseTestClass("a,b"));
        }

        private static void AtypicalTypesHelper<T>(T obj, string name = _testIntParameterNotPresentName)
        {
            var d = new Dictionary<string, string>() { { name, obj.ToString() } };
            var cr = new ConfigRepository(d);
            foreach (var o in GetOverloads<T>())
            {
                var p = o(cr, name, default(T), null, null);
                Assert.IsTrue(p.Value.Equals(obj));
            }
        }

        #endregion

        #region thread safety

        [TestMethod]
        public void ConfigParameterThreadSafetyReadAndRefresh()
        {
            // reading Value while refreshing should not yield invalid Value
            var p = new ConfigParameter<int>(_testCr, _testIntParameterName);

            Action loop = () =>
            {
                _testCr.RefreshAll();
            };
            Action bg = () =>
            {
                var value = p.Value;
                Assert.IsTrue(value == _testIntParameterValue);
            };

            RunTwoTasks(5000, loop, bg);
        }

        [TestMethod]
        public void ConfigParameterThreadSafetyReadAndClearOverride()
        {
            // reading Value while overriding/clearing should not yield invalid Value
            var p = new ConfigParameter<int>(_testCr, _testIntParameterName);
            var newValue = _testIntParameterValue + 1;

            Action loop = () =>
            {
                p.Override(newValue);
                p.ClearOverride();
            };
            Action bg = () =>
            {
                var value = p.Value;
                Assert.IsTrue(value == _testIntParameterValue || value == newValue);
            };

            RunTwoTasks(10000, loop, bg);
        }

        [TestMethod]
        public void ConfigParameterThreadSafetyMultipleRead()
        {
            // multiple Value reads should not yield invalid Value
            var p = new ConfigParameter<int>(_testCr, _testIntParameterName);

            Action loop = () =>
            {
                var value = p.Value;
                Assert.IsTrue(value == _testIntParameterValue);
            };
            Action bg = () =>
            {
                var value = p.Value;
                Assert.IsTrue(value == _testIntParameterValue);
                _testCr.RefreshAll();
            };

            RunTwoTasks(1000000, loop, bg);
        }

        // todo: add multiple refresh/multiple clear override tests, param specific

        #endregion

        #region inheritance

        [TestMethod]
        public void ConfigParameterInheritance()
        {
            // classes inheriting from ConfigParameter should be able to alter value retrieval via protected override of TryGetValue
            string key1 = "key1";
            string key1Alt = "key1Alt";
            string key2 = "key2";
            string key2Alt = "key2Alt";
            string key3 = "key3";
            string key3Alt = "key3Alt";
            int value1 = 1;
            int value2 = 2;
            int value3 = 3;
            int defaultValue = 10;

            var d1 = new Dictionary<string, string>() { { key1, value1.ToString() } };
            var d2 = new Dictionary<string, string>() { { key1Alt, value2.ToString() }, { key2, value2.ToString() }, { key3Alt, value1.ToString() } };
            var d3 = new Dictionary<string, string>() { { key3, value3.ToString() }, { key2Alt, value1.ToString() } };
            var repo = new ConfigRepository(d1, d2, d3);

            var iParam1 = new InheritedParameter<int>(repo, key1, null, defaultValue);
            Assert.IsTrue(iParam1.Value == value1);
            var iParam1Alt1 = new InheritedParameter<int>(repo, key1, key1Alt, defaultValue);
            Assert.IsTrue(iParam1Alt1.Value == value1);
            var iParam1Alt2 = new InheritedParameter<int>(repo, "asdf", key1Alt, defaultValue);
            Assert.IsTrue(iParam1Alt2.Value == value2);

            var iParam2 = new InheritedParameter<int>(repo, key2, null, defaultValue);
            Assert.IsTrue(iParam2.Value == defaultValue);
            var iParam2Alt1 = new InheritedParameter<int>(repo, key2, key2Alt, defaultValue);
            Assert.IsTrue(iParam2Alt1.Value == defaultValue);
            var iParam2Alt2 = new InheritedParameter<int>(repo, "asdf", key2Alt, defaultValue);
            Assert.IsTrue(iParam2Alt2.Value == defaultValue);

            var iParam3 = new InheritedParameter<int>(repo, key3, null, defaultValue);
            Assert.IsTrue(iParam3.Value == value3);
            var iParam3Alt1 = new InheritedParameter<int>(repo, key3, key3Alt, defaultValue);
            Assert.IsTrue(iParam3Alt1.Value == value1);
            var iParam3Alt2 = new InheritedParameter<int>(repo, "asdf", key3Alt, defaultValue);
            Assert.IsTrue(iParam3Alt2.Value == value1);
        }

        #endregion


    }
}
