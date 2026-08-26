import { createFileRoute } from '@tanstack/react-router'

import { ExitInterviewsPage } from '@/features/feedback/components/exit-interviews-page'

/**
 * Qayta aloqa (exit interview) jurnali — faqat `feedback.view` ruxsati bilan.
 *
 * `hodisalar.tsx` dagi bilan bir xil naqsh: marshrutda `beforeLoad` yo'q,
 * sahifaning o'zi ruxsat holatini ko'rsatadi.
 */
export const Route = createFileRoute('/_app/qayta-aloqa')({
  component: ExitInterviewsPage,
})
