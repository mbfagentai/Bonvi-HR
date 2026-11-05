import { CalendarClock } from 'lucide-react'
import { useMemo, useState } from 'react'

import { cn } from '@/shared/lib/cn'
import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, Button, Card, CardContent, buttonVariants } from '@/shared/ui'
import type { Candidate, CandidateNoteKind, PipelineStage } from '../api/recruiting-api'
import { useStages } from '../api/recruiting-queries'
import { lowerFirst, today } from '../lib/recruiting-presentation'
import { CandidateInterviewDialog } from './candidate-interview-dialog'
import { CandidateTelegramLinkButton } from './candidate-telegram-link-button'
import { CandidateStageDialog } from './candidate-stage-dialog'

/**
 * ⭐ Bosqichni o'zgartirish — nomzod kartasidagi tezkor qator.
 *
 * <b>Nima uchun qo'shildi.</b> Foydalanuvchi talabi (2026-09-08): HR
 * nomzodga telefon qilgandan keyin uni «Гаплашилди» bosqichiga, kengashga
 * chaqirgandan keyin esa «Кенгашга чақирилди» bosqichiga o'tkazishi kerak.
 * Ilgari buning yagona yo'li «Bosqichlar» tabini ochib, oyna chaqirib,
 * bosqichni ro'yxatdan tanlab, saqlash edi — kuniga o'nlab marta
 * takrorlanadigan amal uchun to'rt qadam. Bu qator o'sha yo'lni
 * QISQARTIRADI: kerakli bosqich ko'z oldida turadi va bir bosishda
 * shu bosqichning oynasi ochiladi.
 *
 * <b>Nega tugmalar KODDA sanab chiqilmagan.</b> «Гаплашилди» va
 * «Кенгашга чақирилди» — bu kod emas, MA'LUMOTNOMA yozuvi
 * (<c>PipelineStage</c> izohi: voronkani HR o'zi boshqaradi). Ikkita nom
 * kodga yozilsa, HR bosqichni qayta nomlagan kuni tugma jimgina yo'qolardi
 * yoki noto'g'ri bosqichga o'tkazardi. Shu sababli qator FAOL bosqichlar
 * ro'yxatidan chiziladi: HR ma'lumotnomaga bosqich qo'shsa, tugmasi ham
 * o'zidan paydo bo'ladi, tartibi ham voronkanikidek qoladi.
 *
 * <b>⭐ Har bir tugma OYNA ochadi — bir bosishda ko'chirish OLIB
 * TASHLANDI</b> (foydalanuvchi talabi, 2026-09-10). Ilgari tugma
 * to'g'ridan-to'g'ri so'rov yuborardi (`outcome = Pending`, sana va
 * xulosa bo'sh), ostidagi eslatma esa «natijani keyin Bosqichlar
 * bo'limida yozasiz» deb turardi — va u amalda HECH QACHON yozilmadi:
 * telefon suhbatining butun mazmuni yo'qoldi. Endi tugma
 * `CandidateStageDialog` ni QOTIB QO'YILGAN bosqich bilan ochadi va
 * sana, natija, xulosa, keyingi aloqa sanasi bir joyda so'raladi.
 * Oyna BITTA: alohida «tezkor oyna» yozish rad etildi (sabab shu
 * oynaning izohida, SPEC §5.1.1).
 *
 * <b>Voronka TAXTASIDAGI sudrash ham AYNAN shu oynalarni ochadi</b>
 * (2026-09-26, foydalanuvchi talabi; ilgari u bir bosishda ko'chirardi
 * va xulosa yozilmay qolardi). Tanlov `candidate-funnel.tsx` dagi
 * `DroppedCardDialog` da — shu yerdagi `move` bilan bir xil.
 *
 * <b>Ba'zi bosqich BOSHQA oyna ochadi.</b> Ma'lumotnomada «suhbat
 * taklifi» bayrog'i qo'yilgan bosqich (odatda «Кенгашга чақирилди»)
 * bosqich oynasi emas, SUHBAT oynasini ochadi va HR dan sana bilan
 * vaqtni so'raydi — nomzodga aynan shu ma'lumot bilan Telegram xabari
 * ketadi. Bayroq bosqichning o'zida turadi, kodda emas: sabab
 * `PipelineStage.InvitesToInterview` domen izohida.
 *
 * <b>Joriy bosqich — tugma EMAS.</b> U tugma bo'lib, `disabled` qilinsa
 * xiralashardi (`disabled:opacity-40`) va «ishlamayapti» degan ma'no
 * berardi. Aslida u ishlamaydigan tugma emas, HOLAT ko'rsatkichi —
 * shuning uchun bir xil o'lchamdagi to'ldirilgan yorliq.
 *
 * <b>«Oxirgi nima bo'ldi» chizig'i — tugmalardan YUQORIDA.</b> Talab
 * shunday: HR keyingi tugmani bosishdan oldin ilgari nima
 * qilinganini ko'rishi kerak. Uni «Bosqichlar» tabiga yuborish RAD
 * ETILDI — bugungi ish oqimi aynan shunday va hech kim o'sha tabni
 * ochmaydi (SPEC §5.2).
 */
