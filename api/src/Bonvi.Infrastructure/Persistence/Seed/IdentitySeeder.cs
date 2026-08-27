using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.Seed;

/// <summary>
/// Standart rollarni bazaga yozadi va ularning ruxsatlarini koddagi
/// ta'rif (<see cref="SystemRoles.Defaults"/>) bilan sinxronlaydi.
/// </summary>
/// <remarks>
/// <para>
/// Nega migratsiyada emas: rol — ma'lumot, sxema emas. Migratsiyaga yozilsa,
/// administrator ruxsatni o'zgartirgan zahoti keyingi migratsiya uni ortga
/// qaytarib qo'yardi.
/// </para>
/// <para>
/// Yo'q rollar <b>yaratiladi</b>, mavjud <b>tizim</b> rollarining ruxsatlari
/// esa koddagi ro'yxatga <b>moslashtiriladi</b>: yetishmagani qo'shiladi,
/// ortiqchasi olib tashlanadi. Bu qasddan — avvalgi holat faqat qo'shar edi:
/// <c>Permissions</c> ga yangi konstanta qo'shilsa, allaqachon yaratilgan
/// rolga u hech qachon yetib bormasdi (masalan <c>incentives.view</c> —
/// bazada rol bir marta yaratilgach, kod bilan baza abadiy uzoqlashib
/// ketardi). Amal hamon <b>xavfsiz takrorlanadi</b>: o'zgarish bo'lmasa,
/// bazaga yozuv ham bo'lmaydi.
/// </para>
/// <para>
/// Faqat <see cref="Role.IsSystem"/> = <c>true</c> bo'lgan va kodi
/// <see cref="SystemRoles.Defaults"/> da ro'yxatda turgan rollarga tegiladi.
/// Administrator qo'lda yaratgan (yoki tizim rolidan nusxa olib o'zgartirgan)
/// har qanday boshqa rol — <c>IsSystem == false</c> — bu yerda umuman
/// ko'rilmaydi. Rahbariyat rollaridan ruxsat olib tashlash esa bu yerda ham
/// so'ralmaydi: <see cref="Role.Revoke"/> buni allaqachon domen qoidasi
/// sifatida taqiqlaydi (rahbariyat o'zini huquqsiz qoldira olmaydi),
/// shu bilan birga <c>Defaults</c> ikkalasiga ham aynan
/// <see cref="Permissions.All"/> berganidan ular doim to'liq ro'yxatni oladi.
/// </para>
/// <para>
/// <b>Uch rolli modelga KO'CHIRISH ham shu yerda</b>
/// (<see cref="Kochir"/>): allaqachon ishlab turgan bazadagi
/// <c>admin</c> roli <c>super_admin</c> ga QAYTA NOMLANADI. Ko'chirishning
/// tabiiy joyi aynan shu, chunki seeder allaqachon rol jadvalini kod bilan
/// solishtirib turadi va u har ishga tushishda BIR MARTA, migratsiyalardan
/// KEYIN bajariladi.
/// </para>
/// <para>
/// Foydalanuvchi bu yerda yaratilmaydi: parol xeshlash Infrastructure'ning
/// boshqa qismiga tegishli va urug'lantirishni parol siyosatiga bog'lab qo'yardi.
/// Foydalanuvchilarning rol biriktirmalariga ham tegilmaydi — faqat rol
/// ICHIDAGI ruxsatlar ro'yxati sinxronlanadi va (bir marta) rol kodi.
/// </para>
/// </remarks>
public sealed class IdentitySeeder
{
    private readonly AppDbContext _context;

    public IdentitySeeder(AppDbContext context) => _context = context;

    /// <summary>
    /// Yetishmayotgan standart rollarni yaratadi va mavjud tizim rollarining
    /// ruxsatlarini koddagi ta'rifga moslashtiradi.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Proyeksiya emas, to'liq obyektlar: ruxsatlarni Grant/Revoke orqali
        // o'zgartirish uchun EF Core kuzatuvi (change tracking) kerak.
        var mavjudRollar = await _context.Roles
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ko'chirish SINXRONLASHDAN OLDIN: qayta nomlangandan keyin eski rol
        // `super_admin` kodi bilan topiladi va uning ruxsatlari o'sha yerda,
        // umumiy yo'l bilan tekshiriladi. Teskari tartibda esa seeder ikkinchi
        // `super_admin` yaratib qo'yardi.
        var kochirildi = Kochir(mavjudRollar);

        var yangiRollar = new List<Role>();
        var ozgardimi = kochirildi;

        foreach (var (kod, tavsif) in SystemRoles.Defaults)
        {
            var mavjud = mavjudRollar.Find(role => string.Equals(role.Code, kod, StringComparison.Ordinal));

            if (mavjud is null)
            {
                yangiRollar.Add(Yarat(kod, tavsif.Name, tavsif.Permissions));
                continue;
            }

            if (!mavjud.IsSystem)
            {
                // Nazariy jihatdan yuz bermaydi: `Code` unikal va o'zgarmas,
                // tizim rolini faqat shu seeder `isSystem: true` bilan yaratadi.
                // Baribir — o'zgartirilgan (endi tizim bo'lmagan) rolga tegilmaydi.
                continue;
            }

            if (Muvofiqlashtir(mavjud, tavsif.Permissions))
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
    /// Mavjud tizim rolining ruxsatlarini <paramref name="kerakli"/> ro'yxatga
    /// moslashtiradi: yetishmaganini qo'shadi, ortiqchasini olib tashlaydi.
    /// </summary>
    /// <returns>Biror ruxsat qo'shilgan yoki olib tashlangan bo'lsa — <c>true</c>.</returns>
    /// <remarks>
    /// Rahbariyat rollari (<see cref="SystemRoles.FullAccess"/>) uchun olib
    /// tashlash amalda hech qachon sodir bo'lmaydi: <see cref="Role.Revoke"/>
    /// uni domen qoidasi sifatida rad etadi — bu yerda alohida tekshirilmaydi,
    /// natija shunchaki e'tiborsiz qoldiriladi.
    /// </remarks>
    private static bool Muvofiqlashtir(Role rol, IReadOnlyList<string> kerakli)
    {
        var ozgardimi = false;

        foreach (var ruxsat in kerakli)
        {
            if (rol.Has(ruxsat))
            {
                continue;
            }

            var berish = rol.Grant(ruxsat);

            if (berish.IsFailure)
            {
                throw new InvalidOperationException(
                    $"«{rol.Code}» roliga «{ruxsat}» ruxsati berilmadi: {berish.Error}");
            }

            ozgardimi = true;
        }

        var keraklilar = new HashSet<string>(kerakli, StringComparer.Ordinal);

        foreach (var ortiqcha in rol.Permissions.Where(ruxsat => !keraklilar.Contains(ruxsat)).ToList())
        {
            if (rol.Revoke(ortiqcha).IsFailure)
            {
                // Faqat rahbariyat rollari uchun bloklanadi (Role.Revoke qoidasi) — xato emas.
                continue;
            }

            ozgardimi = true;
        }

        return ozgardimi;
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
