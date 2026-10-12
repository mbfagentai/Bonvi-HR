using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// <see cref="AuditCatalog"/> EF modelini TO'LIQ qoplaydimi (SPEC-rollar-va-audit §7.3, §12.1).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu test — yangi jadval qo'shgan odamni to'xtatadigan yagona joy.</b> Entity
/// klassifikatsiyasiz qolsa, interceptor uni jimgina tashlab ketadi (ishlab chiqarishda
/// saqlash yiqilmasin) — ya'ni o'zgarishlari jurnalga tushmaydi va buni hech kim
/// sezmaydi. Shu sababli har bir entity yo yoziladigan, yo SABABI bilan yozilmaydigan
/// bo'lishi kerak.
/// </para>
/// <para>
/// Ikkinchi qoida — sirlar: nomida <c>Hash</c>, <c>Token</c>, <c>Secret</c> yoki
/// <c>Password</c> bo'lgan har bir maydon yashirilgan yoki tashlab ketilgan bo'lishi
/// shart. Yangi «...Token» ustuni qo'shilib, katalogga yozilmasa — qiymati ochiq
/// holda jurnalga tushardi.
/// </para>
/// <para>
/// Bazaga ulanmaydi: model faqat konfiguratsiyalardan quriladi.
/// </para>
/// </remarks>
public sealed class AuditCatalogCoverageTests
{
    private static readonly string[] SirSozlari = ["Hash", "Token", "Secret", "Password"];

    private static readonly Lazy<IModel> Model = new(() =>
    {
        var sozlamalar = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var kontekst = new AppDbContext(sozlamalar);
        return kontekst.Model;
    });

    private static IEnumerable<IEntityType> Entitylar => Model.Value.GetEntityTypes().Where(type => !type.IsOwned());

