using EmployeeReview.Domain.Entities;
using NUnit.Framework;
using System;

namespace EmployeeReview.UnitTests.Domain
{
    [TestFixture]
    public class PerformanceReviewTests
    {
        [Test]
        public void Constructor_ValidData_CreatesPerformanceReview()
        {
            // Arrange
            int employeeId = 1;
            int reviewerId = 2;
            DateTime reviewDate = DateTime.Now.AddDays(-10);
            double score = 4.5;
            string comments = "Great performance";

            // Act
            var review = new PerformanceReview(employeeId, reviewerId, reviewDate, score, comments);

            // Assert
            Assert.AreEqual(employeeId, review.EmployeeId);
            Assert.AreEqual(reviewerId, review.ReviewerId);
            Assert.AreEqual(reviewDate, review.ReviewDate);
            Assert.AreEqual(4.5, review.Score);  // Score should be rounded to 1 decimal place
            Assert.AreEqual(comments, review.Comments);
        }

        [Test]
        public void Constructor_ScoreTooLow_ThrowsArgumentException()
        {
            // Arrange
            int employeeId = 1;
            int reviewerId = 2;
            DateTime reviewDate = DateTime.Now.AddDays(-10);
            double score = 0.5;  // Below minimum of 1.0
            string comments = "Performance needs improvement";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new PerformanceReview(employeeId, reviewerId, reviewDate, score, comments));

            Assert.AreEqual("Score must be between a value from 1.0 to 5.0 (Parameter 'score')", ex.Message);
        }

        [Test]
        public void Constructor_ScoreTooHigh_ThrowsArgumentException()
        {
            // Arrange
            int employeeId = 1;
            int reviewerId = 2;
            DateTime reviewDate = DateTime.Now.AddDays(-10);
            double score = 5.5;  // Above maximum of 5.0
            string comments = "Exceptional performance";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new PerformanceReview(employeeId, reviewerId, reviewDate, score, comments));

            Assert.AreEqual("Score must be between a value from 1.0 to 5.0 (Parameter 'score')", ex.Message);
        }

        [Test]
        public void Constructor_FutureReviewDate_ThrowsArgumentException()
        {
            // Arrange
            int employeeId = 1;
            int reviewerId = 2;
            DateTime reviewDate = DateTime.Now.AddDays(10);  // Future date
            double score = 4.0;
            string comments = "Good performance";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new PerformanceReview(employeeId, reviewerId, reviewDate, score, comments));

            Assert.AreEqual("Review date cannot be in the future (Parameter 'reviewDate')", ex.Message);
        }

        [Test]
        public void Constructor_ScoreRoundedToOneDecimalPlace()
        {
            // Arrange
            int employeeId = 1;
            int reviewerId = 2;
            DateTime reviewDate = DateTime.Now.AddDays(-10);
            double score = 4.567;  // More than one decimal place
            string comments = "Good performance";

            // Act
            var review = new PerformanceReview(employeeId, reviewerId, reviewDate, score, comments);

            // Assert
            Assert.AreEqual(4.6, review.Score);  // Should be rounded to 4.6
        }

        [Test]
        public void Update_ValidData_UpdatesProperties()
        {
            // Arrange
            var review = new PerformanceReview(1, 2, DateTime.Now.AddDays(-20), 3.5, "Original comment");
            DateTime newReviewDate = DateTime.Now.AddDays(-10);
            double newScore = 4.2;
            string newComments = "Updated comment";

            // Act
            review.Update(newReviewDate, newScore, newComments);

            // Assert
            Assert.AreEqual(newReviewDate, review.ReviewDate);
            Assert.AreEqual(newScore, review.Score);
            Assert.AreEqual(newComments, review.Comments);
        }

        [Test]
        public void Update_ScoreTooLow_ThrowsArgumentException()
        {
            // Arrange
            var review = new PerformanceReview(1, 2, DateTime.Now.AddDays(-20), 3.5, "Original comment");
            DateTime newReviewDate = DateTime.Now.AddDays(-10);
            double newScore = 0.5;  // Below minimum of 1.0
            string newComments = "Updated comment";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                review.Update(newReviewDate, newScore, newComments));

            Assert.AreEqual("Score must be between a value from 1.0 to 5.0 (Parameter 'score')", ex.Message);
        }

        [Test]
        public void Update_FutureReviewDate_ThrowsArgumentException()
        {
            // Arrange
            var review = new PerformanceReview(1, 2, DateTime.Now.AddDays(-20), 3.5, "Original comment");
            DateTime newReviewDate = DateTime.Now.AddDays(10);  // Future date
            double newScore = 4.0;
            string newComments = "Updated comment";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                review.Update(newReviewDate, newScore, newComments));

            Assert.AreEqual("Review date cannot be in the future (Parameter 'reviewDate')", ex.Message);
        }
    }
}