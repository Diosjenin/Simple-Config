using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using static SimpleConfig.Tests.TestHelpers.TestHelperMethods;

namespace SimpleConfig.Tests
{
    [TestClass, ExcludeFromCodeCoverage]
    public class ConfigRepositoryTests
    {
        #region test helpers

        private const string _key1 = "foo1";
        private const string _key2 = "foo2";
        private const string _key3 = "foo3";
        private const string _value1 = "bar1";
        private const string _value2 = "bar2";
        private const string _value3 = "bar3";

        private static readonly Dictionary<string, string> _one = new Dictionary<string, string> { { _key1, _value1 }, { _key2, _value2 } };
        private static readonly Dictionary<string, string> _two = new Dictionary<string, string> { { _key1, _value2 }, { _key3, _value3 } };


        private static Dictionary<string, string> d(IEnumerable<KeyValuePair<string, string>> input)
        {
            if (input == null)
            {
                return null;
            }
            var ret = new Dictionary<string, string>();
            foreach (var kvp in input)
            {
                ret.Add(kvp.Key, kvp.Value);
            }
            return ret;
        }
        private static NameValueCollection n(IEnumerable<KeyValuePair<string, string>> input)
        {
            if (input == null)
            {
                return null;
            }
            var ret = new NameValueCollection();
            foreach (var kvp in input)
            {
                ret.Add(kvp.Key, kvp.Value);
            }
            return ret;
        }
        private static NameValueCollectionWrapper w(IEnumerable<KeyValuePair<string, string>> input)
        {
            return new NameValueCollectionWrapper(n(input));
        }


        [Flags]
        private enum OverloadType
        {
            None = 1 << 0,
            Dictionary = 1 << 1,
            NVC = 1 << 2,
            Wrapper = 1 << 3,
            Comparer = 1 << 4,
            Secondaries = 1 << 5,
            All = 1 << 31
        }

        private delegate ConfigRepository Overload(StringComparer comp, IEnumerable<KeyValuePair<string, string>> one, IEnumerable<KeyValuePair<string, string>> two = null);

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Not my fault Microsoft counts one lambda as two hits")]
        private static IEnumerable<Overload> GetOverloads(OverloadType include = OverloadType.All, OverloadType exclude = OverloadType.None)
        {
            // not a dictionary; we want to maintain input order so manual debugging is a bit easier
            var dictionary = new List<KeyValuePair<OverloadType, Overload>>()
            {
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Dictionary, (comp, one, two) => new ConfigRepository(d(one))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Dictionary | OverloadType.Comparer, (comp, one, two) => new ConfigRepository(comp, d(one))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Dictionary | OverloadType.Secondaries, (comp, one, two) => new ConfigRepository(d(one), d(two))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Dictionary | OverloadType.Comparer | OverloadType.Secondaries, (comp, one, two) => new ConfigRepository(comp, d(one), d(two))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.NVC, (comp, one, two) => new ConfigRepository(n(one))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.NVC | OverloadType.Comparer, (comp, one, two) => new ConfigRepository(comp, n(one))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.NVC | OverloadType.Secondaries, (comp, one, two) => new ConfigRepository(n(one), n(two))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.NVC | OverloadType.Comparer | OverloadType.Secondaries, (comp, one, two) => new ConfigRepository(comp, n(one), n(two))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Wrapper, (comp, one, two) => new ConfigRepository(w(one))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Wrapper | OverloadType.Comparer, (comp, one, two) => new ConfigRepository(comp, w(one))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Wrapper | OverloadType.Secondaries, (comp, one, two) => new ConfigRepository(w(one), w(two))),
                 new KeyValuePair<OverloadType, Overload>(OverloadType.All | OverloadType.Wrapper | OverloadType.Comparer | OverloadType.Secondaries, (comp, one, two) => new ConfigRepository(comp, w(one), w(two)))
            };

            List<Overload> lst = new List<Overload>();
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
        public void ConfigRepositoryConstructorBadArgsPrimaryNull()
        {
            ConstructorBadArgsHelper(null);
        }

        [TestMethod]
        public void ConfigRepositoryConstructorBadArgsSecondariesNull()
        {
            ConstructorBadArgsHelper(_one, null, OverloadType.Secondaries);
        }

        private static void ConstructorBadArgsHelper(Dictionary<string, string> one, Dictionary<string, string> two = null, OverloadType include = OverloadType.All)
        {
            foreach (var o in GetOverloads(include))
            {
                AssertThrows<ArgumentNullException>(() => o(null, one, two));
            }
        }

        [TestMethod]
        public void ConfigRepositoryTryGetValueBadArgsIndexLessThanZero()
        {
            TryGetValueBadArgsHelper(-1);
        }

