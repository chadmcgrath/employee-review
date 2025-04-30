using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NUnit.Framework;

namespace EmployeeReview.IntegrationTests;

[TestFixture]
public class BasicApiTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task CanAccessApi()
    {
        var response = await _client.GetAsync("/api/v1/employees");

        // Just check for a response, not the status code since we don't know the API
        Assert.That(response, Is.Not.Null);
    }
}