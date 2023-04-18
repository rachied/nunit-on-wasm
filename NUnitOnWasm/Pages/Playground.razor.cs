using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.JSInterop;
using NUnitOnWasm.Worker;
using SpawnDev.BlazorJS.WebWorkers;
using Stryker.Core.Primitives.Mutants;
using XtermBlazor;
using static Crayon.Output;

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
    
    private Xterm? Terminal { get; set; }
    
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
        Terminal.Reset();
        _webWorker ??= await WebWorkerService.GetWebWorker();
        
        var runner = _webWorker.GetService<IMutationTester>();

        var compiler = new RoslynWorker(HttpClient);

        var (bytes, mutants) = await compiler.MutateAndCompile(await SourceCodeEditor.GetValue(), await TestCodeEditor.GetValue());

        if (bytes is null)
        {
            await PrintError("Error: mutated compilation failed");
            return;
        }

        foreach (var mutant in mutants)
        {
            try
            {
                var testResult = await runner.TestMutation(bytes, mutant.Id);

                foreach (var line in testResult.TextOutput)
                {
                    await Print(line);
                }
                
                await Print($"==================Finished testing Mutant #{mutant.Id}==================");
                
                var survived = testResult is { TestCount: > 0, FailedCount: 0 };
                
                mutant.ResultStatus = survived ? MutantStatus.Survived : MutantStatus.Killed;
            }
            catch (TimeoutException)
            {
                await PrintWarning($"==================Timed out Mutant #{mutant.Id}==================");
                mutant.ResultStatus = MutantStatus.Timeout;
            }
        }
        
        var mutationScore = ((double)mutants.Count(x => x.ResultStatus != MutantStatus.Survived) / mutants.Count) * 100;
        var messageTxt = $"Your mutation score is {mutationScore:N2}%";

        var msg = mutationScore switch
        {
            > 90 => Green(Bold(messageTxt)),
            > 70 => Yellow(Bold(messageTxt)),
            _ => Red(Bold(messageTxt))
        };
        
        await Print(msg);

        foreach (var mutant in mutants)
        {
            await Print(mutant.ResultStatus + " " + mutant.DisplayName);
        }
    }

    public async Task CompileAndRun()
    {
        Terminal.Reset();
        var timedOut = false;
        var code = await SourceCodeEditor.GetValue();
        var tests = await TestCodeEditor.GetValue();
        
        _webWorker ??= await WebWorkerService.GetWebWorker();
        
        var compiler = new RoslynWorker(HttpClient);
        var testWorker = _webWorker.GetService<IMutationTester>();

        try
        {
            var (bytes, errors) = await compiler.Compile(code, tests);

            if (errors.Any())
            {
                await PrintError("Compilation failed!");
                foreach (var error in errors)
                {
                    await PrintError(error);
                }
            }
            
            var testResult = await testWorker
                .RunTests(bytes!)
                .WaitAsync(PlaygroundConstants.TestSuiteMaxDuration);
            
            foreach (var line in testResult.TextOutput)
            {
                await Print(line);
            }

            if (testResult.TestCount == 0)
            {
                await PrintWarning("No tests were found");
                return;
            }
            
            if (testResult.FailedCount > 0)
            {
                await PrintError($"{testResult.FailedCount} tests failed.");
            }
            else
            {
                await PrintSuccess($"All {testResult.TestCount} tests passed.");
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
                await PrintError($"Test suite exceeded timeout of {PlaygroundConstants.TestSuiteMaxDuration.TotalMilliseconds} ms");
            }
        }
    }

    private async Task Print(string message)
    {
        await Terminal.WriteLine(message);
        await Terminal.ScrollToBottom();
    }

    private async Task PrintError(string message) => await Print(Red(Bold(message)));
    
    private async Task PrintWarning(string message) => await Print(Yellow(Bold(message)));
    
    private async Task PrintSuccess(string message) => await Print(Green(Bold(message)));
}