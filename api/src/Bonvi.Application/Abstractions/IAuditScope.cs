namespace Bonvi.Application.Abstractions;

/// <summary>
/// HTTP'siz yozuvlarga nom va guruh beradi: «bu o'zgarishlarni qaysi tizim amali qildi».
/// </summary>
/// <remarks>
/// <para>
/// Ishga tushishdagi urug'lar (<c>startup:identity-seed</c>, <c>startup:first-admin</c>…),
/// fon vazifalari (<c>job:…</c>) va ETL qadamlari (<c>etl:employees</c>) shu bilan
/// o'raladi. Usiz ularning audit qatorlari «tizim, noma'lum amal» bo'lib qolardi.
/// </para>
/// <para>
/// Ichma-ich chaqirish mumkin: qaytarilgan obyekt yopilganda oldingi qiymatlar
/// tiklanadi. Har bir chaqiruv YANGI guruh (<see cref="IRequestContext.CorrelationId"/>)
/// ochadi.
/// </para>
/// </remarks>
public interface IAuditScope
{
    /// <summary>Amal nomini va yangi guruhni shu qamrov uchun o'rnatadi.</summary>
    /// <param name="operation">Masalan <c>startup:catalog-seed</c>.</param>
    /// <returns>Yopilganda oldingi holat tiklanadi.</returns>
    IDisposable Use(string operation);
}
