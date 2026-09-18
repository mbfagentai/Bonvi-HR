import { Link } from '@tanstack/react-router'
import {
  Clock3,
  ImageOff,
  ScanFace,
  TimerOff,
  TriangleAlert,
  UserRound,
  UserSearch,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'

import { useEmployeePhoto } from '@/features/employees/api/employees-queries'
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Alert, Avatar, Badge, Dialog, Skeleton, buttonVariants } from '@/shared/ui'

import type { AttendancePass, PassDirection } from '../api/attendance-api'
import { useAttendancePass, useAttendancePassPhoto } from '../api/attendance-queries'
import {
  DIRECTION_LOOK,
  METHOD_LABEL_KEY,
  formatPassTime,
} from '../lib/attendance-presentation'

/*
 * ⭐ Bo'limning nomi — «Verifikatsiya» — aynan shu fayldagi bitta
 * harakatdan kelib chiqadi: turniket suratga olgan YUZ va xodim
 * kartasidagi RASM yonma-yon turadi va HR bir qarashda «bu rostdan
 * o'shami» deb tekshiradi. Shu sababli ikkala rasm har doim JUFT
 * chiziladi — jurnal qatorida kichik, oynada katta — va hech qachon
 * yolg'iz emas: bittasining o'zi hech narsani tasdiqlamaydi.
 */

/* ═══════════════════════════════════════════════════ kichik bo'laklar */

/**
 * Turniket surati — jurnal va taxta qatoridagi 48×48 miniatyura.
 *
 * Shakli ATAYLAB profil rasmidan boshqacha: kvadrat, yumaloq burchakli
 * (kamera kadri), profil rasmi esa doira. Ikkalasi yonma-yon turganda
 * «qaysi biri turniketdan, qaysi biri kartadan» degan savol tug'ilmasligi
 * kerak — shakl buni so'zsiz aytadi.
 *
 * Surat yo'q bo'lsa — belgi, lekin SABABI bilan (`title`): qurilma
 * yubormagan va saqlash muddati tugab o'chirilgan — ikki xil gap.
 * Birinchisi qurilma sozlamasini tekshirishga, ikkinchisi hech narsaga
 * undamaydi.
 */
export function PassSnapshot({
  pass,
  className,
}: {
  pass: Pick<AttendancePass, 'id' | 'photoKey' | 'photoPurgedAt'>
  className?: string
}) {
  const { t } = useLocale()
  const photo = useAttendancePassPhoto(pass.id, pass.photoKey)
  const src = useObjectUrl(photo.data ?? null)

  const frame = cn('size-12 shrink-0 rounded-md', className)

  if (src !== null) {
    return (
      <img
        src={src}
        alt={t('attendance.snapshotLabel')}
        loading="lazy"
        className={cn(frame, 'border border-[var(--border)] bg-[var(--bg-sunken)] object-cover')}
      />
    )
  }

  // Kalit bor va so'rov hali ketmoqda — skelet. Kalit yo'q bo'lsa so'rov
  // umuman yuborilmaydi (`useAttendancePassPhoto`) va darhol belgi chiziladi.
  if (pass.photoKey !== null && photo.isPending) {
    return <Skeleton className={frame} />
  }

  const purged = pass.photoPurgedAt !== null
  const label = purged ? t('attendance.photoPurged') : t('attendance.photoMissing')

  return (
    <span
      role="img"
      aria-label={label}
      title={label}
      className={cn(frame, 'grid place-items-center bg-[var(--bg-sunken)] text-[var(--fg-tertiary)]')}
    >
      {purged ? (
        <TimerOff className="size-5" strokeWidth={1.8} />
      ) : (
        <ImageOff className="size-5" strokeWidth={1.8} />
      )}
    </span>
  )
}

/**
 * Juftlik: turniket surati + xodim rasmi (48 px).
 *
 * Moslanmagan o'tishda o'ng tomonda bosh harflar EMAS, «noma'lum shaxs»
 * belgisi turadi: bosh harflar uchun ism kerak, ism esa yo'q — va
 * bo'sh doira «rasm yuklanmagan xodim» bilan chalkashardi.
 */
