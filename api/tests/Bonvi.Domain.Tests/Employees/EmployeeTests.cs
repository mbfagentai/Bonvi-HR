using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.Employees;

/// <summary>
/// Xodim (shaxs) kartasi mantig'i.
/// Namunalar Bonvi'ning haqiqiy «№91 бланк» faylidan.
/// </summary>
public sealed class EmployeeTests
{
    private static Employee Yaratilgan(DateOnly? tugilgan = null)
        => Employee.Create(
            FullName.Parse("Мамажонов Бобуржон Фазлитдин ўғли").Value,
            tugilgan).Value;

    [Fact]
    public void Yangi_xodim_tekshiruv_belgisisiz_yaratiladi()
    {
        var x = Yaratilgan();

        x.NeedsReview.ShouldBeFalse();
        x.Pinfl.ShouldBeNull();
        x.DirectionIds.ShouldBeEmpty();
    }

    [Fact]
    public void Kelajakdagi_tugilgan_sana_rad_etiladi()
    {
        var ertaga = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var natija = Employee.Create(FullName.Parse("Тестов Тест").Value, ertaga);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.tugilgan_sana");
    }

    [Fact]
    public void Haqiqiy_bolmagan_yosh_rad_etiladi()
    {
        var natija = Employee.Create(
            FullName.Parse("Тестов Тест").Value,
            new DateOnly(1876, 7, 17));

        natija.IsFailure.ShouldBeTrue();
    }

    // ────────────────────── JSHSHIR ──────────────────────

    [Fact]
    public void JSHSHIR_ornatilganda_jins_va_sana_toldiriladi()
    {
        var x = Yaratilgan();
        var pinfl = Pinfl.Create("31501914320045").Value;   // 15.01.1991, erkak

        x.SetPinfl(pinfl).IsSuccess.ShouldBeTrue();

        x.Gender.ShouldBe(Gender.Male);
        x.BirthDate.ShouldBe(new DateOnly(1991, 1, 15));
    }

    [Fact]
    public void JSHSHIR_ichidagi_sana_mos_kelmasa_xato_qaytadi()
    {
        // Excel'da 527 yozuvdan 32 tasida shunday nomuvofiqlik bor edi —
        // bu kiritish xatosining aniq belgisi
        var x = Yaratilgan(new DateOnly(1991, 1, 16));   // bir kun farq
        var pinfl = Pinfl.Create("31501914320045").Value; // 15.01.1991

        var natija = x.SetPinfl(pinfl);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.jshshir_sana_mos_emas");
        x.Pinfl.ShouldNotBeNull("nomuvofiqlik ko'chirishni to'xtatmasligi kerak");
    }

    [Fact]
    public void JSHSHIR_bosh_qoldirilishi_mumkin()
    {
        // Excel'dagi 540 yozuvdan 3 tasida JSHSHIR umuman yo'q.
        // Majburiy qilinsa, ularni ko'chirib bo'lmaydi.
        var x = Yaratilgan();

        x.SetPinfl(null).IsSuccess.ShouldBeTrue();
        x.Pinfl.ShouldBeNull();
    }

    // ────────────────────── Tug'ilgan sana ──────────────────────

    /// <summary>
    /// Yangi sana va yangi JSHSHIR BIRGA kelganda tahrir o'tadi.
    /// </summary>
    /// <remarks>
    /// Bu — ko'chirilgan yozuvni tuzatishning yagona yo'li. Ikkala qiymat
    /// alohida o'rnatilsa, oraliqda «yangi JSHSHIR + eski sana» juftligi
    /// hosil bo'lardi va tekshiruv uni rad etardi — hatto yangi qiymatlar
    /// bir-biriga to'liq mos bo'lganda ham. O'shanda «tekshirish kerak»
    /// belgisini olib tashlashning imkoni qolmasdi.
    /// </remarks>
    [Fact]
    public void Yangi_sana_va_yangi_jshshir_birga_qabul_qilinadi()
    {
        var x = Yaratilgan(new DateOnly(1991, 1, 16));               // eski, xato sana
        var yangi = Pinfl.Create("31501914320045").Value;            // 15.01.1991

        var natija = x.SetBirthDateAndPinfl(new DateOnly(1991, 1, 15), yangi);

        natija.IsSuccess.ShouldBeTrue();
        x.BirthDate.ShouldBe(new DateOnly(1991, 1, 15));
        x.Pinfl.ShouldNotBeNull();
        x.Pinfl.Value.ShouldBe("31501914320045");
    }

    [Fact]
    public void Tugilgan_sana_alohida_tuzatiladi()
    {
        var x = Yaratilgan(new DateOnly(1990, 5, 5));

        x.SetBirthDate(new DateOnly(1990, 6, 6)).IsSuccess.ShouldBeTrue();

        x.BirthDate.ShouldBe(new DateOnly(1990, 6, 6));
    }

