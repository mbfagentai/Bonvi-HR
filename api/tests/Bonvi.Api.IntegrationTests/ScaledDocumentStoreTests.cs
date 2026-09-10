using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
using Bonvi.Infrastructure.Storage;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Nomzod fayli quvuri: rasm HAQIQATAN kichrayadimi, PDF o'zgarmay
/// qoladimi va yaroqsiz fayl xatoga aylanadimi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu testlar bor.</b> «Rasm kichraytiriladi» degan gap izohda
/// yozilgani bilan bajarilmaydi: quvurdan bitta bo'g'in (masalan
/// <c>ToBoundedAsync</c> ning chaqiruvi) tushib qolsa, hamma narsa
/// ishlayotgandek ko'rinadi — fayllar saqlanadi, karta ochiladi — va
/// muammo faqat bir yildan keyin, disk to'lganda ma'lum bo'ladi. Bazaga
/// yozilgan hajm ham o'shanda yolg'on bo'lib chiqadi.
/// </para>
/// <para>
/// <b>Nega bu yerda, alohida birlik-test loyihasida emas.</b>
/// <see cref="SkiaImageProcessor"/> va <see cref="FileContentInspector"/>
/// <c>Bonvi.Infrastructure</c> yig'masida yotadi, uni ko'radigan test
/// loyihasi esa faqat shu (<c>Bonvi.Application.Tests</c> Application
/// qatlamidan tashqariga chiqmaydi — bu ataylab shunday). Testlarga
/// PostgreSQL kerak emas: <see cref="BonviApiFactory"/> ishlatilmaydi,
/// fayl saqlash o'rniga xotiradagi o'rinbosar turadi. Bu
/// <c>ErrorLogFormatterTests</c> da qabul qilingan qoidaning aynan o'zi.
/// </para>
/// <para>
/// <b>Manba rasm SHOVQINDAN yasaladi.</b> Bir tekis gradient JPEG'da
/// deyarli hech joy egallamaydi va «fayl kichraydi» degan tekshiruv
/// ma'nosini yo'qotardi. Shovqin — haqiqiy suratning eng yomon holati:
/// test o'tsa, telefondagi haqiqiy surat bundan ham yaxshi natija beradi.
/// </para>
/// </remarks>
public sealed class ScaledDocumentStoreTests
{
    /// <summary>Quvurning haqiqiy tarkibi — faqat disk o'rinbosar bilan almashtirilgan.</summary>
    private static (ScaledDocumentStore Store, XotiraSaqlash Disk) Quvur()
    {
        var disk = new XotiraSaqlash();
        return (new ScaledDocumentStore(disk, new FileContentInspector(), new SkiaImageProcessor()), disk);
    }

    // ────────────────────────────────────────────────────────────
    //  Rasm
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Katta JPEG kichrayadi, uzun tomoni AYNAN chegaraga tushadi va
    /// bazaga yozilgan hajm — diskdagi hajm.
    /// </summary>
    [Fact]
    public async Task Katta_jpeg_kichraytiriladi_va_hajm_diskdagi_boladi()
    {
        var (quvur, disk) = Quvur();
        var manba = JpegYasa(3000, 2250, sifat: 100);

        await using var oqim = new MemoryStream(manba, writable: false);
        var natija = await quvur.AcceptAsync(oqim, manba.Length, TestContext.Current.CancellationToken);

        natija.Rejection.ShouldBeNull();
        natija.Key.ShouldNotBeNull();
        natija.ContentType.ShouldBe("image/jpeg");

        // Hajm — YUKLANGAN emas, YOZILGAN. Ikkisi adashtirilsa karta HR ga
        // 4 MB deb ko'rsatib, diskda 300 KB turardi.
        natija.StoredBytes.ShouldBe(disk.Baytlar!.Length);
        natija.StoredBytes.ShouldBeLessThan(manba.Length);

        // O'lcham — yagona haqiqiy dalil: javob hajmi manbaga ham bog'liq.
        var (eni, boyi) = TestImages.JpegOlchami(disk.Baytlar);
        eni.ShouldBe(ScaledDocumentStore.MaxImageEdge);
        boyi.ShouldBe(1500);
    }

