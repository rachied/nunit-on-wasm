namespace Stryker.Core.Primitives.Mutants
{
    public enum MutantStatus
    {
        NotRun,
        Killed,
        Survived,
        Timeout,
        CompileError,
        Ignored,
        NoCoverage
    }
}
