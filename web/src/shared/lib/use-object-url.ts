import { useEffect, useState } from 'react'

/**
 * Blob'dan vaqtinchalik havola yasaydi va komponent ajratilganda uni
 * BEKOR QILADI.
 *
 * NEGA havola emas, BLOB uzatiladi (va kesh ham blob saqlaydi):
 * `URL.createObjectURL` hujjat umriga bog'langan yozuv qoldiradi — uni
 * faqat qo'lda `revokeObjectURL` bilan bo'shatish mumkin. Agar tayyor
 * havola TanStack Query keshida saqlansa, kesh yozuvi tozalanganini
 * ushlab qolish uchun global obunachi kerak bo'lardi va u bir marta
 * unutilsa — har bir rasm xotirada abadiy qolardi. Blob esa oddiy obyekt:
 * unga havola qolmasa brauzer o'zi yig'ishtiradi, havolani esa uni
 * YARATGAN komponent bekor qiladi. Ya'ni sizish mumkin bo'lgan joy yo'q.
 *
 * `useMemo` emas, `useEffect`: `StrictMode` da effektlar qo'sh chaqiriladi
 * va `useMemo` da yaratilgan havola tozalash bosqichida bekor qilinib,
 * qayta yaratilmasdan qolib ketardi — dev rejimida rasm sinardi.
 */
export function useObjectUrl(blob: Blob | null): string | null {
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    if (blob === null) {
      setUrl(null)
      return
    }

    const created = URL.createObjectURL(blob)
    setUrl(created)

    return () => {
      URL.revokeObjectURL(created)
    }
  }, [blob])

  return url
}
