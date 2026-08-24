using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Testlar uchun rasm yasash va o'qish.
/// </summary>
/// <remarks>
/// <para>
/// Nega umumiy: rasm quvuri ikki joyda ishlatiladi — xodim kartasida va
/// hisob sozlamalarida. Yordamchilar test sinfi ichida qolsa, ikkinchi
/// to'plam ularni nusxalashga majbur bo'lardi va PNG yasash mantig'idagi
/// tuzatish bir joyda qolib ketardi.
/// </para>
/// <para>
/// Manba rasm KODDA yasaladi: tashqi fayl testni repozitoriyning holatiga
/// bog'lab qo'yardi va Docker tasviriga nusxalash unutilishi mumkin edi.
/// </para>
/// </remarks>
internal static class TestImages
{
    // ────────────────────────────────────────────────────────────
    //  Yordamchilar — rasm formatlari
    // ────────────────────────────────────────────────────────────

    /// <summary>PNG imzosi (8 bayt).</summary>
    private static readonly byte[] PngImzosi = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Berilgan o'lchamdagi haqiqiy PNG yasaydi.
    /// </summary>
    /// <remarks>
    /// Nega qo'lda: bu shunchaki baytlar to'plami emas, HAQIQIY fayl bo'lishi
    /// kerak — server uni SkiaSharp bilan ochadi. Tayyor base64 satri esa
    /// o'lchamni tanlash imkonini bermasdi, kvadrat bo'lmagan manba esa
    /// markazdan qirqishni tekshirish uchun zarur.
    /// </remarks>
    public static byte[] PngYasa(int eni, int boyi)
    {
        // Har bir qator: filtr bayti (0 — filtr yo'q) + RGB uchliklari
        var xom = new byte[boyi * (1 + (eni * 3))];
        var joy = 0;

        for (var y = 0; y < boyi; y++)
        {
            xom[joy++] = 0;

            for (var x = 0; x < eni; x++)
            {
                xom[joy++] = (byte)(x * 6);
                xom[joy++] = (byte)(y * 6);
                xom[joy++] = 128;
            }
        }

        byte[] siqilgan;

        using (var buffer = new MemoryStream())
        {
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(xom, 0, xom.Length);
            }

            siqilgan = buffer.ToArray();
        }

        var sarlavha = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(sarlavha.AsSpan(0), eni);
        BinaryPrimitives.WriteInt32BigEndian(sarlavha.AsSpan(4), boyi);
        sarlavha[8] = 8;  // bit chuqurligi
        sarlavha[9] = 2;  // rang turi: to'liq rangli RGB

        using var png = new MemoryStream();
        png.Write(PngImzosi, 0, PngImzosi.Length);

        BolakYoz(png, "IHDR", sarlavha);
        BolakYoz(png, "IDAT", siqilgan);
        BolakYoz(png, "IEND", []);

        return png.ToArray();
    }

    /// <summary>PNG bo'lagini yozadi: uzunlik, turi, mazmuni, CRC.</summary>
    private static void BolakYoz(Stream target, string turi, byte[] mazmun)
    {
        var uzunlik = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(uzunlik, mazmun.Length);
        target.Write(uzunlik, 0, uzunlik.Length);

        var turBaytlari = Encoding.ASCII.GetBytes(turi);
        target.Write(turBaytlari, 0, turBaytlari.Length);
        target.Write(mazmun, 0, mazmun.Length);

        var nazorat = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(nazorat, Crc32(turBaytlari, mazmun));
        target.Write(nazorat, 0, nazorat.Length);
    }

    /// <summary>PNG talab qiladigan CRC-32 (polinom 0xEDB88320).</summary>
    private static uint Crc32(byte[] birinchi, byte[] ikkinchi)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var bayt in birinchi)
        {
            crc = Qadam(crc, bayt);
        }

        foreach (var bayt in ikkinchi)
        {
            crc = Qadam(crc, bayt);
        }

        return crc ^ 0xFFFFFFFFu;

        static uint Qadam(uint joriy, byte bayt)
        {
            joriy ^= bayt;

            for (var i = 0; i < 8; i++)
            {
                joriy = (joriy & 1) != 0 ? (joriy >> 1) ^ 0xEDB88320u : joriy >> 1;
            }

            return joriy;
        }
    }

    /// <summary>
    /// JPEG o'lchamini SOF bo'lagidan o'qiydi.
    /// </summary>
    /// <remarks>
    /// Kichraytirish HAQIQATAN bajarilganini boshqacha isbotlab bo'lmaydi:
    /// javob hajmi manba hajmiga ham bog'liq va u hech narsani kafolatlamaydi.
    /// </remarks>
    public static (int Width, int Height) JpegOlchami(byte[] baytlar)
    {
        // Birinchi ikki bayt — SOI (FFD8), undan keyin bo'laklar ketadi
        var joy = 2;

        while (joy + 9 < baytlar.Length)
        {
            if (baytlar[joy] != 0xFF)
            {
                joy++;
                continue;
            }

            var belgi = baytlar[joy + 1];

            // To'ldiruvchi baytlar va argumentsiz belgilar
            if (belgi is 0xFF or 0x01 || (belgi >= 0xD0 && belgi <= 0xD9))
            {
                joy += 2;
                continue;
            }

            var uzunlik = (baytlar[joy + 2] << 8) | baytlar[joy + 3];

            // SOF0…SOF15, lekin DHT (C4), JPG (C8) va DAC (CC) ular emas
            if (belgi >= 0xC0 && belgi <= 0xCF && belgi is not (0xC4 or 0xC8 or 0xCC))
            {
                var boyi = (baytlar[joy + 5] << 8) | baytlar[joy + 6];
                var eni = (baytlar[joy + 7] << 8) | baytlar[joy + 8];

                return (eni, boyi);
            }

            joy += 2 + uzunlik;
        }

        throw new InvalidOperationException("JPEG SOF bo'lagi topilmadi — javob JPEG emas.");
    }
}
