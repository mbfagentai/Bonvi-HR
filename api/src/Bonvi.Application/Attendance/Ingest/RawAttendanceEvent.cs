using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// Har qanday tahlilchi chiqaradigan yagona ko'rinish — bitta o'tish, qurilma aytganidek.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu yerda talqin YO'Q.</b> Yo'nalish — qurilma aytgani (aytmasa
/// <see cref="PassDirection.Unknown"/>); qurilma rolidan xulosa chiqarish
/// buyruq ishlovchisining ishi, chunki rol qurilma yozuvida, tahlilchi esa
/// qurilmani ko'rmaydi. Takror kaliti ham shu sababdan bu yerda emas
/// (SPEC §3.6).
/// </para>
/// <para>
/// <see cref="OccurredAt"/> — vaqt BITTA joyda aylantiriladi: tahlilchi
/// siljishni saqlagan holda beradi, siljish yo'q bo'lsa Toshkent (+5) ni
/// qo'yib <see cref="OffsetAssumed"/> ni belgilaydi. UTC ga o'tkazish va
/// Toshkent sanasini hisoblash <c>AttendancePass</c> ichida.
/// </para>
/// </remarks>
/// <param name="RawPersonId">Qurilmadagi shaxs raqami, bo'sh joylarsiz.</param>
/// <param name="OccurredAt">O'tish lahzasi — qurilma bergan siljish bilan.</param>
/// <param name="OffsetAssumed">Qurilma siljish bermadi va Toshkent taxmin qilindi.</param>
/// <param name="Direction">Qurilma aytgan yo'nalish.</param>
/// <param name="DirectionSource">Yo'nalish qurilmaning o'zidanmi (<c>Device</c>) yoki noma'lummi.</param>
/// <param name="Method">Shaxs qanday tasdiqlangan.</param>
/// <param name="ExternalEventId">Qurilmaning hodisa raqami (Hikvision <c>serialNo</c>), bo'lsa.</param>
/// <param name="PhotoBytes">Yuz surati baytlari, bo'lsa. Hali TEKSHIRILMAGAN.</param>
/// <param name="PhotoContentType">Surat e'lon qilgan turi — faqat ma'lumot uchun, unga ishonilmaydi.</param>
/// <param name="RawEventJson">Manba hodisaning JSON matni — <c>raw_event</c> ustuniga.</param>
public sealed record RawAttendanceEvent(
    string RawPersonId,
    DateTimeOffset OccurredAt,
    bool OffsetAssumed,
    PassDirection Direction,
    DirectionSource DirectionSource,
    PassMethod Method,
    string? ExternalEventId,
    byte[]? PhotoBytes,
    string? PhotoContentType,
    string? RawEventJson);
