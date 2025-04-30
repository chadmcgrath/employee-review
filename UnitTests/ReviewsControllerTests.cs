using EmployeeReview.Api.Controllers.REST;
using EmployeeReview.Application.Services;
using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EmployeeReview.Tests.Api.Controllers.REST
{
    [TestFixture]
    public class ReviewsControllerTests
    {
        private Mock<IPerformanceReviewService> _mockReviewService;
        private Mock<IEmployeeService> _mockEmployeeService;
        private Mock<ILogger<ReviewsController>> _mockLogger;
        private ReviewsController _controller;

        [SetUp]
        public void Setup()
        {
            _mockReviewService = new Mock<IPerformanceReviewService>();
            _mockEmployeeService = new Mock<IEmployeeService>();
            _mockLogger = new Mock<ILogger<ReviewsController>>();
            _controller = new ReviewsController(_mockReviewService.Object, _mockEmployeeService.Object, _mockLogger.Object);
        }

        [Test]
        public async Task CreateReview_ReturnsCreatedAtAction_WhenValidInput()
        {
            // Arrange
            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = 1,
                ReviewerId = 2,
                ReviewDate = DateTime.Now.AddDays(-1),
                Score = 4.5,
                Comments = "Great performance"
            };

            var createdReview = TestEntityFactory.CreatePerformanceReviewDto(
                1, createReviewDto.EmployeeId, createReviewDto.ReviewerId,
                "John Doe", "Jane Smith", createReviewDto.ReviewDate,
                createReviewDto.Score, createReviewDto.Comments);

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId))
                .ReturnsAsync(true);

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId))
                .ReturnsAsync(true);

            _mockReviewService
                .Setup(service => service.CreateReviewAsync(createReviewDto))
                .ReturnsAsync(createdReview);

            // Act
            var result = await _controller.CreateReview(createReviewDto);

            // Assert
            Assert.IsInstanceOf<CreatedAtActionResult>(result);
            var createdAtActionResult = result as CreatedAtActionResult;
            Assert.IsNotNull(createdAtActionResult);

            Assert.AreEqual(nameof(ReviewsController.GetReview), createdAtActionResult.ActionName);
            Assert.AreEqual(createdReview.Id, createdAtActionResult.RouteValues["id"]);

            var returnedReview = createdAtActionResult.Value as PerformanceReviewDto;
            Assert.IsNotNull(returnedReview);
            Assert.AreEqual(createdReview.Id, returnedReview.Id);
            Assert.AreEqual(createdReview.EmployeeId, returnedReview.EmployeeId);
            Assert.AreEqual(createdReview.ReviewerId, returnedReview.ReviewerId);

            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId), Times.Once);
            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId), Times.Once);
            _mockReviewService.Verify(service => service.CreateReviewAsync(createReviewDto), Times.Once);
        }

        [Test]
        public async Task CreateReview_ReturnsNotFound_WhenEmployeeDoesNotExist()
        {
            // Arrange
            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = 999,
                ReviewerId = 2,
                ReviewDate = DateTime.Now,
                Score = 4.5,
                Comments = "Great performance"
            };

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CreateReview(createReviewDto);

            // Assert
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult);
            Assert.AreEqual($"Employee with ID {createReviewDto.EmployeeId} not found", notFoundResult.Value);

            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId), Times.Once);
            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId), Times.Never);
            _mockReviewService.Verify(service => service.CreateReviewAsync(It.IsAny<CreatePerformanceReviewDto>()), Times.Never);
        }

        [Test]
        public async Task CreateReview_ReturnsNotFound_WhenReviewerDoesNotExist()
        {
            // Arrange
            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = 1,
                ReviewerId = 999,
                ReviewDate = DateTime.Now,
                Score = 4.5,
                Comments = "Great performance"
            };

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId))
                .ReturnsAsync(true);

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CreateReview(createReviewDto);

            // Assert
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult);
            Assert.AreEqual($"Reviewer with ID {createReviewDto.ReviewerId} not found", notFoundResult.Value);

            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId), Times.Once);
            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId), Times.Once);
            _mockReviewService.Verify(service => service.CreateReviewAsync(It.IsAny<CreatePerformanceReviewDto>()), Times.Never);
        }

        [Test]
        public async Task CreateReview_ReturnsBadRequest_WhenReviewDateIsInvalid()
        {
            // Arrange
            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = 1,
                ReviewerId = 2,
                ReviewDate = DateTime.Now,
                Score = 4.5,
                Comments = "Great performance"
            };

            string errorMessage = "Review date cannot be before employee's joining date";

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId))
                .ReturnsAsync(true);

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId))
                .ReturnsAsync(true);

            _mockReviewService
                .Setup(service => service.CreateReviewAsync(createReviewDto))
                .ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            var result = await _controller.CreateReview(createReviewDto);

            // Assert
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
            Assert.AreEqual(errorMessage, badRequestResult.Value);

            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId), Times.Once);
            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId), Times.Once);
            _mockReviewService.Verify(service => service.CreateReviewAsync(createReviewDto), Times.Once);
        }

        [Test]
        public async Task CreateReview_ReturnsBadRequest_WhenKeyNotFoundExceptionIsThrown()
        {
            // Arrange
            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = 1,
                ReviewerId = 2,
                ReviewDate = DateTime.Now,
                Score = 4.5,
                Comments = "Great performance"
            };

            string errorMessage = "Entity not found";

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId))
                .ReturnsAsync(true);

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId))
                .ReturnsAsync(true);

            _mockReviewService
                .Setup(service => service.CreateReviewAsync(createReviewDto))
                .ThrowsAsync(new KeyNotFoundException(errorMessage));

            // Act
            var result = await _controller.CreateReview(createReviewDto);

            // Assert
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult);
            Assert.AreEqual(errorMessage, notFoundResult.Value);

            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.EmployeeId), Times.Once);
            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(createReviewDto.ReviewerId), Times.Once);
            _mockReviewService.Verify(service => service.CreateReviewAsync(createReviewDto), Times.Once);
        }

        [Test]
        public async Task GetReview_ReturnsOkResult_WhenReviewExists()
        {
            // Arrange
            int reviewId = 1;
            var review = TestEntityFactory.CreatePerformanceReviewDto(
                reviewId, 2, 3, "John Doe", "Jane Smith",
                DateTime.Now.AddDays(-1), 4.5, "Great performance");

            _mockReviewService
                .Setup(service => service.GetReviewByIdAsync(reviewId))
                .ReturnsAsync(review);

            // Act
            var result = await _controller.GetReview(reviewId);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var returnedReview = okResult.Value as PerformanceReviewDto;
            Assert.IsNotNull(returnedReview);
            Assert.AreEqual(review.Id, returnedReview.Id);
            Assert.AreEqual(review.EmployeeId, returnedReview.EmployeeId);
            Assert.AreEqual(review.ReviewerId, returnedReview.ReviewerId);

            _mockReviewService.Verify(service => service.GetReviewByIdAsync(reviewId), Times.Once);
        }

        [Test]
        public async Task GetReview_ReturnsNotFound_WhenReviewDoesNotExist()
        {
            // Arrange
            int reviewId = 999;

            _mockReviewService
                .Setup(service => service.GetReviewByIdAsync(reviewId))
                .ReturnsAsync((PerformanceReviewDto)null);

            // Act
            var result = await _controller.GetReview(reviewId);

            // Assert
            Assert.IsInstanceOf<NotFoundResult>(result);
            _mockReviewService.Verify(service => service.GetReviewByIdAsync(reviewId), Times.Once);
        }

        [Test]
        public async Task GetEmployeeReviews_ReturnsOkResult_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;
            var reviews = new List<PerformanceReviewDto>
            {
                TestEntityFactory.CreatePerformanceReviewDto(1, employeeId, 2, "John Doe", "Jane Smith", DateTime.Now.AddMonths(-1), 4.0),
                TestEntityFactory.CreatePerformanceReviewDto(2, employeeId, 3, "John Doe", "Bob Johnson", DateTime.Now.AddDays(-1), 4.5)
            };

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(employeeId))
                .ReturnsAsync(true);

            _mockReviewService
                .Setup(service => service.GetReviewsByEmployeeIdAsync(employeeId))
                .ReturnsAsync(reviews);

            // Act
            var result = await _controller.GetEmployeeReviews(employeeId);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var returnedReviews = okResult.Value as IEnumerable<PerformanceReviewDto>;
            Assert.IsNotNull(returnedReviews);
            Assert.AreEqual(reviews.Count, returnedReviews.Count());

            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(employeeId), Times.Once);
            _mockReviewService.Verify(service => service.GetReviewsByEmployeeIdAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task GetEmployeeReviews_ReturnsNotFound_WhenEmployeeDoesNotExist()
        {
            // Arrange
            int employeeId = 999;

            _mockEmployeeService
                .Setup(service => service.EmployeeExistsAsync(employeeId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.GetEmployeeReviews(employeeId);

            // Assert
            Assert.IsInstanceOf<NotFoundObjectResult>(result);
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult);
            Assert.AreEqual($"Employee with ID {employeeId} not found", notFoundResult.Value);

            _mockEmployeeService.Verify(service => service.EmployeeExistsAsync(employeeId), Times.Once);
            _mockReviewService.Verify(service => service.GetReviewsByEmployeeIdAsync(employeeId), Times.Never);
        }

        [Test]
        public async Task GetAnalytics_ReturnsOkResult()
        {
            // Arrange
            var analytics = TestEntityFactory.CreatePerformanceAnalyticsDto();

            _mockReviewService
                .Setup(service => service.GetPerformanceAnalyticsAsync())
                .ReturnsAsync(analytics);

            // Act
            var result = await _controller.GetAnalytics();

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var returnedAnalytics = okResult.Value as PerformanceAnalyticsDto;
            Assert.IsNotNull(returnedAnalytics);
            Assert.AreEqual(analytics.DepartmentPerformance.Count(), returnedAnalytics.DepartmentPerformance.Count());
            Assert.AreEqual(analytics.TopPerformers.Count(), returnedAnalytics.TopPerformers.Count());
            Assert.AreEqual(analytics.MonthlyTrend.Count(), returnedAnalytics.MonthlyTrend.Count());

            _mockReviewService.Verify(service => service.GetPerformanceAnalyticsAsync(), Times.Once);
        }
    }
}