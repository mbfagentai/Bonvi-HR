import { z } from 'zod'

import type { EmployeeDetails } from '../api/employees-api'
import { todayIso } from '@/shared/lib/today'

/**
 * Ishga qabul formasi.
 *
 * Xato XABARI emas, xato KODI saqlanadi (`ism.familiya_bosh`) — xuddi
 * backend qaytaradigan kodlar kabi. Nega: tarjima sxema yaratilganda emas,
 * ekranga chizilganda tanlanadi, shu sababli til almashtirilganda ochiq
 * turgan xato ham darhol o'zgaradi. Va aynan shu kodni server ham
 * qaytaradi — klient va server xatolari foydalanuvchi uchun bir xil.
 *
 * ⚠️ Klient tekshiruvi — QULAYLIK, himoya emas. JSHSHIR nazorat raqami,
 * pasport seriyasi va stavka yig'indisi kabi qoidalar serverdagi
 * `Pinfl`, `PassportSeries` va `Employment` domen sinflarida qat'iy
 * tekshiriladi. Bu yerda faqat foydalanuvchini serverga bormasdan
 * ogohlantiradigan eng oddiy holatlar bor.
 */

/** Barcha maydonlar satr: bo'sh maydon `''` bo'ladi, `0` yoki `null` emas. */
const optionalText = z.string().trim()

/** «2024-01-15» — `<input type="date">` ning o'z formati. */
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/u

/** Ixtiyoriy sana: bo'sh qoldirilsa ham to'g'ri hisoblanadi. */
const dateText = optionalText.refine(
  (value) => value === '' || ISO_DATE.test(value),
  'sana.notogri',
)

/**
 * Shaxs maydonlari — ishga qabul va tahrirlash formalarida BIR XIL.
 *
 * NEGA umumiy: serverda ham ular bitta buyruq (`SaveEmployeeCommand`) —
 * ishga qabul ham, karta tahriri ham o'sha maydonlarni o'sha qoidalar
 * bilan qabul qiladi. Ikki sxemaga nusxalansa, JSHSHIR uzunligi yoki
 * telefon shakli bir joyda o'zgarganda ikkinchisi jimgina eskirardi va
 * buni faqat foydalanuvchi tushunarsiz xato olganda bilib qolardik.
 */
const personFields = {
  lastName: z.string().trim().min(1, 'ism.familiya_bosh').max(100, 'ism.uzun'),
  firstName: z.string().trim().min(1, 'ism.ism_bosh').max(100, 'ism.uzun'),
  middleName: optionalText.max(100, 'ism.uzun'),
  birthDate: dateText,
  /**
   * JSHSHIR bo'sh bo'lishi MUMKIN. Excel'dagi 540 yozuvdan 3 tasida u
   * umuman yo'q va domen ham buni qabul qiladi — majburiy qilinsa,
   * o'sha xodimlarni tizimga kiritib bo'lmaydi.
   */
  pinfl: optionalText.refine(
    (value) => value === '' || /^\d{14}$/u.test(value),
    'pinfl.uzunlik',
  ),
  /**
   * «AB1234567». Kichik harf ham qabul qilinadi — katta harfga
   * yuborishdan oldin (`toPassport`) o'giriladi. Sxema ichida
   * `.transform` ishlatilmaydi: u kirish va chiqish tiplarini
   * ajratib yuboradi va `zodResolver` bilan ziddiyat chiqadi.
   */
  passport: optionalText.refine(
    (value) => value === '' || /^[A-Za-z]{2}\d{7}$/u.test(value),
    'pasport.uzunlik',
  ),
  /** Faqat mahalliy qism: «905088888». Prefiksni maydon o'zi ko'rsatadi. */
  phone: optionalText.refine(
    (value) => value === '' || /^\d{9}$/u.test(value),
    'telefon.uzunlik',
  ),
  secondPhone: optionalText.refine(
    (value) => value === '' || /^\d{9}$/u.test(value),
    'telefon.uzunlik',
  ),
  address: optionalText.max(500, 'malumotnoma.nom_uzun'),
  speciality: optionalText.max(255, 'malumotnoma.nom_uzun'),
  telegram: optionalText.max(255, 'malumotnoma.nom_uzun'),
}

