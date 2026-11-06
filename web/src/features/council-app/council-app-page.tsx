import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronLeft, ChevronRight, FileText, Users } from 'lucide-react'
import { Suspense, lazy, useEffect, useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { CandidateQuestionnaireCard } from '@/features/recruiting/components/candidate-questionnaire-card'
import { COUNCIL_DECISIONS, type CouncilDecision } from '@/features/recruiting/api/council-api'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Badge, Button, Card, CardContent, EmptyState, Select, Skeleton, TextArea } from '@/shared/ui'
import { CouncilAppError, councilAppClient, type CouncilAppCandidate, type CouncilAppClient } from './council-app-api'
import { loadTelegram, targetCandidate } from './telegram'

const PdfView = lazy(() => import('@/shared/ui/pdf-view'))

const DECISION_TONE = { Accept: 'success', Conditional: 'warning', Reserve: 'neutral', Reject: 'danger' } as const

/** The council's Mini App: outside `_app`, identified by Telegram's signed launch data, never by the panel's login. */
export function CouncilAppPage() {
  const { t } = useLocale()
  const { i18n } = useTranslation()
  const [launch, setLaunch] = useState<{ initData: string; target: string | null; openLink: (url: string) => void } | null | 'none'>(null)

  useEffect(() => {
    void loadTelegram().then((app) => {
      // The bot's council texts are in Cyrillic. Only inside Telegram: elsewhere this browser may be a panel user's.
      if (app !== null && app.initData !== '') void i18n.changeLanguage('uz-Cyrl')

      setLaunch(
        app !== null && app.initData !== ''
          ? { initData: app.initData, target: targetCandidate(app), openLink: (url) => app.openTelegramLink(url) }
          : 'none',
      )
    })
  }, [i18n])

  return (
    <div className="mx-auto min-h-dvh max-w-xl space-y-4 bg-[var(--bg)] px-4 py-4">
      {launch === null && <Skeleton className="h-40 w-full rounded-2xl" />}
      {launch === 'none' && <Alert tone="warning">{t('council.app.openInTelegram')}</Alert>}
      {launch !== null && launch !== 'none' && <Launched {...launch} />}
    </div>
  )
}

function Launched({ initData, target, openLink }: { initData: string; target: string | null; openLink: (url: string) => void }) {
  const { t } = useLocale()
  const client = useMemo(() => councilAppClient(initData), [initData])
  const [open, setOpen] = useState(target)
  const me = useQuery({ queryKey: ['council-app', 'me'], queryFn: client.me, retry: false })

  if (me.isPending) {
    return <Skeleton className="h-40 w-full rounded-2xl" />
  }

  if (me.isError) {
    return <Alert tone="danger">{t(me.error instanceof CouncilAppError && me.error.status === 401 ? 'council.app.reopen' : 'council.app.loadError')}</Alert>
  }

  if (!me.data.recognized) {
    return (
      <Card>
        <CardContent className="space-y-3 p-5 text-center">
          <p className="text-[var(--fg)]">{t('council.app.linkHint')}</p>
          {me.data.botUsername !== null && (
            <Button type="button" onClick={() => openLink(`https://t.me/${me.data.botUsername}?start=kengash`)}>
              {t('council.app.linkButton')}
            </Button>
          )}
        </CardContent>
      </Card>
    )
  }

  return open === null ? <CandidateList client={client} onOpen={setOpen} /> : <CandidateView client={client} id={open} onBack={() => setOpen(null)} />
}

