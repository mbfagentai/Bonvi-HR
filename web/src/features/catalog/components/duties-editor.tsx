import { ArrowDown, ArrowUp, ListChecks, LoaderCircle, Plus, X } from 'lucide-react'
import { useEffect, useMemo, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Badge, Button, Input, Skeleton } from '@/shared/ui'
import { useDuties, useDutyMutations } from '../api/catalog-queries'
import type { PositionDuty } from '../api/catalog-api'

const EMPTY: PositionDuty[] = []

/**
 * Lavozim vazifalari muharriri.
 *
 * Tartib muhim: ishga qabul qilishda vazifalar shu ketma-ketlikda
 * shartnomaga tushadi. Shu sababli yuqoriga/pastga tugmalari bor —
 * sudrab tashlash (DnD) emas: bitta vazifani ikki qator ko'tarish uchun
 * sichqonchani aniq ushlab turish shart bo'lmasin.
 */
export function DutiesEditor({
  positionId,
  canManage,
}: {
  /** `null` — lavozim hali saqlanmagan, vazifa biriktirib bo'lmaydi. */
  positionId: string | null
  canManage: boolean
}) {
  const { t } = useLocale()

  const [text, setText] = useState('')
  /**
   * Server javobini kutmasdan ko'rsatiladigan tartib.
   * Muvaffaqiyatli saqlangach `null` ga qaytadi va yana server tartibi
   * hukmron bo'ladi — ikkita haqiqat manbai qolmaydi.
   */
  const [localOrder, setLocalOrder] = useState<string[] | null>(null)

  const duties = useDuties(positionId)
  const { add, remove, reorder } = useDutyMutations(positionId)

  useEffect(() => {
    setLocalOrder(null)
    setText('')
  }, [positionId])

  const list = useMemo(() => {
    const source = [...(duties.data ?? EMPTY)].sort((a, b) => a.sortOrder - b.sortOrder)
    if (localOrder === null) return source

    const byId = new Map(source.map((duty) => [duty.id, duty]))
    const ordered = localOrder.flatMap((id) => {
      const duty = byId.get(id)
      return duty === undefined ? [] : [duty]
    })
    // Mahalliy tartib tuzilgandan keyin qo'shilganlar oxiriga tushadi
    const rest = source.filter((duty) => !localOrder.includes(duty.id))

    return [...ordered, ...rest]
  }, [duties.data, localOrder])

  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction
    const ids = list.map((duty) => duty.id)
    const current = ids[index]
    const swapped = ids[target]
    if (current === undefined || swapped === undefined) return

    const next = [...ids]
    next[index] = swapped
    next[target] = current

    setLocalOrder(next)
    reorder.mutate(next, { onSuccess: () => setLocalOrder(null) })
  }

  const submitDuty = () => {
    const value = text.trim()
    if (value === '') return
    add.mutate(value, { onSuccess: () => setText('') })
  }

  const onKeyDown = (event: ReactKeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter') return
    // Bu maydon lavozim formasi ICHIDA turadi — Enter butun formani
    // yubormasligi kerak, faqat vazifa qo'shsin
    event.preventDefault()
    submitDuty()
  }

  return (
    <section className="space-y-3 rounded-lg border border-[var(--border)] bg-[var(--bg-sunken)] p-4">
      <header className="flex items-center gap-2.5">
        <ListChecks className="size-[18px] text-[var(--fg-secondary)]" strokeWidth={2} />
        <h3 className="text-subhead font-semibold">{t('catalog.positions.duties')}</h3>
        {list.length > 0 && <Badge size="sm">{list.length}</Badge>}
      </header>

      {positionId === null ? (
        <Alert tone="info">{t('catalog.positions.dutiesAfterSave')}</Alert>
      ) : (
        <>
          {duties.isPending && (
            <div className="space-y-2">
              {[0, 1, 2].map((index) => (
                <Skeleton key={index} className="h-11 w-full" />
              ))}
            </div>
          )}

          {!duties.isPending && list.length === 0 && (
            <p className="py-3 text-center text-footnote text-[var(--fg-secondary)]">
              {t('catalog.positions.dutiesEmpty')}
            </p>
          )}

          {!duties.isPending && list.length > 0 && (
            <ol className="space-y-1.5">
              {list.map((duty, index) => (
                <li
                  key={duty.id}
                  className={cn(
                    'flex items-center gap-2.5 rounded-md border border-[var(--border)]',
                    'bg-[var(--bg-elevated)] py-2 pr-1.5 pl-3',
                  )}
                >
                  <span className="tabular w-5 shrink-0 text-caption1 text-[var(--fg-tertiary)]">
                    {index + 1}.
                  </span>

                  <span className="min-w-0 flex-1 text-subhead">{duty.text}</span>

                  {canManage && (
                    <span className="flex shrink-0 items-center">
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-sm"
                        disabled={index === 0 || reorder.isPending}
                        onClick={() => move(index, -1)}
                        title={t('catalog.positions.moveUp')}
                        aria-label={t('catalog.positions.moveUp')}
                      >
                        <ArrowUp />
                      </Button>

                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-sm"
                        disabled={index === list.length - 1 || reorder.isPending}
                        onClick={() => move(index, 1)}
                        title={t('catalog.positions.moveDown')}
                        aria-label={t('catalog.positions.moveDown')}
                      >
                        <ArrowDown />
                      </Button>

                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-sm"
                        disabled={remove.isPending}
                        onClick={() => remove.mutate(duty.id)}
                        title={t('catalog.positions.removeDuty')}
                        aria-label={t('catalog.positions.removeDuty')}
                        className="text-ios-red hover:bg-ios-red/10 hover:text-ios-red"
                      >
                        <X />
                      </Button>
                    </span>
                  )}
                </li>
              ))}
            </ol>
          )}

          {canManage && (
            <div className="flex items-center gap-2">
              <Input
                value={text}
                onChange={(event) => setText(event.target.value)}
                onKeyDown={onKeyDown}
                placeholder={t('catalog.positions.dutyPlaceholder')}
                aria-label={t('catalog.positions.addDuty')}
                disabled={add.isPending}
              />

              <Button
                type="button"
                variant="create"
                onClick={submitDuty}
                disabled={add.isPending || text.trim() === ''}
                className="shrink-0"
              >
                {add.isPending ? <LoaderCircle className="animate-spin" /> : <Plus />}
                <span className="hidden sm:inline">{t('catalog.positions.addDuty')}</span>
              </Button>
            </div>
          )}
        </>
      )}
    </section>
  )
}
