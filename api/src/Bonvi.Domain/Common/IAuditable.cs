namespace Bonvi.Domain.Common;

/// <summary>
/// Kuzatiladigan yozuv: kim yaratdi, kim o'zgartirdi, qachon.
/// Qiymatlar avtomatik to'ldiriladi (Infrastructure qatlamida interceptor orqali).
/// HR ma'lumotida bu majburiy — har bir o'zgarish javobgarlikka ega.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    Guid? CreatedBy { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }

    Guid? UpdatedBy { get; set; }
}

/// <summary>
/// "Yumshoq o'chirish" — yozuv bazadan jismonan o'chirilmaydi.
/// HR ma'lumoti tarixiy hujjat: xodim o'chirilsa ham, uning buyruqlari qoladi.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }

    Guid? DeletedBy { get; set; }
}