function CandidateList({ client, onOpen }: { client: CouncilAppClient; onOpen: (id: string) => void }) {
  const { t, dateTime } = useLocale()
  const list = useQuery({ queryKey: ['council-app', 'list'], queryFn: client.list })

  if (list.isPending) {
    return <Skeleton className="h-64 w-full rounded-2xl" />
  }

  if (list.isError) {
    return <Alert tone="danger">{t('council.app.loadError')}</Alert>
  }

  if (list.data.length === 0) {
    return <EmptyState icon={Users} title={t('council.app.empty')} />
  }

  return (
    <div className="space-y-2">
      <h1 className="text-xl font-semibold text-[var(--fg)]">{t('council.app.title')}</h1>
      {list.data.map((row) => (
        <button
          key={row.id}
          type="button"
          onClick={() => onOpen(row.id)}
          className="flex w-full items-center gap-3 rounded-2xl border border-[var(--border)] bg-[var(--bg-elevated)] p-3 text-left"
        >
          <span className="min-w-0 flex-1">
            <span className="block truncate font-medium text-[var(--fg)]">{row.fullName}</span>
            <span className="block truncate text-sm text-[var(--fg-secondary)]">
              {[row.position, dateTime(row.interviewAt)].filter(Boolean).join(' · ')}
            </span>
          </span>
          {row.hasMyOpinion && <Check className="size-5 shrink-0 text-ios-green" aria-label={t('council.app.given')} />}
          <ChevronRight className="size-5 shrink-0 text-[var(--fg-secondary)]" />
        </button>
      ))}
    </div>
  )
}

function CandidateView({ client, id, onBack }: { client: CouncilAppClient; id: string; onBack: () => void }) {
  const { t, date, dateTime } = useLocale()
  const candidate = useQuery({ queryKey: ['council-app', 'candidate', id], queryFn: () => client.candidate(id), retry: false })
  const photo = usePhoto(client, candidate.data)

  const back = (
    <Button type="button" variant="secondary" size="sm" onClick={onBack}>
      <ChevronLeft />
      {t('council.app.back')}
    </Button>
  )

  if (candidate.isPending) {
    return <Skeleton className="h-64 w-full rounded-2xl" />
  }

  if (candidate.isError) {
    return (
      <div className="space-y-3">
        {back}
        <Alert tone="danger">
          {t(candidate.error instanceof CouncilAppError && candidate.error.status === 404 ? 'council.app.notInvited' : 'council.app.loadError')}
        </Alert>
      </div>
    )
  }

  const { data } = candidate
  const lines: ReadonlyArray<readonly [string, string | null]> = [
    [t('council.app.birthDate'), data.birthDate !== null ? date(data.birthDate) : null],
    [t('council.app.phone'), data.phone],
    [t('council.app.address'), data.address],
    [t('council.app.speciality'), data.speciality],
  ]

  return (
    <div className="space-y-4">
      {back}

      <Card>
        <CardContent className="flex gap-4 p-4">
          {photo !== null && <img src={photo} alt="" className="size-20 shrink-0 rounded-2xl object-cover" />}
          <div className="min-w-0">
            <h1 className="text-lg font-semibold text-[var(--fg)]">{data.fullName}</h1>
            <p className="text-sm text-[var(--fg-secondary)]">{data.position}</p>
            <p className="text-sm text-[var(--fg-secondary)]">{t('council.app.invitedFor', { at: dateTime(data.interviewAt) })}</p>
          </div>
        </CardContent>
        <CardContent className="space-y-2 border-t border-[var(--border)] p-4 text-sm">
          {lines
            .filter(([, value]) => value !== null && value !== '')
            .map(([label, value]) => (
              <div key={label} className="flex justify-between gap-4">
                <span className="text-[var(--fg-secondary)]">{label}</span>
                <span className="text-right text-[var(--fg)]">{value}</span>
              </div>
            ))}
        </CardContent>
      </Card>

      <CandidateQuestionnaireCard questionnaire={data.questionnaire} source={data.source} />

      {data.forms.map((form) => (
        <FormPdf key={form.id} client={client} candidateId={data.id} formId={form.id} number={form.number} />
      ))}

      <OpinionForm client={client} candidate={data} />
    </div>
  )
}

/** The photo needs the launch header, so it is fetched and shown from a blob. */
function usePhoto(client: CouncilAppClient, candidate: CouncilAppCandidate | undefined) {
  const [url, setUrl] = useState<string | null>(null)
  const id = candidate?.hasPhoto === true ? candidate.id : null

  useEffect(() => {
    if (id === null) return undefined

    let live = true
    let created: string | null = null

    void client.photo(id).then(
      (blob) => {
        if (!live) return
        created = URL.createObjectURL(blob)
        setUrl(created)
      },
      () => undefined,
    )

    return () => {
      live = false
      if (created !== null) URL.revokeObjectURL(created)
    }
  }, [client, id])

  return url
}

