using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>Shu telefon raqamli mavjud nomzodlar.</summary>
/// <param name="Phone">Istalgan ko'rinishda: <c>901234567</c>, <c>+998 90 123 45 67</c>.</param>
/// <param name="ExceptId">Tahrirlanayotgan nomzodning o'zi — natijaga tushmaydi.</param>
public sealed record FindCandidatePhoneMatchesQuery(string? Phone, Guid? ExceptId = null);

/// <summary>
/// HR nomzodni qo'lda kiritayotganda «bu raqam bizda bormi?» degan savolga javob.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kerak bo'ldi (2026-09-27).</b> Qo'lda kiritishda dublikat
/// tekshiruvi ATAYLAB yo'q (<see cref="RegisterCandidateCommandHandler"/>
/// izohi) — takrorni HR qidiruv orqali o'zi ko'rardi. 2026-09-26 dan
/// rezervdagilar nomzodlar ro'yxatidan chiqdi va qidiruv ularni topmay
/// qoldi: HR «topilmadi» ni ko'rib, rezervdagi odamni ikkinchi marta
/// kiritishi mumkin edi. Bu so'rov HAMMA holatni ko'radi va forma uni
/// kiritishni to'smasdan ogohlantirish sifatida ko'rsatadi.
/// </para>
/// <para>
/// <b>Raqam to'liq bo'lmasa — bo'sh ro'yxat, xato emas.</b> Forma raqam
/// yozilayotgan paytda ham so'rashi mumkin; chala raqam uchun 400
/// qaytarish mijozda keraksiz xato holatini yasardi.
/// </para>
/// </remarks>
public sealed class FindCandidatePhoneMatchesQueryHandler
{
    /// <summary>Eng ko'p nechta nomzod qaytadi — ogohlantirish uchun yetarli.</summary>
    public const int Limit = 5;

    private readonly IRecruitingReadModel _readModel;

    public FindCandidatePhoneMatchesQueryHandler(IRecruitingReadModel readModel)
        => _readModel = readModel;

    public async Task<Result<IReadOnlyList<CandidatePhoneMatchDto>>> HandleAsync(
        FindCandidatePhoneMatchesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var phone = PhoneNumber.Create(query.Phone);
        if (phone.IsFailure)
        {
            return Result.Success<IReadOnlyList<CandidatePhoneMatchDto>>([]);
        }

        // Kanonik qiymat «+998901234567» — qidiruv faqat raqamlarni solishtiradi.
        var digits = phone.Value.Value[1..];

        var matches = await _readModel
            .FindPhoneMatchesAsync(digits, query.ExceptId, Limit, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(matches);
    }
}
