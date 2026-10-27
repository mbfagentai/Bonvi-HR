using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Commands;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Departments;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Application.Catalog.Positions;
using Bonvi.Application.Catalog.Queries;
using Bonvi.Application.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bonvi.Application.Tests.Catalog;

/// <summary>
/// Ma'lumotnoma handlerlarining DI ulanishini tekshiradi.
/// </summary>
/// <remarks>
/// <para>
/// Generik handler'lar konteynerga OCHIQ generik ko'rinishda yoziladi
/// (<c>typeof(ListReferenceQueryHandler&lt;,&gt;)</c>). Bunday ro'yxatga olish
/// kompilyatsiyada tekshirilmaydi — xato faqat birinchi HTTP so'rovda,
/// ishlab turgan tizimda ko'rinardi.
/// </para>
/// <para>
/// Shu sababli har bir yopiq (closed) tip shu yerda ataylab so'raladi:
/// noto'g'ri ro'yxatga olish testda darhol yiqiladi.
/// </para>
/// </remarks>
public sealed class CatalogWiringTests
{
    /// <summary>
    /// To'liq konteyner: Application + soxta repozitoriylar.
    /// </summary>
    /// <remarks>
    /// Repozitoriylar Infrastructure qatlamida ro'yxatga olinadi, bu test esa
    /// undan mustaqil. Shu sababli ular NSubstitute bilan almashtiriladi —
    /// tekshirilayotgan narsa bog'lanish, xatti-harakat emas.
    /// </remarks>
    private static ServiceProvider Konteyner()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        services.AddScoped(_ => Substitute.For<IUnitOfWork>());
        services.AddScoped(_ => Substitute.For<IDepartmentRepository>());
        services.AddScoped(_ => Substitute.For<IPositionRepository>());

        // Bo'lim boshlig'i mavjudligini tekshirish uchun (SPEC-jamoam §E).
        services.AddScoped(_ => Substitute.For<IEmployeeRepository>());

        // Boshliqni almashtirish «attendance.view» ni ham talab qiladi — joriy foydalanuvchi kerak.
        services.AddScoped(_ => Substitute.For<ICurrentUser>());

        // Infrastructure buni ochiq generik sifatida ro'yxatga oladi;
        // testda esa har bir tip alohida ko'rsatiladi.
        services.AddScoped(_ => Substitute.For<IReferenceRepository<LegalEntity>>());
        services.AddScoped(_ => Substitute.For<IReferenceRepository<Direction>>());
        services.AddScoped(_ => Substitute.For<IReferenceRepository<Department>>());
        services.AddScoped(_ => Substitute.For<IReferenceRepository<Position>>());
        services.AddScoped(_ => Substitute.For<IReferenceRepository<TerminationReason>>());
        services.AddScoped(_ => Substitute.For<IReferenceRepository<AwardType>>());
        services.AddScoped(_ => Substitute.For<IReferenceRepository<PenaltyType>>());
        services.AddScoped(_ => Substitute.For<IReferenceRepository<DocumentType>>());

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Sakkizta_malumotnomaning_generik_handlerlari_yaratiladi()
    {
        using var provider = Konteyner();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<ListReferenceQueryHandler<LegalEntity, LegalEntityDto>>().ShouldNotBeNull();
        sp.GetRequiredService<ListReferenceQueryHandler<Direction, DirectionDto>>().ShouldNotBeNull();
        sp.GetRequiredService<ListReferenceQueryHandler<Department, DepartmentDto>>().ShouldNotBeNull();
        sp.GetRequiredService<ListReferenceQueryHandler<Position, PositionDto>>().ShouldNotBeNull();
        sp.GetRequiredService<ListReferenceQueryHandler<TerminationReason, TerminationReasonDto>>()
            .ShouldNotBeNull();
        sp.GetRequiredService<ListReferenceQueryHandler<AwardType, AwardTypeDto>>().ShouldNotBeNull();
        sp.GetRequiredService<ListReferenceQueryHandler<PenaltyType, PenaltyTypeDto>>().ShouldNotBeNull();
        sp.GetRequiredService<ListReferenceQueryHandler<DocumentType, DocumentTypeDto>>().ShouldNotBeNull();
    }

    [Fact]
    public void Yozish_va_holat_handlerlari_yaratiladi()
    {
        using var provider = Konteyner();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<GetReferenceByIdQueryHandler<AwardType, AwardTypeDto>>().ShouldNotBeNull();
        sp.GetRequiredService<CreateReferenceCommandHandler<AwardType, AwardTypeDto>>().ShouldNotBeNull();
        sp.GetRequiredService<UpdateReferenceCommandHandler<AwardType, AwardTypeDto>>().ShouldNotBeNull();
        sp.GetRequiredService<DeactivateReferenceCommandHandler<AwardType>>().ShouldNotBeNull();
        sp.GetRequiredService<ActivateReferenceCommandHandler<AwardType>>().ShouldNotBeNull();

        // Bo'lim va lavozim ham umumiy amallardan foydalanadi
        sp.GetRequiredService<DeactivateReferenceCommandHandler<Department>>().ShouldNotBeNull();
    }

    [Fact]
    public void Maxsus_use_caselar_yaratiladi()
    {
        using var provider = Konteyner();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<GetDepartmentTreeQueryHandler>().ShouldNotBeNull();
        sp.GetRequiredService<CreateDepartmentCommandHandler>().ShouldNotBeNull();
        sp.GetRequiredService<UpdateDepartmentCommandHandler>().ShouldNotBeNull();
        sp.GetRequiredService<MoveDepartmentCommandHandler>().ShouldNotBeNull();

        sp.GetRequiredService<ListPositionsQueryHandler>().ShouldNotBeNull();
        sp.GetRequiredService<GetPositionQueryHandler>().ShouldNotBeNull();
        sp.GetRequiredService<GetPositionDutiesQueryHandler>().ShouldNotBeNull();
        sp.GetRequiredService<CreatePositionCommandHandler>().ShouldNotBeNull();
        sp.GetRequiredService<UpdatePositionCommandHandler>().ShouldNotBeNull();
        sp.GetRequiredService<AddPositionDutyCommandHandler>().ShouldNotBeNull();
        sp.GetRequiredService<RemovePositionDutyCommandHandler>().ShouldNotBeNull();
        sp.GetRequiredService<ReorderPositionDutiesCommandHandler>().ShouldNotBeNull();
    }

    [Fact]
    public void Har_bir_malumotnomaning_tarifi_royxatga_olingan()
    {
        using var provider = Konteyner();

        provider.GetRequiredService<IReferenceDefinition<LegalEntity, LegalEntityDto>>()
            .ShouldBeOfType<LegalEntityDefinition>();

        provider.GetRequiredService<IReferenceDefinition<PenaltyType, PenaltyTypeDto>>()
            .ShouldBeOfType<PenaltyTypeDefinition>();

        provider.GetRequiredService<IReferenceDefinition<Position, PositionDto>>()
            .ShouldBeOfType<PositionDefinition>();
    }
}
