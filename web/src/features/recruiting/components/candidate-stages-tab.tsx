import { History, Pencil, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, Button, Card, CardContent, EmptyState, IconTile } from '@/shared/ui'
import type { Candidate, CandidateStageEntry } from '../api/recruiting-api'
import { OUTCOME_LOOK } from '../lib/recruiting-presentation'
import { CandidateStageDialog } from './candidate-stage-dialog'

/**
 * ⭐ Bosqichlar tarixi — nomzodning butun yo'li bitta vertikal lentada.
 *
 * `employee-timeline-tab.tsx` ning aynan naqshi va bu ataylab: HR
 * ikkala ekranda ham bir xil narsani ko'radi — «qachon nima bo'ldi»
 * degan savolga javob beradigan oqim. Ikki xil ko'rinish yozilsa,
 * bir xil ma'lumot ikki xil o'qilardi.
 *
 * <b>Tartib — YANGISIDAN ESKISIGA.</b> HR ko'pincha «oxirgi nima
 * bo'ldi?» deb qaraydi. Server esa yozuvlarni YOZILGAN tartibda
 * qaytaradi (`CandidateDto.stages` — «yozilgan tartibda»), shu
 * sababli teskarisiga o'girish klientda bajariladi.
 *
 * <b>Nega sana bo'yicha emas, RO'YXAT tartibi bo'yicha teskari.</b>
 * Bosqich sanasi QO'LDA kiritiladi va o'tgan kun bilan yozilishi
 * odatiy hol («kecha suhbat bo'lgan edi»). Sana bo'yicha saralash
 * bunday yozuvni tarixning o'rtasiga tashlab yuborardi va «hozir
 * nima qildim?» degan savolga javob yo'qolardi. Ro'yxat tartibi esa
 * KIRITILISH ketma-ketligini saqlaydi.
 */
export function CandidateStagesTab({
  candidate,
  canManage,
}: {
  candidate: Candidate
  canManage: boolean
}) {
  const { t } = useLocale()

  const [recording, setRecording] = useState(false)
  /** Lentadagi yozuvni to'ldirish — o'sha bosqich oldindan tanlanadi. */
  const [presetStageId, setPresetStageId] = useState<string | null>(null)

  const entries = useMemo(() => [...candidate.stages].reverse(), [candidate.stages])

  const open = (stageId: string | null) => {
    setPresetStageId(stageId)
    setRecording(true)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {candidate.currentStage === null
            ? t('recruiting.noStage')
            : t('recruiting.currentStage', { stage: candidate.currentStage })}
        </p>

        {canManage && (
          <Button type="button" size="sm" variant="create" onClick={() => open(null)}>
            <Plus />
            {t('recruiting.stageRecord')}
          </Button>
        )}
      </div>

      {entries.length === 0 ? (
        <Card>
          <EmptyState
            icon={History}
            title={t('recruiting.stagesEmptyTitle')}
            description={t('recruiting.stagesEmptyText')}
            action={
              canManage ? (
                <Button type="button" size="sm" variant="create" onClick={() => open(null)}>
                  <Plus />
                  {t('recruiting.stageRecord')}
                </Button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <Card>
          <CardContent className="pt-5">
            <ol className="relative">
              {/* Vertikal chiziq — ikonalar markazidan o'tadi */}
              <span
                aria-hidden
                className="absolute top-2 bottom-2 left-5 w-px bg-[var(--border)]"
              />

              {entries.map((entry) => (
                <StageRow
                  key={entry.id}
                  entry={entry}
                  canManage={canManage}
                  onFill={() => open(entry.stageId)}
                />
              ))}
            </ol>
          </CardContent>
        </Card>
      )}

      <CandidateStageDialog
        candidate={candidate}
        open={recording}
        presetStageId={presetStageId}
        onClose={() => setRecording(false)}
      />
    </div>
  )
}

function StageRow({
  entry,
  canManage,
  onFill,
}: {
  entry: CandidateStageEntry
  canManage: boolean
  onFill: () => void
}) {
  const { t, date, dateTime } = useLocale()

  const look = OUTCOME_LOOK[entry.outcome]

  return (
    <li className="relative flex gap-4 pb-6 last:pb-1">
      {/* Ikona chiziq ustida turadi — atrofidagi halqa fon rangida */}
      <span className="relative z-10 rounded-md ring-4 ring-[var(--bg-elevated)]">
        <IconTile icon={look.icon} tone={look.tone} />
      </span>

      <div className="min-w-0 flex-1 pt-0.5">
        <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
          <span className="text-subhead font-semibold">{entry.stage ?? '—'}</span>

          <Badge tone={look.badge} size="sm">
            {t(`recruiting.outcome.${look.key}`)}
          </Badge>

          {/*
            «Kutilmoqda» yozuvini TO'LDIRISH — lentadan bir bosishda.
            Server aynan shu yozuvni yangilaydi (yangisini qo'shmaydi),
            shu sababli tugma «tahrirlash» emas, «natijani yozish»
            ma'nosini beradi.
          */}
          {canManage && entry.outcome === 'Pending' && (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              onClick={onFill}
              className="-my-1"
            >
              <Pencil />
              {t('recruiting.stageFill')}
            </Button>
          )}
        </div>

        <p className="tabular mt-1 text-caption1 text-[var(--fg-secondary)]">
          {date(entry.date)}
        </p>

        {entry.conclusion !== null && (
          <p className="mt-2 rounded-md bg-[var(--bg-sunken)] px-3 py-2 text-footnote text-[var(--fg-secondary)]">
            {entry.conclusion}
          </p>
        )}

        {/*
          «Kim yozdi» — serverdan faqat identifikator keladi
          (`recordedBy`), ism EMAS: nomzod DTO'sida foydalanuvchi
          nomlari yig'ilmaydi. Shu sababli lentada faqat YOZILGAN
          VAQT ko'rsatiladi — identifikatorni chizish HR uchun
          ma'nosiz, uni ismga aylantirish esa har yozuv uchun
          qo'shimcha so'rov degani (N+1).
        */}
        <p className="mt-1.5 text-caption2 text-[var(--fg-tertiary)]">
          {t('recruiting.recordedAt', { at: dateTime(entry.recordedAt) })}
        </p>
      </div>
    </li>
  )
}
