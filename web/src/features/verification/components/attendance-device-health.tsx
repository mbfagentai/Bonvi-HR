import { Link } from '@tanstack/react-router'
import { Cpu, Unplug } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Alert } from '@/shared/ui'

import type { AttendanceDeviceHealth, DeviceOutage } from '../api/attendance-api'

/**
 * Qurilmalar sog'lig'i — Statistika va Qurilmalar sahifalarining tepasida
 * (SPEC §8.4, §8.7, SPEC-agent §L).
 *
 * ⭐ <b>Agent yoki terminal uzilishi — QIZIL</b> (egasi, 2026-09-27). Agent
 * o'tish bo'lmasa ham har daqiqada gapiradi, ya'ni o'n daqiqalik jimlik —
 * tunda ham, dam olish kunida ham — haqiqiy uzilish: bu vaqtda o'tishlar
 * Bonvi ga yetmayapti (terminal ularni saqlab turadi) va ishdan ketgan
 * odam terminaldan o'chirilmayapti. Chegarani SERVER hisoblaydi — sahifa
 * faqat ko'rsatadi.
 *
 * `Alert` komponenti ishlatilmagan: u matnni `<p>` ichiga soladi, bu yerda
 * esa qurilmalar ro'yxati kerak (ishdan ketgan xodim paneli bilan bir xil
 * sabab). Ranglar `Alert` ning `danger` ohangi bilan AYNAN bir xil.
 */
export function DeviceOutagePanel({ outages }: { outages: readonly DeviceOutage[] }) {
  const { t, num, dateTime } = useLocale()

  if (outages.length === 0) return null

  return (
    <section role="alert" className="rounded-lg bg-ios-red/10 p-4 sm:p-5">
      <div className="flex items-start gap-3">
        <Unplug className="mt-px size-5 shrink-0 text-ios-red" strokeWidth={2.25} />
        <div className="min-w-0 space-y-1">
          <p className="text-callout font-semibold text-ios-red">
            {t('attendance.outageTitle', { n: num(outages.length) })}
          </p>
          <p className="text-footnote text-[var(--fg-secondary)]">{t('attendance.outageHint')}</p>
        </div>
      </div>

      <ul className="mt-3 space-y-2">
        {outages.map((outage) => (
          <li
            key={outage.deviceId}
            className="flex flex-wrap items-baseline gap-x-3 gap-y-1 rounded-md bg-[var(--bg-elevated)] px-3 py-2.5"
          >
            <span className="min-w-0 flex-1 truncate-1 text-subhead font-semibold">{outage.deviceName}</span>
            <span className="text-footnote text-ios-red">
              {outage.state === 'AgentOffline'
                ? t('attendance.outageAgentSince', { time: outage.since === null ? '—' : dateTime(outage.since) })
                : outage.since === null
                  ? t('attendance.outageTerminalNever')
                  : t('attendance.outageTerminalSince', { time: dateTime(outage.since) })}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

/**
 * Statistika sahifasining qurilma ogohlantirishlari — «Hozir ishda» taxtasi
 * almashtirilganda yo'qolib qolgan banner va bo'sh holat shu yerda qaytdi.
 *
 * Tartib muhimlik bo'yicha: uzilish (qizil) → soat (sariq) → hech bir
 * qurilma ulanmagan (ko'k, ma'lumot). Oxirgisi faqat birorta ham terminal
 * gapirmagan bo'lsa: bitta ulanmagan yangi terminal butun sahifani
 * «hech narsa ishlamayapti» degan xabar bilan to'ldirmasin.
 */
export function DeviceHealthBanners({ health }: { health: AttendanceDeviceHealth | undefined }) {
  const { t, num } = useLocale()

  if (health === undefined) return null

  const nothingConnected = health.connectedDevices === 0

  return (
    <>
      <DeviceOutagePanel outages={health.outages} />

      {health.clockWarningDevices > 0 && (
        <Alert tone="warning" title={t('attendance.devicesClockWarningTitle', { n: num(health.clockWarningDevices) })}>
          {t('attendance.devicesClockWarningText')}
        </Alert>
      )}

      {nothingConnected && (
        <div className="flex items-start gap-3 rounded-md bg-[var(--accent-soft)] p-3.5">
          <Cpu className="mt-px size-[18px] shrink-0 text-[var(--accent-on-soft)]" strokeWidth={2.25} />
          <div className="min-w-0 space-y-1">
            <p className="text-subhead font-semibold text-[var(--accent-on-soft)]">
              {t('attendance.noDeviceConnectedTitle')}
            </p>
            <p className="text-footnote text-[var(--fg-secondary)]">
              {t('attendance.noDeviceConnectedText')}{' '}
              <Link to="/verifikatsiya/qurilmalar" className="font-medium text-[var(--accent-text)] underline-offset-2 hover:underline">
                {t('nav.attendanceDevices')}
              </Link>
            </p>
          </div>
        </div>
      )}
    </>
  )
}