    /// <summary>Mavjud JSHSHIRga zid sana rad etiladi.</summary>
    /// <remarks>
    /// Sana tahrirlanadigan bo'lgani bilan karta o'zi bilan o'zi
    /// ziddiyatga tushmasligi kerak: JSHSHIR ham, sana ham saqlangan
    /// bo'lsa, keyin qaysi biri to'g'ri ekanini hech kim ayta olmasdi.
    /// </remarks>
    [Fact]
    public void Mavjud_jshshirga_zid_sana_rad_etiladi()
    {
        var x = Yaratilgan();
        x.SetPinfl(Pinfl.Create("31501914320045").Value).IsSuccess.ShouldBeTrue();

        var natija = x.SetBirthDate(new DateOnly(1991, 1, 16));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.jshshir_sana_mos_emas");
    }

    /// <summary>Yaratishdagi chegaralar tahrirlashda ham amal qiladi.</summary>
    [Fact]
    public void Kelajakdagi_sana_tahrirlashda_ham_rad_etiladi()
    {
        var x = Yaratilgan(new DateOnly(1990, 5, 5));
        var ertaga = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var natija = x.SetBirthDate(ertaga);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("xodim.tugilgan_sana");
        x.BirthDate.ShouldBe(new DateOnly(1990, 5, 5), "rad etilgan sana kartaga tushmasligi kerak");
    }

    /// <summary>Sana berilmasa — o'chirilmaydi, o'zgarmaydi.</summary>
    /// <remarks>
    /// Qism-qism yangilashda <c>null</c> «maydon yuborilmadi» degani.
    /// Tug'ilgan sanani bo'shatish HR uchun ma'noli amal emas.
    /// </remarks>
    [Fact]
    public void Sana_berilmasa_ozgarmaydi()
    {
        var x = Yaratilgan(new DateOnly(1990, 5, 5));

        x.SetBirthDateAndPinfl(null, null).IsSuccess.ShouldBeTrue();

        x.BirthDate.ShouldBe(new DateOnly(1990, 5, 5));
    }

    // ────────────────────── Telegram ──────────────────────

    [Theory]
    [InlineData("https://t.me/B_uzb7", "https://t.me/B_uzb7")]
    [InlineData("@B_uzb7", "https://t.me/B_uzb7")]
    [InlineData("B_uzb7", "https://t.me/B_uzb7")]
    [InlineData("t.me/B_uzb7", "https://t.me/t.me/B_uzb7")]
    public void Telegram_havolasi_normallashtiriladi(string kirish, string kutilgan)
    {
        // Excel'da to'liq havola ham, faqat foydalanuvchi nomi ham uchraydi
        var x = Yaratilgan();

        x.SetTelegram(kirish).IsSuccess.ShouldBeTrue();
        x.TelegramUrl.ShouldBe(kutilgan);
    }

    [Fact]
    public void Bosh_telegram_qiymatni_tozalaydi()
    {
        var x = Yaratilgan();
        x.SetTelegram("@B_uzb7");

        x.SetTelegram("  ").IsSuccess.ShouldBeTrue();
        x.TelegramUrl.ShouldBeNull();
    }

    // ────────────────────── Yo'nalishlar ──────────────────────

    [Fact]
    public void Yonalishlar_kop_ga_kop_bogʻlanadi()
    {
        // Excel'da «Вело, метан, шина, логистика» bitta katakda edi
        var x = Yaratilgan();
        var velo = Guid.CreateVersion7();
        var metan = Guid.CreateVersion7();

        x.AssignDirection(velo);
        x.AssignDirection(metan);
        x.AssignDirection(velo);   // takror

        x.DirectionIds.Count.ShouldBe(2);
    }

    [Fact]
    public void Yonalish_olib_tashlanadi_va_tozalanadi()
    {
        var x = Yaratilgan();
        var a = Guid.CreateVersion7();
        x.AssignDirection(a);
        x.AssignDirection(Guid.CreateVersion7());

        x.RemoveDirection(a);
        x.DirectionIds.Count.ShouldBe(1);

        x.ClearDirections();
        x.DirectionIds.ShouldBeEmpty();
    }

    // ────────────────────── Tekshiruv belgisi ──────────────────────

    [Fact]
    public void Tekshiruv_sabablari_toplanadi()
    {
        var x = Yaratilgan();

        x.FlagForReview("JSHSHIR nazorat raqami xato");
        x.FlagForReview("Telefon raqami yo'q");

        x.NeedsReview.ShouldBeTrue();
        x.ReviewNote.ShouldNotBeNull();
        x.ReviewNote.ShouldContain("JSHSHIR");
        x.ReviewNote.ShouldContain("Telefon");
    }

    [Fact]
    public void Tekshirilgandan_keyin_belgi_olib_tashlanadi()
    {
        var x = Yaratilgan();
        x.FlagForReview("Sabab");

        x.MarkReviewed();

        x.NeedsReview.ShouldBeFalse();
        x.ReviewNote.ShouldBeNull();
    }

    [Fact]
    public void Ikkita_telefon_saqlanadi()
    {
        // Excel'da 13 ta katakda ikkita raqam yopishib qolgan edi
        var x = Yaratilgan();
        var birinchi = PhoneNumber.Create("905088888").Value;
        var ikkinchi = PhoneNumber.Create("903081818").Value;

        x.SetPhones(birinchi, ikkinchi);

        x.Phone!.Value.ShouldBe("+998905088888");
        x.SecondPhone!.Value.ShouldBe("+998903081818");
    }
}
