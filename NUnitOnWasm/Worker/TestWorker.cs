﻿using System.Diagnostics;
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

    public async Task RunMutationTests(string sourceCode)
    {
        var mutatedAssembly = await GetCompilingMutantAssemblies(sourceCode);
    }

    private async Task<Assembly> GetCompilingMutantAssemblies(string sourceCode)
    {
        try
        {
            var orchestrator = new CsharpMutantOrchestrator();

            var tree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
            var root = await tree.GetRootAsync();

        
            Console.WriteLine("Original syntax tree:");
            Console.WriteLine(root.ToFullString());

            var mutatedTree = orchestrator.Mutate(root);
        
            Console.WriteLine($"Mutated the syntax tree with {orchestrator.MutantCount} mutations:");
        
            Console.WriteLine(mutatedTree.ToFullString());


            var assembly = await CompileToAssembly(mutatedTree.ToFullString());

            return assembly ?? throw new Exception("Failed to compile the mutated syntax tree");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task<Assembly?> CompileToAssembly(string sourceCode)
    {
        var refs = await GetDefaultReferences();
        
        var sw = new Stopwatch();
        sw.Start();
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release)
            .WithUsings(PlaygroundConstants.DefaultNamespaces);

        var sourceCodeTree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        var injectionTrees = InjectionSyntaxTrees();

        foreach (var t in injectionTrees)
        {
            Console.WriteLine("Injecting helper class:");
            Console.WriteLine((await t.GetRootAsync()).ToFullString());
        }

        var isoDateTime = DateTime.Now.ToString("yyyyMMddTHHmmss");
        var compilation = CSharpCompilation.Create($"PlaygroundBuild-{isoDateTime}.dll")
            .WithOptions(compilationOptions)
            .WithReferences(refs)
            .AddSyntaxTrees(sourceCodeTree)
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