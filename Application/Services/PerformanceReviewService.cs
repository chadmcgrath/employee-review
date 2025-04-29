using AutoMapper;
using EmployeeReview.Contracts.DTOs;
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
            // Using the repository method that returns DTO directly
            return await _unitOfWork.PerformanceReviewRepository.GetReviewDtoByIdAsync(id);
        }

        public async Task<IEnumerable<PerformanceReviewDto>> GetReviewsByEmployeeIdAsync(int employeeId)
        {
            // Using the repository method that returns DTOs directly
            return await _unitOfWork.PerformanceReviewRepository.GetReviewDtosByEmployeeIdAsync(employeeId);
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

            // Get the full DTO with related data
            return await _unitOfWork.PerformanceReviewRepository.GetReviewDtoByIdAsync(createdReview.Id);
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

            // Get the full DTO with related data
            return await _unitOfWork.PerformanceReviewRepository.GetReviewDtoByIdAsync(id);
        }

        public async Task<bool> DeleteReviewAsync(int id)
        {
            try
            {
                await _unitOfWork.PerformanceReviewRepository.DeleteAsync(id);
                await _unitOfWork.CompleteAsync();
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public async Task<PerformanceAnalyticsDto> GetPerformanceAnalyticsAsync()
        {
            // Get all departments
            var departments = await _unitOfWork.EmployeeRepository.GetAllDepartmentsAsync();

            // Get all data in parallel for better performance
            var departmentPerformanceTask = _unitOfWork.PerformanceReviewRepository.GetAverageScoresByDepartmentAsync(departments);
            var topPerformersTask = _unitOfWork.PerformanceReviewRepository.GetTopPerformingEmployeesAsync(5);
            var monthlyTrendTask = _unitOfWork.PerformanceReviewRepository.GetMonthlyPerformanceTrendAsync();

            // Wait for all tasks to complete
            await Task.WhenAll(departmentPerformanceTask, topPerformersTask, monthlyTrendTask);

            // Create and return the final analytics DTO - no manual mapping needed here
            return new PerformanceAnalyticsDto
            {
                DepartmentPerformance = departmentPerformanceTask.Result,
                TopPerformers = topPerformersTask.Result,
                MonthlyTrend = monthlyTrendTask.Result
            };
        }
    }
}