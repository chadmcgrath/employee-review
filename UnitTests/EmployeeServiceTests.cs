using EmployeeReview.Application.Interfaces;
using EmployeeReview.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace EmployeeReview.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class TestAuthController : ControllerBase
    {
        private readonly JwtHandler _jwtHandler;
        private readonly IWebHostEnvironment _environment;
        private readonly ISecretManagementService _secretService;
        private readonly ILogger<TestAuthController> _logger;

        public TestAuthController(
            JwtHandler jwtHandler,
            IWebHostEnvironment environment,
            ISecretManagementService secretService,
            ILogger<TestAuthController> logger)
        {
            _jwtHandler = jwtHandler ?? throw new ArgumentNullException(nameof(jwtHandler));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _secretService = secretService ?? throw new ArgumentNullException(nameof(secretService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/v1/testauth/token
        [HttpGet("token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetToken([FromQuery] string role = "Admin")
        {
            // Only allow in development environment
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning("Attempt to get test token in non-development environment");
                return BadRequest("This endpoint is only available in development environment");
            }

            _logger.LogInformation("Generating test token with role={Role}", role);

            int? employeeId = null;
            int? reviewerId = null;

            // Set claims based on role
            switch (role)
            {
                case UserRoles.Employee:
                    employeeId = 1;  // Mock employee ID
                    break;
                case UserRoles.Reviewer:
                    reviewerId = 2;  // Mock reviewer ID
                    break;
                case UserRoles.Admin:
                    // Admin doesn't need specific claims
                    break;
                default:
                    return BadRequest($"Invalid role: {role}. Supported roles: Admin, Employee, Reviewer");
            }

            var token = await _jwtHandler.GenerateTokenAsync("testuser", role, employeeId, reviewerId);
            return Ok(new { token });
        }

        // GET: api/v1/testauth/secret
        [HttpGet("secret")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSecret([FromQuery] string role = "Admin")
        {
            // Only allow in development environment
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning("Attempt to get test secret in non-development environment");
                return BadRequest("This endpoint is only available in development environment");
            }

            _logger.LogInformation("Getting test secret with role={Role}", role);

            // Initialize service with role
            var secretService = new SecretManagementService(_environment.EnvironmentName, role);

            // Get JWT secret for the role
            var secret = await secretService.GetSecretAsync("JwtSecret");
            var apiKey = await secretService.GetSecretAsync("ApiKey");

            return Ok(new { jwtSecret = secret, apiKey });
        }
    }
}