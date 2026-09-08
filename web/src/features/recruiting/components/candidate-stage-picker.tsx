import { CalendarClock, LoaderCircle } from 'lucide-react'
import { useState } from 'react'

import { cn } from '@/shared/lib/cn'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, CardContent, buttonVariants } from '@/shared/ui'
import type { Candidate, PipelineStage } from '../api/recruiting-api'
import { useRecordStage, useStages } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { CandidateInterviewDialog } from './candidate-interview-dialog'

/**
 * ⭐ Bosqichni BIR BOSISHDA o'zgartirish — nomzod kartasidagi tezkor qator.
 *
 * <b>Nima uchun qo'shildi.</b> Foydalanuvchi talabi (2026-09-08): HR
 * nomzodga telefon qilgandan keyin uni «Гаплашилди» bosqichiga, kengashga
 * chaqirgandan keyin esa «Кенгашга чақирилди» bosqichiga o'tkazishi kerak.
 * Ilgari buning yagona yo'li «Bosqichlar» tabini ochib, oyna chaqirib,
 * bosqichni ro'yxatdan tanlab, saqlash edi — kuniga o'nlab marta
 * takrorlanadigan amal uchun to'rt qadam.
 *
 * <b>Nega tugmalar KODDA sanab chiqilmagan.</b> «Гаплашилди» va
 * «Кенгашга чақирилди» — bu kod emas, MA'LUMOTNOMA yozuvi
 * (<c>PipelineStage</c> izohi: voronkani HR o'zi boshqaradi). Ikkita nom
 * kodga yozilsa, HR bosqichni qayta nomlagan kuni tugma jimgina yo'qolardi
 * yoki noto'g'ri bosqichga o'tkazardi. Shu sababli qator FAOL bosqichlar
 * ro'yxatidan chiziladi: HR ma'lumotnomaga bosqich qo'shsa, tugmasi ham
 * o'zidan paydo bo'ladi, tartibi ham voronkanikidek qoladi.
 *
 * <b>Nima yuboriladi.</b> Voronka taxtasidagi sudrash bilan AYNAN bir xil
 * so'rov (`useMoveCandidateToStage` izohi): `outcome = Pending`, sana va
 * xulosa bo'sh — «bosqich boshlandi, natija hali yo'q». Natijani HR
 * keyinroq «Bosqichlar» tabidagi oyna orqali yozadi va SHU yozuv
 * to'ldiriladi, tarixda ikkinchi qator paydo bo'lmaydi.
 *
 * <b>Ba'zi bosqich TO'G'RIDAN-TO'G'RI o'tkazmaydi.</b> Ma'lumotnomada
 * «suhbat taklifi» bayrog'i qo'yilgan bosqich (odatda «Кенгашга
 * чақирилди») avval OYNA ochadi va HR dan sana bilan vaqtni so'raydi —
 * nomzodga aynan shu ma'lumot bilan Telegram xabari ketadi. Bayroq
 * bosqichning o'zida turadi, kodda emas: sabab
 * `PipelineStage.InvitesToInterview` domen izohida.
 *
 * <b>Joriy bosqich — tugma EMAS.</b> U tugma bo'lib, `disabled` qilinsa
 * xiralashardi (`disabled:opacity-40`) va «ishlamayapti» degan ma'no
 * berardi. Aslida u ishlamaydigan tugma emas, HOLAT ko'rsatkichi —
 * shuning uchun bir xil o'lchamdagi to'ldirilgan yorliq.
 */
export function CandidateStagePicker({ candidate }: { candidate: Candidate }) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()

  /*
   * Faqat FAOL bosqichlar: faolsizlantirilganiga o'tkazib bo'lmaydi va
   * server buni 409 bilan rad etadi («bosqich.faol_emas»). Ro'yxat
   * keshdan keladi — uni nomzod kartasi allaqachon so'ragan
   * (`candidate-card-page.tsx`).
   */
  const stages = useStages(true)
  const record = useRecordStage()

  /**
   * Suhbat oynasi: `closed` — yopiq, `null` — ochiq, lekin bosqichni
   * o'zgartirmaydi (vaqtni ko'chirish), bosqich — o'sha bosqichga
   * o'tkazadi.
   *
   * Uch holat KERAK va ikkitasi yetmasdi: `null` ning o'zi «yopiq»
   * ma'nosini ham, «bosqichsiz ochiq» ma'nosini ham bildira olmasdi.
   */
  const [inviting, setInviting] = useState<PipelineStage | null | 'closed'>('closed')

  const list = stages.data ?? []

  /*
   * Bo'sh ma'lumotnomada karta UMUMAN chizilmaydi: bo'sh qutini
   * ko'rsatish HR ga hech narsa aytmaydi, voronkani esa
   * «Ma'lumotnomalar» bo'limida sozlash kerak (voronka taxtasidagi bilan
   * bir xil qaror).
   */
  if (list.length === 0) return null

  const move = (stage: PipelineStage) => {
    if (record.isPending) return

    if (stage.invitesToInterview) {
      setInviting(stage)
      return
    }

    record.mutate({
      id: candidate.id,
      stageId: stage.id,
      outcome: 'Pending',
      date: null,
      conclusion: null,
    })
  }

  return (
    <Card>
      <CardContent className="py-4">
        <p className="text-caption1 text-[var(--fg-tertiary)]">{t('recruiting.stageMoveTitle')}</p>

        {record.isError && (
          <Alert tone="danger" className="mt-3">
            {errorMessage(recruitingErrorCode(record.error))}
          </Alert>
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
              Yuklanish belgisi FAQAT bosilgan tugmada: butun qatorni
              aylantirish HR ga qaysi bosqich yozilayotganini
              ko'rsatmasdi. Qolganlari shu paytda o'chiriladi — ikkita
              so'rov ketma-ket ketsa, oxirgisi g'olib chiqib, HR
              kutmagan bosqich qolardi.
            */
            const saving = record.isPending && record.variables?.stageId === stage.id

            return (
              <Button
                key={stage.id}
                type="button"
                size="sm"
                variant="secondary"
                pill
                disabled={record.isPending}
                onClick={() => move(stage)}
              >
                {saving ? (
                  <LoaderCircle className="animate-spin" />
                ) : (
                  stage.invitesToInterview && <CalendarClock />
                )}
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

            <span className="text-caption2 text-[var(--fg-tertiary)]">
              {candidate.source !== 'TelegramBot'
                ? t('recruiting.interviewNoTelegramShort')
                : candidate.interviewNoticeSentAt === null
                  ? t('recruiting.interviewQueued')
                  : t('recruiting.interviewSent')}
            </span>

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
    </Card>
  )
}
