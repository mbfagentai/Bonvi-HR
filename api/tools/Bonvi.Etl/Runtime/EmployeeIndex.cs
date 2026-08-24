using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Etl.Mapping;

namespace Bonvi.Etl.Runtime;

/// <summary>Xodim qanday topilgani.</summary>
public enum EmployeeMatchKind
{
    /// <summary>Topilmadi — yangi xodim.</summary>
    None = 0,

    /// <summary>JSHSHIR bo'yicha — eng ishonchli.</summary>
    Pinfl = 1,

    /// <summary>Pasport bo'yicha.</summary>
    Passport = 2,

    /// <summary>Ism va tug'ilgan sana bo'yicha — oxirgi chora.</summary>
    NameAndBirthDate = 3,

    /// <summary>Faqat ism bo'yicha, va u YAGONA edi.</summary>
    Name = 4,

    /// <summary>Bir nechta xodim mos keldi — tanlash mumkin emas.</summary>
    Ambiguous = 5,
}

/// <summary>Qidiruv natijasi.</summary>
/// <param name="Kind">Qanday topilgani.</param>
/// <param name="Employee">Topilgan xodim (<see cref="EmployeeMatchKind.None"/> va
/// <see cref="EmployeeMatchKind.Ambiguous"/> da <c>null</c>).</param>
/// <param name="Candidates">Nomzodlar soni (noaniqlikda 2 va undan ko'p).</param>
public readonly record struct EmployeeMatch(EmployeeMatchKind Kind, Employee? Employee, int Candidates)
{
    /// <summary>Hech narsa topilmadi.</summary>
    public static readonly EmployeeMatch None = new(EmployeeMatchKind.None, null, 0);

    /// <summary>Xodim aniq topildi.</summary>
    public bool Found => Employee is not null;
}

/// <summary>
/// Xodimlarni takrorlanmasligi uchun indekslaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotentlikning asosi.</b> ETL qayta ishga tushirilganda mavjud
/// xodimni topib, unga tegmasligi kerak. Buning uchun bazadagi barcha
/// xodimlar boshida xotiraga o'qiladi (~2300 yozuv — bu hech narsa) va
/// har bir Excel qatori shu indeksdan qidiriladi.
/// </para>
/// <para>
/// <b>Qidiruv tartibi ataylab shunday:</b>
/// </para>
/// <list type="number">
///   <item><b>JSHSHIR</b> — davlat bergan raqam, hech qachon o'zgarmaydi;</item>
///   <item><b>Pasport</b> — almashadi, lekin bir vaqtda ikki odamda bo'lmaydi;</item>
///   <item><b>Ism + tug'ilgan sana</b> — eng zaif dalil, lekin 3 ta xodimda
///         JSHSHIR ham, pasport ham yo'q.</item>
/// </list>
/// <para>
/// Ism bo'yicha bir nechta xodim topilsa — <see cref="EmployeeMatchKind.Ambiguous"/>.
/// ETL bunday holatda <b>tanlamaydi</b>: ikkita bir xil ismli odamdan noto'g'risiga
/// hodisa yozib qo'yish, hodisani umuman yozmaslikdan yomonroq.
/// </para>
/// </remarks>
public sealed class EmployeeIndex
{
    private readonly Dictionary<string, Employee> _byPinfl = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Employee> _byPassport = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Employee>> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Employee>> _byShortName = new(StringComparer.Ordinal);

    /// <summary>Indeksdagi xodimlar soni.</summary>
    public int Count { get; private set; }

    /// <summary>Xodimni indeksga qo'shadi.</summary>
    public void Add(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        if (employee.Pinfl is { } pinfl)
        {
            _ = _byPinfl.TryAdd(pinfl.Value, employee);
        }

        if (employee.Passport is { } passport)
        {
            _ = _byPassport.TryAdd(passport.Value, employee);
        }

        var nameKey = NameKey(employee.FullName);
        if (!_byName.TryGetValue(nameKey, out var list))
        {
            list = [];
            _byName[nameKey] = list;
        }

        list.Add(employee);

        var shortKey = ShortKey(employee.FullName);
        if (!_byShortName.TryGetValue(shortKey, out var sameShort))
        {
            sameShort = [];
            _byShortName[shortKey] = sameShort;
        }

        sameShort.Add(employee);
        Count++;
    }

