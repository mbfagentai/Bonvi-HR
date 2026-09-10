using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Candidates;

/*
  ═══════════════════════════════════════════════════════════════════
   NOMZOD FAYLLARI — bitta rezyume o'rniga o'ntagacha fayl.

   ⭐ Nega bu fayl `CandidateResumeUseCases.cs` ning O'RNIGA keldi,
   yonida emas. U yerdagi uchala ishlovchi `candidates.resume_*`
   ustunlarini o'qirdi — ustunlar esa migratsiyada o'chirildi
   (`NomzodIzohVaFayllar`). Ya'ni eski yo'lni «eskirgan taxallus»
   sifatida qoldirish ham mumkin emas edi: u kompilyatsiya
   bo'lmaydi. Sabab (nega bitta fayl yetmadi) `CandidateFile` izohida:
   pasport va diplom telefonda uch-to'rt rasm bo'lib keladi.

   Quvur ham boshqa: `DocumentFileStore` emas, `ScaledDocumentStore` —
   rasm serverda kichraytiriladi. Xodim hujjatlari esa BAYTMA-BAYT
   saqlanadi va o'sha sinfga tegilmadi (sababi `ScaledDocumentStore`
   izohida: aks holda butun tashkilotning shartnoma skanlari jimgina
   qayta kodlanardi).
  ═══════════════════════════════════════════════════════════════════
*/

/// <summary>Nomzodga fayl biriktirish — BITTA so'rov, BITTA fayl.</summary>
/// <remarks>
/// Bir so'rovda N ta fayl qabul qilinmaydi: bitta yaroqsiz fayl yo butun
/// to'plamni yiqitardi, yo «qisman muvaffaqiyat» degan holatni
/// tug'dirardi — uni na <c>Result&lt;T&gt;</c>, na <c>ProblemDetails</c>
/// ifodalay oladi. Interfeys tanlangan fayllarni ketma-ket yuboradi va
/// xatoni AYNAN qaysi fayl bilan sodir bo'lganini ko'rsatadi.
/// </remarks>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="FileName">Mijoz yuborgan asl nom — faqat ko'rsatish uchun.</param>
/// <param name="SizeBytes">
/// Faylning e'lon qilingan (YUKLANGAN) hajmi — 20 MB chegarasi shu bo'yicha
/// tekshiriladi. Bazaga esa DISKKA TUSHGAN hajm yoziladi (sababi
/// <see cref="CandidateFile.SizeBytes"/> izohida).
/// </param>
public sealed record UploadCandidateFileCommand(
    Guid CandidateId,
    Stream Content,
    string? FileName,
    long SizeBytes);

/// <summary>Biriktirilgan faylni yuklab olish.</summary>
/// <remarks>
/// ⭐ <b>IKKALA identifikator ham kerak</b> va bu <c>EmployeeDocument</c>
/// dan ATAYLAB farq qiladi (u hujjatni faqat o'z identifikatori bo'yicha
/// topadi va marshrutdagi xodimga qaramaydi). Bu yerda fayl nomzodning
/// AGREGATI ichidan olinadi, ya'ni chalkash juftlik («A nomzod +
/// B nomzodning fayli») 404 qaytaradi va boshqa odamning pasport nusxasi
/// berilmaydi.
/// </remarks>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="FileId">Fayl.</param>
public sealed record GetCandidateFileQuery(Guid CandidateId, Guid FileId);

/// <summary>Biriktirilgan faylni olib tashlash.</summary>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="FileId">Fayl.</param>
public sealed record DeleteCandidateFileCommand(Guid CandidateId, Guid FileId);

/// <summary>Yuklab olinadigan fayl: nomi, turi va baytlari.</summary>
/// <remarks>
/// Eski <c>CandidateResumeFile</c> ning o'rnini oladi va shakli
/// o'zgarmadi — endpoint uni <c>Results.File(...)</c> ga uzatadi, ya'ni
/// <c>Content-Disposition</c> da ASL nom qaytadi.
/// </remarks>
public sealed record CandidateFileContent(string FileName, string ContentType, Stream Content);

