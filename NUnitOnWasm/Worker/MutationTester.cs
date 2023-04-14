using System.Reflection;
using NUnit.Common;
using NUnitOnWasm.TestRunner;
using Stryker.Core.Primitives.Mutants;

namespace NUnitOnWasm.Worker;

public interface IMutationTester
{
    public Task<bool> TestMutation(byte[] assemblyBytes, int activeMutantId);
}

public class MutationTester : IMutationTester
{
    public async Task<bool> TestMutation(byte[] assemblyBytes, int activeMutantId)
    {
        Environment.SetEnvironmentVariable("ActiveMutation", activeMutantId.ToString());
        
        var assembly = Assembly.Load(assemblyBytes);

        var testResults = await RunTests(assembly);

        return testResults is { TestCount: > 0, FailedCount: 0 };
    }
    
    public static async Task<TestResultSummary> RunTests(Assembly assembly)
    { 
        var args = new[] { "--noresult", "--labels=ON" };	
        var writer = new ExtendedTextWrapper(Console.Out);
        var runner = new WasmRunner(assembly);
        
        runner.Execute(writer, TextReader.Null, args);
        
        return new TestResultSummary()
        {
            FailedCount = runner.Summary.FailedCount,
            TestCount = runner.Summary.TestCount,
        };
    }
}