export function PassFacePair({ pass }: { pass: AttendancePass }) {
  const { t } = useLocale()

  return (
    <span className="flex items-center gap-2">
      <PassSnapshot pass={pass} />

      {pass.employeeId === null ? (
        <span
          role="img"
          aria-label={t('attendance.unknownPerson')}
          title={t('attendance.unknownPerson')}
          className="grid size-12 shrink-0 place-items-center rounded-full border border-dashed border-[var(--border-strong)] text-[var(--fg-tertiary)]"
        >
          <UserSearch className="size-5" strokeWidth={1.8} />
        </span>
      ) : (
        <EmployeePhoto
          employeeId={pass.employeeId}
          photoKey={pass.employeePhotoKey}
          name={pass.employeeName ?? ''}
          size="lg"
        />
      )}
    </span>
  )
}

/** Yo'nalish yorlig'i — rang `DIRECTION_LOOK` da (kirish yashil, chiqish ko'k, noma'lum kulrang). */
export function PassDirectionBadge({ direction }: { direction: PassDirection }) {
  const { t } = useLocale()
  const look = DIRECTION_LOOK[direction]
  const Icon = look.icon

  return (
    <Badge tone={look.tone} size="sm">
      <Icon className="size-3" strokeWidth={2.5} />
      {t(look.labelKey)}
    </Badge>
  )
}

/**
 * Bayroqlar: soati adashgan qurilma va taxmin qilingan vaqt mintaqasi.
 *
 * ⚠️ `skewWarning` SERVERDAN o'qiladi, `clockSkewSeconds` dan qayta
 * hisoblanmaydi (SPEC §6.5): 300 soniyalik chegaraning ikkinchi nusxasi
 * klientda yashamasligi kerak.
 *
 * Sariq, qizil emas (SPEC §8.4): o'tish BARIBIR hisobga olinadi — bu
 * xato emas, «qurilmani tekshiring» degan eslatma.
 */
export function PassFlags({ pass }: { pass: AttendancePass }) {
  const { t } = useLocale()

  if (!pass.skewWarning && !pass.offsetAssumed) {
    return <span className="text-[var(--fg-tertiary)]">—</span>
  }

  return (
    <span className="flex flex-wrap items-center gap-1.5">
      {pass.skewWarning && (
        <Badge
          tone="warning"
          size="sm"
          title={t('attendance.skewMinutes', { n: skewMinutes(pass.clockSkewSeconds) })}
        >
          <TriangleAlert className="size-3" strokeWidth={2.5} />
          {t('attendance.skewWarning')}
        </Badge>
      )}
      {pass.offsetAssumed && (
        <Badge tone="neutral" size="sm">
          <Clock3 className="size-3" strokeWidth={2.5} />
          {t('attendance.offsetAssumed')}
        </Badge>
      )}
    </span>
  )
}

/** Soniyadagi og'ish → butun daqiqa, ishorasiz (yo'nalishi ahamiyatsiz — «farq qiladi»). */
function skewMinutes(seconds: number): number {
  return Math.round(Math.abs(seconds) / 60)
}

/* ═════════════════════════════════════════════════════════════ oyna */

/**
 * O'tishni katta ko'rish — turniket surati va xodim rasmi yonma-yon.
 *
 * Ikki manbadan ochiladi va ikkalasi ham shu bitta komponent:
 *   · jurnal yoki taxta qatori bosilganda — o'tish obyekti allaqachon
 *     qo'lda (`pass`), so'rov YUBORILMAYDI;
 *   · `?passId=` havolasi bilan (ishdan ketgan xodim ogohlantirishi) —
 *     faqat identifikator bor, o'tish `GET /passes/{id}` dan o'qiladi
 *     (SPEC §8.4). Jurnal sahifama-sahifa varaqlanmaydi: o'tish ochiq
 *     filtrda bo'lmasligi mumkin.
 *
 * 404 (`davomat.otish_topilmadi` — saqlash muddati tugab o'chirilgan)
 * oynada TARJIMA QILINGAN sabab bo'lib ko'rinadi, bo'sh rasm bo'lib emas.
 *
 * @param passId `null` — oyna yopiq.
 * @param pass Qatordan kelgan o'tish; `null` bo'lsa identifikator bo'yicha o'qiladi.
 */