export const hireSchema = z
  .object({
    // ── 1-bosqich: shaxs ──
    ...personFields,

    // ── 2-bosqich: ish joyi ──
    legalEntityId: z.string().min(1, 'mehnat.yuridik_shaxs_yoq'),
    departmentId: optionalText,
    positionId: optionalText,
    hireDate: z
      .string()
      .trim()
      .min(1, 'mehnat.kirish_sanasi_bosh')
      .regex(ISO_DATE, 'sana.notogri'),
    /** Stavka satr sifatida saqlanadi — «0,5» ham, «0.5» ham kiritiladi. */
    rate: z.string().trim().min(1, 'mehnat.stavka'),
    isPrimary: z.boolean(),

    // ── 3-bosqich: oklad va buyruq ──
    salaryAmount: optionalText.refine(
      (value) => value === '' || /^\d+([.,]\d{1,2})?$/u.test(value),
      'pul.notogri',
    ),
    orderNo: optionalText.max(50, 'malumotnoma.nom_uzun'),
    orderDate: dateText,
  })
  .superRefine((values, ctx) => {
    const rate = toRate(values.rate)
    if (rate === null || rate <= 0 || rate > 2) {
      ctx.addIssue({ code: 'custom', path: ['rate'], message: 'mehnat.stavka' })
    }

    /*
     * Buyruq sanasi ishga kirish sanasidan keyin bo'lishi mumkin (buyruq
     * kechikib rasmiylashtiriladi), lekin ancha OLDIN bo'lishi ham normal.
     * Shu sababli tartib tekshirilmaydi — faqat kelajakdagi sana bloklanadi:
     * bu deyarli har doim yozuv xatosi.
     */
    const today = todayIso()
    if (values.birthDate !== '' && values.birthDate > today) {
      ctx.addIssue({
        code: 'custom',
        path: ['birthDate'],
        message: 'xodim.tugilgan_sana',
      })
    }
  })

export type HireValues = z.infer<typeof hireSchema>

/** Yangi forma uchun boshlang'ich qiymatlar. */
export function emptyHireValues(): HireValues {
  return {
    lastName: '',
    firstName: '',
    middleName: '',
    birthDate: '',
    pinfl: '',
    passport: '',
    phone: '',
    secondPhone: '',
    address: '',
    speciality: '',
    telegram: '',
    legalEntityId: '',
    departmentId: '',
    positionId: '',
    // Ishga kirish sanasi odatda BUGUN — HR buyruqni o'sha kuni kiritadi
    hireDate: todayIso(),
    rate: '1',
    isPrimary: true,
    salaryAmount: '',
    orderNo: '',
    orderDate: '',
  }
}

/** «0,5» va «0.5» — ikkalasi ham qabul qilinadi (klaviatura tartibi har xil). */
export function toRate(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (normalized === '') return null
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : null
}

/** Bo'sh satrni `null` ga aylantiradi — server «kiritilmagan» deb tushunadi. */
export function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/** Pul: «4 500 000» yoki «4500000,50» → 4500000.5. */
export function toAmount(value: string): number | null {
  const normalized = value.replaceAll(/\s/gu, '').replace(',', '.')
  if (normalized === '') return null
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : null
}

/** Pasport doim katta harfda saqlanadi: «ab1234567» → «AB1234567». */
export function toPassport(value: string): string | null {
  const trimmed = value.trim().toUpperCase()
  return trimmed === '' ? null : trimmed
}

/**
 * JSHSHIR ichiga kodlangan tug'ilgan sanani ochadi:
 * «32202917642002» → «1991-02-22».
 *
 * <b>Nega kerak.</b> Server sana va JSHSHIRni JUFTLIK sifatida tekshiradi
 * (`Pinfl.MatchesBirthDate`) va mos kelmasa saqlashni rad etadi. HR esa
 * 14 xonalik raqam ichida qaysi sana yashiringanini ko'z bilan o'qiy
 * olmaydi — ya'ni «mos emas» degan xatoni olib, nimani nimaga
 * moslashtirishni bilmasdi. Shu sababli sana ochib ko'rsatiladi.
 *
 * ⚠️ Bu faqat KO'RSATISH uchun — hech narsani bloklamaydi. Qoida
 * serverda yashaydi; bu yerda uning kirish ma'lumoti ko'rinadigan
 * qilinadi, xolos.
 *
 * Tuzilishi: [0] — jins va asr (1/2 → 1800-lar, 3/4 → 1900-lar,
 * 5/6 → 2000-lar), [1..6] — KKOOYY.
 */
