/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN. `./bonvi gen` bilan almashtiriladi.
 *
 * Tiplar taxmin emas: ular `Bonvi.Application/Dashboard/Dtos/DashboardStatsDto.cs`
 * dan bir-bir ko'chirilgan (`GET /api/dashboard/stats`). Xodimlar va
 * ma'lumotnomalar bo'limlari ham shu uslubda yozilgan — sabab bir xil:
 * Orval hozircha butun loyiha uchun ishga tushirilmagan.
 *
 * Backend shartnomasi o'zgargach:
 *   1. `./bonvi gen` — `@/shared/api/generated/dashboard/*` hosil bo'ladi
 *   2. Shu fayl O'CHIRILADI, `dashboard-queries.ts` generatsiya qilingan
 *      hook'ga o'tkaziladi
 *
 * Bu yerda xodimlar klientidagi kabi «himoya qatlami» (`unknown` ni qo'lda
 * o'qish) ATAYLAB yo'q: u yerda endpoint hali mavjud emas edi va javob
 * shakli noaniq edi. Bu endpoint esa mavjud, uning shakli testlar bilan
 * qadalgan — ortiqcha o'qish qatlami faqat shovqin bo'lardi.
 */
import { api } from '@/shared/api/http-client'

/**
 * Davr almashtirgichining qiymatlari.
 *
 * Server AYNAN shu to'rtta satrni kutadi (registrga sezgir emas).
 * Bular kalendar oralig'i emas, **bugundan orqaga siljuvchi oyna**:
 * «1 oy» — oy boshidan emas, bugundan bir oy orqaga.
 */
export const DASHBOARD_PERIODS = ['day', 'week', 'days15', 'month'] as const
export type DashboardPeriod = (typeof DASHBOARD_PERIODS)[number]

/**
 * Rekruting sonlari — bosh sahifadagi ikkita karta.
 *
 * ⚠️ Bu blok butunlay YO'Q bo'lishi mumkin (`DashboardStats.recruiting`
 * `null`): u faqat `recruiting.view` ruxsati borlarga qaytadi. Bosh sahifa
 * esa `employees.view` bilan ochiladi va bu ikki ruxsat bir odamda birga
 * bo'lishi shart emas.
 *
 * Nol bilan `null` ni ARALASHTIRMASLIK kerak: nol — haqiqiy javob («ochiq
 * vakansiya yo'q»), `null` esa javobning yo'qligi. Birinchisida karta «0»
 * ko'rsatadi, ikkinchisida karta umuman chizilmaydi. Xuddi oklad
 * maskalanishidagi kabi (`salary.view`).
 */
export interface DashboardRecruiting {
  /** Nomzod qabul qiladigan (ochiq) vakansiyalar soni. */
  openVacancies: number
  /**
   * Shu vakansiyalarda jami nechta O'RIN kerakligi.
   *
   * `openVacancies` bilan bir xil emas va bu ataylab: bitta e'londa bir
   * nechta o'rin bo'lishi mumkin (zavod sexiga 20 kishi — bitta e'lon).
   * «5 vakansiya» — rekruterning ish hajmi, «12 o'rin» — kadr ehtiyoji.
   */
  openings: number
  /** Voronkadagi FAOL arizalar — joriy holat, davrga bog'liq emas. */
  activeCandidates: number
  /** Tanlangan davrda kelgan yangi arizalar — natijasidan qat'i nazar. */
  newCandidates: number
}

/** Ma'lumotnomalardagi FAOL yozuvlar soni. */
export interface DashboardCatalogSizes {
  departments: number
  positions: number
  directions: number
  legalEntities: number
}

/**
 * Bosh sahifaning barcha raqamlari.
 *
 * Sonlar ODAM bo'yicha sanaladi, shartnoma bo'yicha emas: ikki firmada
 * ishlaydigan o'rindosh bir marta hisoblanadi.
 */
export interface DashboardStats {
  /** So'ralgan davr — server tomonidan tasdiqlanadi («Day», «Month» …). */
  period: string
  /** `DateOnly` → «2026-07-25». Davrning boshi, shu kun ham kiradi. */
  from: string
  /** Davrning oxiri (odatda bugun), shu kun ham kiradi. */
  to: string
  /** Oldingi — shu uzunlikdagi — davrning boshi. */
  previousFrom: string
  /** Oldingi davrning oxiri. */
  previousTo: string

  /** Hozir ishlayotgan (sinovi yopilgan) xodimlar. */
  activeEmployees: number
  /** Sinov muddatidagi xodimlar. */
  onProbation: number
  /** Endi hech qayerda ishlamaydigan xodimlar — arxiv. */
  formerEmployees: number

  /** Tanlangan davrda ishga qabul qilinganlar. */
  hired: number
  /** Tanlangan davrda ishdan ketganlar. */
  left: number
  /** Oldingi shunday davrda ishga qabul qilinganlar — taqqoslash uchun. */
  hiredPrevious: number
  /** Oldingi shunday davrda ishdan ketganlar — taqqoslash uchun. */
  leftPrevious: number

  /** Bugun tug'ilgan kuni bo'lgan ishlayotgan xodimlar. */
  birthdaysToday: number

  catalog: DashboardCatalogSizes

  /**
   * Rekruting sonlari — ruxsat bo'lmasa `null`.
   *
   * Tip ATAYLAB `| null`, ixtiyoriy maydon (`?`) emas: server maydonni
   * doim yuboradi, faqat qiymati `null` bo'ladi. `?` yozilsa «serverda
   * bunday maydon yo'q» degan boshqa ma'no chiqardi va shartnoma
   * o'zgarganini bu yerdan o'qib bo'lmasdi.
   */
  recruiting: DashboardRecruiting | null
}

export async function fetchDashboardStats(
  period: DashboardPeriod,
  signal?: AbortSignal,
): Promise<DashboardStats> {
  const { data } = await api.get<DashboardStats>('/api/dashboard/stats', {
    params: { period },
    signal,
  })

  return data
}

/**
 * Taqsimotdagi bitta bo'lak.
 *
 * `id === null` — «Belgilanmagan»: bo'limi (yoki yo'nalishi) ko'rsatilmagan
 * xodimlar. Uning NOMI ham serverdan keladi va klientda yasalmaydi: aks
 * holda bir xil tushuncha ikki joyda — C# da va bu yerda — nomlanardi va
 * ular vaqt o'tib ajralib ketardi.
 */
export interface DashboardBreakdownItem {
  id: string | null
  name: string
  count: number
}

/**
 * Faol xodimlarning bo'lim va yo'nalish kesimi.
 *
 * Ikkala ro'yxat ham SERVERDA ko'pdan kamga tartiblangan — klientda qayta
 * saralash kerak emas va qilinmaydi ham: tartib shartnomaning bir qismi.
 *
 * ⚠️ Ikki ro'yxatning ARIFMETIKASI BOSHQA va bu ekranda ko'rinadi:
 *   • `departments` yig'indisi `DashboardStats.activeEmployees` ga TENG —
 *     har bir xodim bitta bo'limga tegishli va u bo'lim daraxtining
 *     yuqori darajasiga (departamentga) yig'iladi;
 *   • `directions` yig'indisi esa undan OSHISHI mumkin — bitta xodim bir
 *     nechta yo'nalishga tegishli bo'la oladi.
 * Ya'ni «umumiy xodimning necha foizi» degan hisob faqat BIRINCHI ro'yxat
 * uchun to'g'ri. Kartada shu farq hisobga olingan.
 */
export interface DashboardBreakdown {
  departments: DashboardBreakdownItem[]
  directions: DashboardBreakdownItem[]
}

/**
 * Taqsimot — parametrsiz.
 *
 * `DashboardPeriod` ATAYLAB yo'q: bu «bugungi holat» kesimi, oraliqqa
 * bog'liq emas. Aynan shu sababli u `/stats` dan alohida endpoint —
 * davr almashtirgichi bosilganda qayta so'ralmasligi kerak
 * (kesh kaliti `dashboard-queries.ts` da).
 */
export async function fetchDashboardBreakdown(
  signal?: AbortSignal,
): Promise<DashboardBreakdown> {
  const { data } = await api.get<DashboardBreakdown>('/api/dashboard/breakdown', {
    signal,
  })

  return data
}
