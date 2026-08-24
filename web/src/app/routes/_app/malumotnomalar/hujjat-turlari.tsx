import { createFileRoute } from '@tanstack/react-router'

import { CATALOG_META, CatalogPage } from '@/features/catalog'

export const Route = createFileRoute('/_app/malumotnomalar/hujjat-turlari')({
  component: () => <CatalogPage meta={CATALOG_META['document-types']} />,
})
