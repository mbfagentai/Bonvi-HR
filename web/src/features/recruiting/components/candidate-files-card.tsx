import {
  Download,
  Expand,
  FileText,
  Image as ImageIcon,
  LoaderCircle,
  Trash2,
  Upload,
  type LucideIcon,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import {
  Alert,
  Button,
  Card,
  CardDivider,
  CardHeader,
  Dialog,
  IconTile,
  Skeleton,
} from '@/shared/ui'
import type { Candidate, CandidateFile } from '../api/recruiting-api'
import {
  useCandidateFileBlob,
  useDeleteCandidateFile,
  useDownloadCandidateFile,
  useUploadCandidateFile,
} from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import {
  MAX_CANDIDATE_FILES,
  RESUME_ACCEPT,
  resumeFileError,
  resumeSizeParts,
} from '../lib/resume-file'

/**
 * ⭐ Nomzodga biriktirilgan FAYLLAR — o'ntagacha.
 *
 * Bu karta bitta rezyume kartasining (`candidate-resume-card.tsx`)
 * o'rniga keldi. Sabab bevosita mijozdan: pasport yoyilmasi, diplom va
 * mehnat daftarchasi telefonda 3-4 ta JPG bo'lib skanerlanadi, bitta
 * o'rin esa HR ni «qaysi birini qoldiraman?» degan tanlovga majbur
 * qilardi. Endi ro'yxat — va u ARZON o'qilishi kerak: nom, hajm, sana,
 * ikkita amal, boshqa hech narsa.
 *
 * <b>Tartib SERVERNIKI</b> — yuklangan tartibda (eskisidan yangisiga) va
 * bu yerda QAYTA SARALANMAYDI. Pasport 1-2-3 varaq bo'lib yuklanadi;
 * xodim hujjatlaridagidek teskari qilinsa karta o'qilmas holga tushadi
 * (SPEC §4.3.4). Ikki xil tartib bo'lmasligi kerak.
 *
 * <b>Rasmga bosish — uni KATTA KO'RISH, yuklash emas.</b> Bu qoida
 * loyihada allaqachon bor (`candidate-photo-editor.tsx`, 2026-09-08
 * talabi: HR suratni kattaroq ko'rmoqchi bo'lib bosardi va o'zini fayl
 * tanlash oynasida topib olardi) va endi biriktirilgan RASMLARGA ham
 * tarqaldi. PDF esa aksincha, YUKLAB OLINADI: ilovada PDF ko'ruvchi yo'q
 * va brauzer/tizim yuklangan faylni o'zi ochadi — ya'ni PDF uchun yuklab
 * olish ko'rish ehtiyojini ham qamrab oladi (`employee-documents-tab.tsx`
 * dagi bilan bir xil mulohaza).
 *
 * <b>Ro'yxatda MINIATYURA yo'q</b> va bu ataylab: o'nta miniatyura o'nta
 * autentifikatsiyalangan so'rov degani va u karta ochilganda ekrandagi
 * eng sekin narsa bo'lardi. Karta — fayllar RO'YXATI, galereya emas
 * (SPEC §5.4.1).
 *
 * <b>Nega `<a href>` emas.</b> Tizim `Authorization: Bearer` sarlavhasi
 * bilan ishlaydi, cookie yo'q — brauzer oddiy havolaga (`<a>`, `<img>`)
 * sarlavha qo'sha olmaydi va server 401 qaytaradi. Shu sababli fayl ham,
 * oynadagi rasm ham `blob` bo'lib olinadi: biri `saveBlobAs` bilan
 * saqlanadi, ikkinchisi `useObjectUrl` bilan ko'rsatiladi.
 *
 * Ruxsat: ro'yxat, yuklab olish va rasm oynasi — `recruiting.view`
 * (ya'ni kartani ochgan har kim); «Fayl qo'shish» va o'chirish —
 * `recruiting.manage` (`canManage`). Ruxsati yo'q foydalanuvchiga tugma
 * KO'RSATILMAYDI: o'chirilgan tugma «senga ruxsat yo'q» deb aytishning
 * eng yomon usuli.
 */
export function CandidateFilesCard({
  candidate,
  canManage,
}: {
  candidate: Candidate
  canManage: boolean
}) {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()

  const upload = useUploadCandidateFile()
  const download = useDownloadCandidateFile()

  const inputRef = useRef<HTMLInputElement>(null)
  /**
   * Yuklashda yiqilgan fayllar — NOMI bilan.
   *
   * NEGA mutatsiyaning o'z `isError` holati yetmaydi: bitta tanlovda
   * bir nechta fayl bo'ladi va ular KETMA-KET yuklanadi (server bitta
   * so'rovda bitta fayl qabul qiladi). Mutatsiya esa faqat OXIRGI
   * so'rovning holatini biladi — «qaysi fayl yiqildi» savoliga javob
   * beradigan joy shu ro'yxat.
   */
  const [failures, setFailures] = useState<UploadFailure[]>([])
  /** Ketma-ket yuklash davom etmoqda. Mutatsiyaning `isPending` i so'rovlar orasida uzilib turadi. */
  const [uploading, setUploading] = useState(false)
  const [deleting, setDeleting] = useState<CandidateFile | null>(null)
  const [viewing, setViewing] = useState<CandidateFile | null>(null)

  /*
    Nomzod almashsa (kartadan kartaga o'tish) oldingi holat qolib
    ketmasin: xato ro'yxati ham, ochiq oynalar ham. `download` —
    mutatsiya, ya'ni xato holati o'zi ketmaydi; o'chirish va rasm
    oynalari esa o'z hook'lari bilan birga ajratiladi.
  */
  useEffect(() => {
    setFailures([])
    setDeleting(null)
    setViewing(null)
    download.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [candidate.id])

  const files = candidate.files
  const full = files.length >= MAX_CANDIDATE_FILES

  /**
   * Tanlangan fayllarni navbat bilan yuklaydi.
   *
   * Uch qadam va tartibi muhim: avval SIG'ADIGANINI ajratish, keyin
   * turini/hajmini tekshirish, oxirida yuklash. Shunda server bilan
   * gaplashmasdan aytiladigan hamma narsa DARHOL aytiladi.
   */
  const pick = async (picked: FileList | null) => {
    if (picked === null || picked.length === 0) return

    const chosen = Array.from(picked)

    /*
      O'nta chegara KLIENTDA ham qo'llanadi (SPEC §5.4).

      Sabab: HR 15 ta fayl tanlab, o'ntasi yuklangach oxirgi beshtasida
      xato ko'rishdan ko'ra, ortiqchasini DARHOL nomi bilan eshitishi
      ma'qul. Kod serverning O'ZI qaytaradigan kod — `nomzod.rezyume_kop`
      — ya'ni yangi tarjima kaliti kerak emas va xato ikki joyda bir xil
      so'z bilan aytiladi (`resume-file.ts` dagi naqsh).

      ⚠️ UMUMIY HAJM (serverda 60 MB) bu yerda TEKSHIRILMAYDI: klient
      saqlanadigan hajmni bila olmaydi, chunki server rasmlarni
      kichiklashtiradi (5 MB telefon surati diskda ~300 KB). Klient
      yig'indisi server bajonidil qabul qiladigan yuklashni rad etardi —
      sabab `resume-file.ts` da batafsil.
    */
    const room = Math.max(MAX_CANDIDATE_FILES - files.length, 0)
    const problems: UploadFailure[] = chosen
      .slice(room)
      .map((file) => ({ fileName: file.name, code: 'nomzod.rezyume_kop' }))

    const queue: File[] = []
    for (const file of chosen.slice(0, room)) {
      // Tur va hajm — serverga bormasdan. 20 MB fayl chiqib, u yerdan 400
      // bilan qaytguncha mobil internetda ancha vaqt ketadi.
      const problem = resumeFileError(file)
      if (problem === null) queue.push(file)
      else problems.push({ fileName: file.name, code: problem })
    }

    setFailures(problems)
    if (queue.length === 0) return

    /*
      BITTA so'rov — BITTA fayl, ketma-ket.

      Bitta faylning yiqilishi qolganlarini TO'XTATMAYDI: uchta varaqdan
      ikkitasi tushib, bittasi tushmasa — HR o'sha bittasini qaytadan
      tanlaydi, hammasini emas. Parallel yuborilmadi: o'nta katta rasm bir
      vaqtda ketsa mobil ulanish bo'g'iladi va xatolar aralashib ketardi.

      Alohida «progress» ko'rsatkichi YOZILMADI: har muvaffaqiyatli
      yuklashdan keyin `providers.tsx` butun keshni bekor qiladi, ya'ni
      yangi qator ro'yxatda O'ZI paydo bo'ladi — bu eng halol progress.
    */
    setUploading(true)
    // `entries()` — indeks kerak (navbatning qolgan qismini nomlash uchun),
    // `queue[index]` esa `noUncheckedIndexedAccess` da `undefined` bo'lib chiqardi.
    for (const [index, file] of queue.entries()) {
      try {
        await upload.mutateAsync({ id: candidate.id, file })
      } catch (error) {
        const code = recruitingErrorCode(error)
        problems.push({ fileName: file.name, code })

        /*
          «Joy yo'q» turidagi ikki xato — navbatning OXIRI: qolgan
          fayllar ham aynan shu javobni oladi, ya'ni so'rov yuborish
          bekor. Lekin qaysi fayllar qolib ketgani HR ga aytiladi —
          jimgina tashlab ketish eng yomon variant.
        */
        if (code === 'nomzod.rezyume_kop' || code === 'nomzod.rezyume_joy') {
          for (const rest of queue.slice(index + 1)) {
            problems.push({ fileName: rest.name, code })
          }
          setFailures([...problems])
          break
        }

        setFailures([...problems])
      }
    }
    setUploading(false)
  }

  const startDownload = (file: CandidateFile) =>
    download.mutate({
      id: candidate.id,
      fileId: file.id,
      // Sarlavha kesilgan bo'lsa (proksi, CORS) — saqlangan nom bilan
      // saqlanadi, «download» degan nomsiz fayl emas.
      fallbackName: file.fileName,
    })

  return (
    <Card>
      <CardHeader
        title={t('recruiting.filesTitle')}
        /*
          Bo'sh holat SARLAVHA OSTIDA, katta `EmptyState` emas: bu karta
          nomzod sahifasidagi kartalar ustunida turadi va «fayl yo'q»
          degan baland blok ostidagi bosqich qatorini ekrandan surib
          yuborardi. Fayl yo'qligi — odatiy hol, xato emas.
        */
        subtitle={
          files.length === 0
            ? t('recruiting.filesEmpty')
            : t('recruiting.filesCount', { n: files.length, max: MAX_CANDIDATE_FILES })
        }
        action={
          canManage ? (
            <>
              {/*
                Fayl tanlash maydoni yashirin: brauzerning standart
                `<input type="file">` qobig'i tizimning qolgan
                tugmalariga umuman o'xshamaydi. Bosish tugmadan
                yuboriladi (`document-dialog.tsx` dagi bilan bir xil).

                `multiple` — HR ning imo-ishorasi o'zgarmadi: uchta
                varaqni bir marta tanlaydi. So'rovlarga bo'lib yuborish
                bu yerning ishi, foydalanuvchining emas.
              */}
              <input
                ref={inputRef}
                type="file"
                accept={RESUME_ACCEPT}
                multiple
                className="hidden"
                tabIndex={-1}
                aria-hidden
                onChange={(event) => {
                  const list = event.target.files
                  // Qiymat DARHOL tozalanadi: aks holda aynan o'sha
                  // fayllarni ikkinchi marta tanlash hodisa hosil qilmaydi.
                  event.target.value = ''
                  void pick(list)
                }}
              />

              <Button
                type="button"
                size="sm"
                variant="secondary"
                // O'nta to'lgan bo'lsa tugma O'CHIRILADI — sababi darhol
                // yonida yozilgan (`filesFullHint`), ya'ni «bosdim,
                // ishlamadi» degan jim holat yo'q.
                disabled={uploading || full}
                onClick={() => inputRef.current?.click()}
              >
                {uploading ? <LoaderCircle className="animate-spin" /> : <Upload />}
                {t('recruiting.filesAdd')}
              </Button>
            </>
          ) : undefined
        }
      />

      {/*
        Qoidalar — FAQAT yuklash huquqi borga: ko'rish huquqi bo'lgan
        foydalanuvchi uchun bu matn bajarib bo'lmaydigan ko'rsatma.

        To'lgan holatda qoidalar o'rniga chegara aytiladi: ikkita izoh
        qatori yonma-yon turgandan ko'ra, o'sha paytda AMALDA bo'lgan
        bittasi turgani ma'qul.
      */}
      {canManage && (
        <p className="px-5 pb-4 text-footnote text-[var(--fg-secondary)]">
          {full ? t('recruiting.filesFullHint') : t('recruiting.filesUploadHint')}
        </p>
      )}

      {(failures.length > 0 || download.isError) && (
        <div className="space-y-2 px-5 pb-4">
          {/*
            Har bir yiqilgan fayl — ALOHIDA qator: «qaysi fayl» va «nima
            uchun» bitta joyda. Bitta umumiy «yuklab bo'lmadi» xabari
            uchta varaqdan qaysi biri qolganini yashirardi.
          */}
          {failures.map((failure, index) => (
            <Alert key={`${index}:${failure.fileName}`} tone="danger">
              {failure.fileName} — {errorMessage(failure.code)}
            </Alert>
          ))}

          {download.isError && (
            <Alert tone="danger">{errorMessage(recruitingErrorCode(download.error))}</Alert>
          )}
        </div>
      )}

      {files.length > 0 && (
        <>
          <CardDivider />
          <ul className="divide-y divide-[var(--border)]">
            {files.map((file) => {
              const image = isImage(file.contentType)
              const busy = download.isPending && download.variables?.fileId === file.id

              return (
                <li key={file.id} className="flex items-start gap-3 px-5 py-4">
                  {/*
                    Qatorning O'ZI bosiladi: rasm — oynada ochiladi, PDF —
                    yuklab olinadi. Tugma faqat NOM va IKONANI qamraydi,
                    butun qatorni emas — amallar tugmalari uning ichida
                    qolsa, tugma ichida tugma bo'lib qolardi (HTML buzilishi
                    va klaviatura bilan yurishning chalkashishi).
                  */}
                  <button
                    type="button"
                    onClick={() => (image ? setViewing(file) : startDownload(file))}
                    aria-label={image ? t('photo.view') : t('documents.download')}
                    className="press group flex min-w-0 flex-1 items-start gap-3 text-left"
                  >
                    <span className="relative shrink-0">
                      <IconTile icon={iconFor(file.contentType)} tone="gray" size="sm" />

                      {/* Sichqoncha ostidagi qatlam — nima bo'lishini oldindan aytadi
                          (`candidate-photo-editor.tsx` dagi bilan bir xil naqsh). */}
                      <span
                        aria-hidden
                        className={cn(
                          'absolute inset-0 grid place-items-center rounded-md bg-black/45',
                          'opacity-0 transition-opacity duration-200 ease-[var(--ease-ios)]',
                          'group-hover:opacity-100 group-focus-visible:opacity-100',
                        )}
                      >
                        {image ? (
                          <Expand className="size-3.5 text-white" strokeWidth={2.5} />
                        ) : (
                          <Download className="size-3.5 text-white" strokeWidth={2.5} />
                        )}
                      </span>
                    </span>

                    <span className="min-w-0 flex-1">
                      <span className="truncate-1 block text-subhead font-medium" title={file.fileName}>
                        {file.fileName}
                      </span>
                      {/*
                        Hajm — SAQLANGAN hajm: server rasmni kichiklashtirib
                        yozadi, ya'ni 4 MB telefon surati bu yerda ~300 KB
                        bo'lib ko'rinadi va bu YOLG'ON emas, aynan diskdagi
                        haqiqat (SPEC §2.2.2). `0` — noma'lum (eski bitta
                        rezyumedan ko'chirilgan qatorda hajm saqlanmagan).

                        Yuklangan VAQT esa endi BOR: bitta rezyume paytida
                        `CandidateDto` da bunday maydon yo'q edi va karta
                        ataylab hech narsa ko'rsatmasdi — `candidate_files`
                        da `created_at` bor.
                      */}
                      <span className="tabular mt-0.5 block text-caption2 text-[var(--fg-tertiary)]">
                        {sizeText(file.sizeBytes, num)} · {dateTime(file.uploadedAt)}
                      </span>
                    </span>
                  </button>

                  <div className="flex shrink-0 items-center gap-1.5">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      title={t('documents.download')}
                      aria-label={t('documents.download')}
                      // Faqat SHU qator kutadi: bitta mutatsiya hamma
                      // qatorga xizmat qiladi, shu sababli qaysi fayl
                      // so'ralgani `variables` dan aniqlanadi
                      // (`employee-documents-tab.tsx` dagi bilan bir xil).
                      disabled={busy}
                      onClick={() => startDownload(file)}
                    >
                      {busy ? <LoaderCircle className="animate-spin" /> : <Download />}
                    </Button>

                    {canManage && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-sm"
                        title={t('common.delete')}
                        aria-label={t('common.delete')}
                        onClick={() => setDeleting(file)}
                      >
                        <Trash2 className="text-[var(--danger-text)]" />
                      </Button>
                    )}
                  </div>
                </li>
              )
            })}
          </ul>
        </>
      )}

      <FileImageDialog
        candidateId={candidate.id}
        file={viewing}
        onClose={() => setViewing(null)}
      />

      <DeleteFileDialog
        candidateId={candidate.id}
        file={deleting}
        onClose={() => setDeleting(null)}
      />
    </Card>
  )
}

/** Yuklashda yiqilgan bitta fayl: nomi va TARJIMA QILINADIGAN xato kodi. */
interface UploadFailure {
  fileName: string
  code: string
}

/**
 * Biriktirilgan RASMNI katta ko'rish.
 *
 * Baytlar `useCandidateFileBlob` dan keladi — kalit ildizi `-photo` bilan
 * tugaydi, ya'ni kartada yozilgan har bir izoh ochiq turgan rasmni qaytadan
 * yuklashga majbur qilmaydi (sabab `recruitingKeys` da).
 *
 * Rasm TO'RTBURCHAK va to'liq ko'rinadi, kesilmaydi: saqlangan fayl kvadrat
 * emas (nomzod suratidan farqi shu) va oynaning butun maqsadi — varaqni
 * o'qish. Uchta holat ham chiziladi: yuklanish (skelet), xato (tugma esa
 * baribir ishlaydi — faylni yuklab olib ko'rish mumkin) va rasmning o'zi.
 */
function FileImageDialog({
  candidateId,
  file,
  onClose,
}: {
  candidateId: string
  /** `null` — oyna yopiq. So'rov ham yuborilmaydi. */
  file: CandidateFile | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const download = useDownloadCandidateFile()

  /*
    Hook'lar SHARTSIZ chaqiriladi va `null` tekshiruvi ulardan KEYIN
    turadi (`employee-documents-tab.tsx` dagi `DeleteDocumentDialog` bilan
    bir xil): erta qaytish hook'lar sonini o'zgartirib, React ni
    yiqitardi. So'rovni to'xtatib turadigan narsa — `enabled`.
  */
  const blob = useCandidateFileBlob(candidateId, file?.id ?? '', file !== null)
  const src = useObjectUrl(blob.data ?? null)

  if (file === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      title={t('recruiting.filesViewTitle')}
      description={file.fileName}
      footer={
        <Button
          type="button"
          disabled={download.isPending}
          onClick={() =>
            download.mutate({ id: candidateId, fileId: file.id, fallbackName: file.fileName })
          }
        >
          {download.isPending ? <LoaderCircle className="animate-spin" /> : <Download />}
          {t('documents.download')}
        </Button>
      }
    >
      <div className="space-y-4">
        {/*
          Yuklab olishning xatosi rasmni YASHIRMAYDI: ikkisi boshqa-boshqa
          so'rov va ko'rinib turgan rasmni «saqlab bo'lmadi» degan sabab
          bilan olib qo'yish foydalanuvchidan ko'rgan narsasini tortib
          olardi.
        */}
        {download.isError && (
          <Alert tone="danger">{errorMessage(recruitingErrorCode(download.error))}</Alert>
        )}

        {blob.isError ? (
          <Alert tone="danger">{errorMessage(recruitingErrorCode(blob.error))}</Alert>
        ) : src === null ? (
          // Skelet — rasmning O'RNIDA: oyna bo'yi sakramasin.
          <Skeleton className="h-72 w-full" />
        ) : (
          <div className="flex justify-center">
            <img
              src={src}
              alt={file.fileName}
              className="max-h-[70vh] w-auto rounded-md object-contain"
            />
          </div>
        )}
      </div>
    </Dialog>
  )
}

/**
 * O'chirishni tasdiqlash.
 *
 * Fayl bazadan HAM, diskdan HAM butunlay ketadi (QATTIQ o'chirish —
 * xodim hujjatidan farqli, u yumshoq o'chiriladi). Sabab: ko'rinmaydigan
 * yozuv o'nta o'rindan bittasini jimgina band qilib turardi va HR
 * «nega yuklanmaydi?» degan javobsiz savolga qolardi (SPEC §2.2.3).
 * Matn ham shuni aniq aytadi: qaytarib bo'lmaydi.
 *
 * Xato oynaning ICHIDA ko'rsatiladi — foydalanuvchi aynan shu yerga
 * qarab turadi va oyna yopilmaydi, ya'ni qaytadan urinish mumkin.
 */
function DeleteFileDialog({
  candidateId,
  file,
  onClose,
}: {
  candidateId: string
  file: CandidateFile | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const remove = useDeleteCandidateFile()

  // Oldingi faylning xato holati keyingisiga o'tib qolmasin.
  useEffect(() => {
    if (file !== null) remove.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [file])

  if (file === null) return null

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!remove.isPending}
      title={t('recruiting.filesDeleteTitle')}
      description={file.fileName}
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose} disabled={remove.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={remove.isPending}
            onClick={() =>
              remove.mutate({ id: candidateId, fileId: file.id }, { onSuccess: onClose })
            }
          >
            {remove.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.delete')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {remove.isError && (
          <Alert tone="danger">{errorMessage(recruitingErrorCode(remove.error))}</Alert>
        )}
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('recruiting.filesDeleteText')}
        </p>
      </div>
    </Dialog>
  )
}

/**
 * Rasmmi?
 *
 * Server `isImage` bayrog'i qaytarmaydi va kerak ham emas: `contentType`
 * MAZMUNDAN aniqlangan, ya'ni bu tekshiruv kengaytmaga emas, haqiqiy
 * turga tayanadi. Rasm — oynada ochiladi, qolgani (PDF) — yuklab olinadi.
 */
function isImage(contentType: string): boolean {
  return contentType.startsWith('image/')
}

/** PDF va rasm — ikkitagina qabul qilinadigan toifa, shu sababli ikkita ikona kifoya. */
function iconFor(contentType: string): LucideIcon {
  return isImage(contentType) ? ImageIcon : FileText
}

/** `0` — NOMA'LUM hajm (ko'chirilgan eski qator): son o'rniga chiziqcha. */
function sizeText(bytes: number, num: (v: number) => string): string {
  if (bytes === 0) return '—'
  const { value, unit } = resumeSizeParts(bytes)
  return `${num(value)} ${unit}`
}
