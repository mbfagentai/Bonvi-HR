import { createFileRoute } from '@tanstack/react-router'

import { BlanksPage } from '@/features/blanks/components/blanks-page'
import { validateBlanksSearch } from '@/features/blanks/lib/blanks-search'

/**
 * «Blankalar» list (docs/SPEC-blankalar.md §7 #6). Search, filters and page live in the
 * address bar — «Orqaga» from a blank returns to the same filtered page.
 */
export const Route = createFileRoute('/_app/blankalar/')({
  validateSearch: validateBlanksSearch,
  component: BlanksPage,
})
