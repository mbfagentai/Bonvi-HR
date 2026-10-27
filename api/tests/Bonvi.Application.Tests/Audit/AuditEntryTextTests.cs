using System.Text.Json;
using Bonvi.Application.Audit;
using Bonvi.Domain.Auditing;

namespace Bonvi.Application.Tests.Audit;

/// <summary>
/// Jurnal qatorining Excel matni — ekrandagi qoidaning nusxasi
/// (<c>web/src/features/audit/lib/audit-format.ts</c>, <c>SPEC-excel-eksport.md</c> §2 #9).
/// </summary>
public sealed class AuditEntryTextTests
{
    // ────────────────────── Bajaruvchi ──────────────────────

    [Fact]
    public void Odam_ismi_va_osha_lahzadagi_roli_bilan()
    {
        var odam = new AuditActorDto(AuditActorKind.User, Guid.NewGuid(), "905088888", "Karimov Anvar", "HR menejer");

        AuditEntryText.ActorName(odam).ShouldBe("Karimov Anvar");
        AuditEntryText.ActorDetail(odam).ShouldBe("HR menejer");
    }

    [Fact]
    public void Ismsiz_odam_telefoni_bilan_tizim_va_qurilma_oz_nomi_bilan()
    {
        AuditEntryText.ActorName(new AuditActorDto(AuditActorKind.User, Guid.NewGuid(), "998905088888", null, null))
            .ShouldBe("+998 90 508 88 88");

        AuditEntryText.ActorName(new AuditActorDto(AuditActorKind.System, null, null, null, null)).ShouldBe("Tizim");
        AuditEntryText.ActorDetail(new AuditActorDto(AuditActorKind.System, null, null, null, null)).ShouldBeNull();
        AuditEntryText.ActorName(new AuditActorDto(AuditActorKind.Device, null, null, null, null)).ShouldBe("Qurilma");

        var ilova = new AuditActorDto(AuditActorKind.EmployeeAccount, null, null, "Valiyev Ali", null);
        AuditEntryText.ActorName(ilova).ShouldBe("Valiyev Ali");
        AuditEntryText.ActorDetail(ilova).ShouldBe("Xodim (ilova)");

        var begona = new AuditActorDto(AuditActorKind.Anonymous, null, "901234567", null, null);
        AuditEntryText.ActorName(begona).ShouldBe("+998 90 123 45 67");
        AuditEntryText.ActorDetail(begona).ShouldBe("Tizimga kirmagan");

        var nomsiz = new AuditActorDto(AuditActorKind.Anonymous, null, null, null, null);
        AuditEntryText.ActorName(nomsiz).ShouldBe("Tizimga kirmagan");
        AuditEntryText.ActorDetail(nomsiz).ShouldBeNull();
    }

    [Fact]
    public void Onlayn_anketa_oz_nomi_bilan_tizimga_kirmagan_deb()
    {
        var anketa = new AuditActorDto(AuditActorKind.Anonymous, null, null, "Onlayn anketa", null);

        AuditEntryText.ActorName(anketa).ShouldBe("Onlayn anketa");
        AuditEntryText.ActorDetail(anketa).ShouldBe("Tizimga kirmagan");
    }

    // ────────────────────── Amal va tur ──────────────────────

    [Fact]
    public void Amal_tanish_biznes_amali_bilan_notanishi_faqat_turi()
    {
        AuditEntryText.Action(Qator(AuditAction.Update, "TransferEmployee")).ShouldBe("O'zgartirildi · Ko'chirish");
        AuditEntryText.Action(Qator(AuditAction.Create, "etl:employees")).ShouldBe("Yaratildi · Excel'dan ko'chirish");
        AuditEntryText.Action(Qator(AuditAction.Update, "DeactivateDirection")).ShouldBe("O'zgartirildi · Faolsizlantirish");
        AuditEntryText.Action(Qator(AuditAction.Export, "ExportSomethingNew")).ShouldBe("Excelga yuklandi");
        AuditEntryText.Action(Qator(AuditAction.Delete, null)).ShouldBe("O'chirildi");
    }

    [Fact]
    public void Parolni_korish_excel_eksporti_deb_yozilmaydi()
    {
        AuditEntryText.Action(Qator(AuditAction.Export, "RevealEmployeePassword")).ShouldStartWith("Parol ko'rildi");
    }

    [Fact]
    public void Jamoa_korishlari_excel_eksporti_deb_yozilmaydi_va_ozbekcha_oqiladi()
    {
        var azo = Qator(
            AuditAction.Export,
            "GetTeamMemberAttendance",
            Ozgarish("action", AuditValueTypes.Text, null, "\"team_member_viewed\""),
            Ozgarish("year", AuditValueTypes.Number, null, "2026"),
            Ozgarish("month", AuditValueTypes.Number, null, "10"));

        AuditEntryText.Action(azo).ShouldBe("Ko'rildi · Jamoa: xodim davomati");
        AuditEntryText.Summary(azo).ShouldBe("Jamoa: xodim davomati ko'rildi");

        var panel = Qator(
            AuditAction.Export,
            "GetTeamBoard",
            Ozgarish("action", AuditValueTypes.Text, null, "\"team_board_viewed\""));

        AuditEntryText.Action(panel).ShouldBe("Ko'rildi · Jamoa paneli");
        AuditEntryText.Summary(panel).ShouldBe("Jamoa paneli ko'rildi");
    }

