import {
  ExternalLink,
  FileText,
  LoaderCircle,
  Maximize2,
  Trash2,
  Upload,
} from 'lucide-react'
import { useRef, useState, type ChangeEvent } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Alert, Button, Dialog, Skeleton } from '@/shared/ui'

import {
  useDeleteMehnatUzScreenshot,
  useMehnatUzScreenshot,
  useUploadMehnatUzScreenshot,
} from '../api/employees-queries'
import { DOCUMENT_ACCEPT, documentFileError } from '../lib/document-file'
import { employeeErrorCode } from '../lib/employee-error'
import { EMPLOYEES_EDIT } from '../lib/employee-presentation'

/**
 * mehnat.uz — havola va profil skrinshoti.
 *
 * <b>Nega «Tekshiruvlar» kartasi ichida.</b> Ular bitta savolning uch
 * qismi: «bu odam mehnat.uz da bormi?» degan javob (ha/yo'q/noma'lum),
 * uning MANZILI va uning DALILI. Uchtasi uch joyda tursa, HR javobni
 * ko'rib, isbotini qidirib yurishi kerak bo'lardi.
 *
 * <b>Nega skrinshot xodim rasmi quvuridan o'tmaydi.</b> Xodim rasmi
 * markazdan kvadrat qirqilib 512×512 JPEG bo'ladi — skrinshot uchun bu
 * halokat: uning butun qiymati chetlardagi MATNDA (ism, JSHSHIR, ish
 * joyi). Shu sababli u HUJJAT quvuridan o'tadi: tekshiriladi, lekin
 * qirqilmaydi ham, kichraytirilmaydi ham. To'liq asos backend'da,
 * `Employee.MehnatUzScreenshotKey` izohida.
 *
 * <b>Nega `<img src="/api/...">` emas.</b> Tizim `Authorization`
 * sarlavhasi bilan ishlaydi, brauzer esa uni `<img>` so'roviga qo'sha
 * olmaydi. Fayl `blob` bo'lib olinadi va `URL.createObjectURL` orqali
 * ko'rsatiladi — `employee-photo.tsx` va `employee-documents-tab.tsx`
 * dagi bilan aynan bir xil yo'l.
 */
