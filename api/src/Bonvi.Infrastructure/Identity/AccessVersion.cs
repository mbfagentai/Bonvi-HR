namespace Bonvi.Infrastructure.Identity;

/// <summary>
/// Kirish huquqlari keshining versiyasi — rol yoki hisob o'zgarganda bir pog'ona oshadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kalitni o'chirish emas, versiya.</b> Rol tahriri shu rolning
/// BARCHA egalariga tegadi, ularning ro'yxatini esa kesh bilmaydi. Versiya
/// esa kesh kalitining bir qismi: u oshgan zahoti eski yozuvlarning
/// hammasi bir yo'la «ko'rinmas» bo'lib qoladi va o'z muddati (≤ 30 s)
/// bilan xotiradan tushib ketadi. Qaysi hisob qaysi rolda ekanini
/// hisoblashning hojati yo'q — xato qilish joyi ham yo'q.
/// </para>
/// <para>
/// <b>Singleton va jarayon ichida.</b> <see cref="Bonvi.Application.Abstractions.IDataChangeSignal"/>
/// bilan bir xil chegara: API bitta konteynerda ishlaydi. Ikkinchi nusxa
/// ko'tarilsa, u yerda o'zgarish ko'pi bilan kesh muddati (30 s) kechikib
/// yetib boradi — xavfsizlik teshigi emas, kechikish.
/// </para>
/// </remarks>
public sealed class AccessVersion
{
    private long _current;

    /// <summary>Joriy versiya. Ma'nosi yo'q son — faqat kesh kaliti uchun.</summary>
    public long Current => Interlocked.Read(ref _current);

    /// <summary>Barcha keshlangan huquqlarni eskiradi.</summary>
    public void Bump() => Interlocked.Increment(ref _current);
}
