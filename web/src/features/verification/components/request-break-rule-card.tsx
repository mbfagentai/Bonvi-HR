import { useQueryClient } from '@tanstack/react-query'
import { Coffee, LoaderCircle } from 'lucide-react'
import { useState } from 'react'

import type { SystemSetting } from '@/features/settings/api/settings-api'
import { useSaveSettings, useSystemSettings } from '@/features/settings/api/settings-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, IconTile, Skeleton, TextField } from '@/shared/ui'

import { attendanceKeys } from '../api/attendance-queries'
import { clockMinutes } from '../lib/attendance-schedule-rules'
import { AttendanceLoadError } from './attendance-manage-states'

/** Ruxsat etilgan oraliq boshi — kunning daqiqasi (`SystemSettingKeys`, sukut 720 = 12:00). */
const FROM_KEY = 'attendance.requestBreakFrom'

/** Ruxsat etilgan oraliq oxiri — kunning daqiqasi (sukut 900 = 15:00). */
const TO_KEY = 'attendance.requestBreakTo'

/** Tanaffus uzunligi: jumadan boshqa kunlarda aniq shu, jumada eng kami (sukut 60). */
const MINUTES_KEY = 'attendance.requestBreakMinutes'

/** Jumada eng ko'pi (sukut 120). */
const FRIDAY_KEY = 'attendance.requestBreakFridayMaxMinutes'

/**
 * Jadval so'rovidagi tanaffus qoidasi — «Ish jadvallari» sahifasida
 * (SPEC-sorov-tahriri §2.2, §7.2).
 *
 * NEGA shu yerda, tizim sozlamalari sahifasida emas: to'rtta son faqat davomat
 * moduliga tegishli va ularni o'zgartiradigan odam aynan jadvallar bilan
 * ishlayotgan bo'ladi — saqlash muddatlari kartasi (`AttendanceRetentionCard`)
 * bilan bir xil sabab. Sozlamalar sahifasining `GROUPS` iga ATAYLAB qo'shilmagan.
 *
 * O'qish va yozish — MAVJUD `/api/settings` orqali, faqat o'zgargan kalitlar
 * yuboriladi. Ikki vaqt serverda KUNNING DAQIQASI (720), forma esa ularni
 * «12:00» ko'rinishida ko'rsatadi va yozadi — HR daqiqani hisoblamasin.
 *
 * Kalitlararo qoida (`from < to`, `daqiqa ≤ jumadagi`, `jumadagi ≤ to − from`)
 * serverda (`davomat.tanaffus_qoidasi_nomuvofiq`); karta uni oldindan aytadi va
 * «Saqlash» ni o'chiradi — server baribir o'zi tekshiradi.
 *
 * ⚠️ Chaqiruvchi kartani faqat `settings.manage` ruxsati borlarga chizadi:
 * `GET /api/settings` ning o'zi shu ruxsat bilan yopiq.
 */
