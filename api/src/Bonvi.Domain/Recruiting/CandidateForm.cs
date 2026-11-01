using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// Nomzod ochiq onlayn formada (ofisdagi planshet yoki o'z telefoni) to'ldirgan anketa —
/// qog'oz blanka HR-F02 ning o'rnini bosuvchi hujjat (SPEC-planshet-anketa §6).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <see cref="CandidateQuestionnaire"/> emas.</b> U — botdagi o'n
/// uch savolning javobi, har biri o'z ustunida, chunki HR ularni ro'yxatda
/// guruhlaydi. HR-F02 esa yuzga yaqin savol, jadvallar (o'qish joylari, ish
/// joylari, oila a'zolari) va matritsalar (til, kompyuter): ularni ustunlarga
/// yoyish o'nlab jadval degani edi, holbuki ular faqat BITTA joyda kerak —
/// anketaning o'zini (PDF) chizishda. Shu sababli javoblar butunligicha bitta
/// <c>jsonb</c> hujjat (<see cref="Answers"/>), HR guruhlaydigan maydonlar esa
/// odatdagidek <see cref="CandidateQuestionnaire"/> ga ham yoziladi.
/// </para>
/// <para>
/// <b>Nega agregat ILDIZI, <see cref="Candidate"/> ning bolasi emas.</b>
/// Javoblar katta (o'nlab kilobayt) va nomzod kartasining har bir o'qilishida
/// kerak emas — bola bo'lganida <c>GetWithStagesAsync</c> ularni har safar
/// tortib kelardi. Invariant ham yo'q: nomzodda istalgancha anketa bo'lishi
/// mumkin (odam ikki marta to'ldirsa, ikkalasi ham hujjat).
/// </para>
/// <para>
/// <b>O'zgarmaydi</b> — <see cref="CandidateQuestionnaire"/> bilan bir xil
/// qoida: bu nomzodning O'Z so'zlari va imzosi. HR tuzatsa, «nomzod nima deb
/// yozdi va nimani imzoladi» degan fakt yo'qolardi.
/// </para>
/// <para>
/// ⚠️ <b>Javoblarda nozik ma'lumot bor</b> (sudlanganlik, sog'liq, kredit):
/// ular faqat <c>recruiting.view</c> bilan o'qiladi va LOGGA YOZILMAYDI
/// (audit katalogida ham ochilmaydi).
/// </para>
/// </remarks>
public sealed class CandidateForm : AggregateRoot
{
    /// <summary>Blankaning kodi — qog'ozdagi «Blanka HR-F02».</summary>
    public const string HrF02 = "HR-F02";

    /// <summary>Blankaning versiyasi — qog'ozdagi «Versiya 3.1».</summary>
    /// <remarks>
    /// <para>
    /// 3.0 (egasi, 2026-10-26): jinsi, pasport va JSHSHIR olib tashlandi, kompyuter jadvali
    /// qisqardi. 2.0 hech qachon chiqmagan — eski yozuv yo'q.
    /// </para>
    /// <para>
    /// 3.1 (HR rahbari, egasi tasdiqlagan, 2026-10-27): «Avval ishlaganmisiz?», umumiy ish
    /// tajribasi olib tashlandi, ishga chiqish — tanlov, kredit savoli majburiy, «BE» o'rniga
    /// «BC». 3.0 yozuvlari bazada QOLADI (<see cref="FormVersion"/> ularda «3.0») va eski
    /// javoblari bilan qayta chiziladi.
    /// </para>
    /// </remarks>
    public const string HrF02Version = "3.1";

    /// <summary>
    /// Avvalgi versiya — 3.1 chiqqanda brauzerda ochiq qolgan eski forma sahifasidan kelgan anketa
    /// shu versiya bilan yoziladi (Application'dagi <c>QuestionnaireFormValidator.IsLegacy</c>).
    /// </summary>
    public const string HrF02LegacyVersion = "3.0";

    /// <summary>Kod ustunining chegarasi.</summary>
    public const int MaxFormCodeLength = 16;

    /// <summary>Versiya ustunining chegarasi.</summary>
    public const int MaxFormVersionLength = 8;

    /// <summary>Til ustunining chegarasi (<c>uz-Latn</c>, <c>uz-Cyrl</c>, <c>ru</c>).</summary>
    public const int MaxLanguageLength = 8;

    /// <summary>Saqlash kaliti ustunining chegarasi — <c>candidate_files.file_key</c> bilan bir xil.</summary>
    public const int MaxKeyLength = 500;

    private CandidateForm(Guid id) : base(id) { }

    /// <summary>EF Core uchun.</summary>
    private CandidateForm() { }

    /// <summary>Qaysi nomzodning anketasi.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>
    /// «Anketa №» — bazadagi ketma-ketlikdan, unikal.
    /// </summary>
    /// <remarks>
    /// Raqam ketma-ket bo'lishi SHART EMAS, faqat unikal: tranzaksiya
    /// bekor bo'lsa ketma-ketlikda «teshik» qoladi va bu zararsiz
    /// (<c>DevicePersonNumbers</c> bilan bir xil qoida).
    /// </remarks>
    public int Number { get; private set; }

    /// <summary>Blanka kodi — hozircha doim <see cref="HrF02"/>.</summary>
    public string FormCode { get; private set; } = HrF02;

    /// <summary>Blanka versiyasi — javoblar sxemasi shu versiyaga bog'liq.</summary>
    public string FormVersion { get; private set; } = HrF02Version;

