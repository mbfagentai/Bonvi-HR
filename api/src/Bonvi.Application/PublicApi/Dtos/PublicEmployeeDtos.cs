using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.PublicApi.Dtos;

/// <summary>
/// Ma'lumotnoma yozuvining ommaviy ko'rinishi: identifikator va nom.
/// </summary>
/// <remarks>
/// Faol/faolsiz belgisi, izoh va tartib raqami ATAYLAB yo'q — ular ichki
/// ma'lumotnoma ekranining tafsilotlari va tashqi iste'molchiga hech
/// narsa demaydi.
/// </remarks>
public sealed record PublicRefDto(Guid Id, string Name);

/// <summary>
/// <c>/reference/*</c> javobining bitta qatori.
/// </summary>
/// <remarks>
/// <paramref name="ParentId"/> faqat BO'LIMLARDA to'ladi (ular daraxt),
/// qolgan uchta ma'lumotnomada u har doim <c>null</c>. Uchta bir xil
/// javobga uchta alohida tip yasash o'rniga bitta yozuv qoldirilgan:
/// shakl bir xil bo'lsa, iste'molchida ham bitta o'quvchi yetadi.
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nomi.</param>
/// <param name="ParentId">Ota bo'lim (faqat bo'limlarda).</param>
public sealed record PublicReferenceDto(Guid Id, string Name, Guid? ParentId);

/// <summary>
/// Xodimning bitta mehnat munosabati — ommaviy ko'rinish.
/// </summary>
/// <remarks>
/// ⚠️ <b>Oklad bu yerda YO'Q va bo'lishi ham mumkin emas</b> — u ichki
/// tizimda ham alohida ruxsat ostida (<c>salary.view</c>). Tashqi
/// iste'molchiga faqat tashkiliy ma'lumot beriladi: qaysi firma, qaysi
/// bo'lim, qaysi lavozim va qanday stavka.
/// </remarks>
/// <param name="Id">Munosabat identifikatori.</param>
/// <param name="LegalEntity">Yuridik shaxs (holdingdagi firma).</param>
/// <param name="Department">Bo'lim. Ko'rsatilmagan bo'lishi mumkin.</param>
/// <param name="Position">Lavozim. Ko'rsatilmagan bo'lishi mumkin.</param>
/// <param name="Rate">Stavka: 1.0 — to'liq, 0.5 — yarim (o'rindoshlik).</param>
/// <param name="Status">Holati: <c>Probation</c> | <c>Active</c> | <c>Terminated</c>.</param>
/// <param name="HireDate">Ishga kirgan sana.</param>
/// <param name="TerminationDate">Ishdan ketgan sana.</param>
/// <param name="IsPrimary">Asosiy ish joyimi — yuqoridagi maydonlar shundan olinadi.</param>
public sealed record PublicEmploymentDto(
    Guid Id,
    PublicRefDto? LegalEntity,
    PublicRefDto? Department,
    PublicRefDto? Position,
    decimal Rate,
    EmploymentStatus Status,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    bool IsPrimary);

