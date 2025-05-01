using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Application.Services;
using EmployeeReview.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;


namespace EmployeeReview.Api.Controllers.REST
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(IEmployeeService employeeService, ILogger<EmployeesController> logger)
        {
            _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/v1/employees
        [HttpGet]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(PaginatedListDto<EmployeeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PaginatedListDto<EmployeeDto>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEmployees([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10,
                                                     [FromQuery] string searchTerm = null, [FromQuery] string department = null)
        {
            _logger.LogInformation("Getting employees with pageNumber={PageNumber}, pageSize={PageSize}, searchTerm={SearchTerm}, department={Department}",
                pageNumber, pageSize, searchTerm, department);

            var employees = await _employeeService.GetEmployeesAsync(pageNumber, pageSize, searchTerm, department);
            return Ok(employees);
        }

        // GET: api/v1/employees/5
        [HttpGet("{id}")]
        [Authorize(Policy = PolicyNames.AdminOrEmployee)]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEmployee(int id)
        {
            _logger.LogInformation("Getting employee with id={Id}", id);

            var employee = await _employeeService.GetEmployeeByIdAsync(id);
            if (employee == null)
                return NotFound();

            return Ok(employee);
        }

        // POST: api/v1/employees
        [HttpPost]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto employeeDto)
        {
            _logger.LogInformation("Creating a new employee");

            var createdEmployee = await _employeeService.CreateEmployeeAsync(employeeDto);
            return CreatedAtAction(nameof(GetEmployee), new { id = createdEmployee.Id }, createdEmployee);
        }

        // PUT: api/v1/employees/5
        [HttpPut("{id}")]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] UpdateEmployeeDto employeeDto)
        {
            _logger.LogInformation("Updating employee with id={Id}", id);

            var updatedEmployee = await _employeeService.UpdateEmployeeAsync(id, employeeDto);
            if (updatedEmployee == null)
                return NotFound();

            return Ok(updatedEmployee);
        }

        // DELETE: api/v1/employees/5
        [HttpDelete("{id}")]
        [Authorize(Policy = PolicyNames.AdminOnly)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            _logger.LogInformation("Deleting employee with id={Id}", id);

            var result = await _employeeService.DeleteEmployeeAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
