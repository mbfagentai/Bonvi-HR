import { Link } from '@tanstack/react-router'
import type { MouseEventHandler, ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { EMPLOYEES_VIEW } from '@/features/employees/lib/employee-presentation'

/**
 * Whether a name in the attendance lists opens anything: the employee card for HR, the
 * attendance-only page for a «Davomat mas'uli».
 */
export function useCanOpenEmployeeAttendance(): boolean {
  const { hasPermission, isAttendanceScoped } = useAuth()

  return isAttendanceScoped || hasPermission(EMPLOYEES_VIEW)
}

/**
 * A link to one employee's attendance. A scoped user never gets a link to `/xodimlar/**`: the
 * server refuses it and it holds personal data, so their target is decided here, once.
 */
export function EmployeeAttendanceLink({
  employeeId,
  year,
  month,
  className,
  onClick,
  children,
}: {
  employeeId: string
  /** The month to open, always together with `year`; omitted means the current month. */
  year?: number
  month?: number
  className?: string
  onClick?: MouseEventHandler<HTMLAnchorElement>
  children: ReactNode
}) {
  const { isAttendanceScoped } = useAuth()

  return isAttendanceScoped ? (
    <Link
      to="/verifikatsiya/xodim/$employeeId"
      params={{ employeeId }}
      search={{ year, month }}
      className={className}
      onClick={onClick}
    >
      {children}
    </Link>
  ) : (
    <Link
      to="/xodimlar/$employeeId"
      params={{ employeeId }}
      search={{ tab: 'attendance', year, month }}
      className={className}
      onClick={onClick}
    >
      {children}
    </Link>
  )
}
