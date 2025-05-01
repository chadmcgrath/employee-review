using EmployeeReview.Application.Services;
using EmployeeReview.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

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

        [HttpGet("token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetToken(
            [FromQuery] string role = "Admin",
            [FromQuery] int? employeeId = null,
            [FromQuery] int? reviewerId = null)
        {
            // Only allow in development environment
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning("Attempt to get test token in non-development environment");
                return BadRequest("This endpoint is only available in development environment");
            }

            // If no explicit employeeId/reviewerId was provided, use default values based on role
            if (role == UserRoles.Employee && !employeeId.HasValue)
            {
                employeeId = 1;  // Default employee ID
            }
            else if (role == UserRoles.Reviewer && !reviewerId.HasValue)
            {
                reviewerId = 2;  // Default reviewer ID
            }

            _logger.LogInformation("Generating test token with role={Role}, employeeId={EmployeeId}, reviewerId={ReviewerId}",
                role, employeeId, reviewerId);

            var token = await _jwtHandler.GenerateTokenAsync("testuser", role, employeeId, reviewerId);

            // For easier debugging, decode and log the claims
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var decodedToken = tokenHandler.ReadJwtToken(token);

            _logger.LogInformation("Generated token with claims: {@Claims}",
                decodedToken.Claims.Select(c => new { c.Type, c.Value }));

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