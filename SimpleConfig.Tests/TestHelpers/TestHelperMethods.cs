using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace SimpleConfig.Tests.TestHelpers
{
    [ExcludeFromCodeCoverage]
    public static class TestHelperMethods
    {
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter",
            Justification = "This syntax is easier to use and *much* cleaner than passing in a Type and doing a runtime check for Exception inheritance")]
        public static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            AssertThrows<TException>(action, null);
        }

        public static void AssertThrows<TException>(Action action, Func<TException, bool> condition)
            where TException : Exception
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            if (condition == null)
            {
                condition = (e) => { return true; };
            }

            bool success = false;
            try
            {
                action();
                success = true;
            }
            catch (TException e) when (condition(e))
            { }
            if (success)
            {
                Assert.Fail("Action did not throw expected " + typeof(TException));
            }
        }

        public static int RunTwoTasks(int loopIterations, Action loopAction, Action backgroundAction)
        {
            return RunTwoTasksHelper(loopIterations, (i) => loopAction(), backgroundAction);
        }

        public static int RunTwoTasks(int loopIterations, Action<int> loopAction, Action backgroundAction)
        {
            return RunTwoTasksHelper(loopIterations, (i) => loopAction(i), backgroundAction);
        }

        // TODO: Modify to break on exception
        private static int RunTwoTasksHelper(int loopIterations, Action<int> loopAction, Action backgroundAction)
        {
            int backgroundCounter = 0;
            var loopTask = Task.Run(() =>
            {
                for (int i = 0; i < loopIterations; i++)
                {
                    loopAction(i);
                }
            });
            var backgroundTask = Task.Run(() =>
            {
                // wait until loopTask has either started or has already finished
                while (loopTask.Status != TaskStatus.Running && loopTask.Status != TaskStatus.RanToCompletion)
                { }
                while (loopTask.Status == TaskStatus.Running)
                {
                    backgroundAction();
                    backgroundCounter++;
                }
            });

            // TODO: async/await lets you catch/throw original exception?
            try
            {
                loopTask.Wait();
                backgroundTask.Wait();
                return backgroundCounter;
            }
            catch (AggregateException ae)
            {
                throw ae.InnerException;
            }
        }

    }
}
