import clsx from 'clsx'
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'

/**
 * Toast (UX §E.6): pastda, tab panelidan yuqorida, 4 soniya, bir
 * vaqtda bittasi.
 *
 * ⚠️ Toast HECH QACHON muhim ma'lumotning yagona nusxasi bo'lmaydi.
 * Belgilash natijasi — toast EMAS: u o'zini tug'dirgan kartada
 * qoladi (UX §C.5), chunki xodimning «belgi bor» degan yagona isboti
 * o'sha jumla.
 */
type ToastTone = 'success' | 'error' | 'info'

interface ToastMessage {
  id: number
  text: string
  tone: ToastTone
}

interface ToastApi {
  show: (text: string, tone?: ToastTone) => void
}

const ToastContext = createContext<ToastApi | null>(null)

const TONE_CLASS: Record<ToastTone, string> = {
  success: 'bg-tint-success',
  error: 'bg-tint-absent',
  info: 'bg-bg-elevated',
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<ToastMessage | null>(null)
  const counter = useRef(0)

  const show = useCallback((text: string, tone: ToastTone = 'info') => {
    counter.current += 1
    setToast({ id: counter.current, text, tone })
  }, [])

  useEffect(() => {
    if (!toast) return undefined

    const timer = window.setTimeout(() => setToast(null), 4000)
    return () => window.clearTimeout(timer)
  }, [toast])

  const api = useMemo<ToastApi>(() => ({ show }), [show])

  return (
    <ToastContext.Provider value={api}>
      {children}

      {toast ? (
        <div className="pointer-events-none fixed bottom-0 left-0 right-0 z-30 flex justify-center pb-[calc(88px+env(safe-area-inset-bottom,0px))]">
          <button
            type="button"
            onClick={() => setToast(null)}
            className={clsx(
              'pointer-events-auto mx-4 max-w-app rounded-control px-4 py-3 text-subhead text-fg shadow-[0_6px_24px_rgba(0,0,0,0.16)]',
              TONE_CLASS[toast.tone],
            )}
            role="status"
            aria-live="polite"
          >
            <span className="server-text">{toast.text}</span>
          </button>
        </div>
      ) : null}
    </ToastContext.Provider>
  )
}

export function useToast(): ToastApi {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast: ToastProvider ichida chaqirilishi kerak')
  return context
}
