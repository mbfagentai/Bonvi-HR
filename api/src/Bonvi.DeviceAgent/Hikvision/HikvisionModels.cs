namespace Bonvi.DeviceAgent.Hikvision;

/*
  ISAPI javoblarining FAQAT kerakli maydonlari.

  Terminal o'nlab maydon qaytaradi (kartalar, barmoq izlari, huquq
  rejalari), lekin ularning birortasi ham agentga kerak emas. To'liq
  modelni yozish firmware yangilanganda uzilib turadigan katta yuzani
  ochardi; bu yerdagilari esa 2026-09-21 da TIRIK qurilmada tekshirilgan
  (DS-K1T341AMF, fw V3.7.0).
*/

/// <summary>Kirish nazorati hodisasi — o'tish yoki eshik holati.</summary>
/// <param name="SerialNo">Terminalning ichki tartib raqami — kursor shu bo'yicha.</param>
/// <param name="Time">Hodisa vaqti, terminalning MAHALLIY siljishi bilan.</param>
/// <param name="EmployeeNoString">
/// Shaxs raqami. Bo'sh bo'lishi NORMAL: eshik ochilishi va tanilmagan
/// yuz ham shu turkumda keladi va ular o'tish EMAS.
/// </param>
/// <param name="CurrentVerifyMode">Tasdiqlash usuli — «face», «card», «fp» satr ichida.</param>
internal sealed record AcsEvent(
    long SerialNo,
    string? Time,
    string? EmployeeNoString,
    string? CurrentVerifyMode);

/// <summary>Hodisa qidiruvining bitta sahifasi.</summary>
/// <param name="InfoList">Sahifadagi hodisalar.</param>
/// <param name="ResponseStatusStrg">
/// <c>MORE</c> — yana sahifa bor; <c>OK</c> / <c>NO MATCH</c> — tugadi.
/// Sahifa oxirini FAQAT shu va <paramref name="TotalMatches"/> aytadi:
/// firmware <c>maxResults</c> dan kam (ko'pincha 30) berishi mumkin va
/// «so'ralganidan kam keldi ⇒ oxirgi sahifa» qoidasi qoldiqni jimgina
/// tashlab ketardi (<c>HikvisionClient.HasMore</c>).
/// </param>
/// <param name="TotalMatches">Qidiruvga mos jami yozuvlar.</param>
internal sealed record AcsEventPayload(
    IReadOnlyList<AcsEvent>? InfoList,
    string? ResponseStatusStrg = null,
    int? TotalMatches = null);

internal sealed record AcsEventResponse(AcsEventPayload? AcsEvent);

/// <summary>Terminaldagi odam.</summary>
/// <param name="EmployeeNo">Shaxs raqami.</param>
/// <param name="Name">Terminaldagi ism.</param>
/// <param name="NumOfFace">
/// Yozilgan yuzlar soni. Firmware bu maydonni bermasligi ham mumkin —
/// o'shanda <c>null</c> va agent «yuzi bor» deb hisoblaydi: yo'q joydan
/// yuz qayta yozish har tekshiruvda takrorlanardi. Haqiqatan yo'q bo'lsa,
/// kartadagi «Qayta yozish» buni tuzatadi.
/// </param>
internal sealed record DevicePerson(string EmployeeNo, string? Name, int? NumOfFace);

internal sealed record UserInfoItem(string? EmployeeNo, string? Name, int? NumOfFace);

/// <summary>Odamlar qidiruvining bitta sahifasi — maydonlar <see cref="AcsEventPayload"/> dagidek.</summary>
internal sealed record UserInfoSearchPayload(
    IReadOnlyList<UserInfoItem>? UserInfo,
    string? ResponseStatusStrg = null,
    int? TotalMatches = null);

internal sealed record UserInfoSearchResponse(UserInfoSearchPayload? UserInfoSearch);
