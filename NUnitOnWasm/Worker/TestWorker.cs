using System.Collections.Immutable;
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

    public Task<(byte[]?, List<Mutant>)> MutateAndCompile(string sourceCode, string testCode);

    public Task<byte[]?> Compile(string sourceCode, string testCode);
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

    public Task<TestResultSummary> RunTests(string sourceCode)
    {
        throw new NotImplementedException();
    }

    public async Task<(byte[]?, List<Mutant>)> MutateAndCompile(string sourceCode, string testCode)
    {
        CsharpMutantOrchestrator = new CsharpMutantOrchestrator();

        var sourceCodeTree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        var sourceCodeRoot = await sourceCodeTree.GetRootAsync();

        Console.WriteLine("Original syntax tree:");
        Console.WriteLine(sourceCodeRoot.ToFullString());

        MutatedTree = CsharpMutantOrchestrator.Mutate(sourceCodeRoot);

        Console.WriteLine($"Mutated the syntax tree with {CsharpMutantOrchestrator.MutantCount} mutations:");
        
        Console.WriteLine(MutatedTree.ToFullString());
        

        var bytes = await Compile(MutatedTree.ToFullString(), testCode);

        return (bytes, CsharpMutantOrchestrator.Mutants.ToList());
    }

    public async Task<byte[]?> Compile(string sourceCode, string testCode)
    {
        var refs = await GetDefaultReferences();
        
        var sw = new Stopwatch();
        sw.Start();
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release)
            .WithUsings(PlaygroundConstants.DefaultNamespaces);

        var sourceCodeTree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        var unitTestTree = SyntaxFactory.ParseSyntaxTree(testCode);
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

        return codeStream.ToArray();
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