import { CalendarCheck } from 'lucide-react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { PageHeader } from '@/shared/ui'

import { ATTENDANCE_VIEW } from '../lib/attendance-presentation'
import { AbsenceRequestsList } from './absence-requests-list'
import { AttendanceNoAccess, AttendancePageTitle } from './attendance-manage-states'

/**
 * Yo'qlik so'rovlari — butun tashkilot bo'yicha navbat (SPEC-so'rovlar §B5).
 *
 * Xodim kartasidagi ro'yxat bilan BITTA komponentdan
 * (<c>AbsenceRequestsList</c>): faqat xodim ustuni qo'shiladi va filtr
 * olib tashlanadi. Ikkinchi nusxa yozilsa, ikki ekran bir xil so'rovni
 * har xil tartibda yoki har xil tugmalar bilan ko'rsatishi mumkin edi.
 */
export function AbsenceRequestsPage() {
  const { t } = useLocale()
  const { hasPermission } = useAuth()

  if (!hasPermission(ATTENDANCE_VIEW)) {
    return <AttendanceNoAccess icon={CalendarCheck} title={t('attendance.requestsTitle')} />
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title={<AttendancePageTitle icon={CalendarCheck} title={t('attendance.requestsTitle')} />}
      />

      <AbsenceRequestsList showEmployee />
    </div>
  )
}
