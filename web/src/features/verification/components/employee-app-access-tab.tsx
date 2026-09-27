import { Link } from '@tanstack/react-router'
import { Check, Copy, Eye, EyeOff, KeyRound, LoaderCircle, PenLine, TriangleAlert } from 'lucide-react'
import { Fragment, useEffect, useRef, useState, type ReactNode } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { formatPhone } from '@/shared/lib/phone'
import { Alert, Button, Dialog, Skeleton, Switch, TextField } from '@/shared/ui'

import {
  useEmployeeAppAccess,
  useEmployeePassword,
  useRegenerateEmployeePassword,
  useSetEmployeePassword,
  useSetMobileCheckInAllowed,
  type EmployeeAppAccess,
} from '../api/employee-app-access'

/** «Nusxalandi» belgisi qancha turadi — qurilma tokeni oynasidagi bilan bir xil. */
const COPIED_MS = 2000

/** HR yozadigan parolning chegaralari — server qoidasi (`EmployeePassword`) bilan bir xil. */
const PASSWORD_MIN = 6
const PASSWORD_MAX = 64

/** Yashirilgan parolning o'rnidagi nuqtalar — uzunligi ATAYLAB doimiy (parol uzunligini aytmaydi). */
const MASK = '••••••'

/**
 * Xodim kartasi «Davomat» tabidagi «Ilova» bo'limi (SPEC-xodim-ilovasi §F).
 *
 * Egasining qarori (2026-09-27): hisob har bir xodimga AVTOMATIK ochiladi
 * (server bo'limni ochgan lahzada ham kafolatlaydi), login — profildagi
 * asosiy telefon, parolni faqat HR beradi va HAR DOIM ko'ra oladi. Shu
 * sababli bu yerda «Hisob ochish», «Hisobni yopish» va bir martalik parol
 * oynasi YO'Q — yagona o'chirgich ishdan ketish.
 *
 * ⚠️ Faqat `attendance.manage` egasiga chiziladi — segmentning o'zi ham
 * (`employee-attendance-tab.tsx`). Server hamma endpointni shu ruxsat bilan
 * qo'riqlaydi.
 *
 * ⚠️ Ochiq parol faqat ko'z tugmasi bosilganda ALOHIDA so'rov bilan keladi
 * va yopilishi bilan keshdan o'chadi (`useEmployeePassword`).
 */
