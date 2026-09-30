using System.Globalization;

namespace Bonvi.Application.Attendance.Agent;

/// <summary>
/// Bonvi qurilmaga yozadigan shaxs raqamlari fazosi (SPEC-agent §C, §D1).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida faza kerak.</b> Terminal Verifix bilan BO'LIShILADI.
/// Agent qurilmadagi ro'yxatni kerakli ro'yxat bilan solishtiradi va
/// ortiqchasini o'chiradi — agar «ortiqcha» ni raqamiga qarab ajrata
/// olmasa, birinchi tekshiruvdayoq Verifix ning barcha odamlarini
/// o'chirib tashlagan bo'lardi. Shu sababli qoida qat'iy: agent FAQAT
/// shu fazadagi raqamni o'chiradi yoki o'zgartiradi.
/// </para>
/// <para>
/// Faza: <b>9 bilan boshlanadigan 9 belgili raqam</b>. Verifix ning o'z
/// raqamlari qisqa tartib sonlar (1, 2, 17…), ya'ni kesishmaydi. Uzunlik
/// ham shartning bir qismi: bitta «9» raqamli Verifix odami tasodifan
/// bizniki bo'lib qolmaydi.
/// </para>
/// <para>
/// ⚠️ Tekshiruv RAQAMNI talab qilmaydi, faqat uzunlik va birinchi
/// belgini. Sabab tarixiy va ataylab: birinchi uchta xodim vaqtinchalik
/// skript bilan yozilgan va ularning raqami <c>9</c> + xodim
/// identifikatorining oxirgi 8 belgisidan tuzilgan (<c>93EAF62FD</c>).
/// Ular ham shu fazaga tegishli va qayta raqamlanmasligi kerak — aks
/// holda qurilmadagi odam ikki marta paydo bo'lardi.
/// </para>
/// </remarks>
public static class DevicePersonNo
{
    /// <summary>Fazaning birinchi belgisi.</summary>
    public const char Prefix = '9';

    /// <summary>Raqamning uzunligi — prefiks bilan birga.</summary>
    public const int Length = 9;

    /// <summary>Raqam Bonvi fazasiga tegishlimi.</summary>
    public static bool IsManaged(string? personNo)
        => personNo is { Length: Length } value && value[0] == Prefix;

    /// <summary>Eng katta tartib soni — 8 xona.</summary>
    public const long MaxSequence = 99_999_999;

    /// <summary>Tartib sonidan raqam yasaydi.</summary>
    /// <remarks>
    /// Ketma-ketlik bazadan keladi (<c>IDevicePersonNumbers</c>), shakl esa
    /// shu yerda: ikkalasini bitta joyda saqlash bazaga Bonvi qoidasini
    /// bilib qo'yishni yuklardi.
    /// </remarks>
    public static string From(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, MaxSequence);

        return string.Create(CultureInfo.InvariantCulture, $"{Prefix}{sequence:D8}");
    }

    /// <summary>
    /// Xodimning mavjud raqamlaridan qurilmaga yoziladiganini tanlaydi.
    /// </summary>
    /// <remarks>
    /// FAQAT Bonvi fazasidagisi qaytadi. Xodimda Verifix davridan qolgan
    /// raqam ham bo'lishi mumkin, lekin u bilan yozilgan odam agentning
    /// fazasidan tashqarida qolardi — ya'ni keyin hech qachon yangilanmas
    /// va o'chirilmas edi. Fazada raqam yo'q bo'lsa <c>null</c>:
    /// chaqiruvchi yangisini ajratadi.
    /// </remarks>
    public static string? Prefer(IEnumerable<string>? personNumbers)
        => personNumbers?.FirstOrDefault(IsManaged);
}
