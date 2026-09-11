import { History, MessageSquareText, Pencil, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, Button, Card, CardContent, EmptyState, IconTile } from '@/shared/ui'
import type { Candidate, CandidateNote, CandidateStageEntry } from '../api/recruiting-api'
import { lowerFirst, OUTCOME_LOOK } from '../lib/recruiting-presentation'
import { CandidateStageDialog } from './candidate-stage-dialog'

/**
 * ⭐ Bosqichlar tarixi — nomzodning butun yo'li bitta vertikal lentada.
 *
 * `employee-timeline-tab.tsx` ning aynan naqshi va bu ataylab: HR
 * ikkala ekranda ham bir xil narsani ko'radi — «qachon nima bo'ldi»
 * degan savolga javob beradigan oqim. Ikki xil ko'rinish yozilsa,
 * bir xil ma'lumot ikki xil o'qilardi.
 *
 * <b>Lentada IKKI OQIM birlashadi</b> — bosqich yozuvlari va AMAL
 * izohlari (`candidate_notes`: boshqa vakansiyaga o'tkazish, rezervdan
 * qaytarish, suhbatni ko'chirish).
 *
 * Nega birlashtirildi (2026-09-10): amal izohi ilgari FAQAT nomzod
 * kartasidagi «oxirgi nima bo'ldi» chizig'ida ko'rinardi va undan
 * yangiroq yozuv paydo bo'lishi bilan interfeysning HECH QAYERIDAN
 * ko'rinmay qolardi. Ya'ni HR izohni yozardi, keyin o'qiy olmasdi —
 * izohning butun maqsadi shu bilan yo'qolardi. Tarix bitta joyda
 * bo'lishi kerak, chunki HR «bu odam bilan nima bo'lgan edi?» degan
 * savolni bitta ekranda so'raydi.
 *
 * <b>Tartib — YANGISIDAN ESKISIGA, YOZILGAN VAQT bo'yicha.</b> HR
 * ko'pincha «oxirgi nima bo'ldi?» deb qaraydi.
 *
 * <b>Nega `date` emas, `recordedAt`.</b> Bosqich SANASI qo'lda
 * kiritiladi va o'tgan kun bilan yozilishi odatiy hol («kecha suhbat
 * bo'lgan edi») — sana bo'yicha saralash bunday yozuvni tarixning
 * o'rtasiga tashlab yuborardi. `recordedAt` esa server qo'yadigan
 * YOZILISH lahzasi: bosqichlar uchun u ro'yxat tartibining aynan o'zini
 * beradi (ilgari shu sababli ro'yxat teskari o'girilardi), lekin
 * izohlarni ham to'g'ri joyga qo'yadi — ro'yxat tartibi buni qila
 * olmasdi, chunki ikki oqim ikki alohida massivdan keladi.
 *
 * ⚠️ Bitta xatti-harakat o'zgardi: «kutilmoqda» yozuvi to'ldirilganda
 * server `recordedAt` ni yangilaydi, ya'ni yozuv lentaning TEPASIGA
 * chiqadi. Bu ataylab qabul qilindi — kartadagi «oxirgi nima bo'ldi»
 * chizig'i allaqachon aynan shu qoida bilan tanlaydi, ikki ko'rinish
 * bir xil javob berishi kerak.
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

  /*
    Ikki oqim bitta lentaga. Tanilmagan sana pastga tushadi (`0`), tashlab
    ketilmaydi: yozuv ko'rinmay qolgandan ko'ra, oxirida turgani ma'qul.
  */
  const items = useMemo(() => {
    const merged: TimelineItem[] = [
      ...candidate.stages.map((entry) => ({ kind: 'stage' as const, entry })),
      ...candidate.notes.map((note) => ({ kind: 'note' as const, note })),
    ]

    return merged.sort((a, b) => stamp(b) - stamp(a))
  }, [candidate.stages, candidate.notes])

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

      {items.length === 0 ? (
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

              {items.map((item) =>
                item.kind === 'stage' ? (
                  <StageRow
                    key={`stage:${item.entry.id}`}
                    entry={item.entry}
                    canManage={canManage}
                    onFill={() => open(item.entry.stageId)}
                  />
                ) : (
                  <NoteRow key={`note:${item.note.id}`} note={item.note} />
                ),
              )}
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

/**
 * Lentaning bitta yozuvi — bosqich yoki amal izohi.
 *
 * Ikki oqim BIRLASHTIRILMADI (umumiy `{ at, text }` shakliga
 * keltirilmadi): bosqichda natija yorlig'i, sanasi va «to'ldirish»
 * tugmasi bor, izohda esa faqat turi va matni. Umumiy shaklga
 * siqilsa, har chizishda «bu qaysi turi edi?» degan shart baribir
 * qaytardi — endi u tipda turibdi va kompilyator tekshiradi.
 */
type TimelineItem =
  | { kind: 'stage'; entry: CandidateStageEntry }
  | { kind: 'note'; note: CandidateNote }

/** Saralash kaliti — ikkala oqimda ham YOZILGAN lahza (sabab fayl boshida). */
function stamp(item: TimelineItem): number {
  const at = item.kind === 'stage' ? item.entry.recordedAt : item.note.createdAt
  const ms = Date.parse(at)

  return Number.isNaN(ms) ? 0 : ms
}

/**
 * Amal izohi — «Boshqa vakansiyaga o'tkazildi», «Rezervdan qaytarildi»,
 * «Suhbat ko'chirildi».
 *
 * Bosqich yozuvidan ATAYLAB kamtarona chizilgan: rangsiz ikona va
 * natija yorlig'i yo'q. Sabab — bu voronkadagi QADAM emas, uning
 * yonidagi qayd. Ikkalasi bir xil ko'rinsa, lenta «nomzod qayerda
 * turibdi» degan savolga javob berishni to'xtatardi.
 *
 * Sana YO'Q, faqat yozilgan vaqt: izohning o'z sanasi umuman
 * bo'lmaydi (`candidate_notes` da faqat `created_at`), ya'ni bosqich
 * yozuvidagi «sana + yozilgan vaqt» juftligi bu yerda takror bo'lardi.
 */
function NoteRow({ note }: { note: CandidateNote }) {
  const { t, dateTime } = useLocale()

  return (
    <li className="relative flex gap-4 pb-6 last:pb-1">
      <span className="relative z-10 rounded-md ring-4 ring-[var(--bg-elevated)]">
        <IconTile icon={MessageSquareText} tone="gray" />
      </span>

      <div className="min-w-0 flex-1 pt-0.5">
        <span className="text-subhead font-semibold">
          {t(`recruiting.noteKind.${lowerFirst(note.kind)}`)}
        </span>

        <p className="mt-2 rounded-md bg-[var(--bg-sunken)] px-3 py-2 text-footnote text-[var(--fg-secondary)]">
          {note.text}
        </p>

        {/* «Kim yozdi» ko'rsatilmaydi — sabab `StageRow` dagi bilan bir xil. */}
        <p className="mt-1.5 text-caption2 text-[var(--fg-tertiary)]">
          {t('recruiting.recordedAt', { at: dateTime(note.createdAt) })}
        </p>
      </div>
    </li>
  )
}
