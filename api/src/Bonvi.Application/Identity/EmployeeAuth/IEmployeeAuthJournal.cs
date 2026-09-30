namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>
/// Xodim ilovasiga kirishning jurnali — port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega port, <c>ILogger</c> emas.</b> Application qatlami loglash
/// kutubxonasiga bog'lanmagan va buni arxitektura testi tekshiradi
/// (<c>IAttendanceJobJournal</c> bilan bir xil sabab).
/// </para>
/// <para>
/// ⭐ <b>Nega jurnal umuman kerak.</b> Kirish rad etilganda MIJOZGA har doim
/// bitta umumiy javob ketadi — aks holda endpoint «bu telefon raqami Bonvi
/// xodimimi?» degan savolga javob beradigan ochiq xizmatga aylanardi. Lekin
/// HR ga «nega u kira olmayapti?» degan savolga javob kerak bo'ladi va u
/// faqat shu yerda bo'lishi mumkin: hisob bormi, xodim o'chirilganmi, yoki
/// ishdan ketganmi.
/// </para>
/// <para>
/// ⚠️ Metodlarga faqat IDENTIFIKATORLAR beriladi — telefon raqami, ism yoki
/// parolning biror qismi HECH QACHON.
/// </para>
/// </remarks>
public interface IEmployeeAuthJournal
{
    /// <summary>Hisob o'chirilgan (yumshoq) xodimga tegishli — INV-1.</summary>
    /// <param name="accountId">Hisob.</param>
    void EmployeeMissing(Guid accountId);

    /// <summary>Xodimda bugunni qamrab oluvchi mehnat munosabati yo'q — INV-2.</summary>
    /// <param name="employeeId">Xodim.</param>
    void NotEmployed(Guid employeeId);

    /// <summary>Bekor qilingan yangilash tokeni qayta ishlatildi — o'g'irlik belgisi.</summary>
    /// <remarks>
    /// Ogohlantirish darajasida: bu yagona holat, undan keyin hisobning
    /// BARCHA sessiyalari yopiladi va odam qaytadan kirishga majbur bo'ladi.
    /// </remarks>
    /// <param name="accountId">Hisob.</param>
    void RefreshTokenReused(Guid accountId);

    /// <summary>Fon vazifasi hisobi yo'q xodimlarga hisob ochib bo'ldi.</summary>
    /// <param name="created">Shu yurishda ochilgan hisoblar soni.</param>
    void AccountsProvisioned(int created);
}
