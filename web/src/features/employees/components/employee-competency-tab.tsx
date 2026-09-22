import {
  ClipboardList,
  GraduationCap,
  Heart,
  Laptop,
  Languages,
  Pencil,
  Sparkles,
  Star,
  type LucideIcon,
} from 'lucide-react'
import { useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Badge, Button, Card, CardContent, CardHeader, Skeleton } from '@/shared/ui'

import { useEmployeeCompetency } from '../api/employees-queries'
import { EMPLOYEES_EDIT } from '../lib/employee-presentation'
import { EmployeeCompetencyDialog } from './employee-competency-dialog'

/**
 * «Qobiliyatlar» tabi (91.15) — ko'rish + tahrirlash formasi, RO'YXAT EMAS.
 *
 * Bitta xodimda bitta profil bo'ladi (tarix yo'q), shu sababli bu yerda
 * `EmployeeTimelineTab`dagi kabi lenta emas, balki `EmployeeOverviewTab`
 * uslubidagi statik karta: ko'rsatiladi va alohida oyna orqali tahrirlanadi.
 *
 * <b>Maydonlar deyarli har doim to'ldirilmagan.</b> Excel'dan kam ma'lumot
 * ko'chgan («компьютер саводхонлиги» ustuni deyarli hech qachon to'ldirilmagan
 * edi). Shu sababli bo'sh qiymatlar boshqa tablardagi `InfoRow` bilan bir xil
 * xotirjam «—» bilan ko'rsatiladi — ogohlantirish yoki xato emas, oddiy holat.
 */
export function EmployeeCompetencyTab({ employeeId }: { employeeId: string }) {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const canEdit = hasPermission(EMPLOYEES_EDIT)

  const [editing, setEditing] = useState(false)
  const query = useEmployeeCompetency(employeeId)

  if (query.isPending) {
    return (
      <Card>
        <CardContent className="space-y-3 pt-5">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </CardContent>
      </Card>
    )
  }

  if (query.isError) {
    return (
      <Card>
        <CardContent className="pt-5">
          <Alert tone="danger">{t('employees.loadFailed')}</Alert>
        </CardContent>
      </Card>
    )
  }

  // `undefined` ham `null` bilan bir xilda o'qiladi — ikkalasi ham
  // «profil hali yaratilmagan» degani, forma bo'sh boshlanadi.
  const profile = query.data ?? null

  const percent =
    profile !== null && profile.computerLiteracyPercent !== null
      ? `${profile.computerLiteracyPercent}%`
      : null

  /*
   * Tillar uch holatda uch xil chiziladi:
   *  1) strukturaviy ro'yxat bor — har biri badge («til · daraja»);
   *  2) ro'yxat bo'sh, lekin Excel'dan ko'chgan ESKI erkin matn bor —
   *     matn «eski yozuv» belgisi bilan (yangi ro'yxat matn o'rniga
   *     kelganini bildirish uchun — matn tahrirlanmaydi, faqat hujjat);
   *  3) ikkalasi ham yo'q — boshqa maydonlar kabi xotirjam «—».
   */
  const skills = profile?.languageSkills ?? []
  const legacyText = profile?.languages ?? null
  const languagesValue =
    skills.length > 0 ? (
      <div className="flex flex-wrap gap-1.5 pt-1">
        {skills.map((skill) => (
          <Badge key={skill.language} tone="neutral">
            {t(`competency.language.${skill.language}`)}
            <span className="text-[var(--fg-tertiary)]">·</span>
            {t(`competency.languageLevel.${skill.level}`)}
          </Badge>
        ))}
      </div>
    ) : legacyText !== null ? (
      <>
        {legacyText}{' '}
        <span className="text-caption2 text-[var(--fg-tertiary)]">
          ({t('competency.legacyLanguages')})
        </span>
      </>
    ) : null

  return (
    <>
      <Card>
        <CardHeader
          title={t('employees.tabCompetency')}
          subtitle={t('competency.hint')}
          action={
            canEdit && (
              <Button type="button" size="sm" variant="secondary" onClick={() => setEditing(true)}>
                <Pencil />
                {t('common.edit')}
              </Button>
            )
          }
        />
        <CardContent>
          <dl className="grid gap-x-8 gap-y-0 sm:grid-cols-2">
            <InfoRow
              icon={Languages}
              label={t('competency.languages')}
              value={languagesValue}
              className="sm:col-span-2"
            />
            <InfoRow icon={Laptop} label={t('competency.computerLiteracy')} value={percent} />
            <InfoRow
              icon={GraduationCap}
              label={t('competency.educationPlace')}
              value={profile?.educationPlace ?? null}
            />
            <InfoRow
              icon={Sparkles}
              label={t('competency.specialization')}
              value={profile?.specialization ?? null}
            />
            <InfoRow
              icon={Heart}
              label={t('competency.interest')}
              value={profile?.interest ?? null}
              className="sm:col-span-2"
            />
            <InfoRow
              icon={Star}
              label={t('competency.otherAbilities')}
              value={profile?.otherAbilities ?? null}
              className="sm:col-span-2"
            />
            {/*
              Nomzodlik anketasidan ko'chgan qo'shimcha ma'lumot — ko'p
              qatorli matn, shu sababli oxirida va butun kenglikda.
              Bo'lmasa qator umuman chizilmaydi (`InfoRow` ning o'z
              qoidasi).
            */}
            <InfoRow
              icon={ClipboardList}
              label={t('competency.candidateNote')}
              value={profile?.candidateNote ?? null}
              className="sm:col-span-2"
            />
          </dl>
        </CardContent>
      </Card>

      {canEdit && (
        <EmployeeCompetencyDialog
          employeeId={employeeId}
          profile={profile}
          open={editing}
          onClose={() => setEditing(false)}
        />
      )}
    </>
  )
}

/**
 * Yorliq + qiymat qatori — `EmployeeOverviewTab`dagi `InfoRow` bilan
 * bir xil ko'rinish (u yerda eksport qilinmagani uchun bu yerda alohida).
 */
function InfoRow({
  icon: Icon,
  label,
  value,
  className,
}: {
  icon: LucideIcon
  label: string
  value: ReactNode
  className?: string
}) {
  const empty = value === null || value === undefined || value === ''

  return (
    <div
      className={cn(
        'flex items-start gap-3 border-b border-[var(--border)] py-3 last:border-0',
        className,
      )}
    >
      <Icon className="mt-0.5 size-4 shrink-0 text-[var(--fg-tertiary)]" strokeWidth={2} />

      <div className="min-w-0 flex-1">
        <dt className="text-caption1 text-[var(--fg-secondary)]">{label}</dt>
        <dd
          className={cn(
            'mt-0.5 text-subhead break-words',
            empty ? 'text-[var(--fg-tertiary)]' : 'text-[var(--fg)]',
          )}
        >
          {empty ? '—' : value}
        </dd>
      </div>
    </div>
  )
}
