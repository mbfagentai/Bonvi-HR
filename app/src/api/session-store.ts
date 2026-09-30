import { readStorage, removeStorage, writeStorage } from '@/lib/storage'

import type { LoginResult } from './types'

/**
 * Sessiya holati — tokenlar.
 *
 * NEGA React konteksti emas, modul: tokenni HTTP qatlami ham o'qiydi va
 * YOZADI (yangilashda, sessiya tugaganda). Ikki manba bo'lsa, ular ajralib qolardi —
 * masalan yangilangan token React holatiga yetib bormay, keyingi so'rov
 * eski (bekor qilingan) token bilan ketib, hamma sessiyani yopib
 * yuborardi. `SessionProvider` shu yerga obuna bo'ladi.
 *
 * ⚠️ Token yo'qolishi ODATIY hol: Safari yozuvlarni 7 kun faolsizlikdan
 * keyin o'chirib yuborishi mumkin (UX §G.4).
 *
 * Parol bu yerda YO'Q va bo'lmaydi: uni HR beradi, xodim almashtirmaydi
 * (2026-09-27 dan «parolni almashtirish shart» bayrog'i ham yo'q).
 */
const TOKEN_KEY = 'bonvi.ilova.token'
const REFRESH_KEY = 'bonvi.ilova.refresh'
const EXPIRES_KEY = 'bonvi.ilova.token-muddat'

/**
 * Eski ilova yozib qoldirgan «parolni almashtirish shart» belgisi — endi
 * o'qilmaydi, faqat tozalanadi (telefonlarda qolib ketmasin).
 */
const LEGACY_MUST_CHANGE_KEY = 'bonvi.ilova.parol-almashsin'

export interface SessionState {
  accessToken: string | null
  refreshToken: string | null
  /** Access token muddati (ms); noma'lum bo'lsa `null`. */
  accessExpiresAt: number | null
  /**
   * Sessiyani SERVER yopdi (yangilash rad etildi) — kirish ekrani buni
   * bir qator bilan aytadi. Safari tozalagan token esa bu emas.
   */
  expired: boolean
}

function load(): SessionState {
  const expires = Number(readStorage(EXPIRES_KEY))
  removeStorage(LEGACY_MUST_CHANGE_KEY)

  return {
    accessToken: readStorage(TOKEN_KEY),
    refreshToken: readStorage(REFRESH_KEY),
    accessExpiresAt: Number.isFinite(expires) && expires > 0 ? expires : null,
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

  /** Yangi juftlik — kirish yoki yangilashdan keyin. */
  setTokens(pair: LoginResult): void {
    const expires = Date.parse(pair.expiresAt)

    writeStorage(TOKEN_KEY, pair.accessToken)
    writeStorage(REFRESH_KEY, pair.refreshToken)
    if (Number.isFinite(expires)) writeStorage(EXPIRES_KEY, String(expires))
    else removeStorage(EXPIRES_KEY)

    emit({
      accessToken: pair.accessToken,
      refreshToken: pair.refreshToken,
      accessExpiresAt: Number.isFinite(expires) ? expires : null,
      expired: false,
    })
  },

  /** Hisobdan chiqish yoki server sessiyani yopgani. */
  clear(expired = false): void {
    removeStorage(TOKEN_KEY)
    removeStorage(REFRESH_KEY)
    removeStorage(EXPIRES_KEY)

    emit({
      accessToken: null,
      refreshToken: null,
      accessExpiresAt: null,
      expired,
    })
  },
}
