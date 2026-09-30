using System.Text.Json;

namespace Bonvi.DeviceAgent;

/// <summary>
/// Hodisa kursori — terminalning oxirgi o'qilgan tartib raqami.
/// </summary>
/// <remarks>
/// <para>
/// Faylda saqlanadi, bazada emas: agent bazani umuman ko'rmaydi (loyiha
/// faylidagi qoida). Fayl yo'qolsa falokat emas — agent oxirgi soatni
/// qaytadan o'qiydi va takror o'tishlar serverda
/// <c>ux_attendance_passes_device_dedupe</c> bilan tashlanadi.
/// </para>
/// <para>
/// ⚠️ Yozish ATOMAR: avval vaqtinchalik faylga, keyin o'rniga
/// ko'chiriladi. Aks holda konteyner yozish o'rtasida o'chsa, faylda
/// yarim JSON qolardi va agent keyingi safar butun jurnalni qaytadan
/// o'qib, minglab takror hodisa yuborardi.
/// </para>
/// </remarks>
internal sealed class AgentState
{
    /// <summary>Uzatilgan hodisalarning eng katta tartib raqami.</summary>
    public long LastSerialNo { get; set; }

    /// <summary>
    /// Uzatilgan hodisalarning eng kech vaqti — TERMINAL soatida, u yozganidek.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Qidiruv shu nuqtadan boshlanadi, agent soatidan emas.</b>
    /// Ilgari oraliq «agentning hozirgi vaqti − 1 daqiqa» edi: terminal
    /// soati bir daqiqadan ko'proq orqada yursa, yangi o'tish hech qaysi
    /// oraliqqa tushmasdi va uzilishsiz ham jimgina yo'qolardi.
    /// Terminal o'z vaqtini o'zi bilan solishtirganda adashmaydi.
    /// </para>
    /// <para>
    /// Shu vaqtdan KEYINGI har qanday hodisa ta'rifi bo'yicha hali
    /// uzatilmagan — tartib raqami qanday bo'lishidan qat'i nazar.
    /// Terminal jurnali tozalanib, raqamlar 1 dan qayta boshlansa ham
    /// o'tishlar shu qoida bilan tutiladi.
    /// </para>
    /// </remarks>
    public DateTimeOffset? LastEventTime { get; set; }

    /// <summary>
    /// Qidiruvning boshi — oxirgi uzatilgan hodisaning (TARTIB RAQAMI bo'yicha
    /// eng oxirgisining) terminal vaqti. <see cref="LastEventTime"/> dan farqi:
    /// bu PASAYISHI mumkin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega ikkita.</b> <see cref="LastEventTime"/> faqat o'sadi va
    /// «bu hodisa yangimi» degan savolga javob beradi. Terminal soati orqaga
    /// to'g'rilansa (masalan bir soatga), yangi o'tishlar undan OLDINGI vaqt
    /// bilan yoziladi — qidiruv o'sha nuqtadan boshlansa ularni ko'rmasdi.
    /// Ilgari bu holatda qidiruv «terminalning hozirgi vaqti − 2 daqiqa» ga
    /// o'tardi, va agent ikki daqiqadan uzoq uzilsa (internet), oradagi
    /// o'tishlar jimgina yo'qolardi.
    /// </para>
    /// <para>
    /// Endi qidiruv shu belgidan boshlanadi: har bo'lakdan keyin u eng oxirgi
    /// YOZILGAN hodisaning vaqtiga suriladi (soat orqaga ketgan bo'lsa —
    /// orqaga ham). Terminal soati bu belgidan ham orqada bo'lib qolsa, belgi
    /// o'sha lahzadagi terminal vaqtiga QOTIRILADI va keyingi o'tish
    /// uzatilguncha o'sha yerda turadi — uzilish qancha cho'zilmasin.
    /// </para>
    /// </remarks>
    public DateTimeOffset? SearchAnchor { get; set; }

    /// <summary>
    /// ESKI kursor — agent soatidagi oxirgi o'qish lahzasi.
    /// </summary>
    /// <remarks>
    /// Endi yozilmaydi. Faqat eski holat faylidan birinchi marta o'tishda
    /// qidiruvning boshini topish uchun o'qiladi
    /// (<see cref="LastEventTime"/> hali yo'q paytda).
    /// </remarks>
    public DateTimeOffset? LastReadAt { get; set; }

    public static AgentState Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AgentState();
            }

            return JsonSerializer.Deserialize<AgentState>(File.ReadAllText(path)) ?? new AgentState();
        }
        catch (Exception)
        {
            // Buzuq fayl — bo'sh holat bilan davom etiladi (yuqoridagi izoh).
            return new AgentState();
        }
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, overwrite: true);
    }
}
