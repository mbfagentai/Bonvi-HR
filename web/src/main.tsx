import { RouterProvider, createRouter } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

import { Providers } from '@/app/providers'
import { routeTree } from '@/app/routeTree.gen'
import '@/styles/globals.css'

/*
 * `scrollRestoration` ATAYLAB YO'Q.
 *
 * U OYNA (window) scroll'ini tiklaydi, oyna esa endi umuman surilmaydi:
 * ilova qobig'i ekran balandligida qotib turadi va suriladigan yagona
 * joy — uning ichidagi `main` (sababi `app-shell.tsx` da). Ya'ni bu
 * sozlama qolgan bo'lsa hech qanday ish bajarmasdan turardi va keyingi
 * o'quvchini «scroll tiklash allaqachon ulangan» deb chalg'itardi.
 * Yangi sahifada tepadan boshlashni `AppShell` o'zi bajaradi.
 */
const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
})

// Marshrut tiplarini global ro'yxatga olish — Link va navigate tip-xavfsiz bo'ladi
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }

  /**
   * Marshrutning statik belgilari (`createFileRoute(...)({ staticData })`).
   *
   * `fullWidth` — sahifa kontenti qobiqning 1400 px chegarasini bekor
   * qilib, `main` ning butun kengligini oladi (`app-shell.tsx`). Sukut —
   * YO'Q: faqat ekrani o'zi keng bo'lishi kerak bo'lgan sahifa yoqadi.
   */
  interface StaticDataRouteOption {
    fullWidth?: boolean
  }
}

const root = document.getElementById('root')
if (!root) throw new Error('#root elementi topilmadi')

createRoot(root).render(
  <StrictMode>
    <Providers>
      <RouterProvider router={router} />
    </Providers>
  </StrictMode>,
)
