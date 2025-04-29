
using AutoMapper;
using EmployeeReview.Contracts.DTOs;
using EmployeeReview.Domain.Entities;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EmployeeReview.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Employee, EmployeeDto>();
            CreateMap<PerformanceReview, PerformanceReviewDto>()
                .ForMember(dest => dest.ReviewerName, opt => opt.MapFrom(src => src.Reviewer.Name));

            // DTO to Entity mappings
            CreateMap<CreateEmployeeDto, Employee>();
            CreateMap<UpdateEmployeeDto, Employee>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
            CreateMap<CreatePerformanceReviewDto, PerformanceReview>();
            CreateMap<UpdatePerformanceReviewDto, PerformanceReview>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}