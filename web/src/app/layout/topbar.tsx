import type { Ref } from 'react'

import { useAuth } from '@/features/auth'
import { NotificationsBell } from '@/features/notifications'
import { GlobalSearch } from '@/features/search'
import { LanguageSwitcher } from '@/shared/ui/language-switcher'
import { ThemeToggle } from '@/shared/ui/theme-toggle'
import { UserMenu } from './user-menu'

/**
 * Yuqori panel. iOS uslubida shaffof, orqa foni bulanadi (blur-bar) —
 * kontent ostidan o'tganda ko'rinib turadi.
 *
 * Tarkibi: sahifa sarlavhasi (ixcham, son bilan) → qidiruv → bo'sh joy →
 * sahifa harakatlari → til, ko'rinish, bildirishnoma va eng chekkada
 * foydalanuvchi menyusi.
 *
 * ⭐ Sarlavha va harakatlar joylari BO'SH turadi — ularni sahifaning
 * `PageHeader` i portal orqali to'ldiradi (egasi 2026-10-22: sahifa
 * tepasidagi katta sarlavha bloki jadvaldan 3–4 qatorlik joyni olardi).
 * Bo'sh joy `empty:hidden` bilan yo'qoladi — sarlavhasiz sahifada (xodim
 * kartasi) ajratgich ham, bo'shliq ham qolmaydi.
 * Menyuni yig'ish tugmasi bu yerda emas — u yon menyuning o'z pastida.
 *
 * `sticky` EMAS, `shrink-0`: panel qobiqning qimirlamaydigan qismida
 * turadi (`app-shell.tsx` dagi izohga qarang), ya'ni uni qadab turishning
 * hojati yo'q — suriladigan maydon undan pastda boshlanadi. `shrink-0`
 * esa kerak: flex ustunida kontent uzun bo'lsa panel siqilib ketardi.
 *
 * `z-30` qoladi — ostidan suriladigan kontent `blur-bar` ning shaffof
 * fonidan o'tganda uning ustiga chiqib ketmasligi kerak.
 */
export function Topbar({
  titleSlotRef,
  actionsSlotRef,
}: {
  titleSlotRef: Ref<HTMLDivElement>
  actionsSlotRef: Ref<HTMLDivElement>
}) {
  // `/api/search` is closed to a «Davomat mas'uli». The bell stays: `/api/notifications/**` is open.
  const { isAttendanceScoped } = useAuth()

  return (
    <header className="blur-bar z-30 shrink-0 border-b border-[var(--border)]">
      <div className="flex h-16 items-center gap-3 px-5 lg:gap-4 lg:px-6">
        {/* Sahifa sarlavhasi — o'ng tomonida ingichka ajratgich */}
        <div
          ref={titleSlotRef}
          className="flex h-10 max-w-[280px] min-w-0 shrink items-center border-r border-[var(--border)] pr-4 empty:hidden xl:max-w-[340px]"
        />

        {/*
          Global qidiruv — bo'sh kenglikning hammasini egallaydi. Mantiqi
          `features/search` da: u marshrutga ham, ruxsatga ham bog'liq,
          panelning o'zi esa faqat joylashtirish bilan shug'ullanadi
          (`user-menu.tsx` bilan bir xil naqsh).

          O'ROVCHI `div` ruxsati yo'q foydalanuvchida ham QOLADI, garchi
          ichi bo'sh bo'lsa ham: `flex-1` bo'sh kenglikni yeb turadi va
          shu sababli o'ngdagi boshqaruvlar (til, ko'rinish, hisob) doim
          bir joyda — chekkada — turadi. Aks holda ular ekranning
          o'rtasiga sirg'alib tushardi.

          Keng ekranda qidiruv 480 px dan oshmaydi — butun enga
          cho'zilgan maydon sarlavha va harakatlarni siqib qo'yardi;
          natijalar oynasi esa o'zi kengroq ochiladi (`global-search.tsx`).
        */}
        <div className="min-w-0 flex-1">
          <div className="lg:max-w-[480px]">
            {!isAttendanceScoped && <GlobalSearch />}
          </div>
        </div>

        {/* Sahifa harakatlari (yangi yozuv, tablar, davr) */}
        <div ref={actionsSlotRef} className="flex shrink-0 items-center empty:hidden" />

        <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
          <LanguageSwitcher />

          <ThemeToggle />

          {/*
            Qo'ng'iroq — o'z moduli bilan birga keladi (tugma, sanoq va
            oyna bitta komponentda). Panel bu yerda ham faqat
            JOYLASHTIRADI, mantiqni saqlamaydi — `GlobalSearch` va
            `UserMenu` bilan bir xil naqsh.

            ⚠️ Ilgari shu o'rinda `onClick` siz tugma va ustida QATTIQ
            YOZIB QO'YILGAN qizil nuqta turardi: u hech narsa qilmasdi va
            hech qachon o'chmasdi. Ikkalasi ham
            `features/notifications` ichida tuzatildi.
          */}
          <NotificationsBell />

          {/* Ajratgich — hisob qismini boshqa boshqaruvlardan ajratadi */}
          <span className="mx-0.5 hidden h-6 w-px bg-[var(--border)] sm:block" />

          <UserMenu />
        </div>
      </div>
    </header>
  )
}
