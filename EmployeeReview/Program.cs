using Asp.Versioning;
using EmployeeReview.Api.Extensions;
using EmployeeReview.Api.Middleware;
using EmployeeReview.Application.Mappings;
using EmployeeReview.Application.Services;
using EmployeeReview.Infrastructure.Data;
using EmployeeReview.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;
var environment = builder.Environment;
bool isDevelopment = environment.IsDevelopment();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Database Configuration
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(configuration.GetConnectionString("DefaultConnection"));
});

// AutoMapper Configuration
services.AddAutoMapper(typeof(MappingProfile).Assembly);

// API Versioning
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

services.AddControllers();

// Register Services
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<IEmployeeService, EmployeeService>();
services.AddScoped<IPerformanceReviewService, PerformanceReviewService>();
services.AddRateLimiting(configuration);

// Configure environment-specific services
ConfigureEnvironmentSpecificServices(services, configuration, isDevelopment);

// Get JWT Secret
var serviceProvider = services.BuildServiceProvider();
var secretService = serviceProvider.GetRequiredService<ISecretManagementService>();
var jwtSecret = secretService.GetSecretAsync("JwtSecret").GetAwaiter().GetResult();

// Configure Swagger
ConfigureSwagger(services, configuration, secretService, isDevelopment);

// Configure Authorization Policies
AuthorizationPolicyProvider.ConfigureAuthorizationPolicies(services);

// Configure JWT Handler
services.AddScoped<JwtHandler>(provider =>
{
    var secretService = provider.GetRequiredService<ISecretManagementService>();
    return new JwtHandler(
        secretService,
        configuration["Jwt:Issuer"],
        configuration["Jwt:Audience"],
        60 // expiry in minutes
    );
});

// Configure JWT Authentication
JwtHandler.ConfigureJwtAuthentication(services,
    configuration["Jwt:Issuer"],
    configuration["Jwt:Audience"],
    jwtSecret);

// Add MultiAuth scheme for JWT or API Key
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "MultiAuth";
    options.DefaultChallengeScheme = "MultiAuth";
})
.AddPolicyScheme("MultiAuth", "JWT or API Key", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        if (context.Request.Headers.ContainsKey("X-API-Key"))
            return "ApiKey";
        return "Bearer";
    };
})
// Add API Key authentication scheme
.AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>("ApiKey", options => { });

var app = builder.Build();

// Configure the HTTP request pipeline
if (isDevelopment)
{
    ConfigureDevelopmentEnvironment(app);
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Configure the middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

if (isDevelopment)
{
    await DatabaseSeeder.SeedDatabase(app);
}

app.Run();

// Environment-specific configuration methods
static void ConfigureEnvironmentSpecificServices(IServiceCollection services, IConfiguration configuration, bool isDevelopment)
{
    // Secret Management Service
    if (isDevelopment)
    {
        services.AddScoped<ISecretManagementService>(provider =>
            new SecretManagementService(provider.GetRequiredService<IWebHostEnvironment>().EnvironmentName, "Admin"));
    }
    else
    {
        services.AddScoped<ISecretManagementService>(provider =>
            new SecretManagementService(provider.GetRequiredService<IWebHostEnvironment>().EnvironmentName, "Employee"));
    }
}
static void ConfigureSwagger(IServiceCollection services, IConfiguration configuration,
    ISecretManagementService secretService, bool isDevelopment)
{
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Employee Review API",
            Version = "v1",
            Description = "A .NET Core Web API for managing employees and performance reviews",
            Contact = new OpenApiContact
            {
                Name = "API Support",
                Email = "support@example.com"
            }
        });

        // Add JWT Authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    },
                    Scheme = "oauth2",
                    Name = "Bearer",
                    In = ParameterLocation.Header
                },
                new string[] {}
            }
        });

        // Add API Key Authentication to Swagger
        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API Key Authentication",
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "ApiKey"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    },
                    In = ParameterLocation.Header
                },
                new string[] {}
            }
        });

        // Development-specific Swagger configuration
        if (isDevelopment)
        {
            // REMOVED: Code that generated and hardcoded the admin token
            // Instead, we let the JavaScript role switcher handle authorization

            Console.WriteLine($"Dev mode: Using dynamic role switcher for authentication");
        }

        // Add operation filter to apply security to endpoints with [Authorize] attribute
        c.OperationFilter<SecurityRequirementsOperationFilter>();

        // Include XML comments
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });
}

