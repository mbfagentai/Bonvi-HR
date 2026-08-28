import { CircleCheck, CircleX, Clock, FileSignature, LoaderCircle, Pencil, Plus } from 'lucide-react'
import { useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Badge,
  Button,
  Card,
  CardContent,
  Dialog,
  EmptyState,
  IconTile,
  Skeleton,
  TextArea,
  TextField,
} from '@/shared/ui'
import type { Candidate, JobOffer } from '../api/recruiting-api'
import { useCloseOffer, useOffers } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { OFFER_LOOK, orNull, today } from '../lib/recruiting-presentation'
import { OfferAcceptDialog } from './offer-accept-dialog'
import { OfferFormDialog } from './offer-form-dialog'

/**
 * ⭐ Ish takliflari — voronkaning oxirgi rasmiy qadami.
 *
 * <b>Nega ro'yxat, bitta taklif emas.</b> Faol taklif BITTA bo'ladi
 * (server ikkinchisini rad etadi — «taklif.faol_mavjud»), lekin vaqt
 * o'tib bir nechtasi to'planadi: rad javobidan keyin HR ko'pincha
 * oklani ko'tarib ikkinchi taklif yuboradi. Ularning ketma-ketligi —
 * MUZOKARA TARIXI va u ko'rinib turishi kerak («qancha taklif
 * qilgandik?» degan savolga javob).
 *
 * <b>Nega taklif rad etilganda nomzod yopilmaydi.</b> Rad javob
 * muzokaraning oxiri emas (`JobOffer` domen izohi). Nomzodni «voz
 * kechgan» qilish alohida amal va u kartaning yuqori qismida.
 *
 * <b>Ro'yxat alohida so'rov bilan olinadi</b>, garchi u nomzod
 * kartasida ham bo'lsa: kartadagi nusxa karta so'rovi bilan eskiradi,
 * bu esa taklif amalidan keyin darhol yangilanadi
 * (`fetchOffers` izohi).
 */
