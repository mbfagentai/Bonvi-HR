import { Camera, FileText, Loader2, Paperclip, Trash2 } from 'lucide-react'
import { useId, useRef, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui'

import { FILE_DEFAULT_MAX } from '../../lib/answers-zod'
import { formatFileSize } from '../../lib/blank-presentation'
import type { BlankAttachmentDto, FileAccept, FileFieldDef, JsonValue } from '../../lib/definition-types'
import { useRenderer } from './renderer-context'

/** §8: hard cap per file — `DocumentLimits.MaxBytes`. */
const HARD_MAX_MB = 20
/** §8: images are downscaled in the browser to ≤ 2400 px JPEG. */
const IMAGE_MAX_PX = 2400
const JPEG_QUALITY = 0.86

const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const OFFICE_EXT = ['.docx', '.xlsx', '.pptx']

function expandAccept(accept: readonly FileAccept[] | undefined): Set<'pdf' | 'image' | 'office'> {
  const list = accept && accept.length > 0 ? accept : ['any' as const]
  const kinds = new Set<'pdf' | 'image' | 'office'>()
  for (const a of list) {
    if (a === 'any') ['pdf', 'image', 'office'].forEach((k) => kinds.add(k as 'pdf' | 'image' | 'office'))
    else kinds.add(a)
  }
  return kinds
}

/** The `<input accept>` string — a hint for the picker; the server re-checks content. */
function acceptAttr(kinds: ReadonlySet<string>): string {
  const parts: string[] = []
  if (kinds.has('pdf')) parts.push('application/pdf', '.pdf')
  if (kinds.has('image')) parts.push(...IMAGE_TYPES, '.jpg', '.jpeg', '.png', '.webp')
  if (kinds.has('office')) parts.push(...OFFICE_EXT)
  return parts.join(',')
}

function kindOf(file: File): 'pdf' | 'image' | 'office' | null {
  const name = file.name.toLowerCase()
  if (file.type === 'application/pdf' || name.endsWith('.pdf')) return 'pdf'
  if (IMAGE_TYPES.includes(file.type) || /\.(jpe?g|png|webp)$/.test(name)) return 'image'
  if (OFFICE_EXT.some((ext) => name.endsWith(ext))) return 'office'
  return null
}

/** Re-encode a large photo as JPEG ≤ 2400 px; small images pass through unchanged. */
async function downscaleImage(file: File): Promise<File> {
  if (typeof createImageBitmap !== 'function') return file
  const bitmap = await createImageBitmap(file)
  try {
    const scale = Math.min(1, IMAGE_MAX_PX / Math.max(bitmap.width, bitmap.height))
    if (scale === 1 && file.type === 'image/jpeg') return file
    const canvas = document.createElement('canvas')
    canvas.width = Math.round(bitmap.width * scale)
    canvas.height = Math.round(bitmap.height * scale)
    const context = canvas.getContext('2d')
    if (!context) return file
    context.fillStyle = '#fff'
    context.fillRect(0, 0, canvas.width, canvas.height)
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', JPEG_QUALITY))
    if (!blob) return file
    const base = file.name.replace(/\.[^.]+$/, '')
    return new File([blob], `${base}.jpg`, { type: 'image/jpeg' })
  } finally {
    bitmap.close()
  }
}

function ids(value: JsonValue | undefined): string[] {
  return Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : []
}

/**
 * `file` field (§8) — per-field `accept`, `maxFiles`, `maxSizeMb`, `capture`.
 * Upload / remove are injected (`uploadFile`, `removeFile` on `<BlankForm>`);
 * the value is the list of attachment ids of this field path.
 */
export function FileField({
  field,
  path,
  value,
  onChange,
  disabled = false,
}: {
  field: FileFieldDef
  path: string
  value: JsonValue | undefined
  onChange: (value: JsonValue) => void
  disabled?: boolean
}) {
  const { t, locale } = useLocale()
  const { attachments, uploadFile, removeFile, openFile } = useRenderer()
  const inputId = useId()
  const cameraInput = useRef<HTMLInputElement>(null)
  const fileInput = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [problems, setProblems] = useState<string[]>([])

  const kinds = expandAccept(field.accept)
  const maxFiles = field.maxFiles ?? FILE_DEFAULT_MAX
  const maxMb = Math.min(field.maxSizeMb ?? HARD_MAX_MB, HARD_MAX_MB)
  const current = ids(value)
  const files = current
    .map((id) => attachments.find((a) => a.id === id))
    .filter((a): a is BlankAttachmentDto => a !== undefined)
  const canAdd = !disabled && uploadFile !== undefined && current.length < maxFiles

  const onPicked = async (list: FileList | null) => {
    if (!list || !uploadFile) return
    const picked = Array.from(list)
    const errors: string[] = []
    const room = maxFiles - current.length
    if (picked.length > room) errors.push(t('blanks.form.errors.tooManyFiles', { max: maxFiles }))
    const added: string[] = []
    setBusy(true)
    try {
      for (const original of picked.slice(0, Math.max(room, 0))) {
        const kind = kindOf(original)
        if (!kind || !kinds.has(kind)) {
          errors.push(t('blanks.form.errors.fileType', { name: original.name }))
          continue
        }
        const file = kind === 'image' ? await downscaleImage(original).catch(() => original) : original
        if (file.size > maxMb * 1024 * 1024) {
          errors.push(t('blanks.form.errors.fileSize', { name: original.name, max: maxMb }))
          continue
        }
        try {
          const attachment = await uploadFile(path, file)
          added.push(attachment.id)
        } catch {
          errors.push(t('blanks.form.errors.fileUpload', { name: original.name }))
        }
      }
    } finally {
      setBusy(false)
      setProblems(errors)
      if (added.length > 0) onChange([...current, ...added])
    }
  }

  const remove = async (attachment: BlankAttachmentDto) => {
    if (!removeFile) return
    setBusy(true)
    try {
      await removeFile(attachment)
      const next = current.filter((id) => id !== attachment.id)
      onChange(next.length === 0 ? null : next)
    } catch {
      setProblems([t('blanks.form.errors.fileRemove', { name: attachment.fileName })])
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-2">
      {files.length > 0 ? (
        <ul className="overflow-hidden rounded-md border border-[var(--border)] bg-[var(--bg-elevated)]">
          {files.map((file) => (
            <li key={file.id} className="flex items-center gap-3 border-b border-[var(--border)] px-3.5 py-2 last:border-b-0">
              <FileText aria-hidden className="size-5 shrink-0 text-[var(--fg-tertiary)]" />
              <button
                type="button"
                className="min-w-0 flex-1 truncate text-left text-body text-[var(--accent-text)] disabled:text-[var(--fg)]"
                disabled={!openFile}
                onClick={() => openFile?.(file)}
              >
                {file.fileName}
              </button>
              <span className="shrink-0 text-footnote text-[var(--fg-tertiary)] tabular-nums">
                {formatFileSize(file.sizeBytes, locale)}
              </span>
              {!disabled && removeFile && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  disabled={busy}
                  aria-label={t('blanks.form.removeFile', { name: file.fileName })}
                  onClick={() => void remove(file)}
                >
                  <Trash2 />
                </Button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        disabled && <p className="text-body text-[var(--fg-tertiary)]">{t('blanks.form.noFiles')}</p>
      )}

      {!disabled && uploadFile && (
        <div className="flex flex-wrap items-center gap-2">
          <input
            ref={fileInput}
            id={inputId}
            type="file"
            className="sr-only"
            accept={acceptAttr(kinds)}
            multiple={maxFiles - current.length > 1}
            onChange={(e) => {
              void onPicked(e.target.files)
              e.target.value = ''
            }}
          />
          <Button type="button" variant="secondary" size="sm" disabled={!canAdd || busy} onClick={() => fileInput.current?.click()}>
            {busy ? <Loader2 className="animate-spin" /> : <Paperclip />}
            {busy ? t('blanks.form.uploading') : t('blanks.form.attach')}
          </Button>
          {field.capture && kinds.has('image') && (
            <>
              <input
                ref={cameraInput}
                type="file"
                className="sr-only"
                accept="image/*"
                capture={field.capture}
                onChange={(e) => {
                  void onPicked(e.target.files)
                  e.target.value = ''
                }}
              />
              <Button type="button" variant="secondary" size="sm" disabled={!canAdd || busy} onClick={() => cameraInput.current?.click()}>
                <Camera />
                {t('blanks.form.takePhoto')}
              </Button>
            </>
          )}
          <span className={cn('text-footnote text-[var(--fg-tertiary)]')}>
            {t('blanks.form.filesLimit', { used: current.length, max: maxFiles, size: maxMb })}
          </span>
        </div>
      )}

      {problems.map((problem) => (
        <p key={problem} role="alert" className="text-footnote text-ios-red">
          {problem}
        </p>
      ))}
    </div>
  )
}
