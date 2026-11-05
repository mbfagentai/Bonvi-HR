import { Check, Cog, Hourglass, Lock, Minus } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

import type { ProcessCaseStep } from '../api/processes-api'
import { isStepFinished, stepProgress, type ProcessColumnSteps } from '../lib/process-step'
import {
  isHolderUnassigned,
  ProcessCountChip,
  ProcessHolderLabel,
  useProcessRoleLabel,
} from './process-chips'

/** A step the system closes by itself has no person to name. */
function isSystemStep(step: ProcessCaseStep): boolean {
  return step.holder.role === null
}

const MARKER_CLASS = 'grid size-[26px] shrink-0 place-items-center rounded-full'
const RING_CLASS = 'border-2 bg-[var(--bg-elevated)]'

/**
 * The stepper's dot, in the prototype's vocabulary: a green check for done, a gear for a system step, a blue ring with a
 * dot for the selected step, a hollow ring for what is still ahead. A state a person must act on keeps its own colour even
 * when selected, because that is the one thing the row has to say. The state's name is there for a screen reader.
 */
function StepMarker({ step, selected }: { step: ProcessCaseStep; selected: boolean }) {
  const { t } = useLocale()
  const system = isSystemStep(step)
  const label = t(`processes.state.${step.state}`)

  if (step.state === 'Done') {
    const Icon = system ? Cog : Check
    return (
      <span
        role="img"
        aria-label={label}
        className={cn(MARKER_CLASS, 'bg-ios-green text-[var(--fg-on-accent)]')}
      >
        <Icon aria-hidden className={system ? 'size-3.5' : 'size-4'} strokeWidth={system ? 2 : 3} />
      </span>
    )
  }

  if (step.state === 'Skipped') {
    return (
      <span
        role="img"
        aria-label={label}
        className={cn(MARKER_CLASS, 'bg-[var(--fg-tertiary)] text-[var(--fg-on-accent)]')}
      >
        <Minus aria-hidden className="size-4" strokeWidth={3} />
      </span>
    )
  }

  if (step.state === 'Blocked') {
    return (
      <span
        role="img"
        aria-label={label}
        className={cn(MARKER_CLASS, RING_CLASS, 'border-ios-red text-ios-red')}
      >
        <Lock aria-hidden className="size-3.5" strokeWidth={2.5} />
      </span>
    )
  }

  if (selected) {
    return (
      <span
        role="img"
        aria-label={label}
        className={cn(MARKER_CLASS, RING_CLASS, 'border-[var(--accent)]')}
      >
        <span className="size-2.5 rounded-full bg-[var(--accent)]" />
      </span>
    )
  }

  if (step.state === 'Waiting') {
    return (
      <span
        role="img"
        aria-label={label}
        className={cn(MARKER_CLASS, RING_CLASS, 'border-ios-orange text-ios-orange')}
      >
        <Hourglass aria-hidden className="size-3" strokeWidth={2.5} />
      </span>
    )
  }

  return (
    <span
      role="img"
      aria-label={label}
      className={cn(
        MARKER_CLASS,
        RING_CLASS,
        'text-[var(--fg-tertiary)]',
        step.state === 'Active' ? 'border-[var(--accent)]' : 'border-[var(--border-strong)]',
      )}
    >
      {system && <Cog aria-hidden className="size-3.5" strokeWidth={2.5} />}
    </span>
  )
}

function StepRow({
  step,
  selected,
  last,
  onSelect,
}: {
  step: ProcessCaseStep
  selected: boolean
  last: boolean
  onSelect: (stepCode: string) => void
}) {
  const { t, num } = useLocale()
  const system = isSystemStep(step)
  const finished = isStepFinished(step.state)
  const roleLabel = useProcessRoleLabel()

  return (
    <li className="relative -mx-2">
      <button
        type="button"
        aria-current={selected ? 'step' : undefined}
        onClick={() => onSelect(step.stepCode)}
        className={cn(
          'relative flex w-full items-start gap-3 rounded-lg border-l-[3px] py-2 pr-2 pl-1.5 text-left',
          'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
          selected
            ? 'border-l-[var(--accent)] bg-[var(--accent-soft)]'
            : 'border-l-transparent hover:bg-[var(--bg-hover)]',
        )}
      >
        <StepMarker step={step} selected={selected} />
        <span className="min-w-0 flex-1 pt-px">
          <span
            className={cn(
              'block text-subhead leading-6',
              selected && 'font-semibold',
              system && !selected && 'text-[var(--fg-secondary)]',
            )}
          >
            {step.title}
          </span>
          {system ? (
            <span className="mt-0.5 block text-caption1 leading-5 text-[var(--fg-secondary)] italic">
              {t('processes.case.systemHolder')}
            </span>
          ) : !finished && isHolderUnassigned(step.holder) ? (
            // The one thing about a holder the row must say: the person's name is the panel's business.
            <ProcessHolderLabel
              holder={step.holder}
              className="mt-0.5 block text-caption1 leading-5 break-words text-[var(--fg-secondary)]"
            />
          ) : (
            <span className="mt-0.5 block text-caption1 leading-5 break-words text-[var(--fg-secondary)]">
              {roleLabel(step.holder.role)}
            </span>
          )}
          {!finished && step.daysLate > 0 && (
            <span className="block text-caption1 leading-5 font-medium text-ios-red">
              {t('processes.due.late', { n: num(step.daysLate) })}
            </span>
          )}
        </span>
      </button>
      {!last && (
        // From dot to dot, centred under the 26 px dot: 3 px accent edge + 6 px padding + 13 px half a dot − 1 px half the line.
        <span
          aria-hidden
          className={cn(
            'pointer-events-none absolute top-[34px] -bottom-2 left-[21px] w-0.5',
            finished ? 'bg-ios-green/45' : 'bg-[var(--border-strong)]',
          )}
        />
      )}
    </li>
  )
}

/**
 * Steps by board column (SPEC §11.5), as the prototype's stepper, numbered on through however many cases the columns come
 * from. The rows choose what the panel and the guide beside them show.
 */
export function ProcessStepTimeline({
  columns,
  selectedStepCode,
  onSelect,
}: {
  columns: readonly ProcessColumnSteps[]
  selectedStepCode: string | undefined
  onSelect: (stepCode: string) => void
}) {
  const { t, num } = useLocale()

  return (
    <nav aria-label={t('processes.case.stepsTitle')} className="space-y-5">
      {columns.map((column, index) => {
        const { done, total } = stepProgress(column.steps)

        return (
          <section key={column.key}>
            <h3 className="mb-1.5 flex items-center justify-between gap-2 text-footnote font-semibold tracking-wide uppercase">
              <span className="min-w-0 break-words">
                {num(index + 1)}. {column.title}
              </span>
              <ProcessCountChip
                label={t('processes.case.columnProgress', { done: num(done), total: num(total) })}
              >
                {num(done)}/{num(total)}
              </ProcessCountChip>
            </h3>
            <ol>
              {column.steps.map((step, stepIndex) => (
                <StepRow
                  key={step.stepCode}
                  step={step}
                  selected={step.stepCode === selectedStepCode}
                  last={stepIndex === column.steps.length - 1}
                  onSelect={onSelect}
                />
              ))}
            </ol>
          </section>
        )
      })}
    </nav>
  )
}
