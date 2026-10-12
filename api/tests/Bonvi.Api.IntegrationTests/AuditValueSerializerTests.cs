using System.Buffers;
using System.Text;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.PublicApi;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Audit qiymatlarining JSON shakli (SPEC-rollar-va-audit §7.5) — haqiqiy EF xossalari bilan.
/// </summary>
/// <remarks>
/// Shakl o'qish tomoni (A2) uchun SHARTNOMA: u sanani <c>yyyy-MM-dd</c>, sanoqni nom,
/// pulni son deb o'qiydi. Bu yerda biror qoida jimgina o'zgarsa, jurnal ekrani eski
/// va yangi qatorlarni ikki xil ko'rsatardi. Bazaga ulanmaydi.
/// </remarks>
public sealed class AuditValueSerializerTests
{
    private static readonly Lazy<IModel> Model = new(() =>
    {
        var sozlamalar = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var kontekst = new AppDbContext(sozlamalar);
        return kontekst.Model;
    });

    [Fact]
    public void Sanoq_nomi_bilan_yoziladi_raqami_bilan_emas()
        => Yoz<Employment>(nameof(Employment.Status), EmploymentStatus.Terminated).ShouldBe("\"Terminated\"");

    [Fact]
    public void Sana_yyyy_MM_dd_shaklida()
        => Yoz<Employment>(nameof(Employment.HireDate), new DateOnly(2025, 1, 5)).ShouldBe("\"2025-01-05\"");

    [Fact]
    public void Vaqt_UTC_ISO_8601_shaklida()
        => Yoz<User>(nameof(User.LastLoginAt), new DateTimeOffset(2026, 10, 12, 10, 30, 0, TimeSpan.FromHours(5)))
            .ShouldBe("\"2026-10-12T05:30:00.0000000+00:00\"");

    [Fact]
    public void Kasr_son_JSON_soni()
        => Yoz<Employment>(nameof(Employment.Rate), 0.5m).ShouldBe("0.5");

    [Fact]
    public void Guid_satr()
    {
        var id = Guid.Parse("01996f00-0000-7000-8000-00000000b071");

        Yoz<Employment>(nameof(Employment.EmployeeId), id).ShouldBe("\"01996f00-0000-7000-8000-00000000b071\"");
    }

    [Fact]
    public void Mantiqiy_qiymat_JSON_bool()
        => Yoz<Employment>(nameof(Employment.IsPrimary), true).ShouldBe("true");

    [Fact]
    public void Bosh_qiymat_null()
        => Yoz<Employment>(nameof(Employment.TerminationDate), null).ShouldBe("null");

    /// <summary>Qiymat o'giruvchili xossa — bazaga yoziladigan ko'rinishi (obyekt emas).</summary>
    [Fact]
    public void Ogiruvchili_xossa_baza_korinishida()
        => Yoz<Employee>(nameof(Employee.Phone), PhoneNumber.Create("901234567").Value).ShouldBe("\"+998901234567\"");

    [Fact]
    public void Oddiy_royxat_JSON_massiv()
        => Yoz<Role>(nameof(Role.Permissions), new List<string> { Permissions.EmployeesView, Permissions.CatalogView })
            .ShouldBe($"[\"{Permissions.EmployeesView}\",\"{Permissions.CatalogView}\"]");

    /// <summary><c>jsonb</c> ustun — satr emas, ichiga joylangan JSON.</summary>
    [Fact]
    public void Jsonb_ustun_obyekt_bolib_ichiga_joylanadi()
    {
        IReadOnlyList<GeoPoint> uchlar =
        [
            new(41.3m, 69.2m),
            new(41.4m, 69.2m),
            new(41.4m, 69.3m),
        ];

        var json = Yoz<WorkSite>(nameof(WorkSite.Polygon), uchlar);

        using var hujjat = JsonDocument.Parse(json);
        hujjat.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        hujjat.RootElement.GetArrayLength().ShouldBe(3);
    }

    /// <summary>Bayt massivi (xesh) — hech qachon, hatto katalog yashirishni unutsa ham.</summary>
    [Fact]
    public void Bayt_massivi_hech_qachon_yozilmaydi()
        => Yoz<ApiKey>(nameof(ApiKey.KeyHash), new byte[] { 1, 2, 3 }).ShouldBe("\"***\"");

    /// <summary>Kirill matni o'qilarli qoladi (<c>А</c> emas).</summary>
    [Fact]
    public void Kirill_matni_ekranlanmaydi()
        => Yoz<Employee>(nameof(Employee.Address), "Қўқон ш.").ShouldBe("\"Қўқон ш.\"");

    private static string Yoz<T>(string xossa, object? qiymat)
    {
        var property = Model.Value.FindEntityType(typeof(T)).ShouldNotBeNull().FindProperty(xossa).ShouldNotBeNull();

        var bufer = new ArrayBufferWriter<byte>();
        using (var yozuvchi = new Utf8JsonWriter(bufer, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            AuditValueSerializer.WriteValue(yozuvchi, property, qiymat);
        }

        return Encoding.UTF8.GetString(bufer.WrittenSpan);
    }
}
