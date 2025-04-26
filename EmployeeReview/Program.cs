
using EmployeeReview.Api.Extensions;
using EmployeeReview.Api.Middleware;
using EmployeeReview.Application.Mappings;
using EmployeeReview.Application.Servces;
using EmployeeReview.Application.Services;
using EmployeeReview.Infrastructure.Data;
using EmployeeReview.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using static EmployeeReview.Application.Servces.EmployeeService;
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


builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

services.AddControllers();

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

// Register services
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
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = configuration["Jwt:Issuer"],
        ValidAudience = configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"])),
        ClockSkew = TimeSpan.Zero
    };
})
.AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>("ApiKey", options => { });


// Register repositories and services
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<IEmployeeService, EmployeeService>();
services.AddScoped<IPerformanceReviewService, PerformanceReviewService>();

// Rate limiting
services.AddRateLimiting(configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Review API v1"));

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
        // In a real app, you'd use a library to create a proper Word document
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

