import { LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Switch, TextField } from '@/shared/ui'

import { useSetWorkSiteActive } from '../api/work-sites-queries'

/** The server appends the reason to the note — keep it short. */
const REASON_MAX = 200

/**
 * Activate / deactivate — only inside the editor, behind a confirmation
 * (same path as `CatalogActiveField`): turning a site off rejects every
 * phone check-in there, so it must not happen from one stray tap in a list.
 *
 * Applied immediately, not with «Save»: the confirmation already asked.
 */
export function WorkSiteActiveField({
  id,
  name,
  active,
  isLastActive,
  disabled = false,
  onReason,
}: {
  id: string
  name: string
  active: boolean
  /** Turning this one off leaves no active site at all. */
  isLastActive: boolean
  disabled?: boolean
  /** Called with the reason the server stored as the site's note. */
  onReason?: (reason: string) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const toggle = useSetWorkSiteActive()

  // `active` is the snapshot the list had when the row was opened
  const [current, setCurrent] = useState(active)
  const [asking, setAsking] = useState(false)
  /*
    The state being confirmed — frozen when the dialog opens. Deriving it
    from `current` would flip the dialog's title and button during its
    closing animation, right after the change succeeded.
  */
  const [next, setNext] = useState(!active)
  const [reason, setReason] = useState('')

  useEffect(() => {
    setCurrent(active)
    setAsking(false)
    setReason('')
  }, [id, active])

  const open = () => {
    toggle.reset()
    setReason('')
    setNext(!current)
    setAsking(true)
  }

  const apply = () => {
    const trimmed = reason.trim()
    toggle.mutate(
      { id, active: next, reason: next || trimmed === '' ? null : trimmed },
      {
        onSuccess: () => {
          setCurrent(next)
          setAsking(false)
          if (!next && trimmed !== '') onReason?.(trimmed)
        },
      },
    )
  }

  return (
    <>
      <div className="rounded-xl border border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-3">
        <Switch
          checked={current}
          disabled={disabled || toggle.isPending}
          onChange={open}
          label={t('attendance.workSites.statusTitle')}
          hint={
            current
              ? t('attendance.workSites.statusHintActive')
              : t('attendance.workSites.statusHintInactive')
          }
        />
      </div>

      <Dialog
        open={asking}
        onClose={() => setAsking(false)}
        size="sm"
        dismissible={!toggle.isPending}
        title={
          next
            ? t('attendance.workSites.activateTitle')
            : t('attendance.workSites.deactivateTitle')
        }
        description={name}
        footer={
          <>
            <Button type="button" onClick={() => setAsking(false)} disabled={toggle.isPending}>
              {t('common.cancel')}
            </Button>
            <Button
              type="button"
              variant={next ? 'primary' : 'danger'}
              onClick={apply}
              disabled={toggle.isPending}
            >
              {toggle.isPending && <LoaderCircle className="animate-spin" />}
              {next ? t('catalog.activate') : t('catalog.deactivate')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-subhead text-[var(--fg-secondary)]">
            {next
              ? t('attendance.workSites.activateText')
              : t('attendance.workSites.deactivateText')}
          </p>

          {!next && isLastActive && (
            <Alert tone="warning">{t('attendance.workSites.lastActiveWarning')}</Alert>
          )}

          {!next && (
            <TextField
              label={t('attendance.workSites.deactivateReason')}
              hint={t('attendance.workSites.deactivateReasonHint')}
              value={reason}
              maxLength={REASON_MAX}
              disabled={toggle.isPending}
              onChange={(event) => setReason(event.target.value)}
            />
          )}

          {toggle.isError && (
            <Alert tone="danger" title={t('catalog.statusChangeFailed')}>
              {errorMessage(problemCode(toggle.error))}
            </Alert>
          )}
        </div>
      </Dialog>
    </>
  )
}
