import type { LocaleCode } from '@/shared/i18n/locales'
import type { BadgeTone } from '@/shared/ui'

/* ═════════════════════════════════════════════════════════════ RUXSATLAR */

/**
 * Ruxsat kodi — backend'dagi `Bonvi.Domain.Identity.Permissions` bilan AYNAN
 * bir xil (`safety-presentation.ts` dagi xuddi shu naqsh).
 *
 * `employees.view` EMAS va bu ataylab: server ham shu chegarani chizgan
 * (`ReportsEndpoints.cs` boshidagi izoh). Bosh sahifa — «hozirgi holat»
 * ekrani va u xodimlar bilan ishlaydigan har kimga ochiq; kadrlar oqimi esa
 * tahliliy hisobot. Ikkalasini bitta ruxsatga qo'shish hisobot huquqi yo'q
 * foydalanuvchiga grafikni ochib qo'yardi.
 */
export const REPORTS_VIEW = 'reports.view'

/* ═══════════════════════════════════════════════════ KETISH TASHABBUSI */

export interface VoluntaryLook {
  tone: BadgeTone
  /** `reports.*` tarjima kaliti. */
  labelKey: string
}

/**
 * «Kim tashabbus qildi» yorlig'ining ko'rinishi.
 *
 * Uch holat — uchta rang, lekin QIZIL YO'Q va bu ataylab: tashkilot
 * tashabbusi bilan ketish xato emas, u shunchaki BOSHQA TABIATLI hodisa.
 * Qizil bu loyihada «buzilgan, tuzatish kerak» degan ma'noda ishlatiladi
 * (`Alert tone="danger"`, o'chirish tugmasi) — uni bu yerga olib kirish
 * intizomiy bo'shatishni tizim xatosi bilan bir qatorga qo'yardi.
 *
 * `warning` esa aynan kerakli ma'noni beradi: «bu qatorga e'tibor ber».
 * Grafikdagi qizil ustun (ketganlar) bilan ham chalkashmaydi — u yerda
 * qizil «kamayish» degani, bu yerda esa sabab haqida gap ketyapti.
 */
export function voluntaryLook(isVoluntary: boolean | null): VoluntaryLook {
  if (isVoluntary === null) {
    return { tone: 'neutral', labelKey: 'reports.voluntaryUnknown' }
  }

  return isVoluntary
    ? { tone: 'info', labelKey: 'reports.voluntary' }
    : { tone: 'warning', labelKey: 'reports.involuntary' }
}

/* ═══════════════════════════════════════════════════════ OY YORLIG'I */

/**
 * Grafikning gorizontal o'qidagi qisqa oy yorlig'i: «08.26».
 *
 * NEGA oy NOMI emas («Avgust», «Авг»): o'n ikkita nomni to'rt tilga
 * tarjima qilish qirq sakkizta yangi kalit degani, o'q esa baribir
 * qisqartirishga majbur bo'lardi. Raqamli shakl esa hamma tilda bir xil
 * kenglikda turadi — 24 oylik grafikda yorliqlar bir-biriga tegmasligi
 * aynan shunga bog'liq.
 *
 * NEGA to'liq yil emas («08.2026»): 24 ta yorliq × 7 belgi o'qni to'ldirib
 * yuboradi. Ikki xonali yil esa yetarli — hisobot eng ko'pi ikki yilni
 * qamraydi, ya'ni asr chegarasi masalasi tug'ilmaydi.
 *
 * Til bo'yicha tartib `formatDate` bilan BIR XIL qoidada: inglizcha
 * sanalar bu loyihada kattadan kichikka yoziladi (`2026-08-26`), shu
 * sababli o'q yorlig'i ham shu tartibda qoladi — bitta ekranda ikki xil
 * sana tartibi bo'lmaydi.
 *
 * ⚠️ `new Date` ATAYLAB ishlatilmagan: «2026-08-01» ni brauzer UTC
 * yarim tunida deb o'qiydi va manfiy siljishli mintaqada u 31-iyulga
 * aylanadi — ya'ni yorliq bir oy orqaga sirg'anardi. Satrni kesish esa
 * mintaqadan butunlay xoli.
 */
export function monthTick(monthStart: string, locale: LocaleCode): string {
  const [year, month] = monthStart.split('-')

  if (year === undefined || month === undefined) return monthStart

  const shortYear = year.slice(-2)

  return locale === 'en' ? `${shortYear}-${month}` : `${month}.${shortYear}`
}

/* ══════════════════════════════════════════════════════════════ FOIZ */

/**
 * Foizni matnga aylantiradi: «4,2%».
 *
 * Bitta kasr xonasi — ikkita chegara orasidagi kelishuv. Butun songa
 * yaxlitlash kichik korxonada ma'lumotni yo'qotadi (35 kishilik shtatda
 * bitta odam ≈ 2,9% — «3%» ham, «0%» ham bo'lib qolishi mumkin edi), ikki
 * xona esa aniqlik illyuziyasini beradi: manbadagi sonlar butun odamlar,
 * ya'ni yuzdan bir foiz hech narsani anglatmaydi.
 *
 * `num` — `useLocale().num`: rus va o'zbek tilida kasr VERGUL bilan
 * ajratiladi, inglizchada nuqta bilan. Shu sababli yaxlitlash bu yerda,
 * formatlash esa tilga tegishli joyda bo'ladi.
 */
export function percentText(value: number, num: (v: number) => string): string {
  return `${num(Math.round(value * 10) / 10)}%`
}
