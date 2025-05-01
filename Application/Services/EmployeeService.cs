using AutoMapper;
using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Domain.Entities;
using EmployeeReview.Infrastructure.Data;

namespace EmployeeReview.Application.Services
{
    public interface IEmployeeService
    {
        Task<EmployeeDto> GetEmployeeByIdAsync(int id);
        Task<PaginatedListDto<EmployeeDto>> GetEmployeesAsync(int pageNumber, int pageSize, string searchTerm = null, string department = null);
        Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeDto employeeDto);
        Task<EmployeeDto> UpdateEmployeeAsync(int id, UpdateEmployeeDto employeeDto);
        Task<bool> DeleteEmployeeAsync(int id);
        Task<bool> EmployeeExistsAsync(int id);
    }

    public class EmployeeService : IEmployeeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public EmployeeService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<EmployeeDto> GetEmployeeByIdAsync(int id)
        {
            // Using the repository method that returns DTO directly, this can return an inactive employee
            return await _unitOfWork.EmployeeRepository.GetEmployeeDtoByIdAsync(id);
        }

        public async Task<PaginatedListDto<EmployeeDto>> GetEmployeesAsync(int pageNumber, int pageSize, string searchTerm = null, string department = null)
        {
            // Apply filters to get appropriate DTOs
            IEnumerable<EmployeeDto> employeeDtos;
            int totalCount;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                employeeDtos = await _unitOfWork.EmployeeRepository.SearchEmployeeDtosByNameOrEmailAsync(searchTerm);
                totalCount = await _unitOfWork.EmployeeRepository.CountAsync(e =>
                    (e.Name.Contains(searchTerm) || e.Email.Contains(searchTerm)) && e.IsActive);
            }
            else if (!string.IsNullOrWhiteSpace(department))
            {
                employeeDtos = await _unitOfWork.EmployeeRepository.GetEmployeeDtosByDepartmentAsync(department);
                totalCount = await _unitOfWork.EmployeeRepository.CountAsync(e =>
                    e.Department == department && e.IsActive);
            }
            else
            {
                employeeDtos = await _unitOfWork.EmployeeRepository.GetEmployeeDtosWithPaginationAsync(pageNumber, pageSize);
                totalCount = await _unitOfWork.EmployeeRepository.CountAsync(e => e.IsActive);
            }

            return new PaginatedListDto<EmployeeDto>
            {
                PageIndex = pageNumber,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = employeeDtos as IReadOnlyList<EmployeeDto> ?? new List<EmployeeDto>(employeeDtos)
            };
        }

        public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeDto employeeDto)
        {
            var employee = _mapper.Map<Employee>(employeeDto);
            var createdEmployee = await _unitOfWork.EmployeeRepository.AddAsync(employee);
            await _unitOfWork.CompleteAsync();

            // Return the DTO directly from repository
            return await _unitOfWork.EmployeeRepository.GetEmployeeDtoByIdAsync(createdEmployee.Id);
        }

        public async Task<EmployeeDto> UpdateEmployeeAsync(int id, UpdateEmployeeDto employeeDto)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
            if (employee == null)
                return null;

            employee.Update(employeeDto.Name, employeeDto.Email, employeeDto.Department);
            await _unitOfWork.EmployeeRepository.UpdateAsync(employee);
            await _unitOfWork.CompleteAsync();

            // Return the DTO directly from repository
            return await _unitOfWork.EmployeeRepository.GetEmployeeDtoByIdAsync(id);
        }

        public async Task<bool> DeleteEmployeeAsync(int id)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
            if (employee == null)
                return false;

            employee.SoftDelete();
            await _unitOfWork.EmployeeRepository.UpdateAsync(employee);
            await _unitOfWork.CompleteAsync();

            return true;
        }

        public async Task<bool> EmployeeExistsAsync(int id)
        {
            // We could put an Any() in the repo
            return await _unitOfWork.EmployeeRepository.CountAsync(e => e.Id == id && e.IsActive) > 0;
        }
    }
}