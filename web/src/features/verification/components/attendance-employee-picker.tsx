import { Search, UserRound } from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useEmployees } from '@/features/employees/api/employees-queries'
import type { EmployeeListItem } from '@/features/employees/api/employees-api'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { STATUS_KEY, STATUS_TONE } from '@/features/employees/lib/employee-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useSearchDraft } from '@/shared/lib/list-search'
import { Alert, Badge, Button, Input, Skeleton } from '@/shared/ui'

import { useAttendanceEmployeeLookup } from '../api/attendance-queries'

/**
 * Tanlangan xodim — ro'yxat qatoridan KERAKLI qismi.
 *
 * NEGA faqat `id` emas: qidiruv matni o'zgarsa tanlangan odam
 * natijalardan chiqib ketadi, lekin uning YUZI va ismi ekranda qolishi
 * kerak — operator kimni tanlaganini qidiruvni qaytarmasdan ko'rsin.
 */
export interface PickedEmployee {
  id: string
  fullName: string
  photoKey: string | null
  position: string | null
  department: string | null
}

/** One result row, whichever route it came from. */
interface PickerRow extends PickedEmployee {
  terminated: boolean
}

export function toPickedEmployee(item: EmployeeListItem): PickedEmployee {
  return {
    id: item.id,
    fullName: item.fullName,
    photoKey: item.photoKey,
    position: item.positionName,
    department: item.departmentName,
  }
}

/**
 * The lookup has no photo key, only `hasPhoto`. The hook still needs a non-null key to send the
 * request, and an image never changes under it within one search session.
 */
const LOOKUP_PHOTO_KEY = 'attendance-lookup'

/** Bir so'rovda ko'rsatiladigan qatorlar — qolgani qidiruv bilan toraytiriladi. */
const PICKER_PAGE_SIZE = 20

/**
 * Xodim tanlash — RASM va ism bilan, hech qachon faqat ism bilan emas.
 *
 * ⭐ NEGA oddiy `<select>` emas (hodisalar formasidagi kabi): egasining
 * talabi — turniket raqami XODIMGA bog'lanadi va xato bog'lanish oylab
 * tabelni buzadi (qaytarish qayta hisoblashni talab qiladi, SPEC §4.3).
 * Ikki kishining ismi bir xil bo'lishi mumkin; ro'yxatda ikkita bir xil
 * satr ko'rgan operator taxmin qiladi, ikkita YUZ ko'rgan operator esa
 * taniydi. Shu sababli har bir qatorda rasm, lavozim va bo'lim turadi.
 *
 * Ishdan ketganlar ham ro'yxatda (holat filtri yo'q) va bu ataylab:
 * Verifix ro'yxatida ketgan odamlar ham bor va ularning ESKI o'tishlari
 * aynan shu bog'lanish orqali tabelga tushadi. Ular kulrang «Ishdan
 * ketgan» yorlig'i bilan ajralib turadi.
 *
 * Qidiruv serverda (`GET /api/employees?search=`) — kirill/lotin
 * yozuvlarini tenglashtirish qoidalari u yerda, klientda ikkinchi nusxasi
 * yo'q.
 */