export function CandidateStagePicker({ candidate }: { candidate: Candidate }) {
  const { t, date, dateTime } = useLocale()

  /*
   * Faqat FAOL bosqichlar: faolsizlantirilganiga o'tkazib bo'lmaydi va
   * server buni 409 bilan rad etadi («bosqich.faol_emas»). Ro'yxat
   * keshdan keladi — uni nomzod kartasi allaqachon so'ragan
   * (`candidate-card-page.tsx`).
   */
  const stages = useStages(true)

  /**
   * Suhbat oynasi: `closed` — yopiq, `null` — ochiq, lekin bosqichni
   * o'zgartirmaydi (vaqtni ko'chirish), bosqich — o'sha bosqichga
   * o'tkazadi.
   *
   * Uch holat KERAK va ikkitasi yetmasdi: `null` ning o'zi «yopiq»
   * ma'nosini ham, «bosqichsiz ochiq» ma'nosini ham bildira olmasdi.
   */
  const [inviting, setInviting] = useState<PipelineStage | null | 'closed'>('closed')

  /**
   * Bosqich oynasi: `null` — yopiq, bosqich — o'sha bosqich QOTIB
   * qo'yilgan holda ochiq.
   *
   * Bu yerda IKKI holat yetadi — suhbat oynasidan farqli o'laroq, u
   * bosqichsiz OCHILMAYDI: qator faqat aniq bir bosqich tugmasidan
   * ochiladi, «bosqichni tanlab yoz» ko'rinishi esa «Bosqichlar»
   * tabida turibdi.
   */
  const [recording, setRecording] = useState<PipelineStage | null>(null)

  /*
   * ⚠️ Hook'lar QAYTISHDAN oldin: pastda bo'sh ma'lumotnomada karta
   * umuman chizilmaydi va shartli chaqirilgan hook React qoidasini
   * buzardi.
   */
  const last = useLastActivity(candidate)

  const list = stages.data ?? []

  /*
   * Bo'sh ma'lumotnomada karta UMUMAN chizilmaydi: bo'sh qutini
   * ko'rsatish HR ga hech narsa aytmaydi, voronkani esa
   * «Ma'lumotnomalar» bo'limida sozlash kerak (voronka taxtasidagi bilan
   * bir xil qaror).
   */
  if (list.length === 0) return null

  /*
   * Tugma bosildi — mutatsiya YO'Q, faqat oyna ochiladi. Shu sababli
   * bu yerda «boshqa so'rov ketayotgan bo'lsa to'xta» tekshiruvi ham
   * kerak emas: kutiladigan holat oynaning O'ZIDA (saqlash tugmasi)
   * va ikkita so'rov yonma-yon ketishi mumkin emas.
   */
  const move = (stage: PipelineStage) => {
    if (stage.invitesToInterview) {
      setInviting(stage)
      return
    }

    setRecording(stage)
  }

  /*
   * «Muddati keldi» yorlig'i — sana BUGUN yoki o'tgan bo'lsa. Sanalar
   * ikkalasi ham `YYYY-MM-DD` (Toshkent kalendari), shu sababli
   * SATRIY solishtirish to'g'ri ishlaydi va `Date` yaratish kerak
   * emas — mintaqa siljishi bu yerga umuman aralashmaydi.
   *
   * Yorliq FAQAT muddati kelganda chiziladi: sananing neytral qiymati
   * shaxsiy ma'lumotlar kartasida allaqachon turibdi va bitta ekranda
   * ikki marta takrorlanishi shovqin bo'lardi (SPEC §5.2).
   */
  const dueDate =
    candidate.nextContactDate !== null && candidate.nextContactDate <= today()
      ? candidate.nextContactDate
      : null

  return (
    <Card>
      <CardContent className="py-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-caption1 text-[var(--fg-tertiary)]">
            {t('recruiting.stageMoveTitle')}
          </p>

          {dueDate !== null && (
            <Badge tone="warning" size="sm" dot title={date(dueDate)}>
              {t('recruiting.nextContactDue')}
            </Badge>
          )}
        </div>

        {/*
          Oxirgi MATNLI yozuv — bitta, eng yangisi. Butun tarixni bu
          yerga chizish RAD ETILDI: karta lentaga aylanardi va lenta
          allaqachon «Bosqichlar» tabida bor. Matn yo'q bo'lsa
          umuman chizilmaydi — bo'sh quti HR ga hech narsa aytmaydi
          (anketa kartasi va bu kartaning o'zi bilan bir xil qoida).
        */}
        {last !== null && (
          <div className="mt-3 rounded-md bg-[var(--bg-sunken)] px-3 py-2">
            <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
              <span className="text-caption2 text-[var(--fg-tertiary)]">
                {t('recruiting.lastActivity')}
              </span>

              <span className="text-footnote font-medium">
                {last.source === 'stage'
                  ? (last.stage ?? '—')
                  : t(`recruiting.noteKind.${lowerFirst(last.kind)}`)}
              </span>

              <span className="tabular text-caption2 text-[var(--fg-tertiary)]">
                {dateTime(last.at)}
              </span>
            </div>

            {/*
              Uch qatordan keyin kesiladi: bu yerda maqsad «nima
              bo'lganini eslatish», to'liq matnni o'qish emas — u
              «Bosqichlar» lentasida to'lig'icha turadi.
            */}
            <p className="mt-1 line-clamp-3 text-footnote text-[var(--fg-secondary)]">
              {last.text}
            </p>
          </div>
        )}

        <div className="mt-3 flex flex-wrap gap-2">
          {list.map((stage) => {
            const current = stage.id === candidate.currentStageId

            if (current) {
              return (
                <span
                  key={stage.id}
                  className={cn(
                    buttonVariants({ variant: 'primary', size: 'sm', pill: true }),
                    'cursor-default',
                  )}
                  aria-current="step"
                >
                  {stage.name}
                </span>
              )
            }

            /*
              Tugmada yuklanish belgisi YO'Q va `disabled` ham yo'q:
              bosish so'rov emas, oyna ochadi. Kutish holati oynaning
              saqlash tugmasida — u ham yagona joyda, chunki so'rovni
              endi faqat oyna yuboradi.
            */
            return (
              <Button
                key={stage.id}
                type="button"
                size="sm"
                variant="secondary"
                pill
                onClick={() => move(stage)}
              >
                {/*
                  Kalendar ikonasi — «bu tugma xabar yuboradi» degan
                  ogohlantirish: suhbat oynasi ochiladi va nomzodga
                  Telegram taklifi ketadi.
                */}
                {stage.invitesToInterview && <CalendarClock />}
                {stage.name}
              </Button>
            )
          })}
        </div>

        {/*
          Chaqirilgan suhbat vaqti — SHU YERDA, bosqich qatorining
          ostida. Sabab: HR uni aynan shu joyda qo'ygan va ko'chirish
          ham shu yerdan boshlanadi. Nomzod kartasining boshqa
          qismlariga tarqatilsa, «qachonga chaqirgan edim?» degan savol
          uchun ekran kezish kerak bo'lardi.

          Vaqt QO'YILGAN, lekin xabar hali ketmagan bo'lsa buni ham
          aytadi: bot bir daqiqada bir marta navbatni so'raydi va
          «yuborildimi?» degan savolga javob ko'rinib turishi kerak.
          Telegram suhbati yo'q nomzodda esa xabar HECH QACHON
          ketmaydi — matn shuni to'g'ridan-to'g'ri aytadi.
        */}
        {candidate.interviewAt !== null && (
          <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-md bg-[var(--bg-sunken)] px-3 py-2">
            <span className="flex items-center gap-1.5 text-footnote">
              <CalendarClock className="size-4 text-[var(--fg-tertiary)]" />
              <span className="tabular font-medium">{dateTime(candidate.interviewAt)}</span>
            </span>

            {/*
              Yetkazilmagan taklif (nomzod botni bloklagan) — bot qayta
              urinmaydi (egasining qarori, 2026-10-05: bitta urinish), shu
              sababli HR'ga ANIQ aytiladi: vaqtni nomzod faqat qo'ng'iroqdan
              biladi.
            */}
            <span
              className={cn(
                'text-caption2',
                candidate.interviewNoticeDelivered === false
                  ? 'text-[var(--danger-text)]'
                  : 'text-[var(--fg-tertiary)]',
              )}
            >
              {!candidate.telegramLinked
                ? t('recruiting.interviewNoTelegramShort')
                : candidate.interviewNoticeSentAt === null
                  ? t('recruiting.interviewQueued')
                  : candidate.interviewNoticeDelivered === false
                    ? t('recruiting.interviewNotDelivered')
                    : candidate.interviewReminderSentAt === null
                      ? t('recruiting.interviewSent')
                      : candidate.interviewReminderDelivered === false
                        ? t('recruiting.interviewReminderNotDelivered')
                        : t('recruiting.interviewReminderSent')}
            </span>

            {!candidate.telegramLinked && <CandidateTelegramLinkButton candidateId={candidate.id} />}

            <Button
              type="button"
              size="sm"
              variant="ghost"
              className="ms-auto"
              onClick={() => setInviting(null)}
            >
              {t('recruiting.interviewReschedule')}
            </Button>
          </div>
        )}

        <p className="mt-3 text-caption2 text-[var(--fg-tertiary)]">
          {t('recruiting.stageMoveHint')}
        </p>
      </CardContent>

      <CandidateInterviewDialog
        candidate={candidate}
        stage={inviting === 'closed' ? null : inviting}
        open={inviting !== 'closed'}
        onClose={() => setInviting('closed')}
      />

      <CandidateStageDialog
        candidate={candidate}
        lockedStage={recording}
        open={recording !== null}
        onClose={() => setRecording(null)}
      />
    </Card>
  )
}

