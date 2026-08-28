import { useEffect, useRef, useState } from 'react'

/**
 * Faqat qidiruvdan iborat manzil holati.
 *
 * NEGA umumiy modul: sakkizta marshrut (sinov, rezerv, qora ro'yxat,
 * nazorat, qobiliyatli, qayta aloqa, hodisalar, foydalanuvchilar) bir xil
 * bitta maydonni saqlaydi. Har biriga o'z `*-search.ts` fayli yozilsa,
 * sakkizta bir xil tekshiruvchi paydo bo'lardi va ular vaqt o'tib
 * ajralib ketardi — bu loyihada aynan shu tarzda ikki nusxadagi qoida
 * xatoga olib kelgan.
 *
 * Filtri KO'P bo'lgan ro'yxatlar (xodimlar, buyruqlar, rag'bat) o'z
 * faylini saqlab qoladi: u yerda qidiruvdan tashqari tur, sana oralig'i,
 * bo'lim, firma va saralash ham bor va ularning tekshiruvi umumiy
 * bo'lolmaydi.
 */
export interface ListSearch {
  /**
   * Qidiruv matni. Bo'sh yoki faqat bo'shliq bo'lsa manzilga umuman
   * yozilmaydi — «?q=» bo'lib osilib turishi shovqindan boshqa narsa
   * emas (`employees-search.ts` dagi bilan bir xil qoida).
   */
  q?: string
}

/**
 * Manzil satridan kelgan qiymatni tekshiradi.
 *
 * Hech qachon xato TASHLAMAYDI: noto'g'ri qiymat («?q[]=1») shunchaki
 * tashlab yuboriladi va sahifa ochilaveradi. Bu qat'iy konvensiya —
 * havolani qo'lda tahrirlagan yoki eski xatcho'pdan kelgan odam
 * ishlaydigan sahifani ko'rishi kerak, «oq ekran» emas.
 */
export function validateListSearch(search: Record<string, unknown>): ListSearch {
  const value = search['q']

  return {
    q: typeof value === 'string' && value.trim() !== '' ? value : undefined,
  }
}

/**
 * Maydondagi matnni manzilga yoziladigan qiymatga o'giradi.
 *
 * Bo'sh satr `undefined` bo'lib qaytadi va TanStack Router uni manzilga
 * umuman qo'shmaydi — ya'ni qidiruvni tozalash havolani ham tozalaydi.
 */
export function toSearchParam(value: string): string | undefined {
  return value.trim() === '' ? undefined : value
}

/**
 * Yozish to'xtagach so'rov yuborilguncha kutiladigan vaqt.
 *
 * 350 ms — mavjud toolbar'larda allaqachon tanlangan qiymat
 * (`employees-toolbar.tsx`): 2300 xodimda bu «F.I.Sh. bo'yicha qidirish»
 * ni 15 ta o'rniga 1 ta so'rovga aylantiradi. Bu yerda konstanta bo'lib
 * turibdi, chunki endi u BIR NECHTA ekranga tegishli va ular bir xil
 * his qilishi kerak.
 */
const DEBOUNCE_MS = 350

/**
 * Qidiruv maydonining «qoralamasi» — har harfda so'rov yubormaydi.
 *
 * Maydon darhol yozilganini ko'rsatadi (`draft`), lekin tashqi holat
 * (manzil satri) faqat yozish to'xtagandan 350 ms keyin yangilanadi.
 *
 * NEGA `onCommit` havolada saqlanadi: u effekt bog'liqligida bo'lsa, ota
 * komponentning har bir qayta render'i (masalan so'rov javobi kelganda)
 * taymerni noldan boshlab yuborardi va qidiruv hech qachon ishga
 * tushmasligi mumkin edi. Havola bilan effekt faqat `draft` o'zgarganda
 * qayta o'rnatiladi.
 *
 * NEGA tashqi qiymat ham kuzatiladi: «orqaga» bosilganda yoki filtr
 * tozalanganda manzildagi matn o'zgaradi va maydon unga ergashishi kerak.
 *
 * ⚠️ Mavjud uchta toolbar (`employees-`, `employments-`, `incentives-`)
 * shu mantiqni o'z ichida takrorlab yozgan edi. Ikkitasi shu hook'ga
 * o'tkazildi; xodimlar toolbar'i ataylab tegilmadi — u hozir boshqa
 * ish doirasida (yuqori paneldagi umumiy qidiruv) o'qilmoqda.
 */
export function useSearchDraft(
  value: string,
  onCommit: (next: string) => void,
): [string, (next: string) => void] {
  const [draft, setDraft] = useState(value)

  const latest = useRef({ value, onCommit })
  latest.current = { value, onCommit }

  useEffect(() => {
    setDraft(value)
  }, [value])

  useEffect(() => {
    if (draft === latest.current.value) return

    const timer = window.setTimeout(() => {
      latest.current.onCommit(draft)
    }, DEBOUNCE_MS)

    return () => window.clearTimeout(timer)
  }, [draft])

  return [draft, setDraft]
}
