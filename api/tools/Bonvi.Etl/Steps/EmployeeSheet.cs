namespace Bonvi.Etl.Steps;

/// <summary>
/// Xodim kartasi joylashgan varaqning ustun tartibi.
/// </summary>
/// <remarks>
/// <para>
/// Asosiy ro'yxat va arxiv <b>deyarli</b> bir xil tuzilgan, lekin 13-ustundan
/// keyin farq qiladi: asosiyda 13 — mehnat daftarchasi (yuridik shaxs),
/// arxivda esa 13 — sudlanganlik, 14 — mehnat daftarchasi.
/// </para>
/// <para>
/// Ustunlar nom bo'yicha emas, RAQAM bo'yicha o'qiladi: bloknotdagi
/// sarlavhalar takrorlanadi, birlashtirilgan va ba'zilari umuman yo'q.
/// </para>
/// <para>
/// <b>Ustun raqami <c>0</c> — «bunday ustun yo'q»</b> degani. Excel ustunlari
/// birdan boshlanadi, shu sababli <c>ExcelRow[0]</c> doim bo'sh katak
/// qaytaradi va qoidalar hech qanday alohida tekshiruvsiz ishlayveradi:
/// bo'sh katak «ma'lumot yo'q» bilan bir xil ma'no beradi.
/// </para>
/// </remarks>
public sealed record EmployeeSheet
{
    /// <summary>Varaq nomi.</summary>
    public required string Name { get; init; }

    /// <summary>Ma'lumot boshlanadigan qator.</summary>
    public required int FirstDataRow { get; init; }

    /// <summary>F.I.Sh.</summary>
    public int FullName { get; init; } = 2;

    /// <summary>Tug'ilgan sana.</summary>
    public int BirthDate { get; init; } = 3;

    /// <summary>Ishga kirgan sana.</summary>
    public int HireDate { get; init; } = 4;

    /// <summary>Yo'nalish.</summary>
    public int Direction { get; init; } = 5;

    /// <summary>Bo'lim.</summary>
    public int Department { get; init; } = 6;

    /// <summary>Lavozim.</summary>
    public int Position { get; init; } = 7;

    /// <summary>Sinov muddati.</summary>
    public int Probation { get; init; } = 8;

    /// <summary>Yashash joyi.</summary>
    public int Address { get; init; } = 9;

    /// <summary>Pasport.</summary>
    public int Passport { get; init; } = 10;

    /// <summary>JSHSHIR.</summary>
    public int Pinfl { get; init; } = 11;

    /// <summary>Telefon.</summary>
    public int Phone { get; init; } = 12;

    /// <summary>Yuridik shaxs («Мехнат дафтарчаси»).</summary>
    public required int LegalEntity { get; init; }

    /// <summary>Sudlanganlik ma'lumoti.</summary>
    public required int CriminalRecord { get; init; }

    /// <summary>Mutaxassislik.</summary>
    public required int Speciality { get; init; }

    /// <summary>Telegram havolasi.</summary>
    public required int Telegram { get; init; }

    /// <summary>Telegram havolasining ikkinchi ustuni (arxivda ikkitasi bor).</summary>
    public int TelegramAlternate { get; init; }

    /// <summary>mehnat.uz havolasi.</summary>
    public required int MehnatUz { get; init; }

    /// <summary>
    /// Blok sarlavhalari bormi (asosiy ro'yxat 49 ta blokka bo'lingan).
    /// </summary>
    public bool HasBlockHeaders { get; init; }

    /// <summary>«91 Умумий ходимлар» — 540 ta aktiv xodim.</summary>
    public static EmployeeSheet Main { get; } = new()
    {
        Name = "91 Умумий ходимлар",
        FirstDataRow = 4,
        LegalEntity = 13,
        CriminalRecord = 18,
        Speciality = 15,
        Telegram = 16,
        MehnatUz = 19,
        HasBlockHeaders = true,
    };

    /// <summary>
    /// «91.2 Янги ишга келганлар» — 2972 qatorda 1960 ta ishga qabul.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ustun tartibi asosiy ro'yxatnikidan butunlay boshqacha, chunki bu varaq
    /// xodim kartasi emas, <b>buyruq jurnali</b>: 6-ustun buyruq raqami,
    /// 7-ustun buyruq sanasi, 11-ustun oklad. Shu sababli bitta ham standart
    /// qiymat qoldirilmaydi — hammasi ochiq yozilgan.
    /// </para>
    /// <para>
    /// <c>FirstDataRow = 5</c>: 1–4-qatorlar sarlavha va yil bloki. Varaq yillar
    /// bo'yicha bloklarga bo'lingan, lekin blok sarlavhasi 2-ustunda EMAS,
    /// 1- yoki 3-ustunda yozilgan. Shu sababli <c>HasBlockHeaders = false</c>:
    /// bunday qatorda F.I.Sh bo'sh bo'ladi va ular ismsizligi uchun o'z-o'zidan
    /// tashlab ketiladi — alohida qoida kerak emas.
    /// </para>
    /// <para>
    /// Yuridik shaxs, sudlanganlik, mutaxassislik, telegram va mehnat.uz
    /// ustunlari bu varaqda umuman yo'q — ular <c>0</c> bilan belgilangan.
    /// Yuridik shaxsni mehnat munosabatlari qadami asosiy ro'yxat va arxivdagi
    /// «Мехнат дафтарчаси» ustunidan oladi.
    /// </para>
    /// <para>
    /// <b>Ikkita ustun boshqa varaqlardagidan farq qiladi.</b> 8-ustun
    /// («йўналиш») ba'zan yo'nalish emas, korxona nomini saqlaydi
    /// (<c>"Vicor" метан МЧЖ</c> — 55 qator). 15-ustun esa sinov muddatining
    /// <b>tugash sanasi</b>, asosiy ro'yxatdagi 8-ustundagi «3 ой» kabi
    /// davomiylik emas — sinov muddatlari qadami buni hisobga olishi kerak.
    /// </para>
    /// </remarks>
    public static EmployeeSheet NewHires { get; } = new()
    {
        Name = "91.2 Янги ишга келганлар",
        FirstDataRow = 5,
        FullName = 2,
        Address = 3,
        BirthDate = 4,
        HireDate = 5,
        Direction = 8,
        Department = 9,
        Position = 10,
        Passport = 12,
        Pinfl = 13,
        Phone = 14,
        Probation = 15,
        LegalEntity = 0,
        CriminalRecord = 0,
        Speciality = 0,
        Telegram = 0,
        MehnatUz = 0,
        HasBlockHeaders = false,
    };

    /// <summary>«91.7 Архив» — 1715 ta ishdan ketgan xodim.</summary>
    public static EmployeeSheet Archive { get; } = new()
    {
        Name = "91.7 Архив",
        FirstDataRow = 3,
        LegalEntity = 14,
        CriminalRecord = 13,
        Speciality = 20,

        // 15-ustun ATAYLAB o'qilmaydi. Sarlavhada u «Телеграмм» deb
        // yozilgan, lekin 54 ta qiymatning 42 tasi — sana. Ya'ni bu yerda
        // haqiqatda mehnat daftarchasi kirgan sana yotibdi va uni telegram
        // havolasi deb o'qish 42 ta soxta hisobot satri berardi.
        Telegram = 16,
        TelegramAlternate = 19,
        MehnatUz = 17,
    };
}