/** «Oxirgi nima bo'ldi» chizig'ining mazmuni — ikki oqimdan bittasi. */
type LastActivity =
  | { source: 'stage'; stage: string | null; at: string; text: string }
  | { source: 'note'; kind: CandidateNoteKind; at: string; text: string }

/**
 * Eng yangi MATNLI yozuv — bosqich xulosalari va bosqichsiz izohlar
 * ichidan bittasi.
 *
 * <b>Nega ikkala oqim ham o'qiladi.</b> HR uchun «oxirgi nima bo'ldi»
 * degan savolga javob ikki joyda: bosqich xulosasi
 * (`stages[].conclusion`) va bosqichi bo'lmagan amal izohi
 * (`notes[]` — boshqa vakansiyaga o'tkazish, rezervdan qaytarish,
 * suhbatni ko'chirish). Bittasini o'qish «ikki kun oldin gaplashgan
 * edik» degan yozuvni jimgina yashirardi.
 *
 * <b>Nega SANA bo'yicha, ro'yxat tartibi bo'yicha emas.</b> Ikki
 * oqimning tartibini bir-biriga qo'shib bo'lmaydi — ular alohida
 * massiv. Solishtirish `Date.parse` bilan: serverdan kelgan ISO satr
 * «Z» yoki «+00:00» bilan yozilishi mumkin va satriy solishtirish
 * o'shanda jimgina noto'g'ri javob berardi.
 *
 * ⚠️ Bosqich yozuvining kaliti — `recordedAt` (YOZILGAN vaqt), `date`
 * emas: `date` QO'LDA kiritiladi va o'tgan kun bilan yozilishi odatiy
 * hol («kecha suhbat bo'lgan edi»). Lenta ham AYNAN shu kalitni —
 * `recordedAt` ni — ishlatadi, ya'ni ikkala ko'rinish bir xil javob
 * beradi (`candidate-stages-tab.tsx` izohi).
 */
