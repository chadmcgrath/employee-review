using AutoMapper;
using EmployeeReview.Domain.Entities;
using EmployeeReview.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EmployeeReview.Infrastructure.Data.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(int id);
    }
    public interface IEmployeeRepository
    {
        Task<Employee> GetByIdAsync(int id);
        Task<IEnumerable<Employee>> GetAllAsync();
        Task AddAsync(Employee employee);
        Task UpdateAsync(Employee employee);
        Task DeleteAsync(int id);
    }

    public interface IPerformanceReviewRepository
    {
        Task<PerformanceReview> GetByIdAsync(int id);
        Task<IEnumerable<PerformanceReview>> GetAllAsync();
        Task AddAsync(PerformanceReview review);
        Task UpdateAsync(PerformanceReview review);
        Task DeleteAsync(int id);
    }

    public interface IUnitOfWork : IDisposable
    {
        IEmployeeRepository EmployeeRepository { get; }
        IPerformanceReviewRepository PerformanceReviewRepository { get; }

        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _dbContext;

        public Repository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<T> GetByIdAsync(int id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>().ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().Where(predicate).ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            string includeString = null,
            bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();

            if (disableTracking)
                query = query.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(includeString))
                query = query.Include(includeString);

            if (predicate != null)
                query = query.Where(predicate);

            if (orderBy != null)
                return await orderBy(query).ToListAsync();

            return await query.ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
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

        public async Task<T> AddAsync(T entity)
        {
            await _dbContext.Set<T>().AddAsync(entity);
            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            _dbContext.Entry(entity).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
            await Task.CompletedTask;
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null)
        {
            if (predicate == null)
                return await _dbContext.Set<T>().CountAsync();
            else
                return await _dbContext.Set<T>().CountAsync(predicate);
        }
    }


    public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
    {
        private readonly IMapper _mapper;

        public EmployeeRepository(AppDbContext dbContext, IMapper mapper) : base(dbContext)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<Employee>> GetEmployeesByDepartmentAsync(string department)
        {
            return await _dbContext.Employees
                .Where(e => e.Department == department && e.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> SearchEmployeesByNameOrEmailAsync(string searchTerm)
        {
            return await _dbContext.Employees
                .Where(e => (e.Name.Contains(searchTerm) || e.Email.Contains(searchTerm)) && e.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<Employee>> GetEmployeesWithPaginationAsync(int pageNumber, int pageSize)
        {
            return await _dbContext.Employees
                .Where(e => e.IsActive)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }

    public class PerformanceReviewRepository : Repository<PerformanceReview>, IPerformanceReviewRepository
    {
        private readonly IMapper _mapper;

        public PerformanceReviewRepository(AppDbContext dbContext, IMapper mapper) : base(dbContext)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<PerformanceReview>> GetReviewsByEmployeeIdAsync(int employeeId)
        {
            return await _dbContext.PerformanceReviews
                .Include(r => r.Reviewer)
                .Where(r => r.EmployeeId == employeeId)
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();
        }

        public async Task<double> GetAverageScoreByDepartmentAsync(string department)
        {
            var result = await _dbContext.PerformanceReviews
                .Include(r => r.Employee)
                .Where(r => r.Employee.Department == department && r.Employee.IsActive)
                .AverageAsync(r => r.Score);

            return result;
        }

        public async Task<IEnumerable<Employee>> GetTopPerformingEmployeesAsync(int count)
        {
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
    }
}
