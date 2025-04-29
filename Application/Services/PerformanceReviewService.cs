using AutoMapper;
using EmployeeReview.Application.DTOs;
using EmployeeReview.Domain.Entities;
using EmployeeReview.Infrastructure.Data;

namespace EmployeeReview.Application.Services
{
    public interface IPerformanceReviewService
    {
        Task<PerformanceReviewDto> GetReviewByIdAsync(int id);
        Task<IEnumerable<PerformanceReviewDto>> GetReviewsByEmployeeIdAsync(int employeeId);
        Task<PerformanceReviewDto> CreateReviewAsync(CreatePerformanceReviewDto reviewDto);
        Task<PerformanceReviewDto> UpdateReviewAsync(int id, UpdatePerformanceReviewDto reviewDto);
        Task<bool> DeleteReviewAsync(int id);
        Task<PerformanceAnalyticsDto> GetPerformanceAnalyticsAsync();
    }


    public class PerformanceReviewService : IPerformanceReviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public PerformanceReviewService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<PerformanceReviewDto> GetReviewByIdAsync(int id)
        {
            var review = await _unitOfWork.PerformanceReviewRepository.GetByIdAsync(id);
            return _mapper.Map<PerformanceReviewDto>(review);
        }

        public async Task<IEnumerable<PerformanceReviewDto>> GetReviewsByEmployeeIdAsync(int employeeId)
        {
            var reviews = await _unitOfWork.PerformanceReviewRepository.GetReviewsByEmployeeIdAsync(employeeId);
            return _mapper.Map<IEnumerable<PerformanceReviewDto>>(reviews);
        }

        public async Task<PerformanceReviewDto> CreateReviewAsync(CreatePerformanceReviewDto reviewDto)
        {
            // Validate employee exists
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(reviewDto.EmployeeId);
            if (employee == null)
                throw new KeyNotFoundException($"Employee with ID {reviewDto.EmployeeId} not found");

            // Validate reviewer exists
            var reviewer = await _unitOfWork.EmployeeRepository.GetByIdAsync(reviewDto.ReviewerId);
            if (reviewer == null)
                throw new KeyNotFoundException($"Reviewer with ID {reviewDto.ReviewerId} not found");

            // Validate review date is not before employee joining date
            if (reviewDto.ReviewDate < employee.DateOfJoining)
                throw new ArgumentException("Review date cannot be before employee's joining date");

            // Validate review date is not before reviewer joining date
            if (reviewDto.ReviewDate < reviewer.DateOfJoining)
                throw new ArgumentException("Review date cannot be before reviewer's joining date");

            var review = new PerformanceReview(
                reviewDto.EmployeeId,
                reviewDto.ReviewerId,
                reviewDto.ReviewDate,
                reviewDto.Score,
                reviewDto.Comments
            );

            var createdReview = await _unitOfWork.PerformanceReviewRepository.AddAsync(review);
            await _unitOfWork.CompleteAsync();

            return _mapper.Map<PerformanceReviewDto>(createdReview);
        }

        public async Task<PerformanceReviewDto> UpdateReviewAsync(int id, UpdatePerformanceReviewDto reviewDto)
        {
            var review = await _unitOfWork.PerformanceReviewRepository.GetByIdAsync(id);
            if (review == null)
                return null;

            // Validate employee and reviewer relationships
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(review.EmployeeId);
            var reviewer = await _unitOfWork.EmployeeRepository.GetByIdAsync(review.ReviewerId);

            // Validate review date is not before employee joining date
            if (reviewDto.ReviewDate < employee.DateOfJoining)
                throw new ArgumentException("Review date cannot be before employee's joining date");

            // Validate review date is not before reviewer joining date
            if (reviewDto.ReviewDate < reviewer.DateOfJoining)
                throw new ArgumentException("Review date cannot be before reviewer's joining date");

            review.Update(reviewDto.ReviewDate, reviewDto.Score, reviewDto.Comments);
            await _unitOfWork.PerformanceReviewRepository.UpdateAsync(review);
            await _unitOfWork.CompleteAsync();

            return _mapper.Map<PerformanceReviewDto>(review);
        }

        public async Task<bool> DeleteReviewAsync(int id)
        {
            var review = await _unitOfWork.PerformanceReviewRepository.GetByIdAsync(id);
            if (review == null)
                return false;

            await _unitOfWork.PerformanceReviewRepository.DeleteAsync(review.Id);
            await _unitOfWork.CompleteAsync();
            return true;
        }

        public async Task<PerformanceAnalyticsDto> GetPerformanceAnalyticsAsync()
        {
            // Get average scores by department
            var departments = await _unitOfWork.EmployeeRepository.GetAllAsync();
            var departmentList = departments.Select(d => d.Department).Distinct().ToList();
            var departmentPerformance = new List<DepartmentPerformanceDto>();

            foreach (var department in departmentList)
            {
                var avgScore = await _unitOfWork.PerformanceReviewRepository.GetAverageScoreByDepartmentAsync(department);
                departmentPerformance.Add(new DepartmentPerformanceDto
                {
                    Department = department,
                    AverageScore = avgScore
                });
            }

            // Get top 5 performing employees
            var topPerformers = await _unitOfWork.PerformanceReviewRepository.GetTopPerformingEmployeesAsync(5);
            var topPerformerDtos = _mapper.Map<List<TopPerformerDto>>(topPerformers);

            // Get monthly performance trend
            var monthlyTrend = await _unitOfWork.PerformanceReviewRepository.GetMonthlyPerformanceTrendAsync();
            var monthlyTrendDtos = monthlyTrend.Select(mt => new MonthlyPerformanceTrendDto
            {
                Month = mt.Key,
                AverageScore = mt.Value
            }).ToList();

            return new PerformanceAnalyticsDto
            {
                DepartmentPerformance = departmentPerformance,
                TopPerformers = topPerformerDtos,
                MonthlyTrend = monthlyTrendDtos
            };
        }
    }
}
