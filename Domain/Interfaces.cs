
using EmployeeReview.Domain.Entities;
using System.Linq.Expressions;


namespace EmployeeReview.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate);
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
                                        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                        string includeString = null,
                                        bool disableTracking = true);
        Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate = null,
                                       Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
                                       List<Expression<Func<T, object>>> includes = null,
                                       bool disableTracking = true);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<int> CountAsync(Expression<Func<T, bool>> predicate = null);
    }



    public interface IEmployeeRepository : IRepository<Employee>
    {
        Task<IEnumerable<Employee>> GetEmployeesByDepartmentAsync(string department);
        Task<IEnumerable<Employee>> SearchEmployeesByNameOrEmailAsync(string searchTerm);
        Task<IEnumerable<Employee>> GetEmployeesWithPaginationAsync(int pageNumber, int pageSize);
    }




    public interface IPerformanceReviewRepository : IRepository<PerformanceReview>
    {
        Task<IEnumerable<PerformanceReview>> GetReviewsByEmployeeIdAsync(int employeeId);
        Task<double> GetAverageScoreByDepartmentAsync(string department);
        Task<IEnumerable<Employee>> GetTopPerformingEmployeesAsync(int count);
        Task<Dictionary<string, double>> GetMonthlyPerformanceTrendAsync();
    }



    public interface IUnitOfWork : IDisposable
    {
        IEmployeeRepository EmployeeRepository { get; }
        IPerformanceReviewRepository PerformanceReviewRepository { get; }
        Task<int> CompleteAsync();
    }
}
