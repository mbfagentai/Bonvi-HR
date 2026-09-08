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
/// <para>
/// ⚠️ <see cref="SystemSettingKind.Secret"/> turida bu maydon HAR DOIM
/// bo'sh. Sir javobda hech qachon qaytmaydi — hatto uni qo'ygan super
/// administratorga ham. Buning o'rniga <paramref name="IsSet"/> va
/// <paramref name="Preview"/> beriladi.
/// </para>
/// </param>
/// <param name="Kind">
/// Turi: <c>Text</c>, <c>Number</c>, <c>Secret</c> yoki <c>Photo</c>.
/// <para>
/// <see cref="SystemSettingKind.Hidden"/> bu yerda HECH QACHON
/// uchramaydi: yashirin sozlamalar javobga umuman qo'shilmaydi
/// (<c>SystemSettingKeys.Visible</c>).
/// </para>
/// </param>
/// <param name="DefaultValue">Sukut qiymat — matn ko'rinishida.</param>
/// <param name="Min">Sonning quyi chegarasi. Matnda <c>null</c>.</param>
/// <param name="Max">Sonning yuqori chegarasi. Matnda <c>null</c>.</param>
/// <param name="MaxLength">Matnning eng katta uzunligi. Sonda <c>null</c>.</param>
/// <param name="IsSet">
/// Qiymat ATAYLAB qo'yilganmi (bazada yozuv bormi).
/// <para>
/// Matn va son uchun bu ma'lumot ortiqcha — ularda sukut qiymat ham
/// haqiqiy qiymat. Sir uchun esa bu YAGONA ko'rsatkich: qiymatning
/// o'zi qaytmaydi, ya'ni «token qo'yilganmi?» degan savolga boshqa
/// javob yo'q.
/// </para>
/// </param>
/// <param name="Preview">
/// Sirning niqoblangan ko'rinishi — masalan <c>8123456789:…mQ4t</c>.
/// Qo'yilmagan sirda va sir bo'lmagan sozlamada <c>null</c>.
/// <para>
/// NEGA umuman ko'rsatiladi: server bir nechta bo'lishi mumkin (sinov
/// va ishchi), token esa almashtiriladi. «Qaysi token turibdi?» degan
/// savolga javobsiz qolgan odam eng oddiy yo'lni tanlaydi — tokenni
/// har ehtimolga qarshi qayta qo'yadi.
/// </para>
/// <para>
/// NEGA aynan shu bo'laklar OCHIQ: ikki nuqtagacha bo'lgan qism —
/// botning identifikatori va u sir emas (uni har qanday odam
/// <c>@BotFather</c> orqali yoki botning o'zi bilan yozishib bilib
/// oladi). Oxirgi to'rt belgi esa tokenlarni bir-biridan ajratish
/// uchun yetadi va qolgan ~30 belgini taxmin qilishga hech narsa
/// bermaydi.
/// </para>
/// </param>
public sealed record SystemSettingDto(
    string Key,
    string Value,
    SystemSettingKind Kind,
    string DefaultValue,
    int? Min,
    int? Max,
    int? MaxLength,
    bool IsSet,
    string? Preview);

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
/// <param name="AboutRussian">
/// «Kompaniya haqida» matni — ruscha. Bo'sh bo'lsa bot kirilldagisini
/// ko'rsatadi, lotin bilan bir xil qoida.
/// </param>
/// <param name="ContactsCyrillic">Kontaktlar va manzil — kirillda.</param>
/// <param name="ContactsLatin">Kontaktlar va manzil — lotinda.</param>
/// <param name="ContactsRussian">Kontaktlar va manzil — ruscha.</param>
/// <param name="Location">
/// Bosh ofisning xaritadagi nuqtasi, «kenglik,uzunlik». Bo'sh bo'lsa
/// bot xarita yubormaydi va kontakt matni bilan cheklanadi.
/// </param>
/// <param name="InterviewPhone">
/// Suhbat taklifidagi mas'ul telefon. Bo'sh bo'lsa xabarda telefon qatori
/// chizilmaydi.
/// </param>
/// <param name="InterviewAddress">
/// Suhbat taklifidagi manzil yorlig'i («Qo'qon»). Bo'sh bo'lsa manzil
/// qatori chizilmaydi — xarita esa <paramref name="Location"/> dan
/// baribir ketadi.
/// </param>
/// <param name="HasLogo">
/// Logotip qo'yilganmi. Bayt EMAS, faqat bayroq: bot rasmni alohida
/// so'rov bilan oladi va uni Telegram'ga BIR MARTA yuklab, keyin
/// <c>file_id</c> ni qayta ishlatadi.
/// </param>
/// <param name="Instagram">Instagram havolasi. Bo'sh — tugma chizilmaydi.</param>
/// <param name="Telegram">Telegram kanali havolasi.</param>
/// <param name="Facebook">Facebook sahifasi havolasi.</param>
/// <param name="Website">Kompaniya sayti havolasi.</param>
/// <param name="OfferUrl">
/// Ommaviy oferta havolasi — anketaning oxirgi qadamida ko'rsatiladi.
/// <para>
/// Bo'sh bo'lsa bot oferta qadamini UMUMAN chizmaydi va anketa bir
/// qadam qisqaradi (qoida BOTDA, bu yerda emas — server matnni ham,
/// bo'shlikni ham o'zgartirmasdan uzatadi). Rozilik so'ralmaydi ham:
/// o'qib bo'lmaydigan hujjatga rozilik olish huquqiy jihatdan hech
/// narsa bermaydi.
/// </para>
/// </param>
/// <param name="ConversationTimeoutMinutes">Yarim qolgan suhbat necha daqiqadan keyin unutiladi.</param>
public sealed record BotSettingsDto(
    string AboutCyrillic,
    string AboutLatin,
    string AboutRussian,
    string ContactsCyrillic,
    string ContactsLatin,
    string ContactsRussian,
    string Location,
    string InterviewPhone,
    string InterviewAddress,
    bool HasLogo,
    string Instagram,
    string Telegram,
    string Facebook,
    string Website,
    string OfferUrl,
    int ConversationTimeoutMinutes);

