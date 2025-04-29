
using AutoMapper;
using EmployeeReview.Application.DTOs;
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
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
            return _mapper.Map<EmployeeDto>(employee);
        }

        public async Task<PaginatedListDto<EmployeeDto>> GetEmployeesAsync(int pageNumber, int pageSize, string searchTerm = null, string department = null)
        {
            // Start with base query
            IEnumerable<Employee> employees;
            int totalCount;

            // Apply search if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                employees = await _unitOfWork.EmployeeRepository.SearchEmployeesByNameOrEmailAsync(searchTerm);
            }
            // Apply department filter if provided
            else if (!string.IsNullOrWhiteSpace(department))
            {
                employees = await _unitOfWork.EmployeeRepository.GetEmployeesByDepartmentAsync(department);
            }
            // No filters, get all
            else
            {
                employees = await _unitOfWork.EmployeeRepository.GetEmployeesWithPaginationAsync(pageNumber, pageSize);
            }

            // Get total count for pagination
            totalCount = await _unitOfWork.EmployeeRepository.CountAsync();

            // Map to DTOs
            var employeeDtos = _mapper.Map<List<EmployeeDto>>(employees);

            // Create paginated result
            var result = new PaginatedListDto<EmployeeDto>
            {
                PageIndex = pageNumber,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = employeeDtos
            };

            return result;
        }

        public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeDto employeeDto)
        {
            var employee = _mapper.Map<Employee>(employeeDto);
            var createdEmployee = await _unitOfWork.EmployeeRepository.AddAsync(employee);
            await _unitOfWork.CompleteAsync();
            return _mapper.Map<EmployeeDto>(createdEmployee);
        }

        public async Task<EmployeeDto> UpdateEmployeeAsync(int id, UpdateEmployeeDto employeeDto)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
            if (employee == null)
                return null;

            employee.Update(employeeDto.Name, employeeDto.Email, employeeDto.Department);
            await _unitOfWork.EmployeeRepository.UpdateAsync(employee);
            await _unitOfWork.CompleteAsync();

            return _mapper.Map<EmployeeDto>(employee);
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
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
            return employee != null;
        }
    }
}
    


    