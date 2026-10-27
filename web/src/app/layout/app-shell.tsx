import { useRouterState } from '@tanstack/react-router'
import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore, type ReactNode } from 'react'

import { LiveNotifications } from '@/features/notifications'
import { cn } from '@/shared/lib/cn'
import { PageHeaderSlotContext, type PageHeaderSlots } from '@/shared/ui'

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

  /*
    Sahifa sarlavhasi va harakatlari yuqori panelga chiqadi (`PageHeader`,
    egasi 2026-10-22) — faqat keng ekranda. Joylar DOM element sifatida
    holatda turadi: panel chizilgach ular paydo bo'ladi va sahifalar shu
    paytda portal orqali o'z sarlavhasini u yerga ko'chiradi.
  */
  const wide = useWideScreen()
  const [titleSlot, setTitleSlot] = useState<HTMLElement | null>(null)
  const [actionsSlot, setActionsSlot] = useState<HTMLElement | null>(null)
  const slots = useMemo<PageHeaderSlots | null>(
    () =>
      wide && titleSlot !== null && actionsSlot !== null
        ? { title: titleSlot, actions: actionsSlot }
        : null,
    [wide, titleSlot, actionsSlot],
  )

  /*
    Kontentning 1400 px chegarasi — o'qiladigan sahifalar (karta, forma,
    ro'yxat) uchun: katta monitorda qator uzunligi ko'zni charchatmasin.
    Ba'zi sahifa esa aynan ENNI talab qiladi — masalan davomat statistikasi:
    unda diagramma, jadval va doimiy filtr ustuni yonma-yon turadi va 1400
    px ga siqilganda katta monitorda o'ngda bo'sh maydon qoladi-yu, jadval
    ezilib turardi (egasi 2026-09-18). Shunday sahifa marshrutida
    `staticData: { fullWidth: true }` yoziladi; qolgan hamma sahifa
    avvalgidek.
  */
  const fullWidth = useRouterState({
    select: (state) => state.matches.some((match) => match.staticData.fullWidth === true),
  })

  return (
    <div className="flex h-dvh overflow-hidden">
      <Sidebar collapsed={collapsed} onToggle={toggle} />

      <PageHeaderSlotContext value={slots}>
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar titleSlotRef={setTitleSlot} actionsSlotRef={setActionsSlot} />

        {/*
          Butun ilovadagi YAGONA suriladigan maydon.

          `scrollbar-gutter: stable` shu yerda: ilgari u `html` da turardi
          va ma'nosi bor edi — hujjat surilardi. Endi hujjat surilmaydi,
          ya'ni u yerda qolgan bo'lsa hech qachon chiqmaydigan scroll
          uchun o'ng chekkada doimiy bo'sh yo'lak band qilib turardi.
          Bu yerda esa u o'z ishini bajaradi: ro'yxat bir sahifadan
          ikkinchisiga o'tganda kontent chapga-o'ngga sakramaydi.
        */}
        {/*
          Jonli bildirishnomalar — butun ilovada BIR MARTA.

          Nega aynan bu yerda: xabar ekran ustida suzadi va u
          `main` ning scroll'i bilan birga surilmasligi kerak. Qobiq
          esa marshrutlar almashganda qayta chizilmaydi — ya'ni
          oqimning ulanishi sahifadan sahifaga o'tganda uzilmaydi.

          Kirish sahifasi bu qobiqdan TASHQARIDA (`_app.tsx`), ya'ni
          tizimga kirmagan odamda oqim umuman ochilmaydi.
        */}
        <LiveNotifications />

        <main
          ref={main}
          className="flex-1 overflow-y-auto px-5 py-5 [scrollbar-gutter:stable] lg:px-6"
        >
          {/*
            Ro'yxat sahifasi (`data-list-frame`) bo'lsa, o'ram `main` ning
            to'liq balandligini oladi: sahifa sarlavhasi, filtrlar va
            sahifalash joyida turadi, qatorlar esa jadvalning O'Z ICHIDA
            aylanadi (`DataTable fill`). Egasi 2026-10-15: «butun sahifa
            tepaga scroll bo'lib ketmasin, listning o'zi scroll bo'lsin».

            Faqat keng va baland oynada (`frame` varianti, globals.css):
            telefonda yoki past noutbukda sarlavha va filtrlar ekranning
            yarmini egallaydi — qatorlar uchun joy qolmaydi, u yerda oddiy
            oqim qulayroq.

            Ro'yxat sahifasi 1400 px chegarasiz — ekranning butun eni
            (egasi 2026-10-22: «katta ekranda ishlatishadi, eniga keng
            bo'lishi muammo emas»). Jadval qatorlari bir qatorli, ustunlar
            esa ko'p — chegara katta monitorda o'ngda bo'sh maydon qoldirib,
            jadvalni gorizontal aylantirishga majbur qilardi.
          */}
          <div
            className={cn(
              'mx-auto w-full',
              // `data-wide-list` — ichida aylanmaydigan, lekin keng ro'yxat (belgilar ro'yxatlari)
              !fullWidth && 'max-w-[1400px] has-[>[data-list-frame]]:max-w-none has-[>[data-wide-list]]:max-w-none',
              'frame:has-[>[data-list-frame]]:h-full',
            )}
          >
            {children}
          </div>
        </main>
      </div>
      </PageHeaderSlotContext>
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
    // Ichida aylanadigan ro'yxatlar ham (`DataTable fill`) — yangi sahifa
    // yoki filtr ham yangi ro'yxat
    ref.current?.querySelectorAll('[data-list-scroll]').forEach((list) => list.scrollTo({ top: 0 }))
  }, [href])

  return ref
}

/** Keng ekran (`lg`, 64rem) — yuqori panelda sahifa sarlavhasiga joy bor. */
const WIDE_QUERY = '(min-width: 64rem)'

function subscribeWide(onChange: () => void): () => void {
  const media = globalThis.matchMedia?.(WIDE_QUERY)
  media?.addEventListener('change', onChange)
  return () => media?.removeEventListener('change', onChange)
}

function useWideScreen(): boolean {
  return useSyncExternalStore(
    subscribeWide,
    () => globalThis.matchMedia?.(WIDE_QUERY).matches ?? true,
    () => true,
  )
}
