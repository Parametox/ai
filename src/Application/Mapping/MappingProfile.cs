using AutoMapper;
using DataAccess.Entities;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Order mappings
        CreateMap<Order, OrderDto>();
        CreateMap<Order, OrderListItemDto>()
            .ForMember(dest => dest.ProductFormatName, opt => opt.MapFrom(src => src.ProductFormat.Name));

        // Project mappings
        CreateMap<Project, ProjectDto>();
        CreateMap<Project, ProjectListItemDto>()
            .ForMember(dest => dest.OrderNumber, opt => opt.MapFrom(src => src.Order.OrderNumber))
            .ForMember(dest => dest.DueDate, opt => opt.MapFrom(src => src.Order.DueDate));

        // Batch mappings
        CreateMap<Batch, BatchDto>();
        CreateMap<Batch, ProjectBatchSummaryDto>()
            .ForMember(dest => dest.ProgressPercent, opt => opt.MapFrom(src => ProgressPercentFromStage(src.Stage)));

        CreateMap<Batch, KanbanBatchDto>()
            .ForMember(dest => dest.BatchId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.ProjectNumber, opt => opt.MapFrom(src => src.Project.ProjectNumber))
            .ForMember(dest => dest.OrderNumber, opt => opt.MapFrom(src => src.Project.Order.OrderNumber))
            .ForMember(dest => dest.DueDate, opt => opt.MapFrom(src => src.Project.Order.DueDate))
            .ForMember(dest => dest.ProgressPercent, opt => opt.MapFrom(src => ProgressPercentFromStage(src.Stage)));

        // ProductFormat mappings
        CreateMap<ProductFormat, ProductFormatDto>();
        CreateMap<ProductFormat, ProductFormatLookupDto>();

        // BatchSplitRule mappings
        CreateMap<BatchSplitRule, BatchSplitRuleDto>();

        // BatchAuditLog mappings
        CreateMap<BatchAuditLog, BatchAuditEventDto>();
    }

    private static int ProgressPercentFromStage(DataAccess.Enums.ProductionStage stage)
        => stage switch
        {
            DataAccess.Enums.ProductionStage.Design => 20,
            DataAccess.Enums.ProductionStage.Print => 40,
            DataAccess.Enums.ProductionStage.Cut => 60,
            DataAccess.Enums.ProductionStage.Pack => 80,
            DataAccess.Enums.ProductionStage.Ship => 100,
            _ => 0
        };
}