/// <summary>
/// Xodim — tashqi loyihalarga ochiladigan YAGONA shakl.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu tipni kengaytirishdan oldin SPEC §1 ni o'qing.</b> Bu yerga
/// qo'shilgan har bir maydon holdingning boshqa loyihalariga — ya'ni
/// BonviHR nazorat qilmaydigan bazalarga va loglarga — ko'chib o'tadi.
/// Quyidagilar ATAYLAB yo'q va hech qachon qo'shilmaydi:
/// oklad, JSHSHIR (<c>pinfl</c>), pasport, manzil, mutaxassislik,
/// sudlanganlik (<c>hasCriminalRecord</c>, <c>criminalRecordNote</c>),
/// mehnat.uz havolasi va skrinshoti, «tuzatish kerak» belgisi va uning
/// izohi (<c>needsReview</c>, <c>reviewNote</c>), belgilar
/// (rezerv / qora ro'yxat / nazorat), ikkinchi telefon.
/// </para>
/// <para>
/// Bu qoidani <c>PublicApiLeakTests</c> qo'riqlaydi: u to'ldirilgan
/// xodimni SHU endpoint orqali seriyalaydi va XOM JSON ichida o'sha
/// qiymatlar ham, maydon nomlari ham yo'qligini tekshiradi. Test DTO
/// shakliga emas, javob matniga qaraydi — shu sababli uni chetlab
/// o'tib bo'lmaydi.
/// </para>
/// <para>
/// <b>Rasm bu yerda YO'Q</b>, faqat <paramref name="PhotoKey"/>: u ikki
/// ish qiladi — rasm bor-yo'qligini bildiradi va mijozdagi keshni
/// eskirtiradi. Baytlar alohida marshrutdan keladi
/// (<c>/employees/{id}/photo</c>), ichki karta bilan bir xil sabab
/// (<c>EmployeeDto.PhotoKey</c> izohi).
/// </para>
/// </remarks>
/// <param name="Id">Xodim identifikatori — iste'molchi tomonda barqaror kalit.</param>
/// <param name="FullName">To'liq F.I.Sh.</param>
/// <param name="ShortName">Qisqa ko'rinish: «Karimov A.B.».</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi.</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Gender">Jins.</param>
/// <param name="Phone">Asosiy telefon raqami.</param>
/// <param name="TelegramUrl">Telegram havolasi.</param>
/// <param name="PhotoKey">Rasm kaliti — borligini bildiradi va keshni eskirtiradi.</param>
/// <param name="Status">ASOSIY mehnat munosabatining holati.</param>
/// <param name="HireDate">Asosiy munosabatning boshlanish sanasi.</param>
/// <param name="TerminationDate">Asosiy munosabatning tugash sanasi.</param>
/// <param name="Employments">
/// YAKUNLANMAGAN mehnat munosabatlari, yangisidan eskisiga
/// (sababi <c>IPublicApiReadModel</c> izohida). Ishdan ketgan xodimda
/// bo'sh bo'ladi — u holda yuqoridagi uch maydon javob beradi.
/// </param>
/// <param name="Directions">Biznes yo'nalishlari.</param>
/// <param name="UpdatedAt">
/// Yozuv oxirgi marta qachon o'zgargan — <c>updatedSince</c> bilan
/// bosqichma-bosqich sinxronlash uchun. Xodim kartasi va uning mehnat
/// munosabatlaridan ENG KEYINGISI olinadi.
/// </param>
public sealed record PublicEmployeeDto(
    Guid Id,
    string FullName,
    string ShortName,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly? BirthDate,
    Gender? Gender,
    string? Phone,
    string? TelegramUrl,
    string? PhotoKey,
    EmploymentStatus? Status,
    DateOnly? HireDate,
    DateOnly? TerminationDate,
    IReadOnlyList<PublicEmploymentDto> Employments,
    IReadOnlyList<PublicRefDto> Directions,
    DateTimeOffset UpdatedAt);

/// <summary>
/// O'chirilgan xodim — iste'molchi o'z nusxasidan olib tashlashi uchun.
/// </summary>
/// <remarks>
/// Faqat identifikator va o'chirilgan vaqt: o'chirilgan yozuvdan boshqa
/// hech nima berilmaydi. Nom yoki telefon qaytarilsa, o'chirish amali
/// ma'nosini yo'qotardi.
/// </remarks>
public sealed record PublicDeletionDto(Guid Id, DateTimeOffset DeletedAt);

/// <summary>
/// <c>/ping</c> javobi — integratsiyani tekshirishning eng arzon yo'li.
/// </summary>
/// <remarks>
/// Kalit NOMI qaytadi, prefiksi emas: iste'molchi «men qaysi kalit bilan
/// keldim» degan savolga javob olishi kerak, kalitning qismlari esa unga
/// kerak emas. <paramref name="ServerTime"/> — UTC: soat farqi bilan
/// bog'liq muammolarni birinchi so'rovning o'zida ko'rsatadi.
/// </remarks>
public sealed record PublicPingDto(bool Ok, string Key, DateTimeOffset ServerTime);
