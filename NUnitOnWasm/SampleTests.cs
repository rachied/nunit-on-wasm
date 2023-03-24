using NUnit.Framework;

namespace NUnitOnWasm;

[TestFixture]
public class SampleTests
{

    [Test]
    public void Hello()
    {
        Assert.Fail("Test message 12345");
    }
}