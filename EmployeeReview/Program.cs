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
// Ultra Simple Role Switcher
(function() {
    console.log('Role switcher script loaded');
    
    // Wait for Swagger UI to finish loading
    const interval = setInterval(function() {
        if (document.querySelector('.swagger-ui')) {
            clearInterval(interval);
            console.log('Swagger UI loaded, initializing role switcher');
            initRoleSwitcher();
        }
    }, 100);

    function initRoleSwitcher() {
        // Create role switcher container
        const container = document.createElement('div');
        container.className = 'role-switcher';
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

        // Create token display
        const tokenDisplay = document.createElement('div');
        tokenDisplay.id = 'current-role-display';
        tokenDisplay.style.marginBottom = '10px';
        tokenDisplay.style.padding = '5px';
        tokenDisplay.style.backgroundColor = '#ddd';
        tokenDisplay.style.borderRadius = '4px';
        tokenDisplay.style.fontWeight = 'bold';
        container.appendChild(tokenDisplay);

        // Add role buttons - SIMPLIFIED
        addRoleButton(container, 'Admin');
        addRoleButton(container, 'Employee');
        addRoleButton(container, 'Reviewer');

        // Add to page
        const swaggerUi = document.querySelector('.swagger-ui');
        if (swaggerUi && swaggerUi.parentNode) {
            swaggerUi.parentNode.insertBefore(container, swaggerUi);
            console.log('Role switcher added to page');
            
            // Default to Admin if no role is selected
            const currentRole = localStorage.getItem('currentRole');
            if (!currentRole) {
                console.log('No role found, defaulting to Admin');
                switchRole('Admin');
            } else {
                console.log('Current role from localStorage:', currentRole);
                updateRoleDisplay();
            }
        }
    }

    function addRoleButton(container, role) {
        console.log('Adding button for role:', role);
        const button = document.createElement('button');
        button.innerText = role;
        button.style.margin = '0 5px 5px 0';
        button.style.padding = '8px 15px';
        button.style.borderRadius = '4px';
        button.style.border = '1px solid #ccc';
        button.style.backgroundColor = '#fff';
        button.style.cursor = 'pointer';
        button.style.fontWeight = 'bold';
        
        // Check if this is the current role
        const currentRole = localStorage.getItem('currentRole');
        if (currentRole === role) {
            button.style.backgroundColor = '#4CAF50';
            button.style.color = 'white';
            button.style.borderColor = '#4CAF50';
        }
        
        button.onclick = function() {
            console.log('Button clicked for role:', role);
            switchRole(role);
        };
        
        container.appendChild(button);
        return button;
    }

    function switchRole(role) {
        console.log('Switching to role:', role);
        
        // Set default parameters based on role
        let employeeId = null;
        let reviewerId = null;
        
        if (role === 'Employee') {
            employeeId = 1;
        } else if (role === 'Reviewer') {
            reviewerId = 2;
        }
        
        // Build the URL with all parameters
        let url = '/api/v1/TestAuth/token?role=' + encodeURIComponent(role);
        if (employeeId) {
            url += '&employeeId=' + encodeURIComponent(employeeId);
        }
        if (reviewerId) {
            url += '&reviewerId=' + encodeURIComponent(reviewerId);
        }
        
        console.log('Fetching token from URL:', url);

        // Fetch the token
        fetch(url)
            .then(response => {
                console.log('Token fetch response status:', response.status);
                if (!response.ok) {
                    throw new Error('Failed to get token: ' + response.status);
                }
                return response.json();
            })
            .then(data => {
                console.log('Token fetch successful, token starts with:', data.token.substring(0, 20) + '...');
                
                // Store token and role info
                localStorage.setItem('authToken', data.token);
                localStorage.setItem('currentRole', role);
                
                // Update UI
                updateRoleDisplay();
                updateButtonStyles(role);
                
                // Show success message
                showMessage('Now using role: ' + role);
                
                // Apply token to subsequent requests
                applyToken(data.token);
                
                // Force reload the page to ensure the new token is used
                window.location.reload();
            })
            .catch(error => {
                console.error('Error getting token:', error);
                showMessage('Error: ' + error.message, true);
            });
    }

    function updateRoleDisplay() {
        const display = document.getElementById('current-role-display');
        if (!display) return;
        
        const currentRole = localStorage.getItem('currentRole');
        const token = localStorage.getItem('authToken');
        
        if (currentRole && token) {
            display.innerText = 'Current Role: ' + currentRole;
            display.style.color = '#000';
        } else {
            display.innerText = 'No role selected';
            display.style.color = '#999';
        }
    }

    function updateButtonStyles(currentRole) {
        // Reset all buttons first
        const buttons = document.querySelectorAll('.role-switcher button');
        buttons.forEach(button => {
            button.style.backgroundColor = '#fff';
            button.style.color = '#000';
            button.style.borderColor = '#ccc';
        });
        
        // Find and highlight the current role button
        buttons.forEach(button => {
            if (button.innerText === currentRole) {
                button.style.backgroundColor = '#4CAF50';
                button.style.color = 'white';
                button.style.borderColor = '#4CAF50';
            }
        });
    }

    function applyToken(token) {
        console.log('Setting up token for API requests');
        
        // Override fetch to add Authorization header
        const originalFetch = window.fetch;
        window.fetch = function(resource, options) {
            // Only modify API requests
            if (typeof resource === 'string' && resource.includes('/api/')) {
                console.log('Adding auth token to request:', resource);
                options = options || {};
                options.headers = options.headers || {};
                options.headers['Authorization'] = 'Bearer ' + token;
            }
            return originalFetch.call(this, resource, options);
        };
        
        console.log('Fetch overridden to add auth token');
    }

    function showMessage(text, isError) {
        console.log('Showing message:', text, 'isError:', isError || false);
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

    // Initialize token from localStorage if available
    const storedToken = localStorage.getItem('authToken');
    if (storedToken) {
        console.log('Found stored token, applying to requests');
        applyToken(storedToken);
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