using System.Text;
using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Infrastructure.Reports.Pdf;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Konstruktor anketasining PDF i (SPEC-anketa-konstruktori §8) — o'rnatilgan anketa uslubida,
/// so'zlar ta'rifdan.
/// </summary>
public sealed partial class QuestionnairePdfRendererTests
{
    private static readonly DateTimeOffset Topshirildi = new(2026, 10, 24, 14, 41, 0, TimeSpan.FromHours(5));

    private readonly DynamicFormPdfRenderer _dynamic = new(NullLogger<DynamicFormPdfRenderer>.Instance);

    [Fact]
    public async Task Konstruktor_anketasi_urug_bilan_qogozdagidek_toliq_PDF_beradi()
    {
        var javoblar = DynamicFormSamples.Checked(QuestionnaireSamples.Full(Bugun), Bugun);
        var pdf = await _dynamic.RenderAsync(
            DynamikModel(javoblar, 1024, QuestionnaireSamples.Photo(), QuestionnaireSamples.Signature()),
            TestContext.Current.CancellationToken);

        Saqla("konstruktor-toliq.pdf", pdf);
        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");

        var yorliq = DynamicFormSamples.Seed.Label;
        var matn = Matn(pdf);

        matn.ShouldContain($"HR-F02 · Nomzod anketasi · v{yorliq}");
        matn.ShouldContain($"Blanka HR-F02 · Versiya {yorliq}");
        matn.ShouldContain("Anketa № 1024 Sana 24.10.2026 Lavozim Payvandchi (Metan zavodi)");
        matn.ShouldContain("A QISM NOMZOD TOMONIDAN TO‘LDIRILADI 1. SHAXSIY MA’LUMOTLAR Familiyasi G‘ulomov Ismi Sardor");

        // Tanlovlar — hamma variant katakchasi bilan, tanlangani belgilangan
        matn.ShouldContain("Guvohnoma toifasi ☐ A ☑ B ☑ C ☐ D");
        matn.ShouldContain("Avtomobil rusumi Chevrolet Cobalt");
        matn.ShouldContain("☑ Tanishlar orqali, kim/qaysi: Karimov Anvar (sex boshlig‘i)");
        matn.ShouldContain("☑ Qabul qilingandan 10 kun keyin");

        // Telefon, telegram, pul, davr — qog'ozdagi ko'rinishda
        matn.ShouldContain("Telefon raqami +998 90 555 11 22");
        matn.ShouldContain("Telegram @sardor_gulomov");
        matn.ShouldContain("Qancha oylik maoshga ishlamoqchisiz? 6 000 000 so‘m");
        matn.ShouldContain("1 «Metan Gaz» MChJ Payvandchi 03.2021 – hozir");
        matn.ShouldContain("2015 – 2019 B 0123456");

        // Darajalar jadvali — qatorlar va «Boshqa: nomi»
        matn.ShouldContain("2 Rus tili 3 3 2");
        matn.ShouldContain("Boshqa: Turk tili 2 1 1");
        matn.ShouldContain("Windows 3 MS Word 3 1C 1");

        // Tilxat A qismning oxirida, B qism — alohida oxirgi betda
        var betlar = Sahifalar(pdf);
        betlar.Count.ShouldBeGreaterThanOrEqualTo(5);
        betlar[^1].ShouldContain("B QISM");
        betlar[^1].ShouldNotContain("11. TILXAT");
        betlar.Take(betlar.Count - 1).ShouldContain(bet => bet.Contains("11. TILXAT", StringComparison.Ordinal), "tilxat A qismda");
        matn.ShouldContain("G‘ulomov Sardor Baxtiyor o‘g‘li");
        matn.ShouldContain("Onlayn to‘ldirildi");
    }

    [Fact]
    public async Task Namuna_PDF_raqamsiz_rasmsiz_va_imzosiz_chiziladi()
    {
        var javoblar = DynamicFormSamples.Checked(QuestionnaireSamples.Minimal(Bugun), Bugun);
        var pdf = await _dynamic.RenderAsync(DynamikModel(javoblar, number: null, photo: null, signature: null), TestContext.Current.CancellationToken);

        Saqla("konstruktor-namuna.pdf", pdf);
        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");

        var matn = Matn(pdf);
        matn.ShouldContain("Anketa № Namuna");
        matn.ShouldNotContain("Anketa № 1024");
        Betlar(pdf).ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task HR_ozgartirgan_yorliq_va_bolim_nomi_PDF_ga_tushadi()
    {
        var taRif = AnketaKonstruktoriSyncApiTests.Tarif("4.2");
        var sogliq = taRif["sections"]!.AsArray().Select(section => section!.AsObject()).First(section => section["id"]!.GetValue<string>() == "health");
        sogliq["pdf_title"] = "Sog‘lig‘ingiz haqida";

        var tarif = FormDefinitionReader.Read(taRif).Value;
        var javoblar = DynamicFormValidator.Validate(DynamicFormSamples.From(QuestionnaireSamples.Full(Bugun)), tarif, Bugun, ReferralSeeds.Options).Value;

        var pdf = await _dynamic.RenderAsync(DynamikModel(javoblar, 7, photo: null, signature: null), TestContext.Current.CancellationToken);
        var matn = Matn(pdf);

        matn.ShouldContain("HR-F02 · Nomzod anketasi · v4.2");
        matn.ShouldContain("Blanka HR-F02 · Versiya 4.2");
        matn.ShouldContain("9. SOG‘LIG‘INGIZ HAQIDA", Case.Sensitive, "bo'lim raqami tartibdan, nomi HR dan");
    }

    [Fact]
    public async Task Buzuq_rasm_konstruktor_PDF_ini_yiqitmaydi()
    {
        var javoblar = DynamicFormSamples.Checked(QuestionnaireSamples.Minimal(Bugun), Bugun);
        var pdf = await _dynamic.RenderAsync(DynamikModel(javoblar, 3, photo: [1, 2, 3, 4], signature: Png(1300, 200)), TestContext.Current.CancellationToken);

        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
    }

    private static DynamicFormPdfModel DynamikModel(DynamicFormAnswers answers, int? number, byte[]? photo, byte[]? signature)
        => new(
            answers,
            number,
            Topshirildi,
            "Payvandchi (Metan zavodi)",
            "uz-Latn",
            photo,
            signature,
            ReferralSeeds.PdfChoices(DynamicFormMapping.Person(answers).ReferralChannelId));
}
