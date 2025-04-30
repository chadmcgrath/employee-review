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
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EmployeeReview.Tests.Application.Services
{
    [TestFixture]
    public class EmployeeServiceTests
    {
        private Mock<IUnitOfWork> _mockUnitOfWork;
        private Mock<IMapper> _mockMapper;
        private Mock<IEmployeeRepository> _mockEmployeeRepository;
        private EmployeeService _employeeService;

        [SetUp]
        public void Setup()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _mockEmployeeRepository = new Mock<IEmployeeRepository>();

            _mockUnitOfWork.Setup(uow => uow.EmployeeRepository).Returns(_mockEmployeeRepository.Object);

            _employeeService = new EmployeeService(_mockUnitOfWork.Object, _mockMapper.Object);
        }

        [Test]
        public async Task GetEmployeeByIdAsync_ReturnsEmployeeDto_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;
            var expectedEmployeeDto = TestEntityFactory.CreateEmployeeDto(
                employeeId, "John Doe", "john.doe@example.com", "IT");

            _mockEmployeeRepository
                .Setup(repo => repo.GetEmployeeDtoByIdAsync(employeeId))
                .ReturnsAsync(expectedEmployeeDto);

            // Act
            var result = await _employeeService.GetEmployeeByIdAsync(employeeId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedEmployeeDto.Id, result.Id);
            Assert.AreEqual(expectedEmployeeDto.Name, result.Name);
            _mockEmployeeRepository.Verify(repo => repo.GetEmployeeDtoByIdAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task GetEmployeesAsync_WithoutFilters_ReturnsPaginatedEmployeeDtos()
        {
            // Arrange
            int pageNumber = 1;
            int pageSize = 10;
            var employeeDtos = new List<EmployeeDto>
            {
                TestEntityFactory.CreateEmployeeDto(1, "John Doe", "john.doe@example.com", "IT"),
                TestEntityFactory.CreateEmployeeDto(2, "Jane Smith", "jane.smith@example.com", "HR")
            };
            int totalCount = 2;

            var paginatedList = TestEntityFactory.CreatePaginatedListDto(employeeDtos, pageNumber, totalCount);

            _mockEmployeeRepository
                .Setup(repo => repo.GetEmployeeDtosWithPaginationAsync(pageNumber, pageSize))
                .ReturnsAsync(employeeDtos);

            _mockEmployeeRepository
                .Setup(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()))
                .ReturnsAsync(totalCount);

            // Act
            var result = await _employeeService.GetEmployeesAsync(pageNumber, pageSize);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(pageNumber, result.PageIndex);
            Assert.AreEqual(totalCount, result.TotalCount);
            Assert.AreEqual(1, result.TotalPages); // 2 items with page size 10 = 1 page
            Assert.AreEqual(employeeDtos.Count, result.Items.Count);
            _mockEmployeeRepository.Verify(repo => repo.GetEmployeeDtosWithPaginationAsync(pageNumber, pageSize), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()), Times.Once);
        }

        [Test]
        public async Task GetEmployeesAsync_WithSearchTerm_ReturnsPaginatedFilteredEmployeeDtos()
        {
            // Arrange
            int pageNumber = 1;
            int pageSize = 10;
            string searchTerm = "John";
            
            var employeeDtos = new List<EmployeeDto>
            {
                TestEntityFactory.CreateEmployeeDto(1, "John Doe", "john.doe@example.com", "IT")
            };
            
            int totalCount = 1;

            _mockEmployeeRepository
                .Setup(repo => repo.SearchEmployeeDtosByNameOrEmailAsync(searchTerm))
                .ReturnsAsync(employeeDtos);

            _mockEmployeeRepository
                .Setup(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()))
                .ReturnsAsync(totalCount);

            // Act
            var result = await _employeeService.GetEmployeesAsync(pageNumber, pageSize, searchTerm);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(pageNumber, result.PageIndex);
            Assert.AreEqual(totalCount, result.TotalCount);
            Assert.AreEqual(1, result.TotalPages);
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("John Doe", result.Items[0].Name);
            _mockEmployeeRepository.Verify(repo => repo.SearchEmployeeDtosByNameOrEmailAsync(searchTerm), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()), Times.Once);
        }

        [Test]
        public async Task GetEmployeesAsync_WithDepartment_ReturnsPaginatedFilteredEmployeeDtos()
        {
            // Arrange
            int pageNumber = 1;
            int pageSize = 10;
            string department = "Engineering";
            
            var employeeDtos = new List<EmployeeDto>
            {
                TestEntityFactory.CreateEmployeeDto(1, "John Doe", "john.doe@example.com", "Engineering"),
                TestEntityFactory.CreateEmployeeDto(3, "Bob Johnson", "bob.johnson@example.com", "Engineering")
            };
            
            int totalCount = 2;

            _mockEmployeeRepository
                .Setup(repo => repo.GetEmployeeDtosByDepartmentAsync(department))
                .ReturnsAsync(employeeDtos);

            _mockEmployeeRepository
                .Setup(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()))
                .ReturnsAsync(totalCount);

            // Act
            var result = await _employeeService.GetEmployeesAsync(pageNumber, pageSize, null, department);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(pageNumber, result.PageIndex);
            Assert.AreEqual(totalCount, result.TotalCount);
            Assert.AreEqual(1, result.TotalPages);
            Assert.AreEqual(2, result.Items.Count);
            Assert.IsTrue(result.Items.All(item => item.Department == "Engineering"));
            _mockEmployeeRepository.Verify(repo => repo.GetEmployeeDtosByDepartmentAsync(department), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()), Times.Once);
        }

        [Test]
        public async Task CreateEmployeeAsync_ReturnsCreatedEmployeeDto()
        {
            // Arrange
            var createEmployeeDto = new CreateEmployeeDto
            {
                Name = "New Employee",
                Email = "new.employee@example.com",
                Department = "HR"
            };

            // Use the factory to create an employee with ID
            var employee = TestEntityFactory.CreateEmployee(
                1, "New Employee", "new.employee@example.com", "HR");

            var expectedEmployeeDto = TestEntityFactory.CreateEmployeeDto(
                1, "New Employee", "new.employee@example.com", "HR");

            _mockMapper
                .Setup(mapper => mapper.Map<Employee>(createEmployeeDto))
                .Returns(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.AddAsync(employee))
                .ReturnsAsync(employee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetEmployeeDtoByIdAsync(employee.Id))
                .ReturnsAsync(expectedEmployeeDto);

            // Act
            var result = await _employeeService.CreateEmployeeAsync(createEmployeeDto);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedEmployeeDto.Id, result.Id);
            Assert.AreEqual(expectedEmployeeDto.Name, result.Name);
            Assert.AreEqual(expectedEmployeeDto.Email, result.Email);
            Assert.AreEqual(expectedEmployeeDto.Department, result.Department);
            _mockMapper.Verify(mapper => mapper.Map<Employee>(createEmployeeDto), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.AddAsync(employee), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetEmployeeDtoByIdAsync(employee.Id), Times.Once);
        }

        [Test]
        public async Task UpdateEmployeeAsync_ReturnsUpdatedEmployeeDto_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;
            var updateEmployeeDto = new UpdateEmployeeDto
            {
                Name = "Updated Name",
                Email = "updated.email@example.com",
                Department = "Marketing"
            };

            // Use the factory to create an existing employee
            var existingEmployee = TestEntityFactory.CreateEmployee(
                employeeId, "Original Name", "original.email@example.com", "Sales");

            var updatedEmployeeDto = TestEntityFactory.CreateEmployeeDto(
                employeeId, "Updated Name", "updated.email@example.com", "Marketing");

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(existingEmployee);

            _mockEmployeeRepository
                .Setup(repo => repo.GetEmployeeDtoByIdAsync(employeeId))
                .ReturnsAsync(updatedEmployeeDto);

            // Act
            var result = await _employeeService.UpdateEmployeeAsync(employeeId, updateEmployeeDto);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(updatedEmployeeDto.Id, result.Id);
            Assert.AreEqual(updatedEmployeeDto.Name, result.Name);
            Assert.AreEqual(updatedEmployeeDto.Email, result.Email);
            Assert.AreEqual(updatedEmployeeDto.Department, result.Department);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.UpdateAsync(existingEmployee), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.GetEmployeeDtoByIdAsync(employeeId), Times.Once);
        }

        [Test]
        public async Task UpdateEmployeeAsync_ReturnsNull_WhenEmployeeDoesNotExist()
        {
            // Arrange
            int employeeId = 999;
            var updateEmployeeDto = new UpdateEmployeeDto
            {
                Name = "Updated Name",
                Email = "updated.email@example.com",
                Department = "Marketing"
            };

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync((Employee)null);

            // Act
            var result = await _employeeService.UpdateEmployeeAsync(employeeId, updateEmployeeDto);

            // Assert
            Assert.IsNull(result);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Employee>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }
        [Test]
        public async Task DeleteEmployeeAsync_EplyeeStillExists()
        {
            // Arrange
            int employeeId = 1;

            // Use the factory to create an employee
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT");

            // Act
            await _employeeService.DeleteEmployeeAsync(employeeId);

            var result = await _employeeService.GetEmployeeByIdAsync(employeeId);

            Assert.IsNotNull(result);
            Assert.AreEqual(false, result.IsActive);
        }
        [Test]
        public async Task DeleteEmployeeAsync_ReturnsTrue_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;
            
            // Use the factory to create an employee
            var employee = TestEntityFactory.CreateEmployee(
                employeeId, "John Doe", "john.doe@example.com", "IT");

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync(employee);

            // Act
            var result = await _employeeService.DeleteEmployeeAsync(employeeId);

            // Assert
            Assert.IsTrue(result);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.UpdateAsync(employee), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Once);
        }

        [Test]
        public async Task DeleteEmployeeAsync_ReturnsFalse_WhenEmployeeDoesNotExist()
        {
            // Arrange
            int employeeId = 999;

            _mockEmployeeRepository
                .Setup(repo => repo.GetByIdAsync(employeeId))
                .ReturnsAsync((Employee)null);

            // Act
            var result = await _employeeService.DeleteEmployeeAsync(employeeId);

            // Assert
            Assert.IsFalse(result);
            _mockEmployeeRepository.Verify(repo => repo.GetByIdAsync(employeeId), Times.Once);
            _mockEmployeeRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Employee>()), Times.Never);
            _mockUnitOfWork.Verify(uow => uow.CompleteAsync(), Times.Never);
        }

        [Test]
        public async Task EmployeeExistsAsync_ReturnsTrue_WhenEmployeeExists()
        {
            // Arrange
            int employeeId = 1;
            
            _mockEmployeeRepository
                .Setup(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()))
                .ReturnsAsync(1);

            // Act
            var result = await _employeeService.EmployeeExistsAsync(employeeId);

            // Assert
            Assert.IsTrue(result);
            _mockEmployeeRepository.Verify(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()), Times.Once);
        }

        [Test]
        public async Task EmployeeExistsAsync_ReturnsFalse_WhenEmployeeDoesNotExist()
        {
            // Arrange
            int employeeId = 999;
            
            _mockEmployeeRepository
                .Setup(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _employeeService.EmployeeExistsAsync(employeeId);

            // Assert
            Assert.IsFalse(result);
            _mockEmployeeRepository.Verify(repo => repo.CountAsync(It.IsAny<Expression<Func<Employee, bool>>>()), Times.Once);
        }
    }
}