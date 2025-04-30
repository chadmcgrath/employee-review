using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace EmployeeReview.Tests.Helpers
{
    public static class TestEntityFactory
    {
        // Helper method to set private Id property
        private static void SetEntityId<T>(T entity, int id)
        {
            PropertyInfo property = typeof(T).GetProperty("Id",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            property?.SetValue(entity, id);
        }

        // Create an Employee with Id set
        public static Employee CreateEmployee(
            int id,
            string name = "Test Employee",
            string email = "test@example.com",
            string department = "IT",
            DateTime? dateOfJoining = null,
            bool isActive = true)
        {
            var employee = new Employee(
                name,
                email,
                department,
                dateOfJoining ?? DateTime.Now.AddYears(-1)
            );

            SetEntityId(employee, id);

            if (!isActive)
            {
                employee.SoftDelete();
            }

            return employee;
        }

        // Create a PerformanceReview with Id set
        public static PerformanceReview CreatePerformanceReview(
            int id,
            int employeeId,
            int reviewerId,
            DateTime? reviewDate = null,
            double score = 4.0,
            string comments = "Good performance")
        {
            var review = new PerformanceReview(
                employeeId,
                reviewerId,
                reviewDate ?? DateTime.Now.AddMonths(-1),
                score,
                comments
            );

            SetEntityId(review, id);

            return review;
        }

        // Create Employee DTO
        public static EmployeeDto CreateEmployeeDto(
            int id,
            string name = "Test Employee",
            string email = "test@example.com",
            string department = "IT",
            DateTime? dateOfJoining = null)
        {
            return new EmployeeDto
            {
                Id = id,
                Name = name,
                Email = email,
                Department = department,
                DateOfJoining = dateOfJoining ?? DateTime.Now.AddYears(-1)
            };
        }

        // Create Performance Review DTO
        public static PerformanceReviewDto CreatePerformanceReviewDto(
            int id,
            int employeeId,
            int reviewerId,
            string employeeName = "Test Employee",
            string reviewerName = "Test Reviewer",
            DateTime? reviewDate = null,
            double score = 4.0,
            string comments = "Good performance")
        {
            return new PerformanceReviewDto
            {
                Id = id,
                EmployeeId = employeeId,
                ReviewerId = reviewerId,
                EmployeeName = employeeName,
                ReviewerName = reviewerName,
                ReviewDate = reviewDate ?? DateTime.Now.AddMonths(-1),
                Score = score,
                Comments = comments
            };
        }

        // Create Performance Analytics DTOs
        public static PerformanceAnalyticsDto CreatePerformanceAnalyticsDto()
        {
            return new PerformanceAnalyticsDto
            {
                DepartmentPerformance = new List<DepartmentPerformanceDto>
                {
                    new DepartmentPerformanceDto { Department = "IT", AverageScore = 4.5 },
                    new DepartmentPerformanceDto { Department = "HR", AverageScore = 4.2 }
                },
                TopPerformers = new List<TopPerformerDto>
                {
                    new TopPerformerDto { EmployeeId = 1, Name = "John Doe", Department = "IT", AverageScore = 4.9 },
                    new TopPerformerDto { EmployeeId = 2, Name = "Jane Smith", Department = "HR", AverageScore = 4.7 }
                },
                MonthlyTrend = new List<MonthlyPerformanceTrendDto>
                {
                    new MonthlyPerformanceTrendDto { Month = "2023-01", AverageScore = 4.0, Year = 2023, MonthNumber = 1 },
                    new MonthlyPerformanceTrendDto { Month = "2023-02", AverageScore = 4.2, Year = 2023, MonthNumber = 2 }
                }
            };
        }

        public static PaginatedListDto<T> CreatePaginatedListDto<T>(List<T> items, int pageIndex = 1, int totalCount = 0) where T : class
        {
            totalCount = totalCount > 0 ? totalCount : items.Count;

            return new PaginatedListDto<T>
            {
                PageIndex = pageIndex,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / 10.0), // Assuming page size of 10
                Items = items
            };
        }
    }
}