export function EmployeeAppAccessTab({
  employeeId,
  canOpenEmployee,
}: {
  employeeId: string
  /** Takror raqamli boshqa xodimning ismi havola bo'ladimi (`employees.view`). */
  canOpenEmployee: boolean
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const query = useEmployeeAppAccess(employeeId)

  if (query.isPending) {
    return <Skeleton className="h-48 w-full" />
  }

  if (query.isError) {
    return (
      <Alert tone="danger" title={t('employees.loadFailed')}>
        {errorMessage(problemCode(query.error), t('dashboard.loadFailedHint'))}
      </Alert>
    )
  }

  return <AppAccessBody employeeId={employeeId} access={query.data} canOpenEmployee={canOpenEmployee} />
}

type DialogKind = 'generate' | 'custom' | null

function AppAccessBody({
  employeeId,
  access,
  canOpenEmployee,
}: {
  employeeId: string
  access: EmployeeAppAccess
  canOpenEmployee: boolean
}) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const toggle = useSetMobileCheckInAllowed(employeeId)

  const [revealed, setRevealed] = useState(false)
  const [dialog, setDialog] = useState<DialogKind>(null)

  const account = access.account
  const phone = access.login === null ? null : (formatPhone(access.login) ?? access.login)

  // Parol ko'rsatilgach yangi berilsa — yangi parol darhol ko'rinadi: HR uni
  // xodimga aytish uchun yasadi.
  const assigned = () => {
    setDialog(null)
    setRevealed(true)
  }

  return (
    <div className="space-y-4">
      <LoginIssueAlert access={access} canOpenEmployee={canOpenEmployee} />

      <dl className="divide-y divide-[var(--border)] overflow-hidden rounded-md bg-[var(--bg-sunken)]">
        <Row label={t('attendance.appLogin')}>
          {phone === null ? (
            <span className="text-[var(--fg-tertiary)]">—</span>
          ) : (
            <span className="tabular font-medium">{phone}</span>
          )}
          <span className="mt-0.5 block text-caption1 text-[var(--fg-tertiary)]">
            {t('attendance.appLoginNote')}
          </span>
        </Row>

        <Row label={t('attendance.appPassword')}>
          {account.passwordAvailable ? (
            <PasswordValue employeeId={employeeId} revealed={revealed} onReveal={setRevealed} />
          ) : (
            <span className="block">
              <span className="block font-medium">{t('attendance.appPasswordUnavailable')}</span>
              <span className="mt-0.5 block text-caption1 text-[var(--fg-tertiary)]">
                {t('attendance.appPasswordUnavailableHint')}
              </span>
            </span>
          )}
        </Row>

        <Row label={t('attendance.appPasswordSetAt')}>{dateTime(account.passwordSetAt)}</Row>

        <Row label={t('attendance.appLastLogin')}>
          {account.lastLoginAt === null ? t('attendance.appNeverSignedIn') : dateTime(account.lastLoginAt)}
        </Row>
      </dl>

      <div className="flex flex-wrap items-center gap-2">
        <Button type="button" size="sm" variant="soft" pill onClick={() => setDialog('generate')}>
          <KeyRound />
          {t('attendance.appGenerate')}
        </Button>
        <Button type="button" size="sm" variant="ghost" pill onClick={() => setDialog('custom')}>
          <PenLine />
          {t('attendance.appCustom')}
        </Button>
      </div>

      {/*
        O'chirgich hisobdan MUSTAQIL: ruxsat xodimga beriladi. Sukut bo'yicha
        hammada o'chiq — avtomatik hisob ochish uni yoqmaydi.
      */}
      <div className="border-t border-[var(--border)] pt-3">
        <Switch
          checked={access.mobileCheckInAllowed}
          disabled={toggle.isPending}
          onChange={(allowed) => toggle.mutate(allowed)}
          label={t('attendance.appMobileCheckIn')}
          hint={t('attendance.appMobileCheckInHint')}
        />
        {toggle.isError && (
          <div className="mt-2">
            <Alert tone="danger">{errorMessage(problemCode(toggle.error))}</Alert>
          </div>
        )}
      </div>

      <p className="text-caption1 text-[var(--fg-tertiary)]">{t('attendance.appHint')}</p>

      <GenerateDialog
        open={dialog === 'generate'}
        employeeId={employeeId}
        onClose={() => setDialog(null)}
        onDone={assigned}
      />
      <CustomPasswordDialog
        open={dialog === 'custom'}
        employeeId={employeeId}
        onClose={() => setDialog(null)}
        onDone={assigned}
      />
    </div>
  )
}

/**
 * Nega bu raqam bilan kirib bo'lmaydi — faqat muammo bo'lsa.
 *
 * Kirish endpointi buni oshkor qilmaydi (umumiy «telefon yoki parol noto'g'ri»
 * javobi), ya'ni xodimga «nega kira olmayapman» deb faqat HR javob bera oladi —
 * va shu yerda.
 */
