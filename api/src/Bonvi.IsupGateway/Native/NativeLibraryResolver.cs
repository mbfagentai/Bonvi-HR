using System.Runtime.InteropServices;

namespace Bonvi.IsupGateway.Native;

/// <summary>
/// <c>HCISUPCMS</c> nomini aniq fayl yo'liga bog'laydi.
/// </summary>
/// <remarks>
/// <para>
/// Tizim qidiruv yo'llariga (<c>LD_LIBRARY_PATH</c>) tayanilmaydi: kutubxona
/// SHLYUZ bilan birga keladigan jilddan yuklanadi, boshqa joydagi bir xil
/// nomli fayl hech qachon tanlanmaydi. Uning o'z bog'liqliklari
/// (<c>libhpr.so</c>) o'sha jilddan <c>RPATH=$ORIGIN</c> orqali topiladi.
/// </para>
/// <para>
/// Yo'q bo'lsa — yuklashda tushunarli xato, jim «topilmadi» emas.
/// </para>
/// </remarks>
internal static class NativeLibraryResolver
{
    private static int _registered;

    public static void Register(string directory)
    {
        // `SetDllImportResolver` yig'ma uchun faqat BIR MARTA chaqirilishi mumkin.
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, (name, _, _) => Resolve(name, directory));
    }

    private static IntPtr Resolve(string name, string directory)
    {
        if (!string.Equals(name, IsupNative.Library, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        var path = Path.Combine(directory, "libHCISUPCMS.so");

        if (!File.Exists(path))
        {
            throw new DllNotFoundException(
                $"Hikvision ISUP kutubxonasi topilmadi: {path}. Qarang: api/native/hikvision-isup/README.md");
        }

        return NativeLibrary.Load(path);
    }
}
