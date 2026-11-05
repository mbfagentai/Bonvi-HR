import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useMemo } from 'react'

import { fetchEmployee } from '@/features/employees/api/employees-api'
import { employeeKeys } from '@/features/employees/api/employees-queries'
import {
  addDuty,
  createCatalogItem,
  fetchCatalog,
  fetchCatalogUsage,
  fetchDepartmentTree,
  fetchDuties,
  moveDepartment,
  reassignCatalogItem,
  removeDuty,
  reorderDuties,
  setCatalogItemActive,
  setDepartmentHead,
  updateCatalogItem,
  type CatalogItem,
  type CatalogItemInput,
  type CatalogKind,
  type DepartmentItem,
  type PositionDuty,
  type ReassignCatalogInput,
  type ReassignCatalogKind,
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
  usage: (kind: CatalogKind, id: string) => ['catalog', kind, 'usage', id] as const,
}

export function useCatalogList<T extends CatalogItem = CatalogItem>(
  kind: CatalogKind,
  onlyActive: boolean,
  enabled = true,
) {
  return useQuery({
    queryKey: catalogKeys.list(kind, onlyActive),
    queryFn: ({ signal }) => fetchCatalog<T>(kind, onlyActive, signal),
    enabled,
  })
}

export function useDepartmentTree(onlyActive: boolean, enabled = true) {
  return useQuery({
    queryKey: catalogKeys.tree(onlyActive),
    queryFn: ({ signal }) => fetchDepartmentTree(onlyActive, signal),
    enabled,
  })
}

/**
 * Bo'limlarning TEKIS ro'yxati — boshliq va yuridik shaxs shu yerda bor,
 * daraxt tugunida esa yo'q (SPEC-jamoam §E). Faolsizlari ham: daraxt ham
 * ularni ko'rsatadi.
 */
export function useDepartmentList() {
  return useCatalogList<DepartmentItem>('departments', false)
}

/**
 * Boshliq ismlari — `bo'lim id → ism`, daraxtdagi xira yozuv uchun.
 *
 * NEGA yangi API'siz: bo'lim faqat boshliq IDENTIFIKATORINI biladi, ism esa
 * xodim kartasida (`GET /api/employees/{id}`). Har bir HAR XIL boshliq uchun
 * bitta so'rov; kesh kaliti xodim kartasiniki bilan bir xil, ya'ni kartani
 * ochgan HR uni qayta yuklamaydi. Boshliqlar soni bo'limlardan ko'p bo'lmaydi
 * (prod'da 105 bo'lim).
 *
 * `enabled = false` (masalan `employees.view` ruxsati yo'q) — ism so'ralmaydi,
 * daraxt boshliqsiz chiziladi.
 */
export function useDepartmentHeadNames(enabled: boolean): ReadonlyMap<string, string> {
  const list = useDepartmentList()

  const headIds = useMemo(() => {
    const ids = new Set<string>()
    for (const item of list.data ?? []) {
      if (item.headEmployeeId) ids.add(item.headEmployeeId)
    }
    return [...ids]
  }, [list.data])

  // Barqaror `combine` — natijalar o'zgarmasa, xarita ham o'sha (daraxt qayta chizilmaydi)
  const combine = useCallback(
    (results: ReadonlyArray<{ data?: { fullName: string } | undefined }>) => {
      const names = new Map<string, string>()
      results.forEach((result, index) => {
        const id = headIds[index]
        if (id !== undefined && result.data) names.set(id, result.data.fullName)
      })
      return names
    },
    [headIds],
  )

  const namesById = useQueries({
    queries: headIds.map((id) => ({
      queryKey: employeeKeys.details(id),
      queryFn: ({ signal }: { signal: AbortSignal }) => fetchEmployee(id, signal),
      enabled,
      // Ism kamdan-kam o'zgaradi; yiqilsa (o'chirilgan xodim) — shunchaki yozuvsiz
      staleTime: 5 * 60_000,
      retry: false,
    })),
    combine,
  })

  return useMemo(() => {
    const byDepartment = new Map<string, string>()
    for (const item of list.data ?? []) {
      const name = item.headEmployeeId ? namesById.get(item.headEmployeeId) : undefined
      if (name) byDepartment.set(item.id, name)
    }
    return byDepartment
  }, [list.data, namesById])
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

/** Faol emas qilish oynasi ochilganda — yozuvda kim qolgani (har ochilishda yangidan). */
export function useCatalogUsage(kind: CatalogKind, id: string, enabled: boolean) {
  return useQuery({
    queryKey: catalogKeys.usage(kind, id),
    queryFn: ({ signal }) => fetchCatalogUsage(kind, id, signal),
    enabled,
    staleTime: 0,
    gcTime: 0,
  })
}

/** Xodimlarni o'tkazib faol emas qilish — keyin ma'lumotnomalar ham, xodimlar ham qayta o'qiladi. */
export function useReassignCatalogItem(kind: ReassignCatalogKind) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: ReassignCatalogInput }) =>
      reassignCatalogItem(kind, id, input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: catalogKeys.root })
      void queryClient.invalidateQueries({ queryKey: employeeKeys.root })
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

/** Bo'lim boshlig'ini xodim kartasidan almashtirish — keyin bo'limlar ro'yxati qayta o'qiladi. */
export function useSetDepartmentHead() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ departmentId, employeeId }: { departmentId: string; employeeId: string | null }) =>
      setDepartmentHead(departmentId, employeeId),
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
