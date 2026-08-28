import { ArrowDown, ArrowUp, GitBranch, Plus } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  IconTile,
  PageHeader,
  Skeleton,
} from '@/shared/ui'
import type { PipelineStage } from '../api/recruiting-api'
import { useReorderStages, useStages } from '../api/recruiting-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'
import { RECRUITING_MANAGE } from '../lib/recruiting-presentation'
import { PipelineStageDialog } from './pipeline-stage-dialog'

const EMPTY: PipelineStage[] = []

/**
 * ⭐ Voronka bosqichlari — ma'lumotnoma, LEKIN tartibi bilan.
 *
 * <b>Nega bu sakkizta ma'lumotnoma bilan bir sahifada emas.</b>
 * `CatalogPage` alifbo tartibidagi ro'yxatni chizadi va uning butun
 * mantiqi shu taxminga tayanadi. Bosqichlarda esa TARTIB —
 * ma'lumotning O'ZI: «Anketa» «Kengash» dan oldin turishi kerak,
 * alifboda esa teskarisi bo'lardi. Jadval o'rniga tartiblangan
 * ro'yxat, saralash o'rniga strelkalar.
 *
 * <b>Nega sudrab tashlash (DnD) emas.</b> `DutiesEditor` da xuddi shu
 * qaror qabul qilingan va sabab bir xil: bitta yozuvni ikki qator
 * ko'tarish uchun sichqonchani aniq ushlab turish shart bo'lmasin.
 * Qolaversa DnD kutubxonasi loyihada yo'q va uni shu ish uchun
 * qo'shish qimmat.
 *
 * <b>Nega butun tartib bitta so'rovda yuboriladi.</b> Server
 * `POST /reorder` da BARCHA bosqichlar ro'yxatini talab qiladi
 * (yetishsa — «bosqich.tartib_royxati»), ya'ni «bittasini bir qator
 * yuqoriga ko'tar» degan so'rov umuman yo'q. Bu to'g'ri: qisman
 * tartib ikki foydalanuvchi bir vaqtda siljitganda buzilardi.
 *
 * <b>Qidiruv YO'Q.</b> Bosqichlar oltitacha bo'ladi — qidiruv maydoni
 * ro'yxatdan uzunroq joy egallardi. `CatalogPage` da u bor, chunki
 * lavozimlar ikki yuzdan oshadi.
 */
