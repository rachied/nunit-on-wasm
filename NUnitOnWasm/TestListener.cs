using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Microsoft.JSInterop;

namespace NUnitOnWasm;

public class TestListener : ITestListener
{
    public IJSRuntime JsRuntime { get; set; }

    public TestListener(IJSRuntime jsRuntime)
    {
        JsRuntime = jsRuntime;
    }

    private void WriteLine(string message) => JsRuntime.InvokeVoidAsync("console.log", message);

    private void WriteError(string message) => JsRuntime.InvokeVoidAsync("console.error", message);

    public void TestStarted(ITest test)
    {
        WriteLine("Test started for " + test.FullName);

    }

    public void TestFinished(ITestResult result)
    {
        if (result.FailCount > 0)
        {
            WriteError($"Tests for {result.FullName} failed with {result.FailCount} errors");
        }
        else
        {
            WriteLine($"Tests for {result.FullName} finished successfully");
        }
    }

    public void TestOutput(TestOutput output)
    {
        WriteLine("Output received: " + output.Text);
    }

    public void SendMessage(TestMessage message)
    {
        WriteLine("Message received: " + message.Message);
    }
}