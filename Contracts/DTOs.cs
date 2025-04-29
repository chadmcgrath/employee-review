

namespace EmployeeReview.Contracts.DTOs
{
    public class EmployeeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public DateTime DateOfJoining { get; set; }
        public bool IsActive { get; set; }
    }


    public class CreateEmployeeDto
    {
        //public string Name { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public DateTime DateOfJoining { get; set; }
    }



    public class UpdateEmployeeDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
    }


    public class PerformanceReviewDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int ReviewerId { get; set; }
        public string ReviewerName { get; set; }
        public DateTime ReviewDate { get; set; }
        public double Score { get; set; }
        public string Comments { get; set; }
    }



    public class CreatePerformanceReviewDto
    {
        public int EmployeeId { get; set; }
        public int ReviewerId { get; set; }
        public DateTime ReviewDate { get; set; }
        public double Score { get; set; }
        public string Comments { get; set; }
    }



    public class UpdatePerformanceReviewDto
    {
        public DateTime ReviewDate { get; set; }
        public double Score { get; set; }
        public string Comments { get; set; }
    }


    public class PaginatedListDto<T> where T : class
    {
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public IReadOnlyList<T> Items { get; set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }


    public class DepartmentPerformanceDto
    {
        public string Department { get; set; }
        public double AverageScore { get; set; }
    }



    public class TopPerformerDto
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public double AverageScore { get; set; }
    }



    public class MonthlyPerformanceTrendDto
    {
        public string Month { get; set; }
        public double AverageScore { get; set; }

        public int Year { get; set; }
        public int MonthNumber { get; set; }
    }




    public class PerformanceAnalyticsDto
    {
        public IEnumerable<DepartmentPerformanceDto> DepartmentPerformance { get; set; }
        public IEnumerable<TopPerformerDto> TopPerformers { get; set; }
        public IEnumerable<MonthlyPerformanceTrendDto> MonthlyTrend { get; set; }
    }

}