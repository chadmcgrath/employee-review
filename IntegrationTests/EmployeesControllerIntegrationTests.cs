using EmployeeReview.Api;
using EmployeeReview.Contracts.DTOs;
using EmployeeReview.IntegrationTests.Fixtures;
using EmployeeReview.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace EmployeeReview.IntegrationTests.Controllers
{
    [TestFixture]
    public class EmployeesControllerIntegrationTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new TestWebApplicationFactory<Program>();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task GetEmployees_ReturnsSuccessAndEmployeeList()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/employees");

            // Act
            var response = await _client.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            var employees = JsonConvert.DeserializeObject<PaginatedListDto<EmployeeDto>>(responseContent);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(employees, Is.Not.Null);
            Assert.That(employees.Items, Is.Not.Empty);
            Assert.That(employees.Items.Count, Is.GreaterThanOrEqualTo(3)); // We seeded 3 employees
        }

        [Test]
        public async Task GetEmployee_WithValidId_ReturnsEmployee()
        {
            // Arrange - Get all employees first to find a valid ID
            var allEmployeesResponse = await _client.GetAsync("/api/v1/employees");
            var allEmployeesContent = await allEmployeesResponse.Content.ReadAsStringAsync();
            var allEmployees = JsonConvert.DeserializeObject<PaginatedListDto<EmployeeDto>>(allEmployeesContent);

            var firstEmployeeId = allEmployees.Items[0].Id;

            // Act - Get specific employee
            var response = await _client.GetAsync($"/api/v1/employees/{firstEmployeeId}");
            var responseContent = await response.Content.ReadAsStringAsync();
            var employee = JsonConvert.DeserializeObject<EmployeeDto>(responseContent);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(employee, Is.Not.Null);
            Assert.That(employee.Id, Is.EqualTo(firstEmployeeId));
        }

        [Test]
        public async Task GetEmployee_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = 999; // Assuming this ID doesn't exist

            // Act
            var response = await _client.GetAsync($"/api/v1/employees/{invalidId}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task CreateEmployee_WithValidData_CreatesNewEmployee()
        {
            // Arrange
            var newEmployee = new CreateEmployeeDto
            {
                Name = "Integration Test Employee",
                Email = "integration.test@example.com",
                Department = "Testing"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/employees", newEmployee);
            var responseContent = await response.Content.ReadAsJsonAsync<EmployeeDto>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(responseContent, Is.Not.Null);
            Assert.That(responseContent.Name, Is.EqualTo(newEmployee.Name));
            Assert.That(responseContent.Email, Is.EqualTo(newEmployee.Email));
            Assert.That(responseContent.Department, Is.EqualTo(newEmployee.Department));

            // Verify that the employee was actually created in the database
            var getResponse = await _client.GetAsync($"/api/v1/employees/{responseContent.Id}");
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task UpdateEmployee_WithValidData_UpdatesEmployee()
        {
            // Arrange - Create an employee first
            var newEmployee = new CreateEmployeeDto
            {
                Name = "Employee To Update",
                Email = "to.update@example.com",
                Department = "Before Update"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/employees", newEmployee);
            var createdEmployee = await createResponse.Content.ReadAsJsonAsync<EmployeeDto>();

            // Prepare update data
            var updateData = new UpdateEmployeeDto
            {
                Name = "Updated Employee Name",
                Email = "updated.email@example.com",
                Department = "After Update"
            };

            // Act
            var updateResponse = await _client.PutAsJsonAsync($"/api/v1/employees/{createdEmployee.Id}", updateData);
            var updatedEmployee = await updateResponse.Content.ReadAsJsonAsync<EmployeeDto>();

            // Assert
            Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(updatedEmployee, Is.Not.Null);
            Assert.That(updatedEmployee.Id, Is.EqualTo(createdEmployee.Id));
            Assert.That(updatedEmployee.Name, Is.EqualTo(updateData.Name));
            Assert.That(updatedEmployee.Email, Is.EqualTo(updateData.Email));
            Assert.That(updatedEmployee.Department, Is.EqualTo(updateData.Department));

            // Verify the update was persisted
            var getResponse = await _client.GetAsync($"/api/v1/employees/{createdEmployee.Id}");
            var retrievedEmployee = await getResponse.Content.ReadAsJsonAsync<EmployeeDto>();
            Assert.That(retrievedEmployee.Name, Is.EqualTo(updateData.Name));
        }

        [Test]
        public async Task DeleteEmployee_WithValidId_RemovesEmployee()
        {
            // Arrange - Create an employee first
            var newEmployee = new CreateEmployeeDto
            {
                Name = "Employee To Delete",
                Email = "to.delete@example.com",
                Department = "Delete Test"
            };

            var createResponse = await _client.PostAsJsonAsync("/api/v1/employees", newEmployee);
            var createdEmployee = await createResponse.Content.ReadAsJsonAsync<EmployeeDto>();

            // Act
            var deleteResponse = await _client.DeleteAsync($"/api/v1/employees/{createdEmployee.Id}");

            // Assert
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            // Verify the employee was removed (or soft deleted)
            var getResponse = await _client.GetAsync($"/api/v1/employees/{createdEmployee.Id}");
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetEmployees_WithSearchTerm_FiltersByNameOrEmail()
        {
            // Arrange - Create an employee with a unique name
            var uniqueName = "UniqueSearchName";
            var newEmployee = new CreateEmployeeDto
            {
                Name = uniqueName,
                Email = "unique.search@example.com",
                Department = "Search Test"
            };

            await _client.PostAsJsonAsync("/api/v1/employees", newEmployee);

            // Act
            var response = await _client.GetAsync($"/api/v1/employees?searchTerm={uniqueName}");
            var employees = await response.Content.ReadAsJsonAsync<PaginatedListDto<EmployeeDto>>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(employees.Items, Is.Not.Empty);
            Assert.That(employees.Items, Has.All.Property("Name").Contains(uniqueName));
        }

        [Test]
        public async Task GetEmployees_WithDepartment_FiltersByDepartment()
        {
            // Arrange - Create an employee with a unique department
            var uniqueDepartment = "UniqueDeptSearchTest";
            var newEmployee = new CreateEmployeeDto
            {
                Name = "Department Test Employee",
                Email = "dept.test@example.com",
                Department = uniqueDepartment
            };

            await _client.PostAsJsonAsync("/api/v1/employees", newEmployee);

            // Act
            var response = await _client.GetAsync($"/api/v1/employees?department={uniqueDepartment}");
            var employees = await response.Content.ReadAsJsonAsync<PaginatedListDto<EmployeeDto>>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(employees.Items, Is.Not.Empty);
            Assert.That(employees.Items, Has.All.Property("Department").EqualTo(uniqueDepartment));
        }
    }
}