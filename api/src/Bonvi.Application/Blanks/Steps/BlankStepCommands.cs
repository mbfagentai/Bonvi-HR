using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Steps;

/// <summary>Qadam amali tanasi (§7 #12; #13 ning <c>payload</c>i ham shu + <c>signerName</c>).</summary>
/// <param name="ExpectedVersion">Blankaning joriy versiyasi — mos kelmasa 409 <c>blanka.ozgargan</c>.</param>
/// <param name="Decision">Qadam qarori (variant qiymati, masalan <c>agree</c>).</param>
/// <param name="DecisionExtra">Tanlangan variantning qo'shimcha qiymati (masalan e'tiroz matni).</param>
/// <param name="Comment">Izoh.</param>
/// <param name="Answers">Qadamning O'Z bo'limlari javoblari — bo'limlar to'liq almashadi; <c>null</c> — o'zgarmaydi.</param>
public sealed record BlankStepActInput(
    int ExpectedVersion,
    string? Decision = null,
    JsonNode? DecisionExtra = null,
    string? Comment = null,
    JsonNode? Answers = null);

/// <summary>Elektron tasdiqlash (§7 #12).</summary>
public sealed record ApproveBlankStepCommand(Guid SubmissionId, string StepCode, BlankStepActInput Act);

/// <summary>HR qurilmasida qo'lda imzo (§7 #13).</summary>
/// <param name="SubmissionId">Blanka.</param>
/// <param name="StepCode">Qadam kodi (ta'rifdagi <c>id</c>).</param>
/// <param name="Payload">JSON: <see cref="BlankStepActInput"/> maydonlari + <c>signerName</c>.</param>
/// <param name="Signature">Chizilgan imzo (PNG).</param>
/// <param name="SignatureSizeBytes">Yuklangan hajm.</param>
public sealed record SignBlankStepInPersonCommand(
    Guid SubmissionId,
    string StepCode,
    string? Payload,
    Stream? Signature,
    long SignatureSizeBytes);

/// <summary>Qoralamaga qaytarish (§7 #14) — izoh majburiy.</summary>
public sealed record ReturnBlankStepCommand(Guid SubmissionId, string StepCode, int ExpectedVersion, string? Comment);

/// <summary>Rad etish (§7 #15) — izoh majburiy.</summary>
public sealed record RejectBlankStepCommand(Guid SubmissionId, string StepCode, int ExpectedVersion, string? Comment);

/// <summary>Qadam imzosi rasmi (§7 #16) — chaqiruvchi oqimni yopadi.</summary>
public sealed record BlankStepSignatureFile(Stream Content);

/// <summary>
/// Blanka tasdiqlanganda (oxirgi guruh imzolandi) chaqiriladigan nuqta — S7 shu yerga yakuniy PDF'ni
/// (<c>final_pdf_storage_key</c>, SPEC §5) ulaydi. Sukut bo'yicha hech narsa qilmaydi.
/// </summary>
/// <remarks>
/// COMMIT'dan KEYIN chaqiriladi. Amalga oshirish xato TASHLAMASLIGI kerak: muvaffaqiyatsizlik o'zi
/// jurnalga yoziladi va tasdiqlashni to'xtatmaydi (PDF so'rov bo'yicha baribir chizilaveradi).
/// </remarks>
public interface IBlankApprovedHook
{
    Task OnApprovedAsync(Guid submissionId, CancellationToken cancellationToken = default);
}

/// <summary>S7 ulanmaguncha — hech narsa qilmaydi.</summary>
public sealed class NoBlankApprovedHook : IBlankApprovedHook
{
    public Task OnApprovedAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
