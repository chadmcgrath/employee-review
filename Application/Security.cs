
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using EmployeeReview.Application.Services;
using Microsoft.AspNetCore.Authentication;


namespace EmployeeReview.Infrastructure.Security
{
    public static class PolicyNames
    {
        public const string AdminOnly = "AdminOnly";
        public const string AdminOrEmployee = "AdminOrEmployee";
        public const string AdminOrReviewerOrEmployee = "AdminOrReviewerOrEmployee";
        public const string AdminOrReviewer = "AdminOrReviewer";
    }


    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Employee = "Employee";
        public const string Reviewer = "Reviewer";
    }



    public static class CustomClaimTypes
    {
        public const string EmployeeId = "employeeId";
        public const string ReviewerId = "reviewerId";
    }

    public class ReviewAccessRequirement : IAuthorizationRequirement
    {
    }

    public class ReviewAccessHandler : AuthorizationHandler<ReviewAccessRequirement>
    {
        private readonly ILogger<ReviewAccessHandler> _logger;

        public ReviewAccessHandler(ILogger<ReviewAccessHandler> logger = null)
        {
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ReviewAccessRequirement requirement)
        {
            _logger?.LogDebug("Evaluating ReviewAccess authorization");

            // Admin always has access
            if (context.User.IsInRole(UserRoles.Admin))
            {
                _logger?.LogDebug("User is Admin - access granted");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Reviewer has access to all employees
            if (context.User.IsInRole(UserRoles.Reviewer))
            {
                _logger?.LogDebug("User is Reviewer - access granted");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Extract employee ID from resource
            int? employeeId = GetEmployeeIdFromResource(context.Resource);
            _logger?.LogDebug("Extracted employee ID from resource: {EmployeeId}", employeeId);

            // If we couldn't determine the employee ID, deny access
            if (!employeeId.HasValue)
            {
                _logger?.LogDebug("Could not determine employee ID - access denied");
                context.Fail();
                return Task.CompletedTask;
            }

            // Check if user is the employee
            var employeeIdClaim = context.User.FindFirst(CustomClaimTypes.EmployeeId);
            if (employeeIdClaim != null && employeeIdClaim.Value == employeeId.Value.ToString())
            {
                _logger?.LogDebug("User is the employee - access granted");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Not authorized
            _logger?.LogDebug("User is not authorized to access this resource");
            context.Fail();
            return Task.CompletedTask;
        }

        private int? GetEmployeeIdFromResource(object resource)
        {
            _logger?.LogDebug("Getting employee ID from resource type: {ResourceType}",
                resource?.GetType().Name ?? "null");

            if (resource == null)
                return null;

            // If resource is a Tuple<int, int> (legacy format)
            if (resource is Tuple<int, int> ids)
            {
                _logger?.LogDebug("Resource is Tuple<int, int>: Employee ID = {EmployeeId}, Reviewer ID = {ReviewerId}",
                    ids.Item1, ids.Item2);
                return ids.Item1; // Employee ID is first item
            }

            // Direct int value
            if (resource is int employeeId)
            {
                _logger?.LogDebug("Resource is direct int: {EmployeeId}", employeeId);
                return employeeId;
            }

            // If the resource is a RouteValueDictionary (from route data)
            if (resource is Microsoft.AspNetCore.Routing.RouteValueDictionary routeValues)
            {
                _logger?.LogDebug("Resource is RouteValueDictionary with keys: {Keys}",
                    string.Join(", ", routeValues.Keys));

                // Try to get employeeId from route values
                if (routeValues.TryGetValue("employeeId", out var idObj) &&
                    int.TryParse(idObj?.ToString(), out var parsedId))
                {
                    _logger?.LogDebug("Found employeeId in route values: {EmployeeId}", parsedId);
                    return parsedId;
                }

                // Also check for 'id' which might be an employee ID
                if (routeValues.TryGetValue("id", out var idObj2) &&
                    int.TryParse(idObj2?.ToString(), out var parsedId2))
                {
                    _logger?.LogDebug("Found id in route values: {EmployeeId}", parsedId2);
                    return parsedId2;
                }
            }

            // If the resource implements IEmployeeResource interface
            if (resource is IEmployeeResource employeeResource)
            {
                _logger?.LogDebug("Resource implements IEmployeeResource: {EmployeeId}",
                    employeeResource.EmployeeId);
                return employeeResource.EmployeeId;
            }

            // If resource is a HttpContext
            if (resource is Microsoft.AspNetCore.Http.HttpContext httpContext)
            {
                _logger?.LogDebug("Resource is HttpContext with route values: {RouteValues}",
                    string.Join(", ", httpContext.Request.RouteValues.Select(kv => $"{kv.Key}={kv.Value}")));

                // Try to get employeeId from route
                if (httpContext.Request.RouteValues.TryGetValue("employeeId", out var idObj) &&
                    int.TryParse(idObj?.ToString(), out var parsedId))
                {
                    _logger?.LogDebug("Found employeeId in HttpContext route values: {EmployeeId}", parsedId);
                    return parsedId;
                }

                // Try to get from query string
                if (httpContext.Request.Query.TryGetValue("employeeId", out var queryValues) &&
                    int.TryParse(queryValues.FirstOrDefault(), out var queryId))
                {
                    _logger?.LogDebug("Found employeeId in query string: {EmployeeId}", queryId);
                    return queryId;
                }
            }

            _logger?.LogDebug("Could not extract employee ID from resource");
            return null;
        }
    }
    

    public class JwtHandler
    {
        private readonly ISecretManagementService _secretService;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expiryInMinutes;

        public JwtHandler(ISecretManagementService secretService, string issuer, string audience, int expiryInMinutes)
        {
            _secretService = secretService ?? throw new ArgumentNullException(nameof(secretService));
            _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
            _audience = audience ?? throw new ArgumentNullException(nameof(audience));
            _expiryInMinutes = expiryInMinutes > 0 ? expiryInMinutes : throw new ArgumentException("Expiry must be greater than 0", nameof(expiryInMinutes));
        }

        // Make sure it's adding the EmployeeId claim correctly

        public async Task<string> GenerateTokenAsync(string username, string role, int? employeeId = null, int? reviewerId = null)
        {
            // Get the secret
            var secret = await _secretService.GetSecretAsync("JwtSecret");
            if (string.IsNullOrEmpty(secret))
            {
                throw new InvalidOperationException("JWT secret not found");
            }

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, role)
    };

            // Add EmployeeId claim if provided
            if (employeeId.HasValue)
            {
                // IMPORTANT: Make sure the claim type matches what's checked in the authorization handler
                claims.Add(new Claim(CustomClaimTypes.EmployeeId, employeeId.Value.ToString()));
            }

            // Add ReviewerId claim if provided
            if (reviewerId.HasValue)
            {
                claims.Add(new Claim(CustomClaimTypes.ReviewerId, reviewerId.Value.ToString()));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_expiryInMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static void ConfigureJwtAuthentication(IServiceCollection services, string issuer, string audience, string secretKey)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // In production, this should be true
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Add debugging events
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        Console.WriteLine("JWT Token validated successfully");
                        return Task.CompletedTask;
                    }
                };
            });
        }
    }



    public class ApiKeyAuthOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "ApiKey";
        public string Scheme => DefaultScheme;
        public string AuthenticationType = DefaultScheme;
    }

    public class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthOptions>
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private readonly ISecretManagementService _secretService;

        public ApiKeyAuthHandler(
            IOptionsMonitor<ApiKeyAuthOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            ISecretManagementService secretService)
            : base(options, logger, encoder, clock)
        {
            _secretService = secretService ?? throw new ArgumentNullException(nameof(secretService));
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
            {
                Console.WriteLine("No API Key header found");
                return AuthenticateResult.NoResult();
            }

            var providedApiKey = apiKeyHeaderValues.ToString();
            if (string.IsNullOrEmpty(providedApiKey))
            {
                Console.WriteLine("Empty API Key provided");
                return AuthenticateResult.NoResult();
            }

            var validApiKey = await _secretService.GetSecretAsync("ApiKey");
            Console.WriteLine($"API Key validation: {providedApiKey.Substring(0, Math.Min(3, providedApiKey.Length))}... vs {validApiKey?.Substring(0, Math.Min(3, validApiKey?.Length ?? 0))}...");

            if (providedApiKey != validApiKey)
            {
                Console.WriteLine("Invalid API Key");
                return AuthenticateResult.Fail("Invalid API Key");
            }

            // API key valid, create authenticated user
            var claims = new[] {
        new Claim(ClaimTypes.NameIdentifier, "ApiUser"),
        new Claim(ClaimTypes.Role, UserRoles.Admin)
    };

            var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Options.Scheme);

            Console.WriteLine("API Key authentication successful");
            return AuthenticateResult.Success(ticket);
        }
    }

    public static class AuthorizationPolicyProvider
    {
        public static void ConfigureAuthorizationPolicies(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // Policy for admin-only endpoints
                options.AddPolicy(PolicyNames.AdminOnly, policy =>
                {
                    policy.RequireRole(UserRoles.Admin);
                });

                // Policy for endpoints that allow admin or the employee
                options.AddPolicy(PolicyNames.AdminOrEmployee, policy =>
                {
                    policy.AddRequirements(new EmployeeAccessRequirement());
                });

                // Policy for review endpoints (admin, reviewer, or the employee)
                options.AddPolicy(PolicyNames.AdminOrReviewerOrEmployee, policy =>
                {
                    policy.AddRequirements(new ReviewAccessRequirement());
                });

            });

            // Register authorization handlers
            services.AddScoped<IAuthorizationHandler, EmployeeAccessHandler>();
            services.AddScoped<IAuthorizationHandler, ReviewAccessHandler>();
        }
    }

    public class EmployeeAccessRequirement : IAuthorizationRequirement
    {
    }

    public class EmployeeAccessHandler : AuthorizationHandler<EmployeeAccessRequirement>
    {
        private readonly ILogger<EmployeeAccessHandler> _logger;

        public EmployeeAccessHandler(ILogger<EmployeeAccessHandler> logger = null)
        {
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EmployeeAccessRequirement requirement)
        {
            _logger?.LogDebug("Evaluating EmployeeAccess authorization");
            _logger?.LogDebug("User claims: {@Claims}", context.User.Claims.Select(c => new { c.Type, c.Value }));

            // Check if user is an Admin (always allowed)
            if (context.User.IsInRole(UserRoles.Admin))
            {
                _logger?.LogDebug("User is in Admin role - access granted");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Get employee ID from claims
            var employeeIdClaim = context.User.FindFirst(CustomClaimTypes.EmployeeId);

            if (employeeIdClaim == null)
            {
                _logger?.LogDebug("No EmployeeId claim found - access denied");
                _logger?.LogDebug("Available claim types: {@ClaimTypes}",
                    context.User.Claims.Select(c => c.Type).ToList());
                context.Fail();
                return Task.CompletedTask;
            }

            // Log the user's employee ID
            _logger?.LogDebug("User has EmployeeId claim: {EmployeeId}", employeeIdClaim.Value);

            // Get the requested employee ID from the resource
            int? requestedEmployeeId = GetEmployeeIdFromResource(context.Resource);
            _logger?.LogDebug("Requested EmployeeId from resource: {RequestedEmployeeId}", requestedEmployeeId);

            // If we couldn't determine the requested employee ID, deny access
            if (!requestedEmployeeId.HasValue)
            {
                _logger?.LogDebug("Could not determine requested EmployeeId - access denied");
                _logger?.LogDebug("Resource type: {ResourceType}", context.Resource?.GetType().Name ?? "null");
                _logger?.LogDebug("Resource value: {ResourceValue}", context.Resource?.ToString() ?? "null");
                context.Fail();
                return Task.CompletedTask;
            }

            // Compare user's employee ID with the requested employee ID
            // IMPORTANT: Convert both to string for comparison to avoid type mismatches
            string employeeIdString = employeeIdClaim.Value;
            string requestedIdString = requestedEmployeeId.Value.ToString();

            _logger?.LogDebug("Comparing claim value '{ClaimValue}' with requested ID '{RequestedId}'",
                employeeIdString, requestedIdString);

            if (string.Equals(employeeIdString, requestedIdString, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("User's EmployeeId matches requested EmployeeId - access granted");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Not authorized
            _logger?.LogDebug("User's EmployeeId does not match requested EmployeeId - access denied");
            context.Fail();
            return Task.CompletedTask;
        }

        private int? GetEmployeeIdFromResource(object resource)
        {
            try
            {
                // Log the resource type to help with debugging
                _logger?.LogDebug("Extracting employee ID from resource type: {ResourceType}",
                    resource?.GetType().Name ?? "null");

                // Check different resource types to extract employee ID
                if (resource == null)
                    return null;

                // Direct int value
                if (resource is int employeeId)
                {
                    _logger?.LogDebug("Resource is direct int: {EmployeeId}", employeeId);
                    return employeeId;
                }

                // If the resource is a RouteValueDictionary (from route data)
                if (resource is Microsoft.AspNetCore.Routing.RouteValueDictionary routeValues)
                {
                    _logger?.LogDebug("Resource is RouteValueDictionary with keys: {Keys}",
                        string.Join(", ", routeValues.Keys));

                    // Try to get employeeId from route values
                    if (routeValues.TryGetValue("employeeId", out var idObj) &&
                        int.TryParse(idObj?.ToString(), out var parsedId))
                    {
                        _logger?.LogDebug("Found employeeId in route values: {EmployeeId}", parsedId);
                        return parsedId;
                    }

                    // Also check for 'id' which might be an employee ID
                    if (routeValues.TryGetValue("id", out var idObj2) &&
                        int.TryParse(idObj2?.ToString(), out var parsedId2))
                    {
                        _logger?.LogDebug("Found id in route values: {EmployeeId}", parsedId2);
                        return parsedId2;
                    }
                }

                // If the resource implements IEmployeeResource interface
                if (resource is IEmployeeResource employeeResource)
                {
                    _logger?.LogDebug("Resource implements IEmployeeResource: {EmployeeId}",
                        employeeResource.EmployeeId);
                    return employeeResource.EmployeeId;
                }

                // If resource is a HttpContext
                if (resource is Microsoft.AspNetCore.Http.HttpContext httpContext)
                {
                    _logger?.LogDebug("Resource is HttpContext with route values: {RouteValues}",
                        string.Join(", ", httpContext.Request.RouteValues.Select(kv => $"{kv.Key}={kv.Value}")));

                    // Try to get employeeId from route
                    if (httpContext.Request.RouteValues.TryGetValue("employeeId", out var idObj) &&
                        int.TryParse(idObj?.ToString(), out var parsedId))
                    {
                        _logger?.LogDebug("Found employeeId in HttpContext route values: {EmployeeId}", parsedId);
                        return parsedId;
                    }

                    // Also check for 'id' from route
                    if (httpContext.Request.RouteValues.TryGetValue("id", out var idObj2) &&
                        int.TryParse(idObj2?.ToString(), out var parsedId2))
                    {
                        _logger?.LogDebug("Found id in HttpContext route values: {EmployeeId}", parsedId2);
                        return parsedId2;
                    }

                    // Try to get from query string
                    if (httpContext.Request.Query.TryGetValue("employeeId", out var queryValues) &&
                        int.TryParse(queryValues.FirstOrDefault(), out var queryId))
                    {
                        _logger?.LogDebug("Found employeeId in query string: {EmployeeId}", queryId);
                        return queryId;
                    }
                }

                // Could not determine employee ID
                _logger?.LogDebug("Could not extract employee ID from resource");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error extracting employee ID from resource");
                return null;
            }
        }
    }
    

    // Interface that can be implemented by resource types to provide employee ID
    public interface IEmployeeResource
    {
        int EmployeeId { get; }
    }
}

