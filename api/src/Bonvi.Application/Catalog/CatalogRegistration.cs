using Bonvi.Application.Catalog.Commands;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Departments;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Application.Catalog.Positions;
using Bonvi.Application.Catalog.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Catalog;

/// <summary>
/// Ma'lumotnoma use-case'larini DI konteyneriga ulash.
/// </summary>
/// <remarks>
/// Alohida faylda: <c>DependencyInjection.cs</c> loyiha o'sgan sari
/// yuzlab qatorga aylanmasligi uchun har bir mavzu o'z ro'yxatini o'zi olib boradi.
/// </remarks>
internal static class CatalogRegistration
{
    /// <summary>
    /// Ma'lumotnomalarning barcha handlerlari va ta'riflarini ro'yxatga oladi.
    /// </summary>
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        services.AddReferenceHandlers();
        services.AddReferenceDefinitions();
        services.AddDepartmentUseCases();
        services.AddPositionUseCases();

        return services;
    }

    /// <summary>
    /// Generik handler'lar — OCHIQ generik ko'rinishda, bir martadan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu ettita qator sakkizta ma'lumotnomaning yettita amalini qoplaydi:
    /// 56 ta ro'yxatga olish o'rniga 7 ta. DI konteyneri
    /// <c>ListReferenceQueryHandler&lt;LegalEntity, LegalEntityDto&gt;</c>
    /// so'ralganda uni o'zi yopib beradi.
    /// </para>
    /// <para>
    /// Natijada yangi ma'lumotnoma qo'shish bu faylning faqat
    /// <see cref="AddReferenceDefinitions"/> qismiga tegadi.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddReferenceHandlers(this IServiceCollection services)
    {
        services.AddScoped(typeof(ListReferenceQueryHandler<,>));
        services.AddScoped(typeof(GetReferenceByIdQueryHandler<,>));
        services.AddScoped(typeof(CreateReferenceCommandHandler<,>));
        services.AddScoped(typeof(UpdateReferenceCommandHandler<,>));
        services.AddScoped(typeof(DeactivateReferenceCommandHandler<>));
        services.AddScoped(typeof(ActivateReferenceCommandHandler<>));

        return services;
    }

    /// <summary>
    /// Har bir ma'lumotnomaning «shaxsiy hujjati».
    /// </summary>
    /// <remarks>
    /// Ta'riflar holatsiz (stateless) — shu sababli singleton. Ular
    /// repozitoriyga ham, so'rov kontekstiga ham bog'liq emas.
    /// </remarks>
    private static IServiceCollection AddReferenceDefinitions(this IServiceCollection services)
    {
        services.AddSingleton<IReferenceDefinition<LegalEntity, LegalEntityDto>, LegalEntityDefinition>();
        services.AddSingleton<IReferenceDefinition<Direction, DirectionDto>, DirectionDefinition>();
        services.AddSingleton<IReferenceDefinition<Department, DepartmentDto>, DepartmentDefinition>();
        services.AddSingleton<IReferenceDefinition<Position, PositionDto>, PositionDefinition>();
        services.AddSingleton<IReferenceDefinition<TerminationReason, TerminationReasonDto>, TerminationReasonDefinition>();
        services.AddSingleton<IReferenceDefinition<AwardType, AwardTypeDto>, AwardTypeDefinition>();
        services.AddSingleton<IReferenceDefinition<AbsenceType, AbsenceTypeDto>, AbsenceTypeDefinition>();
        services.AddSingleton<IReferenceDefinition<PenaltyType, PenaltyTypeDto>, PenaltyTypeDefinition>();
        services.AddSingleton<IReferenceDefinition<DocumentType, DocumentTypeDto>, DocumentTypeDefinition>();

        return services;
    }

    /// <summary>Bo'limlarning daraxt bilan bog'liq use-case'lari.</summary>
    private static IServiceCollection AddDepartmentUseCases(this IServiceCollection services)
    {
        services.AddScoped<GetDepartmentTreeQueryHandler>();
        services.AddScoped<CreateDepartmentCommandHandler>();
        services.AddScoped<UpdateDepartmentCommandHandler>();
        services.AddScoped<MoveDepartmentCommandHandler>();

        return services;
    }

    /// <summary>Lavozimlar va ularning vazifalari.</summary>
    private static IServiceCollection AddPositionUseCases(this IServiceCollection services)
    {
        services.AddScoped<ListPositionsQueryHandler>();
        services.AddScoped<GetPositionQueryHandler>();
        services.AddScoped<GetPositionDutiesQueryHandler>();
        services.AddScoped<CreatePositionCommandHandler>();
        services.AddScoped<UpdatePositionCommandHandler>();
        services.AddScoped<AddPositionDutyCommandHandler>();
        services.AddScoped<RemovePositionDutyCommandHandler>();
        services.AddScoped<ReorderPositionDutiesCommandHandler>();

        return services;
    }
}
