import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError, NoAnswerError } from '@/api/errors'
import type { AttendanceDayDto, CheckInResult, MeDto } from '@/api/types'
import { setCheckInBusy } from '@/app/checkin-busy'
import { lastMark, nextDirection } from '@/data/attendance'
import { invalidateAttendance } from '@/data/queries'
import { useFormat } from '@/lib/format'
import {
  permissionState,
  readPosition,
  shouldPrePrompt,
  type GeoFailureKind,
} from '@/lib/geolocation'
import { useErrorText } from '@/lib/use-error-message'
import { useOnline } from '@/lib/use-online'
import { Button } from '@/ui/button'
import { InlineMessage, Skeleton } from '@/ui/feedback'
import { Sheet } from '@/ui/sheet'

/**
 * BELGILASH KARTASI — ilovaning yagona haqiqiy ishi (UX §C).
 *
 * Bitta karta, bitta tugma, yonida raqobatchi yo'q. Tugma HECH QACHON
 * yo'qolmaydi va joyidan siljimaydi: yorlig'i va holati o'zgaradi,
 * o'rni esa yo'q. Kerak bo'lmaganda ko'zdan yo'qoladigan boshqaruv
 * xodimga ilova ishonchsiz degan tuyg'u beradi.
 *
 * Yo'nalishni xodim TANLAMAYDI: ikki tugma 08:30 da «Chiqish»
 * bosishga taklif qilardi, mobil belgi esa tizimdagi eng zaif dalil —
 * uni yana zaiflashtirishning hojati yo'q (SPEC §B).
 */
const BOUNCE_SECONDS = 60
/** Tasdiqdan keyin karta qancha vaqt «Qabul qilindi» bo'lib turadi. */
const SETTLE_MS = 5000

type Phase =
  /** Holat ma'lumotdan chiqariladi: 1–4-holatlar. */
  | { kind: 'idle' }
  | { kind: 'locating' }
  | { kind: 'sending' }
  | { kind: 'accepted'; result: CheckInResult }
  | { kind: 'rejected'; error: ApiError }
  | { kind: 'geo'; failure: GeoFailureKind }
  | { kind: 'noAnswer' }

interface CheckInCardProps {
  me: MeDto | null
  today: AttendanceDayDto | null
  loading: boolean
  /** `/api/me` yiqilgan — tugma turadi, lekin o'chirilgan. */
  loadFailed: boolean
  onReload: () => void
}

