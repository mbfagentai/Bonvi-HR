using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Identity;
using Shouldly;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Ro'yxatlar ostidagi «20 / 50 / 100 / Barchasi» tanlovi — server tomoni.
/// </summary>
/// <remarks>
/// <para>
/// Egasi 2026-10-15: bo'limlarda sahifa hajmini tanlash mumkin bo'lsin,
/// «Barchasi» bilan birga. «Barchasi» <c>pageSize=10000</c> yuboradi
/// (<c>web/src/shared/lib/use-page-size.ts</c>), ya'ni server uni 200 ga
/// jimgina kesib qo'ysa, HR «hammasi» deb faqat 200 qatorni ko'rardi va
/// sahifalash «1 / 26» ni chiqarardi — tanlov yolg'on gapirardi.
/// </para>
/// <para>
/// Chegara baribir bor: undan kattasi 10 000 ga kesiladi. Tashqi API
/// (<c>/api/v1</c>) o'z 200 lik chegarasida qoladi —
/// <see cref="PublicApiTests"/>.
/// </para>
/// </remarks>
public sealed class ListPageSizeApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>«Barchasi» — frontend'dagi <c>PAGE_SIZE_ALL</c> bilan bir xil.</summary>
    private const int Barchasi = 10_000;

    private readonly BonviApiFactory _factory;

    public ListPageSizeApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>«Barchasi» tanlangan barcha ro'yxat yo'llari.</summary>
    public static TheoryData<string> Royxatlar() => new()
    {
        "/api/employees",
        "/api/recruiting/candidates?includeArchive=true",
        "/api/recruiting/vacancies",
        "/api/probations",
        "/api/incentives",
        "/api/employments/events",
    };

    [Theory]
    [MemberData(nameof(Royxatlar))]
    public async Task Barchasi_200_ga_KESILMAYDI(string yol)
    {
        var sahifa = await SahifaAsync(yol, Barchasi);

        sahifa.PageSize.ShouldBe(Barchasi, "«Barchasi» so'ralgan hajmda qaytishi kerak");
    }

    [Theory]
    [MemberData(nameof(Royxatlar))]
    public async Task Undan_kattasi_chegaraga_kesiladi(string yol)
    {
        var sahifa = await SahifaAsync(yol, 10_000_000);

        sahifa.PageSize.ShouldBe(Barchasi, "chegara baribir bor — butun bazani bitta javobga solib bo'lmaydi");
    }

    /// <summary>Faqat sahifa ma'lumoti — qatorlar bu yerda kerak emas.</summary>
    private sealed record PageInfoResponse(int Page, int PageSize, int Total);

    private async Task<PageInfoResponse> SahifaAsync(string yol, int hajm)
    {
        var ajratgich = yol.Contains('?', StringComparison.Ordinal) ? '&' : '?';

        using var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(
                _factory,
                Permissions.EmployeesView,
                Permissions.EmploymentView,
                Permissions.IncentivesView,
                Permissions.RecruitingView));

        using var javob = await mijoz.GetAsync(
            new Uri($"{yol}{ajratgich}pageSize={hajm}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var sahifa = await javob.Content.ReadFromJsonAsync<PageInfoResponse>(
            TestContext.Current.CancellationToken);

        sahifa.ShouldNotBeNull();
        return sahifa;
    }
}
