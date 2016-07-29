using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleConfig.Helpers;
using System;
using System.Diagnostics.CodeAnalysis;
using static SimpleConfig.Tests.TestHelpers.TestHelperMethods;

namespace SimpleConfig.Tests.Helpers
{
    [TestClass, ExcludeFromCodeCoverage]
    public class EnumerableTryParseTests
    {
        #region Environment.GetResourceString

        [TestMethod, ExpectedException(typeof(FormatException))]
        public void EnumHelpersGetResourceStringBadArgsTooFewArgs()
        {
            Assert.IsTrue(string.IsNullOrWhiteSpace(EnumerableTryParse.GetResourceString("Arg_EnumValueNotFound")));
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void EnumHelpersGetResourceStringBadArgsUnrecognizedArg()
        {
            Assert.IsTrue(string.IsNullOrWhiteSpace(EnumerableTryParse.GetResourceString("Arg_OhNoes")));
        }

        [TestMethod]
        public void EnumHelpersGetResourceString()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(EnumerableTryParse.GetResourceString("Arg_MustContainEnumInfo")));
            Assert.IsFalse(string.IsNullOrWhiteSpace(EnumerableTryParse.GetResourceString("Arg_MustBeType")));
            Assert.IsFalse(string.IsNullOrWhiteSpace(EnumerableTryParse.GetResourceString("Arg_MustBeEnum")));
            Assert.IsFalse(string.IsNullOrWhiteSpace(EnumerableTryParse.GetResourceString("Arg_EnumValueNotFound", "value")));
        }

        #endregion

        #region EnumNameAndValueCache

        enum TestEnum
        {
            ValueZero = 0,
            ValueOne = 1
        }

        [TestMethod]
        public void EnumHelpersCustomCache()
        {
            var cache = EnumerableTryParse.GetCachedNamesAndValues(typeof(TestEnum));

            Assert.IsTrue(cache.Names.Length == 2);
            Assert.IsTrue(cache.Names[0] == TestEnum.ValueZero.ToString());
            Assert.IsTrue(cache.Names[1] == TestEnum.ValueOne.ToString());

            Assert.IsTrue(cache.Values.Length == 2);
            Assert.IsTrue(cache.Values[0] == (int)TestEnum.ValueZero);
            Assert.IsTrue(cache.Values[1] == (int)TestEnum.ValueOne);
        }

        [TestMethod]
        public void EnumHelpersCustomCacheThreadSafety()
        {
            Action loopAction = () =>
            {
                EnumerableTryParse.GetCachedNamesAndValues(typeof(TestEnum));
            };
            Action backgroundAction = () =>
            {
                EnumerableTryParse.GetCachedNamesAndValues(typeof(TestEnum));
                EnumerableTryParse.Clear();
            };

            RunTwoTasks(300000, loopAction, backgroundAction);
        }

        #endregion

    }
}
