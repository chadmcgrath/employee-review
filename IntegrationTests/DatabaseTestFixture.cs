using EmployeeReview.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeReview.IntegrationTests.Fixtures
{

    public class TestDatabaseFixture : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        public DbContextOptions<AppDbContext> ContextOptions { get; }

        public TestDatabaseFixture()
        {
            var serviceCollection = new ServiceCollection();

            // Create a unique database name for this test run to avoid conflicts
            string dbName = $"EmployeeReview_Test_{Guid.NewGuid()}";
            var descriptors = serviceCollection.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)).ToList();
            foreach (var descriptor in descriptors)
            {
                serviceCollection.Remove(descriptor);
            }
            // Configure the in-memory database
            ContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .EnableSensitiveDataLogging()
                .Options;

            // Add the DbContext to the services
            serviceCollection.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            _serviceProvider = serviceCollection.BuildServiceProvider();

            // Initialize the database with seed data
            using var context = new AppDbContext(ContextOptions);
            SeedDatabase(context);
        }

        private void SeedDatabase(AppDbContext context)
        {
            // Clear any existing data
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            // Add test data here
            // You can create methods to add specific entities or scenarios
            SeedEmployees(context);
            SeedReviews(context);

            context.SaveChanges();
        }

        private void SeedEmployees(AppDbContext context)
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
        }

        private void SeedReviews(AppDbContext context)
        {
            // Get the employees (assuming they've been seeded)
            var employees = context.Employees.ToList();
            if (employees.Count < 3) return; // Safety check

            // Create some reviews
            var reviews = new[]
            {
                new Domain.Entities.PerformanceReview(
                    employees[0].Id, employees[1].Id, DateTime.Now.AddMonths(-1), 4.5, "Excellent work on the project"),

                new Domain.Entities.PerformanceReview(
                    employees[0].Id, employees[2].Id, DateTime.Now.AddMonths(-3), 4.0, "Good teamwork"),

                new Domain.Entities.PerformanceReview(
                    employees[1].Id, employees[2].Id, DateTime.Now.AddMonths(-2), 4.7, "Outstanding leadership")
            };

            context.PerformanceReviews.AddRange(reviews);
            context.SaveChanges();
        }

        public AppDbContext CreateContext()
        {
            return new AppDbContext(ContextOptions);
        }

        public void Dispose()
        {
            // Cleanup resources
            _serviceProvider.Dispose();
        }
    }
}