function LoginIssueAlert({
  access,
  canOpenEmployee,
}: {
  access: EmployeeAppAccess
  canOpenEmployee: boolean
}) {
  const { t } = useLocale()

  if (access.loginIssue === 'NoPhone') {
    return (
      <Alert tone="warning" title={t('attendance.appNoPhone')}>
        {t('attendance.appNoPhoneText')}
      </Alert>
    )
  }

  if (access.loginIssue !== 'SharedPhone') return null

  /*
    Ism(lar) tarjima matnining O'RTASIDA turadi va havola bo'lishi mumkin.
    Matn `{{name}}` belgisi bilan butunligicha tarjima qilinadi — tartib
    tilga qarab o'zgarsa ham to'g'ri turadi — keyin shu belgi bo'yicha
    bo'linib, o'rniga havolalar qo'yiladi.
  */
  const marker = '\u0000'
  const [before, after] = t('attendance.appSharedPhone', { name: marker }).split(marker)

  const names = access.sharedWith.map((holder, index) => (
    <Fragment key={holder.employeeId}>
      {index > 0 && ', '}
      {canOpenEmployee ? (
        <Link
          to="/xodimlar/$employeeId"
          params={{ employeeId: holder.employeeId }}
          className="font-semibold text-[var(--accent)] hover:underline"
        >
          {holder.fullName}
        </Link>
      ) : (
        <span className="font-semibold">{holder.fullName}</span>
      )}
    </Fragment>
  ))

  return (
    <Alert tone="warning">
      <span className="inline-flex items-start gap-1.5">
        <TriangleAlert aria-hidden className="mt-0.5 size-4 shrink-0" />
        <span>
          {before}
          {names}
          {after}
        </span>
      </span>
      <span className="mt-1 block text-footnote opacity-80">{t('attendance.appSharedPhoneText')}</span>
    </Alert>
  )
}

/**
 * Parol qatori: yashirin — nuqtalar; ko'z bosilsa — alohida so'rov bilan ochiq
 * matn va nusxalash.
 *
 * `navigator.clipboard` faqat xavfsiz kontekstda ishlaydi — tizim oddiy HTTP
 * da ochilsa tugma ishlamaydi, u holda matn o'zi BELGILANADI va HR Ctrl+C
 * bilan oladi (`attendance-device-token-dialog.tsx` dagi bilan bir xil).
 */
