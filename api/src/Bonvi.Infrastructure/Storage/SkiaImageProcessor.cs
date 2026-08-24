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
/// Holatsiz, shu sababli singleton sifatida ro'yxatga olinadi.
/// </para>
/// </remarks>
public sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>Natija JPEG sifat darajasi.</summary>
    /// <remarks>
    /// 85 — amaliyotda tan olingan chegara: undan yuqorisi fayl hajmini
    /// sezilarli oshiradi, lekin ko'z bilan farqni bermaydi.
    /// </remarks>
    private const int JpegQuality = 85;

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

        // Skia sinxron va tasodifiy murojaat qiladigan buferni talab qiladi,
        // HTTP oqimi esa bir marta oldinga o'qiladi. Hajm chaqiruvchi tomonda
        // allaqachon cheklangan, shu sababli xotiraga nusxalash xavfsiz.
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length == 0)
        {
            return null;
        }

        return Render(buffer.ToArray(), size);
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
