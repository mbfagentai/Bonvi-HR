import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'

import { api } from '@/api/client'
import { ApiError } from '@/api/errors'
import type { AbsenceRequestDto, CreateAbsenceRequest } from '@/api/types'
import { shortTime } from '@/data/attendance'
import { invalidateRequests, useMe, useRequests } from '@/data/queries'
import { useFormat } from '@/lib/format'
import { addDays, daysBetween, parseIsoDate, todayIso } from '@/lib/time'
import { useErrorText } from '@/lib/use-error-message'
import { PushedScreen } from '@/ui/app-shell'
import { BottomAction, Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { EmptyState, ErrorState, InlineMessage, SkeletonLines } from '@/ui/feedback'
import { DateRow, Field, SegmentedControl, SelectRow, TextArea, TimeRow } from '@/ui/form'
import { ConfirmSheet } from '@/ui/sheet'
import { useToast } from '@/ui/toast'

/**
 * Davomiylik — Verifix'dagi «Часть дня / Весь день / Несколько дней»
 * (SPEC-jadval-sorovi §B). Server shartnomasi O'ZGARMAGAN: rejim simda
 * yo'q, u sana va vaqtlardan kelib chiqadi (vaqt bor → qism kun,
 * bitta sana → butun kun, aks holda — bir necha kun).
 */
type Mode = 'part' | 'whole' | 'multi'

/** Formaning boshlang'ich qiymatlari — «iflos» forma shular bilan solishtiriladi. */
interface RequestValues {
  mode: Mode
  absenceTypeId: string | null
  fromDate: string
  toDate: string
  fromTime: string
  toTime: string
  note: string
}

/**
 * Yangi so'rovda — sukutlar; tahrirda — saqlangan so'rovdan. Rejim simda
 * yo'q, u yaratishdagi qoidaning teskarisi bilan tiklanadi (vaqt bor → qism
 * kun, bitta sana → butun kun, aks holda — bir necha kun). «Bir necha kun»
 * bo'lmasa tugash sanasi — yaratishdagi sukut (ertangi kun): xodim rejimni
 * almashtirsa, u boshlanishdan keyin turadi.
 */
function initialValues(editing: AbsenceRequestDto | null, prefill: string): RequestValues {
  if (!editing) {
    // Sukut — «Qism kun»: Verifix formasi ham «Soatlik» bilan ochiladi va
    // MBF dagi so'rovlarning ko'pi bir necha soatlik.
    return {
      mode: 'part',
      absenceTypeId: null,
      fromDate: prefill,
      toDate: addDays(prefill, 1),
      fromTime: '',
      toTime: '',
      note: '',
    }
  }

  const mode: Mode = editing.fromTime ? 'part' : editing.fromDate === editing.toDate ? 'whole' : 'multi'

  return {
    mode,
    absenceTypeId: editing.absenceTypeId,
    fromDate: editing.fromDate,
    toDate: mode === 'multi' ? editing.toDate : addDays(editing.fromDate, 1),
    fromTime: shortTime(editing.fromTime) ?? '',
    toTime: shortTime(editing.toTime) ?? '',
    note: editing.note ?? '',
  }
}

/**
 * YO'QLIK SO'ROVI formasi (UX §A.3, §E.5).
 *
 * Verifix'da «so'rov turini tanlang» varag'ida sakkizta band bor edi;
 * bizda ikkitasi — yo'qlik va jadval (SPEC-jadval-sorovi §C4). Yo'qlik
 * TURI esa shu formadagi maydon, chunki u ma'lumotnomadan keladi
 * (UX §A.1).
 *
 * Manzilda `:id` bo'lsa (`/sorovlar/:id/ozgartirish`) — o'sha forma TAHRIR
 * rejimida: qaror kutayotgan so'rov o'zgartiriladi (SPEC-sorov-tahriri §7.1).
 */
export function RequestNewScreen() {
  const { id } = useParams<{ id: string }>()

  return id ? <RequestEditScreen id={id} /> : <RequestForm editing={null} />
}

/**
 * Tahrir rejimi: so'rov `useRequests()` keshidan (tafsilot ekrani kabi).
 * Forma faqat so'rov topilib, u hali QAROR KUTAYOTGAN bo'lsa chiziladi.
 *
 * ⚠️ Skelet faqat ro'yxat UMUMAN yo'qligida: 409 dan keyingi qayta o'qishda
 * eski ro'yxat turadi, ya'ni forma va xodim yozgan qiymatlar joyida qoladi.
 */
function RequestEditScreen({ id }: { id: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const requests = useRequests()
  const request = (requests.data ?? []).filter((item) => item.id === id)[0] ?? null

  // Server «eskirgan» degan bo'lsa — ro'yxat yangidan: versiya yangilanadi,
  // qaror qabul qilingan bo'lsa forma o'rniga izoh chiqadi.
  // 409 dan keyin forma so'rovning YANGI holati bilan qayta chiziladi (kalit — versiya) va
  // xodimga sababi aytiladi: eski qiymatlar ustidan «Saqlash» HR o'zgarishini ko'rmasdan yozib yuborardi.
  const [staleSeen, setStaleSeen] = useState(false)

  const reload = (): void => {
    setStaleSeen(true)
    invalidateRequests()
    requests.refetch()
  }

  if (!requests.data) {
    return (
      <PushedScreen title={t('requests.editTitle')}>
        <Card>{requests.error ? <ErrorState onRetry={requests.refetch} /> : <SkeletonLines rows={5} />}</Card>
      </PushedScreen>
    )
  }

  if (!request) {
    return (
      <PushedScreen title={t('requests.editTitle')}>
        <Card>
          <EmptyState
            icon="document-text"
            title={t('requests.notFound')}
            text={t('requests.notFoundText')}
            action={<Button onClick={() => navigate('/sorovlar')}>{t('requests.title')}</Button>}
          />
        </Card>
      </PushedScreen>
    )
  }

  // Qaror qabul qilingan yoki bekor qilingan — o'zgartirib bo'lmaydi (server ham 409 beradi)
  if (request.status !== 'Pending') {
    return (
      <PushedScreen title={t('requests.editTitle')}>
        <InlineMessage
          tone="warning"
          action={
            <Button variant="secondary" size="compact" onClick={() => navigate(-1)}>
              {t('common.back')}
            </Button>
          }
        >
          {t('requests.notEditable')}
        </InlineMessage>
      </PushedScreen>
    )
  }

  return <RequestForm key={`${request.id}:${request.version ?? 0}`} editing={request} onStale={reload} staleNotice={staleSeen} />
}

function RequestForm({
  editing,
  onStale,
  staleNotice = false,
}: {
  editing: AbsenceRequestDto | null
  onStale?: () => void
  staleNotice?: boolean
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const errorText = useErrorText()
  const [params] = useSearchParams()

  const me = useMe()

  const prefill = parseIsoDate(params.get('kun')) ? `${params.get('kun')}` : todayIso()

  // Boshlang'ich qiymatlar BIR MARTA olinadi: qayta o'qilgan so'rov (409 dan
  // keyin) xodim yozganini ustidan yozmaydi.
  const [initial] = useState(() => initialValues(editing, prefill))

  const [mode, setMode] = useState<Mode>(initial.mode)
  const [absenceTypeId, setAbsenceTypeId] = useState<string | null>(initial.absenceTypeId)
  const [fromDate, setFromDate] = useState(initial.fromDate)
  // Faqat «Bir necha kun» da o'qiladi — doim boshlanishdan KEYIN turadi.
  const [toDate, setToDate] = useState(initial.toDate)
  const [fromTime, setFromTime] = useState(initial.fromTime)
  const [toTime, setToTime] = useState(initial.toTime)
  const [note, setNote] = useState(initial.note)

  const [touched, setTouched] = useState(false)
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<ApiError | null>(null)
  const [leaveOpen, setLeaveOpen] = useState(false)

  // «Iflos» forma — yozilgan narsa bor. Tasdiq faqat shu holatda
  // so'raladi: bo'sh formadan chiqishda savol berish — bezovta qilish.
  // Yangi so'rovda rejimni almashtirishning o'zi «yozish» emas; tahrirda
  // esa u saqlangan so'rovni o'zgartiradi (masalan vaqtlarni olib tashlaydi).
  const dirty =
    absenceTypeId !== initial.absenceTypeId ||
    note !== initial.note ||
    fromDate !== initial.fromDate ||
    toDate !== initial.toDate ||
    fromTime !== initial.fromTime ||
    toTime !== initial.toTime ||
    (editing !== null && mode !== initial.mode)

  // Tanlangan tur xodimga ochiq ro'yxatda bo'lishi kerak. Tahrirda HR tanlagan
  // yopiq yoki faolsiz tur ro'yxatda yo'q — maydon bo'sh ko'rinadi va xodim
  // boshqasini tanlaydi (server ham yaratishdagi qoidani qo'llaydi).
  const kind = (me.data?.absenceTypes ?? []).filter((item) => item.id === absenceTypeId)[0] ?? null
  const kindMissing = !absenceTypeId || (me.data !== undefined && kind === null)
  const kindError = touched && kindMissing ? t('requests.kindRequired') : null

  // Tur ish vaqtimi yoki shaxsiy vaqtmi — xodim ishlangan vaqt nega kamayishini
  // oldindan bilsin. Eski server bayroqni bermaydi — izoh ham yo'q.
  const kindHelper =
    kind && kind.countsAsWorkTime !== undefined
      ? kind.countsAsWorkTime
        ? t('requests.kindWorkTime')
        : t('requests.kindPersonalTime')
      : undefined

  // Qism kun: ikkala vaqt MAJBURIY va bir kun ichida (yarim tundan o'tgan
  // yo'qlikni server rad etadi — `davomat.sorov_vaqti_notogri`).
  const fromTimeError = touched && mode === 'part' && fromTime === '' ? t('requests.timeRequired') : null
  const toTimeError = !touched || mode !== 'part'
    ? null
    : toTime === ''
      ? t('requests.timeRequired')
      : fromTime !== '' && toTime <= fromTime
        ? t('requests.timeOrder')
        : null

  const dateError = touched && mode === 'multi' && toDate <= fromDate ? t('requests.dateAfter') : null
  const dayCount = daysBetween(fromDate, toDate)

  const changeMode = (next: Mode): void => {
    setMode(next)
    if (next === 'multi' && toDate <= fromDate) setToDate(addDays(fromDate, 1))
  }

  const changeFromDate = (iso: string): void => {
    setFromDate(iso)
    // Oxiri boshidan oldin qolib ketmasin — eng ko'p uchraydigan xatoni
    // forma o'zi tuzatadi.
    if (toDate <= iso) setToDate(addDays(iso, 1))
  }

  const submit = async (): Promise<void> => {
    setTouched(true)
    if (!absenceTypeId || kindMissing) return
    if (mode === 'part' && (fromTime === '' || toTime === '' || toTime <= fromTime)) return
    if (mode === 'multi' && toDate <= fromDate) return

    setSending(true)
    setSendError(null)

    // Vaqt faqat «Qism kun» da yuboriladi: boshqa rejimda tanlangan
    // vaqt holatda qoladi (xodim qaytsa yo'qolmaydi), lekin simga
    // chiqmaydi — server uni ko'p kunlik so'rovda rad etardi.
    const body: CreateAbsenceRequest = {
      absenceTypeId,
      fromDate,
      toDate: mode === 'multi' ? toDate : fromDate,
      fromTime: mode === 'part' ? `${fromTime}:00` : null,
      toTime: mode === 'part' ? `${toTime}:00` : null,
      note: note.trim() === '' ? null : note.trim(),
    }

    try {
      if (editing) {
        // Versiya — xodim KO'RGAN nusxaniki: o'rtada HR o'zgartirgan yoki qaror
        // qilgan bo'lsa server 409 beradi, jimgina ustidan yozilmaydi.
        await api.updateRequest(editing.id, { ...body, version: editing.version ?? 0 })

        invalidateRequests()
        toast.show(t('requests.saved'), 'success')
        // Tafsilot ekraniga qaytish — u yangilangan so'rovni qaytadan o'qiydi
        navigate(-1)
      } else {
        await api.createRequest(body)

        invalidateRequests()
        toast.show(t('requests.sent'), 'success')
        navigate('/sorovlar', { replace: true })
      }
    } catch (cause: unknown) {
      const error = ApiError.from(cause)
      setSendError(error)
      // 409 — so'rov boshqa joyda o'zgargan yoki endi kutmayapti: ro'yxat
      // yangidan o'qiladi, forma esa yozilganini saqlaydi.
      if (editing && error.status === 409 && onStale) onStale()
    } finally {
      setSending(false)
    }
  }

  const goBack = (): void => {
    if (dirty) {
      setLeaveOpen(true)
      return
    }
    navigate(-1)
  }

  const summary =
    mode === 'part'
      ? fromTime !== '' && toTime !== ''
        ? `${format.dateLong(fromDate)}, ${fromTime}–${toTime}`
        : format.dateLong(fromDate)
      : mode === 'whole'
        ? format.dateLong(fromDate)
        : `${t('requests.periodRange', { from: format.date(fromDate), to: format.date(toDate) })}${
            dayCount > 1 ? ` · ${t('requests.periodDays', { n: dayCount })}` : ''
          }`

  return (
    <>
      <PushedScreen title={editing ? t('requests.editTitle') : t('requests.newTitle')} onBack={goBack} bottomSpace>
        {staleNotice ? <InlineMessage tone="warning">{t('requests.staleReloaded')}</InlineMessage> : null}

        <Card>
          {me.isLoading ? (
            <SkeletonLines rows={4} />
          ) : me.error && !me.data ? (
            <ErrorState onRetry={me.refetch} />
          ) : (
            <div className="space-y-4">
              <SegmentedControl<Mode>
                label={t('requests.modeLabel')}
                value={mode}
                options={[
                  { value: 'part', label: t('requests.modePart') },
                  { value: 'whole', label: t('requests.modeWhole') },
                  { value: 'multi', label: t('requests.modeMulti') },
                ]}
                onChange={changeMode}
              />

              <Field label={t('requests.kind')} required error={kindError} helper={kindHelper}>
                <SelectRow
                  sheetTitle={t('requests.selectKind')}
                  value={absenceTypeId}
                  options={me.data?.absenceTypes ?? []}
                  placeholder={t('common.select')}
                  emptyText={t('requests.kindsEmpty')}
                  invalid={kindError !== null}
                  onChange={(id) => setAbsenceTypeId(id)}
                />
              </Field>

              <Field label={mode === 'multi' ? t('requests.fromDate') : t('requests.date')} required>
                <DateRow
                  sheetTitle={mode === 'multi' ? t('requests.fromDate') : t('requests.date')}
                  value={fromDate}
                  onChange={changeFromDate}
                />
              </Field>

              {mode === 'multi' ? (
                <Field
                  label={t('requests.toDate')}
                  required
                  error={dateError}
                  helper={dayCount > 1 ? t('requests.periodDays', { n: dayCount }) : undefined}
                >
                  <DateRow
                    sheetTitle={t('requests.toDate')}
                    value={toDate}
                    min={addDays(fromDate, 1)}
                    invalid={dateError !== null}
                    onChange={(iso) => setToDate(iso)}
                  />
                </Field>
              ) : null}

              {mode === 'part' ? (
                <>
                  <Field label={t('requests.fromTime')} required error={fromTimeError}>
                    <TimeRow
                      sheetTitle={t('requests.fromTime')}
                      value={fromTime}
                      placeholder={t('common.select')}
                      invalid={fromTimeError !== null}
                      onChange={setFromTime}
                    />
                  </Field>

                  <Field label={t('requests.toTime')} required error={toTimeError}>
                    <TimeRow
                      sheetTitle={t('requests.toTime')}
                      value={toTime}
                      placeholder={t('common.select')}
                      invalid={toTimeError !== null}
                      onChange={setToTime}
                    />
                  </Field>
                </>
              ) : null}

              <Field label={t('requests.note')}>
                <TextArea
                  value={note}
                  onChange={setNote}
                  placeholder={t('requests.notePlaceholder')}
                />
              </Field>
            </div>
          )}
        </Card>

        {sendError ? (
          <InlineMessage
            tone="danger"
            title={editing ? t('requests.editFailed') : t('requests.sendFailed')}
            assertive
          >
            {errorText(sendError).body}
          </InlineMessage>
        ) : null}

        <p className="nums px-1 text-footnote text-fg-tertiary">{summary}</p>
      </PushedScreen>

      <BottomAction>
        <Button block busy={sending} onClick={() => void submit()}>
          {editing ? t('common.save') : t('common.submit')}
        </Button>
      </BottomAction>

      <ConfirmSheet
        open={leaveOpen}
        title={t('requests.dirtyTitle')}
        confirmLabel={t('requests.dirtyLeave')}
        cancelLabel={t('common.stay')}
        danger
        onConfirm={() => {
          setLeaveOpen(false)
          navigate(-1)
        }}
        onClose={() => setLeaveOpen(false)}
      />
    </>
  )
}
