import { createFileRoute } from '@tanstack/react-router'

import { CATALOG_META, CatalogPage } from '@/features/catalog'

export const Route = createFileRoute('/_app/malumotnomalar/qayerdan-bilgan')({
  component: () => <CatalogPage meta={CATALOG_META['referral-channels']} />,
})
