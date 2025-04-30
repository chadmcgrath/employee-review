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
using System.Threading.Tasks;

namespace EmployeeReview.Tests.Api.Controllers.REST
{
    [TestFixture]
    public class EmployeesControllerTests
    {
        private Mock<IEmployeeService> _mockEmployeeService;
        private Mock<ILogger<EmployeesController>> _mockLogger;
        private EmployeesController _controller;

        [SetUp]
        public void Setup()
        {
            _mockEmployeeService = new Mock<IEmployeeService>();
            _mockLogger = new Mock<ILogger<EmployeesController>>();
            _controller = new EmployeesController(_mockEmployeeService.Object, _mockLogger.Object);
        }

        [Test]
        public async Task GetEmployees_ReturnsOkResult_WithPaginatedEmployees()
        {
            // Arrange
            int pageNumber = 1;
            int pageSize = 10;

            var employeeDtos = new List<EmployeeDto>
            {
                TestEntityFactory.CreateEmployeeDto(1, "John Doe", "john.doe@example.com", "IT"),
                TestEntityFactory.CreateEmployeeDto(2, "Jane Smith", "jane.smith@example.com", "HR")
            };

            var paginatedEmployees = TestEntityFactory.CreatePaginatedListDto(employeeDtos, pageNumber, 2);

            _mockEmployeeService
                .Setup(service => service.GetEmployeesAsync(pageNumber, pageSize, null, null))
                .ReturnsAsync(paginatedEmployees);

            // Act
            var result = await _controller.GetEmployees(pageNumber, pageSize);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var returnedEmployees = okResult.Value as PaginatedListDto<EmployeeDto>;
            Assert.IsNotNull(returnedEmployees);
            Assert.AreEqual(paginatedEmployees.PageIndex, returnedEmployees.PageIndex);
            Assert.AreEqual(paginatedEmployees.TotalCount, returnedEmployees.TotalCount);
            Assert.AreEqual(paginatedEmployees.Items.Count, returnedEmployees.Items.Count);

            _mockEmployeeService.Verify(service => service.GetEmployeesAsync(pageNumber, pageSize, null, null), Times.Once);
        }

        [Test]
        public async Task GetEmployees_WithSearchTerm_ReturnsFilteredEmployees()
        {
            // Arrange
            int pageNumber = 1;
            int pageSize = 10;
            string searchTerm = "John";

            var employeeDtos = new List<EmployeeDto>
            {
                TestEntityFactory.CreateEmployeeDto(1, "John Doe", "john.doe@example.com", "IT")
            };

            var paginatedEmployees = TestEntityFactory.CreatePaginatedListDto(employeeDtos, pageNumber, 1);

            _mockEmployeeService
                .Setup(service => service.GetEmployeesAsync(pageNumber, pageSize, searchTerm, null))
                .ReturnsAsync(paginatedEmployees);

            // Act
            var result = await _controller.GetEmployees(pageNumber, pageSize, searchTerm);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var returnedEmployees = okResult.Value as PaginatedListDto<EmployeeDto>;
            Assert.IsNotNull(returnedEmployees);
            Assert.AreEqual(1, returnedEmployees.Items.Count);
            Assert.AreEqual("John Doe", returnedEmployees.Items[0].Name);

            _mockEmployeeService.Verify(service => service.GetEmployeesAsync(pageNumber, pageSize, searchTerm, null), Times.Once);
        }

        [Test]
        public async Task GetEmployee_ReturnsOkResult_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;
            var employee = TestEntityFactory.CreateEmployeeDto(
                employeeId, "John Doe", "john.doe@example.com", "IT");

            _mockEmployeeService
                .Setup(service => service.GetEmployeeByIdAsync(employeeId))
                .ReturnsAsync(employee);

            // Act
            var result = await _controller.GetEmployee(employeeId);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var returnedEmployee = okResult.Value as EmployeeDto;
            Assert.IsNotNull(returnedEmployee);
            Assert.AreEqual(employee.Id, returnedEmployee.Id);
            Assert.AreEqual(employee.Name, returnedEmployee.Name);

