using System.Reflection;
using NUnit.Common;
using NUnitOnWasm.TestRunner;
using Stryker.Core.Primitives.Mutants;

namespace NUnitOnWasm.Worker;

public interface IMutationTester
{
    public Task<TestResultSummary> RunTests(byte[] assemblyBytes);
    
    public Task<TestResultSummary> TestMutation(byte[] assemblyBytes, int activeMutantId);
}

public class MutationTester : IMutationTester
{
    public async Task<TestResultSummary> TestMutation(byte[] assemblyBytes, int activeMutantId)
    {
        Environment.SetEnvironmentVariable("ActiveMutation", activeMutantId.ToString());
        return await RunTests(assemblyBytes);
    }
    
    public async Task<TestResultSummary> RunTests(byte[] assemblyBytes)
    {
        var assembly = Assembly.Load(assemblyBytes);
        var sw = new StringWriter();
        
        // var writer = new ExtendedTextWrapper(Console.Out);
        var writer = new ExtendedTextWrapper(sw);

        var args = new[] { "--noresult", "--labels=ON" };	

        var runner = new WasmRunner(assembly);
        
        runner.Execute(writer, TextReader.Null, args);
        
        return new TestResultSummary()
        {
            FailedCount = runner.Summary.FailedCount,
            TestCount = runner.Summary.TestCount,
            TextOutput = sw.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
        };
    }
}