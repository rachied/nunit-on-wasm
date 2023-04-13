using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Common;
using NUnitOnWasm.TestRunner;
using Stryker.Core.Primitives.InjectedHelpers;
using Stryker.Core.Primitives.Logging;
using Stryker.Core.Primitives.Mutants;
using Stryker.Core.Primitives.Options;

namespace NUnitOnWasm.Worker;

public interface ITestWorker
{
    public Task<TestResultSummary> RunTests(string sourceCode);

    public Task RunMutationTests(string sourceCode);
}

public class TestWorker : ITestWorker
{
    private readonly HttpClient _httpClient;

    public CsharpMutantOrchestrator CsharpMutantOrchestrator { get; set; }
    public SyntaxNode MutatedTree { get; set; }

    public TestWorker(HttpClient httpClient)
    {
        _httpClient = httpClient;
        ApplicationLogging.LoggerFactory ??= NullLoggerFactory.Instance;
    }
    
    public async Task<TestResultSummary> RunTests(string sourceCode)
    {
        var assembly = await CompileToAssembly(sourceCode);
        
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
    
    public async Task<TestResultSummary> RunTests(Assembly assembly)
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

    public async Task RunMutationTests(string sourceCode)
    {
        var initialTestResults = await RunTests(sourceCode);

        if (initialTestResults.TestCount == 0 || initialTestResults.FailedCount > 0)
        {
            Console.WriteLine("Error: Initial test run failed");
            return;
        }

        Console.WriteLine("Initial test run succeeded!");

        (var mutatedAssembly, var mutants) = await CompileMutations(sourceCode);

        if (mutatedAssembly is null)
        {
            Console.WriteLine("Error: Mutated assembly is null");
            return;
        }

        foreach (var mutant in mutants.Where(x => x.Id >= 3))
        {
            Console.WriteLine($"PRE: The env var is currently set to {Environment.GetEnvironmentVariable("ActiveMutation")}");
            Console.WriteLine("Running the test suite with active mutation: " + mutant.Id);
            Environment.SetEnvironmentVariable("ActiveMutation", mutant.Id.ToString());
            Console.WriteLine($"POST: The env var is currently set to {Environment.GetEnvironmentVariable("ActiveMutation")}");
            
            var results = await RunTests(mutatedAssembly);

            if (results.TestCount == 0)
            {
                Console.WriteLine($"Error: No test cases were detected for mutant {mutant.Id}");
                continue;
            }

            var status = results.FailedCount > 0 ? "killed" : "survived";
            
            Console.WriteLine($"Finished test run! Mutation {mutant.Id} status: {status}");
        }
        
    }

    private async Task<(Assembly?, List<Mutant>)> CompileMutations(string sourceCode)
    {
        CsharpMutantOrchestrator = new CsharpMutantOrchestrator();

        var tree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        var root = await tree.GetRootAsync();

        
        Console.WriteLine("Original syntax tree:");
        Console.WriteLine(root.ToFullString());

        MutatedTree = CsharpMutantOrchestrator.Mutate(root);

        Console.WriteLine($"Mutated the syntax tree with {CsharpMutantOrchestrator.MutantCount} mutations:");
        
        Console.WriteLine(MutatedTree.ToFullString());
        

        var assembly = await CompileToAssembly(MutatedTree.ToFullString());

        return (assembly, CsharpMutantOrchestrator.Mutants.ToList());
    }

    private async Task<Assembly?> CompileToAssembly(string sourceCode)
    {
        var refs = await GetDefaultReferences();
        
        var sw = new Stopwatch();
        sw.Start();
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release)
            .WithUsings(PlaygroundConstants.DefaultNamespaces);

        var sourceCodeTree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        var unitTestTree = SyntaxFactory.ParseSyntaxTree(PlaygroundConstants.UnitTestClassExample.Trim());
        var injectionTrees = InjectionSyntaxTrees();

        var isoDateTime = DateTime.Now.ToString("yyyyMMddTHHmmss");
        var compilation = CSharpCompilation.Create($"PlaygroundBuild-{isoDateTime}.dll")
            .WithOptions(compilationOptions)
            .WithReferences(refs)
            .AddSyntaxTrees(sourceCodeTree, unitTestTree)
            .AddSyntaxTrees(injectionTrees);

        await using var codeStream = new MemoryStream();
        
        var compilationResult = compilation.Emit(codeStream);
        
        if (!compilationResult.Success)
        {
            return null;
        }
        
        sw.Stop();
        
        Console.WriteLine($"Compilation took {sw.ElapsedMilliseconds} ms");
        
        return Assembly.Load(codeStream.ToArray());
    }

    private static List<SyntaxTree> InjectionSyntaxTrees()
    {
        var trees = new List<SyntaxTree>();
        
        foreach (var (name, code) in CodeInjection.MutantHelpers)
        {
            var tree = CSharpSyntaxTree.ParseText(code, path: name, encoding: Encoding.UTF32);
            trees.Add(tree);
        }

        return trees;
    }
    
    private async Task<List<MetadataReference>> GetDefaultReferences()
    {
        var sw = new Stopwatch();
        sw.Start();
        
        var references = new List<MetadataReference>();
        
        foreach (var lib in PlaygroundConstants.DefaultLibraries)
        {
            await using var referenceStream = await _httpClient.GetStreamAsync($"/_framework/{lib}");
            references.Add(MetadataReference.CreateFromStream(referenceStream));
        }
        
        sw.Stop();
        
        Console.WriteLine($"Downloading assemblies took {sw.ElapsedMilliseconds} ms");

        return references;
    }
}