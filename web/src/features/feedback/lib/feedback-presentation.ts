import { CircleHelp, ThumbsDown, ThumbsUp, type LucideIcon } from 'lucide-react'

import type { BadgeTone, IconTone } from '@/shared/ui'
import type { RecommendationAnswerValue } from '../api/exit-interviews-api'

/* ═════════════════════════════════════════════════════════════ RUXSATLAR */

/**
 * Ruxsat kodlari — backend'dagi `Bonvi.Domain.Identity.Permissions` bilan
 * AYNAN bir xil (`employee-presentation.ts` dagi xuddi shu naqsh).
 *
 * ALOHIDA guruh, `employment.*` emas: ishdan ketuvchining o'z so'zi bilan
 * aytgani nozik ma'lumot, HR ning boshqa qismidan mustaqil ravishda
 * cheklanadi (backend izohiga qarang: `Permissions.FeedbackView`).
 */
export const FEEDBACK_VIEW = 'feedback.view'
export const FEEDBACK_MANAGE = 'feedback.manage'

/* ═══════════════════════════════════════════════════════ TAVSIYA JAVOBI */

export interface RecommendationLook {
  tone: BadgeTone
  iconTone: IconTone
  icon: LucideIcon
  /** `feedback.recommend<Key>` tarjima kaliti. */
  labelKey: string
}

/** «Kompaniyani tavsiya qilarmidi» javobining ko'rinishi. */
export const RECOMMENDATION_LOOK: Readonly<Record<RecommendationAnswerValue, RecommendationLook>> = {
  yes: {
    tone: 'success',
    iconTone: 'green',
    icon: ThumbsUp,
    labelKey: 'feedback.recommendYes',
  },
  no: {
    tone: 'danger',
    iconTone: 'red',
    icon: ThumbsDown,
    labelKey: 'feedback.recommendNo',
  },
  dontKnow: {
    tone: 'neutral',
    iconTone: 'gray',
    icon: CircleHelp,
    labelKey: 'feedback.recommendDontKnow',
  },
}
