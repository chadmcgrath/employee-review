using NUnit.Framework;

[SetUpFixture]
public class TestSetup
{
    [OneTimeSetUp]
    public void GlobalSetup()
    {
        TestContext.Progress.WriteLine("Global Test Setup Running...");
        // Any initialization logic goes here
    }

    [OneTimeTearDown]
    public void GlobalCleanup()
    {
        TestContext.Progress.WriteLine("Global Test Cleanup Running...");
        // Any cleanup logic goes here
    }
}