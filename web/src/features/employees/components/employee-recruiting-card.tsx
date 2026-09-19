import { Link } from '@tanstack/react-router'
import {
  ArrowRight,
  Download,
  FileText,
  Image as ImageIcon,
  LoaderCircle,
  UserSearch,
} from 'lucide-react'

import { recruitingErrorCode } from '@/features/recruiting/lib/recruiting-error'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, CardDivider, CardHeader, IconTile, buttonVariants } from '@/shared/ui'

import type { EmployeeOriginCandidate } from '../api/employees-api'
import { useDownloadCandidateFile, useEmployeeRecruiting } from '../api/employees-queries'
import { fileSizeParts } from '../lib/document-file'

/**
 * ⭐ «Nomzodlik davri» — havola va fayllar, rekrutingdan KELGAN.
 *
 * <b>Muammo qayerdan keldi.</b> Foydalanuvchi aytgan holat
 * (2026-09-19): nomzodning anketa suratlari fayllar bo'limiga
 * yuklangan, odam sinov muddatiga ishga olingan — va xodim kartasidagi
 * «Hujjatlar» bo'p-bo'sh chiqqan. Sabab oddiy: ishga qabul faqat
 * SHAXSIY maydonlarni ko'chiradi, fayllarni emas. Sinov muddatidagi
 * odam ham xodim, ya'ni uning hujjati xodim kartasida ko'rinishi kerak.
 *
 * <b>Nega NUSXA olinmaydi.</b> Xodim hujjatida TUR majburiy
 * (`documentTypeId`), nomzod faylida esa tur tushunchasi umuman yo'q —
 * avtomatik nusxa har fayl uchun taxmin qilishni talab qilardi va tur
 * bo'yicha guruhlash yolg'onga aylanardi. Shu sababli bu bo'lim
 * fayllarni bor joyidan KO'RSATADI, «Kengash xulosasi» bilan aynan bir
 * xil qaror (`employee-council-section.tsx`).
 *
 * ⭐ <b>Havola fayldan MUSTAQIL</b> — foydalanuvchining ikkinchi talabi
 * (o'sha kun): «hujjat bo'lmasa ham, nomzodlik paytida qanday edi degan
 * savol chiqqanda shu tugmani bosib kirib ko'raversin». Shu sababli
 * fayllar ro'yxati bo'm-bo'sh bo'lsa ham karta CHIZILADI va ichida
 * nomzod kartasiga o'tish tugmasi turadi. Ilgari bo'lim faqat fayl
 * bo'lganda ko'rinardi — va aynan fayli yo'q xodimda nomzodlik tarixiga
 * yo'l topib bo'lmasdi.
 *
 * <b>Qarorning narxi ochiq aytiladi.</b> Fayl rekrutingga tegishli
 * bo'lib qoladi: nomzod kartasidan o'chirilsa, bu yerdan ham yo'qoladi
 * (xodimning O'Z hujjati esa yumshoq o'chiriladi). Shuning uchun
 * sarlavha ostidagi izoh fayllar QAYERDA turganini aniq aytadi — HR
 * ularni xodim hujjati deb o'ylab qolmasligi kerak.
 *
 * <b>FAQAT O'QISH.</b> Bu yerda yuklash ham, o'chirish ham yo'q:
 * fayllarning yagona egasi — nomzod kartasi.
 *
 * <b>Ikkalasi ham bo'sh bo'lsa, karta umuman chizilmaydi</b>: xodimlar
 * ko'pchiligi Excel'dan ko'chirilgan va ularda nomzodlik yozuvi yo'q.
 *
 * Ruxsat: `recruiting.view` — chaqiruvchi tekshiradi.
 */
