using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.Seed;

/// <summary>
/// Standart rollarni bazaga yozadi, himoyalangan rollarni koddagi ta'rif
/// bilan sinxronlaydi va eski ma'lumotni yangi ruxsat modeliga ko'chiradi.
/// </summary>
/// <remarks>
/// <para>
/// Nega migratsiyada emas: rol — ma'lumot, sxema emas. Migratsiyaga yozilsa,
/// administrator ruxsatni o'zgartirgan zahoti keyingi migratsiya uni ortga
/// qaytarib qo'yardi.
/// </para>
/// <para>
/// <b>2026-09-26 dan seeder ODDIY rollarga tegmaydi.</b> Ilgari u har
/// ishga tushishda barcha tizim rollarini koddagi ro'yxatga «to'g'rilab»
/// qo'yardi — ya'ni egasi «HR menejeri» dan olib tashlagan ruxsat keyingi
/// qayta ishga tushishda jimgina qaytib kelardi. Endi rollarni egasi
/// o'zi belgilaydi («Rollar» menyusi), shu sababli:
/// </para>
/// <list type="bullet">
///   <item>
///     <b><see cref="SystemRoles.SuperAdmin"/></b> — har safar
///     <see cref="Permissions.All"/> ga tenglanadi: kodga qo'shilgan yangi
///     ruxsat tizim egasiga o'zi yetib boradi.
///   </item>
///   <item>
///     <b><see cref="SystemRoles.RecruitingBot"/></b> — har safar o'zining
///     qat'iy ro'yxatiga tenglanadi (sababi <see cref="SystemRoles.RecruitingBot"/> izohida).
///   </item>
///   <item>
///     <b><see cref="SystemRoles.MainBoss"/>, <see cref="SystemRoles.HrManager"/></b>
///     — faqat BIRINCHI ishga tushishda (bazada super administrator hali
///     yo'q) standart ruxsatlar bilan yaratiladi va keyin HECH QACHON
///     o'zgartirilmaydi. Egasi ularni o'chirsa — qayta yaratilmaydi
///     (sababi <see cref="SeedAsync"/> ichida).
///   </item>
/// </list>
/// <para>
/// <b>Ikki bir martalik ko'chirish ham shu yerda</b>, chunki seeder har
/// ishga tushishda migratsiyalardan KEYIN bir marta bajariladi va ikkala
/// holatda ham to'g'ri ishlaydi (yangi bazada ular hech narsa qilmaydi):
/// <see cref="Kochir"/> (<c>admin</c> → <c>super_admin</c>) va
/// <see cref="EskiMehnatRuxsatiniBol"/> (<c>employment.manage</c> →
/// sakkizta amal kodi). Amal <b>xavfsiz takrorlanadi</b>: o'zgarish
/// bo'lmasa, bazaga yozuv ham bo'lmaydi.
/// </para>
/// <para>
/// Foydalanuvchi bu yerda yaratilmaydi: parol xeshlash Infrastructure'ning
/// boshqa qismiga tegishli va urug'lantirishni parol siyosatiga bog'lab qo'yardi.
/// Foydalanuvchilarning rol biriktirmalariga ham tegilmaydi.
/// </para>
/// </remarks>
public sealed class IdentitySeeder
{
    /// <summary>
    /// Seeder har safar koddagi ta'rifga TENGLAYDIGAN rollar.
    /// </summary>
    /// <remarks>
    /// <see cref="SystemRoles.Protected"/> bilan aynan bir xil to'plam,
    /// lekin ma'nosi boshqa («muharrir tegmaydi» va «seeder boshqaradi»).
    /// Ikkalasi hozircha mos keladi, chunki himoyalangan rolning ruxsatlarini
    /// kimdir boshqarishi kerak va u odam emas, kod.
    /// </remarks>
    private static readonly IReadOnlySet<string> SyncedRoles = SystemRoles.Protected;

    private readonly AppDbContext _context;

    public IdentitySeeder(AppDbContext context) => _context = context;

    /// <summary>
    /// Yetishmayotgan rollarni yaratadi, himoyalangan rollarni sinxronlaydi
    /// va eski ruxsatlarni ko'chiradi.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Proyeksiya emas, to'liq obyektlar: ruxsatlarni o'zgartirish uchun
        // EF Core kuzatuvi (change tracking) kerak.
        var mavjudRollar = await _context.Roles
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        /*
          «Birinchi ishga tushish» — bazada super administrator HALI yo'q:
          yangi baza yoki ikki rolli modeldan kelgan eski baza (u yerda
          `admin` bor, `super_admin` esa yo'q — `Kochir` uni endi yaratadi).

          Oddiy rollar (bosh rahbar, HR menejeri) FAQAT shu holatda
          yaratiladi. Sabab: endi ularni egasi o'chira oladi. «Yo'q bo'lsa
          yarat» qoidasi har ishga tushishda ishlasa, egasi o'chirgan rol
          keyingi deploy'da jimgina qaytib kelardi — va bu aynan egasining
          «rollarni men o'zim belgilayman» degan qaroriga zid.
        */
        var birinchiIshga = !mavjudRollar.Exists(role =>
            string.Equals(role.Code, SystemRoles.SuperAdmin, StringComparison.Ordinal));

