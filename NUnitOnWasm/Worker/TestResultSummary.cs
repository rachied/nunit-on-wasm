﻿namespace NUnitOnWasm.Worker;

public class TestResultSummary
{
    public int TestCount { get; set; }
    public int FailedCount { get; set; }

    public IEnumerable<string> TextOutput { get; set; }
}