    /// <summary>Tekshirilgan va tozalangan javoblar — JSON matni (<c>jsonb</c>).</summary>
    /// <remarks>
    /// Matn sifatida, tur sifatida emas: domen javoblar SXEMASINI bilmaydi
    /// (u Application'da, blanka versiyasi bilan birga yashaydi). Bu yerda
    /// faqat «bo'sh emas» qoidasi.
    /// </remarks>
    public string Answers { get; private set; } = string.Empty;

    /// <summary>Nomzod anketani qaysi tilda to'ldirgan.</summary>
    public string Language { get; private set; } = string.Empty;

    /// <summary>
    /// Anketadagi surat — nomzod suratining MUSTAQIL nusxasi.
    /// </summary>
    /// <remarks>
    /// Nusxa ataylab: HR nomzod suratini keyin almashtirsa, imzolangan
    /// anketaning surati o'zgarmasligi kerak.
    /// </remarks>
    public string PhotoKey { get; private set; } = string.Empty;

    /// <summary>Barmoq bilan chizilgan imzo (PNG).</summary>
    public string SignatureKey { get; private set; } = string.Empty;

    /// <summary>
    /// Forma anketa boshlanganda yasagan identifikator — takroriy
    /// yuborishdan himoya (unikal).
    /// </summary>
    /// <remarks>
    /// Internet yomon bo'lsa javob yo'qoladi va forma qayta yuboradi. Shu
    /// identifikator bo'lmasa, ikkinchi yuborish ikkinchi nomzodni yaratardi.
    /// </remarks>
    public Guid ClientSubmissionId { get; private set; }

    /// <summary>
    /// Nomzod fayllariga biriktirilgan PDF (<see cref="CandidateFile"/>).
    /// </summary>
    /// <remarks>
    /// <c>null</c> — fayllar chegarasi to'lgani uchun biriktirilmagan (anketa
    /// baribir saqlanadi, PDF esa har safar javoblardan qayta chiziladi).
    /// Fayl keyin o'chirilsa ham bu qiymat qoladi — o'qish tomoni faylning
    /// hali borligini tekshiradi.
    /// </remarks>
    public Guid? FileId { get; private set; }

    /// <summary>Yuborilgan lahza.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>
    /// Topshirish paytida amalda bo'lgan HR matnlari reviziyasi (<see cref="FormTextSnapshot"/>).
    /// </summary>
    /// <remarks>
    /// PDF keyin ham AYNAN shu reviziya bilan chiziladi (SPEC-blankalar-hrmentor §5.3):
    /// imzolangan anketaning savollari HR matnni o'zgartirgani uchun o'zgarmasligi kerak.
    /// <c>null</c> — HRMentor ulanmagan edi yoki eski anketa: dasturdagi asl matnlar.
    /// </remarks>
    public int? TextsRevision { get; private set; }

    /// <summary>
    /// HRMentor konstruktorida yig'ilgan anketa versiyasi (<see cref="FormDefinitionSnapshot"/>,
    /// SPEC-anketa-konstruktori §5.1).
    /// </summary>
    /// <remarks>
    /// <c>null</c> — dasturdagi o'rnatilgan anketa (3.0 / 3.1). Qiymat bo'lsa
    /// <see cref="FormVersion"/> — o'sha versiyaning yorlig'i, <see cref="TextsRevision"/> esa
    /// <c>null</c>: matnlar ta'rifning o'zida.
    /// </remarks>
    public int? DefinitionVersion { get; private set; }

    /// <summary>Anketani yozadi.</summary>
    /// <remarks>
    /// Javoblarning MAZMUNI bu yerda tekshirilmaydi — u Application'dagi
    /// tekshiruvchidan o'tgan holda keladi (blanka sxemasi o'sha yerda).
    /// Domen faqat yozuvning o'z yaxlitligini qo'riqlaydi.
    /// </remarks>
    public static Result<CandidateForm> Submit(
        Guid candidateId,
        int number,
        string answers,
        string language,
        string photoKey,
        string signatureKey,
        Guid clientSubmissionId,
        Guid? fileId,
        DateTimeOffset submittedAt,
        string formVersion = HrF02Version,
        int? textsRevision = null,
        int? definitionVersion = null)
    {
        if (candidateId == Guid.Empty)
        {
            return Error.Validation("anketa.nomzod_yoq", "Nomzod ko'rsatilishi shart.");
        }

        if (number <= 0
            || string.IsNullOrWhiteSpace(answers)
            || string.IsNullOrWhiteSpace(language)
            || language.Length > MaxLanguageLength
            || string.IsNullOrWhiteSpace(photoKey)
            || string.IsNullOrWhiteSpace(signatureKey)
            || clientSubmissionId == Guid.Empty
            || string.IsNullOrWhiteSpace(formVersion)
            || formVersion.Length > MaxFormVersionLength
            || definitionVersion is <= 0)
        {
            return Error.Validation("anketa.notogri", "Anketa yozuvi to'liq emas.");
        }

        return new CandidateForm(NewId())
        {
            CandidateId = candidateId,
            Number = number,
            Answers = answers,
            Language = language,
            PhotoKey = photoKey,
            SignatureKey = signatureKey,
            ClientSubmissionId = clientSubmissionId,
            FileId = fileId,
            SubmittedAt = submittedAt,
            FormVersion = formVersion,
            TextsRevision = definitionVersion is null && textsRevision is > 0 ? textsRevision : null,
            DefinitionVersion = definitionVersion,
        };
    }
}
