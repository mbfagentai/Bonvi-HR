import {
  Download,
  FileText,
  Image as ImageIcon,
  LoaderCircle,
  Trash2,
  Upload,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, CardDivider, CardHeader, Dialog, IconTile } from '@/shared/ui'
import type { Candidate } from '../api/recruiting-api'
import {
  useDeleteResume,
  useDownloadResume,
  useUploadResume,
} from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { RESUME_ACCEPT, resumeFileError, resumeSizeParts } from '../lib/resume-file'

/**
 * Nomzod rezyumesi.
 *
 * <b>Nomzodda BITTA rezyume bo'ladi</b> — bu server qoidasi: yangisi
 * eskisini almashtiradi va eski fayl diskdan o'chiriladi. Shu sababli
 * bu yerda ro'yxat emas, BITTA karta: «yuklash» tugmasi mavjud
 * faylni almashtiradi va matn ham shuni aytadi.
 *
 * <b>Ko'rish/yuklab olish — BITTA amal.</b> Backend javobi
 * `Content-Disposition` bilan keladi, ya'ni server niyati aniq:
 * saqlash. Alohida «ko'rish» tugmasi yozish ikkinchi so'rov yo'lini
 * va `blob:` havolasini boshqarishni talab qilardi; brauzer esa
 * yuklab olingan PDF yoki rasmni operatsion tizimning standart
 * dasturida o'zi ochadi — ya'ni yuklab olish ko'rish ehtiyojini ham
 * qamrab oladi (`employee-documents-tab.tsx` dagi bilan AYNAN bir
 * xil mulohaza va u yerda ham shu qaror qabul qilingan).
 *
 * <b>Nega `<a href>` emas.</b> Tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q — brauzer oddiy havolaga
 * sarlavha qo'sha olmaydi va server 401 qaytaradi. Fayl `blob`
 * sifatida olinadi va `saveBlobAs` bilan saqlanadi.
 */