        [TestMethod]
        public void ConfigRepositoryTryGetValueBadArgsIndexGreaterThanNumberOfConfigurationObjects()
        {
            TryGetValueBadArgsHelper(1);
        }

        private static void TryGetValueBadArgsHelper(int index)
        {
            string s;
            foreach (var o in GetOverloads(exclude: OverloadType.Secondaries))
            {
                var repo = o(null, _one);
                AssertThrows<ArgumentOutOfRangeException>(() => repo.TryGetValue(index, _key1, out s));
            }
        }

        #endregion

        #region constructors

        [TestMethod]
        public void ConfigRepositoryConstructorOneConfigObject()
        {
            string s;
            foreach (var o in GetOverloads(exclude: OverloadType.Secondaries))
            {
                var repo = o(null, _one);

                Assert.IsTrue(repo.TryGetValue(_key1, out s));
                Assert.AreEqual(s, _value1);
                Assert.IsTrue(repo.TryGetValue(0, _key1, out s));
                Assert.AreEqual(s, _value1);
            }
        }

        [TestMethod]
        public void ConfigRepositoryConstructorMultipleConfigObjects()
        {
            string s;
            foreach (var o in GetOverloads(OverloadType.Secondaries))
            {
                var repo1 = o(null, _one, _two);

                Assert.IsTrue(repo1.TryGetValue(_key1, out s));
                Assert.AreEqual(s, _value1);
                Assert.IsTrue(repo1.TryGetValue(0, _key1, out s));
                Assert.AreEqual(s, _value1);
                Assert.IsTrue(repo1.TryGetValue(1, _key1, out s));
                Assert.AreEqual(s, _value2);

                Assert.IsTrue(repo1.TryGetValue(_key3, out s));
                Assert.AreEqual(s, _value3);
                Assert.IsFalse(repo1.TryGetValue(0, _key3, out s));
                Assert.IsTrue(repo1.TryGetValue(1, _key3, out s));
                Assert.AreEqual(s, _value3);

                var repo2 = o(null, _two, _one);

                Assert.IsTrue(repo2.TryGetValue(_key1, out s));
                Assert.AreEqual(s, _value2);
                Assert.IsTrue(repo2.TryGetValue(0, _key1, out s));
                Assert.AreEqual(s, _value2);
                Assert.IsTrue(repo2.TryGetValue(1, _key1, out s));
                Assert.AreEqual(s, _value1);

                Assert.IsTrue(repo2.TryGetValue(_key3, out s));
                Assert.AreEqual(s, _value3);
                Assert.IsTrue(repo2.TryGetValue(0, _key3, out s));
                Assert.AreEqual(s, _value3);
                Assert.IsFalse(repo2.TryGetValue(1, _key3, out s));
            }
        }

        [TestMethod]
        public void ConfigRepositoryConstructorParamsArrayIsNullObject()
        {
            Dictionary<string, string>[] dNullArr = null;
            NameValueCollection[] nNullArr = null;
            ConfigWrapper[] wNullArr = null;

            // should not throw exceptions
            new ConfigRepository(d(_one), dNullArr);
            new ConfigRepository(n(_one), nNullArr);
            new ConfigRepository(w(_one), wNullArr);
            new ConfigRepository(StringComparer.Ordinal, d(_one), dNullArr);
            new ConfigRepository(StringComparer.Ordinal, n(_one), nNullArr);
            new ConfigRepository(StringComparer.Ordinal, w(_one), wNullArr);
        }

        [TestMethod]
        public void ConfigRepositoryConstructorKeyNotPresent()
        {
            string s;

            var repo = new ConfigRepository(d(_one));
            Assert.IsFalse(repo.TryGetValue(_key3, out s));
            Assert.IsNull(s);
            Assert.IsFalse(repo.TryGetValue(0, _key3, out s));
            Assert.IsNull(s);
        }

        [TestMethod]
        public void ConfigRepositoryConstructorKeyRemoveNull()
        {
            RemoveKeyHelper(null);
        }

        [TestMethod]
        public void ConfigRepositoryConstructorKeyRemoveEmpty()
        {
            RemoveKeyHelper(string.Empty);
        }

        [TestMethod]
        public void ConfigRepositoryConstructorKeyRemoveWhiteSpace()
        {
            RemoveKeyHelper(" ");
        }

        private static void RemoveKeyHelper(string str)
        {
            var one = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(str, _value1) };
            var two = new Dictionary<string, string>();