function useLastActivity(candidate: Candidate): LastActivity | null {
  return useMemo(() => {
    const items: LastActivity[] = []

    /* Xulosasi BO'SH bosqich yozuvi chetlab o'tiladi: «Кенгашга
       чақирилди — kutilmoqda» qatorining o'zi «nima bo'ldi» degan
       savolga javob bermaydi va haqiqiy matnni pastga surib
       yuborardi. */
    for (const entry of candidate.stages) {
      if (entry.conclusion === null) continue
      items.push({
        source: 'stage',
        stage: entry.stage,
        at: entry.recordedAt,
        text: entry.conclusion,
      })
    }

    /* Izohda matn bo'lmasligi mumkin emas — server bo'sh matnga
       umuman qator yaratmaydi (`candidate_notes`, SPEC §2.3). */
    for (const note of candidate.notes) {
      items.push({ source: 'note', kind: note.kind, at: note.createdAt, text: note.text })
    }

    let best: LastActivity | null = null
    let bestAt = -Infinity

    for (const item of items) {
      const ms = Date.parse(item.at)
      /* Tanilmagan sana — yozuv butunlay tashlab ketiladi: «1970 yil»
         deb chizilgani, chizilmaganidan yomonroq. */
      if (Number.isNaN(ms) || ms <= bestAt) continue
      bestAt = ms
      best = item
    }

    return best
  }, [candidate.stages, candidate.notes])
}