function FormPdf({ client, candidateId, formId, number }: { client: CouncilAppClient; candidateId: string; formId: string; number: number }) {
  const { t } = useLocale()
  const [shown, setShown] = useState(false)
  const pdf = useQuery({
    queryKey: ['council-app', 'pdf', formId],
    queryFn: () => client.pdf(candidateId, formId),
    enabled: shown,
    retry: false,
  })

  if (!shown) {
    return (
      <Button type="button" variant="secondary" className="w-full" onClick={() => setShown(true)}>
        <FileText />
        {t('council.app.openForm', { number })}
      </Button>
    )
  }

  if (pdf.isError) {
    return <Alert tone="danger">{t('council.app.loadError')}</Alert>
  }

  return pdf.data === undefined ? (
    <Skeleton className="h-64 w-full rounded-2xl" />
  ) : (
    <div className="h-[75dvh] overflow-hidden rounded-2xl border border-[var(--border)]">
      <Suspense fallback={<Skeleton className="h-full w-full" />}>
        <PdfView blob={pdf.data} name={`${number}.pdf`} />
      </Suspense>
    </div>
  )
}

function OpinionForm({ client, candidate }: { client: CouncilAppClient; candidate: CouncilAppCandidate }) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const queries = useQueryClient()
  const [decision, setDecision] = useState<CouncilDecision | ''>('')
  const [comment, setComment] = useState('')
  const [missing, setMissing] = useState(false)

  const save = useMutation({
    mutationFn: (body: { decision: CouncilDecision; comment: string | null }) => client.saveOpinion(candidate.id, body),
    onSuccess: async () => {
      setDecision('')
      setComment('')
      await Promise.all([
        queries.invalidateQueries({ queryKey: ['council-app', 'list'] }),
        queries.invalidateQueries({ queryKey: ['council-app', 'candidate', candidate.id] }),
      ])
    },
  })

  const needsComment = decision === 'Conditional' && comment.trim() === ''

  const submit = (event: FormEvent) => {
    event.preventDefault()

    if (decision === '' || needsComment) {
      setMissing(true)
      return
    }

    setMissing(false)
    save.mutate({ decision, comment: comment.trim() === '' ? null : comment.trim() })
  }

  return (
    <Card>
      <CardContent className="p-4">
        <form className="space-y-3" onSubmit={submit}>
          <h2 className="font-semibold text-[var(--fg)]">{t(candidate.myOpinions.length > 0 ? 'council.app.again' : 'council.app.yours')}</h2>

          <Select
            label={t('council.decisionLabel')}
            value={decision}
            placeholder={t('council.choose')}
            error={missing && decision === '' ? t('council.decisionRequired') : undefined}
            options={COUNCIL_DECISIONS.map((value) => ({ value, label: t(`council.decision.${value}`) }))}
            onChange={(event) => setDecision(event.target.value as CouncilDecision | '')}
          />
          <TextArea
            label={t('council.app.comment')}
            rows={3}
            value={comment}
            maxLength={4000}
            error={missing && needsComment ? t('council.app.commentRequired') : undefined}
            onChange={(event) => setComment(event.target.value)}
          />

          {save.isError && (
            <Alert tone="danger">{errorMessage(save.error instanceof CouncilAppError ? save.error.code : null)}</Alert>
          )}
          {save.isSuccess && <Alert tone="success">{t('council.app.saved')}</Alert>}

          <Button type="submit" className="w-full" disabled={save.isPending}>
            {t('council.app.send')}
          </Button>
        </form>

        {candidate.myOpinions.length > 0 && (
          <div className="mt-4 space-y-2 border-t border-[var(--border)] pt-4">
            <h3 className="text-sm font-medium text-[var(--fg-secondary)]">{t('council.app.earlier')}</h3>
            {candidate.myOpinions.map((opinion) => (
              <div key={opinion.at} className="rounded-xl bg-[var(--bg)] p-3 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <Badge tone={DECISION_TONE[opinion.decision]}>{t(`council.decision.${opinion.decision}`)}</Badge>
                  <span className="text-xs text-[var(--fg-secondary)]">{dateTime(opinion.at)}</span>
                </div>
                {opinion.comment !== null && <p className="mt-2 whitespace-pre-line text-[var(--fg)]">{opinion.comment}</p>}
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