export function PipelineStagesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(RECRUITING_MANAGE)

  // Ro'yxatda HAMMA yozuv: faol emaslari ham. Sabab `CatalogPage` dagi
  // bilan bir xil — «yozuv qayerga ketdi?» degan savol tug'ilmasin.
  const list = useStages(false)
  const reorder = useReorderStages()

  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<PipelineStage | null>(null)

  /**
   * Server javobini kutmasdan ko'rsatiladigan tartib.
   *
   * Muvaffaqiyatli saqlangach `null` ga qaytadi va yana SERVER tartibi
   * hukmron bo'ladi — ikkita haqiqat manbai qolmaydi (`DutiesEditor`
   * dagi bilan aynan bir xil mexanizm).
   *
   * NEGA «ma'lumot yangilanganda tozalash» effekti YO'Q. U tabiiy
   * ko'rinadi, lekin zararli bo'lardi: `providers.tsx` har qanday
   * mutatsiyadan keyin butun keshni bekor qiladi, ya'ni ro'yxat
   * BOSHQA amal sababli ham qayta yuklanishi mumkin — o'sha payt
   * yo'ldagi tartib jimgina bekor qilinib, ro'yxat ortga sakrardi.
   */
  const [localOrder, setLocalOrder] = useState<string[] | null>(null)

  const rows = useMemo(() => {
    /*
      Server ro'yxatni ALLAQACHON `sortOrder` bo'yicha qaytaradi —
      klientda qayta saralanmaydi (`catalog-api.ts` dagi qoida: ikki
      xil tartib bo'lmasligi kerak).
    */
    const source = list.data ?? EMPTY
    if (localOrder === null) return source

    const byId = new Map(source.map((stage) => [stage.id, stage]))
    const ordered = localOrder.flatMap((id) => {
      const stage = byId.get(id)
      return stage === undefined ? [] : [stage]
    })
    // Mahalliy tartib tuzilgandan keyin qo'shilganlar oxiriga tushadi
    const rest = source.filter((stage) => !localOrder.includes(stage.id))

    return [...ordered, ...rest]
  }, [list.data, localOrder])

  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction
    const ids = rows.map((stage) => stage.id)
    const current = ids[index]
    const swapped = ids[target]
    if (current === undefined || swapped === undefined) return

    const next = [...ids]
    next[index] = swapped
    next[target] = current

    setLocalOrder(next)
    reorder.mutate(next, { onSuccess: () => setLocalOrder(null) })
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={GitBranch} tone="purple" size="lg" />
            {t('recruiting.stagesTitle')}
          </span>
        }
        subtitle={t('recruiting.stagesSubtitle')}
        actions={
          canManage ? (
            <Button variant="create" onClick={() => setCreating(true)}>
              <Plus />
              {t('common.add')}
            </Button>
          ) : undefined
        }
      />

      {list.isError && (
        <Alert tone="danger" title={t('catalog.loadFailed')}>
          {errorMessage(recruitingErrorCode(list.error))}
        </Alert>
      )}

      {reorder.isError && (
        <Alert tone="danger">{errorMessage(recruitingErrorCode(reorder.error))}</Alert>
      )}

      <Card className="overflow-hidden">
        {list.isPending ? (
          <div className="space-y-2 p-5">
            {[0, 1, 2, 3].map((index) => (
              <Skeleton key={index} className="h-14 w-full" />
            ))}
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            icon={GitBranch}
            title={t('recruiting.stagesEmptyPageTitle')}
            description={t('recruiting.stagesEmptyPageText')}
            action={
              canManage ? (
                <Button variant="create" onClick={() => setCreating(true)}>
                  <Plus />
                  {t('common.add')}
                </Button>
              ) : undefined
            }
          />
        ) : (
          <ol className="divide-y divide-[var(--border)]">
            {rows.map((stage, index) => (
              <li
                key={stage.id}
                className={cn(
                  'flex items-center gap-3 px-4 py-3 sm:px-5',
                  canManage && 'hover:bg-[var(--bg-hover)]',
                )}
              >
                {/*
                  Raqam — voronkadagi O'RIN, ma'lumotnoma kodi EMAS.
                  Ma'lumotnomalarda kod va tartib raqami ataylab olib
                  tashlangan (loyiha konvensiyasi), lekin bu yerda
                  raqam ma'no tashiydi: u bosqichlar ketma-ketligini
                  ko'rsatadi va aynan shu narsa tahrirlanadi.
                */}
                <span className="tabular w-6 shrink-0 text-caption1 text-[var(--fg-tertiary)]">
                  {num(index + 1)}.
                </span>

                <button
                  type="button"
                  disabled={!canManage}
                  onClick={() => setEditing(stage)}
                  className="min-w-0 flex-1 text-left disabled:cursor-default"
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <span
                      className={cn(
                        'truncate-1 font-medium',
                        // Faol emas yozuv o'chirilmagan — shunchaki so'ndirilgan
                        !stage.isActive && 'text-[var(--fg-secondary)]',
                      )}
                    >
                      {stage.name}
                    </span>

                    {!stage.isActive && (
                      <Badge tone="neutral" size="sm" className="shrink-0">
                        {t('catalog.statusInactive')}
                      </Badge>
                    )}
                  </span>

                  {stage.note !== null && (
                    <span className="truncate-1 mt-0.5 block text-caption1 text-[var(--fg-tertiary)]">
                      {stage.note}
                    </span>
                  )}
                </button>

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
                      disabled={index === rows.length - 1 || reorder.isPending}
                      onClick={() => move(index, 1)}
                      title={t('catalog.positions.moveDown')}
                      aria-label={t('catalog.positions.moveDown')}
                    >
                      <ArrowDown />
                    </Button>
                  </span>
                )}
              </li>
            ))}
          </ol>
        )}

        {!list.isPending && rows.length > 0 && (
          <p className="border-t border-[var(--border)] px-5 py-3 text-caption1 text-[var(--fg-tertiary)]">
            {t('catalog.total', { n: num(rows.length) })}
          </p>
        )}
      </Card>

      <PipelineStageDialog
        open={creating || editing !== null}
        item={editing}
        onClose={() => {
          setCreating(false)
          setEditing(null)
        }}
      />
    </div>
  )
}
