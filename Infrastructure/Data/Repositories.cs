using AutoMapper;
using AutoMapper.QueryableExtensions;
using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;


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
        Task<EmployeeDto> GetEmployeeDtoByIdAsync(int id);
        Task<IEnumerable<EmployeeDto>> GetEmployeeDtosByDepartmentAsync(string department);
        Task<IEnumerable<EmployeeDto>> SearchEmployeeDtosByNameOrEmailAsync(string searchTerm);
        Task<IEnumerable<EmployeeDto>> GetEmployeeDtosWithPaginationAsync(int pageNumber, int pageSize);
        Task<List<string>> GetAllDepartmentsAsync();
    }

    /// <summary>
    /// Performance review repository interface for review-specific operations
    /// </summary>
    public interface IPerformanceReviewRepository : IRepository<PerformanceReview>
    {
        Task<PerformanceReviewDto> GetReviewDtoByIdAsync(int id);
        Task<IEnumerable<PerformanceReviewDto>> GetReviewDtosByEmployeeIdAsync(int employeeId);
        Task<List<DepartmentPerformanceDto>> GetAverageScoresByDepartmentAsync(List<string> departments);
        Task<List<TopPerformerDto>> GetTopPerformingEmployeesAsync(int count);
        Task<List<MonthlyPerformanceTrendDto>> GetMonthlyPerformanceTrendAsync();
    }

    /// <summary>
    /// Generic repository implementation with common data access operations
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _dbContext;
        protected readonly IMapper _mapper;
        protected readonly IConfigurationProvider _mapperConfig;

        public Repository(AppDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _mapperConfig = mapper.ConfigurationProvider;
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
            return await _dbContext.Set<T>()
                .Where(predicate)
                .ToListAsync();
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
                query = orderBy(query);

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
        public EmployeeRepository(AppDbContext dbContext, IMapper mapper)
            : base(dbContext, mapper)
        {
        }

        public async Task<EmployeeDto> GetEmployeeDtoByIdAsync(int id)
        {
            return await _dbContext.Employees
                .Where(e => e.Id == id)
                .ProjectTo<EmployeeDto>(_mapperConfig)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<EmployeeDto>> GetEmployeeDtosByDepartmentAsync(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
                throw new ArgumentException("Department cannot be null or empty", nameof(department));

            return await _dbContext.Employees
                .Where(e => e.Department == department && e.IsActive)
                .OrderBy(e => e.Name)
                .ProjectTo<EmployeeDto>(_mapperConfig)
                .ToListAsync();
        }

        public async Task<IEnumerable<EmployeeDto>> SearchEmployeeDtosByNameOrEmailAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

            return await _dbContext.Employees
                .Where(e => (e.Name.Contains(searchTerm) || e.Email.Contains(searchTerm)) && e.IsActive)
                .ProjectTo<EmployeeDto>(_mapperConfig)
                .ToListAsync();
        }

        public async Task<IEnumerable<EmployeeDto>> GetEmployeeDtosWithPaginationAsync(int pageNumber, int pageSize)
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
                .ProjectTo<EmployeeDto>(_mapperConfig)
                .ToListAsync();
        }

        public async Task<List<string>> GetAllDepartmentsAsync()
        {
            return await _dbContext.Employees
                .Where(e => e.IsActive)
                .Select(e => e.Department)
                .Distinct()
                .ToListAsync();
        }
    }

    /// <summary>
    /// Performance review repository implementation with review-specific operations
    /// </summary>
    public class PerformanceReviewRepository : Repository<PerformanceReview>, IPerformanceReviewRepository
    {
        public PerformanceReviewRepository(AppDbContext dbContext, IMapper mapper)
            : base(dbContext, mapper)
        {
        }

        public async Task<PerformanceReviewDto> GetReviewDtoByIdAsync(int id)
        {
            return await _dbContext.PerformanceReviews
                .Where(r => r.Id == id)
                .ProjectTo<PerformanceReviewDto>(_mapperConfig)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<PerformanceReviewDto>> GetReviewDtosByEmployeeIdAsync(int employeeId)
        {
            if (employeeId <= 0)
                throw new ArgumentException("Employee ID must be greater than 0", nameof(employeeId));

            return await _dbContext.PerformanceReviews
                .Where(r => r.EmployeeId == employeeId)
                .OrderByDescending(r => r.ReviewDate)
                .ProjectTo<PerformanceReviewDto>(_mapperConfig)
                .ToListAsync();
        }

        // Keep original implementation for GroupBy methods
        public async Task<List<DepartmentPerformanceDto>> GetAverageScoresByDepartmentAsync(List<string> departments)
        {
            if (departments == null || !departments.Any())
                throw new ArgumentException("Departments list cannot be null or empty", nameof(departments));

            return await _dbContext.PerformanceReviews
                .Include(r => r.Employee)
                .Where(r => departments.Contains(r.Employee.Department) && r.Employee.IsActive)
                .GroupBy(r => r.Employee.Department)
                .Select(g => new DepartmentPerformanceDto
                {
                    Department = g.Key,
                    AverageScore = g.Average(r => r.Score)
                })
                .ToListAsync();
        }

        public async Task<List<TopPerformerDto>> GetTopPerformingEmployeesAsync(int count)
        {
            if (count <= 0)
                throw new ArgumentException("Count must be greater than 0", nameof(count));

            return await _dbContext.PerformanceReviews
                .Include(r => r.Employee)
                .Where(r => r.Employee.IsActive)
                .GroupBy(r => new { r.EmployeeId, r.Employee.Name, r.Employee.Department })
                .Select(g => new TopPerformerDto
                {
                    EmployeeId = g.Key.EmployeeId,
                    Name = g.Key.Name,
                    Department = g.Key.Department,
                    AverageScore = g.Average(r => r.Score)
                })
                .OrderByDescending(x => x.AverageScore)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<MonthlyPerformanceTrendDto>> GetMonthlyPerformanceTrendAsync()
        {
            var oneYearAgo = DateTime.Now.AddYears(-1);

            return await _dbContext.PerformanceReviews
                .Where(r => r.ReviewDate >= oneYearAgo)
                .GroupBy(r => new { r.ReviewDate.Year, r.ReviewDate.Month })
                .Select(g => new MonthlyPerformanceTrendDto
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    AverageScore = g.Average(r => r.Score),
                    Year = g.Key.Year,
                    MonthNumber = g.Key.Month
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.MonthNumber)
                .ToListAsync();
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