import { useLocale } from '@/shared/i18n/use-locale'
import { Badge } from '@/shared/ui'

import type { TurnoverReason } from '../api/turnover-api'
import { percentText, voluntaryLook } from '../lib/reports-presentation'
import { share } from '../lib/turnover-math'

/**
 * Ketish sabablarining taqsimoti — gorizontal «bar list».
 *
 * NEGA doiraviy diagramma (pie) EMAS: sabablar o'nga yaqin va ularning
 * ko'pchiligi bitta-ikkitadan. Doiraning ichida bunday bo'laklar sanchiq
 * bo'lib qoladi va yorlig'i sig'maydi — natijada baribir yon tomonga
 * ro'yxat chizishga to'g'ri kelardi. Qator ko'rinishi esa nomni to'liq
 * ko'rsatadi, sonni va ulushni yonma-yon qo'yadi va uzunligi bo'yicha
 * darhol taqqoslanadi.
 *
 * Tartib SERVERDAN keladi (ko'pdan kamga) va bu yerda qayta saralanmaydi
 * — tartib shartnomaning bir qismi (`turnover-api.ts`).
 */
export function TurnoverReasons({ reasons }: { reasons: readonly TurnoverReason[] }) {
  const { t, num } = useLocale()

  /*
    Maxraj ro'yxatning O'ZIDAN yig'iladi, oylik `left` yig'indisidan emas.

    Ikkalasi bir xil bo'lishi kerak, lekin agar biror ketish sabab
    taqsimotiga tushmay qolsa (masalan hodisa turi boshqacha bo'lsa),
    ustunlar yig'indisi 100% dan oshib ketardi. O'z yig'indisiga bo'lish
    esa har doim izchil manzara beradi: ekranda ko'ringan sonlar ekranda
    ko'ringan foizlarni beradi.
  */
  const total = reasons.reduce((sum, reason) => sum + reason.count, 0)

  return (
    <ul className="space-y-3.5">
      {reasons.map((reason) => {
        const look = voluntaryLook(reason.isVoluntary)
        const percent = share(reason.count, total)

        return (
          <li key={reason.reasonId ?? 'unspecified'}>
            <div className="flex items-center gap-2.5">
              <span className="min-w-0 flex-1 truncate-1 text-footnote" title={reason.reasonName}>
                {reason.reasonName}
              </span>

              <Badge tone={look.tone} size="sm">
                {t(look.labelKey)}
              </Badge>

              <span className="tabular w-6 text-right text-footnote font-semibold">
                {num(reason.count)}
              </span>

              {/*
                Foiz uchun QAT'IY kenglik: qatorlar bir-birining ostida
                turadi va o'ng chekka tekis bo'lmasa, ro'yxat titrab
                ko'rinardi («7,1%» va «21,4%» har xil kenglikda).
              */}
              <span className="tabular w-12 text-right text-caption1 text-[var(--fg-tertiary)]">
                {percentText(percent, num)}
              </span>
            </div>

            {/*
              Ustun uzunligi — BUTUN DAVRDAGI ulush, ya'ni yonidagi foizning
              o'zi.

              NEGA eng katta qatorga nisbatan normallashtirilmagan (unda
              birinchi ustun har doim to'la bo'lardi va taqqoslash
              osonlashardi): u holda ustun uzunligi yonidagi raqamga MOS
              KELMASDI — 21% ko'rsatib turgan qator butun kenglikni
              egallardi. Grafik raqamni takrorlashi kerak, u bilan
              bahslashmasligi. Qolaversa, hamma ustunning kaltaligi ham
              ma'lumot: sabablar tarqoq, bitta ustun sabab yo'q.
            */}
            <div
              aria-hidden
              className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[var(--bg-sunken)]"
            >
              <div
                className="h-full rounded-full bg-[var(--accent)]"
                style={{ width: `${percent}%` }}
              />
            </div>
          </li>
        )
      })}
    </ul>
  )
}
