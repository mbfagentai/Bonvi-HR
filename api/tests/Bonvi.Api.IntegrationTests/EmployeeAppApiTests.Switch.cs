using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim ilovasining o'chirgichi — <c>EmployeeApp:Enabled</c> (prod'da sukut bo'yicha yopiq).
/// </summary>
public sealed partial class EmployeeAppApiTests
{
    /// <summary>
    /// Yopiq ilova: xodim tomonining marshrutlari UMUMAN yo'q — haqiqiy parol va haqiqiy token
    /// bilan ham kirib bo'lmaydi. HR tomoni (hisob, parolni ko'rish) esa ishlayveradi.
    /// </summary>
    [Fact]
    public async Task Yopiq_ilovada_xodim_marshrutlari_yoq_HR_tomoni_ishlaydi()
    {
        var ct = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var xodim = await XodimAsync("off-" + Belgi());

        // Ochiq serverda olingan haqiqiy token va parol — yopiq serverda ham imzo yaroqli.
        var juftlik = await KirganJuftlikAsync(admin, xodim.Id);
        var token = juftlik.GetProperty("accessToken").GetString()!;
        var parol = await ParolAsync(admin, xodim.Id);

        using var yopiq = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(Program.EmployeeAppEnabledKey, "false"));
        using var mijoz = yopiq.CreateClient();

        // Anonim so'rov: endpoint yo'q → loyihaning fallback siyosati (autentifikatsiya shart) → 401.
        // Muhimi — token ham, xeshlash ham yo'q.
        using (var kirish = await mijoz.PostAsJsonAsync(
                   new Uri("/api/employee-auth/login", UriKind.Relative),
                   new { login = xodim.Login, password = parol },
                   ct))
        {
            kirish.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await MatnAsync(kirish)).ShouldNotContain("accessToken");
        }

        // Haqiqiy xodim tokeni bilan ham ma'lumot yo'q: marshrut yo'q → 404.
        using (var sorov = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/me", UriKind.Relative)))
        {
            sorov.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var javob = await mijoz.SendAsync(sorov, ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // Marshrut UMUMAN yo'qligining isboti: autentifikatsiyadan o'tgan (HR) so'rov 404 oladi.
        // Ochiq ilovada xuddi shu so'rovlar 400 (login) va 401 (xodim siyosati) bo'lardi.
        using var hrMijoz = yopiq.CreateClient();
        hrMijoz.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;

        using (var kirish = await hrMijoz.PostAsJsonAsync(
                   new Uri("/api/employee-auth/login", UriKind.Relative),
                   new { login = xodim.Login, password = parol },
                   ct))
        {
            kirish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using (var men = await hrMijoz.GetAsync(new Uri("/api/me", UriKind.Relative), ct))
        {
            men.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // HR tomoni — o'sha yopiq serverda ishlaydi.
        using var karta = await hrMijoz.GetAsync(new Uri($"/api/employees/{xodim.Id}/app-access", UriKind.Relative), ct);
        karta.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(karta));
    }
}