/// <summary>Yuklangan logotipning saqlash kaliti.</summary>
/// <remarks>
/// Kalit ekranga QAYTARILADI: rasm manzili o'zgarganini bilgan brauzer
/// eski nusxani keshdan olmaydi (`AccountPhotoDto` bilan bir xil fikr).
/// </remarks>
/// <param name="Key">Fayl kaliti.</param>
public sealed record BotLogoDto(string Key);

/// <summary>
/// Botning Telegram tokeni — FAQAT bot jarayoniga.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega u <see cref="BotSettingsDto"/> ga qo'shilmadi.</b> O'sha
/// javob <c>recruiting.view</c> ruxsati ostida beriladi va bu ruxsat HR
/// menejerlarida ham bor. Token o'sha yerga qo'shilsa, uni har bir HR
/// menejeri o'qiy olardi — holbuki tokenni QO'YGAN super administratorning
/// o'zi ham uni qaytarib o'qiy olmaydi. Sir uchun bundan kulgili holat
/// bo'lmasdi.
/// </para>
/// <para>
/// Shu sababli token alohida endpoint'da va u ruxsat bilan emas, ROL
/// bilan himoyalangan: uni faqat <c>recruiting_bot</c> xizmat hisobi
/// ola oladi (sabab <c>SettingsEndpoints</c> da).
/// </para>
/// </remarks>
/// <param name="Token">
/// Ochiq token. Ekranda qo'yilmagan bo'lsa <c>null</c> — bunda bot
/// <c>.env</c> dagi zaxira qiymatga tushadi
/// (<c>SystemSettingKeys.BotToken</c> izohi).
/// </param>
public sealed record BotTokenDto(string? Token);