export function AttendanceEmployeePicker({
  value,
  onChange,
  label,
  error,
  disabled = false,
  employedOnly = false,
}: {
  value: PickedEmployee | null
  onChange: (next: PickedEmployee | null) => void
  label?: string
  /** Tayyor, tarjima qilingan matn. */
  error?: string
  disabled?: boolean
  /** Faqat ishlayotganlar — ishdan ketgan odamni tanlash ma'nosiz bo'lgan joyda (bo'lim boshlig'i). */
  employedOnly?: boolean
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { isAttendanceScoped } = useAuth()

  const [search, setSearch] = useState('')
  // Har harfda so'rov ketmaydi — boshqa ro'yxatlar bilan bir xil 350 ms
  const [draft, setDraft] = useSearchDraft(search, setSearch)

  const list = useEmployees(
    {
      page: 1,
      pageSize: PICKER_PAGE_SIZE,
      sort: 'fullName',
      search: search.trim() === '' ? undefined : search.trim(),
      ...(employedOnly ? { status: 'employed' as const } : {}),
    },
    // Tanlov qilingach ro'yxat yashiriladi — so'rov ham to'xtaydi
    value === null && !disabled && !isAttendanceScoped,
  )
  // A «Davomat mas'uli» searches through attendance: `GET /api/employees` is closed to them
  const lookup = useAttendanceEmployeeLookup(
    search.trim(),
    PICKER_PAGE_SIZE,
    value === null && !disabled && isAttendanceScoped,
  )

  const heading = label ?? t('attendance.employee')

  if (value !== null) {
    return (
      <div className="space-y-2">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">{heading}</p>
        <div className="flex items-center gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
          <EmployeePhoto
            employeeId={value.id}
            photoKey={value.photoKey}
            name={value.fullName}
            size="lg"
          />
          <div className="min-w-0 flex-1">
            <p className="truncate-1 font-medium">{value.fullName}</p>
            <p className="truncate-1 text-footnote text-[var(--fg-secondary)]">
              {[value.position, value.department].filter(Boolean).join(' · ') || '—'}
            </p>
          </div>
          {!disabled && (
            <Button type="button" variant="ghost" size="sm" onClick={() => onChange(null)}>
              {t('attendance.chooseAnother')}
            </Button>
          )}
        </div>
        {error !== undefined && (
          <p role="alert" className="text-footnote text-ios-red">
            {error}
          </p>
        )}
      </div>
    )
  }

  const source = isAttendanceScoped ? lookup : list
  const items: PickerRow[] = isAttendanceScoped
    ? (lookup.data ?? []).map((item) => ({
        id: item.id,
        fullName: item.fullName,
        photoKey: item.hasPhoto ? LOOKUP_PHOTO_KEY : null,
        position: item.positionName,
        department: item.departmentName,
        terminated: false,
      }))
    : (list.data?.items ?? []).map((item) => ({
        ...toPickedEmployee(item),
        terminated: item.status === 'terminated',
      }))
  // The lookup returns a bare list, so it cannot say how many more there are
  const total = isAttendanceScoped ? items.length : (list.data?.total ?? 0)
  const hidden = Math.max(0, total - items.length)

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">{heading}</p>

      <Input
        icon={Search}
        shape="pill"
        type="search"
        value={draft}
        disabled={disabled}
        onChange={(event) => setDraft(event.target.value)}
        placeholder={t('attendance.employeeSearchPlaceholder')}
        aria-label={heading}
      />

      <div
        className={cn(
          'max-h-72 overflow-y-auto rounded-lg border bg-[var(--bg-elevated)]',
          error !== undefined ? 'border-ios-red' : 'border-[var(--border)]',
        )}
      >
        {source.isPending ? (
          <div className="space-y-1 p-2" aria-busy>
            {Array.from({ length: 4 }, (_, index) => (
              <div key={`skeleton-${index}`} className="flex items-center gap-3 px-2 py-2">
                <Skeleton className="size-10 rounded-full" />
                <div className="flex-1 space-y-2">
                  <Skeleton className="h-3.5 w-48" />
                  <Skeleton className="h-3 w-32" />
                </div>
              </div>
            ))}
          </div>
        ) : source.isError ? (
          <div className="p-3">
            <Alert tone="danger">{errorMessage(problemCode(source.error))}</Alert>
          </div>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center gap-2 px-4 py-8 text-center">
            <UserRound className="size-6 text-[var(--fg-tertiary)]" strokeWidth={1.8} />
            <p className="text-footnote text-[var(--fg-secondary)]">{t('common.empty')}</p>
          </div>
        ) : (
          <ul className="divide-y divide-[var(--border)]" role="listbox" aria-label={heading}>
            {items.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  role="option"
                  aria-selected={false}
                  disabled={disabled}
                  onClick={() =>
                    onChange({
                      id: item.id,
                      fullName: item.fullName,
                      photoKey: item.photoKey,
                      position: item.position,
                      department: item.department,
                    })
                  }
                  className={cn(
                    'press flex w-full items-center gap-3 px-3 py-2.5 text-left',
                    'hover:bg-[var(--bg-hover)] active:bg-[var(--bg-active)]',
                    'disabled:pointer-events-none disabled:opacity-50',
                  )}
                >
                  <EmployeePhoto
                    employeeId={item.id}
                    photoKey={item.photoKey}
                    name={item.fullName}
                    size="md"
                  />
                  <span className="min-w-0 flex-1">
                    <span className="truncate-1 block text-subhead font-medium">
                      {item.fullName}
                    </span>
                    <span className="truncate-1 block text-caption1 text-[var(--fg-secondary)]">
                      {[item.position, item.department].filter(Boolean).join(' · ') || '—'}
                    </span>
                  </span>
                  {/* Faqat «ishlamaydi» belgilanadi — faol xodim shovqinsiz */}
                  {item.terminated && (
                    <Badge tone={STATUS_TONE.terminated} size="sm">
                      {t(STATUS_KEY.terminated)}
                    </Badge>
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {error !== undefined ? (
        <p role="alert" className="text-footnote text-ios-red">
          {error}
        </p>
      ) : (
        hidden > 0 && (
          <p className="text-footnote text-[var(--fg-tertiary)]">
            {t('attendance.pickerMore', { n: num(hidden) })}
          </p>
        )
      )}
    </div>
  )
}
