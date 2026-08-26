import { LoaderCircle, Upload } from 'lucide-react'
import { useEffect, useRef, useState, type ChangeEvent } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, IconTile, Select, TextArea } from '@/shared/ui'

import type { EmployeeDocument } from '../api/documents-api'
import { useDocumentTypes, useUpdateDocument, useUploadDocument } from '../api/employees-queries'
import { DOCUMENT_ACCEPT, documentFileError, fileSizeParts } from '../lib/document-file'
import { employeeErrorCode } from '../lib/employee-error'

function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/** «84 KB» / «3,2 MB» — tildagi kasr ajratgichi bilan. */
function sizeLabel(bytes: number, num: (v: number) => string): string {
  const { value, unit } = fileSizeParts(bytes)
  return `${num(value)} ${unit}`
}

/**
 * Hujjat yuklash yoki tahrirlash — BITTA komponent
 * (`incentive-dialog.tsx` dagi bilan AYNAN bir xil naqsh).
 *
 * <b>Fayl FAQAT yaratishda tanlanadi.</b> Tahrirlashda fayl maydoni
 * UMUMAN yo'q — faylning o'zi almashtirilmaydi: noto'g'ri fayl
 * yuklangan bo'lsa, bu tuzatish emas, boshqa fakt — yozuv o'chirilib,
 * yangisi qayta yuklanadi (VAZIFA brifi; `UpdateDocumentRequest`da ham
 * `file` maydoni yo'q). Shu sababli tahrirlashda mavjud fayl nomi va
 * hajmi FAQAT O'QISH uchun ko'rsatiladi, tanlov o'rniga — pastdagi izoh
 * buni tushuntiradi.
 *
 * <b>Klient tomonda ham tekshiriladi</b> (`documentFileError`): 20 MB
 * fayl serverga chiqib, u yerdan xato bilan qaytguncha kutish yomon
 * tajriba — server tekshiruvi baribir asosiy himoya bo'lib qoladi.
 */
export function DocumentDialog({
  employeeId,
  mode,
  item,
  onClose,
}: {
  employeeId: string
  /** `null` — oyna yopiq. */
  mode: 'create' | 'edit' | null
  /** Faqat tahrirlashda to'ldirilgan. */
  item: EmployeeDocument | null
  onClose: () => void
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  const open = mode !== null
  const isEdit = mode === 'edit' && item !== null

  const inputRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const [typeId, setTypeId] = useState('')
  const [note, setNote] = useState('')

  const types = useDocumentTypes()
  const create = useUploadDocument(employeeId)
  const update = useUpdateDocument(employeeId)
  const mutation = isEdit ? update : create

  useEffect(() => {
    if (!open) return

    if (isEdit && item !== null) {
      setTypeId(item.documentTypeId)
      setNote(item.note ?? '')
    } else {
      setTypeId('')
      setNote('')
    }

    setFile(null)
    setFileError(null)
    create.reset()
    update.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) return null

  const onPick = (event: ChangeEvent<HTMLInputElement>) => {
    const picked = event.target.files?.[0] ?? null
    // Qiymat darhol tozalanadi: aks holda AYNAN o'sha faylni ikkinchi
    // marta tanlash hodisa hosil qilmaydi (`employee-photo-editor.tsx`
    // dagi bilan bir xil sabab).
    event.target.value = ''
    if (picked === null) return

    const problem = documentFileError(picked)
    if (problem !== null) {
      setFile(null)
      setFileError(problem)
      return
    }

    setFileError(null)
    setFile(picked)
  }

  const incomplete = typeId === '' || (!isEdit && file === null)

  const submit = () => {
    if (incomplete) return

    if (isEdit && item !== null) {
      update.mutate(
        { documentId: item.id, body: { documentTypeId: typeId, note: orNull(note) } },
        { onSuccess: onClose },
      )
    } else if (file !== null) {
      create.mutate({ file, documentTypeId: typeId, note: orNull(note) }, { onSuccess: onClose })
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!mutation.isPending}
      title={isEdit ? t('documents.editTitle') : t('documents.newTitle')}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={mutation.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={submit}
            disabled={mutation.isPending || incomplete}
          >
            {mutation.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {mutation.isError && (
          <Alert tone="danger">{errorMessage(employeeErrorCode(mutation.error))}</Alert>
        )}
        {fileError !== null && <Alert tone="danger">{errorMessage(fileError)}</Alert>}

        <div className="space-y-1.5">
          <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
            {t('documents.file')}
          </span>

          {isEdit && item !== null ? (
            // Fayl O'QISH uchun — tanlov yo'q, pastdagi izoh sababini aytadi.
            <div className="flex items-center gap-3 rounded-md border border-[var(--border)] bg-[var(--bg-sunken)] px-3.5 py-3">
              <IconTile icon={Upload} tone="gray" size="sm" />
              <div className="min-w-0 flex-1">
                <p className="truncate-1 text-subhead font-medium" title={item.fileName}>
                  {item.fileName}
                </p>
                <p className="tabular text-caption2 text-[var(--fg-tertiary)]">
                  {sizeLabel(item.sizeBytes, num)}
                </p>
              </div>
            </div>
          ) : (
            <>
              <input
                ref={inputRef}
                type="file"
                accept={DOCUMENT_ACCEPT}
                onChange={onPick}
                className="hidden"
                tabIndex={-1}
                aria-hidden
              />
              <button
                type="button"
                onClick={() => inputRef.current?.click()}
                className="press flex w-full items-center gap-3 rounded-md border border-dashed border-[var(--border-strong)] px-3.5 py-3 text-left transition-colors duration-200 ease-[var(--ease-ios)] hover:border-[var(--accent)]"
              >
                <IconTile icon={Upload} tone={file === null ? 'gray' : 'brand'} size="sm" />
                <div className="min-w-0 flex-1">
                  <p className="truncate-1 text-subhead font-medium">
                    {file === null ? t('documents.choose') : file.name}
                  </p>
                  <p className="text-caption2 text-[var(--fg-tertiary)]">
                    {file === null ? t('documents.acceptHint') : sizeLabel(file.size, num)}
                  </p>
                </div>
              </button>
            </>
          )}

          {isEdit && (
            <p className="text-caption2 text-[var(--fg-tertiary)]">{t('documents.editFileHint')}</p>
          )}
        </div>

        <Select
          label={t('documents.type')}
          options={(types.data ?? []).map((option) => ({ value: option.id, label: option.name }))}
          placeholder={types.isPending ? t('common.loading') : t('documents.chooseType')}
          value={typeId}
          disabled={types.isPending}
          onChange={(event) => setTypeId(event.target.value)}
        />

        <TextArea
          label={t('documents.note')}
          rows={3}
          value={note}
          onChange={(event) => setNote(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