function PasswordValue({
  employeeId,
  revealed,
  onReveal,
}: {
  employeeId: string
  revealed: boolean
  onReveal: (value: boolean) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const password = useEmployeePassword(employeeId, revealed)
  const inputRef = useRef<HTMLInputElement>(null)
  const [copy, setCopy] = useState<'idle' | 'copied' | 'failed'>('idle')

  useEffect(() => {
    if (copy !== 'copied') return
    const timer = window.setTimeout(() => setCopy('idle'), COPIED_MS)
    return () => window.clearTimeout(timer)
  }, [copy])

  const value = revealed ? (password.data?.password ?? null) : null

  const copyValue = async () => {
    if (value === null) return

    try {
      if (navigator.clipboard === undefined) throw new Error('clipboard')
      await navigator.clipboard.writeText(value)
      setCopy('copied')
    } catch {
      inputRef.current?.focus()
      inputRef.current?.select()
      setCopy('failed')
    }
  }

  return (
    <span className="block">
      <span className="flex items-center gap-1.5">
        {revealed && password.isPending ? (
          <Skeleton className="h-6 w-24" />
        ) : (
          <input
            ref={inputRef}
            readOnly
            value={value ?? MASK}
            spellCheck={false}
            aria-label={t('attendance.appPassword')}
            onFocus={(event) => {
              if (value !== null) event.currentTarget.select()
            }}
            className={cn(
              'min-w-0 flex-1 bg-transparent font-mono text-callout tracking-wider text-[var(--fg)] outline-none',
              value === null && 'text-[var(--fg-secondary)]',
            )}
          />
        )}

        <Button
          type="button"
          size="icon-sm"
          variant="ghost"
          onClick={() => {
            setCopy('idle')
            onReveal(!revealed)
          }}
          aria-label={revealed ? t('attendance.appHidePassword') : t('attendance.appShowPassword')}
          title={revealed ? t('attendance.appHidePassword') : t('attendance.appShowPassword')}
        >
          {revealed ? <EyeOff /> : <Eye />}
        </Button>

        <Button
          type="button"
          size="icon-sm"
          variant="ghost"
          disabled={value === null}
          onClick={() => void copyValue()}
          aria-label={t('attendance.appCopy')}
          title={copy === 'copied' ? t('attendance.copied') : t('attendance.appCopy')}
        >
          {copy === 'copied' ? <Check /> : <Copy />}
        </Button>
      </span>

      {revealed && password.isError && (
        <span role="alert" className="mt-1 block text-footnote text-ios-red">
          {problemCode(password.error) === 'xodim.parol_korinmaydi'
            ? t('attendance.appPasswordUnavailableHint')
            : errorMessage(problemCode(password.error))}
        </span>
      )}

      {copy === 'failed' && (
        <span role="alert" className="mt-1 block text-footnote text-ios-orange">
          {t('attendance.copyFailed')}
        </span>
      )}
    </span>
  )
}

/** Tasdiq: yangi tasodifiy 6 xonali parol — eski parol va sessiyalar to'xtaydi. */
function GenerateDialog({
  open,
  employeeId,
  onClose,
  onDone,
}: {
  open: boolean
  employeeId: string
  onClose: () => void
  onDone: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const generate = useRegenerateEmployeePassword(employeeId)

  useEffect(() => {
    if (open) generate.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!generate.isPending}
      title={t('attendance.appGenerate')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={generate.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={generate.isPending}
            onClick={() => generate.mutate(undefined, { onSuccess: onDone })}
          >
            {generate.isPending && <LoaderCircle className="animate-spin" />}
            {t('attendance.appGenerateAction')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">{t('attendance.appGenerateConfirm')}</p>
        {generate.isError && <Alert tone="danger">{errorMessage(problemCode(generate.error))}</Alert>}
      </div>
    </Dialog>
  )
}

/**
 * HR o'zi yozgan parol — 6–64 belgi.
 *
 * Tekshiruv serverdagi bilan bir xil (chetdagi bo'shliqlar hisobga olinmaydi),
 * lekin yakuniy so'z serverniki: uning xatosi ham shu yerda ko'rinadi.
 */
function CustomPasswordDialog({
  open,
  employeeId,
  onClose,
  onDone,
}: {
  open: boolean
  employeeId: string
  onClose: () => void
  onDone: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSetEmployeePassword(employeeId)
  const [value, setValue] = useState('')
  const [touched, setTouched] = useState(false)

  useEffect(() => {
    if (!open) return
    setValue('')
    setTouched(false)
    save.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  const trimmed = value.trim()
  const localError =
    trimmed.length === 0
      ? errorMessage('xodim.parol_bosh')
      : trimmed.length < PASSWORD_MIN
        ? errorMessage('xodim.parol_qisqa')
        : trimmed.length > PASSWORD_MAX
          ? errorMessage('xodim.parol_uzun')
          : undefined

  const submit = () => {
    setTouched(true)
    if (localError !== undefined) return
    save.mutate(trimmed, { onSuccess: onDone })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!save.isPending}
      title={t('attendance.appCustom')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={save.isPending}>
            {t('common.cancel')}
          </Button>
          <Button type="button" variant="primary" disabled={save.isPending} onClick={submit}>
            {save.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        <TextField
          label={t('attendance.appNewPassword')}
          hint={t('attendance.appNewPasswordHint')}
          error={touched ? localError : undefined}
          value={value}
          maxLength={PASSWORD_MAX + 10}
          autoComplete="off"
          spellCheck={false}
          autoFocus
          onChange={(event) => setValue(event.target.value)}
          inputClassName="font-mono tracking-wider"
        />
        {save.isError && <Alert tone="danger">{errorMessage(problemCode(save.error))}</Alert>}
      </form>
    </Dialog>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid gap-1 px-3.5 py-3 sm:grid-cols-[160px_minmax(0,1fr)] sm:items-center sm:gap-4">
      <dt className="text-footnote text-[var(--fg-secondary)]">{label}</dt>
      <dd className="min-w-0 text-subhead">{children}</dd>
    </div>
  )
}
