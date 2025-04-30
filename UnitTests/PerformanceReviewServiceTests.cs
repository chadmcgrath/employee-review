using AutoMapper;
using EmployeeReview.Application.Services;
using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Domain.Entities;
using EmployeeReview.Infrastructure.Data;
using EmployeeReview.Infrastructure.Data.Repositories;
using EmployeeReview.Tests.Helpers;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EmployeeReview.Tests.Application.Services
{
    [TestFixture]
    public class PerformanceReviewServiceTests
    {
        private Mock<IUnitOfWork> _mockUnitOfWork;
        private Mock<IMapper> _mockMapper;
        private Mock<IPerformanceReviewRepository> _mockReviewRepository;
        private Mock<IEmployeeRepository> _mockEmployeeRepository;
        private PerformanceReviewService _reviewService;

        [SetUp]
        public void Setup()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _mockReviewRepository = new Mock<IPerformanceReviewRepository>();
            _mockEmployeeRepository = new Mock<IEmployeeRepository>();

            _mockUnitOfWork.Setup(uow => uow.PerformanceReviewRepository).Returns(_mockReviewRepository.Object);
            _mockUnitOfWork.Setup(uow => uow.EmployeeRepository).Returns(_mockEmployeeRepository.Object);

            _reviewService = new PerformanceReviewService(_mockUnitOfWork.Object, _mockMapper.Object);
        }

        [Test]
        public async Task GetReviewByIdAsync_ReturnsReviewDto_WhenReviewExists()
        {
            // Arrange
            var reviewId = 1;
            var expectedReviewDto = TestEntityFactory.CreatePerformanceReviewDto(
                reviewId, 2, 3, "John Doe", "Jane Smith", DateTime.Now.AddDays(-1), 4.5, "Excellent work");

            _mockReviewRepository
                .Setup(repo => repo.GetReviewDtoByIdAsync(reviewId))
                .ReturnsAsync(expectedReviewDto);

            // Act
            var result = await _reviewService.GetReviewByIdAsync(reviewId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedReviewDto.Id, result.Id);
            Assert.AreEqual(expectedReviewDto.EmployeeId, result.EmployeeId);
            Assert.AreEqual(expectedReviewDto.ReviewerId, result.ReviewerId);
            Assert.AreEqual(expectedReviewDto.Score, result.Score);
            _mockReviewRepository.Verify(repo => repo.GetReviewDtoByIdAsync(reviewId), Times.Once);
        }

        [Test]
        public async Task GetReviewByIdAsync_ReturnsNull_WhenReviewDoesNotExist()
        {
            // Arrange
            var reviewId = 999;

            _mockReviewRepository
                .Setup(repo => repo.GetReviewDtoByIdAsync(reviewId))
                .ReturnsAsync((PerformanceReviewDto)null);

            // Act
            var result = await _reviewService.GetReviewByIdAsync(reviewId);

            // Assert
            Assert.IsNull(result);
            _mockReviewRepository.Verify(repo => repo.GetReviewDtoByIdAsync(reviewId), Times.Once);
        }

        [Test]
        public async Task GetReviewsByEmployeeIdAsync_ReturnsReviewDtos_WhenEmployeeHasReviews()
        {
            // Arrange
            var employeeId = 1;
            var expectedReviewDtos = new List<PerformanceReviewDto>
            {
                TestEntityFactory.CreatePerformanceReviewDto(1, employeeId, 2, "John Doe", "Jane Smith", DateTime.Now.AddMonths(-1), 4.0),
                TestEntityFactory.CreatePerformanceReviewDto(2, employeeId, 3, "John Doe", "Bob Johnson", DateTime.Now.AddDays(-1), 4.5)
            };

            _mockReviewRepository
                .Setup(repo => repo.GetReviewDtosByEmployeeIdAsync(employeeId))
                .ReturnsAsync(expectedReviewDtos);

            // Act
            var result = await _reviewService.GetReviewsByEmployeeIdAsync(employeeId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            _mockReviewRepository.Verify(repo => repo.GetReviewDtosByEmployeeIdAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task GetReviewsByEmployeeIdAsync_ReturnsEmptyList_WhenEmployeeHasNoReviews()
        {
            // Arrange
            var employeeId = 1;
            var emptyList = new List<PerformanceReviewDto>();

            _mockReviewRepository
                .Setup(repo => repo.GetReviewDtosByEmployeeIdAsync(employeeId))
                .ReturnsAsync(emptyList);

            // Act
            var result = await _reviewService.GetReviewsByEmployeeIdAsync(employeeId);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Any());
            _mockReviewRepository.Verify(repo => repo.GetReviewDtosByEmployeeIdAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task CreateReviewAsync_ReturnsCreatedReviewDto_WhenValidInput()
        {
            // Arrange
            var employeeId = 1;
            var reviewerId = 2;

            // Use the factory to create employees with proper IDs
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT",
                DateTime.Now.AddYears(-2)); // Joined 2 years ago

            var reviewer = TestEntityFactory.CreateEmployee(
                reviewerId, "Jane Smith", "jane.smith@example.com", "HR",
                DateTime.Now.AddYears(-3)); // Joined 3 years ago

            var reviewDate = DateTime.Now.AddDays(-1); // Yesterday

            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = employeeId,
                ReviewerId = reviewerId,
                ReviewDate = reviewDate,
                Score = 4.5,
                Comments = "Great performance"
            };

            // Create a review with ID using the factory
            var review = TestEntityFactory.CreatePerformanceReview(
                1, employeeId, reviewerId, reviewDate, 4.5, "Great performance");

            var expectedReviewDto = TestEntityFactory.CreatePerformanceReviewDto(
                1, employeeId, reviewerId, "John Doe", "Jane Smith", reviewDate, 4.5, "Great performance");

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(reviewerId))
                .ReturnsAsync(reviewer);

            _mockReviewRepository
                .Setup(repo => repo.AddAsync(It.IsAny<PerformanceReview>()))
                .ReturnsAsync(review);

            _mockReviewRepository
                .Setup(repo => repo.GetReviewDtoByIdAsync(review.Id))
                .ReturnsAsync(expectedReviewDto);

            // Act
            var result = await _reviewService.CreateReviewAsync(createReviewDto);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedReviewDto.Id, result.Id);
            Assert.AreEqual(expectedReviewDto.EmployeeId, result.EmployeeId);
            Assert.AreEqual(expectedReviewDto.ReviewerId, result.ReviewerId);
            Assert.AreEqual(expectedReviewDto.Score, result.Score);
            Assert.AreEqual(expectedReviewDto.Comments, result.Comments);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.AddAsync(It.IsAny<PerformanceReview>()), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Once);
            _mockReviewRepository.Verify(repo => repo.GetReviewDtoByIdAsync(review.Id), Times.Once);
        }

        [Test]
        public void CreateReviewAsync_ThrowsKeyNotFoundException_WhenEmployeeDoesNotExist()
        {
            // Arrange
            var employeeId = 999;
            var reviewerId = 2;

            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = employeeId,
                ReviewerId = reviewerId,
                ReviewDate = DateTime.Now,
                Score = 4.5,
                Comments = "Great performance"
            };

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync((Employee)null);

            // Act & Assert
            Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await _reviewService.CreateReviewAsync(createReviewDto));

            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Never);
            _mockReviewRepository.Verify(repo => repo.AddAsync(It.IsAny<PerformanceReview>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public void CreateReviewAsync_ThrowsKeyNotFoundException_WhenReviewerDoesNotExist()
        {
            // Arrange
            var employeeId = 1;
            var reviewerId = 999;

            // Use the factory to create employee with proper ID
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT");

            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = employeeId,
                ReviewerId = reviewerId,
                ReviewDate = DateTime.Now,
                Score = 4.5,
                Comments = "Great performance"
            };

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(reviewerId))
                .ReturnsAsync((Employee)null);

            // Act & Assert
            Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await _reviewService.CreateReviewAsync(createReviewDto));

            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.AddAsync(It.IsAny<PerformanceReview>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public void CreateReviewAsync_ThrowsArgumentException_WhenReviewDateBeforeEmployeeJoining()
        {
            // Arrange
            var employeeId = 1;
            var reviewerId = 2;

            // Use the factory to create employee with a recent join date
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT",
                DateTime.Now.AddMonths(-1)); // Employee joined 1 month ago

            var reviewer = TestEntityFactory.CreateEmployee(
                reviewerId, "Jane Smith", "jane.smith@example.com", "HR",
                DateTime.Now.AddYears(-3)); // Reviewer joined 3 years ago

            // Review date is before employee's joining date
            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = employeeId,
                ReviewerId = reviewerId,
                ReviewDate = DateTime.Now.AddMonths(-2), // 2 months ago (before employee joined)
                Score = 4.5,
                Comments = "Great performance"
            };

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(reviewerId))
                .ReturnsAsync(reviewer);

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _reviewService.CreateReviewAsync(createReviewDto));

            Assert.That(ex.Message, Does.Contain("Review date cannot be before employee's joining date"));

            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.AddAsync(It.IsAny<PerformanceReview>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public void CreateReviewAsync_ThrowsArgumentException_WhenReviewDateBeforeReviewerJoining()
        {
            // Arrange
            var employeeId = 1;
            var reviewerId = 2;

            // Employee joined 3 years ago, but reviewer joined only 1 month ago
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT",
                DateTime.Now.AddYears(-3));

            var reviewer = TestEntityFactory.CreateEmployee(
                reviewerId, "Jane Smith", "jane.smith@example.com", "HR",
                DateTime.Now.AddMonths(-1)); // Reviewer joined 1 month ago

            // Review date is before reviewer's joining date
            var createReviewDto = new CreatePerformanceReviewDto
            {
                EmployeeId = employeeId,
                ReviewerId = reviewerId,
                ReviewDate = DateTime.Now.AddMonths(-2), // 2 months ago (before reviewer joined)
                Score = 4.5,
                Comments = "Great performance"
            };

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(reviewerId))
                .ReturnsAsync(reviewer);

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _reviewService.CreateReviewAsync(createReviewDto));

            Assert.That(ex.Message, Does.Contain("Review date cannot be before reviewer's joining date"));

            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.AddAsync(It.IsAny<PerformanceReview>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public async Task UpdateReviewAsync_ReturnsUpdatedReviewDto_WhenReviewExists()
        {
            // Arrange
            var reviewId = 1;
            var employeeId = 2;
            var reviewerId = 3;

            // Use factory to create entities with proper IDs
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT",
                DateTime.Now.AddYears(-2)); // Joined 2 years ago

            var reviewer = TestEntityFactory.CreateEmployee(
                reviewerId, "Jane Smith", "jane.smith@example.com", "HR",
                DateTime.Now.AddYears(-3)); // Joined 3 years ago

            var updateReviewDto = new UpdatePerformanceReviewDto
            {
                ReviewDate = DateTime.Now.AddDays(-1), // Yesterday
                Score = 4.8,
                Comments = "Updated comments"
            };

            var existingReview = TestEntityFactory.CreatePerformanceReview(
                reviewId, employeeId, reviewerId, DateTime.Now.AddMonths(-1), 4.0, "Original comments");

            var updatedReviewDto = TestEntityFactory.CreatePerformanceReviewDto(
                reviewId, employeeId, reviewerId, "John Doe", "Jane Smith",
                updateReviewDto.ReviewDate, updateReviewDto.Score, updateReviewDto.Comments);

            _mockReviewRepository
                .Setup(repo => repo.GetByIdAsync(reviewId))
                .ReturnsAsync(existingReview);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(reviewerId))
                .ReturnsAsync(reviewer);

            _mockReviewRepository
                .Setup(repo => repo.GetReviewDtoByIdAsync(reviewId))
                .ReturnsAsync(updatedReviewDto);

            // Act
            var result = await _reviewService.UpdateReviewAsync(reviewId, updateReviewDto);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(updatedReviewDto.Id, result.Id);
            Assert.AreEqual(updatedReviewDto.EmployeeId, result.EmployeeId);
            Assert.AreEqual(updatedReviewDto.ReviewerId, result.ReviewerId);
            Assert.AreEqual(updatedReviewDto.Score, result.Score);
            Assert.AreEqual(updatedReviewDto.Comments, result.Comments);
            _mockReviewRepository.Verify(repo => repo.GetByIdAsync(reviewId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.UpdateAsync(existingReview), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Once);
            _mockReviewRepository.Verify(repo => repo.GetReviewDtoByIdAsync(reviewId), Times.Once);
        }

        [Test]
        public async Task UpdateReviewAsync_ReturnsNull_WhenReviewDoesNotExist()
        {
            // Arrange
            var reviewId = 999;
            var updateReviewDto = new UpdatePerformanceReviewDto
            {
                ReviewDate = DateTime.Now,
                Score = 4.8,
                Comments = "Updated comments"
            };

            _mockReviewRepository
                .Setup(repo => repo.GetByIdAsync(reviewId))
                .ReturnsAsync((PerformanceReview)null);

            // Act
            var result = await _reviewService.UpdateReviewAsync(reviewId, updateReviewDto);

            // Assert
            Assert.IsNull(result);
            _mockReviewRepository.Verify(repo => repo.GetByIdAsync(reviewId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PerformanceReview>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public void UpdateReviewAsync_ThrowsArgumentException_WhenReviewDateBeforeEmployeeJoining()
        {
            // Arrange
            var reviewId = 1;
            var employeeId = 2;
            var reviewerId = 3;

            // Use factory to create employee with a recent join date
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT",
                DateTime.Now.AddMonths(-1)); // Employee joined 1 month ago

            var reviewer = TestEntityFactory.CreateEmployee(
                reviewerId, "Jane Smith", "jane.smith@example.com", "HR",
                DateTime.Now.AddMonths(-6)); // Reviewer joined 6 months ago

            // Review date before employee joining date
            var updateReviewDto = new UpdatePerformanceReviewDto
            {
                ReviewDate = DateTime.Now.AddMonths(-2), // 2 months ago (before employee joined)
                Score = 4.8,
                Comments = "Updated comments"
            };

            var existingReview = TestEntityFactory.CreatePerformanceReview(
                reviewId, employeeId, reviewerId, DateTime.Now, 4.0, "Original comments");

            _mockReviewRepository
                .Setup(repo => repo.GetByIdAsync(reviewId))
                .ReturnsAsync(existingReview);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(reviewerId))
                .ReturnsAsync(reviewer);

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _reviewService.UpdateReviewAsync(reviewId, updateReviewDto));

            Assert.That(ex.Message, Does.Contain("Review date cannot be before employee's joining date"));

            _mockReviewRepository.Verify(repo => repo.GetByIdAsync(reviewId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PerformanceReview>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public void UpdateReviewAsync_ThrowsArgumentException_WhenReviewDateBeforeReviewerJoining()
        {
            // Arrange
            var reviewId = 1;
            var employeeId = 2;
            var reviewerId = 3;

            // Employee joined 3 years ago, but reviewer joined only 1 month ago
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT",
                DateTime.Now.AddYears(-3));

            var reviewer = TestEntityFactory.CreateEmployee(
                reviewerId, "Jane Smith", "jane.smith@example.com", "HR",
                DateTime.Now.AddMonths(-1)); // Reviewer joined 1 month ago

            // Review date before reviewer joining date
            var updateReviewDto = new UpdatePerformanceReviewDto
            {
                ReviewDate = DateTime.Now.AddMonths(-2), // 2 months ago (before reviewer joined)
                Score = 4.8,
                Comments = "Updated comments"
            };

            var existingReview = TestEntityFactory.CreatePerformanceReview(
                reviewId, employeeId, reviewerId, DateTime.Now, 4.0, "Original comments");

            _mockReviewRepository
                .Setup(repo => repo.GetByIdAsync(reviewId))
                .ReturnsAsync(existingReview);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(reviewerId))
                .ReturnsAsync(reviewer);

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _reviewService.UpdateReviewAsync(reviewId, updateReviewDto));

            Assert.That(ex.Message, Does.Contain("Review date cannot be before reviewer's joining date"));

            _mockReviewRepository.Verify(repo => repo.GetByIdAsync(reviewId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(reviewerId), Times.Once);
            _mockReviewRepository.Verify(repo => repo.UpdateAsync(It.IsAny<PerformanceReview>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public async Task DeleteReviewAsync_ReturnsTrue_WhenReviewExists()
        {
            // Arrange
            var reviewId = 1;

            _mockReviewRepository
                .Setup(repo => repo.DeleteAsync(reviewId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _reviewService.DeleteReviewAsync(reviewId);

            // Assert
            Assert.IsTrue(result);
            _mockReviewRepository.Verify(repo => repo.DeleteAsync(reviewId), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Once);
        }

        [Test]
        public async Task DeleteReviewAsync_ReturnsFalse_WhenReviewDoesNotExist()
        {
            // Arrange
            var reviewId = 999;

            _mockReviewRepository
                .Setup(repo => repo.DeleteAsync(reviewId))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _reviewService.DeleteReviewAsync(reviewId);

            // Assert
            Assert.IsFalse(result);
            _mockReviewRepository.Verify(repo => repo.DeleteAsync(reviewId), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public async Task GetPerformanceAnalyticsAsync_ReturnsAnalyticsDto()
        {
            // Arrange
            var departments = new List<string> { "IT", "HR", "Finance" };

            var departmentPerformance = new List<DepartmentPerformanceDto>
            {
                new DepartmentPerformanceDto { Department = "IT", AverageScore = 4.5 },
                new DepartmentPerformanceDto { Department = "HR", AverageScore = 4.2 },
                new DepartmentPerformanceDto { Department = "Finance", AverageScore = 3.9 }
            };

            var topPerformers = new List<TopPerformerDto>
            {
                new TopPerformerDto { EmployeeId = 1, Name = "John Doe", Department = "IT", AverageScore = 4.9 },
                new TopPerformerDto { EmployeeId = 2, Name = "Jane Smith", Department = "HR", AverageScore = 4.7 }
            };

            var monthlyTrend = new List<MonthlyPerformanceTrendDto>
            {
                new MonthlyPerformanceTrendDto { Month = "2023-01", AverageScore = 4.0, Year = 2023, MonthNumber = 1 },
                new MonthlyPerformanceTrendDto { Month = "2023-02", AverageScore = 4.2, Year = 2023, MonthNumber = 2 }
            };

            var expectedAnalytics = new PerformanceAnalyticsDto
            {
                DepartmentPerformance = departmentPerformance,
                TopPerformers = topPerformers,
                MonthlyTrend = monthlyTrend
            };

            _mockEmployeeRepository
                .Setup(repo => repo.GetAllDepartmentsAsync())
                .ReturnsAsync(departments);

            _mockReviewRepository
                .Setup(repo => repo.GetAverageScoresByDepartmentAsync(departments))
                .ReturnsAsync(departmentPerformance);

            _mockReviewRepository
                .Setup(repo => repo.GetTopPerformingEmployeesAsync(5))
                .ReturnsAsync(topPerformers);

            _mockReviewRepository
                .Setup(repo => repo.GetMonthlyPerformanceTrendAsync())
                .ReturnsAsync(monthlyTrend);

            // Act
            var result = await _reviewService.GetPerformanceAnalyticsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(departmentPerformance, result.DepartmentPerformance);
            Assert.AreEqual(topPerformers, result.TopPerformers);
            Assert.AreEqual(monthlyTrend, result.MonthlyTrend);
            _mockEmployeeRepository.Verify(repo => repo.GetAllDepartmentsAsync(), Times.Once);
            _mockReviewRepository.Verify(repo => repo.GetAverageScoresByDepartmentAsync(departments), Times.Once);
            _mockReviewRepository.Verify(repo => repo.GetTopPerformingEmployeesAsync(5), Times.Once);
            _mockReviewRepository.Verify(repo => repo.GetMonthlyPerformanceTrendAsync(), Times.Once);
        }
    }
}