namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>
/// Konstruktor anketasini PDF ga chizish uchun hamma narsa (SPEC-anketa-konstruktori §8).
/// </summary>
/// <param name="Answers">TEKSHIRILGAN javoblar — ta'rifi bilan (bo'limlar, savollar, so'zlar shundan).</param>
/// <param name="Number">«Anketa №»; <c>null</c> — namuna (HR ko'rinishi, hech narsa saqlanmaydi).</param>
/// <param name="SubmittedAt">Yuborilgan lahza — TOSHKENT vaqtida.</param>
/// <param name="VacancyTitle">«Lavozim» katagi: vakansiya nomi yoki «Umumiy».</param>
/// <param name="Language">Nomzod anketani qaysi tilda to'ldirgan.</param>
/// <param name="Photo">Surat (JPEG/PNG); yo'q bo'lsa katak bo'sh.</param>
/// <param name="Signature">Imzo (PNG); yo'q bo'lsa chiziq bo'sh.</param>
/// <param name="ReferralChoices">«Qayerdan bildingiz?» variantlari (<see cref="QuestionnairePdfChoice.ReferralChannels"/>).</param>
public sealed record DynamicFormPdfModel(
    DynamicFormAnswers Answers,
    int? Number,
    DateTimeOffset SubmittedAt,
    string VacancyTitle,
    string Language,
    byte[]? Photo,
    byte[]? Signature,
    IReadOnlyList<QuestionnairePdfChoice> ReferralChoices);

/// <summary>
/// Konstruktor anketasini o'rnatilgan PDF uslubida chizadi (Infrastructure, MigraDoc).
/// </summary>
/// <remarks>
/// O'rnatilgan 3.0 / 3.1 anketalari o'z chizuvchisida qoladi (<see cref="IQuestionnairePdfRenderer"/>) —
/// ularning PDF i hech qachon o'zgarmaydi (AC-AK12).
/// </remarks>
public interface IDynamicFormPdfRenderer
{
    Task<byte[]> RenderAsync(DynamicFormPdfModel model, CancellationToken cancellationToken = default);
}
