import {
  Award,
  Briefcase,
  Building2,
  ClipboardList,
  Compass,
  FileText,
  Gauge,
  Gavel,
  HeartPulse,
  IdCard,
  LayoutGrid,
  LogOut,
  MessageSquareQuote,
  Network,
  ShieldAlert,
  Sparkles,
  Timer,
  UserCog,
  Users,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

export interface NavItem {
  /** Tarjima kaliti (`nav.*`). */
  key: string
  to: string
  icon: LucideIcon
  /** Hali qurilmagan bo'lim — menyuda o'chiq ko'rinadi. */
  soon?: boolean
}

export interface NavSection {
  /** Tarjima kaliti. Bo'sh bo'lsa — sarlavhasiz guruh. */
  key?: string
  items: NavItem[]
}

/**
 * Yon menyu tuzilmasi. PLAN.md dagi modullar ro'yxatiga mos keladi.
 * `soon: true` — S-bosqichi hali kelmagan bo'limlar.
 *
 * Ma'lumotnomalar (S2) alohida guruhda va bitta `/malumotnomalar/…`
 * yo'lagida turadi: ular tizimning poydevori, kundalik ish emas —
 * shu sababli xodimlar bilan bog'liq bo'limlardan ajratilgan.
 */
export const NAV: NavSection[] = [
  {
    items: [{ key: 'dashboard', to: '/', icon: LayoutGrid }],
  },
  {
    key: 'sectionPeople',
    items: [
      { key: 'employees',    to: '/xodimlar',      icon: Users },
      { key: 'employment',   to: '/buyruqlar',     icon: Briefcase,     soon: true },
      { key: 'probation',    to: '/sinov',         icon: Timer },
      { key: 'competencies', to: '/qobiliyatlar',  icon: Sparkles,      soon: true },
      { key: 'incentives',   to: '/ragbat',        icon: Award,         soon: true },
      { key: 'documents',    to: '/hujjatlar',     icon: FileText,      soon: true },
    ],
  },
  {
    key: 'lists',
    items: [
      { key: 'reserve',    to: '/rezerv',       icon: ClipboardList },
      { key: 'blacklist',  to: '/qora-royxat',  icon: ShieldAlert },
      { key: 'monitoring', to: '/nazorat',      icon: Gauge },
      { key: 'incidents',  to: '/hodisalar',    icon: HeartPulse,    soon: true },
      { key: 'feedback',   to: '/qayta-aloqa',  icon: MessageSquareQuote, soon: true },
    ],
  },
  {
    key: 'sectionCatalog',
    items: [
      { key: 'legalEntities',      to: '/malumotnomalar/yuridik-shaxslar', icon: Building2 },
      { key: 'directions',         to: '/malumotnomalar/yonalishlar',      icon: Compass },
      { key: 'departments',        to: '/malumotnomalar/bolimlar',         icon: Network },
      { key: 'positions',          to: '/malumotnomalar/lavozimlar',       icon: IdCard },
      { key: 'terminationReasons', to: '/malumotnomalar/sabablar',         icon: LogOut },
      { key: 'awardTypes',         to: '/malumotnomalar/ragbat-turlari',   icon: Award },
      { key: 'penaltyTypes',       to: '/malumotnomalar/jarima-turlari',   icon: Gavel },
      { key: 'documentTypes',      to: '/malumotnomalar/hujjat-turlari',   icon: FileText },
    ],
  },
  {
    key: 'sectionSystem',
    items: [
      { key: 'users', to: '/foydalanuvchilar', icon: UserCog, soon: true },
    ],
  },
]
