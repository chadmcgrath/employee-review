using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using EmployeeReview.Infrastructure.Security; // Make sure to include this for CustomClaimTypes

namespace EmployeeReview.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AuthDebugController : ControllerBase
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<AuthDebugController> _logger;

        public AuthDebugController(
            IAuthorizationService authorizationService,
            ILogger<AuthDebugController> logger)
        {
            _authorizationService = authorizationService;
            _logger = logger;
        }

        // GET: api/v1/authdebug/whoami
        [HttpGet("whoami")]
        [AllowAnonymous]
        public IActionResult WhoAmI()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type.EndsWith("/role"))
                .Select(c => c.Value)
                .ToList();

            // FIXED: Use CustomClaimTypes.EmployeeId for correct case
            var employeeIdClaim = User.FindFirst(CustomClaimTypes.EmployeeId);
            var employeeId = employeeIdClaim?.Value;

            // FIXED: Use CustomClaimTypes.ReviewerId for correct case
            var reviewerIdClaim = User.FindFirst(CustomClaimTypes.ReviewerId);
            var reviewerId = reviewerIdClaim?.Value;

            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                UserName = User.Identity?.Name,
                Roles = roles,
                EmployeeId = employeeId,
                ReviewerId = reviewerId,
                AllClaims = claims
            });
        }

        // GET: api/v1/authdebug/employee/{id}
        [HttpGet("employee/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugEmployeeAccess(int id)
        {
            var debugInfo = new StringBuilder();

            // Log user authentication status
            debugInfo.AppendLine($"User is authenticated: {User.Identity?.IsAuthenticated ?? false}");
            if (User.Identity?.IsAuthenticated ?? false)
            {
                debugInfo.AppendLine($"Username: {User.Identity?.Name}");
            }

            // Log roles
            var roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type.EndsWith("/role"))
                .Select(c => c.Value);
            debugInfo.AppendLine($"User roles: {string.Join(", ", roles)}");

            // FIXED: Use CustomClaimTypes.EmployeeId for correct case
            var employeeIdClaim = User.FindFirst(CustomClaimTypes.EmployeeId);
            debugInfo.AppendLine($"EmployeeId claim: {(employeeIdClaim != null ? employeeIdClaim.Value : "not found")}");

            // Log request path and route data
            debugInfo.AppendLine($"Request path: {Request.Path}");
            debugInfo.AppendLine($"Route value 'id': {RouteData.Values["id"]}");

            // Log authorization check
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, id, "AdminOrReviewerOrEmployee");
            debugInfo.AppendLine($"Authorization result: {(authorizationResult.Succeeded ? "Succeeded" : "Failed")}");

            // Test with alternative ways of passing the resource
            var routeValues = new RouteValueDictionary
            {
                { "employeeId", id }
            };
            var routeAuthResult = await _authorizationService.AuthorizeAsync(User, routeValues, "AdminOrReviewerOrEmployee");
            debugInfo.AppendLine($"Authorization with route values: {(routeAuthResult.Succeeded ? "Succeeded" : "Failed")}");

            // Try with string value instead of int
            var stringIdAuthResult = await _authorizationService.AuthorizeAsync(User, id.ToString(), "AdminOrReviewerOrEmployee");
            debugInfo.AppendLine($"Authorization with string ID: {(stringIdAuthResult.Succeeded ? "Succeeded" : "Failed")}");

            // Log all claims
            debugInfo.AppendLine("\nAll Claims:");
            foreach (var claim in User.Claims)
            {
                debugInfo.AppendLine($"  {claim.Type}: {claim.Value}");
            }

            // Return detailed debug info
            return Ok(new
            {
                DebugInfo = debugInfo.ToString(),
                RequestData = new
                {
                    Path = Request.Path.Value,
                    Method = Request.Method,
                    RouteValues = RouteData.Values.ToDictionary(x => x.Key, x => x.Value?.ToString())
                },
                AuthorizationResults = new
                {
                    StandardCheck = authorizationResult.Succeeded,
                    RouteValueCheck = routeAuthResult.Succeeded,
                    StringIdCheck = stringIdAuthResult.Succeeded
                },
                UserInfo = new
                {
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    UserName = User.Identity?.Name,
                    Roles = roles,
                    // FIXED: Use CustomClaimTypes.EmployeeId for correct case
                    EmployeeId = employeeIdClaim?.Value,
                    Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                }
            });
        }

        // GET: api/v1/authdebug/testallmethods/{id}
        [HttpGet("testallmethods/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> TestAllAuthorizationMethods(int id)
        {
            var results = new Dictionary<string, bool>();

            // Add the auth service and capture context for debugging
            var authContext = HttpContext;
            var httpMethod = Request.Method;
            var path = Request.Path;
            var routeData = RouteData;

            // 1. Test passing direct int
            results["Direct Int"] = (await _authorizationService.AuthorizeAsync(User, id, "AdminOrReviewerOrEmployee")).Succeeded;

            // 2. Test with string
            results["String Value"] = (await _authorizationService.AuthorizeAsync(User, id.ToString(), "AdminOrReviewerOrEmployee")).Succeeded;

            // 3. Test with route value dictionary using "employeeId"
            var routeValues1 = new RouteValueDictionary { { "employeeId", id } };
            results["RouteValues (employeeId)"] = (await _authorizationService.AuthorizeAsync(User, routeValues1, "AdminOrReviewerOrEmployee")).Succeeded;

            // 4. Test with route value dictionary using "id"
            var routeValues2 = new RouteValueDictionary { { "id", id } };
            results["RouteValues (id)"] = (await _authorizationService.AuthorizeAsync(User, routeValues2, "AdminOrReviewerOrEmployee")).Succeeded;

            // 5. Test with HttpContext
            results["HttpContext"] = (await _authorizationService.AuthorizeAsync(User, HttpContext, "AdminOrReviewerOrEmployee")).Succeeded;

            // 6. Test with EmployeeResource model
            var employeeResource = new EmployeeResourceModel { EmployeeId = id };
            results["EmployeeResource"] = (await _authorizationService.AuthorizeAsync(User, employeeResource, "AdminOrReviewerOrEmployee")).Succeeded;

            return Ok(new
            {
                EmployeeId = id,
                AuthorizationResults = results,
                UserClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList(),
                // FIXED: Use CustomClaimTypes.EmployeeId for correct case
                EmployeeIdClaim = User.FindFirst(CustomClaimTypes.EmployeeId)?.Value,
                IsAdmin = User.IsInRole(UserRoles.Admin),
                IsEmployee = User.IsInRole(UserRoles.Employee),
                RequestInfo = new
                {
                    Method = httpMethod,
                    Path = path.Value,
                    RouteData = routeData.Values.ToDictionary(x => x.Key, x => x.Value?.ToString())
                }
            });
        }
    }

    // Model to test IEmployeeResource interface
    public class EmployeeResourceModel : IEmployeeResource
    {
        public int EmployeeId { get; set; }
    }
}