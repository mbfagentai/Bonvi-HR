import {
  Download,
  Eye,
  FileText,
  Image as ImageIcon,
  LoaderCircle,
  Paperclip,
  Pencil,
  Plus,
  Trash2,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Button,
  Card,
  CardDivider,
  CardHeader,
  Dialog,
  EmptyState,
  FileViewer,
  IconTile,
  Skeleton,
  useViewerList,
  viewerKind,
} from '@/shared/ui'

import type { EmployeeDocument } from '../api/documents-api'
import {
  useDeleteDocument,
  useDocumentFileBlob,
  useDocumentTypes,
  useDownloadDocument,
  useEmployeeDocuments,
} from '../api/employees-queries'
import { fileSizeParts } from '../lib/document-file'
import { employeeErrorCode } from '../lib/employee-error'
import { EMPLOYEES_EDIT } from '../lib/employee-presentation'
import { DocumentDialog } from './document-dialog'

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: EmployeeDocument[] = []

interface DocumentGroup {
  typeId: string
  typeName: string
  items: EmployeeDocument[]
}

/**
 * ⭐ «Hujjatlar» tabi (PLAN.md dagi oltinchi, oxirgi).
 *
 * <b>Guruhlash tur bo'yicha, klient tomonda.</b> Endpoint `documentTypeId`
 * filtrini qabul qiladi, lekin HAMMA hujjat bitta so'rov bilan olinadi —
 * xodimda odatda o'nlab hujjat bor, filtrlab-filtrlab so'rov yuborish
 * ortiqcha. Guruh TARTIBI ma'lumotnomaning O'ZIDAN olinadi
 * (`useDocumentTypes` — server allaqachon alifbo bo'yicha saralagan):
 * `Array.sort` bilan qayta saralash xato — brauzer kolatsiyasi serverdan
 * farq qilib, ikki xil tartib ko'rsatib qo'yishi mumkin edi
 * (`catalog-api.ts` dagi shu haqidagi izohga qarang). Faolsizlantirilgan
 * turga tegishli eski hujjat ham yo'qolib ketmaydi — ro'yxat oxiriga,
 * hujjatning o'z nomi bilan qo'shiladi.
 *
 * <b>Jadval emas, ro'yxat.</b> `EmployeeIncentivesTab` dan farqli, bu
 * yerda qator soni odatda kichik va har biri ko'p qatorli (nom + izoh),
 * ya'ni `DataTable`ning saralanadigan ustunlari foyda bermaydi — `flags`
 * tabidagi bilan bir xil, oddiy `<ul>` ro'yxati tanlandi.
 *
 * <b>Ko'rish/yuklab olish — BITTA amal.</b> Backend javobi
 * `Content-Disposition: attachment` bilan keladi, ya'ni server niyati
 * aniq: ko'rish emas, saqlash. Shu sababli alohida «ko'rish» va
 * «yuklab olish» tugmalari o'rniga bitta `Download` amali bor — u
 * baytlarni oladi va `<a download>` orqali diskka saqlaydi
 * (`saveBlobAs`). PDF va rasm — ikkalasi ham xuddi shunday: brauzer
 * yuklab olingan faylni operatsion tizimning standart ko'rish
 * dasturida ochadi, ya'ni yuklab olish ko'rish ehtiyojini ham qamrab
 * oladi va ikkita alohida so'rov yo'lini saqlash shart emas.
 *
 * Ruxsat: `employees.view` ko'rish uchun (sahifaning o'ziga kirish
 * huquqi — `$employeeId.tsx` da tekshiriladi, `EmployeeCompetencyTab`
 * bilan bir xil naqsh), `employees.edit` amallar uchun (qo'shish,
 * tahrirlash, o'chirish).
 */
