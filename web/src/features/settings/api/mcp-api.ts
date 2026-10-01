/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`api-keys-api.ts` boshidagi
 * izoh bilan bir xil naqsh).
 *
 * Tiplar TAXMIN bilan emas, haqiqiy shartnomadan olingan:
 *   api/src/Bonvi.Api/Mcp/McpAdminEndpoints.cs
 *   (`McpStatusDto`, `McpConnectionDto`, `McpAuditDto`)
 * `./bonvi gen` dan keyin ular `@/shared/api/generated/model` da ham bor —
 * lekin generatsiya qilingan kod git'ga tushmaydi, shu sababli bu bo'lim
 * ham qolgan bo'limlar kabi o'z tipini saqlaydi.
 *
 * <b>Marshrutlar `/api/settings` ICHIDA emas</b> — sabab API kalitlaridagi
 * bilan bir xil: o'chirgich bitta sozlama bo'lsa ham, ulanishlar va jurnal
 * YOZUVLAR ro'yxati. Hammasi `settings.manage` bilan himoyalangan
 * (`docs/SPEC-mcp.md` §8).
 */
import { api } from '@/shared/api/http-client'

/**
 * Serverning xulosasi (`McpRuntime`):
 *   · `NotConfigured` — serverda parol yoki ochiq manzil berilmagan;
 *   · `Pending` — ishga tushish tekshiruvi hali tugamagan;
 *   · `Faulty` — o'qish rolini tayyorlash yoki o'z-o'zini tekshirish yiqildi;
 *   · `Off` — hammasi tayyor, lekin egasi o'chirib qo'ygan (SUKUT);
 *   · `On` — ishlayapti.
 */
export type McpState = 'NotConfigured' | 'Pending' | 'Faulty' | 'Off' | 'On'

export interface McpStatus {
  state: McpState
  /** Nega ishlamayapti — o'zbekcha, sirsiz. `On`/`Off` da `null`. */
  reason: string | null
  /**
   * Egasining o'chirgichi — SAQLANGAN qiymat.
   *
   * ⚠️ Holatdan MUSTAQIL: server sozlanmagan yoki nosoz bo'lsa ham u
   * saqlanadi va shu yerda qaytadi. Ekran uni ko'rsatib turadi (o'chirgich
   * nofaol bo'lsa ham) — server tuzatilgach, egasining tanlovi o'zi kuchga
   * kiradi.
   */
  enabled: boolean
  /** AI ilovasiga kiritiladigan manzil (`…/mcp`). */
  serverUrl: string
}

/** `expired` — ulanish 90 kunlik umrini o'tagan (serverning `GrantLifetime`). */
export type McpConnectionStatus = 'active' | 'revoked' | 'expired'

/**
 * Bitta ulanish — «kim, qaysi AI ilovasi orqali».
 *
 * Uzilgan ulanish O'CHIRILMAYDI — ro'yxatda kulrang bo'lib qoladi (API
 * kalitlaridagi bilan bir xil sabab, backend izohida).
 */
export interface McpConnection {
  id: string
  userId: string
  /** Foydalanuvchi o'chirilgan bo'lsa server «O'chirilgan foydalanuvchi» qaytaradi. */
  userName: string
  /** AI ilovasining o'zi aytgan nomi («Claude», «ChatGPT»). */
  clientName: string
  connectedAt: string
  /** Aniqligi — DAQIQA. `null` — ulangan, lekin hali birorta so'rov bo'lmagan. */
  lastUsedAt: string | null
  revokedAt: string | null
  status: McpConnectionStatus
}

/** `denied` — foydalanuvchida o'sha bo'lim ruxsati yo'q (vosita rad etdi). */
export type McpOutcome = 'ok' | 'denied' | 'error'

/**
 * Jurnal qatori.
 *
 * ⚠️ Argumentlar (nima so'ralgani) bu javobda ATAYLAB YO'Q — server ularni
 * faqat bazada saqlaydi (backend izohi).
 */
