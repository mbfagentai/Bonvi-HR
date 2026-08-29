using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Safety.Common;

/// <summary>
/// Hodisa kunidagi lavozimni xodimning mehnat munosabatidan topadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega HR qo'lda tanlamaydi.</b> Lavozim xodimga allaqachon
/// biriktirilgan va uni hodisa formasida ikkinchi marta so'rash —
/// ortiqcha ish. Yomoni shundaki, u xato kiritishning yo'lini ham
/// ochadi: HR shoshib boshqa lavozimni tanlashi mumkin va yozuv
/// haqiqatdan uzoqlashardi.
/// </para>
/// <para>
/// <b>Nega baribir SAQLANADI.</b> Lavozim — voqea vaqtidagi HOLAT
/// (<c>Incident.PositionId</c> izohi): xodim keyinroq boshqa lavozimga
/// o'tkazilsa ham, hodisa yozuvi o'zgarmasligi kerak. Shu sababli u
/// ko'rsatishda joriy lavozimdan olinmaydi — yozib qo'yiladi.
/// </para>
/// <para>
/// <b>Sana bo'yicha tanlanadi, «hozirgi» bo'yicha emas.</b> Hodisa
/// kechikib kiritilishi mumkin (Excel'dan ko'chirilgan yozuvlar shunday)
/// va o'shanda xodim allaqachon boshqa joyda ishlayotgan bo'ladi. Shu
/// sababli o'sha kunni QAMRAB OLGAN munosabat izlanadi.
/// </para>
/// <para>
/// <b>Asosiy ish joyi birinchi.</b> O'rindosh xodimda bir kunda ikkita
/// munosabat kuchda bo'lishi mumkin («VIKOR + Вело унион 0,5 ставка»);
/// lavozimi bor birinchisi olinadi. Hech biri topilmasa <c>null</c> —
/// bu xato emas: maydon ixtiyoriy va ko'chirilgan yozuvlarda mehnat
/// munosabati umuman bo'lmasligi mumkin.
/// </para>
/// </remarks>
public sealed class IncidentPositionResolver
{
    private readonly IEmploymentRepository _employments;

    public IncidentPositionResolver(IEmploymentRepository employments)
    {
        _employments = employments;
    }

    public async Task<Guid?> ResolveAsync(
        Guid employeeId,
        DateOnly on,
        CancellationToken cancellationToken = default)
    {
        var employments = await _employments
            .GetByEmployeeAsync(employeeId, cancellationToken)
            .ConfigureAwait(false);

        return employments
            .Where(employment =>
                employment.HireDate <= on
                && (employment.TerminationDate is null || employment.TerminationDate >= on))
            .OrderByDescending(employment => employment.IsPrimary)
            .ThenByDescending(employment => employment.HireDate)
            .Select(employment => employment.PositionId)
            .FirstOrDefault(positionId => positionId is not null);
    }
}
