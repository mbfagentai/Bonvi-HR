import { getRouteApi } from '@tanstack/react-router'
import { CalendarCheck } from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import {
  dateRangeToSearch,
  searchToDateRange,
  type DateRange,
} from '@/shared/lib/list-search'
import { PageHeader, SegmentedControl } from '@/shared/ui'

import { useAbsenceRequests, useScheduleChangeRequests } from '../api/attendance-queries'
import { ATTENDANCE_VIEW } from '../lib/attendance-presentation'
import { AbsenceRequestsList } from './absence-requests-list'
import { AttendanceNoAccess, AttendancePageTitle } from './attendance-manage-states'
import { ScheduleRequestsList } from './schedule-requests-list'

/** Sahifadagi ikki navbat — yo'qlik va jadval so'rovlari (SPEC-jadval-sorovi §C4). */
type RequestQueue = 'absence' | 'schedule'

/** Segment ustidagi sanoq uchun — faqat `total` kerak, qatorlar emas. */
const COUNT_PAGE = { page: 1, pageSize: 1 } as const

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/verifikatsiya/sorovlar')

/**
 * So'rovlar — butun tashkilot bo'yicha navbat (SPEC-so'rovlar §B5).
 *
 * Tepada ikki segment: «Yo'qlik» va «Jadval o'zgarishi» (SPEC-jadval-sorovi
 * §C4). Menyu nishoni IKKALASINI birga sanaydi, shu sababli har segment
 * yonida o'z kutayotganlari soni turadi — aks holda HR nishonda «3» ni
 * ko'rib, ochilgan «Yo'qlik» navbatini bo'sh topardi.
 *
 * Har navbat xodim kartasidagi ro'yxat bilan BITTA komponentdan
 * (<c>AbsenceRequestsList</c>, <c>ScheduleRequestsList</c>): faqat xodim
 * ustuni qo'shiladi. Ikkinchi nusxa yozilsa, ikki ekran bir xil so'rovni
 * har xil tartibda yoki har xil tugmalar bilan ko'rsatishi mumkin edi.
 */
export function AbsenceRequestsPage() {
  const { t, num } = useLocale()
  const { hasPermission } = useAuth()
  const canView = hasPermission(ATTENDANCE_VIEW)

  const [queue, setQueue] = useState<RequestQueue>('absence')

  /*
    So'rov davri oralig'i — manzilda va IKKALA navbatga umumiy: HR
    «oktabrdagi so'rovlar»ni ko'rib, segmentni almashtirsa, savol o'sha
    qoladi. Segment sanoqlari esa oraliqqa BOG'LANMAYDI — ular menyu
    nishonidagi bilan bir xil «hozir qaror kutayotganlar».
  */
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const range = searchToDateRange(search)

  // `replace` — filtrni aniqlashtirish alohida tarix qadami emas
  const changeRange = (next: DateRange) => {
    void navigate({ search: dateRangeToSearch(next), replace: true })
  }

  const absencePending = useAbsenceRequests({ status: 'Pending', ...COUNT_PAGE }, canView)
  const schedulePending = useScheduleChangeRequests({ status: 'Pending', ...COUNT_PAGE }, canView)

  if (!canView) {
    return <AttendanceNoAccess icon={CalendarCheck} title={t('attendance.requests')} />
  }

  const label = (text: string, count: number | undefined): string =>
    count === undefined || count === 0
      ? text
      : t('attendance.requestsSegmentCount', { label: text, n: num(count) })

  return (
    <div className="space-y-5">
      <PageHeader
        title={<AttendancePageTitle icon={CalendarCheck} title={t('attendance.requests')} />}
      />

      <SegmentedControl
        value={queue}
        onChange={setQueue}
        aria-label={t('attendance.requestsQueueSwitch')}
        options={[
          {
            value: 'absence',
            label: label(t('attendance.requestsQueueAbsence'), absencePending.data?.total),
          },
          {
            value: 'schedule',
            label: label(t('attendance.requestsQueueSchedule'), schedulePending.data?.total),
          },
        ]}
      />

      {queue === 'absence' ? (
        <AbsenceRequestsList
          showEmployee
          showFilters
          range={range}
          onRangeChange={changeRange}
        />
      ) : (
        <ScheduleRequestsList showEmployee range={range} onRangeChange={changeRange} />
      )}
    </div>
  )
}
