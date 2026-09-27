import {
  ChevronLeft,
  ChevronRight,
  FileText,
  Image as ImageIcon,
  LoaderCircle,
  Paperclip,
  Plus,
  Trash2,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useRef, useState, type ChangeEvent } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Alert, Button, Dialog, Skeleton } from '@/shared/ui'

import type { EmployeeCheckFile } from '../api/employees-api'
import {
  useDeleteEmployeeCheckFile,
  useEmployeeCheckFileContent,
  useEmployeeCheckFiles,
  useUploadEmployeeCheckFile,
} from '../api/employees-queries'
import { DOCUMENT_ACCEPT, documentFileError, fileSizeParts } from '../lib/document-file'
import { employeeErrorCode } from '../lib/employee-error'
import { EMPLOYEES_EDIT } from '../lib/employee-presentation'

/**
 * «Tekshiruvlar» kartasining materiallari — dalil fayllari ro'yxati.
 *
 * <b>Nega ro'yxat (2026-09-27).</b> Ilgari kartada BITTA mehnat.uz
 * skrinshoti bor edi va yangisi eskisini almashtirardi. HR bir nechta
 * tekshiruv o'tkazadi (mehnat.uz, qarzdorlik, sudlanganlik) va har
 * birining o'z dalili bor — foydalanuvchi talabi: «фақат битта юклама
 * камлик қилади». Eski skrinshot migratsiya bilan shu ro'yxatning
 * birinchi qatoriga ko'chgan.
 *
 * <b>Fayl faqat BOSILGANDA ko'rinadi</b> (o'sha talab: «файлни устига
 * босса кўринадиган, бошқа вақт кўринмайдиган»). Ro'yxatda nom, sana va
 * hajm; baytlar esa oyna ochilgandagina so'raladi — karta ixcham turadi
 * va o'nta material o'nta rasmni yuklab o'tirmaydi.
 *
 * <b>Nega `<img src="/api/...">` emas.</b> Tizim `Authorization`
 * sarlavhasi bilan ishlaydi, brauzer esa uni `<img>` so'roviga qo'sha
 * olmaydi — fayl `blob` bo'lib olinadi (`employee-photo.tsx` dagi yo'l).
 */