export function CheckInCard({ me, today, loading, loadFailed, onReload }: CheckInCardProps) {
  const { t } = useTranslation()
  const format = useFormat()
  const navigate = useNavigate()
  const online = useOnline()
  const errorText = useErrorText()

  const [phase, setPhase] = useState<Phase>({ kind: 'idle' })
  const [countdown, setCountdown] = useState(0)
  const [prePromptOpen, setPrePromptOpen] = useState(false)
  /** Tasdiq qatori — kvitansiya: u kartadan tushib ketmaydi. */
  const [receipt, setReceipt] = useState<CheckInResult | null>(null)

  const settleTimer = useRef<number | null>(null)

  useEffect(
    () => () => {
      if (settleTimer.current !== null) window.clearTimeout(settleTimer.current)
      setCheckInBusy(false)
    },
    [],
  )

  // 60 soniyalik sanoq — `AttendanceRules.BounceSeconds` bilan bir xil.
  // Bu mijoz `davomat.takroriy_belgi` xatosini OLDINI oladi, uni
  // ko'rsatmaydi: sekin tarmoqda ikki marta bosish — odatiy hol, va
  // odatiy amalga xato qaytarish dizayn nuqsoni.
  useEffect(() => {
    if (countdown <= 0) return undefined

    const timer = window.setInterval(() => {
      setCountdown((value) => (value <= 1 ? 0 : value - 1))
    }, 1000)

    return () => window.clearInterval(timer)
  }, [countdown])

  const direction = nextDirection(today)
  const allowed = me ? me.mobileCheckInAllowed : false
  const last = lastMark(today)

  const busy = phase.kind === 'locating' || phase.kind === 'sending'
  const disabled =
    loading ||
    loadFailed ||
    !allowed ||
    !online ||
    busy ||
    countdown > 0 ||
    (phase.kind === 'rejected' && phase.error.code === 'davomat.mobil_ruxsat_yoq')

  const send = useCallback(async () => {
    setPhase({ kind: 'locating' })

    const position = await readPosition()

    if (!position.ok) {
      setPhase({ kind: 'idle' })

      if (position.kind === 'denied') {
        // Kartada xato matni YO'Q: karta buni tuzata olmaydi, tiklash
        // ekrani esa tuzatadi (UX §C.5).
        navigate('/joylashuv')
        return
      }

      setPhase({ kind: 'geo', failure: position.kind })
      return
    }

    setPhase({ kind: 'sending' })
    setCheckInBusy(true)

    try {
      const result = await api.checkIn({
        direction,
        latitude: position.fix.latitude,
        longitude: position.fix.longitude,
        accuracyMeters: position.fix.accuracyMeters,
      })

      setPhase({ kind: 'accepted', result })
      setReceipt(result)
      setCountdown(BOUNCE_SECONDS)
      invalidateAttendance()
      onReload()

      settleTimer.current = window.setTimeout(() => setPhase({ kind: 'idle' }), SETTLE_MS)
    } catch (cause: unknown) {
      if (cause instanceof NoAnswerError) {
        setPhase({ kind: 'noAnswer' })
      } else {
        const error = ApiError.from(cause)
        setPhase({ kind: 'rejected', error })
        if (error.code === 'davomat.takroriy_belgi') setCountdown(BOUNCE_SECONDS)
      }
    } finally {
      setCheckInBusy(false)
    }
  }, [direction, navigate, onReload])

  const onTap = useCallback(() => {
    void permissionState().then((state) => {
      if (shouldPrePrompt(state)) {
        // Varaq ataylab varaq: uning tugmasi O'ZI jest bo'ladi va
        // brauzerning geolokatsiya so'rovi jest zanjiri ichida qoladi.
        setPrePromptOpen(true)
        return
      }
      void send()
    })
  }, [send])

  const buttonLabel = ((): string => {
    if (phase.kind === 'locating') return t('checkin.buttonLocating')
    if (phase.kind === 'sending') return t('checkin.buttonSending')
    if (countdown > 0) return t('checkin.buttonCountdown', { n: countdown })
    return direction === 'Out' ? t('checkin.buttonOut') : t('checkin.buttonIn')
  })()

  return (
    <section className="rounded-card bg-bg-elevated p-4">
      <StatusLine
        phase={phase}
        today={today}
        loading={loading}
        loadFailed={loadFailed}
        allowed={allowed}
        online={online}
      />

      <div className="my-4">
        <Button
          block
          size="hero"
          busy={busy}
          disabled={disabled}
          onClick={onTap}
          aria-label={countdown > 0 ? t('checkin.countdownAria', { n: countdown }) : undefined}
        >
          {buttonLabel}
        </Button>
      </div>

      <ResultLine
        phase={phase}
        receipt={receipt}
        today={today}
        loading={loading}
        loadFailed={loadFailed}
        allowed={allowed}
        online={online}
        lastMarkTime={last ? format.time(last.occurredAt) : null}
        onReload={onReload}
        onRetry={() => void send()}
        errorText={errorText}
      />

      <Sheet
        open={prePromptOpen}
        title={t('checkin.prePromptTitle')}
        onClose={() => setPrePromptOpen(false)}
      >
        <p className="text-subhead text-fg-secondary">{t('checkin.prePromptText')}</p>

        <div className="mt-4 space-y-2">
          <Button
            block
            onClick={() => {
              setPrePromptOpen(false)
              void send()
            }}
          >
            {t('common.continue')}
          </Button>
          <Button variant="plain" block onClick={() => setPrePromptOpen(false)}>
            {t('common.cancel')}
          </Button>
        </div>
      </Sheet>
    </section>
  )
}

