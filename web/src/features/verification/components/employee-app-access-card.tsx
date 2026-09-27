import { Check, Copy, KeyRound, LoaderCircle, Smartphone, UserPlus } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { formatPhone } from '@/shared/lib/phone'
import {
  Alert,
  Badge,
  Button,
  Card,
  Dialog,
  IconTile,
  Skeleton,
  Switch,
  TextField,
  type BadgeTone,
} from '@/shared/ui'

import {
  useEmployeeAppAccess,
  useIssueEmployeeAccount,
  useResetEmployeeAccountPassword,
  useSetEmployeeAccountActive,
  useSetMobileCheckInAllowed,
  type EmployeeAccount,
  type EmployeeAccountSecret,
} from '../api/employee-app-access'
import { ATTENDANCE_MANAGE } from '../lib/attendance-presentation'

/** «Nusxalandi» belgisi qancha turadi — qurilma tokeni oynasidagi bilan bir xil. */
const COPIED_MS = 2000

/**
 * Xodim kartasidagi «Ilova» bo'limi (SPEC-xodim-ilovasi §F).
 *
 * Uchta ish, boshqa hech narsa: ilova hisobini ochish yoki parolini tiklash
 * (bir martalik parol BIR MARTA ko'rsatiladi — qurilma tokeni kabi),
 * hisobni yopish/qaytarish va «Telefondan belgilash» o'chirgichi.
 *
 * ⚠️ Butun karta faqat `attendance.manage` egasiga chiziladi: server bu
 * endpointlarning hammasini shu ruxsat bilan qo'riqlaydi, va ruxsatsiz
 * foydalanuvchiga «403» bilan to'la karta ko'rsatishning ma'nosi yo'q.
 *
 * ⚠️ Xodim hisoblari tizim foydalanuvchilari EMAS (`users`) va rollar
 * muharririda ko'rinmaydi — ularni faqat shu karta boshqaradi.
 */
export function EmployeeAppAccessCard({ employeeId }: { employeeId: string }) {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(ATTENDANCE_MANAGE)

  if (!canManage) return null

  return <AppAccessCard employeeId={employeeId} />
}

type Confirm = 'reset' | 'deactivate' | 'activate' | null

function AppAccessCard({ employeeId }: { employeeId: string }) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()

  const query = useEmployeeAppAccess(employeeId)
  const toggle = useSetMobileCheckInAllowed(employeeId)

  const [issueOpen, setIssueOpen] = useState(false)
  const [confirm, setConfirm] = useState<Confirm>(null)
  const [secret, setSecret] = useState<EmployeeAccountSecret | null>(null)

  const access = query.data ?? null
  const account = access?.account ?? null

  return (
    <Card className="space-y-3 p-4">
      <div className="flex flex-wrap items-center gap-2.5">
        <IconTile icon={Smartphone} tone="blue" size="sm" />
        <h2 className="min-w-0 flex-1 truncate-1 text-subhead font-semibold">
          {t('attendance.appAccessTitle')}
        </h2>
        {access !== null && <AccountStatusBadge account={account} />}
      </div>

      {query.isPending ? (
        <Skeleton className="h-16 w-full" />
      ) : query.isError || access === null ? (
        <Alert tone="danger" title={t('employees.loadFailed')}>
          {errorMessage(problemCode(query.error), t('dashboard.loadFailedHint'))}
        </Alert>
      ) : (
        <>
          {account === null ? (
            <div className="flex flex-wrap items-center gap-3">
              <p className="min-w-0 flex-1 text-footnote text-[var(--fg-secondary)]">
                {t('attendance.appAccessNoneText')}
              </p>
              <Button type="button" size="sm" variant="soft" pill onClick={() => setIssueOpen(true)}>
                <UserPlus />
                {t('attendance.appAccessIssue')}
              </Button>
            </div>
          ) : (
            <>
              <dl className="grid gap-x-6 gap-y-2.5 rounded-md bg-[var(--bg-sunken)] p-3 sm:grid-cols-3">
                <Fact label={t('attendance.appAccessLogin')}>
                  <span className="tabular">{formatPhone(account.login) ?? account.login}</span>
                </Fact>
                <Fact label={t('attendance.appAccessLastLogin')}>
                  {account.lastLoginAt === null
                    ? t('attendance.appAccessNeverSignedIn')
                    : dateTime(account.lastLoginAt)}
                </Fact>
                <Fact label={t('attendance.appAccessPasswordSetAt')}>
                  {dateTime(account.passwordSetAt)}
                </Fact>
              </dl>

              <div className="flex flex-wrap items-center gap-2">
                <Button type="button" size="sm" variant="soft" pill onClick={() => setConfirm('reset')}>
                  <KeyRound />
                  {t('attendance.appAccessResetPassword')}
                </Button>
                {account.isActive ? (
                  <Button type="button" size="sm" variant="ghost" pill onClick={() => setConfirm('deactivate')}>
                    {t('attendance.appAccessDeactivate')}
                  </Button>
                ) : (
                  <Button type="button" size="sm" variant="ghost" pill onClick={() => setConfirm('activate')}>
                    {t('attendance.appAccessActivate')}
                  </Button>
                )}
              </div>
            </>
          )}

          {/*
            O'chirgich hisobdan MUSTAQIL: ruxsat xodimga beriladi, ya'ni
            hisobi hali yo'q odamga ham oldindan yoqib qo'yish mumkin
            (server izohi, `SetMobileCheckInAllowed`).
          */}
          <div className="border-t border-[var(--border)] pt-3">
            <Switch
              checked={access.mobileCheckInAllowed}
              disabled={toggle.isPending}
              onChange={(allowed) => toggle.mutate(allowed)}
              label={t('attendance.appAccessMobileCheckIn')}
              hint={t('attendance.appAccessMobileCheckInHint')}
            />
            {toggle.isError && (
              <div className="mt-2">
                <Alert tone="danger">{errorMessage(problemCode(toggle.error))}</Alert>
              </div>
            )}
          </div>
        </>
      )}

      <p className="text-caption1 text-[var(--fg-tertiary)]">{t('attendance.appAccessHint')}</p>

      <IssueDialog
        open={issueOpen}
        employeeId={employeeId}
        onClose={() => setIssueOpen(false)}
        onIssued={(created) => {
          setIssueOpen(false)
          setSecret(created)
        }}
      />

      <ConfirmDialog
        kind={confirm}
        employeeId={employeeId}
        onClose={() => setConfirm(null)}
        onSecret={(created) => {
          setConfirm(null)
          setSecret(created)
        }}
      />

      <PasswordDialog secret={secret} onClose={() => setSecret(null)} />
    </Card>
  )
}