export function CandidateResumeCard({
  candidate,
  canManage,
}: {
  candidate: Candidate
  canManage: boolean
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  const upload = useUploadResume()
  const download = useDownloadResume()
  const remove = useDeleteResume()

  const inputRef = useRef<HTMLInputElement>(null)
  /** Klient tomonda topilgan xato kodi — serverga bormasdan oldin. */
  const [localError, setLocalError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)

  // Nomzod almashsa (kartadan kartaga o'tish) oldingi xato qolib ketmasin.
  useEffect(() => {
    setLocalError(null)
    upload.reset()
    download.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [candidate.id])

  const has = candidate.resumeFileName !== null

  const pick = (file: File | undefined) => {
    if (file === undefined) return

    /*
      Cheklov klientda ham tekshiriladi: 20 MB fayl serverga chiqib,
      u yerdan 400 bilan qaytguncha mobil internetda ancha vaqt ketadi
      (`resume-file.ts` izohi). Server tekshiruvi asosiy himoya bo'lib
      qoladi.
    */
    const error = resumeFileError(file)
    setLocalError(error)
    if (error !== null) return

    upload.mutate({ id: candidate.id, file })
  }

  const failure = localError ?? (upload.isError ? recruitingErrorCode(upload.error) : null)

  return (
    <Card>
      <CardHeader
        title={t('recruiting.resume')}
        subtitle={has ? undefined : t('recruiting.resumeEmpty')}
        action={
          canManage ? (
            <>
              {/*
                Fayl tanlash maydoni yashirin: brauzerning standart
                `<input type="file">` qobig'i tizimning qolgan
                tugmalariga umuman o'xshamaydi. Bosish tugmadan
                yuboriladi (`document-dialog.tsx` dagi bilan bir xil).
              */}
              <input
                ref={inputRef}
                type="file"
                accept={RESUME_ACCEPT}
                className="hidden"
                onChange={(event) => {
                  pick(event.target.files?.[0])
                  // Bir xil faylni ikkinchi marta tanlash ham hodisa
                  // hosil qilishi uchun qiymat tozalanadi.
                  event.target.value = ''
                }}
              />

              <Button
                type="button"
                size="sm"
                variant="secondary"
                disabled={upload.isPending}
                onClick={() => inputRef.current?.click()}
              >
                {upload.isPending ? <LoaderCircle className="animate-spin" /> : <Upload />}
                {has ? t('recruiting.resumeReplace') : t('recruiting.resumeUpload')}
              </Button>
            </>
          ) : undefined
        }
      />

      {(failure !== null || download.isError || remove.isError) && (
        <div className="px-5 pb-4">
          <Alert tone="danger">
            {errorMessage(
              failure ??
                recruitingErrorCode(download.error ?? remove.error),
            )}
          </Alert>
        </div>
      )}

      {has && (
        <>
          <CardDivider />
          <div className="flex items-start gap-3 px-5 py-4">
            <IconTile icon={iconFor(candidate.resumeContentType)} tone="gray" size="sm" />

            <div className="min-w-0 flex-1">
              <p
                className="truncate-1 text-subhead font-medium"
                title={candidate.resumeFileName ?? undefined}
              >
                {candidate.resumeFileName}
              </p>
              {/*
                Yuklangan VAQT ko'rsatilmaydi: `CandidateDto` da bunday
                maydon yo'q (xodim hujjatidan farqli — u yerda
                `uploadedAt` bor). Yaqin turgan `closedAt` ni qo'yish
                mumkin edi, lekin u BOSHQA fakt — nomzodning voronkadan
                chiqqan sanasi. Yolg'on ma'lumot ko'rsatgandan ko'ra
                hech narsa ko'rsatmagan yaxshi.
              */}
              <p className="tabular mt-0.5 text-caption2 text-[var(--fg-tertiary)]">
                {sizeText(candidate.resumeSizeBytes, num)}
              </p>
            </div>

            <div className="flex shrink-0 items-center gap-1.5">
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                title={t('documents.download')}
                aria-label={t('documents.download')}
                disabled={download.isPending}
                onClick={() =>
                  download.mutate({
                    id: candidate.id,
                    // Sarlavha kesilgan bo'lsa (proksi, CORS) — nomzod
                    // ismi bilan saqlanadi, «download» degan nomsiz
                    // fayl emas.
                    fallbackName: candidate.resumeFileName ?? `${candidate.fullName}.pdf`,
                  })
                }
              >
                {download.isPending ? (
                  <LoaderCircle className="animate-spin" />
                ) : (
                  <Download />
                )}
              </Button>

              {canManage && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  title={t('common.delete')}
                  aria-label={t('common.delete')}
                  onClick={() => setDeleting(true)}
                >
                  <Trash2 className="text-[var(--danger-text)]" />
                </Button>
              )}
            </div>
          </div>
        </>
      )}

      {/*
        O'chirishni tasdiqlash. Fayl bazadan HAM, diskdan HAM
        butunlay ketadi (qattiq o'chirish) — matn ham shuni aniq
        aytadi (`employee-documents-tab.tsx` dagi bilan bir xil).
      */}
      <Dialog
        open={deleting}
        onClose={() => setDeleting(false)}
        size="sm"
        dismissible={!remove.isPending}
        title={t('recruiting.resumeDeleteTitle')}
        description={candidate.resumeFileName ?? undefined}
        footer={
          <>
            <Button
              type="button"
              variant="ghost"
              onClick={() => setDeleting(false)}
              disabled={remove.isPending}
            >
              {t('common.cancel')}
            </Button>
            <Button
              type="button"
              variant="danger"
              disabled={remove.isPending}
              onClick={() =>
                remove.mutate(candidate.id, { onSuccess: () => setDeleting(false) })
              }
            >
              {remove.isPending && <LoaderCircle className="animate-spin" />}
              {t('common.delete')}
            </Button>
          </>
        }
      >
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('recruiting.resumeDeleteText')}
        </p>
      </Dialog>
    </Card>
  )
}

/** PDF va rasm — ikkitagina qabul qilinadigan toifa, shu sababli ikkita ikona kifoya. */
function iconFor(contentType: string | null): LucideIcon {
  return contentType !== null && contentType.startsWith('image/') ? ImageIcon : FileText
}

function sizeText(bytes: number | null, num: (v: number) => string): string {
  if (bytes === null) return '—'
  const { value, unit } = resumeSizeParts(bytes)
  return `${num(value)} ${unit}`
}
