import { createContext, useContext, useMemo, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useRegisterSW } from 'virtual:pwa-register/react'

import { Button } from '@/ui/button'
import { useCheckInBusy } from './checkin-busy'

/**
 * Yangi versiya (UX §G.5).
 *
 * ⚠️ `prompt` rejimi, `autoUpdate` EMAS: yangilanish faqat xodim
 * o'zi bosganda qo'llanadi. Va u belgilash uchib turgan paytda
 * umuman taklif qilinmaydi.
 */
interface UpdateApi {
  /** Yangi worker kutyapti. */
  needRefresh: boolean
  /** Yangilash va sahifani qayta yuklash. */
  update: () => void
}

const UpdateContext = createContext<UpdateApi>({ needRefresh: false, update: () => {} })

export function UpdateProvider({ children }: { children: ReactNode }) {
  const {
    needRefresh: [needRefresh],
    updateServiceWorker,
  } = useRegisterSW()

  const api = useMemo<UpdateApi>(
    () => ({
      needRefresh,
      update: () => {
        void updateServiceWorker(true)
      },
    }),
    [needRefresh, updateServiceWorker],
  )

  return <UpdateContext.Provider value={api}>{children}</UpdateContext.Provider>
}

export function useUpdate(): UpdateApi {
  return useContext(UpdateContext)
}

/** Pastdagi «Yangi versiya tayyor» xabari. */
export function UpdateToast() {
  const { t } = useTranslation()
  const { needRefresh, update } = useUpdate()
  const checkInBusy = useCheckInBusy()

  if (!needRefresh || checkInBusy) return null

  return (
    <div className="fixed bottom-0 left-0 right-0 z-30 flex justify-center pb-[calc(88px+env(safe-area-inset-bottom,0px))]">
      <div className="mx-4 flex w-full max-w-app items-center rounded-control bg-bg-elevated px-4 py-3 shadow-[0_6px_24px_rgba(0,0,0,0.16)]">
        <span className="min-w-0 flex-1 text-subhead text-fg">{t('update.title')}</span>
        <Button variant="plain" size="compact" onClick={update}>
          {t('update.action')}
        </Button>
      </div>
    </div>
  )
}