/**
 * Hisob holati — bitta nishon.
 *
 * `lockedUntil` endi FAQAT ilovadagi parol almashtirishda noto'g'ri JORIY
 * parol terilganini bildiradi: kirishdagi blok (login + IP bo'yicha) serverda
 * hisobdan tashqarida va bu yerda ko'rinmaydi — u bitta telefonni to'sadi,
 * hisobni emas.
 */
function AccountStatusBadge({ account }: { account: EmployeeAccount | null }) {
  const { t, dateTime } = useLocale()

  let tone: BadgeTone = 'success'
  let label = t('attendance.appAccessActive')

  if (account === null) {
    tone = 'neutral'
    label = t('attendance.appAccessNone')
  } else if (!account.isActive) {
    tone = 'danger'
    label = t('attendance.appAccessInactive')
  } else if (account.lockedUntil !== null && Date.parse(account.lockedUntil) > Date.now()) {
    tone = 'warning'
    label = t('attendance.appAccessLocked', { time: dateTime(account.lockedUntil) })
  } else if (account.mustChangePassword) {
    tone = 'warning'
    label = t('attendance.appAccessMustChange')
  }

  return (
    <Badge tone={tone} size="sm">
      {label}
    </Badge>
  )
}

/**
 * Hisob ochish — telefon raqami ixtiyoriy.
 *
 * Bo'sh qolsa server xodim kartasidagi asosiy raqamni oladi. Maydon baribir
 * bor: xodimlarda takroriy raqam ko'p uchraydi (`xodim.login_band`) va HR
 * boshqa raqam kirita olishi kerak.
 */