export function EmployeeDocumentsTab({ employeeId }: { employeeId: string }) {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(EMPLOYEES_EDIT)

  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<EmployeeDocument | null>(null)
  const [deleting, setDeleting] = useState<EmployeeDocument | null>(null)

  const list = useEmployeeDocuments(employeeId)
  const types = useDocumentTypes()
  const download = useDownloadDocument(employeeId)

  const groups = useMemo<DocumentGroup[]>(() => {
    const docs = list.data ?? EMPTY

    const byType = new Map<string, EmployeeDocument[]>()
    for (const doc of docs) {
      const bucket = byType.get(doc.documentTypeId)
      if (bucket === undefined) byType.set(doc.documentTypeId, [doc])
      else bucket.push(doc)
    }

    // Har bir guruh ichida — yangisidan eskisiga (oxirgi yuklangan tepada).
    const sorted = (items: EmployeeDocument[]) =>
      [...items].sort((a, b) => b.uploadedAt.localeCompare(a.uploadedAt))

    const result: DocumentGroup[] = []
    const seen = new Set<string>()

    for (const type of types.data ?? []) {
      const bucket = byType.get(type.id)
      if (bucket === undefined) continue
      seen.add(type.id)
      result.push({ typeId: type.id, typeName: type.name, items: sorted(bucket) })
    }

    // Faolsizlantirilgan (yoki hali yuklanmagan) turga tegishli hujjatlar —
    // nomi hujjatning o'zidan (`documentTypeName`), ro'yxat oxirida.
    const leftover = [...byType.entries()].filter(([id]) => !seen.has(id))
    leftover.sort((a, b) => (a[1][0]?.documentTypeName ?? '').localeCompare(b[1][0]?.documentTypeName ?? ''))
    for (const [typeId, bucket] of leftover) {
      result.push({ typeId, typeName: bucket[0]?.documentTypeName ?? '—', items: sorted(bucket) })
    }

    return result
  }, [list.data, types.data])

  /*
    Ko'rish oynasida ← / → — ekrandagi tartibda (guruhlar ketma-ket, ichida yangisidan
    eskisiga): HR qaysi ketma-ketlikni ko'rib turgan bo'lsa, oyna ham shu bilan yuradi.
  */
  const ordered = useMemo(() => groups.flatMap((group) => group.items), [groups])
  const viewer = useViewerList(ordered)

  const loading = list.isPending || types.isPending
  const total = (list.data ?? EMPTY).length

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('documents.subtitle', { n: total })}
        </p>

        {canManage && (
          <Button type="button" size="sm" variant="create" onClick={() => setCreating(true)}>
            <Plus />
            {t('documents.add')}
          </Button>
        )}
      </div>

      {download.isError && (
        <Alert tone="danger">{errorMessage(employeeErrorCode(download.error))}</Alert>
      )}

      {loading ? (
        <Card>
          <div className="space-y-3 p-5">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </div>
        </Card>
      ) : groups.length === 0 ? (
        <Card>
          <EmptyState
            icon={Paperclip}
            title={t('documents.emptyTitle')}
            description={t('documents.emptyText')}
            action={
              canManage ? (
                <Button type="button" size="sm" variant="create" onClick={() => setCreating(true)}>
                  <Plus />
                  {t('documents.add')}
                </Button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        groups.map((group) => (
          <Card key={group.typeId}>
            <CardHeader
              title={group.typeName}
              subtitle={t('documents.count', { n: group.items.length })}
            />
            <CardDivider />
            <ul className="divide-y divide-[var(--border)]">
              {group.items.map((doc) => (
                <li key={doc.id} className="flex items-start gap-3 px-5 py-4">
                  {/*
                    ⭐ Hujjatga bosish — uni KO'RISH (egasi, 2026-10-30): to'liq ekranli oyna,
                    yuklab olmasdan. Tugma nom va ikonani qamraydi, amallar tugmalari tashqarida
                    (tugma ichida tugma bo'lmasin — `candidate-files-card.tsx` dagi qoida).
                  */}
                  <button
                    type="button"
                    onClick={() => viewer.open(doc.id)}
                    title={t('viewer.open')}
                    className="press group flex min-w-0 flex-1 items-start gap-3 text-left"
                  >
                    <span className="relative shrink-0">
                      <IconTile icon={iconFor(doc.contentType)} tone="gray" size="sm" />
                      <span
                        aria-hidden
                        className="absolute inset-0 grid place-items-center rounded-md bg-black/45 opacity-0 transition-opacity duration-200 ease-[var(--ease-ios)] group-hover:opacity-100 group-focus-visible:opacity-100"
                      >
                        <Eye className="size-3.5 text-white" strokeWidth={2.5} />
                      </span>
                    </span>

                    <span className="min-w-0 flex-1">
                      <span className="truncate-1 block text-subhead font-medium" title={doc.fileName}>
                        {doc.fileName}
                      </span>
                      <span className="tabular mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
                        {sizeText(doc.sizeBytes, num)} · {dateTime(doc.uploadedAt)}
                      </span>
                      {doc.note !== null && (
                        <span className="mt-1.5 block text-footnote text-[var(--fg-secondary)]">{doc.note}</span>
                      )}
                    </span>
                  </button>

                  <div className="flex shrink-0 items-center gap-1.5">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      title={t('documents.download')}
                      aria-label={t('documents.download')}
                      disabled={download.isPending && download.variables?.id === doc.id}
                      onClick={() => download.mutate(doc)}
                    >
                      {download.isPending && download.variables?.id === doc.id ? (
                        <LoaderCircle className="animate-spin" />
                      ) : (
                        <Download />
                      )}
                    </Button>

                    {canManage && (
                      <>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-sm"
                          title={t('common.edit')}
                          aria-label={t('common.edit')}
                          onClick={() => setEditing(doc)}
                        >
                          <Pencil />
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-sm"
                          title={t('common.delete')}
                          aria-label={t('common.delete')}
                          onClick={() => setDeleting(doc)}
                        >
                          <Trash2 className="text-[var(--danger-text)]" />
                        </Button>
                      </>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          </Card>
        ))
      )}

      <DocumentDialog
        employeeId={employeeId}
        mode={creating ? 'create' : editing !== null ? 'edit' : null}
        item={editing}
        onClose={() => {
          setCreating(false)
          setEditing(null)
        }}
      />

      <DeleteDocumentDialog employeeId={employeeId} item={deleting} onClose={() => setDeleting(null)} />

      <DocumentViewer
        employeeId={employeeId}
        doc={viewer.current}
        onClose={viewer.close}
        position={viewer.position}
        onPrevious={viewer.onPrevious}
        onNext={viewer.onNext}
        onDownload={(doc) => download.mutate(doc)}
        downloading={download.isPending && download.variables?.id === viewer.current?.id}
      />
    </div>
  )
}

/** Hujjat — to'liq ekranli ko'rish oynasida; sarlavha ostida hujjat turi va izohi. */
function DocumentViewer({
  employeeId,
  doc,
  onClose,
  position,
  onPrevious,
  onNext,
  onDownload,
  downloading,
}: {
  employeeId: string
  doc: EmployeeDocument | null
  onClose: () => void
  position: { index: number; total: number } | undefined
  onPrevious: (() => void) | undefined
  onNext: (() => void) | undefined
  /** Tabning o'z mutatsiyasi — xatosi tab tepasidagi xato qatorida chiqadi. */
  onDownload: (doc: EmployeeDocument) => void
  downloading: boolean
}) {
  const { num, dateTime } = useLocale()
  const file = useDocumentFileBlob(employeeId, doc?.id ?? null)

  return (
    <FileViewer
      open={doc !== null}
      onClose={onClose}
      title={doc?.fileName ?? ''}
      subtitle={
        doc === null
          ? undefined
          : [doc.documentTypeName, sizeText(doc.sizeBytes, num), dateTime(doc.uploadedAt), doc.note]
              .filter((part) => part !== null && part !== '')
              .join(' · ')
      }
      kind={viewerKind(doc?.contentType)}
      blob={file.data ?? null}
      error={file.isError}
      onDownload={doc === null ? undefined : () => onDownload(doc)}
      downloading={downloading}
      position={position}
      onPrevious={onPrevious}
      onNext={onNext}
    />
  )
}

/** PDF va rasm — ikkitagina qabul qilinadigan toifa, shu sababli ikkita ikona kifoya. */
function iconFor(contentType: string): LucideIcon {
  return contentType.startsWith('image/') ? ImageIcon : FileText
}

function sizeText(bytes: number, num: (v: number) => string): string {
  const { value, unit } = fileSizeParts(bytes)
  return `${num(value)} ${unit}`
}

/**
 * O'chirishni tasdiqlash.
 *
 * Fayl bazadan HAM, saqlash joyidan HAM butunlay ketadi (qattiq
 * o'chirish — yumshoq emas, incentives'dagidan farqli): shu sababli
 * tasdiqlash matni ham «qaytarib bo'lmaydi» deb aniq ogohlantiradi.
 */
function DeleteDocumentDialog({
  employeeId,
  item,
  onClose,
}: {
  employeeId: string
  item: EmployeeDocument | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const del = useDeleteDocument(employeeId)

  // Oldingi yozuvning xato holati keyingisiga o'tib qolmasin.
  useEffect(() => {
    if (item !== null) del.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!del.isPending}
      title={t('documents.deleteTitle')}
      description={item.fileName}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={del.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={() => del.mutate(item.id, { onSuccess: onClose })}
            disabled={del.isPending}
          >
            {del.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {del.isError && <Alert tone="danger">{errorMessage(employeeErrorCode(del.error))}</Alert>}
        <p className="text-subhead text-[var(--fg-secondary)]">{t('documents.deleteConfirm')}</p>
      </div>
    </Dialog>
  )
}
