import {
  CircleCheck,
  CircleX,
  FileSpreadsheet,
  History,
  LoaderCircle,
  Search,
  Upload,
} from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type DragEvent } from 'react'

import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { formatPinfl } from '@/features/employees/lib/employee-presentation'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  Dialog,
  Pagination,
  SegmentedControl,
  Select,
  type SegmentOption,
} from '@/shared/ui'

import {
  ATTENDANCE_IDENTITY_PROVIDERS,
  IDENTITY_MATCH_KINDS,
  type AttendanceIdentityProvider,
  type IdentityMatchKind,
  type ImportCandidate,
  type ImportPreview,
  type ImportPreviewRow,
  type ImportResult,
} from '../api/attendance-api'
import { useConfirmIdentityImport, usePreviewIdentityImport } from '../api/attendance-queries'
import { identityProviderLabel } from '../lib/attendance-manage-presentation'
import {
  MATCH_LOOK,
  MAX_IMPORT_FILE_BYTES,
  MAX_IMPORT_ROWS,
  sortImportRows,
  type ImportDecision,
} from '../lib/attendance-presentation'
import { AttendanceEmployeePicker, type PickedEmployee } from './attendance-employee-picker'

/** `attendance_identities.display_name_in_device` — varchar(200). */
const DISPLAY_NAME_MAX = 200

/** Qabul qilinadigan fayllar — server ham faqat shularni o'qiydi (`davomat.import_fayl_turi`). */
const ACCEPT = '.xlsx,.csv'

/**
 * Oldindan ko'rishning bir sahifasi.
 *
 * NEGA sahifalanadi: 5000 qatorning har biri o'z tanlov boshqaruvi bilan
 * bir vaqtda chizilsa, oyna har bosishda sekundlab «qotadi». 25 ta —
 * operator bir qarashda ko'radigan va bir nafasda hal qiladigan miqdor.
 */
const ROWS_PER_PAGE = 25

type Step = 'pick' | 'preview' | 'result'
type RowFilter = 'attention' | 'all'

interface RowDecision {
  decision: ImportDecision
  /** Kimga bog'lanadi. `confirm` da — taklif, `choose` da — operator tanlovi. */
  employee: PickedEmployee | null
  /** Nomzodlar ro'yxatidan tashqari xodimni qidirish ochiqmi. */
  searching: boolean
}

/**
 * Verifix ro'yxatini import qilish: fayl → oldindan ko'rish → qarorlar →
 * tasdiqlash → natija.
 *
 * ⭐ Server oldindan ko'rishni SAQLAMAYDI (SPEC §4.4): qarorlarni shu oyna
 * ushlaydi va faqat tasdiqlanganini qaytaradi. Shu sababli oldindan
 * ko'rish bosqichida oyna fon bosilganda yoki Escape bilan YOPILMAYDI —
 * 300 qatorlik qarorlar bir tasodifiy bosishda yo'qolardi.
 *
 * ⭐ Standart qarorlar `MATCH_LOOK` dan (SPEC §8.4): aniq moslik —
 * tasdiqlashga tayyor, noaniq va topilmagan — QAROR TALAB QILADI va
 * tepada, allaqachon bog'langan — o'tkazib yuboriladi. «Tasdiqlash»
 * tugmasi qaror kutayotgan qator qolmaguncha yoqilmaydi: noaniq qatorni
 * ko'rmasdan o'tib ketish noto'g'ri odamni turniketga bog'lashning eng
 * oson yo'li. Ko'p qatorni birdan o'tkazib yuborish uchun alohida tugma bor
 * — bu ONGLI qaror, tasodif emas.
 */
