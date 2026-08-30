import { api } from '@/shared/api/http-client'

/** Ruxsat kodi — backend'dagi `Permissions.SettingsManage` bilan bir xil. */
export const SETTINGS_MANAGE = 'settings.manage'

/** Sozlamaning turi: erkin matn yoki butun son. */
export type SettingKind = 'Text' | 'Number'

/**
 * Bitta sozlama — qiymati va CHEGARALARI bilan.
 *
 * Chegaralar (`min`, `max`, `maxLength`) serverdan keladi, mijozda
 * takrorlanmaydi: ular domen qoidasi va ikki joyda yozilsa, biri
 * o'zgarganda ikkinchisi jimgina eskirardi. Forma ularni faqat
 * KO'RSATADI va oldindan tekshiradi — yakuniy so'z serverda.
 */
export interface SystemSetting {
  key: string
  value: string
  kind: SettingKind
  defaultValue: string
  min: number | null
  max: number | null
  maxLength: number | null
}

export interface SettingUpdate {
  key: string
  value: string
}

export async function fetchSettings(signal?: AbortSignal): Promise<SystemSetting[]> {
  const { data } = await api.get<unknown>('/api/settings', { signal })
  return Array.isArray(data) ? (data as SystemSetting[]) : []
}

export async function saveSettings(items: SettingUpdate[]): Promise<void> {
  await api.put('/api/settings', { items })
}
