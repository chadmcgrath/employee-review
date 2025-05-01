using EmployeeReview.Contracts.DTOs;
using EmployeeReview.IntegrationTests.Helpers;
using IntegrationTests;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace EmployeeReview.IntegrationTests.Controllers
{
    [TestFixture]
    public class PerformanceReviewsControllerIntegrationTests
    {
        private AuthenticatedTestWebApplicationFactory _factory;
        private HttpClient _client;
        private int _employeeId1;
        private int _employeeId2;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new AuthenticatedTestWebApplicationFactory();
            _client = _factory.CreateAuthenticatedClient();

            // Get or create employees for tests
            await SetupTestEmployees();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }
        private async Task SetupTestEmployees()
        {
            // Get existing employees
            var response = await _client.GetAsync("/api/v1/employees");
            var employees = await response.Content.ReadAsJsonAsync<PaginatedListDto<EmployeeDto>>();

            if (employees.Items.Count >= 2)
            {
                _employeeId1 = employees.Items[0].Id;
                _employeeId2 = employees.Items[1].Id;
            }
            else
            {
                // Create test employees if needed
                var employee1 = new CreateEmployeeDto
                {
                    Name = "Review Test Employee 1",
                    Email = "review1@example.com",
                    Department = "Testing"
                };

                var employee2 = new CreateEmployeeDto
                {
                    Name = "Review Test Employee 2",
                    Email = "review2@example.com",
                    Department = "HR"
                };

                var createResponse1 = await _client.PostAsJsonAsync("/api/v1/employees", employee1);
                var createdEmployee1 = await createResponse1.Content.ReadAsJsonAsync<EmployeeDto>();
                _employeeId1 = createdEmployee1.Id;

                var createResponse2 = await _client.PostAsJsonAsync("/api/v1/employees", employee2);
                var createdEmployee2 = await createResponse2.Content.ReadAsJsonAsync<EmployeeDto>();
                _employeeId2 = createdEmployee2.Id;
            }
        }

        [Test]
        public async Task GetPerformanceReviews_ReturnsSuccessAndReviewList()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/reviews");
            var responseContent = await response.Content.ReadAsStringAsync();
            var reviews = JsonConvert.DeserializeObject<IEnumerable<PerformanceReviewDto>>(responseContent);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reviews, Is.Not.Null);
        }

        [Test]
        public async Task CreatePerformanceReview_WithValidData_CreatesNewReview()
        {
            // Arrange
            var newReview = new CreatePerformanceReviewDto
            {
                EmployeeId = _employeeId1,
                ReviewerId = _employeeId2,
                ReviewDate = DateTime.Now.AddDays(-1),
                Score = 4.2,
                Comments = "Integration test review comment"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/reviews", newReview);
            var createdReview = await response.Content.ReadAsJsonAsync<PerformanceReviewDto>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(createdReview, Is.Not.Null);
            Assert.That(createdReview.EmployeeId, Is.EqualTo(newReview.EmployeeId));
            Assert.That(createdReview.ReviewerId, Is.EqualTo(newReview.ReviewerId));
            Assert.That(createdReview.Score, Is.EqualTo(newReview.Score));
            Assert.That(createdReview.Comments, Is.EqualTo(newReview.Comments));

            // Verify that the review was actually created
            var getResponse = await _client.GetAsync($"/api/v1/reviews/{createdReview.Id}");
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetPerformanceReview_WithValidId_ReturnsReview()
        {
            // Arrange - Create a review first
            var newReview = new CreatePerformanceReviewDto
            {
                EmployeeId = _employeeId1,
                ReviewerId = _employeeId2,
                ReviewDate = DateTime.Now.AddDays(-2),
                Score = 3.8,
                Comments = "Test review for GetPerformanceReview test"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/reviews", newReview);
            var createdReview = await createResponse.Content.ReadAsJsonAsync<PerformanceReviewDto>();

            // Act
            var response = await _client.GetAsync($"/api/v1/reviews/{createdReview.Id}");
            var retrievedReview = await response.Content.ReadAsJsonAsync<PerformanceReviewDto>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(retrievedReview, Is.Not.Null);
            Assert.That(retrievedReview.Id, Is.EqualTo(createdReview.Id));
            Assert.That(retrievedReview.EmployeeId, Is.EqualTo(newReview.EmployeeId));
            Assert.That(retrievedReview.ReviewerId, Is.EqualTo(newReview.ReviewerId));
            Assert.That(retrievedReview.Score, Is.EqualTo(newReview.Score));
            Assert.That(retrievedReview.Comments, Is.EqualTo(newReview.Comments));
        }

        [Test]
        public async Task GetPerformanceReview_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = 999; // Assuming this ID doesn't exist

            // Act
            var response = await _client.GetAsync($"/api/v1/reviews/{invalidId}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task UpdatePerformanceReview_WithValidData_UpdatesReview()
        {
            // Arrange - Create a review first
            var newReview = new CreatePerformanceReviewDto
            {
                EmployeeId = _employeeId1,
                ReviewerId = _employeeId2,
                ReviewDate = DateTime.Now.AddDays(-5),
                Score = 3.5,
                Comments = "Review to be updated"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/reviews", newReview);
            var createdReview = await createResponse.Content.ReadAsJsonAsync<PerformanceReviewDto>();

            // Prepare update data
            var updateData = new UpdatePerformanceReviewDto
            {
                Score = 4.7,
                Comments = "Updated review comment",
                ReviewDate = DateTime.Now,
            };

            // Act
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1/reviews/{createdReview.Id}", updateData);
            var updatedReview = await updateResponse.Content.ReadAsJsonAsync<PerformanceReviewDto>();

            // Assert
            Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(updatedReview, Is.Not.Null);
            Assert.That(updatedReview.Id, Is.EqualTo(createdReview.Id));
            Assert.That(updatedReview.Score, Is.EqualTo(updateData.Score));
            Assert.That(updatedReview.Comments, Is.EqualTo(updateData.Comments));

            // Verify the update was persisted
            var getResponse = await _client.GetAsync($"/api/v1/reviews/{createdReview.Id}");
            var retrievedReview = await getResponse.Content.ReadAsJsonAsync<PerformanceReviewDto>();
            Assert.That(retrievedReview.Score, Is.EqualTo(updateData.Score));
            Assert.That(retrievedReview.Comments, Is.EqualTo(updateData.Comments));
        }

        [Test]
        public async Task UpdatePerformanceReview_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = 999; // Assuming this ID doesn't exist
            var updateData = new UpdatePerformanceReviewDto
            {
                Score = 4.0,
                Comments = "Updated comments"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/v1/reviews/{invalidId}", updateData);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task DeletePerformanceReview_WithValidId_RemovesReview()
        {
            // Arrange - Create a review first
            var newReview = new CreatePerformanceReviewDto
            {
                EmployeeId = _employeeId1,
                ReviewerId = _employeeId2,
                ReviewDate = DateTime.Now.AddDays(-10),
                Score = 3.0,
                Comments = "Review to be deleted"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/reviews", newReview);
            var createdReview = await createResponse.Content.ReadAsJsonAsync<PerformanceReviewDto>();

            // Act
            var deleteResponse = await _client.DeleteAsync($"/api/v1/reviews/{createdReview.Id}");

            // Assert
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            // Verify the review was removed
            var getResponse = await _client.GetAsync($"/api/v1/reviews/{createdReview.Id}");
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task DeletePerformanceReview_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = 999; // Assuming this ID doesn't exist

            // Act
            var response = await _client.DeleteAsync($"/api/v1/reviews/{invalidId}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
       

        [Test]
        public async Task GetReviewsForEmployee_ReturnsPaginatedResults()
        {
            // Arrange
            int pageSize = 2;
            int pageNumber = 1;

            // Create several reviews to ensure pagination works
            for (int i = 0; i < 5; i++)
            {
                var newReview = new CreatePerformanceReviewDto
                {
                    EmployeeId = _employeeId1,
                    ReviewerId = _employeeId2,
                    ReviewDate = DateTime.Now.AddDays(-i * 60),
                    Score = 3.5 + (i * 0.1),
                    Comments = $"Pagination test review {i}"
                };

                await _client.PostAsJsonAsync("/api/v1/reviews", newReview);
            }

            // Act
            var response = await _client.GetAsync($"/api/v1/reviews?employeeId={_employeeId1}&pageSize={pageSize}&pageNumber={pageNumber}");
            var reviews = await response.Content.ReadAsJsonAsync<IEnumerable<PerformanceReviewDto >> ();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        }

        [Test]
        public async Task GetRecentPerformanceReviews_ReturnsCorrectResults()
        {
            // Arrange - Create reviews with different dates
            for (int i = 1; i <= 3; i++)
            {
                var newReview = new CreatePerformanceReviewDto
                {
                    EmployeeId = _employeeId1,
                    ReviewerId = _employeeId2,
                    // Create reviews at different dates: current, 6 months ago, 1 year ago
                    ReviewDate = DateTime.Now.AddMonths(-6 * (i - 1)),
                    Score = 4.0,
                    Comments = $"Date test review {i}"
                };

                await _client.PostAsJsonAsync("/api/v1/reviews", newReview);
            }

            // Act - Get reviews from the last 7 months
            var sevenMonthsAgo = DateTime.Now.AddMonths(-7).ToString("yyyy-MM-dd");
            var response = await _client.GetAsync($"/api/v1/reviews?fromDate={sevenMonthsAgo}");
            var reviews = await response.Content.ReadAsJsonAsync<IEnumerable<PerformanceReviewDto>>();

            // Assert - Should only get reviews from the last 7 months (current and 6 months ago)
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(reviews.Count, Is.GreaterThanOrEqualTo(2));

            // All returned reviews should have dates after 7 months ago
            var sevenMonthsAgoDate = DateTime.Now.AddMonths(-7);
            foreach (var review in reviews)
            {
                Assert.That(review.ReviewDate, Is.GreaterThanOrEqualTo(sevenMonthsAgoDate));
            }
        }

        [Test]
        public async Task AuthorizedEndpoint_WithDifferentRole_WorksAsExpected()
        {
            using var readerClient = _factory.CreateAuthenticatedClient("Reader");

            var response = await readerClient.GetAsync("/api/v1/reviews");

            // Assert - Readers can not view performance reviews
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
    }
}