function IssueDialog({
  open,
  employeeId,
  onClose,
  onIssued,
}: {
  open: boolean
  employeeId: string
  onClose: () => void
  onIssued: (created: EmployeeAccountSecret) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const issue = useIssueEmployeeAccount(employeeId)
  const [login, setLogin] = useState('')

  useEffect(() => {
    if (!open) return
    setLogin('')
    issue.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!issue.isPending}
      title={t('attendance.appAccessIssueTitle')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={issue.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={issue.isPending}
            onClick={() => issue.mutate(login, { onSuccess: onIssued })}
          >
            {issue.isPending && <LoaderCircle className="animate-spin" />}
            {t('attendance.appAccessIssue')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <TextField
          label={t('attendance.appAccessLogin')}
          hint={t('attendance.appAccessLoginHint')}
          value={login}
          inputMode="tel"
          autoComplete="off"
          onChange={(event) => setLogin(event.target.value)}
        />
        {issue.isError && <Alert tone="danger">{errorMessage(problemCode(issue.error))}</Alert>}
      </div>
    </Dialog>
  )
}

/** Tasdiq: parolni tiklash, hisobni yopish yoki qaytarish. */
function ConfirmDialog({
  kind,
  employeeId,
  onClose,
  onSecret,
}: {
  kind: Confirm
  employeeId: string
  onClose: () => void
  onSecret: (created: EmployeeAccountSecret) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const reset = useResetEmployeeAccountPassword(employeeId)
  const setActive = useSetEmployeeAccountActive(employeeId)

  useEffect(() => {
    reset.reset()
    setActive.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kind])

  if (kind === null) return null

  const pending = reset.isPending || setActive.isPending
  const error = reset.error ?? setActive.error

  const text: Record<Exclude<Confirm, null>, { title: string; body: string; action: string }> = {
    reset: {
      title: t('attendance.appAccessResetPassword'),
      body: t('attendance.appAccessResetConfirm'),
      action: t('attendance.appAccessResetPassword'),
    },
    deactivate: {
      title: t('attendance.appAccessDeactivate'),
      body: t('attendance.appAccessDeactivateConfirm'),
      action: t('attendance.appAccessDeactivate'),
    },
    activate: {
      title: t('attendance.appAccessActivate'),
      body: t('attendance.appAccessActivateConfirm'),
      action: t('attendance.appAccessActivate'),
    },
  }

  const run = () => {
    if (kind === 'reset') {
      reset.mutate(undefined, { onSuccess: onSecret })
      return
    }

    setActive.mutate(kind === 'activate', { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!pending}
      title={text[kind].title}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant={kind === 'deactivate' ? 'danger' : 'primary'}
            disabled={pending}
            onClick={run}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {text[kind].action}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">{text[kind].body}</p>
        {error !== null && <Alert tone="danger">{errorMessage(problemCode(error))}</Alert>}
      </div>
    </Dialog>
  )
}

/**
 * Bir martalik parol — HAYOTDA BIR MARTA (qurilma tokeni oynasi bilan bir xil qoida).
 *
 * ⚠️ Parol faqat shu oynaning xotirasida: keshga, manzilga va
 * `localStorage` ga tushmaydi. Oyna yopilishi bilan `secret` `null` bo'ladi
 * va parol DOM'dan chiqadi. Fon bosilganda va Escape bilan YOPILMAYDI —
 * tasodifiy bosish parolni qaytarib bo'lmas qilib yo'qotardi va xodimga
 * yana tiklashga to'g'ri kelardi.
 */
function PasswordDialog({
  secret,
  onClose,
}: {
  secret: EmployeeAccountSecret | null
  onClose: () => void
}) {
  const { t } = useLocale()

  if (secret === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="md"
      dismissible={false}
      title={t('attendance.appAccessPasswordTitle')}
      description={formatPhone(secret.account.login) ?? secret.account.login}
      footer={
        <Button type="button" variant="primary" onClick={onClose}>
          {t('attendance.appAccessPasswordSaved')}
        </Button>
      }
    >
      <div className="space-y-5">
        <Alert tone="warning" title={t('attendance.appAccessPasswordOnce')}>
          {t('attendance.appAccessPasswordOnceText')}
        </Alert>

        <PasswordField value={secret.password} />
      </div>
    </Dialog>
  )
}

/**
 * Faqat o'qiladigan maydon + nusxalash.
 *
 * `navigator.clipboard` faqat xavfsiz kontekstda ishlaydi — tizim oddiy HTTP
 * da ochilsa tugma ishlamaydi, u holda matn o'zi BELGILANADI va HR Ctrl+C
 * bilan oladi (`attendance-device-token-dialog.tsx` dagi bilan bir xil).
 */
function PasswordField({ value }: { value: string }) {
  const { t } = useLocale()
  const inputRef = useRef<HTMLInputElement>(null)
  const [state, setState] = useState<'idle' | 'copied' | 'failed'>('idle')

  useEffect(() => {
    if (state !== 'copied') return
    const timer = window.setTimeout(() => setState('idle'), COPIED_MS)
    return () => window.clearTimeout(timer)
  }, [state])

  const copy = async () => {
    try {
      if (navigator.clipboard === undefined) throw new Error('clipboard')
      await navigator.clipboard.writeText(value)
      setState('copied')
    } catch {
      inputRef.current?.focus()
      inputRef.current?.select()
      setState('failed')
    }
  }

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">
        {t('attendance.appAccessPassword')}
      </p>

      <div className="flex flex-wrap items-center gap-2">
        <input
          ref={inputRef}
          readOnly
          value={value}
          spellCheck={false}
          aria-label={t('attendance.appAccessPassword')}
          onFocus={(event) => event.currentTarget.select()}
          className={cn(
            'h-11 min-w-0 flex-1 rounded-md border border-[var(--border-strong)] px-3.5',
            'bg-[var(--bg-sunken)] font-mono text-callout tracking-wider text-[var(--fg)] outline-none',
            'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10',
          )}
        />
        <Button
          type="button"
          variant={state === 'copied' ? 'soft' : 'secondary'}
          onClick={() => void copy()}
        >
          {state === 'copied' ? <Check /> : <Copy />}
          {state === 'copied' ? t('attendance.copied') : t('attendance.appAccessCopy')}
        </Button>
      </div>

      {state === 'failed' && (
        <p role="alert" className="text-footnote text-ios-orange">
          {t('attendance.copyFailed')}
        </p>
      )}
    </div>
  )
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-caption1 text-[var(--fg-tertiary)]">{label}</dt>
      <dd className="mt-0.5 truncate-1 text-footnote">{children}</dd>
    </div>
  )
}
