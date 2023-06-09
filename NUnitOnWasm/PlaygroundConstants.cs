﻿namespace NUnitOnWasm;

public class PlaygroundConstants
{
    public static TimeSpan TestSuiteMaxDuration = TimeSpan.FromSeconds(5);
    
    public static string UnitTestClassExample = @"using System;
using NUnit.Framework;

namespace PlaygroundExecution 
{
    [TestFixture]
    public class UnitTests
    {
        [Test]
        public void PassingTest()
        {
            Assert.That(1 == 1);
        }

        [TestCase(40,40)]
        public void Parameterized_Test(int a, int b)
        {
            Assert.That(a == b);
        }
    }
}";

    public static string[] DefaultNamespaces =
    {
        "System", 
        "System.Text", 
        "System.Collections.Generic", 
        "System.IO", 
        "System.Linq",
        "System.Console",
        "System.Threading", 
        "System.Threading.Tasks"
    };
    
    public static string[] DefaultLibraries =
    {
        // System dependencies
        "System.dll",
        "System.Console.dll",
        "System.Buffers.dll",
        "System.Collections.dll",
        "System.Core.dll",
        "System.Runtime.dll",
        "System.IO.dll",
        "System.Linq.dll",
        "System.Linq.Expressions.dll",
        "System.Linq.Parallel.dll",
        "System.Private.CoreLib.dll",
        "mscorlib.dll",
        "netstandard.dll",
        
        // 3rd party dependencies
        "NUnit.Framework.dll",
    };

}