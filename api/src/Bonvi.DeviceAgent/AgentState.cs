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
    /// <summary>Oxirgi o'qilgan hodisaning tartib raqami.</summary>
    public long LastSerialNo { get; set; }

    /// <summary>Kursor qaysi lahzagacha surilgan.</summary>
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
