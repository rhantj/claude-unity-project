using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BlackHoleSim.Tests
{
    /// <summary>MCP hook: run all EditMode tests and log a parseable result line.</summary>
    public static class EditModeTestRunner
    {
        static TestRunnerApi _api;

        public static void RunAll()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new Callbacks());
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log($"[TESTRESULT] passed={result.PassCount} failed={result.FailCount} " +
                          $"skipped={result.SkipCount} status={result.TestStatus}");
            }
        }
    }
}
