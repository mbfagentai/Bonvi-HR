import { createRootRoute, Link, Outlet } from '@tanstack/react-router'

import { isFormOnlySite } from '@/features/kiosk/lib/kiosk-site'
import { cn } from '@/shared/lib/cn'
import { buttonVariants } from '@/shared/ui'

export const Route = createRootRoute({
  component: Outlet,
  notFoundComponent: NotFound,
})

function NotFound() {
  // The form-only site (SPEC-planshet-anketa §2.3) serves only `/anketa*` —
  // its 404 leads back to the form, never to an HR route.
  const home = isFormOnlySite() ? '/anketa' : '/'

  return (
    <div className="grid min-h-dvh place-items-center p-8 text-center">
      <div>
        <p className="text-largetitle font-semibold text-[var(--fg-tertiary)]">404</p>
        <p className="mt-2 text-title3 font-medium">Bunday sahifa topilmadi</p>
        {/*
          Tugma ko'rinishi qo'lda takrorlanmaydi: `buttonVariants` bilan
          bir xil rang, bosish javobi va o'lcham olinadi. Ilgari bu yerda
          faqat `bg-[var(--accent)]` turardi — hover ham, bosish holati ham
          yo'q edi, ya'ni bu tugma tizimdagi boshqalardan ajralib qolardi.
        */}
        <Link
          to={home}
          className={cn(buttonVariants({ variant: 'primary' }), 'mt-6')}
        >
          Bosh sahifaga qaytish
        </Link>
      </div>
    </div>
  )
}
