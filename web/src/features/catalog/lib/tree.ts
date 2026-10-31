import { matchesSearch } from '@/shared/lib/search-fold'

import type { DepartmentNode } from '../api/catalog-api'

export interface FlatDepartment {
  node: DepartmentNode
  /** Ildizdan uzoqlik: 0 — ildiz bo'lim. Chekinish shu bo'yicha hisoblanadi. */
  depth: number
  /** Ota tugunlar zanjiri (ildizdan boshlab). Yopilgan tugunni aniqlash uchun. */
  parents: string[]
}

/**
 * Daraxtni ekranga chizish uchun tekis ro'yxatga aylantiradi.
 *
 * NEGA rekursiv JSX emas: tekis ro'yxatda klaviatura bilan yurish, qidiruv
 * va «hammasini yopish» bir xil oddiy ishlaydi — har biri uchun daraxtni
 * qayta aylanib chiqish shart emas.
 */
export function flattenTree(
  nodes: readonly DepartmentNode[],
  depth = 0,
  parents: string[] = [],
): FlatDepartment[] {
  return nodes.flatMap((node) => [
    { node, depth, parents },
    ...flattenTree(node.children ?? [], depth + 1, [...parents, node.id]),
  ])
}

/** Tugun va uning butun avlodi. Ota tanlashda o'zini o'ziga bermaslik uchun. */
export function collectSubtreeIds(node: DepartmentNode): string[] {
  return [node.id, ...(node.children ?? []).flatMap(collectSubtreeIds)]
}

/**
 * Daraxtni qidiruv bo'yicha filtrlaydi.
 *
 * Tugun o'zi mos kelmasa ham, ichida mos keladigani bo'lsa — qoladi:
 * aks holda topilgan bo'lim daraxtdagi joyini yo'qotadi va foydalanuvchi
 * u qayerdan kelganini tushunmaydi.
 */
export function filterTree(
  nodes: readonly DepartmentNode[],
  query: string,
): DepartmentNode[] {
  const needle = query.trim()
  if (needle === '') return [...nodes]

  // Faqat nom bo'yicha: bo'lim nomi butun ma'lumotnoma bo'yicha unikal va
  // daraxtda ko'rinib turgan yagona matn — foydalanuvchi ko'rmagan narsani
  // qidirmaydi.
  // Yig'ish server qidiruvidagi bilan bir xil (`search-fold.ts`):
  // «Кукон» yozilganda «Қўқон» bo'limi ham topiladi.
  const matches = (node: DepartmentNode): boolean => matchesSearch(node.name, needle)

  const walk = (list: readonly DepartmentNode[]): DepartmentNode[] =>
    list.flatMap((node) => {
      const children = walk(node.children ?? [])
      if (!matches(node) && children.length === 0) return []
      // Mos kelgan tugunning butun ichi ochiq qoladi, oraliq tugunda esa
      // faqat mos kelgan shoxlar ko'rinadi
      return [{ ...node, children: matches(node) ? (node.children ?? []) : children }]
    })

  return walk(nodes)
}

export function countTree(nodes: readonly DepartmentNode[]): number {
  return nodes.reduce((total, node) => total + 1 + countTree(node.children ?? []), 0)
}

/**
 * «Faol» / «Faol emas» tablari uchun daraxt (egasi, 2026-10-31).
 *
 * Boshqa holatdagi tugun faqat ichida kerakli holatdagi bo'lim bo'lsa qoladi — daraxtdagi joyi
 * yo'qolmasin (`filterTree` dagi qoidaning o'zi). Masalan, «Faol emas» tabida yopilgan quyi bo'lim
 * o'zining faol ota bo'limi ostida ko'rinadi.
 */
export function filterTreeByStatus(
  nodes: readonly DepartmentNode[],
  active: boolean,
): DepartmentNode[] {
  return nodes.flatMap((node) => {
    const children = filterTreeByStatus(node.children ?? [], active)
    if (node.isActive !== active && children.length === 0) return []
    return [{ ...node, children }]
  })
}

/** Tanlangan holatdagi tugunlar soni (yo'l uchun qolgan boshqa holatdagilar sanalmaydi). */
export function countTreeByStatus(nodes: readonly DepartmentNode[], active: boolean): number {
  return nodes.reduce(
    (total, node) =>
      total + (node.isActive === active ? 1 : 0) + countTreeByStatus(node.children ?? [], active),
    0,
  )
}
