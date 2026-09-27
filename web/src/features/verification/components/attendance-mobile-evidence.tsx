import { MapPin, Smartphone } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

import type { AttendancePass } from '../api/attendance-api'

/*
 * ⭐ TELEFON BELGISINING DALILI (SPEC-xodim-ilovasi §F).
 *
 * Telefon belgisi — tizimdagi eng zaif dalil: turniket surati yo'q, faqat
 * telefon aytgan nuqta bor. «U haqiqatan zavodda edimi?» degan savolga HR
 * javobni SHU yerdan oladi: qaysi obyektda, to'siqdan qancha masofada va
 * telefon qanchalik aniq o'lchagan. Sonlar QAROR LAHZASIDAGI — obyekt keyin
 * surilgan yoki radiusi o'zgartirilgan bo'lsa ham, belgi qanday asosda
 * qabul qilingani o'zgarmaydi (server ularni muzlatib saqlaydi).
 *
 * Jadval qatorida — bitta qisqa satr (obyekt · masofa · aniqlik), oynada —
 * to'liq. KOORDINATA faqat oynada: bu odamning qayerda turgani, jadvalda
 * ko'z tushadigan joyda yotmasligi kerak.
 */

type Evidence = NonNullable<AttendancePass['mobile']>

/** Metrni o'qiladigan matnga: «85 m», «1,2 km». */
function useMeters(): (value: number) => string {
  const { t, num } = useLocale()

  return (value) => {
    const meters = Math.max(0, value)

    if (meters >= 1000) {
      return t('attendance.kmShort', { n: num(Math.round(meters / 100) / 10) })
    }

    return t('attendance.metersShort', { n: num(Math.round(meters)) })
  }
}

/**
 * Masofa shaklga qarab o'qiladi: doirada — MARKAZGACHA, ko'pburchakda —
 * CHEGARAGACHA (ichkarida 0 — «hudud ichida»).
 */
function useDistanceText(): (evidence: Evidence) => string {
  const { t } = useLocale()
  const meters = useMeters()

  return (evidence) => {
    if (evidence.shape === 'Polygon') {
      return evidence.distanceMeters <= 0
        ? t('attendance.mobileInsideArea')
        : t('attendance.mobileDistanceEdge', { distance: meters(evidence.distanceMeters) })
    }

    return t('attendance.mobileDistanceCircle', { distance: meters(evidence.distanceMeters) })
  }
}

/**
 * Jadval qatori uchun qisqa satr: «📱 Ombor · markazdan 85 m · ±17 m».
 *
 * Terminal o'tishida (`mobile === null`) hech narsa chizilmaydi — qator
 * tinch turadi.
 */
export function MobileEvidenceLine({ pass, className }: { pass: AttendancePass; className?: string }) {
  const { t } = useLocale()
  const meters = useMeters()
  const distance = useDistanceText()
  const evidence = pass.mobile

  if (evidence === null || evidence === undefined) return null

  const text = [
    evidence.workSiteName,
    distance(evidence),
    `±${meters(evidence.accuracyMeters)}`,
  ].join(' · ')

  return (
    <span
      title={t('attendance.mobileEvidenceTitle')}
      className={cn(
        'mt-0.5 flex min-w-0 items-center gap-1 text-caption2 text-[var(--fg-tertiary)]',
        className,
      )}
    >
      <Smartphone className="size-3 shrink-0" strokeWidth={2} aria-hidden />
      <span className="truncate-1">{text}</span>
    </span>
  )
}

/**
 * Oyna uchun to'liq dalil: obyekt, masofa, ruxsat, aniqlik va koordinata.
 */
export function MobileEvidenceDetails({ pass }: { pass: AttendancePass }) {
  const { t } = useLocale()
  const meters = useMeters()
  const distance = useDistanceText()
  const evidence = pass.mobile

  if (evidence === null || evidence === undefined) return null

  const allowance =
    evidence.shape === 'Polygon'
      ? t('attendance.mobileAllowanceBuffer', { distance: meters(evidence.allowanceMeters) })
      : t('attendance.mobileAllowanceRadius', { distance: meters(evidence.allowanceMeters) })

  const latitude = evidence.latitude.toFixed(6)
  const longitude = evidence.longitude.toFixed(6)

  // OpenStreetMap — ishlab turgan tizimning xaritasi ham shu (Joylashuvlar
  // sahifasi); yangi tashqi xizmat qo'shilmaydi. Havola yangi oynada.
  const mapUrl = `https://www.openstreetmap.org/?mlat=${latitude}&mlon=${longitude}#map=18/${latitude}/${longitude}`

  return (
    <section className="space-y-2">
      <h3 className="flex items-center gap-1.5 text-footnote font-medium text-[var(--fg-secondary)]">
        <Smartphone className="size-4" strokeWidth={2} />
        {t('attendance.mobileEvidenceTitle')}
      </h3>

      <dl className="grid gap-x-6 gap-y-3 rounded-lg bg-[var(--bg-sunken)] p-4 sm:grid-cols-2">
        <Row label={t('attendance.mobileSite')}>{evidence.workSiteName}</Row>
        <Row label={t('attendance.mobileDistance')}>
          <span className="tabular">{distance(evidence)}</span>
        </Row>
        <Row label={t('attendance.mobileAllowance')}>
          <span className="tabular">{allowance}</span>
        </Row>
        <Row label={t('attendance.mobileAccuracy')}>
          <span className="tabular">±{meters(evidence.accuracyMeters)}</span>
        </Row>
        <Row label={t('attendance.mobileCoordinates')}>
          <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span className="tabular font-mono text-footnote">
              {latitude}, {longitude}
            </span>
            <a
              href={mapUrl}
              target="_blank"
              rel="noreferrer noopener"
              className="inline-flex items-center gap-1 text-footnote text-[var(--accent)] hover:underline"
            >
              <MapPin className="size-3.5" strokeWidth={2} />
              {t('attendance.mobileOnMap')}
            </a>
          </span>
        </Row>
      </dl>

      <p className="text-caption1 text-[var(--fg-tertiary)]">{t('attendance.mobileEvidenceHint')}</p>
    </section>
  )
}

/**
 * Tabel katagi oynachasi uchun satr: «Telefondan: 2 ta belgi».
 *
 * Katakda telefon belgisi bo'lmasa — hech narsa: 950 kishining tabelida
 * har katakda «telefondan: 0» yozuvi shovqin bo'lardi.
 */
export function MobileMarksLine({ count }: { count: number | undefined }) {
  const { t, num } = useLocale()

  if (count === undefined || count <= 0) return null

  return (
    <p className="flex items-start gap-2 text-footnote text-[var(--fg-secondary)]">
      <Smartphone className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />
      <span>{t('attendance.mobileMarks', { n: num(count) })}</span>
    </p>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-caption1 text-[var(--fg-tertiary)]">{label}</dt>
      <dd className="mt-0.5 text-subhead">{children}</dd>
    </div>
  )
}
