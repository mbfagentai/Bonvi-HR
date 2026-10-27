import { useQuery } from '@tanstack/react-query'

import { fetchCandidateReferrals, type CandidateReferralsParams } from './candidate-referrals-api'
import { fetchTurnover } from './turnover-api'

/**
 * Kesh kalitlari.
 *
 * Oy soni kalitning ICHIDA (`dashboard-queries.ts` dagi davr bilan bir xil
 * sabab): almashtirgich bosilganda yangi so'rov ketadi, lekin oldingi davr
 * keshda qoladi. HR uch davr orasida u yoqdan-bu yoqqa yuradi («yilda
 * shunchami — ikki yilda-chi?») va qaytishda grafik darhol chiziladi.
 */
export const reportKeys = {
  root: ['reports'] as const,
  turnover: (months: number) => ['reports', 'turnover', months] as const,
  candidateReferrals: (params: CandidateReferralsParams) =>
    ['reports', 'candidate-referrals', params] as const,
}

/**
 * Kadrlar oqimi hisoboti.
 */
export function useTurnover(months: number) {
  return useQuery({
    queryKey: reportKeys.turnover(months),
    queryFn: ({ signal }) => fetchTurnover(months, signal),

    /**
     * Davr almashtirilganda grafik bo'shab, keyin qayta to'lmaydi — eski
     * ustunlar ko'rinib turadi va faqat holat «yangilanmoqda» bo'ladi.
     * Skelet faqat ENG BIRINCHI yuklashda chiqadi: aks holda har bosishda
     * kartaning balandligi sakrab, ekran silkinardi.
     */
    placeholderData: (previous) => previous,

    /**
     * Besh daqiqa — bosh sahifadagi bir daqiqadan UZUNROQ, ataylab.
     *
     * U yerdagi sonlar «bugun» haqida va bir kishi qabul qilinsa darhol
     * o'zgaradi. Bu yerdagi eng kichik birlik esa OY: bitta yangi buyruq
     * 12 oylik ustunlar qatorini ko'zga ko'rinadigan darajada
     * o'zgartirmaydi. Ya'ni tez-tez qayta so'rash faqat og'ir yig'ma
     * so'rovni bekorga takrorlash bo'lardi.
     */
    staleTime: 5 * 60_000,
  })
}

/**
 * «Nomzodlar qayerdan bilgan» (SPEC-qayerdan-bilgan §7). Same caching as the
 * turnover: the previous numbers stay on screen while a new period loads, so
 * the card does not jump. `enabled` — off for an inverted date range (the
 * server would answer 400) and without the recruiting permission.
 */
export function useCandidateReferrals(params: CandidateReferralsParams, enabled: boolean) {
  return useQuery({
    queryKey: reportKeys.candidateReferrals(params),
    queryFn: ({ signal }) => fetchCandidateReferrals(params, signal),
    enabled,
    placeholderData: (previous) => previous,
    staleTime: 5 * 60_000,
  })
}