    /// <summary>Tik (portret) rasmda ham chegara UZUN tomonga qo'yiladi.</summary>
    /// <remarks>
    /// Alohida test, chunki eni va bo'yini almashtirib qo'yish eng oson
    /// xato: kvadrat manbada u umuman sezilmaydi.
    /// </remarks>
    [Fact]
    public async Task Tik_rasmda_chegara_uzun_tomonga_qoyiladi()
    {
        var (quvur, disk) = Quvur();

        // A4, 300 dpi — skanerdan chiqadigan eng odatiy o'lcham.
        var manba = JpegYasa(2480, 3508, sifat: 95);

        await using var oqim = new MemoryStream(manba, writable: false);
        var natija = await quvur.AcceptAsync(oqim, manba.Length, TestContext.Current.CancellationToken);

        natija.Rejection.ShouldBeNull();

        var (eni, boyi) = TestImages.JpegOlchami(disk.Baytlar!);
        boyi.ShouldBe(ScaledDocumentStore.MaxImageEdge);
        eni.ShouldBe(1414);
    }

    /// <summary>PNG ham JPEG bo'lib saqlanadi — chiqish turi bitta.</summary>
    [Fact]
    public async Task Png_jpeg_bolib_saqlanadi()
    {
        var (quvur, disk) = Quvur();
        var manba = TestImages.PngYasa(2400, 1000);

        await using var oqim = new MemoryStream(manba, writable: false);
        var natija = await quvur.AcceptAsync(oqim, manba.Length, TestContext.Current.CancellationToken);

        natija.ContentType.ShouldBe("image/jpeg");

        var (eni, boyi) = TestImages.JpegOlchami(disk.Baytlar!);
        eni.ShouldBe(ScaledDocumentStore.MaxImageEdge);
        boyi.ShouldBe(833);
    }

    /// <summary>
    /// Chegaradan kichik rasm KATTALASHTIRILMAYDI, lekin qayta kodlanadi.
    /// </summary>
    /// <remarks>
    /// Ikkinchi shart birinchisidan muhim: mijozning shikoyati aynan
    /// «kichik, lekin 5 MB» fayldan boshlangan. Kattalashtirish esa bir
    /// bayt ma'lumot qo'shmasdi.
    /// </remarks>
    [Fact]
    public async Task Kichik_rasm_kattalashtirilmaydi_lekin_qayta_kodlanadi()
    {
        var (quvur, disk) = Quvur();
        var manba = JpegYasa(800, 600, sifat: 100);

        await using var oqim = new MemoryStream(manba, writable: false);
        var natija = await quvur.AcceptAsync(oqim, manba.Length, TestContext.Current.CancellationToken);

        var (eni, boyi) = TestImages.JpegOlchami(disk.Baytlar!);
        eni.ShouldBe(800);
        boyi.ShouldBe(600);
        natija.StoredBytes.ShouldBeLessThan(manba.Length);
    }

    // ────────────────────────────────────────────────────────────
    //  PDF
    // ────────────────────────────────────────────────────────────

    /// <summary>PDF baytma-bayt saqlanadi — hujjat qayta yozilmaydi.</summary>
    [Fact]
    public async Task Pdf_baytma_bayt_saqlanadi()
    {
        var (quvur, disk) = Quvur();
        var manba = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");

        await using var oqim = new MemoryStream(manba, writable: false);
        var natija = await quvur.AcceptAsync(oqim, manba.Length, TestContext.Current.CancellationToken);

        natija.ContentType.ShouldBe("application/pdf");
        natija.StoredBytes.ShouldBe(manba.Length);
        disk.Baytlar.ShouldBe(manba);
    }

