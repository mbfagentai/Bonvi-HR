using System.Text;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// Qidiruv matnini «bir xil ko'rinadigan» belgilardan tozalaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu kerak.</b> HR odamni topolmasa, uni bazada YO'Q deb hisoblaydi
/// va yangi karta ochadi — loyihaning eng katta muammosi (Excel'da bitta
/// odam uch marta yozilgani) aynan shundan boshlangan. Ya'ni topilmagan
/// qidiruv — bu bo'sh ekran emas, bu <b>kelajakdagi dublikat</b>.
/// </para>
/// <para>
/// Ikki xil «topilmaslik» aniqlangan va ikkalasi ham ko'zga ko'rinmaydi:
/// </para>
/// <list type="number">
///   <item>
///     <b>Ism: қ / ғ / ҳ / ў.</b> Bu harflar rus klaviaturasida YO'Q. HR
///     rus tartibida yozadi va «Абдуқодиров» o'rniga «Абдукодиров» kiritadi.
///     Ekranda ikkalasi deyarli bir xil, kompyuter uchun esa boshqa so'z.
///   </item>
///   <item>
///     <b>Pasport: kirill gomogliflari.</b> Excel'da 607 ta pasportda kirill
///     «А» (U+0410) turgan edi, lotin «A» (U+0041) emas. ETL ularni lotinga
///     o'girdi — lekin HR baribir kirill klaviaturada yozadi va «АВ5725936»
///     deb qidirganda yozuv ko'z oldida tursa ham topilmaydi.
///   </item>
/// </list>
/// <para>
/// <b>Nega bitta emas, ikkita almashtirish.</b> Ism ustuni kirillda, kod
/// ustunlari (pasport, JSHSHIR, telefon) esa lotin va raqamda saqlanadi.
/// Bitta umumiy qoida ikkalasini ham buzardi: kirill «С» ni lotin «C» ga
/// o'girsak «Собиров» «Cобиров» ga aylanib, ism bo'yicha qidiruv o'ladi.
/// Shu sababli bitta qidiruv matnidan IKKI naqsh hosil qilinadi va har
/// biri o'z ustunlariga qo'llanadi.
/// </para>
/// <para>
/// <b>Odam ismlari bu yerda EMAS.</b> 2026-10-14 dan xodim, nomzod va
/// foydalanuvchi ismi lotin/kirill farqsiz qidiriladi — bu qoida bazadagi
/// <c>search_key()</c> funksiyasida (migratsiya <c>IsmQidiruvKaliti</c>) va
/// uning C# nusxasi ATAYLAB yo'q. <see cref="ForName"/> endi faqat vakansiya
/// nomi qidiruvida (<c>VacancySearchQuery</c> dagi <c>translate(...)</c> bilan
/// AYNAN bir xil bo'lishi shart) va davomat faylidagi ismni xodimga ANIQ
/// moslashda ishlatiladi.
/// </para>
/// </remarks>
internal static class SearchFolding
{
    /// <summary>Ismda «yig'iladigan» harflar — kichik registrda.</summary>
    /// <remarks>
    /// <c>ў → у</c> ataylab: <see cref="DatabaseCollations"/> da tushuntirilgan
    /// qaror shu yerda ham davom etadi — «Тошпулатов» va «Тўшпулатов» bir
    /// odam bo'lishi mumkin va HR ikkalasini ham ko'rishi kerak.
    /// <c>ё → е</c> ham shu mantiqda: rus klaviaturasida «ё» alohida burchakda
    /// turadi va uni bosish odat emas.
    /// </remarks>
    public const string NameFoldFrom = "қғҳўё";

    /// <summary>Nimaga almashtiriladi. Uzunligi <see cref="NameFoldFrom"/> bilan teng.</summary>
    public const string NameFoldTo = "кгхуе";