export function MehnatUzBlock({
  employeeId,
  url,
  screenshotKey,
}: {
  employeeId: string
  /** `null` — havola kiritilmagan. Tahrirlash kartaning formasida. */
  url: string | null
  /** `null` — skrinshot yuklanmagan; so'rov ham yuborilmaydi. */
  screenshotKey: string | null
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(EMPLOYEES_EDIT)

  const inputRef = useRef<HTMLInputElement>(null)

  const [viewing, setViewing] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const screenshot = useMehnatUzScreenshot(employeeId, screenshotKey)
  const upload = useUploadMehnatUzScreenshot(employeeId)
  const remove = useDeleteMehnatUzScreenshot(employeeId)

  const src = useObjectUrl(screenshot.data ?? null)

  /*
   * Tur BLOB dan olinadi, kengaytmadan emas: server faylni qayta
   * kodlamaydi va u PDF ham bo'lishi mumkin. PDF ni `<img>` ga berish
   * singan rasm belgisini chizardi.
   */
  const isPdf = screenshot.data?.type === 'application/pdf'

  const openPicker = () => inputRef.current?.click()

  const onPick = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null

    // Qiymat darhol tozalanadi: aks holda AYNAN o'sha faylni ikkinchi
    // marta tanlash hodisa hosil qilmaydi (`employee-photo-editor` dagi
    // bilan bir xil tuzoq).
    event.target.value = ''
    if (file === null) return

    const problem = documentFileError(file)
    if (problem !== null) {
      setFailure(problem)
      return
    }

    setFailure(null)
    try {
      await upload.mutateAsync(file)
    } catch (error) {
      setFailure(employeeErrorCode(error))
    }
  }

  const confirmDelete = async () => {
    setFailure(null)
    try {
      await remove.mutateAsync()
      setConfirmingDelete(false)
    } catch (error) {
      setFailure(employeeErrorCode(error))
    }
  }

  const hasScreenshot = screenshotKey !== null

  return (
    <div className="space-y-2.5 rounded-md bg-[var(--bg-sunken)] px-3 py-2.5">
      <input
        ref={inputRef}
        type="file"
        accept={DOCUMENT_ACCEPT}
        onChange={(event) => void onPick(event)}
        className="hidden"
        tabIndex={-1}
        aria-hidden
      />

      {/* ───────── Havola ───────── */}
      <div className="flex items-start gap-2.5">
        <ExternalLink
          className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]"
          strokeWidth={2}
        />

        <div className="min-w-0 flex-1">
          <p className="text-footnote font-medium text-[var(--fg-secondary)]">
            {t('employees.mehnatUzLink')}
          </p>

          {url === null ? (
            <p className="mt-0.5 text-subhead text-[var(--fg-tertiary)]">
              {t('employees.mehnatUzNoLink')}
            </p>
          ) : (
            /*
              `noopener noreferrer` — yangi tabda ochilgan sahifa bizning
              oynamizga (`window.opener`) tegmasligi uchun. Havola matni
              ATAYLAB to'liq manzil emas: u ekranni yorib yuborardi.
            */
            <a
              href={url}
              target="_blank"
              rel="noopener noreferrer"
              title={url}
              className="mt-0.5 inline-flex items-center gap-1.5 text-subhead text-[var(--accent-text)] hover:underline"
            >
              {t('employees.mehnatUzOpenProfile')}
              <ExternalLink className="size-3.5" strokeWidth={2.2} />
            </a>
          )}
        </div>
      </div>

      {/* ───────── Skrinshot ───────── */}
      <div className="border-t border-[var(--border)] pt-2.5">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">
          {t('employees.mehnatUzScreenshot')}
        </p>

        {failure !== null && (
          <Alert tone="danger" className="mt-2">
            {errorMessage(failure)}
          </Alert>
        )}

        {!hasScreenshot ? (
          <p className="mt-0.5 text-subhead text-[var(--fg-tertiary)]">
            {t('employees.mehnatUzNoScreenshot')}
          </p>
        ) : screenshot.isPending ? (
          <Skeleton className="mt-2 h-24 w-full" />
        ) : (
          /*
            Ko'rib chiqish DOIM bosiladigan: skrinshotdagi matn kichik
            ko'rinishda o'qilmaydi va uning yagona ma'nosi — o'qilishi.
            Shu sababli kichik nusxa faqat «bor» degan belgi, asosiy
            ko'rinish esa modal oynada.
          */
          <button
            type="button"
            onClick={() => setViewing(true)}
            title={t('employees.mehnatUzZoom')}
            className={cn(
              'press group relative mt-2 block w-full overflow-hidden rounded-md',
              'border border-[var(--border)] bg-[var(--bg-elevated)]',
            )}
          >
            {isPdf || src === null ? (
              <span className="flex items-center gap-2.5 px-3 py-4 text-left">
                <FileText className="size-5 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
                <span className="text-subhead">{t('employees.mehnatUzPdf')}</span>
              </span>
            ) : (
              /*
                `object-top` — `object-cover` bilan birga: skrinshotning
                eng muhim qismi (sarlavha, ism) YUQORIDA turadi, markazdan
                kesish esa aynan uni yo'qotardi.
              */
              <img
                src={src}
                alt={t('employees.mehnatUzScreenshot')}
                className="h-28 w-full object-cover object-top"
              />
            )}

            <span
              aria-hidden
              className={cn(
                'absolute inset-0 grid place-items-center bg-black/45',
                'opacity-0 transition-opacity duration-200 ease-[var(--ease-ios)]',
                'group-hover:opacity-100 group-focus-visible:opacity-100',
              )}
            >
              <Maximize2 className="size-5 text-white" strokeWidth={2} />
            </span>
          </button>
        )}

        {canManage && (
          <div className="mt-2 flex flex-wrap gap-1.5">
            <Button
              type="button"
              size="sm"
              variant="secondary"
              onClick={openPicker}
              disabled={upload.isPending}
            >
              {upload.isPending ? <LoaderCircle className="animate-spin" /> : <Upload />}
              {hasScreenshot ? t('employees.mehnatUzReplace') : t('employees.mehnatUzUpload')}
            </Button>

            {hasScreenshot && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setConfirmingDelete(true)}
                className="text-ios-red hover:bg-ios-red/10 hover:text-ios-red"
              >
                <Trash2 />
                {t('common.delete')}
              </Button>
            )}
          </div>
        )}

        {canManage && (
          <p className="mt-1.5 text-caption2 text-[var(--fg-tertiary)]">
            {t('employees.mehnatUzUploadHint')}
          </p>
        )}
      </div>

      {/* ───────── To'liq ko'rinish ───────── */}
      <Dialog
        open={viewing}
        onClose={() => setViewing(false)}
        size="lg"
        title={t('employees.mehnatUzScreenshot')}
        description={t('employees.mehnatUzZoomHint')}
        footer={
          <Button type="button" onClick={() => setViewing(false)}>
            {t('common.close')}
          </Button>
        }
      >
        {src === null ? (
          <Skeleton className="h-64 w-full" />
        ) : isPdf ? (
          /*
            PDF `<iframe>` da: brauzerning o'z ko'ruvchisi varaqlash va
            kattalashtirishni bepul beradi. Rasm uchun esa u ortiqcha —
            oddiy `<img>` yengilroq va tez chiziladi.
          */
          <iframe
            src={src}
            title={t('employees.mehnatUzScreenshot')}
            className="h-[70vh] w-full rounded-md border border-[var(--border)]"
          />
        ) : (
          /*
            `object-contain` — modal ichida rasm QIRQILMAYDI: bu yerda
            uning butun ma'nosi to'liq o'qilishida.
          */
          <img
            src={src}
            alt={t('employees.mehnatUzScreenshot')}
            className="max-h-[70vh] w-full rounded-md border border-[var(--border)] object-contain"
          />
        )}
      </Dialog>

      {/* ───────── O'chirishni tasdiqlash ───────── */}
      <Dialog
        open={confirmingDelete}
        onClose={() => setConfirmingDelete(false)}
        size="sm"
        dismissible={!remove.isPending}
        title={t('employees.mehnatUzDeleteTitle')}
        footer={
          <>
            <Button
              type="button"
              variant="ghost"
              onClick={() => setConfirmingDelete(false)}
              disabled={remove.isPending}
            >
              {t('common.cancel')}
            </Button>

            <Button
              type="button"
              variant="danger"
              onClick={() => void confirmDelete()}
              disabled={remove.isPending}
            >
              {remove.isPending ? <LoaderCircle className="animate-spin" /> : <Trash2 />}
              {t('common.delete')}
            </Button>
          </>
        }
      >
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('employees.mehnatUzDeleteText')}
        </p>
      </Dialog>
    </div>
  )
}
