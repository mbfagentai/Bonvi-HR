using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// Rad etish xabarining navbati — nomzodga Telegram orqali javob.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nima uchun qo'shildi.</b> Foydalanuvchi talabi: «agar kandidat
/// otkaz olsa telegramdan ham javob ololsin — statusi qabul qilinmadi
/// deb o'zgartirilganda avtomatik xabar jo'natilsin».
/// </para>
/// <para>
/// ⭐ <b>Nega NAVBAT, to'g'ridan-to'g'ri yuborish emas.</b> Bot alohida
/// jarayon (alohida konteyner) va u API ga O'ZI ulanadi — teskarisi
/// emas. Ya'ni rad etish paytida API botga xabar yubora olmaydi:
/// uning botga yo'li yo'q.
/// </para>
/// <para>
/// Shu sababli mas'uliyat ikkiga bo'lingan: API faqat FAKTNI yozadi
/// (nomzod rad etildi), bot esa «xabar kutayotganlar» ro'yxatini
/// davriy so'rab turadi va yuboradi. Bu ayni paytda ishonchliroq ham:
/// bot o'chirilgan bo'lsa ham nomzodlar navbatda qoladi va bot
/// ko'tarilganda javobini oladi.
/// </para>
/// <para>
/// <b>Kamida bir marta, ko'pi bilan bir marta.</b> Belgi xabar
/// HAQIQATAN ketgandan keyin qo'yiladi (<c>MarkRejectionNoticeSent</c>
/// izohi), ya'ni tarmoq uzilsa xabar keyingi aylanishda qayta
/// yuboriladi. Takroriy xabar esa agregatda to'siladi.
/// </para>
/// </remarks>
public sealed record ListPendingRejectionNoticesQuery(int Limit = 20);

public sealed class ListPendingRejectionNoticesQueryHandler
{
    /// <summary>Bir aylanishda ko'pi bilan nechta xabar olinadi.</summary>
    /// <remarks>
    /// Bot ularni BIRIN-KETIN yuboradi va Telegram tezlik chegarasiga
    /// ega. Katta to'da bir aylanishda yuborilsa, oxirgilari rad
    /// etilardi va qayta urinish uchun butun aylanish kutilardi.
    /// </remarks>
    private const int MaxLimit = 50;

    private readonly IRecruitingReadModel _readModel;

    public ListPendingRejectionNoticesQueryHandler(IRecruitingReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<RejectionNoticeDto>>> HandleAsync(
        ListPendingRejectionNoticesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, MaxLimit);

        var rows = await _readModel
            .ListPendingRejectionNoticesAsync(limit, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(rows);
    }
}

/// <summary>Xabar yuborilganini belgilaydi.</summary>
/// <remarks>
/// Buni BOT chaqiradi va faqat xabar haqiqatan ketgandan keyin.
/// IDEMPOTENT: allaqachon belgilangan bo'lsa hech narsa o'zgarmaydi va
/// muvaffaqiyat qaytadi — bot bir xil so'rovni takrorlashi mumkin
/// (javob yo'qolgan bo'lsa) va bu xato bo'lmasligi kerak.
/// <para>
/// <c>Delivered = false</c> — Telegram xabarni ANIQ rad etdi (nomzod botni
/// bloklagan). Yozuv baribir navbatdan chiqadi: bitta urinish yetarli
/// (egasining qarori, 2026-10-05; <c>Candidate.RejectionNoticeDelivered</c>).
/// </para>
/// </remarks>
public sealed record MarkRejectionNoticeSentCommand(Guid CandidateId, bool Delivered = true);

public sealed class MarkRejectionNoticeSentCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MarkRejectionNoticeSentCommandHandler(
        ICandidateRepository candidates,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        MarkRejectionNoticeSentCommand command,
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

        candidate.MarkRejectionNoticeSent(_clock.UtcNow, command.Delivered);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