export function AttendanceImportDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  const previewMutation = usePreviewIdentityImport()
  const confirmMutation = useConfirmIdentityImport()

  const inputRef = useRef<HTMLInputElement>(null)

  const [step, setStep] = useState<Step>('pick')
  const [provider, setProvider] = useState<AttendanceIdentityProvider>('Verifix')
  const [file, setFile] = useState<File | null>(null)
  const [dragOver, setDragOver] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const [preview, setPreview] = useState<ImportPreview | null>(null)
  const [decisions, setDecisions] = useState<Record<number, RowDecision>>({})
  const [filter, setFilter] = useState<RowFilter>('attention')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<ImportResult | null>(null)

  useEffect(() => {
    if (!open) return
    setStep('pick')
    setProvider('Verifix')
    setFile(null)
    setDragOver(false)
    setFailure(null)
    setPreview(null)
    setDecisions({})
    setFilter('attention')
    setPage(1)
    setResult(null)
    previewMutation.reset()
    confirmMutation.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  /** Tartib bir marta — qaror kutadiganlar tepada, fayl tartibi saqlangan. */
  const sortedRows = useMemo(
    () => (preview === null ? [] : sortImportRows(preview.rows)),
    [preview],
  )

  /*
    «Qaror kerak» filtri BOSHLANG'ICH natijaga qaraydi, joriy qarorga emas.
    Aks holda operator qatorni hal qilishi bilan u ro'yxatdan sakrab
    chiqib, qolgan qatorlar kursor ostida surilib ketardi.
  */
  const attentionRows = useMemo(
    () => sortedRows.filter((row) => initialDecision(row) === 'choose'),
    [sortedRows],
  )

  const kindCounts = useMemo(() => {
    const counts: Record<IdentityMatchKind, number> = {
      ExactPinfl: 0,
      ExactName: 0,
      Ambiguous: 0,
      None: 0,
      AlreadyMapped: 0,
    }
    for (const row of sortedRows) counts[row.matchKind] += 1
    return counts
  }, [sortedRows])

  const tally = useMemo(() => {
    let send = 0
    let skip = 0
    let unresolved = 0
    for (const row of sortedRows) {
      const state = decisions[row.rowNumber]
      if (state === undefined || state.decision === 'skip') skip += 1
      else if (state.employee === null) unresolved += 1
      else send += 1
    }
    return { send, skip, unresolved }
  }, [sortedRows, decisions])

  const visibleRows = filter === 'attention' ? attentionRows : sortedRows
  const pageCount = Math.max(1, Math.ceil(visibleRows.length / ROWS_PER_PAGE))
  const currentPage = Math.min(page, pageCount)
  const pageRows = visibleRows.slice(
    (currentPage - 1) * ROWS_PER_PAGE,
    currentPage * ROWS_PER_PAGE,
  )

  /* ─────────────────────────────────────────────────────── fayl tanlash */

  /**
   * Tur va hajm — serverga bormasdan (`candidate-files-card.tsx` naqshi).
   * Kodlar serverniki, ya'ni tarjima bitta va xabar ikki joyda bir xil.
   */
  const choose = (chosen: File | undefined) => {
    setFailure(null)
    if (chosen === undefined) return

    const name = chosen.name.toLowerCase()
    if (!name.endsWith('.xlsx') && !name.endsWith('.csv')) {
      setFile(null)
      setFailure('davomat.import_fayl_turi')
      return
    }
    if (chosen.size > MAX_IMPORT_FILE_BYTES) {
      setFile(null)
      setFailure('davomat.import_fayl_katta')
      return
    }
    setFile(chosen)
  }

  const onDrop = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault()
    setDragOver(false)
    if (previewMutation.isPending) return
    choose(event.dataTransfer.files[0])
  }

  const startPreview = () => {
    if (file === null) return
    setFailure(null)

    previewMutation.mutate(
      { file, provider },
      {
        onSuccess: (data) => {
          setPreview(data)
          setDecisions(initialDecisions(data.rows))
          // Qaror kerak bo'lgan qator bo'lmasa — birdan to'liq ro'yxat
          setFilter(
            data.rows.some((row) => initialDecision(row) === 'choose')
              ? 'attention'
              : 'all',
          )
          setPage(1)
          setStep('preview')
        },
        onError: (error) => setFailure(problemCode(error)),
      },
    )
  }

  /* ─────────────────────────────────────────────────────── qarorlar */

  const updateRow = (rowNumber: number, patch: Partial<RowDecision>) =>
    setDecisions((previous) => {
      const current = previous[rowNumber]
      if (current === undefined) return previous
      return { ...previous, [rowNumber]: { ...current, ...patch } }
    })

  /** Qator qarori almashtirilganda tanlangan odam ham qarorga mos qayta o'rnatiladi. */
  const changeDecision = (row: ImportPreviewRow, decision: ImportDecision) =>
    updateRow(row.rowNumber, {
      decision,
      employee: decision === 'confirm' ? suggestedEmployee(row) : null,
      searching: false,
    })

  const skipUnresolved = () =>
    setDecisions((previous) => {
      const next = { ...previous }
      for (const [key, state] of Object.entries(previous)) {
        if (state.decision === 'choose' && state.employee === null) {
          next[Number(key)] = { ...state, decision: 'skip', searching: false }
        }
      }
      return next
    })

  const submit = () => {
    if (preview === null || tally.send === 0 || tally.unresolved > 0) return
    setFailure(null)

    const rows = sortedRows.flatMap((row) => {
      const state = decisions[row.rowNumber]
      if (state === undefined || state.decision === 'skip' || state.employee === null) return []
      return [
        {
          rawPersonId: row.rawPersonId,
          employeeId: state.employee.id,
          // Ustun 200 belgi (`display_name_in_device`) — fayldagi uzun satr tasdiqni yiqitmasin
          displayNameInDevice: row.fullNameInFile?.slice(0, DISPLAY_NAME_MAX) ?? null,
        },
      ]
    })

    confirmMutation.mutate(
      // Oldindan ko'rish qaysi makonda moslangan bo'lsa — AYNAN o'sha (§6.3)
      { provider: preview.provider, rows },
      {
        onSuccess: (data) => {
          setResult(data)
          setStep('result')
        },
        onError: (error) => setFailure(problemCode(error)),
      },
    )
  }

  /* ─────────────────────────────────────────────────────── chizish */

  const pending = previewMutation.isPending || confirmMutation.isPending

  const providerOptions = ATTENDANCE_IDENTITY_PROVIDERS.map((value) => ({
    value,
    label: identityProviderLabel(value, t),
  }))

  const filterOptions: Array<SegmentOption<RowFilter>> = [
    { value: 'attention', label: t('attendance.importFilterAttention', { n: num(attentionRows.length) }) },
    { value: 'all', label: t('attendance.importFilterAll', { n: num(sortedRows.length) }) },
  ]

  const footer =
    step === 'pick' ? (
      <>
        <Button type="button" onClick={onClose} disabled={pending}>
          {t('common.cancel')}
        </Button>
        <Button
          type="button"
          variant="primary"
          onClick={startPreview}
          disabled={file === null || pending}
        >
          {previewMutation.isPending && <LoaderCircle className="animate-spin" />}
          {t('attendance.importPreview')}
        </Button>
      </>
    ) : step === 'preview' ? (
      <>
        <Button
          type="button"
          onClick={() => {
            setStep('pick')
            setFailure(null)
          }}
          disabled={pending}
          className="mr-auto"
        >
          {t('attendance.importAnotherFile')}
        </Button>
        <Button type="button" onClick={onClose} disabled={pending}>
          {t('common.cancel')}
        </Button>
        <Button
          type="button"
          variant="primary"
          onClick={submit}
          disabled={pending || tally.send === 0 || tally.unresolved > 0}
        >
          {confirmMutation.isPending && <LoaderCircle className="animate-spin" />}
          {t('attendance.importConfirm', { n: num(tally.send) })}
        </Button>
      </>
    ) : (
      <Button type="button" variant="primary" onClick={onClose}>
        {t('common.close')}
      </Button>
    )

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      dismissible={step === 'result' || (step === 'pick' && !pending)}
      title={t('attendance.importTitle')}
      description={
        step === 'preview' && preview !== null
          ? t('attendance.importRowsRead', { n: num(preview.totalRows) })
          : t('attendance.importSubtitle')
      }
      footer={footer}
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}

        {step === 'pick' && (
          <>
            <Select
              label={t('attendance.identityProvider')}
              hint={t('attendance.importProviderHint')}
              options={providerOptions}
              value={provider}
              disabled={pending}
              onChange={(event) => setProvider(event.target.value as AttendanceIdentityProvider)}
            />

            {/*
              Tashlash maydoni — `<label>`: bosilganda yashirin fayl
              maydonini o'zi ochadi, klaviaturadan ham ishlaydi. Brauzerning
              standart `<input type="file">` qobig'i tizimning qolgan
              boshqaruvlariga o'xshamaydi (`candidate-files-card.tsx` izohi).
            */}
            <label
              onDragOver={(event) => {
                event.preventDefault()
                if (!pending) setDragOver(true)
              }}
              onDragLeave={() => setDragOver(false)}
              onDrop={onDrop}
              className={cn(
                'press flex cursor-pointer flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed px-6 py-10 text-center',
                'transition-colors duration-200 ease-[var(--ease-ios)]',
                dragOver
                  ? 'border-[var(--accent)] bg-[var(--accent-soft)]'
                  : 'border-[var(--border-strong)] bg-[var(--bg-sunken)] hover:bg-[var(--bg-hover)]',
                pending && 'pointer-events-none opacity-60',
              )}
            >
              <input
                ref={inputRef}
                type="file"
                accept={ACCEPT}
                className="sr-only"
                disabled={pending}
                onChange={(event) => {
                  choose(event.target.files?.[0])
                  // Bir xil faylni qayta tanlash ham `onChange` bersin
                  event.target.value = ''
                }}
              />
              <span className="grid size-14 place-items-center rounded-full bg-[var(--bg-elevated)] shadow-card">
                {file === null ? (
                  <Upload className="size-6 text-[var(--fg-secondary)]" />
                ) : (
                  <FileSpreadsheet className="size-6 text-ios-green" />
                )}
              </span>
              {file === null ? (
                <span className="text-subhead font-medium">{t('attendance.importDrop')}</span>
              ) : (
                <span className="min-w-0 space-y-1">
                  <span className="truncate-1 block text-subhead font-medium">{file.name}</span>
                  <span className="tabular block text-caption1 text-[var(--fg-secondary)]">
                    {t('attendance.importFileSize', { size: fileSizeLabel(file.size, num) })}
                  </span>
                </span>
              )}
              <span className="text-footnote text-[var(--fg-tertiary)]">
                {t('attendance.importLimitsHint', {
                  rows: num(MAX_IMPORT_ROWS),
                  mb: num(MAX_IMPORT_FILE_BYTES / (1024 * 1024)),
                })}
              </span>
            </label>
          </>
        )}

        {step === 'preview' && preview !== null && (
          <>
            {/* Natija turlari bo'yicha sanoq — bir qarashda «ish qancha?» */}
            <div className="flex flex-wrap gap-2">
              {IDENTITY_MATCH_KINDS.map((kind) => {
                const look = MATCH_LOOK[kind]
                const Icon = look.icon
                return (
                  <Badge key={kind} tone={look.tone}>
                    <Icon className="size-3.5" strokeWidth={2.25} />
                    {t(look.labelKey)}
                    <span className="tabular">{num(kindCounts[kind])}</span>
                  </Badge>
                )
              })}
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-[var(--bg-sunken)] px-4 py-3">
              <p className="tabular text-footnote text-[var(--fg-secondary)]">
                {t('attendance.importTally', {
                  send: num(tally.send),
                  skip: num(tally.skip),
                  unresolved: num(tally.unresolved),
                })}
              </p>
              {tally.unresolved > 0 && (
                <Button type="button" size="sm" variant="ghost" onClick={skipUnresolved}>
                  {t('attendance.importSkipUnresolved', { n: num(tally.unresolved) })}
                </Button>
              )}
            </div>

            {tally.unresolved > 0 && (
              <Alert tone="warning">
                {t('attendance.importUnresolvedHint', { n: num(tally.unresolved) })}
              </Alert>
            )}

            {preview.rows.length === 0 ? (
              <p className="py-10 text-center text-subhead text-[var(--fg-secondary)]">
                {t('attendance.importEmptyFile')}
              </p>
            ) : (
              <>
                <SegmentedControl
                  options={filterOptions}
                  value={filter}
                  onChange={(next) => {
                    setFilter(next)
                    setPage(1)
                  }}
                  size="sm"
                  aria-label={t('common.filters')}
                />

                {visibleRows.length === 0 ? (
                  <div className="flex flex-col items-center gap-2 py-10 text-center">
                    <CircleCheck className="size-7 text-ios-green" strokeWidth={1.8} />
                    <p className="text-subhead text-[var(--fg-secondary)]">
                      {t('attendance.importNothingToDecide')}
                    </p>
                  </div>
                ) : (
                  <ul className="space-y-3">
                    {pageRows.map((row) => {
                      const state = decisions[row.rowNumber]
                      if (state === undefined) return null
                      return (
                        <ImportRowCard
                          key={row.rowNumber}
                          row={row}
                          state={state}
                          disabled={pending}
                          onDecision={(decision) => changeDecision(row, decision)}
                          onEmployee={(employee) =>
                            /*
                              Qidiruvdagi tanlov bekor qilinsa (`null`), qidiruv
                              OCHIQ qoladi: operator boshqasini izlayapti,
                              nomzodlar ro'yxatiga qaytarib tashlash uni chalg'itardi.
                            */
                            updateRow(row.rowNumber, { employee, searching: employee === null })
                          }
                          onSearching={(searching) => updateRow(row.rowNumber, { searching })}
                        />
                      )
                    })}
                  </ul>
                )}

                {visibleRows.length > ROWS_PER_PAGE && (
                  <Pagination
                    page={currentPage}
                    pageSize={ROWS_PER_PAGE}
                    total={visibleRows.length}
                    onPageChange={setPage}
                    disabled={pending}
                    className="px-0 sm:px-0"
                  />
                )}
              </>
            )}
          </>
        )}

        {step === 'result' && result !== null && <ImportResultView result={result} />}
      </div>
    </Dialog>
  )
}