            _mockEmployeeService.Verify(service => service.GetEmployeeByIdAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task GetEmployee_ReturnsNotFound_WhenEmployeeDoesNotExist()
        {
            // Arrange
            int employeeId = 999;

            _mockEmployeeService
                .Setup(service => service.GetEmployeeByIdAsync(employeeId))
                .ReturnsAsync((EmployeeDto)null);

            // Act
            var result = await _controller.GetEmployee(employeeId);

            // Assert
            Assert.IsInstanceOf<NotFoundResult>(result);
            _mockEmployeeService.Verify(service => service.GetEmployeeByIdAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task CreateEmployee_ReturnsCreatedAtAction_WithCreatedEmployee()
        {
            // Arrange
            var createEmployeeDto = new CreateEmployeeDto
            {
                Name = "New Employee",
                Email = "new.employee@example.com",
                Department = "HR"
            };

            var createdEmployee = TestEntityFactory.CreateEmployeeDto(
                1, createEmployeeDto.Name, createEmployeeDto.Email, createEmployeeDto.Department);

            _mockEmployeeService
                .Setup(service => service.CreateEmployeeAsync(createEmployeeDto))
                .ReturnsAsync(createdEmployee);

            // Act
            var result = await _controller.CreateEmployee(createEmployeeDto);

            // Assert
            Assert.IsInstanceOf<CreatedAtActionResult>(result);
            var createdAtActionResult = result as CreatedAtActionResult;
            Assert.IsNotNull(createdAtActionResult);

            Assert.AreEqual(nameof(EmployeesController.GetEmployee), createdAtActionResult.ActionName);
            Assert.AreEqual(createdEmployee.Id, createdAtActionResult.RouteValues["id"]);

            var returnedEmployee = createdAtActionResult.Value as EmployeeDto;
            Assert.IsNotNull(returnedEmployee);
            Assert.AreEqual(createdEmployee.Id, returnedEmployee.Id);
            Assert.AreEqual(createdEmployee.Name, returnedEmployee.Name);

            _mockEmployeeService.Verify(service => service.CreateEmployeeAsync(createEmployeeDto), Times.Once);
        }

        [Test]
        public async Task UpdateEmployee_ReturnsOkResult_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;
            var updateEmployeeDto = new UpdateEmployeeDto
            {
                Name = "Updated Name",
                Email = "updated.email@example.com",
                Department = "Marketing"
            };

            var updatedEmployee = TestEntityFactory.CreateEmployeeDto(
                employeeId, updateEmployeeDto.Name, updateEmployeeDto.Email, updateEmployeeDto.Department);

            _mockEmployeeService
                .Setup(service => service.UpdateEmployeeAsync(employeeId, updateEmployeeDto))
                .ReturnsAsync(updatedEmployee);

            // Act
            var result = await _controller.UpdateEmployee(employeeId, updateEmployeeDto);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);

            var returnedEmployee = okResult.Value as EmployeeDto;
            Assert.IsNotNull(returnedEmployee);
            Assert.AreEqual(updatedEmployee.Id, returnedEmployee.Id);
            Assert.AreEqual(updatedEmployee.Name, returnedEmployee.Name);

            _mockEmployeeService.Verify(service => service.UpdateEmployeeAsync(employeeId, updateEmployeeDto), Times.Once);
        }

        [Test]
        public async Task UpdateEmployee_ReturnsNotFound_WhenEmployeeDoesNotExist()
        {
            // Arrange
            int employeeId = 999;
            var updateEmployeeDto = new UpdateEmployeeDto
            {
                Name = "Updated Name",
                Email = "updated.email@example.com",
                Department = "Marketing"
            };

            _mockEmployeeService
                .Setup(service => service.UpdateEmployeeAsync(employeeId, updateEmployeeDto))
                .ReturnsAsync((EmployeeDto)null);

            // Act
            var result = await _controller.UpdateEmployee(employeeId, updateEmployeeDto);

            // Assert
            Assert.IsInstanceOf<NotFoundResult>(result);
            _mockEmployeeService.Verify(service => service.UpdateEmployeeAsync(employeeId, updateEmployeeDto), Times.Once);
        }

        [Test]
        public async Task DeleteEmployee_ReturnsNoContent_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;

            _mockEmployeeService
                .Setup(service => service.DeleteEmployeeAsync(employeeId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteEmployee(employeeId);

            // Assert
            Assert.IsInstanceOf<NoContentResult>(result);
            _mockEmployeeService.Verify(service => service.DeleteEmployeeAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task DeleteEmployee_ReturnsNotFound_WhenEmployeeDoesNotExist()
        {
            // Arrange
            int employeeId = 999;

            _mockEmployeeService
                .Setup(service => service.DeleteEmployeeAsync(employeeId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteEmployee(employeeId);

            // Assert
            Assert.IsInstanceOf<NotFoundResult>(result);
            _mockEmployeeService.Verify(service => service.DeleteEmployeeAsync(employeeId), Times.Once);
        }
    }
}