/// <summary>
/// Faylni qabul qiladi, mazmunini tekshiradi, rasm bo'lsa kichraytiradi va
/// saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tekshiruvlar ARZONIDAN boshlanadi</b>
/// (<c>UploadEmployeeDocumentCommandHandler</c> bilan bir xil tartib):
/// (1) nomzod bormi, (2) o'rin bormi, (3) faylning O'ZI. Noma'lum nomzod
/// yoki to'lgan ro'yxat uchun 4 MB lik suratni o'qib, dekodlab, saqlab,
/// so'ng o'chirishning ma'nosi yo'q.
/// </para>
/// <para>
/// ⚠️ <b>Soni ikki joyda tekshiriladi va bu ataylab.</b> Bu yerdagisi —
/// arzon to'xtash, haqiqiy invariant esa <c>Candidate.AttachFile</c> da
/// (sababi <see cref="RecruitingErrors.ResumeTooMany"/> izohida): faqat
/// agregat ichidagi tekshiruv poygaga bardosh beradi.
/// </para>
/// <para>
/// <b>Yetim fayl qolmaydi.</b> Domen faylni rad etsa (soni, umumiy hajmi
/// yoki bo'sh qiymat), diskka allaqachon yozilgan bayt darhol
/// o'chiriladi — eski rezyume yuklovchisidagi bilan aynan bir xil qoida.
/// </para>
/// <para>
/// <b>Javob — YARATILGAN FAYL, butun karta emas</b> (xodim hujjatlari
/// bilan bir xil shakl). Eski rezyume yuklovchisi butun nomzodni
/// qaytarardi; interfeysga bu kerak emas: har bir muvaffaqiyatli
/// yozishdan keyin <c>providers.tsx</c> butun keshni bekor qiladi va
/// karta baribir qayta so'raladi.
/// </para>
/// </remarks>
public sealed class UploadCandidateFileCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly ScaledDocumentStore _files;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UploadCandidateFileCommandHandler(
        ICandidateRepository candidates,
        ScaledDocumentStore files,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _files = files;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateFileDto>> HandleAsync(
        UploadCandidateFileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        // Oqimga TEGMASDAN oldin: o'rin bormi (izohi sinf tepasida).
        if (candidate.Files.Count >= Candidate.MaxFiles)
        {
            return RecruitingErrors.ResumeTooMany;
        }

        var accepted = await _files
            .AcceptAsync(command.Content, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return accepted.Rejection == DocumentRejection.TooLarge
                ? RecruitingErrors.ResumeTooLarge
                : RecruitingErrors.ResumeInvalid;
        }

        /*
          ⚠️ Domenga `accepted.StoredBytes` beriladi, `command.SizeBytes`
          EMAS. Eski rezyume kodi yuklangan hajmni yozardi; rasm serverda
          kichraytirilgach o'sha son YOLG'ONGA aylanadi — 4 MB lik telefon
          surati diskda ~300 KB bo'ladi. Ikki sonning ikki vazifasi bor
          (`CandidateFile.SizeBytes` izohi): chegara yuklangan hajmda,
          baza esa saqlangan hajmda.
        */
        var attached = candidate.AttachFile(
            key,
            command.FileName,
            accepted.ContentType!,
            accepted.StoredBytes,
            _currentUser.UserId);

        if (attached.IsFailure)
        {
            // Fayl ALLAQACHON diskka saqlangan — domen uni rad etsa,
            // ortidan yetim fayl qolmasligi kerak.
            await _files.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return attached.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return RecruitingMapper.ToDto(attached.Value);
    }
}

/// <summary>
/// Faylni baytlari bilan qaytaradi.
/// </summary>
/// <remarks>
/// Fayl nomzodning AGREGATI ichidan izlanadi, ya'ni ikkala identifikator
/// bo'yicha qidirish (<see cref="GetCandidateFileQuery"/> izohi) alohida
/// shart bilan emas, TUZILISH bilan ta'minlanadi — boshqa nomzodning
/// faylini bu yo'ldan olib bo'lmaydi.
/// </remarks>
public sealed class GetCandidateFileQueryHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly ScaledDocumentStore _files;

    public GetCandidateFileQueryHandler(ICandidateRepository candidates, ScaledDocumentStore files)
    {
        _candidates = candidates;
        _files = files;
    }

    public async Task<Result<CandidateFileContent>> HandleAsync(
        GetCandidateFileQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var candidate = await _candidates
            .GetWithStagesAsync(query.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var file = candidate.Files.FirstOrDefault(candidateFile => candidateFile.Id == query.FileId);
        if (file is null)
        {
            return RecruitingErrors.ResumeNotFound;
        }

        var content = await _files.OpenAsync(file.FileKey, cancellationToken).ConfigureAwait(false);

        // Yozuv bor, fayl yo'q — buzilgan holat, lekin mijozga u «bunday
        // fayl yo'q» bo'lib ko'rinishi kerak: 500 xatosi bu yerda
        // foydalanuvchiga hech narsa aytmaydi (izohi
        // `RecruitingErrors.ResumeNotFound` da).
        return content is null
            ? RecruitingErrors.ResumeNotFound
            : new CandidateFileContent(file.FileName, file.ContentType, content);
    }
}

/// <summary>
/// Faylni olib tashlaydi — yozuvni ham, diskdagi baytlarni ham.
/// </summary>
/// <remarks>
/// <para>
/// <b>Yumshoq o'chirish YO'Q</b> (<c>EmployeeDocument</c> dan ataylab
/// farq): sabablari <see cref="CandidateFile"/> izohida — ko'rinmaydigan
/// yozuv o'nta o'rindan bittasini jimgina band qilib turardi va yuklash
/// SABABSIZ to'xtardi.
/// </para>
/// <para>
/// ⚠️ <b>Tartib: avval saqlash, keyin diskdan o'chirish.</b> Teskarisi
/// bo'lsa va saqlash yiqilsa, bazadagi yozuv MAVJUD BO'LMAGAN faylga
/// ishora qilib qolardi — eski rezyume yuklovchisida yozilgan qoidaning
/// aynan o'zi.
/// </para>
/// <para>
/// <b>IDEMPOTENT emas</b>: noma'lum identifikator uchun 404
/// («nomzod.rezyume_topilmadi»). Suratni o'chirish esa idempotent —
/// farqi shunda: u BITTA o'rin («rasm qolmasligi kerak, u esa
/// qolmagan»), bu yerda chaqiruvchi ANIQ bir qatorni nomlagan va
/// jimgina «bajarildi» deyish allaqachon yo'q bo'lgan faylni ko'rsatib
/// turgan eskirgan ekranni yashirardi.
/// </para>
/// </remarks>
public sealed class DeleteCandidateFileCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly ScaledDocumentStore _files;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCandidateFileCommandHandler(
        ICandidateRepository candidates,
        ScaledDocumentStore files,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _files = files;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteCandidateFileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        // Domen kalitni QAYTARADI — chaqiruvchi uni o'zi izlab yurmaydi
        // (izohi `Candidate.RemoveFile` da).
        var removed = candidate.RemoveFile(command.FileId);
        if (removed.IsFailure)
        {
            return removed.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Bu yerda fayl HAQIQATAN o'chiriladi: foydalanuvchi ataylab «olib
        // tashla» dedi va undan keyin faylga hech kim ishora qilmaydi.
        await _files.DeleteAsync(removed.Value, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
