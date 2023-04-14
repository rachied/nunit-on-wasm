namespace TestProject1;

public class Tests
{
    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable("ActiveMutation", "5");
    }
    
    [Test]
    public void RoboBar_OffersBeer_When_CustomerIs18Plus()
    {
        var bar = new RoboBar();
        var response = bar.GetGreeting(19);

        Assert.That(response, Is.EqualTo("Here have a beer!"));
    }

    [Test]
    public void RoboBar_DeniesService_When_CustomerIsUnderage()
    {
        var bar = new RoboBar();
        var response = bar.GetGreeting(17);

        Assert.That(response, Is.EqualTo("Sorry not today!"));
    }
}