import { Link } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'
import { ChevronRight, ShieldAlert } from 'lucide-react'

import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { tashkentParts } from '@/shared/lib/timezone'
import { todayIso } from '@/shared/lib/today'
import { Badge, buttonVariants } from '@/shared/ui'

import type { FormerEmployeeAlert } from '../api/attendance-api'
import { formatPassTime } from '../lib/attendance-presentation'
import { PassDirectionBadge } from './attendance-pass-photo-dialog'
import { EmployeeAttendanceLink } from './employee-attendance-link'

/**
 * «Ishdan ketgan xodim binoga kirdi» — modulning YAGONA to'liq kenglikdagi
 * qizil yuzasi (SPEC §8.4, §8.7). Statistika sahifasining eng tepasida,
 * diagramma kartasidan YUQORIDA turadi (egasining talabi — sahifa qayta
 * qurilganda ham saqlanib qoldi).
 *
 * ⚠️ YOPISH TUGMASI YO'Q VA BO'LMAYDI. Blok faqat asosidagi fakt yo'qolganda
 * — 24 soat davomida bunday o'tish bo'lmaganda — o'zi ketadi. «×» tugmasi
 * «binoda bo'lmasligi kerak bo'lgan odam bor» degan yagona ogohlantirishni
 * birinchi ko'rgan odamning bir bosishiga bog'lab qo'yardi.
 *
 * Tizim qurilmaga «kiritma» buyrug'ini yubormaydi (§2.13) — shu sababli
 * blok nima QILISH kerakligini ham aytadi, aks holda u foydasiz.
 *
 * `Alert` komponenti ishlatilmagan: u matnni `<p>` ichiga soladi, bu yerda
 * esa har bir odam uchun havolali qator kerak (`<p>` ichida ro'yxat HTML
 * qoidasini buzardi). Ranglar va ikonka joylashuvi `Alert` ning `danger`
 * ohangi bilan AYNAN bir xil.
 */
export function FormerEmployeeAlertPanel({
  alerts,
  canOpenEmployee,
}: {
  alerts: readonly FormerEmployeeAlert[]
  /**
   * Xodim sahifasi ochiladimi (`useCanOpenEmployeeAttendance`). Yo'q bo'lsa ism HAVOLA
   * EMAS — oddiy matn: 403 ga olib boradigan havola bajarilmaydigan va'da
   * bo'lardi (statistika sahifasidagi qoida).
   */
  canOpenEmployee: boolean
}) {
  const { t, num, dateTime } = useLocale()
  const { isAttendanceScoped } = useAuth()

  /*
    Oyna — so'nggi 24 SOAT, ya'ni o'tish kechagi bo'lishi mumkin: kechagi
    23:40 bugun «23:40» deb o'qilsa, HR uni hozirgina bo'lgan deb
    o'ylardi. Bugungisi — faqat soat (sana shovqin), boshqasi — sana bilan.
  */
  const today = todayIso()
  const passTime = (occurredAt: string): string => {
    const instant = new Date(occurredAt)
    if (Number.isNaN(instant.getTime())) return formatPassTime(occurredAt)

    const { year, month, day } = tashkentParts(instant)
    return `${year}-${month}-${day}` === today ? formatPassTime(instant) : dateTime(instant)
  }

  return (
    <section role="alert" className="rounded-lg bg-ios-red/10 p-4 sm:p-5">
      <div className="flex items-start gap-3">
        <ShieldAlert className="mt-px size-5 shrink-0 text-ios-red" strokeWidth={2.25} />
        <div className="min-w-0 space-y-1">
          <p className="text-callout font-semibold text-ios-red">
            {alerts.length === 1
              ? t('attendance.formerEmployeeAlertTitle')
              : t('attendance.formerEmployeeAlertTitlePlural', { n: num(alerts.length) })}
          </p>
          <p className="text-footnote text-[var(--fg-secondary)]">
            {t('attendance.formerEmployeeAlertHint')}
          </p>
        </div>
      </div>

      {/* Qatorlar oq — qizil fon ustida qatlam: har bir odam alohida o'qiladi */}
      <ul className="mt-4 space-y-2">
        {alerts.map((alert) => (
          <li
            key={alert.passId}
            className="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-md bg-[var(--bg-elevated)] p-3"
          >
            {canOpenEmployee && isAttendanceScoped ? (
              <EmployeeAttendanceLink
                employeeId={alert.employeeId}
                className="press flex min-w-[220px] flex-1 items-center gap-3 rounded-md"
              >
                <AlertPerson alert={alert} />
              </EmployeeAttendanceLink>
            ) : canOpenEmployee ? (
              <Link
                to="/xodimlar/$employeeId"
                params={{ employeeId: alert.employeeId }}
                className="press flex min-w-[220px] flex-1 items-center gap-3 rounded-md"
              >
                <AlertPerson alert={alert} />
              </Link>
            ) : (
              <span className="flex min-w-[220px] flex-1 items-center gap-3">
                <AlertPerson alert={alert} />
              </span>
            )}

            <span className="flex flex-wrap items-center gap-x-3 gap-y-1.5 text-footnote text-[var(--fg-secondary)]">
              <span className="tabular font-semibold text-[var(--fg)]">
                {passTime(alert.occurredAt)}
              </span>
              <PassDirectionBadge direction={alert.direction} />
              {alert.deviceName !== null && <span>{alert.deviceName}</span>}
              {alert.passCount > 1 && (
                <Badge tone="danger" size="sm">
                  {t('attendance.formerEmployeePassCount', { n: num(alert.passCount) })}
                </Badge>
              )}
            </span>

            <Link
              to="/verifikatsiya/otishlar"
              search={{ passId: alert.passId }}
              className={buttonVariants({ variant: 'secondary', size: 'sm' })}
            >
              {t('attendance.formerEmployeeOpenPass')}
              <ChevronRight />
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}

/** Qatordagi odam — rasm, ism, bo'shatilgan sana va firma (havola bo'lsa ham, bo'lmasa ham bir xil). */
function AlertPerson({ alert }: { alert: FormerEmployeeAlert }) {
  const { t, date } = useLocale()

  return (
    <>
      <EmployeePhoto
        employeeId={alert.employeeId}
        photoKey={alert.photoKey}
        name={alert.fullName}
        size="md"
      />
      <span className="min-w-0">
        <span className="truncate-1 block font-semibold">{alert.fullName}</span>
        <span className="truncate-1 block text-caption1 text-[var(--fg-secondary)]">
          {[
            alert.terminationDate === null
              ? null
              : t('attendance.formerEmployeeTerminatedOn', {
                  date: date(alert.terminationDate),
                }),
            alert.legalEntityName,
          ]
            .filter(Boolean)
            .join(' · ')}
        </span>
      </span>
    </>
  )
}