function StatusLine({
  phase,
  today,
  loading,
  loadFailed,
  allowed,
  online,
}: {
  phase: Phase
  today: AttendanceDayDto | null
  loading: boolean
  loadFailed: boolean
  allowed: boolean
  online: boolean
}) {
  const { t } = useTranslation()

  if (loading) return <Skeleton className="h-5 w-44" />
  if (loadFailed) return <p className="text-subhead text-fg">{t('state.loadFailed')}</p>
  if (phase.kind === 'accepted')
    return <p className="text-subhead text-fg">{t('checkin.accepted')}</p>
  if (!online) return <p className="text-subhead text-fg">{t('checkin.offline')}</p>
  if (!allowed) return <p className="text-subhead text-fg">{t('checkin.notAllowed')}</p>

  // 1–3-holatlar MA'LUMOTDAN chiqariladi: bugun belgi yo'q —
  // «Bugun hali belgilamadingiz»; oxirgisi `Kirish` — «Ish
  // kunidasiz»; oxirgisi `Chiqish` — «Ish kuningiz yakunlandi».
  // Uchinchisidan birinchisiga qaytish ODATIY: odamlar tushlikka
  // chiqib, qaytib keladi va bu hech qachon ogohlantirilmaydi.
  const last = lastMark(today)
  const key = !last ? 'checkin.ready' : last.direction === 'In' ? 'checkin.atWork' : 'checkin.done'

  return <p className="text-subhead text-fg">{t(key)}</p>
}

interface ResultLineProps {
  phase: Phase
  receipt: CheckInResult | null
  today: AttendanceDayDto | null
  loading: boolean
  loadFailed: boolean
  allowed: boolean
  online: boolean
  lastMarkTime: string | null
  onReload: () => void
  onRetry: () => void
  errorText: ReturnType<typeof useErrorText>
}

