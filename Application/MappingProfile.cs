
using AutoMapper;
using EmployeeReview.Application.DTOs;
using EmployeeReview.Domain.Entities;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EmployeeReview.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Employee, EmployeeDto>();
            CreateMap<CreateEmployeeDto, Employee>();
            CreateMap<UpdateEmployeeDto, Employee>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<PerformanceReview, PerformanceReviewDto>()
                .ForMember(dest => dest.ReviewerName, opt => opt.MapFrom(src => src.Reviewer.Name));
            CreateMap<CreatePerformanceReviewDto, PerformanceReview>();
            CreateMap<UpdatePerformanceReviewDto, PerformanceReview>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Analytics mappings
            CreateMap<(string Department, double AverageScore), DepartmentPerformanceDto>()
                .ForMember(dest => dest.Department, opt => opt.MapFrom(src => src.Department))
                .ForMember(dest => dest.AverageScore, opt => opt.MapFrom(src => src.AverageScore));

            CreateMap<(int EmployeeId, string Name, string Department, double AverageScore), TopPerformerDto>()
                .ForMember(dest => dest.EmployeeId, opt => opt.MapFrom(src => src.EmployeeId))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Department, opt => opt.MapFrom(src => src.Department))
                .ForMember(dest => dest.AverageScore, opt => opt.MapFrom(src => src.AverageScore));

            CreateMap<(string Month, double AverageScore), MonthlyPerformanceTrendDto>()
                .ForMember(dest => dest.Month, opt => opt.MapFrom(src => src.Month))
                .ForMember(dest => dest.AverageScore, opt => opt.MapFrom(src => src.AverageScore));
        }
    }
}