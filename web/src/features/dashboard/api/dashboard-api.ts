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
