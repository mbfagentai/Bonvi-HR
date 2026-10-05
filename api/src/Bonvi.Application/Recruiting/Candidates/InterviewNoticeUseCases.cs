using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// Nomzodni suhbatga chaqirish: bosqich + vaqt + Telegram xabari navbati.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nima uchun alohida amal.</b> Foydalanuvchi talabi (2026-09-08):
/// «Кенгашга чақирилди» tugmasi bosilganda HR dan suhbat sanasi va vaqti
/// so'ralishi, nomzod o'sha bosqichga o'tishi va unga Telegram orqali
/// taklif ketishi kerak. Uchala ish BIR tranzaksiyada bajarilishi shart:
/// bosqich yozilib, vaqt yozilmay qolsa nomzod «chaqirilgan» bo'lib
/// ko'rinardi, lekin hech qanday xabar olmasdi.
/// </para>
/// <para>
/// <b>Nega <c>RecordCandidateStageCommand</c> ga maydon qo'shilmadi.</b>
/// Bosqich yozish — voronkaning kundalik amali va u har xil joydan
/// chaqiriladi (oyna, tezkor qator, voronkadagi sudrash). Unga «suhbat
/// vaqti» maydonini qo'shish har bir chaqiruvchini o'sha maydon haqida
/// o'ylashga majburlardi, holbuki ularning uchtasidan ikkitasiga u
/// umuman tegishli emas.
/// </para>
/// <para>
/// <b>Xabar bu yerda YUBORILMAYDI.</b> API botga murojaat qila olmaydi
/// (sababi <see cref="ListPendingRejectionNoticesQuery"/> izohida) — shu
/// sababli bu yerda faqat FAKT yoziladi, bot esa navbatni davriy so'rab
/// turadi.
/// </para>
/// </remarks>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="StageId">
/// Qaysi bosqichga o'tkaziladi. Bo'sh bo'lsa bosqich O'ZGARMAYDI — suhbat
/// vaqtini KO'CHIRISH aynan shunday ishlaydi: nomzod allaqachon
/// «chaqirilgan» bosqichida turadi va uni qayta yozish tarixga ikkinchi
/// bir xil qator qo'shardi.
/// </param>
/// <param name="InterviewAt">Suhbat lahzasi.</param>
/// <param name="Conclusion">
/// Izoh matni — ixtiyoriy. ⭐ <b>Ikkita manzili bor va qaysi biriga
/// tushishini SERVER hal qiladi</b>:
/// <list type="bullet">
///   <item><paramref name="StageId"/> BERILGAN → matn yangi bosqich
///   yozuvining <c>Conclusion</c> maydoniga tushadi (bugungi
///   xatti-harakat, o'zgarmadi).</item>
///   <item><paramref name="StageId"/> BO'SH (suhbatni ko'chirish, bosqich
///   o'zgarmaydi) → matn <c>candidate_notes</c> ga
///   <see cref="CandidateNoteKind.InterviewRescheduled"/> turi bilan
///   yoziladi.</item>
/// </list>
/// Nega mijozga IKKITA maydon berilmadi (<c>conclusion</c> va
/// <c>note</c>): tanlov — saqlash qarori, ya'ni serverning ishi; brauzerga
/// berilsa u yerda yana bitta adashish joyi paydo bo'lardi. Ilgari maydon
/// ko'chirishda umuman KO'RSATILMASDI va yozilgan matn jimgina tashlab
/// yuborilardi — aynan shu nosozlik tuzatilyapti.
/// </param>
/// <param name="NextContactDate">Keyingi aloqa sanasi — berilsa QO'YILADI.</param>
/// <param name="ClearNextContact">
/// <c>true</c> — saqlangan sana TOZALANADI. Uch holatli qoidaning to'liq
/// izohi <see cref="RecordCandidateStageCommand.ClearNextContact"/> da;
/// amalga oshirilishi ikkalasida BITTA
/// (<see cref="RecordCandidateStageCommandHandler.ApplyNextContact"/>).
/// </param>
public sealed record InviteCandidateToInterviewCommand(
    Guid CandidateId,
    Guid? StageId,
    DateTimeOffset InterviewAt,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool ClearNextContact = false);

public sealed class InviteCandidateToInterviewCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IPipelineStageRepository _stages;
    private readonly RecruitingMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public InviteCandidateToInterviewCommandHandler(
        ICandidateRepository candidates,
        IPipelineStageRepository stages,
        RecruitingMapper mapper,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _stages = stages;
        _mapper = mapper;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        InviteCandidateToInterviewCommand command,
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

        /*
          ⚠️ Vaqt BOSQICHDAN OLDIN tekshiriladi. Teskarisi bo'lsa,
          o'tgan sana kiritilganda bosqich allaqachon yozilgan bo'lardi
          va xato javobini olgan HR nomzodni «chaqirilgan» holatida
          topib qolardi.
        */
        var invited = candidate.InviteToInterview(command.InterviewAt, _clock.Today);
        if (invited.IsFailure)
        {
            return invited.Error;
        }

        if (command.StageId is { } stageId)
        {
            var stage = await _stages.GetByIdAsync(stageId, cancellationToken).ConfigureAwait(false);
            if (stage is null)
            {
                return RecruitingErrors.StageNotFound;
            }

            if (!stage.IsActive)
            {
                return RecruitingErrors.StageInactive;
            }

            /*
              Xulosa hozir bo'sh bo'lishi mumkin: suhbat hali bo'lgani
              yo'q va u keyin, LENTADAGI SHU QATORNI ochib yoziladi
              (<c>EditCandidateStageEntryCommand</c>). Chaqirishning o'zi
              esa tarixda alohida fakt bo'lib qoladi: ikkinchi marta
              chaqirilgan nomzodda ikkita qator bo'ladi va bu to'g'ri —
              u haqiqatan ikki marta chaqirilgan
              (<c>Candidate.RecordStage</c> izohi).

              Sana — suhbat KUNI, bugungi kun emas: bosqich lentasi
              «qachon nima bo'ladi» degan savolga javob berishi kerak.
            */
            var recorded = candidate.RecordStage(
                stageId,
                command.InterviewAt.TashkentDate(),
                command.Conclusion,
                _currentUser.UserId);

            if (recorded.IsFailure)
            {
                return recorded.Error;
            }
        }
        else if (!string.IsNullOrWhiteSpace(command.Conclusion))
        {
            /*
              Bosqich o'zgarmaydi — ya'ni izohni biriktiradigan bosqich
              yozuvi YO'Q va u alohida jadvalga tushadi. Ilgari bu matn
              interfeysda umuman so'ralmasdi (maydon yashirilardi), so'rovda
              kelib qolsa ham JIMGINA tashlanardi.

              Bo'shlik tekshiruvi shu yerda: domen `AddNote` bo'sh matn
              uchun istisno tashlaydi, xato kodi bermaydi (sababi
              `CandidateNote.Create` izohida).
            */
            candidate.AddNote(
                CandidateNoteKind.InterviewRescheduled,
                command.Conclusion,
                _currentUser.UserId);
        }

        // Aloqa sanasi eng oxirida, saqlashdan OLDIN: yuqoridagi
        // tekshiruvlardan birortasi xato qaytarsa, o'zgargan sana ortidan
        // qolib ketmasligi kerak.
        RecordCandidateStageCommandHandler.ApplyNextContact(
            candidate,
            command.NextContactDate,
            command.ClearNextContact);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Suhbat taklifi kutayotgan nomzodlar — botga.</summary>
/// <remarks>
/// <see cref="ListPendingRejectionNoticesQuery"/> ning egizagi va o'sha
/// izohdagi qoidalarga to'liq bo'ysunadi.
/// </remarks>
public sealed record ListPendingInterviewNoticesQuery(int Limit = 20);

public sealed class ListPendingInterviewNoticesQueryHandler
{
    /// <summary>Bir aylanishda ko'pi bilan nechta xabar olinadi.</summary>
    private const int MaxLimit = 50;

    private readonly IRecruitingReadModel _readModel;

    public ListPendingInterviewNoticesQueryHandler(IRecruitingReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<InterviewNoticeDto>>> HandleAsync(
        ListPendingInterviewNoticesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, MaxLimit);

        var rows = await _readModel
            .ListPendingInterviewNoticesAsync(limit, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(rows);
    }
}

/// <summary>Suhbat taklifi yuborilganini belgilaydi — buni BOT chaqiradi.</summary>
/// <remarks>
/// <see cref="MarkRejectionNoticeSentCommand"/> bilan bir xil qoida:
/// faqat xabar HAQIQATAN ketgandan keyin va takroriy chaqiruvda xato
/// bermaydi. <c>Delivered = false</c> — Telegram rad etdi; bitta urinish
/// yetarli, HR ekranda «yetkazilmadi» ni ko'radi.
/// </remarks>
public sealed record MarkInterviewNoticeSentCommand(Guid CandidateId, bool Delivered = true);

public sealed class MarkInterviewNoticeSentCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MarkInterviewNoticeSentCommandHandler(
        ICandidateRepository candidates,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        MarkInterviewNoticeSentCommand command,
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

        candidate.MarkInterviewNoticeSent(_clock.UtcNow, command.Delivered);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