/* ═══════════════════════════════════════════════════════════ qator */

function ImportRowCard({
  row,
  state,
  disabled,
  onDecision,
  onEmployee,
  onSearching,
}: {
  row: ImportPreviewRow
  state: RowDecision
  disabled: boolean
  onDecision: (decision: ImportDecision) => void
  onEmployee: (employee: PickedEmployee | null) => void
  onSearching: (searching: boolean) => void
}) {
  const { t, num } = useLocale()
  const look = MATCH_LOOK[row.matchKind]
  const Icon = look.icon
  const hasSuggestion = row.suggestedEmployeeId !== null && row.matchKind !== 'AlreadyMapped'

  /*
    Taklifi bor qatorda uch yo'l: tasdiqlash, boshqasini tanlash, o'tkazib
    yuborish (SPEC §8.4). Taklifsiz qatorda «tasdiqlash» ma'nosiz — nima
    tasdiqlanadi? — shu sababli ikki yo'l: tanlash yoki o'tkazib yuborish.
  */
  const options: Array<SegmentOption<ImportDecision>> = hasSuggestion
    ? [
        { value: 'confirm', label: t('attendance.importDecisionConfirm') },
        { value: 'choose', label: t('attendance.chooseAnother') },
        { value: 'skip', label: t('attendance.skipRow') },
      ]
    : [
        { value: 'choose', label: t('attendance.importDecisionChoose') },
        { value: 'skip', label: t('attendance.skipRow') },
      ]

  const unresolved = state.decision === 'choose' && state.employee === null
  const chosenIsCandidate =
    state.employee !== null &&
    row.candidates.some((candidate) => candidate.employeeId === state.employee?.id)

  return (
    <li
      className={cn(
        'rounded-xl border bg-[var(--bg-elevated)] p-4',
        unresolved ? 'border-ios-orange/60' : 'border-[var(--border)]',
      )}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="tabular text-caption1 text-[var(--fg-tertiary)]">
              {t('attendance.importRowNumber', { n: num(row.rowNumber) })}
            </span>
            <Badge size="sm" className="font-mono">
              {row.rawPersonId}
            </Badge>
          </div>
          <p className="truncate-1 font-medium">
            {row.fullNameInFile ?? t('attendance.importNoNameInFile')}
          </p>
          {row.pinflInFile !== null && (
            <p className="tabular text-caption1 text-[var(--fg-secondary)]">
              {t('attendance.pinflInFile', { pinfl: formatPinfl(row.pinflInFile) ?? row.pinflInFile })}
            </p>
          )}
        </div>

        <Badge tone={look.tone} size="sm">
          <Icon className="size-3" strokeWidth={2.5} />
          {t(look.labelKey)}
        </Badge>
      </div>

      {row.matchKind === 'AlreadyMapped' ? (
        <p className="mt-3 text-footnote text-[var(--fg-secondary)]">
          {t('attendance.importAlreadyMappedTo', {
            name: row.suggestedEmployeeName ?? '—',
          })}
        </p>
      ) : (
        <div className="mt-3 space-y-3">
          <SegmentedControl
            options={options}
            value={state.decision}
            onChange={onDecision}
            size="sm"
            className="w-full sm:w-auto"
            aria-label={t('attendance.importDecision')}
          />

          {state.decision === 'confirm' && state.employee !== null && (
            <EmployeeLine
              employee={state.employee}
              pinfl={candidateOf(row, state.employee.id)?.pinfl ?? null}
            />
          )}

          {state.decision === 'choose' && (
            <>
              {row.candidates.length > 0 && (
                <CandidateList
                  candidates={row.candidates}
                  selectedId={state.employee?.id ?? null}
                  disabled={disabled}
                  onSelect={(candidate) => onEmployee(candidateToPicked(candidate))}
                />
              )}

              {row.candidates.length === 0 ||
              state.searching ||
              (state.employee !== null && !chosenIsCandidate) ? (
                <AttendanceEmployeePicker
                  value={chosenIsCandidate ? null : state.employee}
                  onChange={onEmployee}
                  disabled={disabled}
                  label={
                    row.candidates.length > 0
                      ? t('attendance.searchOtherEmployee')
                      : t('attendance.employee')
                  }
                />
              ) : (
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  disabled={disabled}
                  onClick={() => onSearching(true)}
                >
                  <Search />
                  {t('attendance.searchOtherEmployee')}
                </Button>
              )}
            </>
          )}

          {state.decision === 'skip' && (
            <p className="text-footnote text-[var(--fg-tertiary)]">
              {t('attendance.importWillSkip')}
            </p>
          )}
        </div>
      )}
    </li>
  )
}

