import { useNavigate, useRouterState } from '@tanstack/react-router'
import { Search } from 'lucide-react'
import { useState, type FormEvent } from 'react'

import { useAuth } from '@/features/auth'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'
import { validateEmployeesSearch } from '@/features/employees/lib/employees-search'
import { useLocale } from '@/shared/i18n/use-locale'
import { Input } from '@/shared/ui'

/**
 * Xodimlar ro'yxatining yo'li.
 *
 * `const` — TypeScript uni satr LITERALI deb biladi, ya'ni router
 * `to` ni tanib, mavjud bo'lmagan manzilni yozib qo'yishga yo'l
 * qo'ymaydi. Oddiy `string` bo'lsa bu tekshiruv yo'qolardi.
 */
const EMPLOYEES_PATH = '/xodimlar'

/**
 * Yuqori paneldagi umumiy qidiruv.
 *
 * Enter bosilganda xodimlar ro'yxatiga o'tadi va matnni manzilga
 * (`/xodimlar?q=…`) qo'yadi. Qidiruvning O'ZI bu yerda yo'q: ro'yxat
 * allaqachon F.I.Sh., JSHSHIR, pasport va telefon bo'yicha SERVER
 * tomonda qidiradi va natijani sahifalab beradi.
 *
 * NEGA har harfda ochiladigan tezkor natija oynasi (dropdown) EMAS —
 * uch sabab:
 *   1. U yangi endpoint, yangi komponent, o'zining klaviatura
 *      boshqaruvi (yuqoriga/pastga, Escape, fokus qopqoni) va o'zining
 *      «yuklanmoqda/bo'sh/xato» holatlarini talab qiladi — holbuki
 *      oxirida foydalanuvchini baribir shu ro'yxatga olib borardi.
 *   2. Ro'yxatda filtr, saralash, ikki ko'rinish va sahifalash bor.
 *      Kichik oyna ularning hech birini bera olmaydi, ya'ni u qidiruvning
 *      IKKINCHI, kambag'alroq yo'li bo'lib qolardi — va vaqt o'tib
 *      birinchisidan ajralib ketardi (ro'yxatga qo'shilgan yangi filtr
 *      oynaga qo'shilmay qolardi).
 *   3. HR odatda bitta odamni emas, «Karimovlar»ni ko'radi — beshta
 *      qatorlik oyna emas, to'liq ro'yxat kerak.
 *
 * Ya'ni bu yerdagi eng arzon va eng to'g'ri ish — matnni manzilga uzatib,
 * tayyor ekranga topshirish.
 */
