import { Archive, LoaderCircle } from 'lucide-react'
import { useState } from 'react'

import type { SystemSetting } from '@/features/settings/api/settings-api'
import { useSaveSettings, useSystemSettings } from '@/features/settings/api/settings-queries'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, IconTile, Skeleton, TextField } from '@/shared/ui'

import { AttendanceLoadError } from './attendance-manage-states'

/** Surat saqlash muddati (`SystemSettingKeys.PhotoRetentionDays`). */
const PHOTO_KEY = 'attendance.photoRetentionDays'

/** O'tish yozuvi saqlash muddati (`SystemSettingKeys.PassRetentionDays`). `0` — hech qachon. */
const PASS_KEY = 'attendance.passRetentionDays'

/**
 * Saqlash muddatlari — turniket suratlari va o'tish yozuvlari (SPEC §2.9,
 * §8.9) — QURILMALAR sahifasida.
 *
 * NEGA shu yerda, tizim sozlamalari sahifasida emas: bu ikki son faqat
 * davomat moduliga tegishli va ularni o'zgartiradigan odam aynan qurilmalar
 * bilan ishlayotgan bo'ladi (disk to'lishi — turniket suratlaridan). Egasi
 * modul umumiy sahifalarga tegmasligini talab qilgan, shu sababli sozlamalar
 * sahifasi asl holiga qaytarildi va muddatlar modul ichiga ko'chdi.
 *
 * O'qish va yozish — MAVJUD `/api/settings` orqali (`useSystemSettings`,
 * `useSaveSettings`): yangi endpoint yo'q, chegaralar va tekshiruv serverda.
 *
 * ⚠️ Chaqiruvchi kartani faqat `settings.manage` ruxsati borlarga chizadi:
 * `GET /api/settings` ning o'zi shu ruxsat bilan yopiq.
 */
export function AttendanceRetentionCard() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  const settings = useSystemSettings(true)
  const save = useSaveSettings()

  /** Tahrirlanayotgan qiymatlar: kalit → matn. Yo'q kalit — serverdagi qiymat. */
  const [draft, setDraft] = useState<Readonly<Record<string, string>>>({})

  const items = settings.data ?? []
  const photo = items.find((item) => item.key === PHOTO_KEY)
  const pass = items.find((item) => item.key === PASS_KEY)
  const fields = [photo, pass].filter((item): item is SystemSetting => item !== undefined)

  const valueOf = (item: SystemSetting) => draft[item.key] ?? item.value
  const changed = fields.filter((item) => valueOf(item) !== item.value)

  const photoLowered = photo !== undefined && isLowered(photo.value, valueOf(photo), false)
  const passLowered = pass !== undefined && isLowered(pass.value, valueOf(pass), true)

  const submit = () => {
    save.mutate(
      changed.map((item) => ({ key: item.key, value: valueOf(item).trim() })),
      // Saqlangach nusxa tozalanadi — maydonlar serverdagi yangi qiymatni ko'rsatadi
      { onSuccess: () => setDraft({}) },
    )
  }

  /*
    Server bu ikki kalitni bermasa (eski backend) — kartada ko'rsatadigan
    narsa yo'q. Bo'sh sarlavhali karta «sozlab bo'lmaydi» degan yolg'on
    taassurot qoldirardi.
  */
  if (settings.data !== undefined && fields.length === 0) return null

  return (
    <Card className="overflow-hidden">
      <div className="flex items-start gap-3 border-b border-[var(--border)] px-4 py-4 sm:px-5">
        <IconTile icon={Archive} tone="purple" />
        <div className="min-w-0">
          <h2 className="text-subhead font-semibold">{t('attendance.retentionTitle')}</h2>
          <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">
            {t('attendance.retentionHint')}
          </p>
        </div>
      </div>

      {settings.isPending ? (
        <div className="grid gap-4 px-4 py-4 sm:px-5 md:grid-cols-2">
          {Array.from({ length: 2 }, (_, index) => (
            <div key={index} className="space-y-2">
              <Skeleton className="h-4 w-48" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-3 w-64" />
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
            {photo !== undefined && (
              <div className="space-y-2">
                <TextField
                  label={t('attendance.photoRetentionLabel')}
                  type="number"
                  inputMode="numeric"
                  min={photo.min ?? undefined}
                  max={photo.max ?? undefined}
                  hint={hintFor(photo, t('attendance.photoRetentionHint'), null)}
                  value={valueOf(photo)}
                  onChange={(event) => setDraft((current) => ({ ...current, [PHOTO_KEY]: event.target.value }))}
                />
                {photoLowered && <Alert tone="warning">{t('attendance.photoRetentionLowered')}</Alert>}
              </div>
            )}

            {pass !== undefined && (
              <div className="space-y-2">
                <TextField
                  label={t('attendance.passRetentionLabel')}
                  type="number"
                  inputMode="numeric"
                  /*
                    `0` — «hech qachon o'chirilmasin» — server chegarasidan
                    TASHQARIDAGI alohida qiymat (`SystemSettingDefinition.AllowZero`).
                    DTO buni tashimaydi, shu sababli maydon uni shu yerda
                    ochadi; 1…29 ni baribir server rad etadi.
                  */
                  min={0}
                  max={pass.max ?? undefined}
                  hint={hintFor(pass, t('attendance.passRetentionHint'), t('attendance.passRetentionNever'))}
                  value={valueOf(pass)}
                  onChange={(event) => setDraft((current) => ({ ...current, [PASS_KEY]: event.target.value }))}
                />
                {passLowered && <Alert tone="warning">{t('attendance.passRetentionLowered')}</Alert>}
              </div>
            )}
          </div>

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
              disabled={save.isPending || changed.length === 0}
            >
              {save.isPending && <LoaderCircle className="animate-spin" />}
              {t('common.save')}
            </Button>
          </div>
        </div>
      )}
    </Card>
  )

  /**
   * Maydon izohi: ma'nosi, chegara va (bo'lsa) «0 — hech qachon». Tartib
   * sozlamalar sahifasidagi bilan bir xil — chegara faqat ma'no
   * tushunilgandan keyin foyda beradi.
   */
  function hintFor(item: SystemSetting, meaning: string, never: string | null): string {
    return [
      meaning,
      item.min !== null && item.max !== null
        ? t('settings.system.range', { min: num(item.min), max: num(item.max) })
        : null,
      never,
    ]
      .filter((part): part is string => part !== null)
      .join(' · ')
  }
}

/**
 * Muddat qisqarmoqdami — ya'ni keyingi tungi tozalash hozir saqlanayotgan
 * narsani QAYTARIB BO'LMAYDIGAN qilib o'chiradimi.
 *
 * Saqlashni TO'SMAYDI — faqat oldindan aytadi: qaror HR niki, hukm
 * serverniki. Yozilayotgan (butun son emas) qiymatda ogohlantirish yo'q.
 *
 * @param zeroMeansNever O'tish muddatida `0` — «hech qachon»: undan istalgan
 * songa o'tish ham qisqartirish, `0` ga o'tish esa aksincha.
 */
function isLowered(saved: string, next: string, zeroMeansNever: boolean): boolean {
  const before = wholeDays(saved)
  const after = wholeDays(next)
  if (before === null || after === null) return false

  if (zeroMeansNever) {
    if (after === 0) return false
    if (before === 0) return true
  }

  return after < before
}

/** Butun kun bo'lsa — son, aks holda `null`. */
function wholeDays(value: string): number | null {
  const trimmed = value.trim()
  return /^\d+$/u.test(trimmed) ? Number(trimmed) : null
}