/**
 * Taklif qilingan xodim — tasdiqlashdan oldin YUZIGA ko'z yugurtirish uchun.
 *
 * Bo'lim va lavozim faqat server odamni nomzodlar ro'yxatida bergan
 * bo'lsa chiqadi (aniq moslik qatorida ular shartnomada yo'q, §4.4);
 * u holda o'rnida JSHSHIR turadi — JSHSHIR bo'yicha moslikda u yuzdan ham
 * ishonchliroq dalil.
 */
function EmployeeLine({ employee, pinfl }: { employee: PickedEmployee; pinfl: string | null }) {
  const { t } = useLocale()

  return (
    <div className="flex items-center gap-3 rounded-lg bg-[var(--bg-sunken)] px-3 py-2.5">
      <EmployeePhoto
        employeeId={employee.id}
        photoKey={employee.photoKey}
        name={employee.fullName}
        size="md"
      />
      <div className="min-w-0 flex-1">
        <p className="truncate-1 text-subhead font-medium">{employee.fullName}</p>
        <p className="truncate-1 text-caption1 text-[var(--fg-secondary)]">
          {[employee.position, employee.department].filter(Boolean).join(' · ') ||
            (pinfl === null ? '—' : t('attendance.pinflInFile', { pinfl: formatPinfl(pinfl) ?? pinfl }))}
        </p>
      </div>
    </div>
  )
}

