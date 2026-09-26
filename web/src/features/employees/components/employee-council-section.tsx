import { CircleDot, CircleSlash } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Card, CardContent, CardHeader, IconTile } from '@/shared/ui'

import type { EmployeeStageEntry } from '../api/employees-api'
import { useEmployeeStages } from '../api/employees-queries'

/*
 * ⭐ «Natija» yorlig'i OLIB TASHLANDI (2026-09-16, foydalanuvchi
 * qarori). Ilgari har qatorda yashil «o'tdi» / qizil «o'tmadi» /
 * sariq «kutilmoqda» turardi.
 *
 * Sabab faqat soddalik emas: arxivdagi yorliqni ETL Excel MATNIDAN
 * taxmin qilib qo'ygan edi va u ko'p joyda xulosaga zid chiqardi —
 * «o'tdi» deb turgan qatorning xulosasi aslida rad etilgani haqida
 * gapirardi. Xulosa MATNI esa o'z o'rnida qoladi va u yagona ishonchli
 * manba.
 */

/**
 * ⭐ «Kengash xulosasi» — nomzodlik davridagi bosqich xulosalari.
 *
 * <b>FAQAT O'QISH, va bu foydalanuvchining aniq qarori.</b> Bu yerda
 * yangi yozuv qo'shib bo'lmaydi, chunki xulosa NOMZODNI baholaydi va u
 * rekruting voronkasida — odam hali xodim bo'lmagan paytda — yoziladi.
 * Xodim kartasidan ham yozish mumkin bo'lsa, tizimda ikkita manba paydo
 * bo'lardi (voronkadagi haqiqiy tarix va kartada keyin yozilgani) va
 * qaysi biri to'g'ri ekanini hech kim ayta olmasdi.
 *
 * <b>Yozuv bo'lmasa bo'lim UMUMAN chizilmaydi</b> (2026-09-26,
 * foydalanuvchi qarori). Ilgari bo'sh holat sababini aytib turardi
 * («rekruting orqali qo'shilmagan»), lekin 2347 xodimning deyarli
 * hammasi Excel'dan ko'chirilgan va bu karta ko'pchilik profilda bo'sh
 * joy egallab turardi. Yuklanish paytida ham chizilmaydi — aks holda
 * skelet chiqib, keyin yo'qolib, sahifa sakrardi.
 *
 * Ko'rinishi — vertikal lenta, `employee-timeline-tab.tsx` naqshi:
 * ikona, chap chiziq va o'ngda mazmun. Ikkalasi ham bir xil savolga
 * javob beradi («shu odam bilan nima bo'ldi»), shu sababli bir xil
 * o'qilishi kerak.
 */
export function EmployeeCouncilSection({ employeeId }: { employeeId: string }) {
  const { t } = useLocale()

  const stages = useEmployeeStages(employeeId)
  const items = stages.data ?? []

  if (items.length === 0) return null

  return (
    <Card className="lg:col-span-5">
      <CardHeader title={t('employees.councilTitle')} subtitle={t('employees.councilHint')} />
      <CardContent>
        <ol className="relative">
          {/* Vertikal chiziq — ikonalar markazidan o'tadi (lenta naqshi) */}
          <span aria-hidden className="absolute top-2 bottom-2 left-5 w-px bg-[var(--border)]" />

          {items.map((entry) => (
            <StageRow key={entry.id} entry={entry} />
          ))}
        </ol>
      </CardContent>
    </Card>
  )
}

function StageRow({ entry }: { entry: EmployeeStageEntry }) {
  const { t, date } = useLocale()
  return (
    <li className="relative flex gap-4 pb-6 last:pb-1">
      {/* Ikona chiziq ustida turadi — atrofidagi halqa fon rangida */}
      <span className="relative z-10 rounded-md ring-4 ring-[var(--bg-elevated)]">
        <IconTile icon={CircleDot} tone="gray" />
      </span>

      <div className="min-w-0 flex-1 pt-0.5">
        <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
          {/*
            Bosqich nomi ma'lumotnomadan o'chirilgan bo'lishi mumkin —
            o'shanda xulosa matni baribir qoladi va u yozuvning eng
            qimmatli qismi. Nom o'rniga neytral belgi qo'yiladi.
          */}
          <span className="text-subhead font-semibold">
            {entry.stage ?? t('employees.councilUnknownStage')}
          </span>
        </div>

        <p className="tabular mt-1 text-caption1 text-[var(--fg-secondary)]">
          {date(entry.date)}
        </p>

        {entry.conclusion === null ? (
          /*
            Xulosasiz yozuv — bu «kutilmoqda» holatining odatiy ko'rinishi
            (suhbat boshlangan, natija hali yozilmagan). Qatorni umuman
            chizmaslik esa lentada tushunarsiz bo'shliq qoldirardi.
          */
          <p className="mt-2 flex items-center gap-1.5 text-footnote text-[var(--fg-tertiary)]">
            <CircleSlash className="size-3.5 shrink-0" strokeWidth={2} />
            {t('employees.councilNoConclusion')}
          </p>
        ) : (
          <p className="mt-2 rounded-md bg-[var(--bg-sunken)] px-3 py-2 text-footnote whitespace-pre-line text-[var(--fg-secondary)]">
            {entry.conclusion}
          </p>
        )}

        {entry.recordedByName !== null && (
          <p className="mt-1.5 text-caption2 text-[var(--fg-tertiary)]">
            {t('employees.councilRecordedBy', { name: entry.recordedByName })}
          </p>
        )}
      </div>
    </li>
  )
}
