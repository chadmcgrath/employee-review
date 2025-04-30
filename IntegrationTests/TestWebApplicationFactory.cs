using EmployeeReview.Api;
using EmployeeReview.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace EmployeeReview.IntegrationTests.Fixtures
{
    public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Find the DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    // Remove the registered DbContext
                    services.Remove(descriptor);
                }

                // Create a unique database name for this test run
                string dbName = $"EmployeeReview_IntegrationTest_{Guid.NewGuid()}";

                // Add DB context using an in-memory database for testing
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });

                // Build the service provider
                var sp = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database context
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<AppDbContext>();
                    var logger = scopedServices.GetRequiredService<ILogger<TestWebApplicationFactory<TStartup>>>();

                    // Ensure the database is created
                    db.Database.EnsureCreated();

                    try
                    {
                        // Seed the database with test data
                        SeedDatabase(db);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the database. Error: {Message}", ex.Message);
                    }
                }
            });
        }

        private void SeedDatabase(AppDbContext context)
        {
            // Add test employees
            var employees = new[]
            {
                new Domain.Entities.Employee("John Doe", "john.doe@example.com", "IT", DateTime.Now.AddYears(-2)),
                new Domain.Entities.Employee("Jane Smith", "jane.smith@example.com", "HR", DateTime.Now.AddYears(-3)),
                new Domain.Entities.Employee("Robert Johnson", "robert.johnson@example.com", "Finance", DateTime.Now.AddYears(-1))
            };

            context.Employees.AddRange(employees);
            context.SaveChanges();

            // Create some reviews
            var reviewers = context.Employees.ToList();
            if (reviewers.Count < 3) return; // Safety check

            var reviews = new[]
            {
                new Domain.Entities.PerformanceReview(
                    reviewers[0].Id, reviewers[1].Id, DateTime.Now.AddMonths(-1), 4.5, "Excellent work on the project"),

                new Domain.Entities.PerformanceReview(
                    reviewers[0].Id, reviewers[2].Id, DateTime.Now.AddMonths(-3), 4.0, "Good teamwork"),

                new Domain.Entities.PerformanceReview(
                    reviewers[1].Id, reviewers[2].Id, DateTime.Now.AddMonths(-2), 4.7, "Outstanding leadership")
            };

            context.PerformanceReviews.AddRange(reviews);
            context.SaveChanges();
        }
    }
}