/** Noaniq moslikdagi 2…5 nomzod — har biri YUZI bilan (egasining talabi). */
function CandidateList({
  candidates,
  selectedId,
  disabled,
  onSelect,
}: {
  candidates: ImportCandidate[]
  selectedId: string | null
  disabled: boolean
  onSelect: (candidate: ImportCandidate) => void
}) {
  const { t } = useLocale()

  return (
    <div className="grid gap-2 sm:grid-cols-2" role="radiogroup" aria-label={t('attendance.importCandidates')}>
      {candidates.map((candidate) => {
        const selected = candidate.employeeId === selectedId
        return (
          <button
            key={candidate.employeeId}
            type="button"
            role="radio"
            aria-checked={selected}
            disabled={disabled}
            onClick={() => onSelect(candidate)}
            className={cn(
              'press flex items-center gap-3 rounded-lg border px-3 py-2.5 text-left',
              'disabled:pointer-events-none disabled:opacity-50',
              selected
                ? 'border-[var(--accent)] bg-[var(--accent-soft)]'
                : 'border-[var(--border)] hover:bg-[var(--bg-hover)]',
            )}
          >
            <EmployeePhoto
              employeeId={candidate.employeeId}
              photoKey={candidate.photoKey}
              name={candidate.fullName}
              size="md"
            />
            <span className="min-w-0 flex-1">
              <span className="truncate-1 block text-subhead font-medium">
                {candidate.fullName}
              </span>
              <span className="truncate-1 block text-caption1 text-[var(--fg-secondary)]">
                {[candidate.position, candidate.department].filter(Boolean).join(' · ') || '—'}
              </span>
              {candidate.pinfl !== null && (
                <span className="tabular block text-caption2 text-[var(--fg-tertiary)]">
                  {formatPinfl(candidate.pinfl) ?? candidate.pinfl}
                </span>
              )}
            </span>
            {selected && <CircleCheck className="size-5 shrink-0 text-[var(--accent-text)]" />}
          </button>
        )
      })}
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════ natija */

function ImportResultView({ result }: { result: ImportResult }) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <ResultTile tone="success" label={t('attendance.importCreated')} value={num(result.created)} />
        <ResultTile tone="neutral" label={t('attendance.importSkipped')} value={num(result.skipped)} />
        <ResultTile
          tone={result.failed.length > 0 ? 'danger' : 'neutral'}
          label={t('attendance.importFailed')}
          value={num(result.failed.length)}
        />
      </div>

      <Alert tone="success" title={t('attendance.importDone')}>
        {t('attendance.importRetroactiveHint')}
      </Alert>

      {result.failed.length > 0 && (
        <div className="rounded-xl border border-[var(--border)]">
          <p className="border-b border-[var(--border)] px-4 py-3 text-footnote font-semibold">
            {t('attendance.importFailedTitle')}
          </p>
          <ul className="max-h-64 divide-y divide-[var(--border)] overflow-y-auto">
            {result.failed.map((failure, index) => (
              <li
                // Bir raqam ikki marta rad etilishi mumkin (faylda takror) — indeks ham kalitda
                key={`${failure.rawPersonId}-${index}`}
                className="flex items-start gap-3 px-4 py-2.5"
              >
                <CircleX className="mt-0.5 size-4 shrink-0 text-ios-red" />
                <span className="min-w-0">
                  <span className="block font-mono text-footnote font-medium">
                    {failure.rawPersonId}
                  </span>
                  <span className="block text-caption1 text-[var(--fg-secondary)]">
                    {errorMessage(failure.code)}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="flex items-start gap-2 text-footnote text-[var(--fg-tertiary)]">
        <History className="mt-0.5 size-4 shrink-0" />
        {t('attendance.importAfterHint')}
      </p>
    </div>
  )
}

function ResultTile({
  tone,
  label,
  value,
}: {
  tone: 'success' | 'neutral' | 'danger'
  label: string
  value: string
}) {
  return (
    <div className="rounded-xl bg-[var(--bg-sunken)] px-4 py-3">
      <p className="text-footnote text-[var(--fg-secondary)]">{label}</p>
      <p
        className={cn(
          'tabular mt-1 text-title2 leading-tight font-semibold',
          tone === 'success' && 'text-ios-green',
          tone === 'danger' && 'text-ios-red',
        )}
      >
        {value}
      </p>
    </div>
  )
}

/* ═══════════════════════════════════════════════════════════ yordamchilar */

function candidateOf(row: ImportPreviewRow, employeeId: string): ImportCandidate | undefined {
  return row.candidates.find((candidate) => candidate.employeeId === employeeId)
}

function candidateToPicked(candidate: ImportCandidate): PickedEmployee {
  return {
    id: candidate.employeeId,
    fullName: candidate.fullName,
    photoKey: candidate.photoKey,
    position: candidate.position,
    department: candidate.department,
  }
}

/**
 * Serverning taklifi — tanlangan xodim shaklida.
 *
 * Aniq moslikda (`ExactPinfl` / `ExactName`) server taklif qilingan odamni
 * `candidates` ga HAM qo'yadi — rasmi, bo'limi va lavozimi bilan. Shuning
 * uchun operator tasdiqlashdan oldin YUZNI ko'radi.
 */
function suggestedEmployee(row: ImportPreviewRow): PickedEmployee | null {
  if (row.suggestedEmployeeId === null) return null

  const candidate = candidateOf(row, row.suggestedEmployeeId)
  if (candidate !== undefined) return candidateToPicked(candidate)

  /*
    Shartnomadan chetga chiqqan javob (taklif bor, nomzodlarda yo'q) — rasm
    kaliti ham noma'lum, bosh harflar chiziladi. Rasmni «so'rab ko'rish»
    endi yo'q: kalit DTO da bor va uning yo'qligi «rasm yo'q» degani.
  */
  return {
    id: row.suggestedEmployeeId,
    fullName: row.suggestedEmployeeName ?? '',
    photoKey: null,
    position: null,
    department: null,
  }
}

/**
 * Bitta qatorning boshlang'ich qarori — `MATCH_LOOK` bo'yicha (SPEC §8.4).
 *
 * «Tasdiqlashga tayyor» natija taklifsiz kelsa (shartnomadan chetga
 * chiqqan javob), qator TANLOVGA tushadi: tasdiqlanadigan odam yo'q, uni
 * jimgina o'tkazib yuborish esa operatordan qarorni yashirardi.
 */
function initialDecision(row: ImportPreviewRow): ImportDecision {
  const decision = MATCH_LOOK[row.matchKind].decision
  return decision === 'confirm' && row.suggestedEmployeeId === null ? 'choose' : decision
}

/** Boshlang'ich qarorlar — butun fayl uchun. */
function initialDecisions(rows: readonly ImportPreviewRow[]): Record<number, RowDecision> {
  const result: Record<number, RowDecision> = {}

  for (const row of rows) {
    const decision = initialDecision(row)
    result[row.rowNumber] = {
      decision,
      employee: decision === 'confirm' ? suggestedEmployee(row) : null,
      searching: false,
    }
  }

  return result
}

/** «1,2 MB» / «340 KB» — foydalanuvchi faylini tanib olishi uchun, aniq hisob uchun emas. */
function fileSizeLabel(bytes: number, num: (value: number) => string): string {
  const megabytes = bytes / (1024 * 1024)
  if (megabytes >= 1) return `${num(Math.round(megabytes * 10) / 10)} MB`
  return `${num(Math.max(1, Math.round(bytes / 1024)))} KB`
}
