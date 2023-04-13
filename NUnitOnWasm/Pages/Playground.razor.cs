using System.Reflection;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.JSInterop;
using NUnit.Common;
using NUnit.Framework.Interfaces;
using NUnitLite;
using NUnitOnWasm.TestRunner;
using NUnitOnWasm.Worker;
using SpawnDev.BlazorJS.WebWorkers;
using Stryker.Core.Primitives.Logging;

namespace NUnitOnWasm.Pages;

public partial class Playground
{
    [Inject]
    public WebWorkerService WebWorkerService { get; set; }
    
    [Inject] 
    public IJSRuntime JsRuntime { get; set; }

    [Inject]
    public HttpClient HttpClient { get; set; }
    
    [Inject] public ILoggerFactory LoggerFactory { get; set; }
    
    private StandaloneCodeEditor? Editor { get; set; }

    private readonly List<MetadataReference> _references = new();
    
    private WebWorker? _webWorker;
    
    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = "csharp",
            Value = PlaygroundConstants.SourceCodeExample,
            Minimap = new EditorMinimapOptions(){ Enabled = false, },
            
        };
    }

    public async Task Mutate()
    {
        _webWorker ??= await WebWorkerService.GetWebWorker();
        ApplicationLogging.LoggerFactory = LoggerFactory;

        var runner = new TestWorker(HttpClient);

         await runner
            .RunMutationTests(await Editor.GetValue());

         var mutatedCode = runner.MutatedTree.ToFullString();

         await Editor.SetValue(mutatedCode);
    }

    public async Task CompileAndRun()
    {
        var timedOut = false;
        var code = await Editor.GetValue();
        
        _webWorker ??= await WebWorkerService.GetWebWorker();
        
        Console.WriteLine($"Max worker count is {WebWorkerService.MaxWorkerCount}");
        
        var runner = _webWorker.GetService<ITestWorker>();

        try
        {
            var result = await runner
                .RunTests(code)
                .WaitAsync(PlaygroundConstants.TestSuiteMaxDuration);

            if (result.TestCount == 0)
            {
                await Alert("No tests were found");
                return;
            }

            if (result.FailedCount > 0)
            {
                await Alert($"{result.FailedCount} tests failed");
            }
            else
            {
                await Alert($"All {result.TestCount} tests passed, nice!");
            }
        }
        catch (TimeoutException e)
        {
            timedOut = true;
        }
        finally
        {
            _webWorker?.Dispose();
            _webWorker = null;
            

            if (timedOut)
            {
                await Alert($"Test suite exceeded timeout of {PlaygroundConstants.TestSuiteMaxDuration.TotalMilliseconds} ms");
            }
        }
    }

    private async Task<Assembly?> CompileToAssembly(string sourceCode)
    {
        await AddNetCoreDefaultReferences();
        
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false, optimizationLevel: OptimizationLevel.Release)
            .WithUsings(PlaygroundConstants.DefaultNamespaces);

        var tree = SyntaxFactory.ParseSyntaxTree(sourceCode.Trim());
        
        var isoDateTime = DateTime.Now.ToString("yyyyMMddTHHmmss");
        var compilation = CSharpCompilation.Create($"PlaygroundBuild-{isoDateTime}.dll")
            .WithOptions(compilationOptions)
            .WithReferences(_references)
            .AddSyntaxTrees(tree);
        
        await using var codeStream = new MemoryStream();
        
        var compilationResult = compilation.Emit(codeStream);
        
        if (!compilationResult.Success)
        {
            await OnCompilationError(compilationResult);
            return null;
        }
        
        return Assembly.Load(codeStream.ToArray());
    }

    private async Task OnCompilationError(EmitResult compilationResult)
    {
        var diagnostics = compilationResult.Diagnostics
            .Select(x => x.ToString())
            .ToList()
            .Prepend("Build failed.");

        var errorMessage = string.Join("\n", diagnostics);

        await Alert(errorMessage);
    }

    private async Task Alert(string message) => await JsRuntime.InvokeVoidAsync("alert", message);
    
    private async Task Print(string message) => await JsRuntime.InvokeVoidAsync("console.log", message);

    private async Task AddNetCoreDefaultReferences()
    {
        foreach (var lib in PlaygroundConstants.DefaultLibraries)
        {
            await using var referenceStream = await HttpClient.GetStreamAsync($"/_framework/{lib}");
            _references.Add(MetadataReference.CreateFromStream(referenceStream));
        }
    }
}