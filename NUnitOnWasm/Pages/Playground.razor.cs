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
using Stryker.Core.Primitives.Mutants;

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
    
    private StandaloneCodeEditor? SourceCodeEditor { get; set; }
    
    private StandaloneCodeEditor? TestCodeEditor { get; set; }
    
    private readonly List<MetadataReference> _references = new();
    
    private WebWorker? _webWorker;

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = "csharp",
            Theme = "vs-dark",
            Value = editor.Id.StartsWith("test") 
                ? PlaygroundConstants.UnitTestClassExample 
                : PlaygroundConstants.SourceCodeExample,
            Minimap = new EditorMinimapOptions()
            {
                Enabled = false,
            },
            SmoothScrolling = true,
        };
    }

    public async Task Mutate()
    {
        _webWorker ??= await WebWorkerService.GetWebWorker();
        
        var runner = _webWorker.GetService<IMutationTester>();

        var compiler = new TestWorker(HttpClient);

        (var bytes, var mutants) = await compiler.MutateAndCompile(await SourceCodeEditor.GetValue(), await TestCodeEditor.GetValue());

        if (bytes is null)
        {
            Console.WriteLine("Error: byte array from compilation is null");
            return;
        }

        foreach (var mutant in mutants)
        {
            try
            {
                var survived = await runner.TestMutation(bytes, mutant.Id);

                mutant.ResultStatus = survived ? MutantStatus.Survived : MutantStatus.Killed;
            }
            catch (TimeoutException e)
            {
                mutant.ResultStatus = MutantStatus.Timeout;
            }
        }

        var textResponse = string.Empty;
        
        foreach (var mutant in mutants)
        {
            textResponse+= mutant.ResultStatus + " " + mutant.DisplayName + "\n";
        }
        
        var mutationScore = ((double)mutants.Count(x => x.ResultStatus != MutantStatus.Survived) / mutants.Count) * 100;

        textResponse += $"Your mutation score is {mutationScore:N2}%";

        await Alert(textResponse);
    }

    public async Task CompileAndRun()
    {
        var timedOut = false;
        var code = await SourceCodeEditor.GetValue();
        
        _webWorker ??= await WebWorkerService.GetWebWorker();
        
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