    [Fact]
    public void Yozuv_turi_ozbekcha_notanishi_xom()
    {
        AuditEntryText.EntityType("EmploymentEvent").ShouldBe("Buyruq");
        AuditEntryText.EntityType("SomethingNew").ShouldBe("SomethingNew");
    }

    // ────────────────────── Xulosa ──────────────────────

    [Fact]
    public void Bitta_maydonli_tahrir_qiymati_bilan()
    {
        var oklad = Qator(
            AuditAction.Update,
            null,
            Ozgarish("Salary", AuditValueTypes.Money, """{"amount":4000000,"currency":"UZS"}""", """{"amount":5500000.5,"currency":"UZS"}"""));

        AuditEntryText.Summary(oklad).ShouldBe("Oklad: 4 000 000 so'm → 5 500 000,5 so'm");

        var bolim = Qator(
            AuditAction.Update,
            null,
            Ozgarish("DepartmentId", AuditValueTypes.Ref, "\"a1\"", "\"b2\"", oldLabel: "Ombor", newLabel: "Sex"));

        AuditEntryText.Summary(bolim).ShouldBe("Bo'lim: Ombor → Sex");

        var sana = Qator(AuditAction.Update, null, Ozgarish("HireDate", AuditValueTypes.Date, null, "\"2026-10-01\""));

        AuditEntryText.Summary(sana).ShouldBe("Ishga kirgan sana: — → 01.10.2026");
    }

    [Fact]
    public void Yashirilgan_qiymat_faqat_nuqtalar_bilan()
    {
        var manzil = Qator(AuditAction.Update, null, Ozgarish("Address", AuditValueTypes.Text, null, null, masked: true));

        AuditEntryText.Summary(manzil).ShouldBe("Manzil: •••");
    }

    [Fact]
    public void Sanoq_qiymati_mavjud_tarjima_bilan()
    {
        var holat = Qator(
            AuditAction.Update,
            null,
            Ozgarish("Status", AuditValueTypes.Enum, "\"InProgress\"", "\"Passed\""),
            entityName: "Probation");

        AuditEntryText.Summary(holat).ShouldBe("Holat: Davom etmoqda → O'tdi");
    }

    [Fact]
    public void Kop_maydonli_ozgarish_nomlari_va_yana_n_ta()
    {
        var yaratish = Qator(
            AuditAction.Create,
            null,
            Ozgarish("FullName.LastName", AuditValueTypes.Text, null, "\"A\""),
            Ozgarish("FullName.FirstName", AuditValueTypes.Text, null, "\"B\""),
            Ozgarish("Address", AuditValueTypes.Text, null, null, masked: true),
            Ozgarish("Phone", AuditValueTypes.Text, null, "\"+998901234567\""),
            Ozgarish("Speciality", AuditValueTypes.Text, null, "\"X\""),
            Ozgarish("SomeNewField", AuditValueTypes.Text, null, "\"Y\""));

        AuditEntryText.Summary(yaratish).ShouldBe("Familiya, Ism, Manzil, Telefon va yana 2 ta");
    }

    [Fact]
    public void Hodisalar_tafsilotdan_bitta_jumla()
    {
        AuditEntryText.Summary(Qator(AuditAction.LoginFailed, null, Ozgarish("reason", AuditValueTypes.Text, null, "\"locked\"")))
            .ShouldBe("Hisob vaqtincha bloklangan");

        AuditEntryText.Summary(Qator(
                AuditAction.PermissionDenied,
                null,
                Ozgarish("method", AuditValueTypes.Text, null, "\"DELETE\""),
                Ozgarish("path", AuditValueTypes.Text, null, "\"/api/roles/1\"")))
            .ShouldBe("DELETE /api/roles/1");

        AuditEntryText.Summary(Qator(AuditAction.Export, null, Ozgarish("rows", AuditValueTypes.Number, null, "1250")))
            .ShouldBe("1 250 ta qator");

        AuditEntryText.Summary(Qator(AuditAction.Login, null)).ShouldBe(string.Empty);
    }

    // ────────────────────── Yordamchilar ──────────────────────

    private static AuditEntryDto Qator(
        AuditAction action,
        string? operation,
        params AuditChangeDto[] changes)
        => Qator(action, operation, "Employee", changes);

    private static AuditEntryDto Qator(
        AuditAction action,
        string? operation,
        AuditChangeDto change,
        string entityName)
        => Qator(action, operation, entityName, [change]);

    private static AuditEntryDto Qator(AuditAction action, string? operation, string entityName, AuditChangeDto[] changes)
        => new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            action,
            operation,
            Module: null,
            entityName,
            EntityId: null,
            EntityLabel: null,
            SubjectEmployeeId: null,
            new AuditActorDto(AuditActorKind.System, null, null, null, null),
            changes,
            CorrelationId: null);

    private static AuditChangeDto Ozgarish(
        string field,
        string valueType,
        string? oldJson,
        string? newJson,
        string? oldLabel = null,
        string? newLabel = null,
        bool masked = false)
        => new(field, valueType, Json(oldJson), Json(newJson), oldLabel, newLabel, masked);

    private static JsonElement? Json(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var hujjat = JsonDocument.Parse(json);
        return hujjat.RootElement.Clone();
    }
}
