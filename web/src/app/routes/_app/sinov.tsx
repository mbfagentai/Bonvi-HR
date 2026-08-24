import { createFileRoute } from '@tanstack/react-router'

import { ProbationsPage } from '@/features/probations/components/probations-page'

/**
 * Sinov muddatidagi xodimlar.
 *
 * HR eng ko'p so'ragan imkoniyat shu sahifada: muddat tugashiga 7 kun
 * qolganlar yuqorida ogohlantirish bo'lib chiqadi. Excel'da bu qo'lda
 * kuzatilardi va tez-tez o'tkazib yuborilardi.
 */
export const Route = createFileRoute('/_app/sinov')({
  component: ProbationsPage,
})
