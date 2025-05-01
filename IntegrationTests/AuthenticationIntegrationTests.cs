using EmployeeReview.Infrastructure.Security;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace EmployeeReview.IntegrationTests.Controllers
{
    [TestFixture]
    public class AuthenticationTests
    {
        private AuthenticatedTestWebApplicationFactory _factory;
        private HttpClient _client;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new AuthenticatedTestWebApplicationFactory();

            // Start with an unauthenticated client
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task ValidJwtAuthentication_ShouldBeAuthorized()
        {
            // Arrange - Get an authenticated client with JWT
            using var jwtClient = _factory.CreateAuthenticatedClient(UserRoles.Admin);

            // Act - Try to access a protected resource
            var response = await jwtClient.GetAsync("/api/v1/employees");

            // Assert - Should be authorized
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Failed with status code: {response.StatusCode}");

            // Additional verification - try another endpoint
            var reviewsResponse = await jwtClient.GetAsync("/api/v1/reviews");
            Assert.That(reviewsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Reviews endpoint failed with status code: {reviewsResponse.StatusCode}");
        }

        
        [Test]
        public async Task InvalidJwtAuthentication_ShouldBeUnauthorized()
        {
            // Arrange - Create client with invalid token
            var invalidClient = _factory.CreateClient();
            invalidClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "invalid.token.here");

            // Act - Try to access a protected resource
            var response = await invalidClient.GetAsync("/api/v1/employees");

            // Assert - Should be unauthorized
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

    }
}