function ResultLine({
  phase,
  receipt,
  today,
  loading,
  loadFailed,
  allowed,
  online,
  lastMarkTime,
  onReload,
  onRetry,
  errorText,
}: ResultLineProps) {
  const { t } = useTranslation()
  const format = useFormat()

  if (loading) return <Skeleton className="h-4 w-56" />

  if (loadFailed) {
    return (
      <InlineMessage
        tone="danger"
        title={t('state.loadFailed')}
        action={
          <Button variant="secondary" size="compact" onClick={onReload}>
            {t('common.retry')}
          </Button>
        }
      >
        {t('state.loadFailedText')}
      </InlineMessage>
    )
  }

  if (phase.kind === 'geo') {
    const titleKey =
      phase.failure === 'timeout'
        ? 'checkinError.geoTimeoutTitle'
        : phase.failure === 'unsupported'
          ? 'checkinError.geoUnsupportedTitle'
          : 'checkinError.geoUnavailableTitle'
    const textKey =
      phase.failure === 'timeout'
        ? 'checkinError.geoTimeoutText'
        : phase.failure === 'unsupported'
          ? 'checkinError.geoUnsupportedText'
          : 'checkinError.geoUnavailableText'

    return (
      <InlineMessage
        tone="warning"
        title={t(titleKey)}
        assertive
        action={
          phase.failure === 'unsupported' ? null : (
            <Button variant="secondary" size="compact" onClick={onRetry}>
              {t('common.retry')}
            </Button>
          )
        }
      >
        {t(textKey)}
      </InlineMessage>
    )
  }

  if (phase.kind === 'noAnswer') {
    /*
      ⚠️ ENG NOZIK HOLAT (UX §C.5). Javob yo'qolgan — belgi BOR
      bo'lishi ham mumkin. Avtomatik qayta urinish ikkinchi o'tish
      yasardi yoki `davomat.takroriy_belgi` ga urilib, xodimga
      YOLG'ON aytardi. Shu sababli tugma «Qayta urinish» emas,
      «Holatni tekshirish».
    */
    return (
      <InlineMessage
        tone="warning"
        title={t('checkinError.noAnswerTitle')}
        assertive
        action={
          <Button variant="secondary" size="compact" onClick={onReload}>
            {t('checkin.checkStatus')}
          </Button>
        }
      >
        {t('checkinError.noAnswerText')}
      </InlineMessage>
    )
  }

  if (phase.kind === 'rejected') {
    const { title, body } = errorText(phase.error)
    const code = phase.error.code

    // «Siz hozir belgilagansiz» — XATO emas, ma'lumot: hech narsa
    // noto'g'ri ketmagan, shu sababli qizil bo'lmaydi va tugmasi yo'q.
    const isRepeat = code === 'davomat.takroriy_belgi'
    const isNoPermission = code === 'davomat.mobil_ruxsat_yoq'

    return (
      <InlineMessage
        tone={isRepeat ? 'info' : 'warning'}
        title={title ?? undefined}
        assertive={!isRepeat}
        action={
          isRepeat ? null : isNoPermission ? (
            <Button variant="secondary" size="compact" onClick={onReload}>
              {t('common.refresh')}
            </Button>
          ) : (
            <Button variant="secondary" size="compact" onClick={onRetry}>
              {t('common.retry')}
            </Button>
          )
        }
      >
        {body}
      </InlineMessage>
    )
  }

  if (phase.kind === 'locating') {
    return (
      <p className="text-footnote text-fg-secondary" role="status" aria-live="polite">
        {t('checkin.hintLocating')}
      </p>
    )
  }

  if (phase.kind === 'sending') return null

  if (phase.kind === 'accepted') {
    return (
      <p className="text-subhead text-fg" role="status" aria-live="polite">
        {t(phase.result.direction === 'Out' ? 'checkin.acceptedOut' : 'checkin.acceptedIn', {
          time: format.time(phase.result.occurredAt),
          site: phase.result.workSiteName,
        })}
      </p>
    )
  }

  if (!online) {
    return <p className="text-footnote text-fg-secondary">{t('checkin.offlineText')}</p>
  }

  if (!allowed) {
    return <p className="text-footnote text-fg-secondary">{t('checkin.notAllowedText')}</p>
  }

  // Tasdiq qatori kartada QOLADI: xodimning «belgi bor» degan yagona
  // isboti — serverning vaqti yozilgan shu jumla (UX §C.2).
  if (receipt) {
    return (
      <p className="text-footnote text-fg-secondary" role="status" aria-live="polite">
        {t(receipt.direction === 'Out' ? 'checkin.acceptedOut' : 'checkin.acceptedIn', {
          time: format.time(receipt.occurredAt),
          site: receipt.workSiteName,
        })}
      </p>
    )
  }

  if (today && today.firstInAt && !today.lastOutAt) {
    return (
      <p className="text-footnote text-fg-secondary">
        {t('checkin.resultIn', {
          time: format.time(today.firstInAt),
          site: t('mark.sourceTerminal'),
        })}
      </p>
    )
  }

  if (today && today.lastOutAt) {
    return (
      <p className="text-footnote text-fg-secondary">
        {t('checkin.resultOut', {
          time: format.time(today.lastOutAt),
          worked: format.duration(today.workedMinutes),
        })}
      </p>
    )
  }

  if (lastMarkTime) {
    return <p className="text-footnote text-fg-secondary">{lastMarkTime}</p>
  }

  if (today && today.planStartAt) {
    return (
      <p className="text-footnote text-fg-secondary">
        {t('checkin.hintSchedule', { time: format.time(today.planStartAt) })}
      </p>
    )
  }

  return <p className="text-footnote text-fg-secondary">{t('checkin.hintNoSchedule')}</p>
}