export function TopbarSearch() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const navigate = useNavigate()

  /*
   * Joriy manzil — yuborish paytida «ro'yxatning o'zidamizmi?» degan
   * savolga javob beradi (pastdagi `submit`). `useRouterState` sahifadan
   * sahifaga o'tganda panelni qayta chizadi, lekin panel qobiqda bir
   * marta o'rnatiladi va bu bir nechta element — arzon.
   */
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const search = useRouterState({ select: (state) => state.location.search })

  /*
   * Maydon matni — MAHALLIY holat, manzil bilan sinxronlanmaydi.
   *
   * NEGA sinxronlanmaydi: ro'yxatning o'z qidiruv maydoni har 350 ms da
   * manzilga yozadi (`employees-toolbar.tsx`), ya'ni sinxron bo'lsa
   * pastdagi maydonga yozilgan matn yuqoridagisiga «sakrab» chiqib,
   * foydalanuvchi qaysi maydonda ishlayotganini bilmay qolardi. Ikkita
   * qidiruv maydoni bir-birini boshqarmagani ma'qul: yuqoridagisi —
   * «ro'yxatga o'tish», pastdagisi — «shu ro'yxatni toraytirish».
   */
  const [text, setText] = useState('')

  /*
   * Ruxsati yo'q bo'lsa maydon UMUMAN chizilmaydi.
   *
   * NEGA o'chirilgan (`disabled`) holatda emas: `/xodimlar` sahifasi
   * `employees.view` talab qiladi. Maydonni ko'rsatib qo'ysak,
   * foydalanuvchi familiyasini yozib, Enter bosib, 403 ga tushardi —
   * bu bajarilmaydigan va'da. Yon menyudagi bandlar ham aynan shu qoida
   * bilan filtrlanadi (`nav-items.ts` dagi `permission` izohi).
   *
   * Barcha hook'lardan KEYIN: React qoidasi bo'yicha ular har render'da
   * bir xil tartibda chaqirilishi shart.
   */
  if (!hasPermission(EMPLOYEES_VIEW)) return null

  const submit = (event: FormEvent<HTMLFormElement>) => {
    // Standart yuborish sahifani qayta yuklaydi — SPA'da bu butun
    // ilovani noldan ko'tarish demak
    event.preventDefault()

    /*
     * Bu yerda matn QIRQILADI — `employees-toolbar.tsx` dan farqli.
     * Sabab: u yerda yozish davom etayotgan bo'lishi mumkin va qirqilgan
     * probel 350 ms dan keyin maydondan yo'qolardi. Bu yerda esa Enter
     * bosilgan, ya'ni yozish TUGAGAN.
     */
    const query = text.trim()

    /*
     * Bo'sh qidiruv bilan hech qayerga o'tilmaydi.
     *
     * NEGA: bo'sh `q` — «butun ro'yxatni och» degani, bu esa Enter bosgan
     * odamning niyati emas. Bundan tashqari, ro'yxatda turgan odam
     * maydonni tozalab Enter bossa, mavjud qidiruvi ham yo'qolib ketardi.
     */
    if (query === '') return

    const onList = pathname === EMPLOYEES_PATH

    void navigate({
      to: EMPLOYEES_PATH,
      /*
       * Ro'yxatda turgan bo'lsak — mavjud manzil holati saqlanadi:
       * `?department=`, `?status=terminated` («Ishdan ketganlar» bandi),
       * saralash va ko'rinish yo'qolmasin. Boshqa sahifadan kelayotgan
       * bo'lsak — toza holat: o'sha sahifaning o'z parametrlarini
       * (masalan hisobotdagi `?period=year`) sudrab kelishning ma'nosi yo'q.
       *
       * Manzil `validateEmployeesSearch` orqali o'tkaziladi — ro'yxatning
       * o'z marshruti ham aynan shundan foydalanadi, ya'ni qoida bitta
       * joyda turadi va notanish parametr bu yerdan o'tib ketolmaydi.
       *
       * Sahifa raqami esa ATAYLAB 1 ga qaytariladi (`undefined` —
       * standart qiymat manzilga yozilmaydi): yangi qidiruvda 3-sahifada
       * qolish «hech narsa topilmadi» degan yolg'on taassurot berardi.
       */
      search: {
        ...(onList ? validateEmployeesSearch(search) : {}),
        q: query,
        page: undefined,
      },
      /*
       * Ro'yxatda turgan bo'lsak — tarixga yozilmaydi.
       *
       * NEGA: bu holda qidiruv shunchaki filtrni aniqlashtirish, xuddi
       * ro'yxatning o'z maydoniga yozgandek (`employees-page.tsx` da ham
       * filtr o'zgarishi `replace`). Aks holda qayta-qayta Enter bosgan
       * odam «orqaga» tugmasini o'nlab marta bosishga majbur bo'lardi.
       * Boshqa sahifadan kelganda esa bu HAQIQIY qadam — «orqaga»
       * foydalanuvchini o'zi turgan ekranga qaytarishi kerak.
       */
      replace: onList,
    })
  }

  return (
    /*
     * ATAYLAB forma: Enter bilan yuborish shunda BEPUL keladi (alohida
     * `onKeyDown` tekshiruvi kerak emas) va mobil klaviaturada «bajarish»
     * tugmasi qidiruv ko'rinishini oladi. `role="search"` — ekran
     * o'quvchilar uchun standart mo'ljal belgisi.
     */
    <form role="search" onSubmit={submit}>
      <Input
        icon={Search}
        shape="pill"
        size="lg"
        type="search"
        value={text}
        onChange={(event) => setText(event.target.value)}
        placeholder={t('common.searchPlaceholder')}
        aria-label={t('common.search')}
      />
    </form>
  )
}
