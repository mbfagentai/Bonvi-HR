import {
  Award,
  Building2,
  CalendarCheck,
  Compass,
  FileText,
  Gavel,
  IdCard,
  LogOut,
  Megaphone,
  Network,
  type LucideIcon,
} from 'lucide-react'

import type { IconTone } from '@/shared/ui'
import type { CatalogKind, SimpleCatalogKind } from '../api/catalog-api'

export interface CatalogKindMeta {
  /** URL segmenti va API yo'li: `/api/catalog/{kind}`. */
  kind: CatalogKind
  /** Tarjima kaliti: `catalog.kinds.<i18nKey>.title` va `.subtitle`. */
  i18nKey: string
  icon: LucideIcon
  tone: IconTone
}

/**
 * Ma'lumotnoma turlari.
 *
 * Rang bezak emas, belgi: yashil — rag'bat, qizil — jarima, kulrang —
 * ishdan ketish. Foydalanuvchi menyudan sahifaga o'tganda qaysi bo'limda
 * ekanini sarlavhani o'qimasdan ham biladi.
 */
export const CATALOG_META: Readonly<Record<SimpleCatalogKind, CatalogKindMeta>> = {
  'legal-entities': {
    kind: 'legal-entities',
    i18nKey: 'legalEntities',
    icon: Building2,
    tone: 'brand',
  },
  directions: {
    kind: 'directions',
    i18nKey: 'directions',
    icon: Compass,
    tone: 'teal',
  },
  'termination-reasons': {
    kind: 'termination-reasons',
    i18nKey: 'terminationReasons',
    icon: LogOut,
    tone: 'gray',
  },
  'award-types': {
    kind: 'award-types',
    i18nKey: 'awardTypes',
    icon: Award,
    tone: 'green',
  },
  'penalty-types': {
    kind: 'penalty-types',
    i18nKey: 'penaltyTypes',
    icon: Gavel,
    tone: 'red',
  },
  'document-types': {
    kind: 'document-types',
    i18nKey: 'documentTypes',
    icon: FileText,
    tone: 'orange',
  },
  'absence-types': {
    kind: 'absence-types',
    i18nKey: 'absenceTypes',
    icon: CalendarCheck,
    tone: 'blue',
  },
  /*
    Vakansiya nomlari — karnay ikonasi: bu E'LON, ichki hujjat emas.

    NEGA `purple` EMAS: o'sha ohang LAVOZIMLARniki (`POSITIONS_META`).
    Ikkita ro'yxatni bir xil rangda ko'rsatish aynan biz yo'q qilmoqchi
    bo'lgan chalkashlikni qaytarardi — HR yon menyuda ikkita binafsha
    bandni ko'rib, ularni bir narsaning ikki ko'rinishi deb o'ylardi.

    `blue` bo'limlar bilan takrorlanadi va bu zarar qilmaydi: ohanglar
    sakkizta, ma'lumotnomalar to'qqizta — takror muqarrar. Muhimi
    QO'SHNILAR ajralib tursin, ikonalar esa (tarmoq / karnay) bir-biriga
    umuman o'xshamaydi.
  */
  'vacancy-titles': {
    kind: 'vacancy-titles',
    i18nKey: 'vacancyTitles',
    icon: Megaphone,
    tone: 'blue',
  },
}

export const DEPARTMENTS_META: CatalogKindMeta = {
  kind: 'departments',
  i18nKey: 'departments',
  icon: Network,
  tone: 'blue',
}

export const POSITIONS_META: CatalogKindMeta = {
  kind: 'positions',
  i18nKey: 'positions',
  icon: IdCard,
  tone: 'purple',
}

/** Ma'lumotnomalarni boshqarish ruxsati. Yo'q bo'lsa — sahifa faqat o'qishga. */
export const CATALOG_MANAGE_PERMISSION = 'catalog.manage'
