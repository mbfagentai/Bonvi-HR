import { useCallback, useEffect, useRef, useState } from 'react'

import { ApiError } from '@/api/errors'

/**
 * Kichik so'rov qobig'i — TanStack Query O'RNIGA, ataylab.
 *
 * NEGA kutubxona emas: bu ilovada beshta o'qish so'rovi bor va poli
 * iOS 12.2 (UX §F.1). Bitta kesh jadvali va bitta effekt shu beshtasi
 * uchun yetarli; kutubxona esa bundle'ga o'nlab kilobayt va yana bitta
 * brauzer poli qo'shardi.
 *
 * Ikkita qoida shu yerda hal qilinadi va butun ilovada bir xil bo'ladi
 * (UX §E.9):
 *   · BIRINCHI yuklashda — skelet;
 *   · fonda qayta yuklashda — hech narsa: spinner ham, xiralashtirish
 *     ham yo'q, chunki ekrandagi ma'lumot o'zgarmaydi.
 */
export interface QueryResult<T> {
  data: T | null
  /** Birinchi yuklash ketyapti — skelet shu bayroqqa qarab chiziladi. */
  isLoading: boolean
  /** Fonda yangilanmoqda — ekranda hech narsa ko'rsatilmaydi. */
  isRefreshing: boolean
  error: ApiError | null
  /** Ma'lumot keshdan kelgan va yangilash muvaffaqiyatsiz tugagan. */
  isStale: boolean
  /** Kesh yozilgan lahza — «Internet yo'q. 08:35 dagi ma'lumot.» uchun. */
  fetchedAt: Date | null
  refetch: () => void
}

interface CacheEntry {
  data: unknown
  at: number
}

/**
 * Kesh MODUL darajasida: ekranlar orasida borib-kelganda ro'yxat
 * qaytadan skelet bo'lib miltillamasligi uchun. Ilova yopilganda
 * yo'qoladi — uzoq muddatli kesh service worker zimmasida (UX §G.2).
 */
const cache = new Map<string, CacheEntry>()

export function clearQueryCache(): void {
  cache.clear()
}

export function useQuery<T>(key: string | null, fetcher: () => Promise<T>): QueryResult<T> {
  const cached = key ? cache.get(key) : undefined

  const [data, setData] = useState<T | null>((cached?.data as T | undefined) ?? null)
  const [fetchedAt, setFetchedAt] = useState<Date | null>(cached ? new Date(cached.at) : null)
  const [error, setError] = useState<ApiError | null>(null)
  const [isLoading, setLoading] = useState<boolean>(!cached && key !== null)
  const [isRefreshing, setRefreshing] = useState<boolean>(false)
  const [nonce, setNonce] = useState(0)

  // Kalit almashsa (masalan boshqa oy tanlansa) keshdagisi DARHOL
  // ko'rsatiladi — render paytida, effektda emas: aks holda bitta
  // kadr davomida oldingi oyning kataklari turib qolardi.
  const keyRef = useRef(key)
  if (keyRef.current !== key) {
    keyRef.current = key
    const next = key ? cache.get(key) : undefined
    setData((next?.data as T | undefined) ?? null)
    setFetchedAt(next ? new Date(next.at) : null)
    setError(null)
    setLoading(!next && key !== null)
  }

  const fetcherRef = useRef(fetcher)
  fetcherRef.current = fetcher

  useEffect(() => {
    if (key === null) return undefined

    let alive = true
    const hadData = cache.has(key)

    if (hadData) setRefreshing(true)
    else setLoading(true)

    fetcherRef
      .current()
      .then((result) => {
        if (!alive) return
        cache.set(key, { data: result, at: Date.now() })
        setData(result)
        setFetchedAt(new Date())
        setError(null)
      })
      .catch((cause: unknown) => {
        if (!alive) return
        setError(ApiError.from(cause))
      })
      .then(() => {
        if (!alive) return
        setLoading(false)
        setRefreshing(false)
      })

    return () => {
      alive = false
    }
  }, [key, nonce])

  const refetch = useCallback(() => {
    setNonce((value) => value + 1)
  }, [])

  return {
    data,
    isLoading,
    isRefreshing,
    error,
    isStale: error !== null && data !== null,
    fetchedAt,
    refetch,
  }
}

/** Keshni bitta kalit bo'yicha tozalaydi — yozuvdan keyin. */
export function invalidateQuery(prefix: string): void {
  const keys: string[] = []
  cache.forEach((_value, key) => {
    if (key === prefix || key.indexOf(`${prefix}:`) === 0) keys.push(key)
  })
  keys.forEach((key) => cache.delete(key))
}