    // ────────────────────────────────────────────────────────────
    //  Yaroqsiz va dushmanona fayllar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// «.jpg» deb nomlangan matn fayli rad etiladi — tur MAZMUNDAN
    /// aniqlanadi.
    /// </summary>
    [Fact]
    public async Task Rasm_bolmagan_fayl_rad_etiladi()
    {
        var (quvur, disk) = Quvur();
        var manba = Encoding.UTF8.GetBytes("#!/bin/sh\necho salom\n");

        await using var oqim = new MemoryStream(manba, writable: false);
        var natija = await quvur.AcceptAsync(oqim, manba.Length, TestContext.Current.CancellationToken);

        natija.Rejection.ShouldBe(DocumentRejection.Invalid);
        natija.Key.ShouldBeNull();
        natija.StoredBytes.ShouldBe(0);

        // Rad etilgan fayldan keyin diskda hech narsa qolmasligi kerak.
        disk.Baytlar.ShouldBeNull();
    }

    /// <summary>
    /// Piksel «bomba»si rad etiladi va DEKODLASHGA URINILMAYDI.
    /// </summary>
    /// <remarks>
    /// 200 baytlik fayl 40000×40000 deb e'lon qiladi — dekodlansa 6 GB
    /// xotira so'raladi va konteyner o'ladi. Test aynan shu himoyani
    /// qadaydi: usiz bitta yuklash butun API'ni to'xtatardi va bu «xato
    /// javob» emas, «xizmat yo'q» bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Piksel_bombasi_rad_etiladi()
    {
        var (quvur, disk) = Quvur();
        var manba = PngBombaYasa(40000, 40000);

        // Fayl TUR TEKSHIRUVIDAN o'tishi shart: aks holda test rasm
        // qayta ishlovchisidagi piksel himoyasini emas, undan oldingi
        // «bu rasm emas» tekshiruvini o'lchab, yolg'on yashil bo'lib
        // qolardi — himoya olib tashlansa ham test o'tib ketardi.
        new FileContentInspector().Detect(manba).ShouldBe(DetectedFileKind.Png);

        await using var oqim = new MemoryStream(manba, writable: false);
        var natija = await quvur.AcceptAsync(oqim, manba.Length, TestContext.Current.CancellationToken);

        natija.Rejection.ShouldBe(DocumentRejection.Invalid);
        disk.Baytlar.ShouldBeNull();
    }

    /// <summary>
    /// Chegaradan katta fayl oqim O'QILISHIDAN OLDIN rad etiladi.
    /// </summary>
    /// <remarks>
    /// Oqim o'rniga hech qachon o'qilmasligi kerak bo'lgan
    /// <see cref="OqilmasOqim"/> beriladi: shart buzilsa test istisno
    /// bilan yiqiladi, «hajm tekshiruvi keyinga surilgan» degan xato
    /// jimgina o'tib ketmaydi.
    /// </remarks>
    [Fact]
    public async Task Chegaradan_katta_fayl_oqim_oqilmasdan_rad_etiladi()
    {
        var (quvur, disk) = Quvur();

        await using var oqim = new OqilmasOqim();
        var natija = await quvur.AcceptAsync(
            oqim,
            DocumentLimits.MaxBytes + 1,
            TestContext.Current.CancellationToken);

        natija.Rejection.ShouldBe(DocumentRejection.TooLarge);
        natija.Key.ShouldBeNull();
        disk.Baytlar.ShouldBeNull();
    }

