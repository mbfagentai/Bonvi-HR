import { Cpu, Settings2, Smartphone, User, UserRoundX, type LucideIcon } from 'lucide-react'

import { AuditAction, AuditActorKind } from '@/shared/api/generated/model'
import { cn } from '@/shared/lib/cn'
import { Avatar, Badge, IconTile, type BadgeTone } from '@/shared/ui'

import type { AuditActor } from '../api/audit-queries'
import { actorView, entryActionLabel, type AuditFormatter } from '../lib/audit-format'

/**
 * Amal yorlig'ining rangi — rang ma'no tashiydi (iOS HIG): yaratish yashil,
 * o'chirish va sessiya o'g'irlanishi shubhasi qizil, muvaffaqiyatsiz kirish
 * va ruxsatsiz urinish to'q sariq («qarab qo'ying»), eksport — binafsha
 * (ma'lumot tizimdan CHIQDI).
 */
const ACTION_TONE: Readonly<Record<AuditAction, BadgeTone>> = {
  [AuditAction.Create]: 'success',
  [AuditAction.Update]: 'info',
  [AuditAction.Delete]: 'danger',
  [AuditAction.Login]: 'neutral',
  [AuditAction.Logout]: 'neutral',
  [AuditAction.LoginFailed]: 'warning',
  [AuditAction.PermissionDenied]: 'warning',
  [AuditAction.TokenReuse]: 'danger',
  [AuditAction.Export]: 'purple',
}

export function AuditActionBadge({
  fmt,
  action,
  operation,
}: {
  fmt: AuditFormatter
  action: AuditAction
  /** Amal nomi — yorliqning yagona istisnosi uchun (`entryActionLabel`). */
  operation: string | null
}) {
  return (
    <Badge tone={ACTION_TONE[action]} size="sm">
      {entryActionLabel(fmt, action, operation)}
    </Badge>
  )
}

/** Odam bo'lmagan bajaruvchining ikonkasi — bosh harfli doira faqat odamga. */
const ACTOR_ICON: Readonly<Record<AuditActorKind, LucideIcon>> = {
  [AuditActorKind.User]: User,
  [AuditActorKind.System]: Settings2,
  [AuditActorKind.Anonymous]: UserRoundX,
  [AuditActorKind.EmployeeAccount]: Smartphone,
  [AuditActorKind.Device]: Cpu,
}

/**
 * Bajaruvchi: bosh harfli doira (yoki ikonka), ism va o'sha lahzadagi rol
 * nomi. Rasm ATAYLAB yuklanmaydi: foydalanuvchi rasmi `users.manage` bilan
 * ochiladi va jurnalni undan torroq rol ham ko'radi — har qatorda 403 bo'lardi.
 */
export function AuditActorCell({
  fmt,
  actor,
  size = 'sm',
  inline = false,
  className,
}: {
  fmt: AuditFormatter
  actor: AuditActor
  size?: 'sm' | 'md'
  /**
   * Jadval qatori: ism va rol BIR QATORDA (egasi 2026-10-22 — jadvalda
   * qator ikkiga bo'linmaydi). Kartalar va oyna avvalgidek ikki qatorli.
   */
  inline?: boolean
  className?: string
}) {
  const view = actorView(fmt, actor)

  const icon = view.person ? (
    <Avatar name={view.name} size={size} />
  ) : (
    <IconTile icon={ACTOR_ICON[actor.kind]} tone="gray" size={size === 'md' ? 'md' : 'sm'} />
  )

  if (inline) {
    return (
      <div className={cn('flex items-center gap-2.5', className)}>
        {icon}
        <span className="text-subhead font-medium">{view.name}</span>
        {view.secondary !== null && (
          <span className="text-caption1 text-[var(--fg-tertiary)]">{view.secondary}</span>
        )}
      </div>
    )
  }

  return (
    <div className={cn('flex min-w-0 items-center gap-2.5', className)}>
      {icon}

      <div className="min-w-0">
        <p className="truncate-1 text-subhead font-medium" title={view.name}>
          {view.name}
        </p>
        {view.secondary !== null && (
          <p className="truncate-1 text-caption1 text-[var(--fg-tertiary)]" title={view.secondary}>
            {view.secondary}
          </p>
        )}
      </div>
    </div>
  )
}
