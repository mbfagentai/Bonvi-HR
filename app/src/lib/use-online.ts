import { useEffect, useState } from 'react'

/**
 * Internet bormi.
 *
 * ⚠️ `navigator.onLine` faqat «tarmoq interfeysi bor» deganini
 * biladi — Wi-Fi ulangan, lekin internet yo'q holatni ko'rmaydi.
 * Shu sababli u FAQAT tugmani oldindan o'chirish uchun ishlatiladi;
 * haqiqiy javob baribir so'rovning o'zidan keladi.
 */
export function useOnline(): boolean {
  const [online, setOnline] = useState<boolean>(() => window.navigator.onLine !== false)

  useEffect(() => {
    const goOnline = (): void => setOnline(true)
    const goOffline = (): void => setOnline(false)

    window.addEventListener('online', goOnline)
    window.addEventListener('offline', goOffline)

    return () => {
      window.removeEventListener('online', goOnline)
      window.removeEventListener('offline', goOffline)
    }
  }, [])

  return online
}
