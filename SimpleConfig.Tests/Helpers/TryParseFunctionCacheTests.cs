using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleConfig.Helpers;
using SimpleConfig.Tests.TestHelpers;
using System;
using System.Diagnostics.CodeAnalysis;

namespace SimpleConfig.Tests.Helpers
{
    [TestClass, ExcludeFromCodeCoverage]
    public class TryParseFunctionCacheTests
    {
        #region parse types

        [TestMethod]
        public void TryParseFunctionCacheParseNumbers()
        {
            ParseHelper((byte)123);
            ParseHelper(123);
            ParseHelper((long)123.456);
            ParseHelper(123.456);
        }

        [TestMethod]
        public void TryParseFunctionCacheParseEnumFromName()
        {
            ParseHelper(TestEnumDefault.OptionOne);
        }

        [TestMethod]
        public void TryParseFunctionCacheParseEnumFromNumbers()
        {
            ParseHelper(TestEnumDefault.OptionOne, ((int)TestEnumDefault.OptionOne).ToString());
            ParseHelper(TestEnumInt.OptionOne, ((int)TestEnumInt.OptionOne).ToString());
            ParseHelper(TestEnumUInt.OptionOne, ((uint)TestEnumUInt.OptionOne).ToString());
            ParseHelper(TestEnumLong.OptionOne, ((long)TestEnumLong.OptionOne).ToString());
            ParseHelper(TestEnumULong.OptionOne, ((ulong)TestEnumULong.OptionOne).ToString());
        }

        [TestMethod]
        [SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Flags")]
        public void TryParseFunctionCacheParseEnumFromValueFlags()
        {
            ParseHelper(TestEnumFlags.OptionZero, ((ulong)TestEnumFlags.OptionZero).ToString());
            ParseHelper(TestEnumFlags.OptionOne | TestEnumFlags.OptionTwo,
                ((ulong)(TestEnumFlags.OptionOne | TestEnumFlags.OptionTwo)).ToString());
        }

        [TestMethod]
        public void TryParseFunctionCacheParseString()
        {
            ParseHelper("testString");
        }

        [TestMethod]
        public void TryParseFunctionCacheParseChar()
        {
            ParseHelper('t');
        }

        [TestMethod]
        public void TryParseFunctionCacheParseBool()
        {
            ParseHelper(true);
            ParseHelper(true, "TRUE");
        }

        [TestMethod]
        public void TryParseFunctionCacheParseGuid()
        {
            ParseHelper(Guid.NewGuid());
        }

        [TestMethod]
        public void TryParseFunctionCacheParseDateTime()
        {
            ParseHelper(new DateTime(1969, 07, 19));
        }

        [TestMethod]
        public void TryParseFunctionCacheParseTimeSpan()
        {
            ParseHelper(new TimeSpan(1, 48, 0));
        }

        [TestMethod]
        public void TryParseFunctionCacheParseCustomClass()
        {
            ParseHelper(new TryParseTestClass("a,b"));
        }

        [TestMethod]
        public void TryParseFunctionCacheParseClassWithoutTryParse()
        {
            Assert.IsNull(TryParseFunctionCache.GetTryParseFunction<Type>());
        }

        #endregion

        #region thread safety

        [TestMethod]
        public void TryParseFunctionCacheThreadSafetyMultipleAddSameType()
        {
            Action loopAction = () =>
            {
                TryParseFunctionCache.GetTryParseFunction<int>();
                TryParseFunctionCache.Clear();
            };
            Action backgroundAction = () =>
            {
                TryParseFunctionCache.GetTryParseFunction<int>();
            };

            TestHelperMethods.RunTwoTasks(10000, loopAction, backgroundAction);
        }

        #endregion


        #region helpers

        private static void ParseHelper<T>(T obj, string inputString = null)
        {
            if (inputString == null)
            {
                inputString = obj.ToString();
            }

            var tryParseFunction = TryParseFunctionCache.GetTryParseFunction<T>();
            Assert.IsNotNull(tryParseFunction);

            T output;
            Assert.IsTrue(tryParseFunction(inputString, out output));
            Assert.AreEqual(obj, output);
        }

        #endregion

    }
}
