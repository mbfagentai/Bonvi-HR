import { useCallback, useEffect, useState } from 'react'

import { isInAppBrowser, isIos, isStandalone } from '@/lib/device'
import { readStorage, writeStorage } from '@/lib/storage'

/**
 * O'rnatish taklifi (UX §G.4).
 *
 * Qoidalar taklifni NAG'ga aylantirmaslik uchun: birinchi sessiyada
 * umuman ko'rinmaydi, yopilsa 7 kun ko'rinmaydi, o'rnatilgandan keyin
 * boshqa hech qachon. Doimiy kirish yo'li `Sozlamalar` da qoladi —
 * bir marta yopgan odam ham keyin o'rnata oladi.
 *
 * ⚠️ `beforeinstallprompt` faqat Chromium'da bor. iOS'da API umuman
 * yo'q, shu sababli u yerda qo'lda qo'shish yo'li MATN bilan
 * tushuntiriladi.
 */
const SESSION_KEY = 'bonvi.ilova.sessiyalar'
const DISMISSED_KEY = 'bonvi.ilova.ornatish-yopildi'
const INSTALLED_KEY = 'bonvi.ilova.ornatildi'

const WEEK_MS = 7 * 24 * 60 * 60 * 1000

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

/** Sessiyalar sonini oshiradi — ilova ochilganda bir marta. */
export function countSession(): void {
  const current = Number(readStorage(SESSION_KEY) ?? '0')
  writeStorage(SESSION_KEY, String(current + 1))
}

export type InstallVariant = 'android' | 'ios' | null

export interface InstallApi {
  /** Karta ko'rsatilsinmi va qaysi ko'rinishda. */
  variant: InstallVariant
  /** `Sozlamalar` uchun: taklif tayyor turibdimi. */
  canPrompt: boolean
  prompt: () => void
  dismiss: () => void
}

export function useInstall(): InstallApi {
  const [deferred, setDeferred] = useState<BeforeInstallPromptEvent | null>(null)
  const [dismissedAt, setDismissedAt] = useState<number>(() =>
    Number(readStorage(DISMISSED_KEY) ?? '0'),
  )
  const [installed, setInstalled] = useState<boolean>(() => readStorage(INSTALLED_KEY) === '1')

  useEffect(() => {
    const onPrompt = (event: Event): void => {
      // Brauzerning o'z mini-paneli to'xtatiladi: taklif BIZNING
      // kartamizda, o'z vaqtida chiqadi.
      event.preventDefault()
      setDeferred(event as BeforeInstallPromptEvent)
    }

    const onInstalled = (): void => {
      writeStorage(INSTALLED_KEY, '1')
      setInstalled(true)
    }

    window.addEventListener('beforeinstallprompt', onPrompt)
    window.addEventListener('appinstalled', onInstalled)

    return () => {
      window.removeEventListener('beforeinstallprompt', onPrompt)
      window.removeEventListener('appinstalled', onInstalled)
    }
  }, [])

  const dismiss = useCallback(() => {
    const now = Date.now()
    writeStorage(DISMISSED_KEY, String(now))
    setDismissedAt(now)
  }, [])

  const prompt = useCallback(() => {
    if (!deferred) return
    void deferred.prompt()
    setDeferred(null)
  }, [deferred])

  const sessions = Number(readStorage(SESSION_KEY) ?? '0')
  const recentlyDismissed = dismissedAt > 0 && Date.now() - dismissedAt < WEEK_MS

  let variant: InstallVariant = null

  if (!installed && !isStandalone() && !isInAppBrowser() && sessions > 1 && !recentlyDismissed) {
    if (deferred) variant = 'android'
    else if (isIos()) variant = 'ios'
  }

  return { variant, canPrompt: deferred !== null, prompt, dismiss }
}
