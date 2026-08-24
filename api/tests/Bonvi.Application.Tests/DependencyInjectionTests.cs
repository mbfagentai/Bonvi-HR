using Bonvi.Application.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Tests;

/// <summary>
/// Application qatlamining DI ro'yxatga olinishi testlari.
/// </summary>
/// <remarks>
/// Bu testlar arzon, lekin qimmatli: DI konfiguratsiyasidagi xato odatda
/// dastur ishga tushganda va faqat ma'lum bir so'rov kelganda ko'rinadi.
/// Bu yerda esa u darhol tutiladi.
/// </remarks>
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_xatosiz_royxatga_oladi()
    {
        var services = new ServiceCollection();

        var natija = services.AddApplication();

        natija.ShouldBeSameAs(services);
    }

    [Fact]
    public void Barcha_validatorlar_avtomatik_topiladi()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        using var provider = services.BuildServiceProvider();

        // Har bir validator yaratilishi mumkin bo'lishi kerak —
        // bog'liqligi yetishmasa, shu yerda yiqiladi.
        var validatorlar = services
            .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>))
            .ToList();

        foreach (var descriptor in validatorlar)
        {
            provider.GetService(descriptor.ServiceType)
                .ShouldNotBeNull($"«{descriptor.ServiceType.Name}» validatori yaratilmadi");
        }
    }

    [Fact]
    public void Application_yigmalasi_togri_aniqlanadi()
    {
        DependencyInjection.Assembly.GetName().Name.ShouldBe("Bonvi.Application");
    }
}
