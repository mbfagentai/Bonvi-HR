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
  const needle = query.trim().toLocaleLowerCase()
  if (needle === '') return [...nodes]

  const matches = (node: DepartmentNode): boolean =>
    node.name.toLocaleLowerCase().includes(needle) ||
    (node.code ?? '').toLocaleLowerCase().includes(needle)

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