        // Ko'chirish SINXRONLASHDAN OLDIN: qayta nomlangandan keyin eski rol
        // `super_admin` kodi bilan topiladi va uning ruxsatlari o'sha yerda,
        // umumiy yo'l bilan tekshiriladi. Teskari tartibda esa seeder ikkinchi
        // `super_admin` yaratib qo'yardi.
        var ozgardimi = Kochir(mavjudRollar);

        if (EskiMehnatRuxsatiniBol(mavjudRollar))
        {
            ozgardimi = true;
        }

        var yangiRollar = new List<Role>();

        foreach (var (kod, tavsif) in SystemRoles.Defaults)
        {
            var mavjud = mavjudRollar.Find(role => string.Equals(role.Code, kod, StringComparison.Ordinal));
            var sinxronlanadi = SyncedRoles.Contains(kod);

            if (mavjud is null)
            {
                if (sinxronlanadi || birinchiIshga)
                {
                    yangiRollar.Add(Yarat(kod, tavsif.Name, tavsif.Permissions));
                }

                continue;
            }

            if (!sinxronlanadi || !mavjud.IsSystem)
            {
                // Oddiy rol — egasiniki, seeder unga tegmaydi. `IsSystem`
                // bo'lmagan himoyalangan kod esa nazariy holat: kod unikal
                // va tizim rolini faqat shu seeder yaratadi.
                continue;
            }

            if (Tengla(mavjud, tavsif.Permissions))
            {
                ozgardimi = true;
            }
        }

        if (yangiRollar.Count > 0)
        {
            _context.Roles.AddRange(yangiRollar);
        }

        if (yangiRollar.Count == 0 && !ozgardimi)
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ikki rolli modeldan uch rolliga BIR MARTALIK ko'chirish:
    /// <c>admin</c> → <c>super_admin</c>.
    /// </summary>
    /// <returns>Ko'chirish bajarilgan bo'lsa — <c>true</c>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Nega qayta nomlash, nega yangi rol emas.</b> Rolning
    /// <c>Id</c> si o'zgarmasa, foydalanuvchilardagi
    /// <c>role_ids</c> massivi ham tegilmaydi — hech kim bir lahzaga ham
    /// rolsiz qolmaydi va <c>.env</c> dagi boshlang'ich hisob o'z huquqini
    /// yo'qotmaydi. Yangi rol yaratilsa, har bir foydalanuvchi qatorini
    /// qayta yozish kerak bo'lardi; ishga tushish paytida uzilish esa bir
    /// qism hisoblarni ruxsatsiz qoldirardi.
    /// </para>
    /// <para>
    /// <b>Nega migratsiya emas.</b> Rol — ma'lumot, sxema emas (fayl
    /// boshidagi izohga qarang). Migratsiyada <c>UPDATE roles SET code=…</c>
    /// yozish mumkin edi, lekin u faqat MAVJUD bazalarga tegardi va yangi
    /// bazada ma'nosiz qadam bo'lib qolardi. Seeder esa ikkala holatda ham
    /// to'g'ri ishlaydi: yangi bazada <c>admin</c> umuman yo'q va bu metod
    /// hech narsa qilmaydi.
    /// </para>
    /// <para>
    /// <b>Nega faqat tizim roli.</b> Administrator qo'lda <c>admin</c> kodli
    /// oddiy rol yaratgan bo'lsa (<c>IsSystem == false</c>), unga tegilmaydi
    /// — u boshqa maqsad uchun. Ikkala kod bir vaqtda uchrasa (<c>admin</c>
    /// ham, <c>super_admin</c> ham bor) ham ko'chirish bajarilmaydi: bu holat
    /// faqat qo'lda aralashuvdan kelib chiqadi va uni jimgina «tuzatish»
    /// ikkita rolni bir-biriga urib yuborardi. Eski rol o'z holicha qoladi,
    /// <see cref="SystemRoles.Defaults"/> da yo'qligi uchun sinxronlashga
    /// ham tushmaydi.
    /// </para>
    /// </remarks>
    private static bool Kochir(List<Role> mavjudRollar)
    {
        var eski = mavjudRollar.Find(role =>
            role.IsSystem && string.Equals(role.Code, SystemRoles.LegacyAdmin, StringComparison.Ordinal));

        if (eski is null)
        {
            return false;
        }

        var yangiBor = mavjudRollar.Exists(role =>
            string.Equals(role.Code, SystemRoles.SuperAdmin, StringComparison.Ordinal));

        if (yangiBor)
        {
            return false;
        }

        var (nom, _) = SystemRoles.Defaults[SystemRoles.SuperAdmin];

        var natija = eski.Recode(SystemRoles.SuperAdmin, nom);

        if (natija.IsFailure)
        {
            // Kodda yozilgan qiymat rad etildi — bu dastur xatosi, ma'lumot
            // xatosi emas. Jimgina o'tkazib yuborilsa, tizim eski rol bilan
            // ishlab ketardi va sabab hech qayerda ko'rinmasdi.
            throw new InvalidOperationException(
                $"«{SystemRoles.LegacyAdmin}» roli «{SystemRoles.SuperAdmin}» ga ko'chirilmadi: {natija.Error}");
        }

        return true;
    }

