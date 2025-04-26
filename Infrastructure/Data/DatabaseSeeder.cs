using EmployeeReview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EmployeeReview.Infrastructure.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedDatabase(IHost app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<AppDbContext>();
            var env = services.GetRequiredService<IHostEnvironment>();

            // Only seed in development environment
            if (!env.IsDevelopment())
                return;

            // Apply migrations
            await context.Database.MigrateAsync();

            // Check if database already has data
            if (await context.Employees.AnyAsync())
                return;

            // Seed employees and reviews
            await SeedEmployeesAndReviews(context);
        }

        private static async Task SeedEmployeesAndReviews(AppDbContext context)
        {
            // Departments for random assignment
            var departments = new[] { "HR", "IT", "Finance", "Marketing", "Sales", "Operations", "R&D", "Customer Service", "Legal", "Administration" };
            var random = new Random();

            // Generate 50 employees
            var employees = new List<Employee>();
            for (int i = 1; i <= 50; i++)
            {
                var joiningDate = DateTime.Now.AddDays(-random.Next(30, 1825)); // Random date between 1 month and 5 years ago
                var department = departments[random.Next(departments.Length)];

                var employee = new Employee(
                    name: $"Employee {i}",
                    email: $"employee{i}@company.com",
                    department: department,
                    dateOfJoining: joiningDate
                );

                employees.Add(employee);
            }

            // Add employees to context
            await context.Employees.AddRangeAsync(employees);
            await context.SaveChangesAsync();

            // Generate 222 reviews
            var reviews = new List<PerformanceReview>();
            for (int i = 1; i <= 222; i++)
            {
                // Pick a random employee
                var employee = employees[random.Next(employees.Count)];

                // Pick a random reviewer (different from employee)
                Employee reviewer;
                do
                {
                    reviewer = employees[random.Next(employees.Count)];
                } while (reviewer.Id == employee.Id);

                // Random review date after both employee and reviewer join dates
                var latestJoinDate = new[] { employee.DateOfJoining, reviewer.DateOfJoining }.Max();
                var reviewDate = latestJoinDate.AddDays(random.Next(30, 365));

                // Ensure review date is not in the future
                if (reviewDate > DateTime.Now)
                    reviewDate = DateTime.Now.AddDays(-random.Next(1, 30));

                // Random score between 1.0 and 5.0
                double score = Math.Round(random.NextDouble() * 4.0 + 1.0, 1);

                var review = new PerformanceReview(
                    employeeId: employee.Id,
                    reviewerId: reviewer.Id,
                    reviewDate: reviewDate,
                    score: score,
                    comments: GenerateRandomComment(score)
                );

                reviews.Add(review);
            }

            // Add reviews to context
            await context.PerformanceReviews.AddRangeAsync(reviews);
            await context.SaveChangesAsync();
        }

        private static string GenerateRandomComment(double score)
        {
            // Generate comments based on score range
            var excellentComments = new[]
            {
                "Consistently exceeds expectations and delivers exceptional results.",
                "Outstanding performer who takes initiative and drives team success.",
                "Demonstrates exceptional leadership qualities and technical expertise.",
                "Top performer who consistently delivers high-quality work ahead of schedule.",
                "Exceptional collaborator who elevates the entire team's performance."
            };

            var goodComments = new[]
            {
                "Consistently meets and often exceeds expectations.",
                "Reliable team member who produces quality work.",
                "Shows strong initiative and good problem-solving skills.",
                "Communicates effectively and works well with the team.",
                "Demonstrates solid technical skills and commitment to growth."
            };

            var averageComments = new[]
            {
                "Meets most expectations and delivers acceptable work.",
                "Shows potential but has areas for improvement.",
                "Performs adequately on assigned tasks but could show more initiative.",
                "Works well with guidance but could be more proactive.",
                "Satisfactory performance with moderate technical skills."
            };

            var belowAverageComments = new[]
            {
                "Struggles to meet some expectations and needs improvement.",
                "Requires additional support and training in core responsibilities.",
                "Works hard but needs to improve quality and attention to detail.",
                "Would benefit from more structured guidance and feedback.",
                "Shows potential but inconsistent in delivering results."
            };

            var poorComments = new[]
            {
                "Consistently falls short of expectations and requires significant improvement.",
                "Needs immediate performance improvement plan to address deficiencies.",
                "Struggles with basic job requirements and meeting deadlines.",
                "Communication issues impact team collaboration and project outcomes.",
                "Performance concerns need to be addressed promptly for continued employment."
            };

            var random = new Random();
            if (score >= 4.5) return excellentComments[random.Next(excellentComments.Length)];
            if (score >= 3.5) return goodComments[random.Next(goodComments.Length)];
            if (score >= 2.5) return averageComments[random.Next(averageComments.Length)];
            if (score >= 1.5) return belowAverageComments[random.Next(belowAverageComments.Length)];
            return poorComments[random.Next(poorComments.Length)];
        }
    }
}