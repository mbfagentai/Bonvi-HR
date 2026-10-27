using Bonvi.Application.Abstractions;
using SkiaSharp;

namespace Bonvi.Infrastructure.Storage;

/// <summary>
/// <see cref="IImageProcessor"/> ning SkiaSharp ustidagi amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega SkiaSharp.</b> ImageSharp litsenziyasi tijorat tashkiloti uchun
/// pullik (batafsil sabab <c>Directory.Packages.props</c> da). SkiaSharp esa
/// MIT va aynan shu vazifa uchun yetarli: ochish, o'girish, kichraytirish.
/// </para>
/// <para>
/// <b>Xavfsizlik.</b> Fayl turi kengaytmaga ham, mijoz yuborgan sarlavhaga
/// ham qaramaydi: Skia dekoderi faylni ocholmasa yoki formati ro'yxatda
/// bo'lmasa — javob <c>null</c>. Ya'ni «.jpg» deb nomlangan skript ham,
/// qo'llab-quvvatlanmaydigan formatdagi haqiqiy rasm ham shu yerda to'xtaydi.
/// </para>
/// <para>
/// <b>Siqilgan «bomba» fayldan himoya.</b> Dekodlangan rasm xotirada
/// piksel×4 bayt joy oladi — 50 KB lik PNG 40000×40000 deb e'lon qilsa,
/// bu 6 GB dekodlashga urinish va konteynerning o'lishi degani. Shu sababli
/// yangi <see cref="ToBoundedAsync"/> yo'lida piksel hisobi BIR BAYT
/// ajratilishidan oldin, sarlavhadagi o'lchamdan tekshiriladi
/// (<see cref="MaxSourcePixels"/>).
/// </para>
/// <para>
/// Holatsiz, shu sababli singleton sifatida ro'yxatga olinadi.
/// </para>
/// </remarks>
public sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>Natija JPEG sifat darajasi.</summary>
    /// <remarks>
    /// <para>
    /// 85 — amaliyotda tan olingan chegara: undan yuqorisi fayl hajmini
    /// sezilarli oshiradi, lekin ko'z bilan farqni bermaydi.
    /// </para>
    /// <para>
    /// Bu raqam IKKI metodning ham yagona manbasi. Hujjat surati uchun
    /// ikkinchi doimiy (masalan 82) ATAYLAB kiritilmadi: u fayl hajmini
    /// 15% dan kam qisqartiradi va saqlash hisobini o'zgartirmaydi
    /// (3-5 MB → baribir 0,3-0,7 MB), lekin bir-biriga moslab turish kerak
    /// bo'lgan ikkinchi raqam paydo bo'lardi.
    /// </para>
    /// </remarks>
    private const int JpegQuality = 85;

    /// <summary>
    /// Manba rasmning eng ko'p piksel soni — 80 megapiksel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hisob.</b> 80 MP × 4 bayt (RGBA) ≈ 320 MB — server uchun (eng past
    /// chegara 2 GB RAM) qabul qilinadigan eng yuqori vaqtinchalik sarf.
    /// Bundan kattasi bitta yuklash bilan butun API'ni o'ldirardi va bu
    /// «xato javob» emas, «xizmat yo'q» bo'lardi.
    /// </para>
    /// <para>
    /// <b>Nega haqiqiy fayllarga to'sqinlik qilmaydi.</b> 600 dpi da
    /// skanerlangan A4 — 35 MP, 50 MP telefon surati — 50 MP; ikkalasi ham
    /// bemalol o'tadi. Chegaraga faqat ataylab yasalgan fayl yetadi.
    /// </para>
    /// <para>
    /// <b>Rad etilgan muqobil</b> — Skia'ning bosqichli dekodlashi
    /// (<c>SKCodec.GetScaledDimensions</c>, JPEG uchun 1/2, 1/4, 1/8):
    /// xotirani manba hajmidan qat'i nazar cheklardi, lekin FAQAT JPEG'da
    /// ishlaydi (PNG/WebP to'liq o'lchamni qaytaradi — ya'ni «bomba»
    /// aynan shu yerda o'tib ketardi) va ikkinchi dekodlash yo'lini
    /// qo'shardi. Chegara ikkalasini ham bitta shart bilan yopadi.
    /// </para>
    /// <para>
    /// <see cref="ToSquareAsync"/> ham 2026-10-24 dan shu tekshiruvdan o'tadi:
    /// onlayn anketa suratni internetdan ochiq yo'l orqali qabul qiladi va
    /// xavfsizlik ko'rigi kichik fayldagi ulkan o'lcham API jarayonini o'ldirishini
    /// ko'rsatdi. Haqiqiy suratlar (≤ 50 MP) bunga tegmaydi.
    /// </para>
    /// </remarks>
    private const long MaxSourcePixels = 80L * 1000 * 1000;

    /// <summary>
    /// Qabul qilinadigan MANBA formatlari.
    /// </summary>
    /// <remarks>
    /// Skia GIF, BMP va boshqa formatlarni ham ocha oladi. Ro'yxat ataylab
    /// qisqa: HTTP shartnomasida faqat shu uch tur e'lon qilingan va
    /// «ochildi — demak mayli» qoidasi shartnomani jimgina kengaytirardi.
    /// </remarks>
    private static bool IsSupported(SKEncodedImageFormat format)
        => format is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp;

    public async Task<ProcessedImage?> ToSquareAsync(
        Stream source,
        int size,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var bytes = await BufferAsync(source, cancellationToken).ConfigureAwait(false);

        return bytes is null ? null : Render(bytes, size);
    }

    public async Task<ProcessedImage?> ToBoundedAsync(
        Stream source,
        int maxEdge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEdge, 1);

        var bytes = await BufferAsync(source, cancellationToken).ConfigureAwait(false);

        return bytes is null ? null : RenderBounded(bytes, maxEdge);
    }

    public async Task<ProcessedImage?> ToFlatPngAsync(
        Stream source,
        int maxWidth,
        int maxHeight,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHeight, 1);

        var bytes = await BufferAsync(source, cancellationToken).ConfigureAwait(false);

        return bytes is null ? null : RenderFlatPng(bytes, maxWidth, maxHeight);
    }

    /// <summary>Oqimni xotiraga o'qiydi; bo'sh bo'lsa — <c>null</c>.</summary>
    /// <remarks>
    /// Skia sinxron va tasodifiy murojaat qiladigan buferni talab qiladi,
    /// HTTP oqimi esa bir marta oldinga o'qiladi. Hajm chaqiruvchi tomonda
    /// allaqachon cheklangan (<c>PhotoLimits.MaxBytes</c> /
    /// <c>DocumentLimits.MaxBytes</c>), shu sababli xotiraga nusxalash
    /// xavfsiz.
    /// </remarks>
    private static async Task<byte[]?> BufferAsync(Stream source, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return buffer.Length == 0 ? null : buffer.ToArray();
    }

    /// <summary>Rasmni o'giradi, markazdan qirqadi va kichraytiradi.</summary>
    private static ProcessedImage? Render(byte[] bytes, int size)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);

        if (codec is null || !IsSupported(codec.EncodedFormat))
        {
            return null;
        }

        // Piksel hisobi SARLAVHADAN — «bomba» fayl dekodlashdan oldin to'xtaydi
        if (ExceedsPixelBudget(codec.Info))
        {
            return null;
        }

        using var image = SKImage.FromEncodedData(data);
        if (image is null)
        {
            return null;
        }

        var origin = codec.EncodedOrigin;

        // Telefonda olingan surat ko'pincha «yotgan» holda saqlanadi va
        // to'g'ri tomoni faqat EXIF belgisida ko'rsatiladi. Skia uni o'zi
        // qo'llamaydi — hisobga olinmasa, xodim rasmi yonboshlab qolardi.
        var turned = SwapsSides(origin);
        var width = turned ? image.Height : image.Width;
        var height = turned ? image.Width : image.Height;

        var side = Math.Min(width, height);
        var left = (width - side) / 2f;
        var top = (height - side) / 2f;
        var scale = size / (float)side;

        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface is null)
        {
            return null;
        }

        var canvas = surface.Canvas;

        // JPEG shaffoflikni bilmaydi: PNG'ning shaffof joylari tozalanmasa
        // qora bo'lib chiqadi. Oq fon hujjat suratiga eng mos keladi.
        canvas.Clear(SKColors.White);

        // Kanvas o'zgartirishlari CHAQIRILISH TARTIBIGA TESKARI qo'llanadi:
        // avval EXIF burilishi, keyin markazdan siljitish, oxirida masshtab.
        canvas.Scale(scale);
        canvas.Translate(-left, -top);
        ApplyOrigin(canvas, origin, image.Width, image.Height);

        // Mitchell — kubik filtr: kichraytirishda tishli chekka ham,
        // ortiqcha «yuvilish» ham bermaydi.
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

        if (encoded is null)
        {
            return null;
        }

        // Oqim xotirada: uni chaqiruvchi saqlashga uzatadi va o'zi yopadi.
        var result = new MemoryStream(encoded.ToArray(), writable: false);

        return new ProcessedImage(result, "image/jpeg", size, size);
    }

    /// <summary>Rasmni o'giradi va chegara qutisiga sig'diradi (qirqmasdan).</summary>
    /// <remarks>
    /// <para>
    /// <see cref="Render"/> bilan bir xil qadamlar — bir xil qabul qilinadigan
    /// formatlar, bir xil EXIF o'girish, bir xil Mitchell filtri, bir xil oq
    /// fon va bir xil <see cref="JpegQuality"/>. Farqi ikkitada: (1) markazdan
    /// KVADRAT QIRQILMAYDI, (2) chegaradan kichik rasm kattalashtirilmaydi.
    /// Ikkita metod bitta umumlashtirilgan metodga birlashtirilmadi: qirqish
    /// va sig'dirish geometriyasi boshqacha, birlashtirilsa shartlar
    /// ichida yashiringan «qaysi rejim» mantiqi paydo bo'lardi.
    /// </para>
    /// </remarks>
    private static ProcessedImage? RenderBounded(byte[] bytes, int maxEdge)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);

        if (codec is null || !IsSupported(codec.EncodedFormat))
        {
            return null;
        }

        // Piksel hisobi SARLAVHADAN olinadi va dekodlashdan OLDIN
        // tekshiriladi — «bomba» fayl xotira ajratilishidan avval to'xtaydi.
        if (ExceedsPixelBudget(codec.Info))
        {
            return null;
        }

        using var image = SKImage.FromEncodedData(data);

        // Kodek ochildi, dekodlash esa yiqildi (buzilgan ichki tuzilma).
        // Chaqiruvchi uchun natija — «yaroqsiz fayl», istisno emas.
        //
        // ⚠️ CHALA fayl bu yerga TUSHMAYDI: Skia qirqilgan JPEG'ni
        // muvaffaqiyatli, lekin QISMAN dekodlaydi (qolgan qismi bo'sh
        // bo'lib chiqadi) — tekshirilgan, shunday. Ya'ni chala yuklangan
        // skan rad etilmaydi, yarim rasm bo'lib saqlanadi. Ataylab shunday
        // qoldirilgan: (1) HTTP tanasi uzilib qolsa so'rov bu yergacha
        // yetib kelmaydi — Kestrel uni o'zi rad etadi, ya'ni real holat
        // faqat «diskdagi fayl allaqachon chala»; (2) yarim rasm ekranda
        // KO'RINADI va HR uni qayta yuklaydi, JPEG'ning oxiridagi EOI
        // belgisini qo'lda tekshirish esa har format uchun alohida hiyla
        // va noto'g'ri rad etish xavfini qo'shardi.
        if (image is null)
        {
            return null;
        }

        var origin = codec.EncodedOrigin;

        // Telefonda olingan hujjat surati ham «yotgan» holda keladi va
        // to'g'ri tomoni faqat EXIF belgisida bo'ladi. Hisobga olinmasa,
        // pasport skani yonboshlab saqlanardi.
        var turned = SwapsSides(origin);
        var sourceWidth = turned ? image.Height : image.Width;
        var sourceHeight = turned ? image.Width : image.Height;

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return null;
        }

        // Math.Min(1, …) — KATTALASHTIRISH YO'Q qoidasi shu bitta joyda.
        var longest = Math.Max(sourceWidth, sourceHeight);
        var fit = Math.Min(1f, maxEdge / (float)longest);

        // Yakuniy o'lcham butun pikselga yaxlitlanadi; 1 dan kichik bo'lib
        // qolmasligi kerak (juda tor manba: masalan 4000×1 lik chizim).
        var width = Math.Max(1, (int)Math.Round(sourceWidth * fit));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * fit));

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        if (surface is null)
        {
            return null;
        }

        var canvas = surface.Canvas;

        // JPEG shaffoflikni bilmaydi: PNG'ning shaffof joylari tozalanmasa
        // qora bo'lib chiqadi. Oq fon hujjat suratiga eng mos keladi.
        canvas.Clear(SKColors.White);

        // Kanvas o'zgartirishlari CHAQIRILISH TARTIBIGA TESKARI qo'llanadi:
        // avval EXIF burilishi, keyin masshtab.
        //
        // Masshtab eni va bo'yi uchun ALOHIDA hisoblanadi (yagona `fit`
        // emas): yaxlitlashdan keyingi qoldiq shundagina chekkada bir piksel
        // oq chiziq qoldirmaydi. Ikki koeffitsient orasidagi farq yarim
        // pikselcha, ya'ni nisbat ko'zga ko'rinadigan darajada buzilmaydi.
        canvas.Scale(width / (float)sourceWidth, height / (float)sourceHeight);
        ApplyOrigin(canvas, origin, image.Width, image.Height);

        // Mitchell — kubik filtr: kichraytirishda tishli chekka ham,
        // ortiqcha «yuvilish» ham bermaydi. Muhr va qo'lda yozilgan matn
        // o'qilishi kerak, shu sababli filtr tanlovi bu yerda muhim.
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

        if (encoded is null)
        {
            return null;
        }

        // Oqim xotirada: uni chaqiruvchi saqlashga uzatadi va o'zi yopadi.
        var result = new MemoryStream(encoded.ToArray(), writable: false);

        return new ProcessedImage(result, "image/jpeg", width, height);
    }

    /// <summary>PNG imzoni oq fonga yotqizib qayta kodlaydi (<see cref="ToFlatPngAsync"/>).</summary>
    /// <remarks>
    /// EXIF burilishi va masshtab yo'q: imzo kanvasdan keladi, ya'ni o'lchami va
    /// yo'nalishi allaqachon to'g'ri. O'lcham sarlavhadan tekshiriladi — chegaradan
    /// katta fayl umuman dekodlanmaydi.
    /// </remarks>
    private static ProcessedImage? RenderFlatPng(byte[] bytes, int maxWidth, int maxHeight)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);

        if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png)
        {
            return null;
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;

        if (width <= 0 || height <= 0 || width > maxWidth || height > maxHeight)
        {
            return null;
        }

        using var image = SKImage.FromEncodedData(data);
        if (image is null)
        {
            return null;
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);

        if (surface is null)
        {
            return null;
        }

        // Shaffof PNG PDF'da kulrang chegara qoldirardi — oq fon blankadagi qog'oz rangi
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, 0, 0);

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100);

        if (encoded is null)
        {
            return null;
        }

        return new ProcessedImage(new MemoryStream(encoded.ToArray(), writable: false), "image/png", width, height);
    }

    /// <summary>E'lon qilingan o'lcham piksel chegarasidan oshadimi.</summary>
    /// <remarks>
    /// <c>long</c> ga o'tkazish shart: 40000×40000 <c>int</c> da toshib
    /// ketadi va manfiy son beradi — ya'ni tekshiruv o'z-o'zidan o'tib
    /// ketardi. Aynan shunday fayl bilan bu himoya ma'nosini yo'qotardi.
    /// </remarks>
    private static bool ExceedsPixelBudget(SKImageInfo info)
        => (long)info.Width * info.Height > MaxSourcePixels;

    /// <summary>EXIF yo'nalishi eni va bo'yini almashtiradimi.</summary>
    private static bool SwapsSides(SKEncodedOrigin origin)
        => origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// EXIF yo'nalishini kanvas o'zgartirishlariga aylantiradi.
    /// </summary>
    /// <remarks>
    /// Har bir holat manba nuqtasi (x, y) ni o'girilgan tasvirdagi joyiga
    /// ko'chiradi. Matritsa qo'lda yig'ilmaydi — <c>Translate</c>,
    /// <c>Scale</c> va <c>RotateDegrees</c> ketma-ketligi o'qishga osonroq
    /// va SkiaSharp versiyalari orasida barqaror.
    /// </remarks>
    private static void ApplyOrigin(SKCanvas canvas, SKEncodedOrigin origin, int width, int height)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                // Gorizontal ko'zgu: x → width - x
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;

            case SKEncodedOrigin.BottomRight:
                // 180° burilish
                canvas.Translate(width, height);
                canvas.Scale(-1, -1);
                break;

            case SKEncodedOrigin.BottomLeft:
                // Vertikal ko'zgu: y → height - y
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;

            case SKEncodedOrigin.LeftTop:
                // Transpozitsiya: (x, y) → (y, x)
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;

            case SKEncodedOrigin.RightTop:
                // Soat yo'nalishida 90°
                canvas.Translate(height, 0);
                canvas.RotateDegrees(90);
                break;

            case SKEncodedOrigin.RightBottom:
                // Teskari transpozitsiya: (x, y) → (height - y, width - x)
                canvas.Translate(height, width);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;

            case SKEncodedOrigin.LeftBottom:
                // Soat yo'nalishiga qarshi 90°
                canvas.Translate(0, width);
                canvas.RotateDegrees(-90);
                break;

            case SKEncodedOrigin.TopLeft:
            default:
                // Belgi yo'q yoki oddiy holat — hech narsa qilinmaydi.
                break;
        }
    }
}