export function CandidateOffersTab({
  candidate,
  canManage,
}: {
  candidate: Candidate
  canManage: boolean
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const offers = useOffers(candidate.id)

  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<JobOffer | null>(null)
  const [accepting, setAccepting] = useState<JobOffer | null>(null)
  const [closing, setClosing] = useState<{ offer: JobOffer; action: 'decline' | 'expire' } | null>(
    null,
  )

  /*
   * Nomzod allaqachon ishga olingan bo'lsa, yangi taklif yuborib
   * bo'lmaydi — server «nomzod.allaqachon_ishga_olingan» qaytaradi.
   * Tugmani ko'rsatib, keyin xato berish o'rniga u umuman
   * chizilmaydi va sabab yozib qo'yiladi.
   */
  const closed = candidate.status !== 'InProgress'
  const hasPending = (offers.data ?? []).some((offer) => offer.status === 'Sent')

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('recruiting.offersSubtitle', { n: (offers.data ?? []).length })}
        </p>

        {canManage && !closed && !hasPending && (
          <Button type="button" size="sm" variant="create" onClick={() => setCreating(true)}>
            <Plus />
            {t('recruiting.offerCreate')}
          </Button>
        )}
      </div>

      {canManage && hasPending && (
        <Alert tone="info">{t('recruiting.offerPendingHint')}</Alert>
      )}

      {offers.isError && (
        <Alert tone="danger">{errorMessage(recruitingErrorCode(offers.error))}</Alert>
      )}

      {offers.isPending ? (
        <Card>
          <div className="space-y-3 p-5">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
          </div>
        </Card>
      ) : (offers.data ?? []).length === 0 ? (
        <Card>
          <EmptyState
            icon={FileSignature}
            title={t('recruiting.offersEmptyTitle')}
            description={t('recruiting.offersEmptyText')}
            action={
              canManage && !closed ? (
                <Button type="button" size="sm" variant="create" onClick={() => setCreating(true)}>
                  <Plus />
                  {t('recruiting.offerCreate')}
                </Button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <div className="space-y-3">
          {(offers.data ?? []).map((offer) => (
            <OfferCard
              key={offer.id}
              offer={offer}
              canManage={canManage}
              onEdit={() => setEditing(offer)}
              onAccept={() => setAccepting(offer)}
              onClose={(action) => setClosing({ offer, action })}
            />
          ))}
        </div>
      )}

      <OfferFormDialog
        candidate={candidate}
        item={editing}
        open={creating || editing !== null}
        onClose={() => {
          setCreating(false)
          setEditing(null)
        }}
      />

      <OfferAcceptDialog
        candidate={candidate}
        offer={accepting}
        onClose={() => setAccepting(null)}
      />

      <OfferCloseDialog item={closing} onClose={() => setClosing(null)} />
    </div>
  )
}

/**
 * Bitta taklif.
 *
 * Amallar FAQAT «yuborilgan» holatdagi taklifda: javob kelgach taklif
 * tarixning bir qismiga aylanadi va uni o'zgartirish tarixni
 * soxtalashtirish bo'lardi (server ham buni rad etadi —
 * «taklif.javob_berilgan»).
 */
function OfferCard({
  offer,
  canManage,
  onEdit,
  onAccept,
  onClose,
}: {
  offer: JobOffer
  canManage: boolean
  onEdit: () => void
  onAccept: () => void
  onClose: (action: 'decline' | 'expire') => void
}) {
  const { t, date, money } = useLocale()

  const look = OFFER_LOOK[offer.status]
  const open = offer.status === 'Sent'

  return (
    <Card>
      <CardContent className="space-y-3 py-4">
        <div className="flex flex-wrap items-start gap-3">
          <IconTile icon={look.icon} tone={open ? 'brand' : 'gray'} size="sm" />

          <div className="min-w-0 flex-1">
            <p className="tabular text-title3 font-semibold">
              {money(offer.salary.amount, offer.salary.currency)}
            </p>
            <p className="tabular mt-0.5 flex flex-wrap gap-x-3 gap-y-1 text-caption1 text-[var(--fg-secondary)]">
              <span>{t('recruiting.offerSentAt', { date: date(offer.sentAt) })}</span>
              <span>{t('recruiting.offerStart', { date: date(offer.startDate) })}</span>
              {offer.expiresAt !== null && (
                <span>{t('recruiting.offerExpires', { date: date(offer.expiresAt) })}</span>
              )}
            </p>
          </div>

          <Badge tone={look.tone} size="sm" className="shrink-0">
            {t(`recruiting.offerStatus.${look.key}`)}
          </Badge>
        </div>

        {offer.note !== null && (
          <p className="rounded-md bg-[var(--bg-sunken)] px-3 py-2 text-footnote text-[var(--fg-secondary)]">
            {offer.note}
          </p>
        )}

        {offer.respondedAt !== null && (
          <p className="tabular text-caption1 text-[var(--fg-tertiary)]">
            {t('recruiting.offerRespondedAt', { date: date(offer.respondedAt) })}
            {offer.responseNote !== null && ` · ${offer.responseNote}`}
          </p>
        )}

        {canManage && open && (
          <div className="flex flex-wrap items-center gap-2 border-t border-[var(--border)] pt-3">
            {/*
              «Qabul qilindi» — birinchi va ASOSIY tugma: bu voronkaning
              maqsadi. Qolganlari ikkilamchi.
            */}
            <Button type="button" size="sm" variant="primary" onClick={onAccept}>
              <CircleCheck />
              {t('recruiting.offerAccept')}
            </Button>

            <Button type="button" size="sm" variant="secondary" onClick={() => onClose('decline')}>
              <CircleX />
              {t('recruiting.offerDecline')}
            </Button>

            <Button type="button" size="sm" variant="ghost" onClick={() => onClose('expire')}>
              <Clock />
              {t('recruiting.offerExpire')}
            </Button>

            <Button type="button" size="sm" variant="ghost" onClick={onEdit} className="ml-auto">
              <Pencil />
              {t('common.edit')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * Rad javobi va «muddati o'tdi» — bitta oyna.
 *
 * Ikkalasi ham taklifni YOPADI, nomzodni EMAS: farq faqat sababda va
 * hisobotdagi ma'noda («u rad etdi» / «javob umuman kelmadi»).
 * Tuzilishi bir xil bo'lgani uchun ikkita komponent yozish takror
 * bo'lardi.
 */
function OfferCloseDialog({
  item,
  onClose,
}: {
  item: { offer: JobOffer; action: 'decline' | 'expire' } | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const close = useCloseOffer()

  const [at, setAt] = useState(today)
  const [note, setNote] = useState('')

  useEffect(() => {
    if (item === null) return
    setAt(today())
    setNote('')
    close.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const { offer, action } = item

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!close.isPending}
      title={t(`recruiting.offerClose.${action}Title`)}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={close.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            disabled={close.isPending}
            onClick={() =>
              close.mutate(
                { id: offer.id, action, at: orNull(at), note: orNull(note) },
                { onSuccess: onClose },
              )
            }
          >
            {close.isPending && <LoaderCircle className="animate-spin" />}
            {t(`recruiting.offerClose.${action}Confirm`)}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {close.isError && (
          <Alert tone="danger">{errorMessage(recruitingErrorCode(close.error))}</Alert>
        )}

        <p className="text-subhead text-[var(--fg-secondary)]">
          {t(`recruiting.offerClose.${action}Text`)}
        </p>

        <TextField
          label={t('recruiting.respondedAt')}
          type="date"
          value={at}
          disabled={close.isPending}
          onChange={(event) => setAt(event.target.value)}
        />

        <TextArea
          label={t('recruiting.responseNote')}
          placeholder={t('recruiting.responseNotePlaceholder')}
          value={note}
          disabled={close.isPending}
          onChange={(event) => setNote(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
