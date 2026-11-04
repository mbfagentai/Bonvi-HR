import { useNavigate } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { useMemo, useState } from 'react'

import {
  AttendanceEmployeePicker,
  type PickedEmployee,
} from '@/features/verification/components/attendance-employee-picker'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, Select } from '@/shared/ui'

import { useBlankTemplates, useCreateBlank } from '../api/blanks-queries'
import { blankErrorCode } from '../lib/blank-error'

const FORM_ID = 'blank-create-form'

/**
 * New blank: template + subject → POST #7 → the new draft opens.
 *
 * `clientRequestId` is fixed for the lifetime of the dialog: a double click or a retry
 * after a dropped response returns the SAME draft (server idempotency, SPEC §5).
 * Candidate-subject templates arrive in Phase 2 — they are listed but disabled.
 *
 * `employee` presets the subject (employee card «Blankalar» tab): only employee-subject
 * templates are offered and the picker is locked to that person.
 */
export function BlankCreateDialog({ onClose, employee: preset }: { onClose: () => void; employee?: PickedEmployee }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  const templates = useBlankTemplates()
  const create = useCreateBlank()

  const [clientRequestId] = useState(() => crypto.randomUUID())
  const [templateCode, setTemplateCode] = useState('')
  const [employee, setEmployee] = useState<PickedEmployee | null>(preset ?? null)
  const [failure, setFailure] = useState<string | null>(null)

  const available = useMemo(
    () =>
      (templates.data ?? []).filter(
        (item) => item.isActive && item.versionId !== null && (preset === undefined || item.subjectKind === 'Employee'),
      ),
    [preset, templates.data],
  )
  const options = useMemo(
    () =>
      available.map((item) => ({
        value: item.code,
        label: `${item.code} · ${item.title}`,
        disabled: item.subjectKind === 'Candidate',
      })),
    [available],
  )
  const template = available.find((item) => item.code === templateCode) ?? null
  const needsEmployee = template?.subjectKind === 'Employee'
  const incomplete = template === null || (needsEmployee && employee === null)

  function submit() {
    if (template === null || incomplete) return
    setFailure(null)
    create.mutate(
      {
        templateCode: template.code,
        employeeId: needsEmployee ? (employee?.id ?? null) : null,
        candidateId: null,
        clientRequestId,
      },
      {
        onSuccess: (created) => {
          onClose()
          void navigate({ to: '/blankalar/$submissionId', params: { submissionId: created.id } })
        },
        onError: (error) => setFailure(blankErrorCode(error)),
      },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!create.isPending}
      title={t('blanks.create.title')}
      description={t('blanks.create.text')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={create.isPending}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form={FORM_ID} variant="primary" disabled={create.isPending || incomplete}>
            {create.isPending && <LoaderCircle className="animate-spin" />}
            {t('blanks.create.submit')}
          </Button>
        </>
      }
    >
      <form
        id={FORM_ID}
        noValidate
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}
        {templates.isError && <Alert tone="danger">{errorMessage(blankErrorCode(templates.error))}</Alert>}

        <Select
          label={t('blanks.list.template')}
          placeholder={templates.isPending ? t('common.loading') : t('blanks.create.chooseTemplate')}
          options={options}
          value={templateCode}
          disabled={create.isPending || templates.isPending}
          onChange={(event) => {
            setTemplateCode(event.target.value)
            setFailure(null)
          }}
        />
        {!templates.isPending && available.length === 0 && !templates.isError && (
          <p className="text-footnote text-[var(--fg-secondary)]">{t('blanks.create.noTemplates')}</p>
        )}

        {needsEmployee && (
          <AttendanceEmployeePicker
            label={t('blanks.create.employee')}
            value={employee}
            onChange={setEmployee}
            employedOnly
            disabled={create.isPending || preset !== undefined}
          />
        )}
      </form>
    </Dialog>
  )
}
