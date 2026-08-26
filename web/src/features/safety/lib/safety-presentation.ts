import { Info, OctagonAlert, Skull, TriangleAlert, type LucideIcon } from 'lucide-react'

import type { BadgeTone, IconTone } from '@/shared/ui'
import type { IncidentSeverityValue } from '../api/incidents-api'

/* ═════════════════════════════════════════════════════════════ RUXSATLAR */

/**
 * Ruxsat kodlari — backend'dagi `Bonvi.Domain.Identity.Permissions` bilan
 * AYNAN bir xil (`employee-presentation.ts` dagi xuddi shu naqsh).
 *
 * ALOHIDA guruh, `employees.*` yoki `lists.*` emas: baxtsiz hodisa
 * hujjati intizomiy/tibbiy nozik ma'lumot, HR ning boshqa qismidan
 * mustaqil ravishda cheklanadi (backend izohiga qarang: `Permissions.SafetyView`).
 */
export const SAFETY_VIEW = 'safety.view'
export const SAFETY_MANAGE = 'safety.manage'

/* ══════════════════════════════════════════════════════════ JABRLANISH */

export interface SeverityLook {
  tone: BadgeTone
  iconTone: IconTone
  icon: LucideIcon
  /** `safety.severity<Key>` tarjima kaliti. */
  labelKey: string
}

/**
 * Jabrlanish darajasining ko'rinishi — `FLAG_LOOK` bilan bir xil naqsh
 * (`employee-presentation.ts`).
 *
 * To'rtta daraja ikkita rangga tushadi (og'ir/qattiq — ikkalasi ham
 * qizil): `BadgeTone`da xavfni to'rt bosqichga ajratadigan alohida rang
 * yo'q. Shu sababli ikonalar farqlaydi — «Og'ir»da sakkiz qirrali
 * ogohlantirish, «Halokatli»da bosh suyagi, ikkalasi ham bir xil qizil
 * fonda, lekin bir qarashda ajraladi.
 */
export const SEVERITY_LOOK: Readonly<Record<IncidentSeverityValue, SeverityLook>> = {
  minor: {
    tone: 'info',
    iconTone: 'blue',
    icon: Info,
    labelKey: 'safety.severityMinor',
  },
  moderate: {
    tone: 'warning',
    iconTone: 'orange',
    icon: TriangleAlert,
    labelKey: 'safety.severityModerate',
  },
  severe: {
    tone: 'danger',
    iconTone: 'red',
    icon: OctagonAlert,
    labelKey: 'safety.severitySevere',
  },
  fatal: {
    tone: 'danger',
    iconTone: 'red',
    icon: Skull,
    labelKey: 'safety.severityFatal',
  },
}
