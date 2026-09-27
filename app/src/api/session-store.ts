import { readStorage, removeStorage, writeStorage } from '@/lib/storage'

import type { LoginResult } from './types'

/**
 * Sessiya holati — tokenlar va «parolni almashtirish shart» bayrog'i.
 *
 * NEGA React konteksti emas, modul: tokenni HTTP qatlami ham o'qiydi va
 * YOZADI (yangilashda, sessiya tugaganda, server «avval parolni
 * almashtiring» deganda). Ikki manba bo'lsa, ular ajralib qolardi —
 * masalan yangilangan token React holatiga yetib bormay, keyingi so'rov
 * eski (bekor qilingan) token bilan ketib, hamma sessiyani yopib
 * yuborardi. `SessionProvider` shu yerga obuna bo'ladi.
 *
 * ⚠️ Token yo'qolishi ODATIY hol: Safari yozuvlarni 7 kun faolsizlikdan
 * keyin o'chirib yuborishi mumkin (UX §G.4).
 *
 * ⚠️ Bir martalik parol bu yerda YO'Q va bo'lmaydi: u faqat xotirada,
 * `SessionProvider` ichida yashaydi (hech qachon `localStorage` da emas).
 */
const TOKEN_KEY = 'bonvi.ilova.token'
const REFRESH_KEY = 'bonvi.ilova.refresh'
const EXPIRES_KEY = 'bonvi.ilova.token-muddat'
const MUST_CHANGE_KEY = 'bonvi.ilova.parol-almashsin'

export interface SessionState {
  accessToken: string | null
  refreshToken: string | null
  /** Access token muddati (ms); noma'lum bo'lsa `null`. */
  accessExpiresAt: number | null
  mustChangePassword: boolean
  /**
   * Sessiyani SERVER yopdi (yangilash rad etildi) — kirish ekrani buni
   * bir qator bilan aytadi. Safari tozalagan token esa bu emas.
   */
  expired: boolean
}

function load(): SessionState {
  const expires = Number(readStorage(EXPIRES_KEY))

  return {
    accessToken: readStorage(TOKEN_KEY),
    refreshToken: readStorage(REFRESH_KEY),
    accessExpiresAt: Number.isFinite(expires) && expires > 0 ? expires : null,
    mustChangePassword: readStorage(MUST_CHANGE_KEY) === '1',
    expired: false,
  }
}

let state: SessionState = load()
const listeners: Array<() => void> = []

function emit(next: SessionState): void {
  state = next
  listeners.slice().forEach((listener) => listener())
}

export const sessionStore = {
  get(): SessionState {
    return state
  },

  subscribe(listener: () => void): () => void {
    listeners.push(listener)
    return () => {
      const index = listeners.indexOf(listener)
      if (index >= 0) listeners.splice(index, 1)
    }
  },

  /** Yangi juftlik — kirish, yangilash yoki parol almashtirishdan keyin. */
  setTokens(pair: LoginResult): void {
    const expires = Date.parse(pair.expiresAt)

    writeStorage(TOKEN_KEY, pair.accessToken)
    writeStorage(REFRESH_KEY, pair.refreshToken)
    writeStorage(MUST_CHANGE_KEY, pair.mustChangePassword ? '1' : '0')
    if (Number.isFinite(expires)) writeStorage(EXPIRES_KEY, String(expires))
    else removeStorage(EXPIRES_KEY)

    emit({
      accessToken: pair.accessToken,
      refreshToken: pair.refreshToken,
      accessExpiresAt: Number.isFinite(expires) ? expires : null,
      mustChangePassword: pair.mustChangePassword,
      expired: false,
    })
  },

  /** Server «avval bir martalik parolni almashtiring» dedi (403). */
  requirePasswordChange(): void {
    if (state.mustChangePassword) return
    writeStorage(MUST_CHANGE_KEY, '1')
    emit({ ...state, mustChangePassword: true })
  },

  /** Hisobdan chiqish yoki server sessiyani yopgani. */
  clear(expired = false): void {
    removeStorage(TOKEN_KEY)
    removeStorage(REFRESH_KEY)
    removeStorage(EXPIRES_KEY)
    removeStorage(MUST_CHANGE_KEY)

    emit({
      accessToken: null,
      refreshToken: null,
      accessExpiresAt: null,
      mustChangePassword: false,
      expired,
    })
  },
}
