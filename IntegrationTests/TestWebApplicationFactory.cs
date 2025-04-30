using EmployeeReview.Api;
using EmployeeReview.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System;
using System.Linq;

namespace EmployeeReview.IntegrationTests
{
    public class SimpleTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace the database with in-memory
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                // Create a database and seed it
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();

                SeedData(db);
            });
        }

        private void SeedData(AppDbContext db)
        {
            // Add sample employees
            if (!db.Employees.Any())
            {
                db.Employees.AddRange(
                    new Domain.Entities.Employee("John Doe", "john@example.com", "Engineering", DateTime.Now.AddYears(-2)),
                    new Domain.Entities.Employee("Jane Smith", "jane@example.com", "HR", DateTime.Now.AddYears(-3))
                );
                db.SaveChanges();
            }

            // Add a few reviews if needed
            if (!db.PerformanceReviews.Any() && db.Employees.Count() >= 2)
            {
                var employees = db.Employees.ToList();
                db.PerformanceReviews.Add(
                    new Domain.Entities.PerformanceReview(
                        employees[0].Id,
                        employees[1].Id,
                        DateTime.Now.AddMonths(-1),
                        4.5,
                        "Great work!"
                    )
                );
                db.SaveChanges();
            }
        }
    }
}