            string s;
            var exclude = str == null ? OverloadType.Dictionary : OverloadType.None;
            foreach (var o in GetOverloads(exclude: exclude))
            {
                var repo = o(null, one, two);

                Assert.IsFalse(repo.TryGetValue(str, out s));
                Assert.IsNull(s);
                Assert.IsFalse(repo.TryGetValue(0, str, out s));
                Assert.IsNull(s);
            }
        }

        [TestMethod]
        public void ConfigRepositoryConstructorCaseSensitivity()
        {
            string s;
            foreach (var o in GetOverloads(OverloadType.Comparer))
            {
                var repo1 = o(null, _one, _two);

                Assert.IsTrue(repo1.TryGetValue(_key1, out s));
                Assert.AreEqual(s, _value1);
                Assert.IsTrue(repo1.TryGetValue(0, _key1, out s));
                Assert.AreEqual(s, _value1);

                Assert.IsTrue(repo1.TryGetValue(_key1.ToUpperInvariant(), out s));
                Assert.AreEqual(s, _value1);
                Assert.IsTrue(repo1.TryGetValue(0, _key1.ToUpperInvariant(), out s));
                Assert.AreEqual(s, _value1);

                var repo2 = o(StringComparer.Ordinal, _one, _two);

                Assert.IsTrue(repo1.TryGetValue(_key1, out s));
                Assert.AreEqual(s, _value1);
                Assert.IsTrue(repo1.TryGetValue(0, _key1, out s));
                Assert.AreEqual(s, _value1);

                Assert.IsFalse(repo2.TryGetValue(_key1.ToUpperInvariant(), out s));
                Assert.IsFalse(repo2.TryGetValue(0, _key1.ToUpperInvariant(), out s));
            }
        }