export interface McpAuditEntry {
  id: number
  at: string
  userName: string
  clientName: string
  /** Vosita nomi — `MCP_TOOL_NAMES` dan biri yoki (yangi vosita bo'lsa) noma'lum nom. */
  tool: string
  outcome: McpOutcome
  /** Qaytgan yozuvlar soni. `null` — vosita sanamaydi yoki rad etildi. */
  rowCount: number | null
  durationMs: number
}

/**
 * Serverdagi vositalar (`docs/SPEC-mcp.md` §7) — jurnaldagi nomni odam
 * o'qiydigan yorliqqa aylantirish uchun.
 *
 * <b>Nega ro'yxat KODDA.</b> Jurnalda vosita nomi xom holida keladi
 * (`search_employees`) va yorliqni frontend tanlaydi — `role-keys.ts`
 * dagi bilan bir xil fikr. `scripts/check-locales.mjs` aynan shu massivni
 * o'qib, har biriga to'rt tilda `settings.mcp.tool.*` matni borligini
 * tekshiradi. Ro'yxatda yo'q nom (backend yangi vosita qo'shgan) ekranni
 * buzmaydi — u xom holida chiqadi.
 *
 * ⚠️ `export const NOM = [...] as const` shaklida qolishi kerak —
 * tekshiruv skripti uni oddiy muntazam ifoda bilan o'qiydi.
 */
export const MCP_TOOL_NAMES = [
  'overview',
  'search_employees',
  'get_employee',
  'upcoming',
  'missing_documents',
  'list_flags',
  'org_structure',
  'employment_events',
  'turnover',
  'probations',
  'incentives',
  'exit_interviews',
  'incidents',
  'vacancies',
  'search_candidates',
  'get_candidate',
  'recruiting_funnel',
  'attendance_day',
  'attendance_summary',
  'absence_requests',
] as const

/** Jurnaldan nechta qator so'raladi. Server chegarasi 1…200, sukut 100. */
export const MCP_AUDIT_TAKE = 100

export async function fetchMcpStatus(signal?: AbortSignal): Promise<McpStatus> {
  const { data } = await api.get<McpStatus>('/api/mcp/status', { signal })
  return data
}

/**
 * Qayta tekshirish: server o'qish rolini qayta tayyorlaydi va o'z-o'zini
 * tekshirishni qayta o'tkazadi. Javob — YANGI holat.
 */
export async function checkMcp(): Promise<McpStatus> {
  const { data } = await api.post<McpStatus>('/api/mcp/check')
  return data
}

/**
 * O'chirgich. Javob — yangi holat.
 *
 * Server sozlanmagan bo'lsa ham qiymat SAQLANADI (holat `NotConfigured`
 * bo'lib qoladi) — ekran baribir o'chirgichni nofaol qiladi, chunki bu
 * paytda tanlovni o'zgartirishning ko'rinadigan natijasi yo'q.
 */
export async function setMcpEnabled(enabled: boolean): Promise<McpStatus> {
  const { data } = await api.put<McpStatus>('/api/mcp/enabled', { enabled })
  return data
}

export async function fetchMcpConnections(signal?: AbortSignal): Promise<McpConnection[]> {
  const { data } = await api.get<unknown>('/api/mcp/connections', { signal })
  return Array.isArray(data) ? (data as McpConnection[]) : []
}

/**
 * Uzish. Ulanish DARHOL ishlamay qoladi (token har chaqiruvda bazadan
 * tekshiriladi). Amal IDEMPOTENT — allaqachon uzilganida ham 204.
 *
 * Tanasi yo'q: server marshrutdan faqat `id` ni oladi.
 */
export async function revokeMcpConnection(id: string): Promise<void> {
  await api.post(`/api/mcp/connections/${id}/revoke`)
}

export async function fetchMcpAudit(take: number, signal?: AbortSignal): Promise<McpAuditEntry[]> {
  const { data } = await api.get<unknown>('/api/mcp/audit', { params: { take }, signal })
  return Array.isArray(data) ? (data as McpAuditEntry[]) : []
}
