import { useCallback, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Button, Card, CardContent, CardHeader, SegmentedControl } from '@/shared/ui'

import { validateAnswers, type FieldErrors } from '../../lib/answers-zod'
import type { Answers, BlankAttachmentDto, RefAnswer } from '../../lib/definition-types'
import { BlankForm, type BlankServerError } from './blank-form'
import { HR_F06_EXAMPLE } from './renderer-demo-definition'
import type { RefSearch, RemoveFile, UploadFile } from './renderer-context'

/**
 * DEV-ONLY demo of the generic renderer on the SPEC §4.9 HR-F06 example.
 * Not registered in routes or navigation — mount it by hand while working on
 * the renderer. Search and uploads are in-memory fakes; nothing leaves the page.
 */

const DEMO_PEOPLE: RefAnswer[] = [
  { id: '00000000-0000-7000-8000-000000000001', label: 'Aliyev Anvar' },
  { id: '00000000-0000-7000-8000-000000000002', label: 'Karimova Dilnoza' },
  { id: '00000000-0000-7000-8000-000000000003', label: 'Rahimov Bekzod' },
]

const PRESETS = {
  draft: ['hdr', 's1', 'demoExtras'],
  day7: ['d7', 's3'],
  day30: ['d30', 's3'],
  view: [],
} as const
type Preset = keyof typeof PRESETS

const INITIAL: Answers = {
  employee: { id: DEMO_PEOPLE[0]?.id ?? '', label: DEMO_PEOPLE[0]?.label ?? '' },
  position: 'Operator / Ishlab chiqarish',
  hireDate: '2026-10-20',
}

const searchPeople: RefSearch = async (_kind, query) => {
  await new Promise((resolve) => window.setTimeout(resolve, 250))
  const q = query.toLowerCase()
  return DEMO_PEOPLE.filter((p) => p.label.toLowerCase().includes(q))
}

export function RendererDemo() {
  const { t } = useLocale()
  const [preset, setPreset] = useState<Preset>('draft')
  const [answers, setAnswers] = useState<Answers>(INITIAL)
  const [attachments, setAttachments] = useState<BlankAttachmentDto[]>([])
  const [errors, setErrors] = useState<FieldErrors>({})
  const [serverError, setServerError] = useState<BlankServerError | null>(null)

  const editable = PRESETS[preset]

  const upload = useCallback<UploadFile>(async (fieldPath, file) => {
    await new Promise((resolve) => window.setTimeout(resolve, 400))
    const attachment: BlankAttachmentDto = {
      id: crypto.randomUUID(),
      fieldPath,
      kind: file.type,
      fileName: file.name,
      sizeBytes: file.size,
      uploadedAt: new Date().toISOString(),
    }
    setAttachments((list) => [...list, attachment])
    return attachment
  }, [])

  const remove = useCallback<RemoveFile>(async (attachment) => {
    setAttachments((list) => list.filter((a) => a.id !== attachment.id))
  }, [])

  return (
    <div className="mx-auto max-w-3xl space-y-4 p-4">
      <Card>
        <CardHeader title={`${HR_F06_EXAMPLE.code} · ${HR_F06_EXAMPLE.title}`} subtitle={t('blanks.form.demoHint')} />
        <CardContent className="flex flex-wrap items-center gap-3">
          <SegmentedControl<Preset>
            aria-label={t('blanks.form.demoEditable')}
            value={preset}
            onChange={(next) => {
              setPreset(next)
              setErrors({})
            }}
            options={[
              { value: 'draft', label: 'Draft' },
              { value: 'day7', label: 'day7' },
              { value: 'day30', label: 'day30' },
              { value: 'view', label: 'View' },
            ]}
          />
          <Button
            type="button"
            variant="primary"
            size="sm"
            onClick={() => setErrors(validateAnswers(HR_F06_EXAMPLE, answers, { requireFilled: true, sections: editable }))}
          >
            {t('blanks.form.demoCheck')}
          </Button>
          <Button
            type="button"
            size="sm"
            onClick={() => setServerError(serverError ? null : { code: 'blanka.majburiy', detail: 'mentor' })}
          >
            {t('blanks.form.demoServerError')}
          </Button>
        </CardContent>
      </Card>

      <BlankForm
        definition={HR_F06_EXAMPLE}
        answers={answers}
        onChange={(next) => {
          setAnswers(next)
          setServerError(null)
        }}
        editableSections={editable}
        errors={errors}
        serverError={serverError}
        attachments={attachments}
        searchRef={searchPeople}
        uploadFile={upload}
        removeFile={remove}
      />

      <Card>
        <CardHeader title={t('blanks.form.demoAnswers')} />
        <CardContent>
          <pre className="overflow-x-auto rounded-md bg-[var(--bg-sunken)] p-3 text-footnote">
            {JSON.stringify(answers, null, 2)}
          </pre>
        </CardContent>
      </Card>
    </div>
  )
}