    [Fact]
    public void Har_bir_entity_yo_yoziladi_yo_sababi_bilan_tashlab_ketiladi()
    {
        var klassifikatsiyasiz = Entitylar
            .Select(type => type.ClrType)
            .Where(type => !AuditCatalog.Audited.ContainsKey(type) && !AuditCatalog.NotAudited.ContainsKey(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        klassifikatsiyasiz.ShouldBeEmpty(
            "Bu entity'lar audit katalogida yo'q — AuditCatalog'ga qo'shing (yoziladi yoki sababi bilan yozilmaydi): "
            + string.Join(", ", klassifikatsiyasiz));
    }

    [Fact]
    public void Hech_bir_entity_ikki_joyda_ham_turmaydi()
    {
        AuditCatalog.Audited.Keys.Intersect(AuditCatalog.NotAudited.Keys).ShouldBeEmpty();
    }

    [Fact]
    public void Katalogda_modelda_yoq_eskirgan_tur_yoq()
    {
        var modelda = Entitylar.Select(type => type.ClrType).ToHashSet();

        AuditCatalog.Audited.Keys.Concat(AuditCatalog.NotAudited.Keys)
            .Where(type => !modelda.Contains(type))
            .Select(type => type.Name)
            .ShouldBeEmpty("katalogda EF modelida bo'lmagan tur bor — o'chirilgan entity katalogdan ham olinsin");
    }

    [Fact]
    public void Yozilmaydigan_har_bir_entity_sababi_bilan()
    {
        AuditCatalog.NotAudited.Where(pair => string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => pair.Key.Name)
            .ShouldBeEmpty();
    }

    /// <summary>Nomida sir so'zi bor har bir maydon — yashirin yoki tashlab ketilgan.</summary>
    [Fact]
    public void Sir_nomli_har_bir_maydon_yashirilgan_yoki_tashlab_ketilgan()
    {
        var ochiq = new List<string>();

        foreach (var type in Entitylar)
        {
            if (!AuditCatalog.Audited.TryGetValue(type.ClrType, out var qoida))
            {
                continue;
            }

            foreach (var maydon in Maydonlar(type))
            {
                var sirli = SirSozlari.Any(soz => maydon.Contains(soz, StringComparison.OrdinalIgnoreCase));

                if (sirli && !qoida.MaskedFields.Contains(maydon) && !qoida.IsExcluded(maydon))
                {
                    ochiq.Add(type.ClrType.Name + "." + maydon);
                }
            }
        }

        ochiq.ShouldBeEmpty("Sir nomli maydon jurnalga OCHIQ tushardi — AuditCatalog'da Mask(...) yoki Exclude(...) qiling: "
            + string.Join(", ", ochiq));
    }

    /// <summary>Katalogdagi har bir maydon nomi haqiqatan bor — harf xatosi qoidani jimgina o'chirib qo'ymasin.</summary>
    [Fact]
    public void Katalogdagi_har_bir_maydon_nomi_haqiqatan_mavjud()
    {
        var notanish = new List<string>();

        foreach (var (clrType, qoida) in AuditCatalog.Audited)
        {
            var type = Model.Value.FindEntityType(clrType).ShouldNotBeNull();
            var maydonlar = Maydonlar(type).ToHashSet(StringComparer.Ordinal);

            IEnumerable<string?> ishlatilgan =
            [
                .. qoida.MaskedFields,
                .. qoida.ExcludedFields,
                qoida.SubjectProperty,
                qoida.LabelKeyProperty,
            ];

            notanish.AddRange(ishlatilgan
                .Where(nom => nom is not null && !maydonlar.Contains(nom))
                .Select(nom => clrType.Name + "." + nom));
        }

        notanish.ShouldBeEmpty("AuditCatalog'da mavjud bo'lmagan maydon: " + string.Join(", ", notanish));
    }

    /// <summary>Xodim va yorliq kalitlari — <see cref="Guid"/> maydonlar.</summary>
    [Fact]
    public void Xodim_va_yorliq_kalitlari_Guid_maydonlar()
    {
        foreach (var (clrType, qoida) in AuditCatalog.Audited)
        {
            var type = Model.Value.FindEntityType(clrType).ShouldNotBeNull();

            foreach (var kalit in new[] { qoida.SubjectProperty, qoida.LabelKeyProperty })
            {
                if (kalit is null)
                {
                    continue;
                }

                var turi = type.FindProperty(kalit).ShouldNotBeNull($"{clrType.Name}.{kalit}").ClrType;
                (turi == typeof(Guid) || turi == typeof(Guid?)).ShouldBeTrue($"{clrType.Name}.{kalit} — Guid emas");
            }
        }
    }

    /// <summary>
    /// Yoziladigan entity'ning kaliti — bitta <see cref="Guid"/> (domen beradi, Guid v7), baza yasamaydi.
    /// </summary>
    /// <remarks>
    /// Audit qatori saqlashdan OLDIN yig'iladi: kalitni baza bersa (identity ustun), <c>entity_id</c>
    /// ga vaqtinchalik qiymat yozilib qolardi va tarixni obyektga bog'lab bo'lmasdi.
    /// </remarks>
    [Fact]
    public void Yoziladigan_entity_kaliti_bitta_Guid()
    {
        AuditCatalog.Audited.Keys
            .Select(clrType => Model.Value.FindEntityType(clrType).ShouldNotBeNull())
            .Where(type => type.FindPrimaryKey() is not { Properties: [{ ClrType: var turi }] } || turi != typeof(Guid))
            .Select(type => type.ClrType.Name)
            .ShouldBeEmpty();
    }

    /// <summary>Har bir yoziladigan entity'ning bo'limi bor va u haqiqiy bo'lim kaliti.</summary>
    [Fact]
    public void Har_bir_qoidaning_bolimi_haqiqiy_kalit()
    {
        AuditCatalog.Audited.Values
            .Where(qoida => !AccessModuleKeys.All.Contains(qoida.Module))
            .Select(qoida => qoida.EntityType.Name)
            .ShouldBeEmpty();
    }

    /// <summary>Har bir yoziladigan entity'ning yorlig'i bor — jurnalda «nima» bo'sh qolmasin.</summary>
    [Fact]
    public void Har_bir_qoidaning_yorligi_bor()
    {
        AuditCatalog.Audited.Values
            .Where(qoida => !qoida.HasOwnLabel && !qoida.UsesSubjectLabel && qoida.LabelLookup is null)
            .Select(qoida => qoida.EntityType.Name)
            .ShouldBeEmpty();

        AuditCatalog.Audited.Values
            .Where(qoida => qoida.UsesSubjectLabel && qoida.SubjectSource == AuditSubjectSource.None)
            .Select(qoida => qoida.EntityType.Name)
            .ShouldBeEmpty("xodim nomi yorliq bo'lsa, xodim manbai ham bo'lishi kerak");
    }

    /// <summary>
    /// Fon vazifalari har daqiqada yozadigan maydonlar — tashlab ketilgan (jurnal to'lib ketmasin).
    /// </summary>
    [Theory]
    [InlineData("AttendanceDevice", "LastSeenAt")]
    [InlineData("AttendanceDevice", "AgentSeenAt")]
    [InlineData("AttendanceDevice", "TerminalSeenAt")]
    [InlineData("AttendanceDevice", "ClockOffsetSeconds")]
    [InlineData("AttendanceDevice", "HeldRemovals")]
    [InlineData("User", "LastLoginAt")]
    [InlineData("User", "FailedLoginAttempts")]
    [InlineData("User", "LockedUntil")]
    [InlineData("ApiKey", "LastUsedAt")]
    [InlineData("ApiKey", "LastUsedIp")]
    [InlineData("EmployeeAccount", "LastLoginAt")]
    [InlineData("Department", "Path")]
    public void Har_daqiqada_yangilanadigan_maydon_tashlab_ketilgan(string entity, string maydon)
    {
        var qoida = AuditCatalog.Audited.Values.Single(rule => rule.EntityType.Name == entity);

        qoida.IsExcluded(maydon).ShouldBeTrue($"{entity}.{maydon}");
    }

    /// <summary>Mashina yasaydigan va juda ko'p yoziladigan jadvallar umuman yozilmaydi.</summary>
    [Theory]
    [InlineData("AttendancePass")]
    [InlineData("AttendanceDay")]
    [InlineData("DeviceEnrollment")]
    [InlineData("MobileCheckIn")]
    [InlineData("RefreshToken")]
    [InlineData("EmployeeRefreshToken")]
    [InlineData("NotificationState")]
    [InlineData("McpAuditEntry")]
    [InlineData("AuditLog")]
    public void Yuqori_hajmli_jadval_yozilmaydi(string entity)
    {
        AuditCatalog.NotAudited.Keys.Select(type => type.Name).ShouldContain(entity);
    }

    // ────────────────────────────────────────────────────────────
    //  O'qish tomoni (A2): maydon sinflari va havolalar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Xodim kartasining shaxsiy maydonlari (SPEC §3.10) — jurnalda ham shaxsiy: ruxsatsiz
    /// o'quvchiga qiymati ko'rinmaydi.
    /// </summary>
    [Theory]
    [InlineData("BirthDate")]
    [InlineData("Pinfl")]
    [InlineData("Passport")]
    [InlineData("SecondPhone")]
    [InlineData("Address")]
    [InlineData("HasCriminalRecord")]
    [InlineData("CriminalRecordNote")]
    [InlineData("MehnatUzUrl")]
    public void Xodimning_shaxsiy_maydoni_shaxsiy_sinfda(string maydon)
    {
        var qoida = AuditCatalog.Audited[typeof(Employee)];

        Model.Value.FindEntityType(typeof(Employee)).ShouldNotBeNull().FindProperty(maydon)
            .ShouldNotBeNull($"Employee.{maydon} — xossa nomi o'zgargan, ro'yxatni yangilang");
        qoida.ClassOf(maydon, discriminator: null).ShouldBe(AuditFieldClass.Personal, $"Employee.{maydon}");
    }

    /// <summary>
    /// Har bir pul maydoni ongli ravishda sinflangan: oklad (yoki qo'shimcha haq) YOKI ilovada
    /// ham ochiq summa. Yangi <c>Money</c> ustuni «o'z-o'zidan ochiq» bo'lib qolmasin.
    /// </summary>
    [Fact]
    public void Har_bir_pul_maydoni_oklad_yoki_ochiq_deb_belgilangan()
    {
        var sinfsiz = new List<string>();
        var jami = 0;

        foreach (var (clrType, qoida) in AuditCatalog.Audited)
        {
            var type = Model.Value.FindEntityType(clrType).ShouldNotBeNull();

            foreach (var pul in type.GetComplexProperties().Where(complex => complex.ComplexType.ClrType == typeof(Money)))
            {
                jami++;
                var sinf = qoida.ClassOf(pul.Name, discriminator: "-");

                if (sinf is not (AuditFieldClass.Salary or AuditFieldClass.ExtraPay) && !qoida.OpenMoneyFields.Contains(pul.Name))
                {
                    sinfsiz.Add(clrType.Name + "." + pul.Name);
                }
            }
        }

        jami.ShouldBeGreaterThanOrEqualTo(4, "Employment.Salary, EmploymentEvent.From/ToSalary, Incentive.Amount");
        sinfsiz.ShouldBeEmpty(
            "Pul maydoni sinfsiz — AuditCatalog'da Salary(...) yoki OpenMoney(...) qiling: " + string.Join(", ", sinfsiz));
    }

    /// <summary>Okladlar — oklad sinfida (qo'shimcha haq bo'ladigani ham, tur ma'lum bo'lsa).</summary>
    [Theory]
    [InlineData(typeof(Employment), "Salary")]
    [InlineData(typeof(EmploymentEvent), "FromSalary")]
    [InlineData(typeof(EmploymentEvent), "ToSalary")]
    public void Oklad_maydoni_oklad_sinfida(Type entity, string maydon)
    {
        AuditCatalog.Audited[entity].ClassOf(maydon + ".Amount", discriminator: "Transfer").ShouldBe(AuditFieldClass.Salary);
    }

    /// <summary>
    /// Qo'shimcha vazifadagi <c>ToSalary</c> — qo'shimcha haq; tur NOMA'LUM bo'lsa ham (ehtiyot
    /// tomoni). Ajratuvchi maydon haqiqiy sanoq va qiymati uning a'zosi.
    /// </summary>
    [Fact]
    public void Qoshimcha_vazifa_haqi_alohida_sinfda()
    {
        var qoida = AuditCatalog.Audited[typeof(EmploymentEvent)];
        var qoshimcha = qoida.ExtraPay.ShouldNotBeNull();

        qoida.ClassOf("ToSalary", nameof(EmploymentEventType.AdditionalDuty)).ShouldBe(AuditFieldClass.ExtraPay);
        qoida.ClassOf("ToSalary", discriminator: null).ShouldBe(AuditFieldClass.ExtraPay);
        qoida.ClassOf("FromSalary", nameof(EmploymentEventType.AdditionalDuty)).ShouldBe(AuditFieldClass.Salary);

        var ajratuvchi = Model.Value.FindEntityType(typeof(EmploymentEvent)).ShouldNotBeNull()
            .FindProperty(qoshimcha.Discriminator).ShouldNotBeNull();

        ajratuvchi.ClrType.IsEnum.ShouldBeTrue();
        Enum.GetNames(ajratuvchi.ClrType).ShouldContain(qoshimcha.Value);
    }

    /// <summary>
    /// Har bir <see cref="Guid"/> maydon (kalit va tashlab ketilganlardan tashqari) — havola:
    /// jurnalda identifikator emas, nom ko'rinsin. Yangi «...Id» ustuni qo'shgan odam uning
    /// jadvalini aytmaguncha test yashil bo'lmaydi.
    /// </summary>
    [Fact]
    public void Har_bir_Guid_maydoni_havola_yoki_tashlab_ketilgan()
    {
        var havolasiz = new List<string>();

        foreach (var (clrType, qoida) in AuditCatalog.Audited)
        {
            var type = Model.Value.FindEntityType(clrType).ShouldNotBeNull();
            var kalitlar = type.FindPrimaryKey()?.Properties.Select(key => key.Name).ToHashSet(StringComparer.Ordinal) ?? [];

            foreach (var property in type.GetProperties())
            {
                if (!GuidMaydoni(property) || kalitlar.Contains(property.Name) || qoida.IsExcluded(property.Name))
                {
                    continue;
                }

                if (qoida.RefOf(property.Name) is null)
                {
                    havolasiz.Add(clrType.Name + "." + property.Name);
                }
            }
        }

        havolasiz.ShouldBeEmpty(
            "Guid maydon havolasiz — AuditCatalog'da Ref(AuditRefTarget..., ...) qiling: " + string.Join(", ", havolasiz));
    }

    /// <summary>O'qish qoidalaridagi har bir maydon nomi haqiqatan bor — harf xatosi qoidani jimgina o'chirmasin.</summary>
    [Fact]
    public void Sinf_va_havola_maydonlari_haqiqatan_mavjud()
    {
        var notanish = new List<string>();

        foreach (var (clrType, qoida) in AuditCatalog.Audited)
        {
            var type = Model.Value.FindEntityType(clrType).ShouldNotBeNull();

            bool Bor(string nom) => type.FindProperty(nom) is not null || type.FindComplexProperty(nom) is not null;

            IEnumerable<string> ishlatilgan =
            [
                .. qoida.FieldClasses.Keys,
                .. qoida.Refs.Keys,
                .. qoida.OpenMoneyFields,
                .. qoida.ExtraPay is { } qoshimcha ? [qoshimcha.Field, qoshimcha.Discriminator] : Array.Empty<string>(),
            ];

            notanish.AddRange(ishlatilgan.Where(nom => !Bor(nom)).Select(nom => clrType.Name + "." + nom));

            // Havola — faqat Guid yoki Guid ro'yxati
            notanish.AddRange(qoida.Refs.Keys
                .Where(nom => type.FindProperty(nom) is { } property && !GuidMaydoni(property))
                .Select(nom => clrType.Name + "." + nom + " (Guid emas)"));
        }

        notanish.ShouldBeEmpty("AuditCatalog'ning o'qish qoidasida noto'g'ri maydon: " + string.Join(", ", notanish));
    }

    /// <summary>Guid, Guid? yoki Guid'lar ro'yxati (<c>RoleIds</c>, <c>DirectionIds</c>).</summary>
    private static bool GuidMaydoni(IProperty property)
    {
        var turi = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        if (turi == typeof(Guid))
        {
            return true;
        }

        return property.IsPrimitiveCollection
               && property.GetElementType() is { } element
               && (Nullable.GetUnderlyingType(element.ClrType) ?? element.ClrType) == typeof(Guid);
    }

    /// <summary>Oddiy va murakkab (nuqtali) maydon nomlari — interceptor ishlatadigan shaklda.</summary>
    private static IEnumerable<string> Maydonlar(IEntityType type)
    {
        foreach (var property in type.GetProperties())
        {
            yield return property.Name;
        }

        foreach (var complex in type.GetComplexProperties())
        {
            foreach (var nested in Yoy(complex, complex.Name))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<string> Yoy(IComplexProperty complex, string prefiks)
    {
        foreach (var property in complex.ComplexType.GetProperties())
        {
            yield return prefiks + "." + property.Name;
        }

        foreach (var inner in complex.ComplexType.GetComplexProperties())
        {
            foreach (var nested in Yoy(inner, prefiks + "." + inner.Name))
            {
                yield return nested;
            }
        }
    }
}
