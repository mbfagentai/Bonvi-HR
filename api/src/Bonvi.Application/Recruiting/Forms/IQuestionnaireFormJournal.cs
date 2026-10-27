namespace Bonvi.Application.Recruiting.Forms;

/// <summary>
/// Onlayn anketalar jurnali — port.
/// </summary>
/// <remarks>
/// Nega port, <c>ILogger</c> emas: Application qatlami loglash kutubxonasiga
/// bog'lanmagan (<c>IAttendanceJobJournal</c> bilan bir xil sabab). ⚠️ Faqat
/// anketa RAQAMI va xato kodi — ism, telefon va javoblar HECH QACHON
/// (ularda sudlanganlik va sog'liq haqidagi ma'lumot bor).
/// </remarks>
public interface IQuestionnaireFormJournal
{
    /// <summary>Anketa saqlandi, lekin PDF nomzod fayllariga biriktirilmadi.</summary>
    /// <param name="formNumber">«Anketa №».</param>
    /// <param name="reasonCode">Sabab — domen xato kodi (masalan <c>nomzod.rezyume_kop</c>).</param>
    void PdfNotAttached(int formNumber, string reasonCode);
}
