using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Common;
using NUnitOnWasm.TestRunner;

namespace NUnitOnWasm.Worker;

public interface ITestWorker
{
    public Task<TestResultSummary> RunTests(string sourceCode);
}

public class TestWorker : ITestWorker
{
    private readonly HttpClient _httpClient;

    public TestWorker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<TestResultSummary> RunTests(string sourceCode)
    {
        var sw = new Stopwatch();
        sw.Start();
        var assembly = await CompileToAssembly(sourceCode);
        
        var args = new[] { "--noresult", "--labels=ON" };	
        var writer = new ExtendedTextWrapper(Console.Out);
        var runner = new WasmRunner(assembly);
        
        runner.Execute(writer, TextReader.Null, args);
        
        sw.Stop();
        
        Console.WriteLine($"roundtrip took {sw.ElapsedMilliseconds} ms");

        return new TestResultSummary()
        {
            FailedCount = runner.Summary.FailedCount,
            TestCount = runner.Summary.TestCount,
        };
    }
    
    private async Task<Assembly?> CompileToAssembly(string sourceCode)
    {
        var refs = await GetDefaultReferences();
        
        var sw = new Stopwatch();
        sw.Start();
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release)
            .WithUsings(PlaygroundConstants.DefaultNamespaces);

        var tree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        
        var isoDateTime = DateTime.Now.ToString("yyyyMMddTHHmmss");
        var compilation = CSharpCompilation.Create($"PlaygroundBuild-{isoDateTime}.dll")
            .WithOptions(compilationOptions)
            .WithReferences(refs)
            .AddSyntaxTrees(tree);
        
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