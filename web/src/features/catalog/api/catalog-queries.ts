import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  addDuty,
  createCatalogItem,
  fetchCatalog,
  fetchDepartmentTree,
  fetchDuties,
  moveDepartment,
  removeDuty,
  reorderDuties,
  setCatalogItemActive,
  updateCatalogItem,
  type CatalogItem,
  type CatalogItemInput,
  type CatalogKind,
  type PositionDuty,
} from './catalog-api'

/**
 * Kesh kalitlari.
 *
 * Barchasi `['catalog', …]` bilan boshlanadi: har qanday o'zgarishdan keyin
 * shu ildizni bekor qilish kifoya. Ma'lumotnomalar kichik va kamdan-kam
 * o'zgaradi — nozik-nozik bekor qilish murakkablikni oqlamaydi.
 */
export const catalogKeys = {
  root: ['catalog'] as const,
  list: (kind: CatalogKind, onlyActive: boolean) =>
    ['catalog', kind, 'list', onlyActive] as const,
  tree: (onlyActive: boolean) => ['catalog', 'departments', 'tree', onlyActive] as const,
  duties: (positionId: string) => ['catalog', 'positions', positionId, 'duties'] as const,
}

export function useCatalogList<T extends CatalogItem = CatalogItem>(
  kind: CatalogKind,
  onlyActive: boolean,
) {
  return useQuery({
    queryKey: catalogKeys.list(kind, onlyActive),
    queryFn: ({ signal }) => fetchCatalog<T>(kind, onlyActive, signal),
  })
}

export function useDepartmentTree(onlyActive: boolean) {
  return useQuery({
    queryKey: catalogKeys.tree(onlyActive),
    queryFn: ({ signal }) => fetchDepartmentTree(onlyActive, signal),
  })
}

/**
 * Yaratish va tahrirlash bitta mutatsiyada: forma bir xil, farq faqat
 * `id` borligida. Ikkiga bo'lish chaqiruvchi tomonda shart holatini
 * ikkilantirar edi.
 */
export function useSaveCatalogItem<
  T extends CatalogItem = CatalogItem,
  TInput extends CatalogItemInput = CatalogItemInput,
>(kind: CatalogKind) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, body }: { id: string | null; body: TInput }) =>
      id === null
        ? createCatalogItem<T, TInput>(kind, body)
        : updateCatalogItem<T, TInput>(kind, id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: catalogKeys.root })
    },
  })
}

export function useToggleCatalogItem(kind: CatalogKind) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      setCatalogItemActive(kind, id, active),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: catalogKeys.root })
    },
  })
}

export function useMoveDepartment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, parentId }: { id: string; parentId: string | null }) =>
      moveDepartment(id, parentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: catalogKeys.root })
    },
  })
}

/** `positionId` yo'q bo'lsa (yangi lavozim hali saqlanmagan) so'rov yuborilmaydi. */
export function useDuties(positionId: string | null) {
  return useQuery({
    queryKey: catalogKeys.duties(positionId ?? ''),
    queryFn: ({ signal }) => fetchDuties(positionId ?? '', signal),
    enabled: positionId !== null && positionId !== '',
  })
}

export function useDutyMutations(positionId: string | null) {
  const queryClient = useQueryClient()
  const id = positionId ?? ''

  /**
   * Server har bir amaldan keyin YANGILANGAN TO'LIQ ro'yxatni qaytaradi,
   * shu sababli javob keshga to'g'ridan-to'g'ri yoziladi: qo'shimcha `GET`
   * ham, ro'yxatning bir zumga bo'shab qolishi ham bo'lmaydi.
   *
   * Lavozimlar ro'yxati esa alohida bekor qilinadi — «vazifalar soni»
   * ustuni shu ma'lumotga bog'liq.
   */
  const applied = (duties: PositionDuty[]) => {
    queryClient.setQueryData(catalogKeys.duties(id), duties)
    void queryClient.invalidateQueries({ queryKey: catalogKeys.list('positions', true) })
    void queryClient.invalidateQueries({ queryKey: catalogKeys.list('positions', false) })
  }

  const add = useMutation({
    mutationFn: (text: string) => addDuty(id, text),
    onSuccess: applied,
  })

  const remove = useMutation({
    mutationFn: (dutyId: string) => removeDuty(id, dutyId),
    onSuccess: applied,
  })

  const reorder = useMutation({
    mutationFn: (orderedIds: string[]) => reorderDuties(id, orderedIds),
    onSuccess: applied,
  })

  return { add, remove, reorder }
}