export function AttendancePassPhotoDialog({
  passId,
  pass,
  onClose,
}: {
  passId: string | null
  pass: AttendancePass | null
  onClose: () => void
}) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()

  /*
    Hook SHARTSIZ chaqiriladi, so'rovni `enabled` to'xtatadi — erta
    qaytish hook'lar sonini o'zgartirib React ni yiqitardi
    (`candidate-files-card.tsx` dagi `FileImageDialog` bilan bir xil).
  */
  const fetched = useAttendancePass(passId ?? '', passId !== null && pass === null)
  const data = pass ?? fetched.data ?? null

  const title =
    data === null
      ? t('attendance.passesTitle')
      : (data.employeeName ?? t('attendance.unknownPerson'))

  return (
    <Dialog
      open={passId !== null}
      onClose={onClose}
      size="lg"
      title={title}
      description={
        data === null
          ? undefined
          : [dateTime(data.occurredAt), data.deviceName].filter(Boolean).join(' · ')
      }
      footer={
        data?.employeeId != null ? (
          <Link
            to="/xodimlar/$employeeId"
            params={{ employeeId: data.employeeId }}
            className={buttonVariants({ variant: 'secondary' })}
          >
            <UserRound />
            {t('attendance.openEmployee')}
          </Link>
        ) : undefined
      }
    >
      {data === null ? (
        fetched.isError ? (
          <Alert tone="danger">{errorMessage(problemCode(fetched.error))}</Alert>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            <Skeleton className="aspect-square w-full rounded-lg" />
            <Skeleton className="aspect-square w-full rounded-lg" />
          </div>
        )
      ) : (
        <PassDetails pass={data} />
      )}
    </Dialog>
  )
}

function PassDetails({ pass }: { pass: AttendancePass }) {
  const { t, date } = useLocale()

  return (
    <div className="space-y-5">
      {/*
        Ikki rasm bir xil o'lchamda va bir qatorda — ko'z chapdan o'ngga
        bir harakatda solishtiradi. Tor ekranda ustma-ust tushadi, lekin
        tartib o'zgarmaydi: avval turniket, keyin karta.
      */}
      <div className="grid gap-4 sm:grid-cols-2">
        <PhotoPanel label={t('attendance.snapshotLabel')} icon={ScanFace}>
          <LargeSnapshot pass={pass} />
        </PhotoPanel>

        <PhotoPanel label={t('attendance.profilePhotoLabel')} icon={UserRound}>
          <LargeProfilePhoto pass={pass} />
        </PhotoPanel>
      </div>

      <dl className="grid gap-x-6 gap-y-3 rounded-lg bg-[var(--bg-sunken)] p-4 sm:grid-cols-2">
        <Detail label={t('attendance.time')}>
          <span className="tabular font-semibold">{formatPassTime(pass.occurredAt)}</span>
          <span className="tabular ml-2 text-[var(--fg-secondary)]">{date(pass.occurredAt)}</span>
        </Detail>

        <Detail label={t('attendance.direction')}>
          <PassDirectionBadge direction={pass.direction} />
        </Detail>

        <Detail label={t('attendance.device')}>{pass.deviceName ?? '—'}</Detail>

        <Detail label={t('attendance.method')}>{t(METHOD_LABEL_KEY[pass.method])}</Detail>

        <Detail label={t('employee.department')}>{pass.department ?? '—'}</Detail>

        <Detail label={t('employee.position')}>{pass.position ?? '—'}</Detail>

        {/*
          Qurilmadagi shaxs raqami moslangan o'tishda ham ko'rsatiladi:
          noto'g'ri moslikni (boshqa odamga bog'langan raqamni) aynan shu
          qatordan payqash mumkin — yuz bir odamniki, raqam boshqasiniki.
        */}
        <Detail label={t('attendance.personId')}>
          <span className="rounded-xs bg-[var(--bg-elevated)] px-1.5 py-0.5 font-mono text-footnote">
            {pass.rawPersonId}
          </span>
        </Detail>

        <Detail label={t('attendance.skewWarning')}>
          {pass.skewWarning || pass.offsetAssumed ? (
            <span className="space-y-1">
              <PassFlags pass={pass} />
              {pass.skewWarning && (
                <span className="block text-caption1 text-[var(--fg-secondary)]">
                  {t('attendance.skewMinutes', { n: skewMinutes(pass.clockSkewSeconds) })}
                </span>
              )}
            </span>
          ) : (
            '—'
          )}
        </Detail>
      </dl>
    </div>
  )
}

