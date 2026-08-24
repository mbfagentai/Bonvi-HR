using System.Reflection;
using Bonvi.Domain.Common;
using NetArchTest.Rules;

namespace Bonvi.Architecture.Tests;

/// <summary>
/// Clean Architecture qoidalarini avtomatik himoyalaydi.
/// </summary>
/// <remarks>
/// <para>
/// Bu testlar loyihaning eng muhim himoyasi. Arxitektura qoidasi hujjatda
/// yozilgan bo'lsa — u ertami-kechmi buziladi. Bu yerda esa qoida buzilishi
/// bilan build yiqiladi va kod yetkazib berilmaydi.
/// </para>
/// <para>
/// Ruxsat etilgan bog'liqlik yo'nalishi:
/// <c>Api → Infrastructure → Application → Domain</c>.
/// Teskari yo'nalish yoki sakrash taqiqlanadi.
/// </para>
/// </remarks>
public sealed class LayerDependencyTests
{
    private const string DomainNamespace = "Bonvi.Domain";
    private const string ApplicationNamespace = "Bonvi.Application";
    private const string InfrastructureNamespace = "Bonvi.Infrastructure";
    private const string ApiNamespace = "Bonvi.Api";

    private static Assembly DomainAssembly => typeof(Entity).Assembly;

    private static Assembly ApplicationAssembly => Bonvi.Application.Common.DependencyInjection.Assembly;

    private static Assembly InfrastructureAssembly => Bonvi.Infrastructure.DependencyInjection.Assembly;

    private static Assembly ApiAssembly => typeof(Program).Assembly;

    // ────────────────────────────────────────────────────────────
    //  DOMAIN — yechimning yuragi, hech kimga bog'liq emas
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Domain_boshqa_qatlamlarga_boglanmasligi_kerak()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        AssertOk(result, "Domain qatlami yuqoridagi qatlamlarga bog'lanmasligi kerak");
    }

    [Fact]
    public void Domain_texnologiya_kutubxonalariga_boglanmasligi_kerak()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions.DependencyInjection",
                "Npgsql",
                "Hangfire",
                "FluentValidation",
                "Serilog")
            .GetResult();

        AssertOk(
            result,
            "Domain sof C# bo'lishi kerak — biznes qoidalari ma'lumotlar bazasi yoki veb ramkasiga bog'lanmaydi");
    }

    // ────────────────────────────────────────────────────────────
    //  APPLICATION — faqat Domain'ga bog'lanadi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Application_Infrastructure_va_Api_ga_boglanmasligi_kerak()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        AssertOk(result, "Application qatlami amalga oshirish tafsilotlarini bilmasligi kerak");
    }

    [Fact]
    public void Application_EF_Core_ga_boglanmasligi_kerak()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        AssertOk(
            result,
            "Use-case'lar ma'lumotlar bazasini bilmasligi kerak — faqat repozitoriy interfeysi orqali ishlaydi");
    }

    [Fact]
    public void Application_ASP_NET_Core_ga_boglanmasligi_kerak()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        AssertOk(result, "Use-case'lar HTTP haqida hech narsa bilmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  INFRASTRUCTURE — Api'ga qarab bog'lanmaydi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Infrastructure_Api_ga_boglanmasligi_kerak()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        AssertOk(result, "Infrastructure qatlami veb qatlamiga qarab bog'lanmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  API — EF Core'ga to'g'ridan-to'g'ri murojaat qilmaydi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Api_Npgsql_ga_todan_togri_boglanmasligi_kerak()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn("Npgsql")
            .GetResult();

        AssertOk(
            result,
            "Api qatlami ma'lumotlar bazasi drayveri bilan to'g'ridan-to'g'ri ishlamasligi kerak");
    }

    private static void AssertOk(NetArchTest.Rules.TestResult result, string qoida)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var buzganlar = string.Join(
            Environment.NewLine + "   • ",
            result.FailingTypeNames ?? []);

        result.IsSuccessful.ShouldBeTrue(
            $"""
             ❌ Arxitektura qoidasi buzildi.

                Qoida: {qoida}

                Qoidani buzgan tiplar:
                • {buzganlar}
             """);
    }
}
