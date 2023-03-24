using NUnit;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace NUnitOnWasm;

public class MyTestRunner : TestFilter, ITestListener
{
    public void Run()
    {
        Console.WriteLine("Starting tests");
        Console.WriteLine();
        var builder = new DefaultTestAssemblyBuilder();
        var runner = new NUnitTestAssemblyRunner(builder);
        runner.Load(typeof(MyTestRunner).Assembly, new Dictionary<string, object>()
        {
            //https://github.com/nunit/nunit/issues/2922
            [FrameworkPackageSettings.NumberOfTestWorkers] = 0,
            [FrameworkPackageSettings.SynchronousEvents] = true,
            [FrameworkPackageSettings.RunOnMainThread] = true
        });
        runner.Run(this, this);
        Console.WriteLine();
        Console.WriteLine("Test run finished");
    }
    #region ITestListener
    public void TestStarted(ITest test) => Console.WriteLine("Running " + test.FullName);
    public void TestFinished(ITestResult result)
    {
        var symbol = result.FailCount > 0 ? "✖" : (result.PassCount > 0 ? "✔" : "❓");
        Console.WriteLine($"\t{symbol}{result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
    }
    public void TestOutput(TestOutput output) => Console.WriteLine("\t🖶" + output.Text);

    public void SendMessage(TestMessage message) => Console.WriteLine("\t🛈" + message.Message);
    #endregion

    #region TestFilter 
    //See NUnit.Framework.Internal.TestFilter+EmptyFilter
    public override bool Match(ITest test) => true;
    public override TNode AddToXml(TNode parentNode, bool recursive) => parentNode.AddElement("filter");
    public override bool Pass(ITest test) => true;
    public override bool IsExplicitMatch(ITest test) => false;
    #endregion
}