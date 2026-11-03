import { fetchCatalog, type CatalogKind } from '@/features/catalog/api/catalog-api'
import { fetchEmployees } from '@/features/employees/api/employees-api'

import { fetchBlankSubmissions } from '../api/blanks-api'
import type { DocRefSearch, RefSearch } from '../components/renderer/renderer-context'
import type { RefKind } from './definition-types'

const LIMIT = 20

const CATALOG: Partial<Record<RefKind, CatalogKind>> = {
  department: 'departments',
  position: 'positions',
  direction: 'directions',
}

/**
 * `ref` field search for `<BlankForm searchRef>`: employees through the server search
 * (`search_key` rule), catalogs filtered on the client (they are short and cached by the
 * browser). Candidates are Phase 2 (SPEC §14) — the search returns nothing for now.
 */
export const searchBlankRef: RefSearch = async (kind, query) => {
  const needle = query.trim()
  if (kind === 'employee') {
    const page = await fetchEmployees({ page: 1, pageSize: LIMIT, sort: 'fullName', search: needle })
    return page.items.map((item) => ({ id: item.id, label: item.fullName }))
  }
  const catalog = CATALOG[kind]
  if (catalog === undefined) return []
  const lower = needle.toLocaleLowerCase()
  const items = await fetchCatalog(catalog, true)
  return items
    .filter((item) => lower === '' || item.name.toLocaleLowerCase().includes(lower))
    .slice(0, LIMIT)
    .map((item) => ({ id: item.id, label: item.name }))
}

/** `docRef` field search: Approved blanks of the allowed templates, by number or subject name. */
export const searchBlankDocRef: DocRefSearch = async (templateCodes, query) => {
  const pages = await Promise.all(
    templateCodes.map((templateCode) =>
      fetchBlankSubmissions({ templateCode, status: 'Approved', search: query, page: 1, pageSize: LIMIT }),
    ),
  )
  return pages
    .flatMap((page) => page.items)
    .filter((item) => item.number !== null)
    .map((item) => ({ submissionId: item.id, templateCode: item.templateCode, number: String(item.number) }))
}