export function pinflBirthDate(pinfl: string): string | null {
  const digits = pinfl.replaceAll(/\D/gu, '')
  if (digits.length !== 14) return null

  const first = Number(digits[0])
  if (first < 1 || first > 6) return null

  const century = first <= 2 ? 1800 : first <= 4 ? 1900 : 2000
  const iso = `${century + Number(digits.slice(5, 7))}-${digits.slice(3, 5)}-${digits.slice(1, 3)}`

  /*
   * Mavjud bo'lmagan sana ham kodlangan bo'lishi mumkin («31.02») —
   * Excel'da aynan shunday yozuvlar uchragan. Bunday holatda hech narsa
   * ko'rsatilmaydi: yolg'on sanani izoh qilib chizgandan ko'ra jim
   * turgan ma'qul, xatoni baribir server aytadi.
   */
  const parsed = new Date(`${iso}T00:00:00Z`)
  if (Number.isNaN(parsed.getTime())) return null

  return parsed.toISOString().slice(0, 10) === iso ? iso : null
}

/** Telefonni serverga to'liq ko'rinishda yuboramiz: «+998905088888». */
export function toFullPhone(value: string): string | null {
  const digits = value.replaceAll(/\D/gu, '')
  return digits === '' ? null : `+998${digits}`
}

/**
 * Serverdagi to'liq raqamdan maydonga tushadigan qismini ajratadi:
 * «+998905088888» → «905088888».
 *
 * Maydon prefiksni («+998») o'zi chizadi. Agar to'liq raqam maydonga
 * qo'yilsa, foydalanuvchi «+998+998…» ni ko'rardi va tuzatishga urinib
 * to'g'ri raqamni buzib qo'yardi.
 */
export function toLocalPhone(value: string | null): string {
  const digits = (value ?? '').replaceAll(/\D/gu, '')
  const local = digits.length === 12 && digits.startsWith('998') ? digits.slice(3) : digits
  return local
}

/* ═══════════════════════════════════════════ xodim kartasini tahrirlash */

/**
 * Uch holatli belgi: ha / yo'q / <b>ma'lum emas</b>.
 *
 * «Ma'lum emas» ni «yo'q» ga aylantirib bo'lmaydi: Excel'da bu kataklar
 * ko'p yozuvda bo'sh qolgan va bo'shlikni «tekshiruvdan o'tmagan» deb
 * yozib qo'yish xodimga tuhmat bo'lardi. Serverda ham shu sababli
 * `bool?` turadi.
 */
export const TRISTATE = ['unknown', 'yes', 'no'] as const
export type Tristate = (typeof TRISTATE)[number]

export function toTristate(value: boolean | null): Tristate {
  if (value === null) return 'unknown'
  return value ? 'yes' : 'no'
}

export function fromTristate(value: Tristate): boolean | null {
  if (value === 'unknown') return null
  return value === 'yes'
}

/**
 * Xodim kartasini tahrirlash.
 *
 * Bosqichga bo'linmaydi — ishga qabuldan farqi shunda. Bu forma odatda
 * BITTA noto'g'ri maydonni tuzatish uchun ochiladi (Excel'dan ko'chirishda
 * 3872 yozuv «tekshirish kerak» deb belgilangan): agar JSHSHIR uchinchi
 * bosqichda yashiringan bo'lsa, HR uni topguncha uch marta bosishi kerak
 * bo'lardi.
 */
export const employeeSchema = z
  .object({
    ...personFields,
    /**
     * Bo'sh satr — «ko'rsatilmagan». Serverga `null` ketadi va domen
     * bunday holatda jinsni O'ZGARTIRMAYDI (`SetGender` faqat qiymat
     * berilganda chaqiriladi) — ya'ni bo'sh tanlov mavjud qiymatni
     * o'chirmaydi.
     */
    gender: z.enum(['', 'male', 'female']),
    /**
     * Sudlanganlik — «ha/yo'q» emas, MATN: Excel'da bu ustunda
     * «судланмаган», «2019 й. 168-модда» kabi turli yozuvlar bor.
     */
    criminalRecordNote: optionalText.max(1000, 'malumotnoma.nom_uzun'),
    hasCriminalRecord: z.enum(TRISTATE),
    /**
     * mehnat.uz profiliga havola.
     *
     * Bu yerda faqat UZUNLIK tekshiriladi, shakl EMAS — va bu ataylab:
     * server sxemasiz kiritilgan matnga `https://` ni O'ZI qo'shadi
     * (`Employee.SetMehnatUzUrl`). Agar mijoz «https:// bilan boshlansin»
     * deb turib olsa, brauzerdan nusxalangan «mehnat.uz/...» rad
     * etilardi — holbuki server uni bemalol qabul qiladi. Haqiqiy xato
     * (masalan `ftp://`) serverdan `xodim.mehnat_havola` bo'lib qaytadi.
     */
    mehnatUzUrl: optionalText.max(500, 'malumotnoma.nom_uzun'),
    /**
     * To'liq ro'yxat yuboriladi, farq emas: server yo'nalishlarni tozalab,
     * kelganini qaytadan biriktiradi. «Qaysi biri qo'shildi» ni hisoblash
     * mijozning ishi emas.
     */
    directionIds: z.array(z.string()),
  })
  .superRefine((values, ctx) => {
    /*
     * Kelajakdagi tug'ilgan sana — deyarli har doim yozuv xatosi, va
     * domen ham uni rad etadi (`ValidateBirthDate`). Qoida shu yerda
     * TAKRORLANADI, chunki tahrirlash formasida sana endi ochiq:
     * bo'lmasa HR «2027» deb yozib, saqlab, faqat serverdan qaytgach
     * bilib olardi. Ishga qabul formasida bu tekshiruv allaqachon bor —
     * ikkalasi bir xil chegarada turishi kerak.
     */
    const today = todayIso()
    if (values.birthDate !== '' && values.birthDate > today) {
      ctx.addIssue({
        code: 'custom',
        path: ['birthDate'],
        message: 'xodim.tugilgan_sana',
      })
    }
  })

