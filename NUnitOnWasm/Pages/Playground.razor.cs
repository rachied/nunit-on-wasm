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

namespace NUnitOnWasm.Pages;

public partial class Playground
{
    [Inject]
    public WebWorkerService WebWorkerService { get; set; }
    
    [Inject] 
    public IJSRuntime JsRuntime { get; set; }

    [Inject]
    public HttpClient HttpClient { get; set; }
    
    private StandaloneCodeEditor? Editor { get; set; }

    private readonly List<MetadataReference> _references = new();
    
    private WebWorker? _webWorker;
    
    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = "csharp",
            Value = PlaygroundConstants.UnitTestClassExample,
            Minimap = new EditorMinimapOptions(){ Enabled = false, },
            
        };
    }

    public async Task CompileAndRun()
    {
        var code = await Editor.GetValue();
        
        _webWorker ??= await WebWorkerService.GetWebWorker();
        
        var runner = _webWorker.GetService<ITestWorker>();

        try
        {
            var result = await runner
                .RunTests(code)
                .WaitAsync(TimeSpan.FromSeconds(5));

            if (result.TestCount == 0)
            {
                await Alert("An unexpected error occurred");
                return;
            }

            if (result.FailedCount > 0)
            {
                await Alert("One or more tests failed");
            }
            else
            {
                await Alert("All tests passed, nice!");
            }
        }
        catch (TimeoutException e)
        {
            await Alert("Test suite timed out");
        }
        finally
        {
            _webWorker?.Dispose();
            _webWorker = null;
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

    private async Task AddNetCoreDefaultReferences()
    {
        foreach (var lib in PlaygroundConstants.DefaultLibraries)
        {
            await using var referenceStream = await HttpClient.GetStreamAsync($"/_framework/{lib}");
            _references.Add(MetadataReference.CreateFromStream(referenceStream));
        }
    }
}