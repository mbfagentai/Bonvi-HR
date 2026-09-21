import { Link } from '@tanstack/react-router'
import { Cake } from 'lucide-react'

/*
 * Rasm xodimlar modulidan OLINADI, bu yerda qayta yozilmaydi
 * (`birthdays-dialog.tsx` dagi bilan bir xil sabab): rasm `Authorization`
 * sarlavhasi bilan `blob` bo'lib so'raladi, kesh kaliti `photoKey` ga
 * bog'lanadi va kaliti yo'q xodim uchun so'rov umuman yuborilmaydi.
 */
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Card, IconTile, Skeleton } from '@/shared/ui'

import type { BirthdayFilters, UpcomingBirthday } from '../api/attendance-api'
import { useAttendanceBirthdays } from '../api/attendance-queries'
import { formatBirthday } from '../lib/attendance-presentation'

/** Skelet qatorlari — ro'yxatning odatiy uzunligi emas, ko'z ilg'aydigan qismi. */
const SKELETON_ROWS = [0, 1, 2]

/**
 * Yaqinlashib kelayotgan tug'ilgan kunlar — filtr ustunining ostidagi karta
 * (SPEC-panel §E).
 *
 * <b>Nega bu yerda.</b> Verifix'da ham shu joyda turadi va HR uni aynan
 * statistikani ochganda ko'radi: ekranda «bugun kim ishda» bilan yonma-yon
 * «kimni tabriklash kerak» degan savol turadi.
 *
 * <b>Ro'yxat FILTRGA bo'ysunadi</b> (§E2): tepadagi ustunda bo'lim
 * tanlansa, ro'yxat ham o'sha bo'limnikiga aylanadi. Davrga esa
 * bo'ysunmaydi — bu «keyingi navbatda kim» degan savol, tanlangan
 * oraliqning kesimi emas.
 *
 * <b>Tartib SERVERDA.</b> Klient qayta saralamaydi va keyingi sanani
 * o'zi hisoblamaydi: yil chegarasi va 29-fevral qoidasi bitta joyda
 * yashaydi (`AttendanceReadModel.NextBirthday`).
 */
export function BirthdaysCard({
  filters,
  enabled,
  canOpenEmployee,
}: {
  filters: BirthdayFilters
  enabled: boolean
  canOpenEmployee: boolean
}) {
  const { t } = useLocale()

  const query = useAttendanceBirthdays(filters, enabled)
  const items = query.data ?? []

  return (
    <Card className="space-y-3 p-4">
      <div className="flex items-center gap-2.5">
        <IconTile icon={Cake} tone="purple" size="sm" />
        <h2 className="text-subhead font-semibold">{t('attendance.birthdays')}</h2>
      </div>

      {query.isPending ? (
        /* Spinner emas, skelet: ro'yxat kelganda ustun sakramaydi. */
        <div aria-hidden className="space-y-3 py-0.5">
          {SKELETON_ROWS.map((row) => (
            <div key={row} className="flex items-center gap-3">
              <Skeleton className="size-8 rounded-full" />
              <div className="min-w-0 flex-1 space-y-1.5">
                <Skeleton className="h-3.5 w-28" />
                <Skeleton className="h-3 w-20" />
              </div>
            </div>
          ))}
        </div>
      ) : query.isError ? (
        /*
          Yon ustundagi xato — QISQA matn, `Alert` emas. Statistikaning
          o'zi ishlab turganda bu karta ekranni egallab olmasligi kerak:
          u yordamchi ma'lumot, ishning o'zi emas.
        */
        <p className="text-footnote text-[var(--fg-secondary)]">
          {t('attendance.birthdaysLoadFailed')}
        </p>
      ) : items.length === 0 ? (
        <p className="text-footnote text-[var(--fg-secondary)]">
          {t('attendance.birthdaysEmpty')}
        </p>
      ) : (
        <ul className="-mx-2 divide-y divide-[var(--border)]">
          {items.map((item) => (
            <BirthdayRow key={item.employeeId} item={item} linked={canOpenEmployee} />
          ))}
        </ul>
      )}
    </Card>
  )
}

/**
 * Bitta qator: rasm, ism, lavozim va o'ngda sana.
 *
 * Qatorning HAMMASI bitta nishon (rasm ham, ism ham) — iOS ro'yxatlaridagi
 * qoida. `Link`, tugma emas: bu shunchaki o'tish va «yangi oynada ochish»
 * ishlashi kerak (modaldagidan farqi — u yerda bosishda oyna ham yopilardi).
 *
 * Ruxsati yo'q foydalanuvchida qator BOSILMAYDI, lekin ko'rinadi: ism,
 * lavozim va sana panelning jadvalida allaqachon ochiq.
 */
function BirthdayRow({ item, linked }: { item: UpcomingBirthday; linked: boolean }) {
  const { t } = useLocale()

  /*
    «Bugun» va «Ertaga» — so'z bilan: yon ustunda aynan shu ikkisi
    harakatga chorlaydi va sanani o'qib, bugungi kun bilan solishtirishga
    majbur qilmaslik kerak. Qolganlari qisqa sana (`20.09`) — tizimning
    boshqa joylaridagi `dd.MM.yyyy` ning kesilgani, chunki yil bu yerda
    shovqin.
  */
  const day =
    item.daysUntil === 0
      ? t('attendance.birthdayToday')
      : item.daysUntil === 1
        ? t('attendance.birthdayTomorrow')
        : formatBirthday(item.nextBirthday)

  const content = (
    <>
      <EmployeePhoto
        employeeId={item.employeeId}
        photoKey={item.photoKey}
        name={item.fullName}
        size="sm"
      />

      <span className="min-w-0 flex-1">
        <span
          className={cn(
            'truncate-1 block text-footnote font-semibold',
            linked && 'text-[var(--accent-text)]',
          )}
        >
          {item.fullName}
        </span>

        {item.positionName !== null && (
          <span className="truncate-1 block text-caption1 text-[var(--fg-secondary)]">
            {item.positionName}
          </span>
        )}
      </span>

      <span
        className={cn(
          'tabular shrink-0 text-caption1',
          item.daysUntil === 0
            ? 'font-semibold text-ios-purple'
            : 'text-[var(--fg-tertiary)]',
        )}
      >
        {day}
      </span>
    </>
  )

  const className = 'flex w-full items-center gap-2.5 rounded-md px-2 py-2 text-left'

  return (
    <li>
      {linked ? (
        <Link
          to="/xodimlar/$employeeId"
          params={{ employeeId: item.employeeId }}
          className={cn(
            className,
            'press transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]',
          )}
        >
          {content}
        </Link>
      ) : (
        <span className={className}>{content}</span>
      )}
    </li>
  )
}