    /// <summary>
    /// Kod maydonlaridagi kirill gomogliflari va ularning lotin juftligi.
    /// </summary>
    /// <remarks>
    /// Faqat SHAKLI bir xil bo'lgan juftliklar. «Д» va «Н» ro'yxatda yo'q:
    /// ular lotin «D» va «N» ga o'xshamaydi, ya'ni HR ularni adashtirib
    /// yozmaydi. Ortiqcha juftlik qo'shish qidiruvni «kengaytirmaydi» —
    /// u faqat noto'g'ri moslik keltiradi.
    /// </remarks>
    private static readonly (char Cyrillic, char Latin)[] CodeHomoglyphs =
    [
        ('а', 'a'), ('в', 'b'), ('е', 'e'), ('к', 'k'), ('м', 'm'),
        ('н', 'h'), ('о', 'o'), ('р', 'p'), ('с', 'c'), ('т', 't'),
        ('у', 'y'), ('х', 'x'), ('і', 'i'), ('ј', 'j'), ('ѕ', 's'),
    ];

    /// <summary>
    /// Ism ustuni uchun naqsh: kichik registr + qo'shimcha harflar yig'iladi.
    /// </summary>
    /// <remarks>
    /// Registr avval tushiriladi, chunki almashtirish jadvali faqat kichik
    /// harflarni biladi. <c>ILIKE</c> ning o'zi registrga befarq, lekin
    /// naqshdagi katta «Қ» yig'ilmasdan qolsa, u yig'ilgan ustunga hech
    /// qachon mos kelmasdi — xato aynan shu yerda tug'ilardi.
    /// </remarks>
    public static string ForName(string search)
        => Fold(search.ToLowerInvariant());

    /// <summary>
    /// Pasport va JSHSHIR ustunlari uchun naqsh: kirill gomogliflari
    /// lotinga o'giriladi.
    /// </summary>
    public static string ForCode(string search)
    {
        var lower = search.ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);

        foreach (var symbol in lower)
        {
            var replaced = symbol;

            foreach (var (cyrillic, latin) in CodeHomoglyphs)
            {
                if (symbol == cyrillic)
                {
                    replaced = latin;
                    break;
                }
            }

            _ = builder.Append(replaced);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Telefon ustunlari uchun naqsh: faqat raqamlar qoladi, hammasi
    /// boshqasi (bo'shliq, tire, qavs, «+») tashlab yuboriladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega alohida, <see cref="ForCode"/> emas.</b> <c>PhoneNumber</c>
    /// bazada kanonik shaklda saqlanadi (<c>+998905088888</c>), lekin HR
    /// ekranda ko'rgan formatlangan raqamni (<c>+998 90 508 88 88</c>)
    /// ko'chirib qidiruvga qo'yishi mumkin. <see cref="ForCode"/> faqat
    /// harflarni almashtiradi, ajratgichlarni tashlamaydi — natijada
    /// bo'shliqli qidiruv hech narsa topmasdi.
    /// </para>
    /// <para>
    /// <b>Nega pasport/JSHSHIR uchun ishlatilmaydi.</b> Ulardan harflarni
    /// olib tashlash pasport seriyasini (<c>AB7412589</c> dagi «AB»)
    /// yo'qotardi va qidiruv butunlay ishlamay qolardi. Shu sababli bu
    /// naqsh FAQAT <c>phone</c> va <c>second_phone</c> ustunlariga
    /// qo'llanadi.
    /// </para>
    /// <para>
    /// Natija bo'sh bo'lishi mumkin (HR ism bilan qidirsa, matnda birorta
    /// ham raqam yo'q) — bu holda chaqiruvchi telefon shartini SO'ROVGA
    /// UMUMAN QO'SHMASLIGI kerak, aks holda <c>%%</c> naqshi butun
    /// jadvalni olib kelardi.
    /// </para>
    /// </remarks>
    public static string ForPhone(string search)
    {
        var builder = new StringBuilder(search.Length);

        foreach (var symbol in search)
        {
            if (symbol is >= '0' and <= '9')
            {
                _ = builder.Append(symbol);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// SQL dagi <c>translate()</c> ning C# dagi aynan o'sha ishi.
    /// </summary>
    private static string Fold(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var symbol in value)
        {
            var index = NameFoldFrom.IndexOf(symbol, StringComparison.Ordinal);
            _ = builder.Append(index >= 0 ? NameFoldTo[index] : symbol);
        }

        return builder.ToString();
    }
}