    /// <summary>
    /// Eski yagona <c>employment.manage</c> ruxsatini sakkizta amal kodiga
    /// almashtiradi — BIR MARTALIK ko'chirish.
    /// </summary>
    /// <returns>Biror rol o'zgargan bo'lsa — <c>true</c>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Hech kim deploy paytida huquq yo'qotmaydi:</b> eski kodni olgan
    /// har bir rol (egasi yaratgani ham) uning o'rniga
    /// <see cref="Permissions.EmploymentActions"/> ning HAMMASINI oladi —
    /// ya'ni ertalab qila olgan har bir amalini deploy'dan keyin ham qiladi.
    /// Bo'linishni (masalan okladni faqat «Kadr bo'limi boshlig'i» ga
    /// qoldirishni) egasi keyin o'zi, muharrirdan qiladi.
    /// </para>
    /// <para>
    /// Birinchi ishdan keyin bironta rolda eski kod qolmaydi, ya'ni bu metod
    /// hech narsa qilmaydi — alohida «bajarildi» belgisi kerak emas.
    /// </para>
    /// <para>
    /// Super administrator bu yerda o'tkazib yuboriladi: undan ruxsat olib
    /// tashlab bo'lmaydi (<see cref="Role.Revoke"/>), uning ro'yxati esa
    /// baribir quyida <see cref="Permissions.All"/> ga tenglanadi — eski
    /// kod o'sha yerda tushib qoladi.
    /// </para>
    /// </remarks>
    private static bool EskiMehnatRuxsatiniBol(List<Role> mavjudRollar)
    {
        var ozgardimi = false;

        foreach (var rol in mavjudRollar.Where(role => role.Has(Permissions.LegacyEmploymentManage)))
        {
            if (string.Equals(rol.Code, SystemRoles.SuperAdmin, StringComparison.Ordinal))
            {
                continue;
            }

            var olish = rol.Revoke(Permissions.LegacyEmploymentManage);
            if (olish.IsFailure)
            {
                throw new InvalidOperationException(
                    $"«{rol.Code}» rolidan «{Permissions.LegacyEmploymentManage}» olinmadi: {olish.Error}");
            }

            foreach (var amal in Permissions.EmploymentActions)
            {
                var berish = rol.Grant(amal);
                if (berish.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"«{rol.Code}» roliga «{amal}» ruxsati berilmadi: {berish.Error}");
                }
            }

            ozgardimi = true;
        }

        return ozgardimi;
    }

    /// <summary>
    /// Seeder boshqaradigan rolning ruxsatlarini <paramref name="kerakli"/>
    /// ro'yxatga TENGLAYDI: yetishmagani qo'shiladi, ortiqchasi tushadi.
    /// </summary>
    /// <returns>Ro'yxat o'zgargan bo'lsa — <c>true</c>.</returns>
    /// <remarks>
    /// To'plam sifatida solishtiriladi: faqat tartibi farq qilgan ro'yxat
    /// qayta yozilmaydi — aks holda har ishga tushish bekorga saqlash va
    /// ruxsatlar keshini tozalash bilan tugardi.
    /// </remarks>
    private static bool Tengla(Role rol, IReadOnlyList<string> kerakli)
    {
        if (rol.Permissions.ToHashSet(StringComparer.Ordinal).SetEquals(kerakli))
        {
            return false;
        }

        var natija = rol.ReplacePermissions(kerakli);

        if (natija.IsFailure)
        {
            // Kodda yozilgan ro'yxat rad etildi — dastur xatosi, ma'lumot xatosi emas.
            throw new InvalidOperationException(
                $"«{rol.Code}» rolining ruxsatlari yangilanmadi: {natija.Error}");
        }

        return true;
    }

    /// <summary>
    /// Tizim rolini yig'adi. Xatolik bu yerda istisno: <see cref="SystemRoles"/>
    /// kodda yozilgan, ya'ni noto'g'ri qiymat — dastur xatosi, foydalanuvchi xatosi emas.
    /// </summary>
    private static Role Yarat(string kod, string nom, IReadOnlyList<string> ruxsatlar)
    {
        var natija = Role.Create(kod, nom, isSystem: true);

        if (natija.IsFailure)
        {
            throw new InvalidOperationException(
                $"«{kod}» tizim roli yaratilmadi: {natija.Error}");
        }

        var role = natija.Value;

        foreach (var ruxsat in ruxsatlar)
        {
            var berish = role.Grant(ruxsat);

            if (berish.IsFailure)
            {
                throw new InvalidOperationException(
                    $"«{kod}» roliga «{ruxsat}» ruxsati berilmadi: {berish.Error}");
            }
        }

        return role;
    }
}
