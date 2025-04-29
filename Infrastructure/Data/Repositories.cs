using AutoMapper;
using EmployeeReview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EmployeeReview.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Generic repository interface defining common data access operations
    /// </summary>
    public interface IRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            List<Expression<Func<T, object>>> includes = null,
            bool disableTracking = true);
        Task<int> CountAsync(Expression<Func<T, bool>> predicate = null);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(int id);
        Task SaveChangesAsync();
    }

    /// <summary>
    /// Employee repository interface for employee-specific operations
    /// </summary>
    public interface IEmployeeRepository : IRepository<Employee>
    {
        Task<IEnumerable<Employee>> GetEmployeesByDepartmentAsync(string department);
        Task<IEnumerable<Employee>> SearchEmployeesByNameOrEmailAsync(string searchTerm);
        Task<IEnumerable<Employee>> GetEmployeesWithPaginationAsync(int pageNumber, int pageSize);
    }

    /// <summary>
    /// Performance review repository interface for review-specific operations
    /// </summary>
    public interface IPerformanceReviewRepository : IRepository<PerformanceReview>
    {
        Task<IEnumerable<PerformanceReview>> GetReviewsByEmployeeIdAsync(int employeeId);
        Task<double> GetAverageScoreByDepartmentAsync(string department);
        Task<IEnumerable<Employee>> GetTopPerformingEmployeesAsync(int count);
        Task<Dictionary<string, double>> GetMonthlyPerformanceTrendAsync();
    }

    /// <summary>
    /// Generic repository implementation with common data access operations
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _dbContext;

        public Repository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public virtual async Task<T> GetByIdAsync(int id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>().ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().Where(predicate).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            List<Expression<Func<T, object>>> includes = null,
            bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();

            if (disableTracking)
                query = query.AsNoTracking();

            if (includes != null)
                query = includes.Aggregate(query, (current, include) => current.Include(include));

            if (predicate != null)
                query = query.Where(predicate);

            if (orderBy != null)
                return await orderBy(query).ToListAsync();

            return await query.ToListAsync();
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null)
        {
            if (predicate == null)
                return await _dbContext.Set<T>().CountAsync();
            else
                return await _dbContext.Set<T>().CountAsync(predicate);
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            await _dbContext.Set<T>().AddAsync(entity);
            return entity;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _dbContext.Entry(entity).State = EntityState.Modified;
        }

        public virtual async Task DeleteAsync(int id)
        {
            var entity = await _dbContext.Set<T>().FindAsync(id);
            if (entity == null)
                throw new KeyNotFoundException($"Entity with ID {id} not found");

            _dbContext.Set<T>().Remove(entity);
        }

        public virtual async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Employee repository implementation with employee-specific operations
    /// </summary>
    public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
    {
        private readonly IMapper _mapper;

        public EmployeeRepository(AppDbContext dbContext, IMapper mapper) : base(dbContext)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<IEnumerable<Employee>> GetEmployeesByDepartmentAsync(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
                throw new ArgumentException("Department cannot be null or empty", nameof(department));

            return await GetAsync(
                predicate: e => e.Department == department && e.IsActive,
                orderBy: q => q.OrderBy(e => e.Name)
            );
        }

        public async Task<IEnumerable<Employee>> SearchEmployeesByNameOrEmailAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

            return await GetAsync(
                predicate: e => (e.Name.Contains(searchTerm) || e.Email.Contains(searchTerm)) && e.IsActive
            );
        }

        public async Task<IEnumerable<Employee>> GetEmployeesWithPaginationAsync(int pageNumber, int pageSize)
        {
            if (pageNumber <= 0)
                throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));

            if (pageSize <= 0)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

            return await _dbContext.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }

    /// <summary>
    /// Performance review repository implementation with review-specific operations
    /// </summary>
    public class PerformanceReviewRepository : Repository<PerformanceReview>, IPerformanceReviewRepository
    {
        private readonly IMapper _mapper;

        public PerformanceReviewRepository(AppDbContext dbContext, IMapper mapper) : base(dbContext)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<IEnumerable<PerformanceReview>> GetReviewsByEmployeeIdAsync(int employeeId)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Employee ID must be greater than 0", nameof(employeeId));

            return await GetAsync(
                predicate: r => r.EmployeeId == employeeId,
                orderBy: q => q.OrderByDescending(r => r.ReviewDate),
                includes: new List<Expression<Func<PerformanceReview, object>>> { r => r.Reviewer });
        }

        public async Task<double> GetAverageScoreByDepartmentAsync(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
                throw new ArgumentException("Department cannot be null or empty", nameof(department));

            var result = await _dbContext.PerformanceReviews
                .Include(r => r.Employee)
                .Where(r => r.Employee.Department == department && r.Employee.IsActive)
                .AverageAsync(r => r.Score);

            return result;
        }

        public async Task<IEnumerable<Employee>> GetTopPerformingEmployeesAsync(int count)
        {
            if (count <= 0)
                throw new ArgumentException("Count must be greater than 0", nameof(count));

            // Group reviews by employee and calculate average scores
            var topEmployees = await _dbContext.PerformanceReviews
                .Include(r => r.Employee)
                .Where(r => r.Employee.IsActive)
                .GroupBy(r => new { r.EmployeeId, r.Employee.Name, r.Employee.Department })
                .Select(g => new
                {
                    EmployeeId = g.Key.EmployeeId,
                    Name = g.Key.Name,
                    Department = g.Key.Department,
                    AverageScore = g.Average(r => r.Score)
                })
                .OrderByDescending(x => x.AverageScore)
                .Take(count)
                .ToListAsync();

            // Fetch the actual employee entities
            var employeeIds = topEmployees.Select(e => e.EmployeeId).ToList();
            return await _dbContext.Employees
                .Where(e => employeeIds.Contains(e.Id))
                .ToListAsync();
        }

        public async Task<Dictionary<string, double>> GetMonthlyPerformanceTrendAsync()
        {
            var oneYearAgo = DateTime.Now.AddYears(-1);

            var monthlyAverages = await _dbContext.PerformanceReviews
                .Where(r => r.ReviewDate >= oneYearAgo)
                .GroupBy(r => new { r.ReviewDate.Year, r.ReviewDate.Month })
                .Select(g => new
                {
                    YearMonth = $"{g.Key.Year}-{g.Key.Month:D2}",
                    AverageScore = g.Average(r => r.Score)
                })
                .OrderBy(x => x.YearMonth)
                .ToDictionaryAsync(k => k.YearMonth, v => v.AverageScore);

            return monthlyAverages;
        }

        public override async Task<PerformanceReview> AddAsync(PerformanceReview review)
        {
            if (review == null)
                throw new ArgumentNullException(nameof(review));

            if (review.Score < 1 || review.Score > 5)
                throw new ArgumentOutOfRangeException(nameof(review.Score), "Performance score must be between 1 and 5");

            // Check if employee exists
            var employee = await _dbContext.Employees.FindAsync(review.EmployeeId);
            if (employee == null)
                throw new KeyNotFoundException($"Employee with ID {review.EmployeeId} not found");

            return await base.AddAsync(review);
        }
    }
}