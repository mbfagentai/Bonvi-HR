import { createFileRoute } from '@tanstack/react-router'

import { CandidateCardPage } from '@/features/recruiting/components/candidate-card-page'

/**
 * Nomzod kartasi: shaxsiy ma'lumot, rezyume, bosqichlar tarixi va
 * ish takliflari.
 *
 * Tab tanlovi manzilda SAQLANMAYDI (xodim kartasidagi bilan bir xil
 * qaror): u ro'yxatni filtrlamaydi va «orqaga» bosilganda tiklanishi
 * shart bo'lgan holat emas — karta har safar «Umumiy» dan ochiladi.
 */
export const Route = createFileRoute('/_app/nomzodlar/$candidateId')({
  component: CandidateCardPage,
})