    /// <summary>Bo'sh yuklash rad etiladi.</summary>
    [Fact]
    public async Task Bosh_fayl_rad_etiladi()
    {
        var (quvur, _) = Quvur();

        await using var oqim = new MemoryStream([], writable: false);
        var natija = await quvur.AcceptAsync(oqim, 0, TestContext.Current.CancellationToken);

        natija.Rejection.ShouldBe(DocumentRejection.Invalid);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Manba JPEG — yasovchi <see cref="TestImages"/> da, umumiy.</summary>
    /// <remarks>
    /// Nusxa ATAYLAB olib tashlandi: aynan shu manba rasm uchi-uchiga
    /// testda ham kerak (<c>CandidateFileApiTests</c>) va ikkita nusxa
    /// bir kun ajralib ketardi.
    /// </remarks>
    private static byte[] JpegYasa(int eni, int boyi, int sifat) => TestImages.JpegYasa(eni, boyi, sifat);

    /// <summary>
    /// Sarlavhasida katta o'lcham e'lon qilingan, mazmuni esa qo'g'irchoq
    /// bo'lgan PNG.
    /// </summary>
    /// <remarks>
    /// <see cref="TestImages.PngYasa"/> HAQIQIY piksellarni yozadi, ya'ni
    /// 40000×40000 uchun test o'zi 6 GB talab qilardi. Bu yerda faqat
    /// IHDR muhim: kodek uni o'qiydi va quvur dekodlashgacha to'xtaydi.
    /// </remarks>
    private static byte[] PngBombaYasa(int eni, int boyi)
    {
        var sarlavha = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(sarlavha.AsSpan(0), eni);
        BinaryPrimitives.WriteInt32BigEndian(sarlavha.AsSpan(4), boyi);
        sarlavha[8] = 8;  // bit chuqurligi
        sarlavha[9] = 2;  // rang turi: to'liq rangli RGB

        // Bir necha yuz bayt «piksel» — dekoder uchun boshlanishi to'g'ri
        // ko'rinsin. E'lon qilingan 1,6 gigapikselga bu, tabiiyki, yetmaydi.
        byte[] mazmun;

        using (var buffer = new MemoryStream())
        {
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(new byte[512], 0, 512);
            }

            mazmun = buffer.ToArray();
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        BolakYoz(png, "IHDR", sarlavha);
        BolakYoz(png, "IDAT", mazmun);
        BolakYoz(png, "IEND", []);

        return png.ToArray();

        // CRC to'g'ri bo'lishi SHART: noto'g'ri CRC'da kodek faylni umuman
        // ocholmaydi, quvur uni «rasm emas» deb rad etadi va test piksel
        // himoyasini emas, tur tekshiruvini o'lchab, yolg'on yashil bo'lib
        // qolardi.
        static void BolakYoz(Stream target, string turi, byte[] mazmun)
        {
            var uzunlik = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(uzunlik, mazmun.Length);
            target.Write(uzunlik);

            var turBaytlari = Encoding.ASCII.GetBytes(turi);
            target.Write(turBaytlari);
            target.Write(mazmun);

            var crc = 0xFFFFFFFFu;

            foreach (var bayt in turBaytlari.Concat(mazmun))
            {
                crc ^= bayt;

                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }
            }

            var nazorat = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(nazorat, crc ^ 0xFFFFFFFFu);
            target.Write(nazorat);
        }
    }

    /// <summary>Diskning o'rinbosari: oxirgi yozilgan baytlarni eslab qoladi.</summary>
    /// <remarks>
    /// Haqiqiy <see cref="LocalFileStorage"/> ATAYLAB ishlatilmadi: unga
    /// papka, sozlama va jurnal kerak, testga esa faqat «diskka nima
    /// tushdi» degan javob kerak — aynan shu son bazaga yoziladi.
    /// </remarks>
    private sealed class XotiraSaqlash : IFileStorage
    {
        /// <summary>Oxirgi saqlangan mazmun; hech narsa saqlanmagan bo'lsa — <c>null</c>.</summary>
        public byte[]? Baytlar { get; private set; }

        public async Task<StoredFile> SaveAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Baytlar = buffer.ToArray();

            return new StoredFile(
                "2026/09/" + Guid.CreateVersion7().ToString("N") + Path.GetExtension(fileName),
                fileName,
                contentType,
                Baytlar.Length);
        }

        public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(Baytlar is null ? null : new MemoryStream(Baytlar, writable: false));

        public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Baytlar = null;
            return Task.FromResult(true);
        }
    }

    /// <summary>O'qilishi TEST XATOSI bo'lgan oqim.</summary>
    private sealed class OqilmasOqim : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException(
                "Hajm chegarasi oqim o'qilishidan OLDIN tekshirilishi kerak edi.");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
