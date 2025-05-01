using EmployeeReview.Api;
using EmployeeReview.Application.Services;
using EmployeeReview.Domain.Entities;
using EmployeeReview.Infrastructure.Data;
using EmployeeReview.Infrastructure.Security;
using IntegrationTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

namespace EmployeeReview.IntegrationTests
{
    public class AuthenticatedTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        // Generate a static API key for all tests
        private const string TEST_API_KEY = "integration-test-api-key";

        // Dictionary to cache tokens for different roles
        private readonly Dictionary<string, string> _tokenCache = new Dictionary<string, string>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Set testing environment
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                // DB Setup - Remove existing registrations
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }

                var optionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions));
                if (optionsDescriptor != null)
                {
                    services.Remove(optionsDescriptor);
                }

                var contextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(AppDbContext));
                if (contextDescriptor != null)
                {
                    services.Remove(contextDescriptor);
                }

                // Configure in-memory database with isolated service provider
                var serviceProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                    options.UseInternalServiceProvider(serviceProvider);
                });

                // Override the API Key handler to recognize our test key
                // We need to modify the ApiKeyAuthHandler to check for our test key
                SetupApiKeyHandling(services);

                // Replace the ISecretManagementService implementation
                // Remove existing registration first
                var secretServiceDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ISecretManagementService));
                if (secretServiceDescriptor != null)
                {
                    services.Remove(secretServiceDescriptor);
                }

                // Add our test implementation with proper admin role capitalization
                services.AddScoped<ISecretManagementService>(provider =>
                    new TestSecretManagementService("Admin"));

                // Create and seed the database
                using var scope = services.BuildServiceProvider().CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();

                SeedData(db);
            });
        }

        // Configure API Key handling for testing
        private void SetupApiKeyHandling(IServiceCollection services)
        {
            // Find and remove the existing ApiKeyAuthHandler registration
            var apiKeyHandlerDescriptor = services.SingleOrDefault(
                d => d.ServiceType.Name.Contains("IAuthenticationHandler") &&
                     d.ImplementationType?.Name == "ApiKeyAuthHandler");

            if (apiKeyHandlerDescriptor != null)
            {
                services.Remove(apiKeyHandlerDescriptor);
            }

            // Register our test version of the handler or modify the configuration

            // Method 1: Directly register a fake key in the secrets service
            // This will be used by the original ApiKeyAuthHandler to validate keys
            var secretService = services.BuildServiceProvider().GetService<ISecretManagementService>();
            if (secretService != null && secretService is TestSecretManagementService testSecretService)
            {
                // Try to store the test API key in the secret manager
                testSecretService.SetSecretAsync("ApiKey", TEST_API_KEY).GetAwaiter().GetResult();
            }

            // Method 2: Find the ApiKeyAuthHandler and use reflection to set the key
            // This is a fallback approach if the handler loads the key in a different way
            services.PostConfigure<ApiKeyAuthOptions>(options =>
            {
                // Find the ApiKey property using reflection if it exists but isn't public
                var optionsType = typeof(ApiKeyAuthOptions);
                var apiKeyProperty = optionsType.GetProperty("ApiKey",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (apiKeyProperty != null)
                {
                    apiKeyProperty.SetValue(options, TEST_API_KEY);
                }
                else
                {
                    // Try to add a field
                    var apiKeyField = optionsType.GetField("_apiKey",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (apiKeyField != null)
                    {
                        apiKeyField.SetValue(options, TEST_API_KEY);
                    }
                    else
                    {
                        Console.WriteLine("[WARNING] Could not set API Key through reflection. Authentication with API key might fail.");
                    }
                }
            });
        }

        // Create an authenticated client with JWT token
        public HttpClient CreateAuthenticatedClient(string userRole = UserRoles.Admin)
        {
            var client = CreateClient();

            // Cache based on exact role string to prevent case issues
            if (!_tokenCache.TryGetValue(userRole, out string token))
            {
                using var scope = Services.CreateScope();
                var jwtHandler = scope.ServiceProvider.GetRequiredService<JwtHandler>();

                // Generate token (don't change the role's case)
                token = jwtHandler.GenerateTokenAsync("test-user", userRole).GetAwaiter().GetResult();
                _tokenCache[userRole] = token;

                Console.WriteLine($"Generated JWT token for role '{userRole}': {token.Substring(0, 20)}...");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        // Create an authenticated client with API Key
        public HttpClient CreateApiKeyClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("X-API-Key", TEST_API_KEY);

            // For debugging purposes
            Console.WriteLine($"Created client with API Key: {TEST_API_KEY}");

            return client;
        }

        private void SeedData(AppDbContext db)
        {
            // Add sample employees
            if (!db.Employees.Any())
            {
                db.Employees.AddRange(
                    new Employee("John Doe", "john@example.com", "Engineering", DateTime.Now.AddYears(-2)),
                    new Employee("Jane Smith", "jane@example.com", "HR", DateTime.Now.AddYears(-3)),
                    new Employee("Test User", "test@example.com", "QA", DateTime.Now.AddYears(-1))
                );
                db.SaveChanges();
            }

            // Add sample performance reviews
            if (!db.PerformanceReviews.Any() && db.Employees.Count() >= 2)
            {
                var employees = db.Employees.ToList();

                db.PerformanceReviews.AddRange(
                    new PerformanceReview(
                        employees[0].Id,
                        employees[1].Id,
                        DateTime.Now.AddMonths(-1),
                        4.5,
                        "Great work on the project!"
                    ),
                    new PerformanceReview(
                        employees[0].Id,
                        employees[1].Id,
                        DateTime.Now.AddMonths(-4),
                        4.0,
                        "Good performance overall, but could improve communication."
                    ),
                    new PerformanceReview(
                        employees[1].Id,
                        employees[0].Id,
                        DateTime.Now.AddMonths(-2),
                        4.8,
                        "Excellent HR support and team management."
                    )
                );
                db.SaveChanges();
            }
        }
    }
}