export type EmployeeValues = z.infer<typeof employeeSchema>

/**
 * Kartadagi qiymatlarni formaga tushiradi.
 *
 * Telegram AYNAN saqlangan ko'rinishda qo'yiladi (odatda
 * «https://t.me/nom»). Uni «@nom» ga qisqartirish vasvasasi bor, lekin
 * Excel'dan t.me bo'lmagan havolalar ham kelgan — ularni «@» bilan
 * yuborsak, server oldiga «https://t.me/» qo'shib, mavjud havolani
 * buzib qo'yardi.
 */
export function toEmployeeValues(employee: EmployeeDetails): EmployeeValues {
  return {
    lastName: employee.lastName,
    firstName: employee.firstName,
    middleName: employee.middleName ?? '',
    birthDate: employee.birthDate ?? '',
    gender: employee.gender ?? '',
    pinfl: employee.pinfl ?? '',
    passport: employee.passport ?? '',
    phone: toLocalPhone(employee.phone),
    secondPhone: toLocalPhone(employee.secondPhone),
    address: employee.address ?? '',
    speciality: employee.speciality ?? '',
    telegram: employee.telegramUrl ?? '',
    criminalRecordNote: employee.criminalRecordNote ?? '',
    hasCriminalRecord: toTristate(employee.hasCriminalRecord),
    mehnatUzUrl: employee.mehnatUzUrl ?? '',
    directionIds: employee.directionIds,
  }
}

/* ══════════════════════════════════════════════════ bo'shatish va belgilar */

export const terminateSchema = z.object({
  terminationDate: z
    .string()
    .trim()
    .min(1, 'mehnat.ketish_sanasi')
    .regex(ISO_DATE, 'sana.notogri'),
  terminationReasonId: optionalText,
  orderNo: optionalText.max(50, 'malumotnoma.nom_uzun'),
  orderDate: dateText,
  note: optionalText.max(1000, 'malumotnoma.nom_uzun'),
})

export type TerminateValues = z.infer<typeof terminateSchema>

export const flagSchema = z
  .object({
    type: z.enum(['reserve', 'blacklist', 'monitoring', 'capable']),
    startDate: z.string().trim().min(1, 'belgi.sana').regex(ISO_DATE, 'sana.notogri'),
    endDate: dateText,
    reason: optionalText.max(1000, 'malumotnoma.nom_uzun'),
  })
  .superRefine((values, ctx) => {
    /*
     * Qora ro'yxat — jiddiy qaror: u odamni qayta ishga olishdan to'sadi.
     * Excel'da sababsiz yozuvlar bor edi va oradan yil o'tgach hech kim
     * nima uchun ekanini eslay olmasdi. Domen ham shu qoidani qo'yadi
     * (`belgi.sabab_bosh`) — bu yerda faqat serverga bormasdan aytiladi.
     */
    if (values.type === 'blacklist' && values.reason.trim() === '') {
      ctx.addIssue({ code: 'custom', path: ['reason'], message: 'belgi.sabab_bosh' })
    }

    if (values.endDate !== '' && values.endDate < values.startDate) {
      ctx.addIssue({ code: 'custom', path: ['endDate'], message: 'belgi.sana' })
    }
  })

export type FlagValues = z.infer<typeof flagSchema>
