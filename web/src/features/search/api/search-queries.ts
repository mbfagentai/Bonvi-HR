import { keepPreviousData, useQuery } from '@tanstack/react-query'

import { fetchGlobalSearch, SEARCH_MIN_LENGTH } from './search-api'

export const searchKeys = {
  root: ['search'] as const,
  global: (query: string) => ['search', 'global', query] as const,
}

/**
 * Global qidiruv natijasi.
 *
 * @param query Kechiktirilgan (debounce) va qirqilgan matn — har harfda
 *   emas, yozish to'xtaganda so'raladi (`global-search.tsx`).
 *
 * `keepPreviousData` — yangi harf qo'shilganda ro'yxat bo'shab, qayta
 * to'lmaydi: eski natija ko'rinib turadi va yangisi kelganda almashadi.
 *
 * `staleTime` 30 soniya: HR bitta odamni qidirib, kartasiga kirib, qaytib
 * xuddi shu matnni yana ko'radi — qayta so'rash shart emas. Lekin xodim
 * qo'shilgan/o'zgargan bo'lsa ham uzoq eskirib qolmaydi.
 */
export function useGlobalSearch(query: string) {
  return useQuery({
    queryKey: searchKeys.global(query),
    queryFn: ({ signal }) => fetchGlobalSearch(query, signal),
    enabled: query.length >= SEARCH_MIN_LENGTH,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  })
}