export function EmployeeRecruitingCard({ employeeId }: { employeeId: string }) {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()

  const origin = useEmployeeRecruiting(employeeId)
  const download = useDownloadCandidateFile()

  const candidates = origin.data?.candidates ?? []
  const files = origin.data?.files ?? []

  /*
    Yuklanayotganda hech narsa chizilmaydi (skelet ham): xodimlarning
    KO'PCHILIGIDA bu bo'lim umuman bo'lmaydi va skelet deyarli har
    kartada bir lahza ko'rinib, keyin yo'qolardi — tabning pastki qismi
    har safar sakrardi. Bo'lim qo'shimcha, joy band qilishga arzimaydi.
  */
  if (origin.isPending || (candidates.length === 0 && files.length === 0)) return null

  return (
    <Card>
      <CardHeader
        title={t('documents.candidateTitle')}
        subtitle={files.length === 0 ? undefined : t('documents.candidateHint')}
      />
      <CardDivider />

      {/*
        Havola TEPADA, fayllardan oldin: u fayl bor-yo'qligidan qat'i
        nazar turadigan yagona narsa va bo'limning asosiy javobi —
        «nomzodlik paytida qanday edi».
      */}
      <div className="flex flex-wrap gap-2 px-5 py-4">
        {candidates.map((candidate) => (
          <CandidateLink key={candidate.id} candidate={candidate} many={candidates.length > 1} />
        ))}
      </div>

      {files.length > 0 && (
        <>
          <CardDivider />

          {download.isError && (
            <div className="px-5 pt-4">
              <Alert tone="danger">{errorMessage(recruitingErrorCode(download.error))}</Alert>
            </div>
          )}

          <ul className="divide-y divide-[var(--border)]">
            {files.map((file) => (
              <li key={file.id} className="flex items-start gap-3 px-5 py-4">
                <IconTile icon={iconFor(file.contentType)} tone="gray" size="sm" />

                <div className="min-w-0 flex-1">
                  <p className="truncate-1 text-subhead font-medium" title={file.fileName}>
                    {file.fileName}
                  </p>
                  <p className="tabular mt-0.5 text-caption2 text-[var(--fg-tertiary)]">
                    {sizeText(file.sizeBytes, num)} · {dateTime(file.uploadedAt)}
                  </p>
                </div>

                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  className="shrink-0"
                  title={t('documents.download')}
                  aria-label={t('documents.download')}
                  disabled={download.isPending && download.variables?.id === file.id}
                  onClick={() => download.mutate(file)}
                >
                  {download.isPending && download.variables?.id === file.id ? (
                    <LoaderCircle className="animate-spin" />
                  ) : (
                    <Download />
                  )}
                </Button>
              </li>
            ))}
          </ul>
        </>
      )}
    </Card>
  )
}

/**
 * Nomzod kartasiga o'tish tugmasi.
 *
 * `<Link>` + `buttonVariants`, `Button` EMAS: bu HAVOLA va u o'ng
 * tugma bilan yangi oynada ochilishi kerak — HR nomzodlik tarixini
 * yonma-yon qo'yib solishtiradi. `<button>` + `navigate` bu imkoniyatni
 * yo'qotardi. Ko'rinish esa tugmanikidek bo'lsin, shu sababli uslub
 * cva'dan olinadi (`settings-dialog.tsx` va `__root.tsx` dagi bilan bir
 * xil naqsh).
 *
 * <b>Yorliq bitta yozuvda ODDIY, ikkitasida ANIQLASHTIRILGAN.</b> Bitta
 * bo'lsa «Nomzod kartasi» yetarli. Ikkita bo'lsa (odam ketib qaytib
 * kelgan) ularni ajratish kerak — e'lon nomi va yopilish sanasi bilan;
 * ular yo'q bo'lsa yorliq baribir tugmalikka yaraydi.
 */
function CandidateLink({
  candidate,
  many,
}: {
  candidate: EmployeeOriginCandidate
  many: boolean
}) {
  const { t, date } = useLocale()

  const detail = many
    ? [candidate.vacancy, candidate.closedAt === null ? null : date(candidate.closedAt)]
        .filter((part): part is string => part !== null && part !== '')
        .join(' · ')
    : ''

  return (
    <Link
      to="/nomzodlar/$candidateId"
      params={{ candidateId: candidate.id }}
      className={buttonVariants({ variant: 'soft', size: 'sm' })}
    >
      <UserSearch />
      {detail === '' ? t('documents.candidateOpen') : `${t('documents.candidateOpen')} · ${detail}`}
      <ArrowRight />
    </Link>
  )
}

/** PDF va rasm — ikkitagina toifa (`employee-documents-tab.tsx` dagi bilan bir xil). */
function iconFor(contentType: string) {
  return contentType.startsWith('image/') ? ImageIcon : FileText
}

/**
 * Hajm `0` bo'lsa — chiziqcha: eski ko'chirilgan qatorlarda hajm
 * saqlanmagan va «0 KB» yolg'on bo'lardi (`CandidateFile.SizeBytes` izohi).
 */
function sizeText(bytes: number, num: (v: number) => string): string {
  if (bytes <= 0) return '—'
  const { value, unit } = fileSizeParts(bytes)
  return `${num(value)} ${unit}`
}