export function EmployeeCheckFiles({ employeeId }: { employeeId: string }) {
  const { t, num, date } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(EMPLOYEES_EDIT)

  const inputRef = useRef<HTMLInputElement>(null)

  const files = useEmployeeCheckFiles(employeeId)
  const upload = useUploadEmployeeCheckFile(employeeId)

  const [viewingId, setViewingId] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<EmployeeCheckFile | null>(null)
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null)
  const [failures, setFailures] = useState<string[]>([])

  const items = files.data ?? []

  const onPick = async (event: ChangeEvent<HTMLInputElement>) => {
    const picked = Array.from(event.target.files ?? [])

    // Qiymat darhol tozalanadi: aks holda AYNAN o'sha faylni ikkinchi
    // marta tanlash hodisa hosil qilmaydi (`employee-photo-editor` dagi
    // bilan bir xil tuzoq).
    event.target.value = ''
    if (picked.length === 0) return

    /*
      Fayllar KETMA-KET yuboriladi: server bitta so'rovda bitta faylni
      oladi, parallel yuborish esa 20 MB lik fayllar bilan tarmoqni
      bo'g'ib qo'yardi. Bittasi yiqilsa qolganlari baribir yuklanadi —
      xato o'sha fayl nomi bilan ro'yxatda ko'rsatiladi.
    */
    const problems: string[] = []
    setFailures([])
    setProgress({ done: 0, total: picked.length })

    for (const [index, file] of picked.entries()) {
      const rejected = documentFileError(file)

      if (rejected !== null) {
        problems.push(t('employees.checkFilesFailedItem', { name: file.name, error: errorMessage(rejected) }))
      } else {
        try {
          await upload.mutateAsync(file)
        } catch (error) {
          problems.push(
            t('employees.checkFilesFailedItem', {
              name: file.name,
              error: errorMessage(employeeErrorCode(error)),
            }),
          )
        }
      }

      setProgress({ done: index + 1, total: picked.length })
    }

    setProgress(null)
    setFailures(problems)
  }

  return (
    <div className="rounded-md bg-[var(--bg-sunken)] px-3 py-2.5">
      <input
        ref={inputRef}
        type="file"
        multiple
        accept={DOCUMENT_ACCEPT}
        onChange={(event) => void onPick(event)}
        className="hidden"
        tabIndex={-1}
        aria-hidden
      />

      {/* ───────── Sarlavha ───────── */}
      <div className="flex items-start gap-2.5">
        <Paperclip className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />

        <div className="min-w-0 flex-1">
          <p className="text-footnote font-medium text-[var(--fg-secondary)]">
            {t('employees.checkFilesTitle')}
            {items.length > 0 && (
              <span className="tabular ml-1.5 text-[var(--fg-tertiary)]">{num(items.length)}</span>
            )}
          </p>
          <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">{t('employees.checkFilesHint')}</p>
        </div>

        {canManage && (
          <Button
            type="button"
            size="sm"
            variant="secondary"
            onClick={() => inputRef.current?.click()}
            disabled={progress !== null}
            className="shrink-0"
          >
            {progress !== null ? <LoaderCircle className="animate-spin" /> : <Plus />}
            {t('employees.checkFilesAdd')}
          </Button>
        )}
      </div>

      {files.isError && (
        <Alert tone="danger" className="mt-2">
          {errorMessage(employeeErrorCode(files.error))}
        </Alert>
      )}

      {failures.length > 0 && (
        <Alert tone="danger" className="mt-2">
          <ul className="space-y-0.5">
            {failures.map((failure) => (
              <li key={failure}>{failure}</li>
            ))}
          </ul>
        </Alert>
      )}

      {/* ───────── Ro'yxat ───────── */}
      {files.isPending ? (
        <Skeleton className="mt-2.5 h-12 w-full" />
      ) : items.length === 0 && progress === null ? (
        /* Sarlavha matni bilan bir chiziqda — ikona (16px) + oraliq (10px) */
        <p className="mt-2 pl-[26px] text-subhead text-[var(--fg-tertiary)]">{t('employees.checkFilesEmpty')}</p>
      ) : (
        <ul
          className={cn(
            'mt-2.5 divide-y divide-[var(--border)] overflow-hidden rounded-md',
            'border border-[var(--border)] bg-[var(--bg-elevated)]',
          )}
        >
          {items.map((item) => {
            const Icon = iconFor(item.contentType)

            return (
              <li key={item.id} className="flex items-center">
                {/*
                  Butun qator — bitta tugma: fayl FAQAT shu bosilganda
                  ochiladi. O'chirish alohida tugma, qator ichida emas —
                  aks holda uni bosish faylni ham ochib yuborardi.
                */}
                <button
                  type="button"
                  onClick={() => setViewingId(item.id)}
                  className="press flex min-w-0 flex-1 items-center gap-2.5 px-3 py-2.5 text-left"
                >
                  <Icon className="size-5 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />

                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-subhead text-[var(--fg)]" title={item.fileName}>
                      {item.fileName}
                    </span>
                    <span className="tabular block text-caption1 text-[var(--fg-tertiary)]">
                      {metaText(item, num, date)}
                    </span>
                  </span>

                  <ChevronRight className="size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
                </button>

                {canManage && (
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() => setDeleting(item)}
                    aria-label={t('employees.checkFilesDeleteTitle')}
                    title={t('employees.checkFilesDeleteTitle')}
                    className="mr-1.5 shrink-0 text-ios-red hover:bg-ios-red/10 hover:text-ios-red"
                  >
                    <Trash2 />
                  </Button>
                )}
              </li>
            )
          })}

          {progress !== null && (
            <li className="flex items-center gap-2.5 px-3 py-2.5 text-subhead text-[var(--fg-secondary)]">
              <LoaderCircle className="size-5 shrink-0 animate-spin text-[var(--fg-tertiary)]" />
              <span className="tabular">
                {t('employees.checkFilesUploading', {
                  done: num(progress.done),
                  total: num(progress.total),
                })}
              </span>
            </li>
          )}
        </ul>
      )}

      {canManage && (
        <p className="mt-1.5 text-caption2 text-[var(--fg-tertiary)]">{t('employees.checkFilesUploadHint')}</p>
      )}

      <CheckFileViewer
        employeeId={employeeId}
        items={items}
        viewingId={viewingId}
        onNavigate={setViewingId}
        onClose={() => setViewingId(null)}
      />

      <DeleteCheckFileDialog employeeId={employeeId} item={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}

/** PDF va rasm — ikkitagina qabul qilinadigan toifa (`employee-documents-tab` bilan bir xil). */
function iconFor(contentType: string): LucideIcon {
  return contentType.startsWith('image/') ? ImageIcon : FileText
}

/** «27.09.2026 · 245 KB» — hajm noma'lum bo'lsa (eski skrinshot) faqat sana. */
function metaText(
  item: EmployeeCheckFile,
  num: (value: number) => string,
  date: (value: string) => string,
): string {
  const when = item.createdAt === '' ? '' : date(item.createdAt)
  if (item.sizeBytes === null) return when

  const { value, unit } = fileSizeParts(item.sizeBytes)
  return when === '' ? `${num(value)} ${unit}` : `${when} · ${num(value)} ${unit}`
}