function PhotoPanel({
  label,
  icon: Icon,
  children,
}: {
  label: string
  icon: LucideIcon
  children: ReactNode
}) {
  return (
    <figure className="min-w-0 space-y-2">
      <figcaption className="flex items-center gap-1.5 text-footnote font-medium text-[var(--fg-secondary)]">
        <Icon className="size-4" strokeWidth={2} />
        {label}
      </figcaption>
      {children}
    </figure>
  )
}

/**
 * Turniket surati — katta.
 *
 * `object-contain`, `cover` EMAS: terminal kadri kvadrat emas va yuz
 * ko'pincha kadrning chetida bo'ladi. Kesib tashlash aynan tekshiriladigan
 * narsani — yuzni — kesib yuborishi mumkin edi.
 */
function LargeSnapshot({ pass }: { pass: AttendancePass }) {
  const { t } = useLocale()
  const photo = useAttendancePassPhoto(pass.id, pass.photoKey)
  const src = useObjectUrl(photo.data ?? null)

  if (src !== null) {
    return (
      <img
        src={src}
        alt={t('attendance.snapshotLabel')}
        className="aspect-square w-full rounded-lg bg-[var(--bg-sunken)] object-contain"
      />
    )
  }

  if (pass.photoKey !== null && photo.isPending) {
    return <Skeleton className="aspect-square w-full rounded-lg" />
  }

  const purged = pass.photoPurgedAt !== null

  return (
    <EmptyFrame
      icon={purged ? TimerOff : ImageOff}
      text={purged ? t('attendance.photoPurged') : t('attendance.photoMissing')}
    />
  )
}

/**
 * Xodim kartasidagi rasm — katta.
 *
 * ⭐ Rasm kaliti bor ekan — HAQIQIY RASM, bosh harflar emas (egasining
 * doimiy talabi). Bosh harflar faqat kalit YO'Q bo'lganda chiziladi va
 * ostida «rasm yuklanmagan» deb yoziladi: aks holda HR yuzni harf bilan
 * solishtirishga urinib, «mos emas» deb o'ylashi mumkin edi.
 */
function LargeProfilePhoto({ pass }: { pass: AttendancePass }) {
  const { t } = useLocale()
  const photo = useEmployeePhoto(pass.employeeId ?? '', pass.employeePhotoKey)
  const src = useObjectUrl(photo.data ?? null)

  if (pass.employeeId === null) {
    return <EmptyFrame icon={UserSearch} text={t('attendance.unknownPerson')} />
  }

  if (src !== null) {
    return (
      <img
        src={src}
        alt={pass.employeeName ?? ''}
        className="aspect-square w-full rounded-lg bg-[var(--bg-sunken)] object-cover"
      />
    )
  }

  if (pass.employeePhotoKey !== null && photo.isPending) {
    return <Skeleton className="aspect-square w-full rounded-lg" />
  }

  return (
    <div className="grid aspect-square w-full place-items-center rounded-lg bg-[var(--bg-sunken)]">
      <div className="flex flex-col items-center gap-3 px-6 text-center">
        <Avatar name={pass.employeeName ?? ''} size="xl" />
        <p className="text-footnote text-[var(--fg-secondary)]">{t('photo.none')}</p>
      </div>
    </div>
  )
}

function EmptyFrame({ icon: Icon, text }: { icon: LucideIcon; text: string }) {
  return (
    <div className="grid aspect-square w-full place-items-center rounded-lg bg-[var(--bg-sunken)]">
      <div className="flex flex-col items-center gap-3 px-6 text-center">
        <span className="grid size-14 place-items-center rounded-full bg-[var(--bg-elevated)] shadow-card">
          <Icon className="size-7 text-[var(--fg-tertiary)]" strokeWidth={1.6} />
        </span>
        <p className="text-footnote text-[var(--fg-secondary)]">{text}</p>
      </div>
    </div>
  )
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-caption1 text-[var(--fg-tertiary)]">{label}</dt>
      <dd className="mt-0.5 text-subhead">{children}</dd>
    </div>
  )
}