    /// <summary>
    /// Xodim allaqachon bormi. Tartib: JSHSHIR → pasport → ism va tug'ilgan sana.
    /// </summary>
    public EmployeeMatch Find(Pinfl? pinfl, PassportSeries? passport, FullName? fullName, DateOnly? birthDate)
    {
        if (pinfl is not null && _byPinfl.TryGetValue(pinfl.Value, out var byPinfl))
        {
            return new EmployeeMatch(EmployeeMatchKind.Pinfl, byPinfl, 1);
        }

        if (passport is not null && _byPassport.TryGetValue(passport.Value, out var byPassport))
        {
            return new EmployeeMatch(EmployeeMatchKind.Passport, byPassport, 1);
        }

        if (fullName is null)
        {
            return EmployeeMatch.None;
        }

        if (!_byName.TryGetValue(NameKey(fullName), out var sameName) || sameName.Count == 0)
        {
            return EmployeeMatch.None;
        }

        if (birthDate is { } birth)
        {
            var matching = sameName.FindAll(candidate => candidate.BirthDate == birth);

            if (matching.Count == 1)
            {
                return new EmployeeMatch(EmployeeMatchKind.NameAndBirthDate, matching[0], 1);
            }

            if (matching.Count > 1)
            {
                return new EmployeeMatch(EmployeeMatchKind.Ambiguous, null, matching.Count);
            }

            // Ism mos, sana mos emas. Bu boshqa odam bo'lishi ham, sana xato
            // kiritilgan bo'lishi ham mumkin — TAXMIN QILINMAYDI.
            return sameName.Count == 1
                ? new EmployeeMatch(EmployeeMatchKind.Ambiguous, null, 1)
                : new EmployeeMatch(EmployeeMatchKind.Ambiguous, null, sameName.Count);
        }

        return sameName.Count == 1
            ? new EmployeeMatch(EmployeeMatchKind.Name, sameName[0], 1)
            : new EmployeeMatch(EmployeeMatchKind.Ambiguous, null, sameName.Count);
    }

    /// <summary>
    /// Faqat ism bo'yicha qidiradi — tug'ilgan sanasi yo'q varaqlar uchun
    /// («91.3 Лавозими ўзгарган», «91.5 Синов муддати»).
    /// </summary>
    public EmployeeMatch FindByName(FullName fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        if (!_byName.TryGetValue(NameKey(fullName), out var sameName) || sameName.Count == 0)
        {
            return EmployeeMatch.None;
        }

        return sameName.Count == 1
            ? new EmployeeMatch(EmployeeMatchKind.Name, sameName[0], 1)
            : new EmployeeMatch(EmployeeMatchKind.Ambiguous, null, sameName.Count);
    }

    /// <summary>
    /// Familiya va ism bo'yicha YAGONA nomzodni topadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu <b>bog'lash usuli emas</b> — hisobot uchun maslahat. Hodisa
    /// varaqlarida otasining ismi tez-tez tushib qolgan
    /// («Абдуллаев Умиджон» ↔ «Абдуллаев Умиджон Мўйдинжон ўғли»), va
    /// bunday 99 ta odam bor.
    /// </para>
    /// <para>
    /// ETL ularni <b>o'zi bog'lamaydi</b>: O'zbekistonda aka-uka bir xil
    /// familiya va ismda bo'lishi odatiy hol, ya'ni «yagona moslik» hali
    /// «bir xil odam» degani emas. Noto'g'ri odamga yozilgan ishdan
    /// bo'shatish buyrug'i — yo'qolgan yozuvdan ancha yomonroq. Shu sababli
    /// nomzod faqat hisobotda ko'rsatiladi va qarorni HR qabul qiladi.
    /// </para>
    /// </remarks>
    public Employee? SuggestByShortName(FullName fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);

        return _byShortName.TryGetValue(ShortKey(fullName), out var candidates) && candidates.Count == 1
            ? candidates[0]
            : null;
    }

    /// <summary>Familiya va ism bo'yicha kalit — otasining ismisiz.</summary>
    private static string ShortKey(FullName fullName)
        => AliasTable.Key($"{fullName.LastName} {fullName.FirstName}");

    /// <summary>
    /// Ism bo'yicha solishtirish kaliti.
    /// </summary>
    /// <remarks>
    /// Registr va ortiqcha bo'shliqlar hisobga olinmaydi. Kirill imlo
    /// variantlari (<c>ў/у</c>) ATAYLAB birlashtirilmaydi: «Тошпулатов» va
    /// «Тўшпулатов» turli familiya bo'lishi mumkin, ularni birlashtirish
    /// ikki odamni bittaga qo'shib yuborardi.
    /// </remarks>
    public static string NameKey(FullName fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        return AliasTable.Key(fullName.Full);
    }
}