export function RequestBreakRuleCard() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const queryClient = useQueryClient()

  const settings = useSystemSettings(true)
  const save = useSaveSettings()

  /** Tahrirlanayotgan qiymatlar: kalit → serverdagi shakl (daqiqa satri); yo'q kalit — serverniki. */
  const [draft, setDraft] = useState<Readonly<Record<string, string>>>({})

  const items = settings.data ?? []
  const from = items.find((item) => item.key === FROM_KEY)
  const to = items.find((item) => item.key === TO_KEY)
  const minutes = items.find((item) => item.key === MINUTES_KEY)
  const friday = items.find((item) => item.key === FRIDAY_KEY)
  const fields = [from, to, minutes, friday].filter(
    (item): item is SystemSetting => item !== undefined,
  )

  const valueOf = (item: SystemSetting) => draft[item.key] ?? item.value
  const changed = fields.filter((item) => valueOf(item).trim() !== item.value)

  const edit = (key: string, value: string) => setDraft((current) => ({ ...current, [key]: value }))

  /*
    Qoida joriy (saqlanmagan qiymatlar bilan) — to'rttasi ham butun son bo'lsa.
    Yozilayotgan son hali butun bo'lmasa kalitlararo tekshiruv yo'q: noto'g'ri
    sonni server o'zining chegara xabari bilan rad etadi.
  */
  const rule =
    from === undefined || to === undefined || minutes === undefined || friday === undefined
      ? null
      : {
          from: wholeNumber(valueOf(from)),
          to: wholeNumber(valueOf(to)),
          minutes: wholeNumber(valueOf(minutes)),
          friday: wholeNumber(valueOf(friday)),
        }

  const inconsistent =
    rule !== null &&
    rule.from !== null &&
    rule.to !== null &&
    rule.minutes !== null &&
    rule.friday !== null &&
    !(rule.from < rule.to && rule.minutes <= rule.friday && rule.friday <= rule.to - rule.from)

  /** Bo'shatilgan vaqt maydoni — uni daqiqaga aylantirib bo'lmaydi. */
  const fromEmpty = from !== undefined && wholeNumber(valueOf(from)) === null
  const toEmpty = to !== undefined && wholeNumber(valueOf(to)) === null

  const submit = () => {
    save.mutate(
      changed.map((item) => ({ key: item.key, value: valueOf(item).trim() })),
      {
        onSuccess: () => {
          // Nusxa tozalanadi — maydonlar serverdagi yangi qiymatni ko'rsatadi
          setDraft({})
          // Ochiq jadval so'rovi formalari yangi sonlar bilan tekshirsin
          void queryClient.invalidateQueries({ queryKey: attendanceKeys.scheduleRequestBreakRule })
        },
      },
    )
  }

  /*
    Server bu kalitlarni bermasa (eski backend) — kartada ko'rsatadigan narsa
    yo'q (`AttendanceRetentionCard` dagi qoida).
  */
  if (settings.data !== undefined && fields.length === 0) return null

  return (
    <Card className="overflow-hidden">
      <div className="flex items-start gap-3 border-b border-[var(--border)] px-4 py-4 sm:px-5">
        <IconTile icon={Coffee} tone="orange" />
        <div className="min-w-0">
          <h2 className="text-subhead font-semibold">{t('attendance.breakRuleTitle')}</h2>
          <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">
            {t('attendance.breakRuleHint')}
          </p>
        </div>
      </div>

      {settings.isPending ? (
        <div className="grid gap-4 px-4 py-4 sm:px-5 md:grid-cols-2">
          {Array.from({ length: 4 }, (_, index) => (
            <div key={index} className="space-y-2">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-12 w-full" />
            </div>
          ))}
        </div>
      ) : settings.isError ? (
        <AttendanceLoadError
          className="py-10"
          error={settings.error}
          onRetry={() => void settings.refetch()}
          retrying={settings.isFetching}
        />
      ) : (
        <div className="space-y-4 px-4 py-4 sm:px-5">
          {save.isError && <Alert tone="danger">{errorMessage(problemCode(save.error))}</Alert>}

          <div className="grid gap-4 md:grid-cols-2">
            {from !== undefined && (
              <TextField
                label={t('attendance.breakRuleFrom')}
                type="time"
                required
                value={clockOf(valueOf(from))}
                error={fromEmpty ? t('attendance.breakRuleTimeEmpty') : undefined}
                onChange={(event) => edit(FROM_KEY, minuteText(event.target.value))}
              />
            )}

            {to !== undefined && (
              <TextField
                label={t('attendance.breakRuleTo')}
                type="time"
                required
                value={clockOf(valueOf(to))}
                error={toEmpty ? t('attendance.breakRuleTimeEmpty') : undefined}
                onChange={(event) => edit(TO_KEY, minuteText(event.target.value))}
              />
            )}

            {minutes !== undefined && (
              <TextField
                label={t('attendance.breakRuleMinutes')}
                type="number"
                inputMode="numeric"
                min={minutes.min ?? undefined}
                max={minutes.max ?? undefined}
                hint={rangeHint(minutes)}
                value={valueOf(minutes)}
                onChange={(event) => edit(MINUTES_KEY, event.target.value)}
              />
            )}

            {friday !== undefined && (
              <TextField
                label={t('attendance.breakRuleFridayMax')}
                type="number"
                inputMode="numeric"
                min={friday.min ?? undefined}
                max={friday.max ?? undefined}
                hint={rangeHint(friday)}
                value={valueOf(friday)}
                onChange={(event) => edit(FRIDAY_KEY, event.target.value)}
              />
            )}
          </div>

          {/*
            Ziddiyatli to'rtlik — server xabarining o'zi: HR qaysi shart buzilganini
            «Saqlash» ni bosmasdan ko'radi. Aks holda — formalar aynan nimani
            talab qilishi (jadval so'rovi oynasidagi eslatmaning o'zi).
          */}
          {inconsistent ? (
            <Alert tone="warning">{errorMessage('davomat.tanaffus_qoidasi_nomuvofiq')}</Alert>
          ) : (
            rule !== null &&
            rule.from !== null &&
            rule.to !== null &&
            rule.minutes !== null &&
            rule.friday !== null && (
              <p className="tabular text-footnote text-[var(--fg-secondary)]">
                {t('attendance.requestBreakRuleHint', {
                  from: clockOf(String(rule.from)),
                  to: clockOf(String(rule.to)),
                  n: num(rule.minutes),
                  max: num(rule.friday),
                })}
              </p>
            )
          )}

          <div className="flex items-center justify-end gap-3">
            {changed.length > 0 && (
              <span className="text-footnote text-[var(--fg-secondary)]">
                {t('settings.system.unsaved')}
              </span>
            )}
            <Button
              type="button"
              variant="primary"
              onClick={submit}
              disabled={
                save.isPending || changed.length === 0 || inconsistent || fromEmpty || toEmpty
              }
            >
              {save.isPending && <LoaderCircle className="animate-spin" />}
              {t('common.save')}
            </Button>
          </div>
        </div>
      )}
    </Card>
  )

  /** Son maydonining chegarasi — serverdan (`settings.system.range`). */
  function rangeHint(item: SystemSetting): string | undefined {
    return item.min !== null && item.max !== null
      ? t('settings.system.range', { min: num(item.min), max: num(item.max) })
      : undefined
  }
}

/** Butun manfiy bo'lmagan son bo'lsa — son, aks holda `null`. */
function wholeNumber(value: string): number | null {
  const trimmed = value.trim()
  return /^\d+$/u.test(trimmed) ? Number(trimmed) : null
}

/** Kunning daqiqasi («720») → «12:00»; o'qib bo'lmasa — bo'sh (maydon bo'sh ko'rinadi). */
function clockOf(value: string): string {
  const minutes = wholeNumber(value)
  if (minutes === null || minutes > 24 * 60 - 1) return ''

  const hours = String(Math.floor(minutes / 60)).padStart(2, '0')
  return `${hours}:${String(minutes % 60).padStart(2, '0')}`
}

/** «12:00» → «720» (serverdagi shakl); bo'sh yoki yaroqsiz — `''`. */
function minuteText(clock: string): string {
  const minutes = clockMinutes(clock)
  return minutes === null ? '' : String(minutes)
}
