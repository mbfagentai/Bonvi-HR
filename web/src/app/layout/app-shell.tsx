import { useRouterState } from '@tanstack/react-router'
import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'

import { Sidebar } from './sidebar'
import { Topbar } from './topbar'

const STORAGE_KEY = 'bonvi.sidebar'

/**
 * Ilova qobig'i: yon menyu + yuqori panel + kontent.
 * Menyuning yig'ilgan holati saqlanadi.
 *
 * ⚠️ QOBIQNING O'ZI HECH QACHON SURILMAYDI.
 *
 * Ildiz `h-dvh overflow-hidden` — ya'ni hujjat (document) balandligi doim
 * aynan ekran balandligi va brauzerning o'z scroll'i umuman paydo
 * bo'lmaydi. Suriladigan yagona joy — `main`.
 *
 * NEGA ilgarigi `sticky` yechimi tashlandi: yon menyu ham, yuqori panel
 * ham `position: sticky` bilan ushlab turilardi, hujjatning o'zi esa
 * surilaverardi. Sticky — «suriladigan oqim ichida vaqtincha qadalib
 * turish» degani, «qimirlamaydi» degani EMAS: uni istalgan ota
 * elementdagi `overflow`, `transform`, `filter` yoki `contain` jimgina
 * ishdan chiqaradi va u holda element oddiy oqim bilan birga surilib
 * ketadi — aynan shu yuz berdi, menyu kontent bilan birga tepaga chiqib
 * ko'zdan yo'qoldi. Bu yerda esa qadalib turish CSS xususiyatiga emas,
 * TUZILMAGA tayanadi: menyu suriladigan konteynerning ICHIDA emas,
 * YONIDA turadi, ya'ni uni surib yuborishning imkoni yo'q.
 *
 * Yon ta'siri (ataylab qabul qilingan): `main` ichidagi `sticky`
 * elementlar endi oynaga emas, `main` ga nisbatan qadaladi — jadval
 * sarlavhalari uchun bu to'g'ri xatti-harakat.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1'
    } catch {
      return false
    }
  })

  // Tor ekranda menyu avtomatik yig'iladi
  useEffect(() => {
    const media = globalThis.matchMedia?.('(max-width: 1024px)')
    if (!media) return

    const apply = (matches: boolean) => {
      if (matches) setCollapsed(true)
    }

    apply(media.matches)
    const onChange = (event: MediaQueryListEvent) => apply(event.matches)
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  const toggle = useCallback(() => {
    setCollapsed((previous) => {
      const next = !previous
      try {
        localStorage.setItem(STORAGE_KEY, next ? '1' : '0')
      } catch {
        // Maxfiy rejimda saqlash mumkin emas — bu xato emas
      }
      return next
    })
  }, [])

  const main = useMainScrollReset()

  return (
    <div className="flex h-dvh overflow-hidden">
      <Sidebar collapsed={collapsed} onToggle={toggle} />

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar />

        {/*
          Butun ilovadagi YAGONA suriladigan maydon.

          `scrollbar-gutter: stable` shu yerda: ilgari u `html` da turardi
          va ma'nosi bor edi — hujjat surilardi. Endi hujjat surilmaydi,
          ya'ni u yerda qolgan bo'lsa hech qachon chiqmaydigan scroll
          uchun o'ng chekkada doimiy bo'sh yo'lak band qilib turardi.
          Bu yerda esa u o'z ishini bajaradi: ro'yxat bir sahifadan
          ikkinchisiga o'tganda kontent chapga-o'ngga sakramaydi.
        */}
        <main
          ref={main}
          className="flex-1 overflow-y-auto px-5 py-6 [scrollbar-gutter:stable] lg:px-8 lg:py-8"
        >
          <div className="mx-auto w-full max-w-[1400px]">{children}</div>
        </main>
      </div>
    </div>
  )
}

/**
 * Yangi sahifaga o'tilganda kontentni tepasidan boshlaydi.
 *
 * NEGA QO'LDA: brauzer buni yangi manzilga o'tganda O'ZI qiladi — lekin
 * faqat HUJJAT scroll'i uchun. Endi suriladigan joy `main`, u esa
 * marshrutlar almashganda ham DOM'da o'sha-o'sha element bo'lib qoladi,
 * ya'ni scroll holati ham o'zgarmasdi: ro'yxatning oxirida turib xodim
 * kartasini ochgan HR yangi sahifani o'rtasidan ko'rardi.
 *
 * Kalit sifatida `href` olinadi, `pathname` emas: sahifa raqami va filtr
 * manzil satrida yashaydi (`employees-search.ts`), ya'ni «2-sahifaga
 * o'tish» ham yangi ro'yxat va u ham tepadan boshlanishi kerak.
 *
 * `scrollRestoration` routerdan olib tashlandi (`main.tsx`) — u oyna
 * scroll'ini tiklardi, oyna esa endi umuman surilmaydi. Ya'ni «orqaga»
 * bosganda ro'yxatning oldingi joyiga qaytish yo'qoldi; buning evaziga
 * menyu joyida qoladi. Kerak bo'lsa keyin `main` uchun alohida
 * holat xaritasi bilan qaytariladi (`useElementScrollRestoration`).
 */
function useMainScrollReset() {
  const ref = useRef<HTMLElement>(null)
  const href = useRouterState({ select: (state) => state.location.href })

  useEffect(() => {
    ref.current?.scrollTo({ top: 0 })
  }, [href])

  return ref
}
