
using EmployeeReview.Domain.Entities;
using NUnit.Framework;
using System;
namespace EmployeeReview.UnitTests.Domain
{
    [TestFixture]
    public class EmployeeTests
    {
        [Test]
        public void Constructor_ValidData_CreatesEmployee()
        {
            // Arrange
            string name = "John Doe";
            string email = "john@example.com";
            string department = "IT";
            DateTime dateOfJoining = DateTime.Now.AddYears(-1);

            // Act
            var employee = new Employee(name, email, department, dateOfJoining);

            // Assert
            Assert.AreEqual(name, employee.Name);
            Assert.AreEqual(email, employee.Email);
            Assert.AreEqual(department, employee.Department);
            Assert.AreEqual(dateOfJoining, employee.DateOfJoining);
            Assert.IsTrue(employee.IsActive);
            Assert.IsNotNull(employee.Reviews);
            Assert.IsNotNull(employee.ReviewsAsReviewer);
        }

        [Test]
        public void Constructor_EmptyName_ThrowsArgumentException()
        {
            // Arrange
            string name = "";
            string email = "john@example.com";
            string department = "IT";
            DateTime dateOfJoining = DateTime.Now.AddYears(-1);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new Employee(name, email, department, dateOfJoining));

            Assert.AreEqual("Name cannot be empty (Parameter 'name')", ex.Message);
        }

        [Test]
        public void Constructor_EmptyEmail_ThrowsArgumentException()
        {
            // Arrange
            string name = "John Doe";
            string email = "";
            string department = "IT";
            DateTime dateOfJoining = DateTime.Now.AddYears(-1);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new Employee(name, email, department, dateOfJoining));

            Assert.AreEqual("Email cannot be empty (Parameter 'email')", ex.Message);
        }

        [Test]
        public void Constructor_EmptyDepartment_ThrowsArgumentException()
        {
            // Arrange
            string name = "John Doe";
            string email = "john@example.com";
            string department = "";
            DateTime dateOfJoining = DateTime.Now.AddYears(-1);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new Employee(name, email, department, dateOfJoining));

            Assert.AreEqual("Department cannot be empty (Parameter 'department')", ex.Message);
        }

        [Test]
        public void Constructor_FutureDateOfJoining_ThrowsArgumentException()
        {
            // Arrange
            string name = "John Doe";
            string email = "john@example.com";
            string department = "IT";
            DateTime dateOfJoining = DateTime.Now.AddYears(1);  // Future date

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new Employee(name, email, department, dateOfJoining));

            Assert.AreEqual("Date of joining cannot be in the future (Parameter 'dateOfJoining')", ex.Message);
        }
    }
}

