using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using static SimpleConfig.Tests.TestHelpers.TestHelperMethods;

namespace SimpleConfig.Tests
{
    [TestClass, ExcludeFromCodeCoverage]
    public class ConfigWrapperTests
    {
        #region test helpers

        private delegate void AddAction<TReal>(TReal source, string key, object value);

        private static AddAction<Dictionary<string, string>> _addToGenericDictionary = (x, k, v) => x.Add(k, v.ToString());
        private static AddAction<Hashtable> _addToNonGenericDictionary = (x, k, v) => x.Add(k, v);
        private static AddAction<NameValueCollection> _addToNameValueCollection = (x, k, v) => x.Add(k, v.ToString());

        #endregion


        #region bad args

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void ConfigWrapperConstructorBadArgsDictionaryConfigNull()
        {
            new DictionaryWrapper(null);
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void ConfigWrapperConstructorBadArgsNameValueCollectionConfigNull()
        {
            new NameValueCollectionWrapper(null);
        }

        #endregion

        #region constructors - deep copy and refresh

        [TestMethod]
        public void ConfigWrapperConstructorDeepCopyGenericDictionary()
        {
            DeepCopyHelper<IDictionary, Dictionary<string, string>, DictionaryWrapper>(_addToGenericDictionary);
        }

        [TestMethod]
        public void ConfigWrapperConstructorDeepCopyNonGenericDictionary()
        {
            DeepCopyHelper<IDictionary, Hashtable, DictionaryWrapper>(_addToNonGenericDictionary);
        }

        [TestMethod]
        public void ConfigWrapperConstructorDeepCopyNameValueCollection()
        {
            DeepCopyHelper<NameValueCollection, NameValueCollection, NameValueCollectionWrapper>(_addToNameValueCollection);
        }

        private static void DeepCopyHelper<T, TReal, TWrapper>(AddAction<TReal> addAction)
            where TReal : T, new()
            where TWrapper : ConfigWrapper<T>
        {
            // deep copy should be deep and should obey the given StringComparer
            var collection = new TReal();
            var wrapper = Activator.CreateInstance(typeof(TWrapper), collection) as TWrapper;

            var deepCopy = wrapper.MakeDeepCopy(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(deepCopy.Count, 0);

            var key = "key";
            int value = 123;

            addAction(collection, key, value);
            Assert.AreEqual(deepCopy.Count, 0);

            deepCopy = wrapper.MakeDeepCopy(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(deepCopy.Count, 1);
            Assert.AreEqual(deepCopy[key], value.ToString());
            Assert.AreEqual(deepCopy[key.ToUpperInvariant()], value.ToString());

            deepCopy = wrapper.MakeDeepCopy(StringComparer.Ordinal);
            Assert.AreEqual(deepCopy.Count, 1);
            Assert.AreEqual(deepCopy[key], value.ToString());
            Assert.IsFalse(deepCopy.ContainsKey(key.ToUpperInvariant()));
        }

        #endregion

        #region thread safety

        [TestMethod]
        public void ConfigWrapperThreadSafetySimultaneousRefreshGenericDictionary()
        {
            SimultaneousRefresh<IDictionary, Dictionary<string, string>, DictionaryWrapper>(2500, _addToGenericDictionary);
        }

        [TestMethod]
        public void ConfigWrapperThreadSafetySimultaneousRefreshNonGenericDictionary()
        {
            SimultaneousRefresh<IDictionary, Hashtable, DictionaryWrapper>(2000, _addToNonGenericDictionary);
        }

        [TestMethod]
        public void ConfigWrapperThreadSafetySimultaneousRefreshNameValueCollection()
        {
            SimultaneousRefresh<NameValueCollection, NameValueCollection, NameValueCollectionWrapper>(1000, _addToNameValueCollection);
        }

        private static void SimultaneousRefresh<T, TReal, TWrapper>(int loopIterations, AddAction<TReal> addAction)
            where TReal : T, new()
            where TWrapper : ConfigWrapper<T>
        {
            // simultaneous deep copy creations should not break each other
            var collection = new TReal();
            int collectionCount = 100;
            for (int i = 0; i < collectionCount; i++)
            {
                addAction(collection, "key_" + i, i);
            }
            var wrapper = Activator.CreateInstance(typeof(TWrapper), collection) as TWrapper;

            Action loopAction = () =>
            {
                var deepCopy = wrapper.MakeDeepCopy(StringComparer.Ordinal);
                Assert.AreEqual(deepCopy.Count, collectionCount);
            };

            RunTwoTasks(loopIterations, loopAction, loopAction);
        }


        [TestMethod]
        public void ConfigWrapperThreadSafetyRefreshAndModifyGenericDictionary()
        {
            RefreshAndModify<IDictionary, Dictionary<string, string>, DictionaryWrapper>(50000, _addToGenericDictionary, (d, k) => d.Remove(k));
        }

        [TestMethod]
        public void ConfigWrapperThreadSafetyRefreshAndModifyNonGenericDictionary()
        {
            RefreshAndModify<IDictionary, Hashtable, DictionaryWrapper>(20000, _addToNonGenericDictionary, (d, k) => d.Remove(k));
        }

        [TestMethod]
        public void ConfigWrapperThreadSafetyRefreshAndModifyNameValueCollection()
        {
            RefreshAndModify<NameValueCollection, NameValueCollection, NameValueCollectionWrapper>(2000, _addToNameValueCollection, (d, k) => d.Remove(k));
        }

        private static void RefreshAndModify<T, TReal, TWrapper>(int iterations, AddAction<TReal> addAction, Action<TReal, string> removeAction)
            where TReal : T, new()
            where TWrapper : ConfigWrapper<T>
        {
            // deep copy creation should fail if source key-value collection is being modified
            var collection = new TReal();
            var wrapper = Activator.CreateInstance(typeof(TWrapper), collection) as TWrapper;

            string key = "asdf";
            Action loopAction = () =>
            {
                addAction(collection, key, key);
                removeAction(collection, key);
            };
            Action backgroundAction = () =>
            {
                wrapper.MakeDeepCopy(StringComparer.Ordinal);
            };

            AssertThrows<Exception>(() => RunTwoTasks(iterations, loopAction, backgroundAction), (e) => { return e is InvalidOperationException || e is ArgumentNullException; });
        }

        #endregion

    }
}
