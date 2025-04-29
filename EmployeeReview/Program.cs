
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
using static EmployeeReview.Application.Services.EmployeeService;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;
var environment = builder.Environment;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();


services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(configuration.GetConnectionString("DefaultConnection"));
});

services.AddAutoMapper(typeof(MappingProfile).Assembly);


services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

services.AddControllers();

services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<IEmployeeService, EmployeeService>();
services.AddScoped<IPerformanceReviewService, PerformanceReviewService>();
services.AddRateLimiting(configuration);
// In development mode, use mock service with role parameter for testing
if (environment.IsDevelopment())
{
    services.AddScoped<ISecretManagementService>(provider =>
        new SecretManagementService(environment.EnvironmentName, "Admin"));
}
else
{
    services.AddScoped<ISecretManagementService>(provider =>
        new SecretManagementService(environment.EnvironmentName, "Reader"));
}

var serviceProvider = services.BuildServiceProvider();
var secretService = serviceProvider.GetRequiredService<ISecretManagementService>();
var jwtSecret = secretService.GetSecretAsync("JwtSecret").GetAwaiter().GetResult();

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
    if (environment.IsDevelopment())
    {
        // Create a JWT handler to generate a development token
        var jwtHandler = new JwtHandler(
            secretService,
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            60 // expiry in minutes
        );

        // Generate a test token
        var token = jwtHandler.GenerateTokenAsync("test-admin", UserRoles.Admin).GetAwaiter().GetResult();

        // Add global security requirement with the token
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                new[] { "Bearer " + token }
            }
        });

        Console.WriteLine($"Dev mode: Auto-generated JWT token for Swagger: {token.Substring(0, 20)}...");
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

// Debug output to verify configuration
Console.WriteLine($"JWT Secret from SecretService: {jwtSecret?.Substring(0, Math.Min(10, jwtSecret?.Length ?? 0))}...");
Console.WriteLine($"JWT Issuer: {configuration["Jwt:Issuer"]}");
Console.WriteLine($"JWT Audience: {configuration["Jwt:Audience"]}");


var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Review API v1"));

    // Get the JWT handler and generate a token
    using (var scope = app.Services.CreateScope())
    {
        var jwtHandler = scope.ServiceProvider.GetRequiredService<JwtHandler>();
        var token = jwtHandler.GenerateTokenAsync("test-admin", UserRoles.Admin).GetAwaiter().GetResult();

        // Add development authentication middleware
        app.Use(async (context, next) =>
        {
            // Only add auth header if not already present
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Request.Headers.Add("Authorization", $"Bearer {token}");
            }

            await next();
        });

        Console.WriteLine($"Dev middleware: Added JWT authorization to all requests");
    }
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
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Global exception handler middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseHttpsRedirection();

// Enable Serilog request logging
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// Use rate limiting
app.UseRateLimiter();

app.MapControllers();

// Seed the database in development mode
if (app.Environment.IsDevelopment())
{
    // Run database migration and seeding
    await DatabaseSeeder.SeedDatabase(app);
}

app.Run();

// Add this at the bottom of Program.cs
public class SecurityRequirementsOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        // Check for authorize attribute
        var hasAuthorize = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Any();

        if (!hasAuthorize)
            return;

        // Initialize if null
        operation.Security ??= new List<Microsoft.OpenApi.Models.OpenApiSecurityRequirement>();

        // Add JWT bearer token security requirement
        operation.Security.Add(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });

        // Add API Key security requirement
        operation.Security.Add(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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