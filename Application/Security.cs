
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
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ReviewAccessRequirement requirement)
        {
            // Parse the resource to get employee ID and reviewer ID
            int? employeeId = null;
            int? reviewerId = null;

            if (context.Resource is Tuple<int, int> ids)
            {
                employeeId = ids.Item1;
                reviewerId = ids.Item2;
            }

            // Admin always has access
            if (context.User.IsInRole(UserRoles.Admin))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check if user is the employee being reviewed
            if (employeeId.HasValue &&
                context.User.HasClaim(c => c.Type == CustomClaimTypes.EmployeeId && c.Value == employeeId.Value.ToString()))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check if user is the reviewer
            if (reviewerId.HasValue &&
                context.User.HasClaim(c => c.Type == CustomClaimTypes.ReviewerId && c.Value == reviewerId.Value.ToString()))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Not authorized
            context.Fail();
            return Task.CompletedTask;
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

        public async Task<string> GenerateTokenAsync(string userId, string role, int? employeeId = null, int? reviewerId = null)
        {
            var secretKey = await _secretService.GetSecretAsync("JwtSecret");
            var key = Encoding.ASCII.GetBytes(secretKey);

            var claims = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
            });

            // Add optional claims if provided
            if (employeeId.HasValue)
                claims.AddClaim(new Claim(CustomClaimTypes.EmployeeId, employeeId.Value.ToString()));

            if (reviewerId.HasValue)
                claims.AddClaim(new Claim(CustomClaimTypes.ReviewerId, reviewerId.Value.ToString()));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = claims,
                Expires = DateTime.UtcNow.AddMinutes(_expiryInMinutes),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
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

    // Handler for employee access requirement
    public class EmployeeAccessHandler : AuthorizationHandler<EmployeeAccessRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EmployeeAccessRequirement requirement)
        {
            // Get the requested employee ID from the resource
            int? requestedEmployeeId = null;
            if (context.Resource is int employeeId)
            {
                requestedEmployeeId = employeeId;
            }

            // Check if user is an Admin (always allowed)
            if (context.User.IsInRole(UserRoles.Admin))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check if user is the employee in question
            if (requestedEmployeeId.HasValue &&
                context.User.HasClaim(c => c.Type == CustomClaimTypes.EmployeeId && c.Value == requestedEmployeeId.Value.ToString()))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Not authorized
            context.Fail();
            return Task.CompletedTask;
        }
    }
}

