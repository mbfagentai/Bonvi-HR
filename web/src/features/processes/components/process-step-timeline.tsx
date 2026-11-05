import {
  CheckCircle2,
  Circle,
  CircleDot,
  Hourglass,
  Lock,
  MinusCircle,
  type LucideIcon,
} from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'

import type { ProcessCaseStep, ProcessColumn, ProcessStepState } from '../api/processes-api'
import { groupStepsByColumn } from '../lib/process-step'
import { ProcessDueChip, ProcessHolderLabel } from './process-chips'

const STATE_ICON: Record<ProcessStepState, { icon: LucideIcon; className: string }> = {
  Done: { icon: CheckCircle2, className: 'text-ios-green' },
  Skipped: { icon: MinusCircle, className: 'text-[var(--fg-tertiary)]' },
  Active: { icon: CircleDot, className: 'text-[var(--accent)]' },
  Blocked: { icon: Lock, className: 'text-ios-red' },
  Waiting: { icon: Hourglass, className: 'text-ios-orange' },
  Locked: { icon: Circle, className: 'text-[var(--fg-tertiary)]' },
}

/** The icon of a step's state, with the state's name for a screen reader. */
export function ProcessStepStateIcon({ state }: { state: ProcessStepState }) {
  const { t } = useLocale()
  const { icon: Icon, className } = STATE_ICON[state]

  return (
    <Icon
      role="img"
      aria-label={t(`processes.state.${state}`)}
      className={cn('size-5 shrink-0', className)}
    />
  )
}

/** A case's steps by board column (SPEC §11.5). The rows choose what the step panel beside them shows. */
export function ProcessStepTimeline({
  columns,
  steps,
  selectedStepCode,
  onSelect,
}: {
  columns: readonly ProcessColumn[]
  steps: readonly ProcessCaseStep[]
  selectedStepCode: string | undefined
  onSelect: (stepCode: string) => void
}) {
  const today = todayIso()

  return (
    <div className="space-y-5">
      {groupStepsByColumn(columns, steps).map((column) => (
        <section key={column.key} className="space-y-1">
          <h3 className="px-2.5 text-footnote font-semibold text-[var(--fg-secondary)]">
            {column.title}
          </h3>
          <ul className="space-y-0.5">
            {column.steps.map((step) => {
              const selected = step.stepCode === selectedStepCode
              const finished = step.state === 'Done' || step.state === 'Skipped'

              return (
                <li key={step.stepCode}>
                  <button
                    type="button"
                    aria-current={selected ? 'step' : undefined}
                    onClick={() => onSelect(step.stepCode)}
                    className={cn(
                      'flex w-full items-start gap-2.5 rounded-md px-2.5 py-2 text-left transition-colors',
                      selected ? 'bg-[var(--accent-soft)]' : 'hover:bg-[var(--bg-hover)]',
                    )}
                  >
                    <ProcessStepStateIcon state={step.state} />
                    <span className="min-w-0 flex-1 space-y-1">
                      <span
                        className={cn(
                          'block text-subhead',
                          finished ? 'text-[var(--fg-secondary)]' : 'font-medium',
                        )}
                      >
                        {step.title}
                      </span>
                      <ProcessHolderLabel
                        holder={step.holder}
                        className="block text-footnote text-[var(--fg-secondary)]"
                      />
                    </span>
                    {!finished && (
                      <ProcessDueChip
                        dueDate={step.dueDate}
                        daysLate={step.daysLate}
                        today={today}
                      />
                    )}
                  </button>
                </li>
              )
            })}
          </ul>
        </section>
      ))}
    </div>
  )
}
