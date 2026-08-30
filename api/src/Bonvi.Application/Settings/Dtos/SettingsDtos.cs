using Bonvi.Domain.Settings;

namespace Bonvi.Application.Settings.Dtos;

/// <summary>
/// Bitta sozlama — joriy qiymati va TA'RIFI bilan birga.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega ta'rif ham javobda.</b> Sozlamalar ekrani har bir maydonni
/// o'zi chizadi: son uchun raqamli maydon va chegaralar, matn uchun ko'p
/// qatorli maydon. Bu bilim frontendda qattiq yozilsa, yangi sozlama
/// qo'shilganda IKKI joyni (bu yerdagi ro'yxat va ekran kodi)
/// o'zgartirish kerak bo'lardi — biri unutilsa, sozlama umuman
/// ko'rinmasdi yoki chegarasiz maydon bo'lib chizilardi.
/// </para>
/// <para>
/// <b>Nega <paramref name="DefaultValue"/> ham beriladi.</b> Ekranda
/// «sukut qiymatga qaytarish» tugmasi bo'lishi tabiiy va u qiymatni
/// bilishi kerak. Bundan tashqari, foydalanuvchi «7» ni o'zi yozganmi
/// yoki u tizimning taklifi ekanini ko'rib turadi.
/// </para>
/// </remarks>
/// <param name="Key">Barqaror kalit (<c>bot.about.latin</c>).</param>
/// <param name="Value">
/// Joriy qiymat. Sozlama hech qachon o'zgartirilmagan bo'lsa —
/// <paramref name="DefaultValue"/> ning O'ZI (javobda «yozilgan» va
/// «sukut» qiymat ATAYLAB farq qilmaydi: mijozga har doim TIZIM
/// ISHLATAYOTGAN qiymat ko'rsatiladi).
/// </param>
/// <param name="Kind">Turi: <c>Text</c> yoki <c>Number</c>.</param>
/// <param name="DefaultValue">Sukut qiymat — matn ko'rinishida.</param>
/// <param name="Min">Sonning quyi chegarasi. Matnda <c>null</c>.</param>
/// <param name="Max">Sonning yuqori chegarasi. Matnda <c>null</c>.</param>
/// <param name="MaxLength">Matnning eng katta uzunligi. Sonda <c>null</c>.</param>
public sealed record SystemSettingDto(
    string Key,
    string Value,
    SystemSettingKind Kind,
    string DefaultValue,
    int? Min,
    int? Max,
    int? MaxLength);

/// <summary>
/// Telegram bot o'qiydigan sozlamalar — TIPLASHGAN ko'rinishda.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega umumiy ro'yxat emas.</b> Bot uchun sozlama — bu kalit-qiymat
/// jadvali emas, uchta aniq parametr. Umumiy ro'yxat berilganda bot
/// javobni o'zi kavlab, kalitlarni izlab, sonni matndan o'zi o'girishi
/// kerak bo'lardi — ya'ni <see cref="SystemSettingKeys"/> dagi bilim
/// bot koduda IKKINCHI marta paydo bo'lardi. Bu yerda esa u serverda
/// qoladi va botga tayyor qiymatlar keladi.
/// </para>
/// <para>
/// <b>Matn hech qachon <c>null</c> emas</b> — bo'sh satr bo'lishi
/// mumkin. Bo'sh matnda bot «Kompaniya haqida» tugmasini UMUMAN
/// chizmaydi (<c>BotOptions.AboutCyrillic</c> izohi), ya'ni bo'shlik
/// botning o'zi uchun ma'noli qiymat.
/// </para>
/// </remarks>
/// <param name="AboutCyrillic">«Kompaniya haqida» matni — kirillda.</param>
/// <param name="AboutLatin">
/// «Kompaniya haqida» matni — lotinda. Bo'sh bo'lsa bot kirilldagisini
/// ko'rsatadi (bu qoida BOTDA, bu yerda emas: server matnni o'zgartirib
/// yubormaydi, aks holda HR lotin matnini bo'shatolmasdi).
/// </param>
/// <param name="ConversationTimeoutMinutes">Yarim qolgan suhbat necha daqiqadan keyin unutiladi.</param>
public sealed record BotSettingsDto(
    string AboutCyrillic,
    string AboutLatin,
    int ConversationTimeoutMinutes);
