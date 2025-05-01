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

static void ConfigureDevelopmentEnvironment(WebApplication app)
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Review API v1");

        // Important: Inject our custom JavaScript for role switching
        c.InjectJavascript("/swagger-ui/role-switcher.js");
    });

    // Serve our custom JavaScript file
    app.MapGet("/swagger-ui/role-switcher.js", async context =>
    {
        context.Response.ContentType = "application/javascript";

        // This is the JavaScript content - replace with your actual file path if you want to serve from disk
        string js = @"
// role-switcher.js
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
        container.className = 'role-switcher';
        container.style.padding = '10px';
        container.style.backgroundColor = '#f8f8f8';
        container.style.borderRadius = '4px';
        container.style.margin = '10px 0';
        container.style.textAlign = 'center';
        container.style.boxShadow = '0 1px 3px rgba(0,0,0,0.1)';

        // Add title
        const title = document.createElement('div');
        title.innerText = 'Test with different roles:';
        title.style.fontWeight = 'bold';
        title.style.marginBottom = '8px';
        container.appendChild(title);

        // Add role buttons
        const roles = ['Admin', 'Employee', 'Reviewer'];
        roles.forEach(role => {
            const button = document.createElement('button');
            button.innerText = role;
            button.style.margin = '0 5px';
            button.style.padding = '6px 12px';
            button.style.border = '1px solid #ccc';
            button.style.borderRadius = '4px';
            button.style.cursor = 'pointer';
            button.style.backgroundColor = '#fff';
            button.style.fontWeight = 'normal';
            button.dataset.role = role;
            
            // Add current role indicator
            const currentRole = localStorage.getItem('currentRole');
            if (currentRole === role) {
                button.style.backgroundColor = '#4CAF50';
                button.style.color = 'white';
                button.style.borderColor = '#4CAF50';
            }
            
            button.addEventListener('click', function() {
                switchRole(role);
            });
            
            container.appendChild(button);
        });

        // Add a status line to show current token
        const statusLine = document.createElement('div');
        statusLine.className = 'status-line';
        statusLine.style.fontSize = '12px';
        statusLine.style.marginTop = '8px';
        statusLine.style.color = '#666';
        container.appendChild(statusLine);
        
        updateStatusLine(statusLine);

        // Insert before the Swagger UI container
        const swaggerUi = document.querySelector('.swagger-ui');
        if (swaggerUi && swaggerUi.parentNode) {
            swaggerUi.parentNode.insertBefore(container, swaggerUi);
        }
        
        // If we have a token, add it to authorization
        const token = localStorage.getItem('authToken');
        if (token) {
            addAuthToRequests(token);
        }
    }

    function switchRole(role) {
        // Fetch token from the TestAuth API
        fetch(`/api/v1/TestAuth/token?role=${role}`)
            .then(response => response.json())
            .then(data => {
                // Save token and role to localStorage
                localStorage.setItem('authToken', data.token);
                localStorage.setItem('currentRole', role);
                
                // Update UI to reflect the change
                updateRoleButtons(role);
                
                // Update token in requests
                addAuthToRequests(data.token);
                
                // Show success message
                showMessage(`Now testing as: ${role}`);
                
                // Update status line
                updateStatusLine(document.querySelector('.status-line'));
                
                // Reload the page to refresh the Swagger UI
                window.location.reload();
            })
            .catch(error => {
                console.error('Error switching role:', error);
                showMessage('Error switching role. Check console for details.', true);
            });
    }

    function updateRoleButtons(currentRole) {
        // Update all role buttons to reflect current selection
        const buttons = document.querySelectorAll('.role-switcher button');
        buttons.forEach(button => {
            if (button.dataset.role === currentRole) {
                button.style.backgroundColor = '#4CAF50';
                button.style.color = 'white';
                button.style.borderColor = '#4CAF50';
            } else {
                button.style.backgroundColor = '#fff';
                button.style.color = '#000';
                button.style.borderColor = '#ccc';
            }
        });
    }

    function updateStatusLine(statusLine) {
        if (!statusLine) return;
        
        const role = localStorage.getItem('currentRole');
        const token = localStorage.getItem('authToken');
        
        if (role && token) {
            statusLine.innerText = `Active role: ${role} (token automatically applied to all requests)`;
        } else {
            statusLine.innerText = 'No role selected';
        }
    }

    function addAuthToRequests(token) {
        // Hook into Swagger UI's fetch to add the token
        const originalFetch = window.fetch;
        window.fetch = function(resource, options) {
            // Clone options to avoid modifying the original
            options = options || {};
            options = { ...options };
            
            // Add headers if not present
            if (!options.headers) {
                options.headers = {};
            }
            
            // Add authorization header with token
            if (token && resource.toString().includes('/api/')) {
                options.headers['Authorization'] = `Bearer ${token}`;
            }
            
            return originalFetch.call(this, resource, options);
        };
    }

    function showMessage(message, isError) {
        // Create or get message container
        let msgContainer = document.querySelector('.role-switcher-message');
        if (!msgContainer) {
            msgContainer = document.createElement('div');
            msgContainer.className = 'role-switcher-message';
            msgContainer.style.padding = '10px';
            msgContainer.style.margin = '10px 0';
            msgContainer.style.borderRadius = '4px';
            msgContainer.style.textAlign = 'center';
            
            // Insert after role switcher
            const roleSwitcher = document.querySelector('.role-switcher');
            if (roleSwitcher && roleSwitcher.parentNode) {
                roleSwitcher.parentNode.insertBefore(msgContainer, roleSwitcher.nextSibling);
            }
        }
        
        // Set message style based on type
        msgContainer.style.backgroundColor = isError ? '#f8d7da' : '#d4edda';
        msgContainer.style.color = isError ? '#721c24' : '#155724';
        msgContainer.innerText = message;
        
        // Auto-remove after 3 seconds
        setTimeout(() => {
            if (msgContainer.parentNode) {
                msgContainer.parentNode.removeChild(msgContainer);
            }
        }, 3000);
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