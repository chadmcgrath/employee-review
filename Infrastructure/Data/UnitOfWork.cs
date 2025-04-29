using EmployeeReview.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using AutoMapper;

namespace EmployeeReview.Infrastructure.Data
{
    

    public interface IUnitOfWork : IDisposable
    {
        IEmployeeRepository EmployeeRepository { get; }
        IPerformanceReviewRepository PerformanceReviewRepository { get; }

        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
    
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _dbContext;
        private readonly IMapper _mapper;
        private IDbContextTransaction _transaction;

        private IEmployeeRepository _employeeRepository;
        private IPerformanceReviewRepository _performanceReviewRepository;

        public UnitOfWork(AppDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public IEmployeeRepository EmployeeRepository =>
            _employeeRepository ??= new EmployeeRepository(_dbContext, _mapper);

        public IPerformanceReviewRepository PerformanceReviewRepository =>
            _performanceReviewRepository ??= new PerformanceReviewRepository(_dbContext, _mapper);

        public async Task<int> CompleteAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _dbContext.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                await _dbContext.SaveChangesAsync();
                await _transaction.CommitAsync();
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }

        public async Task RollbackTransactionAsync()
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}