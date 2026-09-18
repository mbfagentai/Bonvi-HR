namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Qabul yo'lining o'lchov chegaralari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>AttendanceRules</c> da emas.</b> U yerdagi sonlar ikki tomondan
/// ishlatiladi — qabul ham, tabel hisoblash ham (masalan soat og'ishi).
/// Bu yerdagilar esa faqat SO'ROVNING shaklini cheklaydi va tabelga hech
/// qanday ta'sir qilmaydi. So'rov tanasining umumiy chegarasi
/// (<c>AttendanceRules.MaxIngestBodyBytes</c>) o'sha yerda qoldi, chunki uni
/// Api qatlami ham, domen izohi ham tilga oladi.
/// </para>
/// </remarks>
public static class IngestLimits
{
    /// <summary>Bitta so'rovdagi eng ko'p hodisa.</summary>
    /// <remarks>
    /// Hikvision har bir o'tishni ALOHIDA so'rovda yuboradi; bu chegara faqat
    /// oddiy JSON (vositachi agent, ZKTeco moslamasi) uchun. 200 — agent
    /// aloqa uzilib qolgandan keyin to'plangan navbatni bir necha so'rovda
    /// bo'shatishi uchun yetarli, lekin bitta so'rovning ishlash vaqtini
    /// (har bir hodisa alohida yoziladi) bir soniya atrofida ushlab turadi.
    /// </remarks>
    public const int MaxEventsPerRequest = 200;

    /// <summary>Bitta yuz suratining eng katta hajmi (bayt).</summary>
    /// <remarks>
    /// 1 MB. Terminal surati odatda 20–80 KB; bundan kattasi surat emas,
    /// noto'g'ri sozlangan qurilma. Chegaradan oshgan surat TASHLANADI,
    /// o'tishning o'zi esa baribir yoziladi — suratsiz o'tish ham davomat.
    /// </remarks>
    public const int MaxPhotoBytes = 1_048_576;

    /// <summary>Qurilma bergan hodisa raqamining eng katta uzunligi.</summary>
    /// <remarks>
    /// Raqam takrorni ushlash kalitiga (<c>dedupe_key</c>, 200 belgi) «s:»
    /// oldqo'shimchasi bilan tushadi. Hikvision <c>serialNo</c> — o'nlik son,
    /// ya'ni 100 belgi har qanday haqiqiy qurilma uchun katta zaxira.
    /// Undan uzuni hodisa raqami emas, buzuq tana — so'rov rad etiladi.
    /// </remarks>
    public const int MaxExternalEventIdLength = 100;

    /// <summary>Multipart tanadagi eng ko'p qism.</summary>
    /// <remarks>
    /// Hikvision ikki, ba'zan uch qism yuboradi (hodisa, surat, qo'shimcha
    /// surat). Chegara 2 MB ichida minglab mayda qism yasab xotirani
    /// band qilishga yo'l qo'ymaydi.
    /// </remarks>
    public const int MaxMultipartParts = 10;
}