/**
 * Materialni ko'rish oynasi — fayl FAQAT shu yerda chiziladi.
 *
 * Bir nechta material bo'lsa oyna ichida varaqlanadi (tugmalar va ←/→
 * klavishalari): HR odatda hammasini ketma-ket ko'rib chiqadi va har
 * safar oynani yopib, keyingisini bosish ortiqcha qadam bo'lardi.
 */
function CheckFileViewer({
  employeeId,
  items,
  viewingId,
  onNavigate,
  onClose,
}: {
  employeeId: string
  items: EmployeeCheckFile[]
  viewingId: string | null
  onNavigate: (id: string) => void
  onClose: () => void
}) {
  const { t, num, date } = useLocale()

  const index = items.findIndex((item) => item.id === viewingId)
  const item = index === -1 ? null : (items[index] ?? null)

  const content = useEmployeeCheckFileContent(employeeId, item?.id ?? null)
  const src = useObjectUrl(content.data ?? null)

  const previous = index > 0 ? items[index - 1] : undefined
  const next = index !== -1 && index < items.length - 1 ? items[index + 1] : undefined

  useEffect(() => {
    if (item === null) return

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'ArrowLeft' && previous !== undefined) onNavigate(previous.id)
      if (event.key === 'ArrowRight' && next !== undefined) onNavigate(next.id)
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [item, previous, next, onNavigate])

  if (item === null) return null

  /*
    Tur ro'yxatdan olinadi, blob'dan emas: u server tomonda MAZMUN
    bo'yicha aniqlangan va javobdan oldin ma'lum — PDF ni `<img>` ga
    berib, singan rasm belgisini chizish xavfi yo'q.
  */
  const isPdf = item.contentType === 'application/pdf'

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      title={<span className="block truncate">{item.fileName}</span>}
      description={`${metaText(item, num, date)} · ${t('employees.checkFilesViewHint')}`}
      footer={
        <div className="flex w-full flex-wrap items-center justify-between gap-2">
          {items.length > 1 ? (
            <div className="flex items-center gap-1.5">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => previous !== undefined && onNavigate(previous.id)}
                disabled={previous === undefined}
              >
                <ChevronLeft />
                {t('employees.checkFilesPrev')}
              </Button>

              <span className="tabular text-footnote text-[var(--fg-secondary)]">
                {t('employees.checkFilesPosition', { n: num(index + 1), total: num(items.length) })}
              </span>

              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => next !== undefined && onNavigate(next.id)}
                disabled={next === undefined}
              >
                {t('employees.checkFilesNext')}
                <ChevronRight />
              </Button>
            </div>
          ) : (
            <span />
          )}

          <Button type="button" onClick={onClose}>
            {t('common.close')}
          </Button>
        </div>
      }
    >
      {content.isError ? (
        <Alert tone="danger">{t('employees.checkFilesLoadFailed')}</Alert>
      ) : src === null ? (
        <Skeleton className="h-64 w-full" />
      ) : isPdf ? (
        /*
          PDF `<iframe>` da: brauzerning o'z ko'ruvchisi varaqlash va
          kattalashtirishni bepul beradi.
        */
        <iframe
          src={src}
          title={item.fileName}
          className="h-[70vh] w-full rounded-md border border-[var(--border)]"
        />
      ) : (
        /*
          `object-contain` — rasm QIRQILMAYDI: skrinshotning butun ma'nosi
          undagi matnning to'liq o'qilishida.
        */
        <img
          src={src}
          alt={item.fileName}
          className="max-h-[70vh] w-full rounded-md border border-[var(--border)] object-contain"
        />
      )}
    </Dialog>
  )
}

/** O'chirishni tasdiqlash. Serverda YUMSHOQ: yozuv tarixda, fayl diskda qoladi. */
function DeleteCheckFileDialog({
  employeeId,
  item,
  onClose,
}: {
  employeeId: string
  item: EmployeeCheckFile | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteEmployeeCheckFile(employeeId)

  // Oldingi materialning xato holati keyingisiga o'tib qolmasin.
  useEffect(() => {
    if (item !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('employees.checkFilesDeleteTitle')}
      description={item.fileName}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>

          <Button
            type="button"
            variant="danger"
            onClick={() => remove.mutate(item.id, { onSuccess: onClose })}
            disabled={remove.isPending}
          >
            {remove.isPending ? <LoaderCircle className="animate-spin" /> : <Trash2 />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {remove.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(remove.error))}</Alert>}
        <p className="text-subhead text-[var(--fg-secondary)]">{t('employees.checkFilesDeleteText')}</p>
      </div>
    </Dialog>
  )
}