        [TestMethod]
        public void ConfigRepositoryConstructorCaseSensitivityDoesNotChangeWrapper()
        {
            var wrapper = w(_one);
            var repoOrdinalIgnore = new ConfigRepository(StringComparer.OrdinalIgnoreCase, wrapper);
            var repoOrdinal = new ConfigRepository(StringComparer.Ordinal, wrapper);

            string s;
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(repoOrdinalIgnore.TryGetValue(_key1, out s));
                Assert.IsTrue(repoOrdinalIgnore.TryGetValue(_key1.ToUpperInvariant(), out s));
                repoOrdinalIgnore.RefreshAll();

                Assert.IsTrue(repoOrdinal.TryGetValue(_key1, out s));
                Assert.IsFalse(repoOrdinal.TryGetValue(_key1.ToUpperInvariant(), out s));
                repoOrdinal.RefreshAll();
            }
        }

        #endregion

        #region parameter registration (refresh and override)

        [TestMethod]
        public void ConfigRepositoryRegisterRefreshAll()
        {
            // params should not reflect updated values in config until after calling RefreshAll()
            var dict = d(_one);
            ConfigRepository repo = new ConfigRepository(dict);
            ConfigParameter<string> foo1 = new ConfigParameter<string>(repo, _key1);
            ConfigParameter<string> foo2 = new ConfigParameter<string>(repo, _key2);

            dict[_key1] = _value3;
            dict[_key2] = _value3;
            Assert.AreEqual(foo1.Value, _value1);
            Assert.AreEqual(foo2.Value, _value2);

            repo.RefreshAll();
            Assert.AreEqual(foo1.Value, _value3);
            Assert.AreEqual(foo2.Value, _value3);
        }

        [TestMethod]
        public void ConfigRepositoryRegisterClearOverrideAll()
        {
            // overridden params should all revert to expected values after calling ClearOverrideAll()
            ConfigRepository repo = new ConfigRepository(d(_one));
            ConfigParameter<string> foo1 = new ConfigParameter<string>(repo, _key1);
            ConfigParameter<string> foo2 = new ConfigParameter<string>(repo, _key2);

            foo1.Override(_value3);
            foo2.Override(_value3);
            Assert.AreEqual(foo1.Value, _value3);
            Assert.AreEqual(foo2.Value, _value3);

            repo.ClearOverrideAll();
            Assert.AreEqual(foo1.Value, _value1);
            Assert.AreEqual(foo2.Value, _value2);
        }

        #endregion

        #region parameter deregistration (memory safety)

        [TestMethod]
        public void ConfigRepositoryDeregisterShouldNotKeepStrongReference()
        {
            // repos should not maintain strong references to their params
            var repo = new ConfigRepository(d(_one));

            WeakReference w = null;
            Action outOfScopeAction = () =>
            {
                // this ConfigParameter will go out of scope once the anonymous function exits
                var p = new ConfigParameter<string>(repo, _key1);
                w = new WeakReference(p, true);
                Assert.IsTrue(w.IsAlive);
            };
            outOfScopeAction();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            // custom finalizers are run on first GC pass, but their containing objects aren't collected until next pass, so we must collect again
            GC.Collect();

            Assert.IsFalse(w.IsAlive);
        }

        [TestMethod]
        public void ConfigRepositoryDeregisterShouldNotKeepDeadWeakReference()
        {
            // repos should not maintain weak references to their params after they are destroyed
            var repo = new ConfigRepository(d(_one));
            ConfigParameter<int> p;

            Func<int, long> loopGcCollectAndReturnRAM = (iterations) =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    p = new ConfigParameter<int>(repo, _key1);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                return GC.GetTotalMemory(true);
            };

            // iterate enough times to reach peak total memory usage under real GC conditions
            int peakMemoryIterations = 400000;
            long startRAM = loopGcCollectAndReturnRAM(peakMemoryIterations);

            // iterate many more times; ensure memory usage doesn't go beyond initial peak
            int postPeakIterations = peakMemoryIterations * 3;
            long endRAM = loopGcCollectAndReturnRAM(postPeakIterations);
            Assert.IsTrue(endRAM - startRAM < 1024, $"Memory usage started at {startRAM} and ended at {endRAM}, an increase of {endRAM - startRAM} bytes over {postPeakIterations} params");
        }

        #endregion

        #region thread safety

        [TestMethod]
        public void ConfigRepositoryThreadSafetyModifyWhileCreate()
        {
            // construction should fail if source key-value collection is being modified
            var d = new Dictionary<string, string>();
            ConfigRepository repo;

            Action loopAction = () =>
            {
                repo = new ConfigRepository(d);
            };
            Action backgroundAction = () =>
            {
                d.Add(_key1, _value1);
                d.Remove(_key1);
            };

            AssertThrows<Exception>(() => RunTwoTasks(10000, loopAction, backgroundAction), (e) => { return e is InvalidOperationException || e is ArgumentNullException; });
        }

        [TestMethod]
        public void ConfigRepositoryThreadSafetyRefreshWhileTryGetValue()
        {
            // RefreshAll should not break value read attempts
            var d = new Dictionary<string, string>();
            var repo = new ConfigRepository(d);

            Action loopAction = () =>
            {
                d.Add(_key1, _value1);
                repo.RefreshAll();
                d[_key1] = _value2;
                repo.RefreshAll();
                d.Remove(_key1);
                repo.RefreshAll();
            };
            Action backgroundAction = () =>
            {
                string str;
                repo.TryGetValue(_key1, out str);
                repo.TryGetValue(0, _key1, out str);
            };

            RunTwoTasks(25000, loopAction, backgroundAction);
        }

        [TestMethod]
        public void ConfigRepositoryThreadSafetyRegisterWhileRefresh()
        {
            DeregisterHelper((repo) => repo.RefreshAll());
        }

        [TestMethod]
        public void ConfigRepositoryThreadSafetyRegisterWhileClearOverride()
        {
            DeregisterHelper((repo) => repo.ClearOverrideAll());
        }

        private static void DeregisterHelper(Action<ConfigRepository> registeredAction)
        {
            // parameter creation and destruction should not break RefreshAll, ClearOverrideAll, or parameter iteration
            var repo = new ConfigRepository(d(_one));
            ConfigParameter<int> param1;
            ConfigParameter<int> param2;

            Action<int> loopAction = (i) =>
            {
                param1 = new ConfigParameter<int>(repo, _key1);
                param2 = new ConfigParameter<int>(repo, _key2);
            };

            RunTwoTasks(50000, loopAction, () => registeredAction(repo));
        }

        [TestMethod]
        public void ConfigRepositoryThreadSafetySimultaneousRefresh()
        {
            SimultaneousRegisteredHelper(10000, (repo) => repo.RefreshAll());
        }

        [TestMethod]
        public void ConfigRepositoryThreadSafetySimultaneousClearOverride()
        {
            SimultaneousRegisteredHelper(50000, (repo) => repo.ClearOverrideAll());
        }

        [SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals",
            Justification = "Locals exist to trigger real registered functions, but we only care than an exception is not thrown")]
        private static void SimultaneousRegisteredHelper(int iterations, Action<ConfigRepository> registeredAction)
        {
            // simultaneous RefreshAll, ClearOverrideAll, or parameter iteration calls should not break each other
            var repo = new ConfigRepository(d(_one), d(_two));
            var p1 = new ConfigParameter<string>(repo, _key1);
            var p2 = new ConfigParameter<string>(repo, _key2);
            var p3 = new ConfigParameter<string>(repo, _key3);

            RunTwoTasks(iterations, () => registeredAction(repo), () => registeredAction(repo));
        }

        #endregion

    }
}
