import { use, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

import { cn } from '@/shared/lib/cn'
import { PageHeaderSlotContext } from './page-header-slot'

/**
 * Sahifa boshi: sarlavha, ostida son yoki holat, harakatlar.
 *
 * ⭐ Keng ekranda u SAHIFADA EMAS, YUQORI PANELDA chiziladi (egasi
 * 2026-10-22): sarlavha va son qidiruvdan chapga ixcham ko'rinishda,
 * harakatlar o'ngdagi boshqaruvlar oldiga ko'chadi. Sabab — katta sarlavha
 * bloki ro'yxat sahifasida 3–4 qatorlik joyni yeb qo'yardi. Panel joylari
 * `app-shell.tsx` da (`PageHeaderSlotContext`), u yerga portal orqali
 * chiziladi — ya'ni tugmalar sahifaning React daraxtida qoladi (holat,
 * oynalar, ruxsatlar o'zgarmaydi).
 *
 * Panelga faqat SON/HOLAT (`subtitle`) va TUGMALAR (`actions` — «Qo'shish»,
 * «Eksport») chiqadi. Egasining ikkinchi qarori (o'sha kun): davr, sana,
 * tablar va ko'rinish almashtirgichi JOYIDA qolsin — ular `toolbar`, sahifada
 * chiziladi. Uzun tavsif (`description`) panelda «ortiqcha ma'lumot» —
 * faqat tor ekrandagi katta sarlavha ostida.
 *
 * Tor ekranda joy yo'q — avvalgidek iOS'ning «Large Title» naqshi.
 */
export function PageHeader({
  title,
  subtitle,
  description,
  actions,
  toolbar,
  className,
}: {
  title: ReactNode
  /** Qisqa: yozuvlar soni yoki holat («539 ta xodim»). Panelda ko'rinadi. */
  subtitle?: ReactNode
  /** Sahifa nima haqida — panelda chiqmaydi. */
  description?: ReactNode
  /** Tugmalar — panelga chiqadi. */
  actions?: ReactNode
  /** Filtr, davr, tablar — sahifada, o'ng tomonda (avvalgi joyida) qoladi. */
  toolbar?: ReactNode
  className?: string
}) {
  const slots = use(PageHeaderSlotContext)

  if (slots !== null) {
    return (
      <>
        {createPortal(
          /*
            Sarlavhalar sahifalarda `IconTile` bilan yoziladi — panelda
            ikonka YASHIRILADI (`data-icon-tile`): o'sha ikonka yon menyuda
            faol band ustida allaqachon turibdi, panelda esa ikki qatorli
            matn yonida u faqat joy olardi. 30 ta sahifani alohida
            o'zgartirish o'rniga bitta joyda — yangi sahifa ham o'zi ixcham.
          */
          <div className="flex min-w-0 flex-col justify-center [&_[data-icon-tile]]:hidden">
            <h1 className="truncate text-body leading-tight font-semibold tracking-tight">
              {title}
            </h1>
            {subtitle && (
              <p
                // Uzun izoh (ma'lumotnomalar) panelda kesiladi — to'liq matn sichqoncha ostida
                title={typeof subtitle === 'string' ? subtitle : undefined}
                className="mt-0.5 truncate text-caption1 leading-tight text-[var(--fg-secondary)] tabular-nums"
              >
                {subtitle}
              </p>
            )}
          </div>,
          slots.title,
        )}
        {actions && createPortal(<div className="flex items-center gap-2">{actions}</div>, slots.actions)}
        {toolbar && (
          <div className={cn('flex flex-wrap items-center justify-end gap-3', className)}>{toolbar}</div>
        )}
      </>
    )
  }

  const subline = subtitle ?? description

  return (
    <div className={cn('flex flex-wrap items-start justify-between gap-4', className)}>
      <div className="min-w-0">
        <h1 className="text-title1 leading-tight font-semibold tracking-tight">{title}</h1>
        {subline && <p className="mt-1.5 text-subhead text-[var(--fg-secondary)]">{subline}</p>}
      </div>
      {(actions || toolbar) && (
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          {toolbar}
          {actions}
        </div>
      )}
    </div>
  )
}