// Add this to your ConfigureDevelopmentEnvironment method in Program.cs
static void ConfigureDevelopmentEnvironment(WebApplication app)
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Review API v1");

        // Inject our custom JavaScript for role switching
        c.InjectJavascript("/swagger-ui/simple-role-switcher.js");
    });

    // Serve our custom JavaScript file
    app.MapGet("/swagger-ui/simple-role-switcher.js", async context =>
    {
        context.Response.ContentType = "application/javascript";

        string js = @"
// Simple Role Switcher with Debug Info
(function() {
    // Wait for Swagger UI to finish loading
    const interval = setInterval(function() {
        if (document.querySelector('.swagger-ui')) {
            clearInterval(interval);
            initRoleSwitcher();
        }
    }, 100);

    function initRoleSwitcher() {
        // Create role switcher container
        const container = document.createElement('div');
        container.style.padding = '15px';
        container.style.backgroundColor = '#f0f0f0';
        container.style.margin = '10px 0';
        container.style.borderRadius = '4px';
        container.style.border = '1px solid #ddd';

        // Create title
        const title = document.createElement('h3');
        title.innerText = 'Test with different roles';
        title.style.margin = '0 0 10px 0';
        container.appendChild(title);

        // Create button container
        const buttonContainer = document.createElement('div');
        buttonContainer.style.marginBottom = '10px';
        container.appendChild(buttonContainer);

        // Add role buttons
        addRoleButton(buttonContainer, 'Admin', null, null);
        addRoleButton(buttonContainer, 'Employee', 1, null);
        addRoleButton(buttonContainer, 'Reviewer', null, 2);

        // Add debug info container
        const debugContainer = document.createElement('div');
        debugContainer.style.marginTop = '10px';
        debugContainer.style.padding = '10px';
        debugContainer.style.backgroundColor = '#fff';
        debugContainer.style.border = '1px solid #ddd';
        debugContainer.style.borderRadius = '4px';
        debugContainer.style.fontSize = '12px';
        debugContainer.style.fontFamily = 'monospace';
        debugContainer.style.whiteSpace = 'pre-wrap';
        debugContainer.id = 'auth-debug-info';
        container.appendChild(debugContainer);

        // Create a refresh button for debug info
        const refreshButton = document.createElement('button');
        refreshButton.innerText = 'Refresh Token Info';
        refreshButton.style.marginTop = '10px';
        refreshButton.style.padding = '5px 10px';
        refreshButton.onclick = function() {
            updateDebugInfo();
        };
        container.appendChild(refreshButton);

        // Add to page
        const swaggerUi = document.querySelector('.swagger-ui');
        if (swaggerUi && swaggerUi.parentNode) {
            swaggerUi.parentNode.insertBefore(container, swaggerUi);
        }

        // Update debug info initially
        updateDebugInfo();
    }

    function addRoleButton(container, role, employeeId, reviewerId) {
        const button = document.createElement('button');
        button.innerText = role + (employeeId ? ' (ID: ' + employeeId + ')' : '') + 
                          (reviewerId ? ' (Reviewer ID: ' + reviewerId + ')' : '');
        button.style.margin = '0 5px 5px 0';
        button.style.padding = '8px 15px';
        button.style.borderRadius = '4px';
        button.style.border = '1px solid #ccc';
        button.style.backgroundColor = '#fff';
        button.style.cursor = 'pointer';
        button.style.fontWeight = 'bold';
        
        // Check if this is the current role
        const currentToken = localStorage.getItem('authToken');
        const currentRole = localStorage.getItem('currentRole');
        const currentEmpId = localStorage.getItem('currentEmployeeId');
        const currentRevId = localStorage.getItem('currentReviewerId');
        
        if (currentRole === role && 
            ((!employeeId && !currentEmpId) || (employeeId && currentEmpId == employeeId)) &&
            ((!reviewerId && !currentRevId) || (reviewerId && currentRevId == reviewerId))) {
            button.style.backgroundColor = '#4CAF50';
            button.style.color = 'white';
            button.style.borderColor = '#4CAF50';
        }
        
        button.onclick = function() {
            switchToRole(role, employeeId, reviewerId);
        };
        
        container.appendChild(button);
        return button;
    }

    function switchToRole(role, employeeId, reviewerId) {
        // Build the URL with all parameters
        let url = '/api/v1/TestAuth/token?role=' + encodeURIComponent(role);
        if (employeeId) {
            url += '&employeeId=' + encodeURIComponent(employeeId);
        }
        if (reviewerId) {
            url += '&reviewerId=' + encodeURIComponent(reviewerId);
        }

        // Fetch the token
        fetch(url)
            .then(response => {
                if (!response.ok) {
                    throw new Error('Failed to get token: ' + response.status);
                }
                return response.json();
            })
            .then(data => {
                // Store token and role info
                localStorage.setItem('authToken', data.token);
                localStorage.setItem('currentRole', role);
                localStorage.setItem('currentEmployeeId', employeeId || '');
                localStorage.setItem('currentReviewerId', reviewerId || '');
                
                // Hook fetch to add auth header
                hookFetch(data.token);
                
                // Show success message and update debug info
                showMessage('Now using role: ' + role + 
                           (employeeId ? ' with Employee ID: ' + employeeId : '') +
                           (reviewerId ? ' with Reviewer ID: ' + reviewerId : ''));
                
                // Update the visual state of buttons
                updateButtonStyles(role, employeeId, reviewerId);
                
                // Update debug info
                updateDebugInfo();
            })
            .catch(error => {
                console.error('Error getting token:', error);
                showMessage('Error: ' + error.message, true);
            });
    }

    function hookFetch(token) {
        // Only hook if not already hooked
        if (!window.fetchHooked) {
            const originalFetch = window.fetch;
            window.fetch = function(resource, options) {
                // Only modify API requests
                if (typeof resource === 'string' && resource.includes('/api/')) {
                    options = options || {};
                    options.headers = options.headers || {};
                    options.headers['Authorization'] = 'Bearer ' + token;
                }
                return originalFetch.call(this, resource, options);
            };
            window.fetchHooked = true;
        }
    }

    function updateButtonStyles(currentRole, currentEmpId, currentRevId) {
        const buttons = document.querySelectorAll('.swagger-ui button');
        // Reset all buttons first
        buttons.forEach(button => {
            if (button.innerText.includes('Admin') || 
                button.innerText.includes('Employee') || 
                button.innerText.includes('Reviewer')) {
                button.style.backgroundColor = '#fff';
                button.style.color = '#000';
                button.style.borderColor = '#ccc';
            }
        });
        
        // Highlight the current role button
        buttons.forEach(button => {
            const text = button.innerText;
            if ((text.includes(currentRole)) &&
                (!currentEmpId || text.includes('ID: ' + currentEmpId)) &&
                (!currentRevId || text.includes('Reviewer ID: ' + currentRevId))) {
                button.style.backgroundColor = '#4CAF50';
                button.style.color = 'white';
                button.style.borderColor = '#4CAF50';
            }
        });
    }

    function updateDebugInfo() {
        const debugContainer = document.getElementById('auth-debug-info');
        if (!debugContainer) return;
        
        const token = localStorage.getItem('authToken');
        if (!token) {
            debugContainer.innerText = 'No auth token found. Click a role button to get started.';
            return;
        }
        
        try {
            // Decode the JWT token
            const parts = token.split('.');
            if (parts.length !== 3) {
                debugContainer.innerText = 'Invalid token format';
                return;
            }
            
            // Decode the payload
            const payload = JSON.parse(atob(parts[1]));
            
            // Format and display token info
            let info = 'CURRENT TOKEN INFO:\\n';
            info += '-----------------\\n';
            info += 'Role: ' + (payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'Not found') + '\\n';
            
            // Look for employee ID claim
            const empIdClaim = Object.keys(payload).find(key => 
                key === 'EmployeeId' || 
                key === 'employeeId' || 
                key === 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/employeeId');
            
            if (empIdClaim) {
                info += 'Employee ID Claim (' + empIdClaim + '): ' + payload[empIdClaim] + '\\n';
            } else {
                info += 'Employee ID Claim: Not found\\n';
            }
            
            // Look for reviewer ID claim
            const revIdClaim = Object.keys(payload).find(key => 
                key === 'ReviewerId' || 
                key === 'reviewerId' || 
                key === 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/reviewerId');
            
            if (revIdClaim) {
                info += 'Reviewer ID Claim (' + revIdClaim + '): ' + payload[revIdClaim] + '\\n';
            } else {
                info += 'Reviewer ID Claim: Not found\\n';
            }
            
            info += '\\nALL CLAIMS:\\n';
            info += '-----------\\n';
            Object.keys(payload).forEach(key => {
                info += key + ': ' + payload[key] + '\\n';
            });
            
            info += '\\nTOKEN:\\n';
            info += '------\\n';
            info += token.substring(0, 20) + '...' + token.substring(token.length - 10);
            
            debugContainer.innerText = info;
        } catch (e) {
            debugContainer.innerText = 'Error decoding token: ' + e.message;
        }
    }

    function showMessage(text, isError) {
        const message = document.createElement('div');
        message.innerText = text;
        message.style.padding = '10px';
        message.style.margin = '10px 0';
        message.style.borderRadius = '4px';
        message.style.textAlign = 'center';
        message.style.backgroundColor = isError ? '#f8d7da' : '#d4edda';
        message.style.color = isError ? '#721c24' : '#155724';
        
        // Add to page
        const swaggerUi = document.querySelector('.swagger-ui');
        if (swaggerUi && swaggerUi.parentNode) {
            swaggerUi.parentNode.insertBefore(message, swaggerUi);
        }
        
        // Remove after a few seconds
        setTimeout(() => {
            if (message.parentNode) {
                message.parentNode.removeChild(message);
            }
        }, 3000);
    }

    // Initialize with token from storage if available
    const storedToken = localStorage.getItem('authToken');
    if (storedToken) {
        hookFetch(storedToken);
    }
})();";

        await context.Response.WriteAsync(js);
    });

    // Setup documents directory
    SetupDocumentsDirectory(app);
}

static void SetupDocumentsDirectory(WebApplication app)
{
    // Ensure the Documents directory exists for DesignDecisions.docx
    var documentsDir = Path.Combine(app.Environment.ContentRootPath, "Documents");
    if (!Directory.Exists(documentsDir))
    {
        Directory.CreateDirectory(documentsDir);
    }

    // Create a sample DesignDecisions.docx file if it doesn't exist
    var designDocPath = Path.Combine(documentsDir, "DesignDecisions.docx");
    if (!File.Exists(designDocPath))
    {
        // Create a simple text file with a .docx extension as a placeholder
        File.WriteAllText(designDocPath, "This is a placeholder for the Design Decisions document.");
    }
}

// Swagger Security Requirements Filter
public class SecurityRequirementsOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        // Check for authorize attribute
        var hasAuthorize = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Any();

        if (!hasAuthorize)
            return;

        // Initialize if null
        operation.Security ??= new List<OpenApiSecurityRequirement>();

        // Add JWT bearer token security requirement
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });

        // Add API Key security requirement
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                new string[] {}
            }
        });
    }
}

public partial class Program { }