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
  Library,
  LogOut,
  MessageSquareQuote,
  Network,
  ShieldAlert,
  Sparkles,
  Timer,
  UserCog,
  UserMinus,
  Users,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

export interface NavItem {
  /** Tarjima kaliti (`nav.*`). */
  key: string
  to: string
  icon: LucideIcon
  /**
   * Manzil qidiruv parametrlari — bitta sahifaning boshqa KO'RINISHI.
   *
   * NEGA alohida sahifa emas: «ishdan ketganlar» xodimlar ro'yxatining
   * o'zi, faqat boshqa filtr bilan. Unga alohida sahifa yozish qidiruv,
   * saralash, sahifalash va kartochka ko'rinishini ikkinchi marta
   * takrorlash demak — va ular vaqt o'tib bir-biridan ajralib ketardi.
   *
   * Menyudagi faol band shu parametrlarga ham qarab aniqlanadi
   * (`sidebar.tsx` dagi `isActive`), aks holda ikkala band birdaniga
   * yonib turardi.
   */
  search?: Readonly<Record<string, string>>
  /** Hali qurilmagan bo'lim — menyuda o'chiq ko'rinadi. */
  soon?: boolean
  /**
   * Ichki bandlar. Bo'lsa, bu band HAVOLA EMAS — ochiluvchi guruh.
   *
   * NEGA kerak: ma'lumotnomalar sakkizta va ular kundalik ish emas —
   * tizimning poydevori, yiliga bir necha marta ochiladi. Sakkizta band
   * yon menyuning yarmini egallab, har kuni ishlatiladigan bo'limlarni
   * pastga surib yuborardi. Guruh sukut bo'yicha YOPIQ turadi.
   */
  children?: readonly NavItem[]
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
      { key: 'probation',    to: '/sinov',         icon: Timer },
      /*
        Ishdan ketganlar — arxiv. Alohida band ATAYLAB: agar ketgan odamni
        tizimdan umuman topib bo'lmasa, HR uni «bazada yo'q» deb o'ylab
        dublikat yaratadi — loyihaning eng asosiy muammosi shundan
        boshlangan. Xodimlar ro'yxatida esa ular ko'rinmaydi, chunki
        u kundalik ish ekrani va unda hozir ishlayotganlar turadi.
      */
      { key: 'formerEmployees', to: '/xodimlar', icon: UserMinus,
        search: { status: 'terminated' } },
      { key: 'employment',   to: '/buyruqlar',     icon: Briefcase,     soon: true },
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
    /*
      Sarlavhasiz guruh: bandning O'ZI sarlavha vazifasini bajaradi.
      Ustiga yana «MA'LUMOTNOMALAR» yozuvi qo'yilsa, bir xil so'z ikki
      marta yonma-yon turardi.
    */
    items: [
      {
        key: 'sectionCatalog',
        // Faqat «shu yo'lakdamiz» tekshiruvi uchun — bu band bosilganda
        // hech qayerga o'tmaydi, guruhni ochadi.
        to: '/malumotnomalar',
        icon: Library,
        children: [
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
    ],
  },
  {
    key: 'sectionSystem',
    items: [
      { key: 'users', to: '/foydalanuvchilar', icon: UserCog, soon: true },
    ],
  },
]
