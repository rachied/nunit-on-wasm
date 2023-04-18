using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Stryker.Core.Primitives.InjectedHelpers;
using Stryker.Core.Primitives.Logging;
using Stryker.Core.Primitives.Mutants;

namespace NUnitOnWasm.Worker;

public interface IRoslynWorker
{
    public Task<(byte[]?, List<Mutant>)> MutateAndCompile(string sourceCode, string testCode);

    public Task<(byte[]?, List<string>)> Compile(string sourceCode, string testCode);
}

public class RoslynWorker : IRoslynWorker
{
    private readonly HttpClient _httpClient;

    public RoslynWorker(HttpClient httpClient)
    {
        _httpClient = httpClient;
        ApplicationLogging.LoggerFactory = NullLoggerFactory.Instance;
    }

    public Task<TestResultSummary> RunTests(string sourceCode, string testCode)
    {
        throw new NotImplementedException();
    }

    public async Task<(byte[]?, List<Mutant>)> MutateAndCompile(string sourceCode, string testCode)
    {
        var orchestrator = new CsharpMutantOrchestrator();

        var sourceCodeTree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        var sourceCodeRoot = await sourceCodeTree.GetRootAsync();

        Console.WriteLine("Original syntax tree:");
        Console.WriteLine(sourceCodeRoot.ToFullString());

        var mutatedTree = orchestrator.Mutate(sourceCodeRoot);

        Console.WriteLine($"Mutated the syntax tree with {orchestrator.MutantCount} mutations:");
        
        Console.WriteLine(mutatedTree.ToFullString());
        

        var (bytes, errors) = await Compile(mutatedTree.ToFullString(), testCode);

        return (bytes, orchestrator.Mutants.ToList());
    }

    public async Task<(byte[]?, List<string>)> Compile(string sourceCode, string testCode)
    {
        var refs = await LoadDefaultReferences();
        
        var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary, 
                concurrentBuild: false, // WASM does not support concurrent builds
                optimizationLevel: OptimizationLevel.Release)
            .WithUsings(PlaygroundConstants.DefaultNamespaces);

        var sourceCodeTree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        var unitTestTree = SyntaxFactory.ParseSyntaxTree(testCode);
        var injectionTrees = GetInstrumentationSyntaxTrees();

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
            var errors = compilationResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage()).ToList();

            return (null, errors);
        }

        return (codeStream.ToArray(), new List<string>());
    }

    private static List<SyntaxTree> GetInstrumentationSyntaxTrees()
    {
        var trees = new List<SyntaxTree>();
        
        foreach (var (name, code) in CodeInjection.MutantHelpers)
        {
            var tree = CSharpSyntaxTree.ParseText(code, path: name, encoding: Encoding.UTF32);
            trees.Add(tree);
        }

        return trees;
    }
    
    private async Task<List<MetadataReference>> LoadDefaultReferences()
    {
        var references = new List<MetadataReference>();
        
        foreach (var lib in PlaygroundConstants.DefaultLibraries)
        {
            await using var referenceStream = await _httpClient.GetStreamAsync($"/_framework/{lib}");
            references.Add(MetadataReference.CreateFromStream(referenceStream));
        }
        
        return references;
    }
}