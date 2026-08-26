import {
  Award,
  Briefcase,
  BriefcaseBusiness,
  Building2,
  ChartColumn,
  ClipboardList,
  Compass,
  FileSignature,
  FileText,
  Gauge,
  Gavel,
  HeartPulse,
  IdCard,
  LayoutGrid,
  Library,
  LogOut,
  MessageCircle,
  MessageSquareQuote,
  Network,
  ShieldAlert,
  Sparkles,
  ThumbsUp,
  Timer,
  UserCog,
  UserMinus,
  UserPlus,
  UserSearch,
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
   * Ruxsat kodi — bo'lsa, band FAQAT shu ruxsati bor foydalanuvchiga
   * ko'rinadi (`sidebar.tsx` filtrlaydi, `useAuth().hasPermission`).
   *
   * NEGA `soon` kabi «o'chiq ko'rinish» emas: `soon` — «hali qurilmagan,
   * lekin YO'L XARITASI sifatida ko'rinsin» degani, bu esa boshqa
   * masala — «bu odam BUTUNLAY yeta olmaydigan bo'lim». Masalan
   * `/foydalanuvchilar`: uning `GET`i ham `users.manage` bilan
   * himoyalangan (`UserEndpoints.cs`), ya'ni HR menejeri bu ro'yxatni
   * SERVERDAN OLA OLMAYDI — bandni ko'rsatib, sahifani «bo'sh/xatoli»
   * holatda ochish ortiqcha chalkashlik bo'lardi.
   */
  permission?: string
  /**
   * Yorliq yonidagi kichik sanoq belgisi (masalan, sinovdagilar soni).
   *
   * Statik ro'yxatda HAR DOIM `undefined`: bu massiv modul yuklanganda
   * bir marta tuziladi, sanoq esa jonli so'rovdan keladi. Qiymatni
   * `sidebar.tsx` band chizilayotganda ustiga qo'yadi — maydon shu yerda
   * faqat TIP sifatida e'lon qilingan. `0` ham chizilmasin degan qoida
   * bilan bu yerda emas, chizuvchi joyda — bu yerga `0` ni ham
   * ruxsat berish kifoya.
   */
  count?: number
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
    /*
      Sarlavhasiz birinchi guruh — O'QISH uchun mo'ljallangan tahliliy
      ekranlar. Ikkalasi ham hech narsani o'zgartirmaydi: bosh sahifa
      «hozir nima bo'lyapti», hisobotlar «bir yilda nima bo'ldi» degan
      savolga javob beradi. Quyidagi guruhlar esa ish qilinadigan
      joylar — xodim qo'shiladi, buyruq yoziladi, ma'lumotnoma
      to'ldiriladi.

      Hisobotlar `permission` bilan (`users`/`incidents` bandlaridagi
      naqsh): backend `GET /api/reports/turnover` ni `reports.view` bilan
      himoyalagan, ya'ni bu ruxsatsiz odam ma'lumotni SERVERDAN OLA
      OLMAYDI — bandni ko'rsatib, sahifani «bo'sh/xatoli» holatda ochish
      ortiqcha chalkashlik bo'lardi.
    */
    items: [
      { key: 'dashboard', to: '/',           icon: LayoutGrid },
      { key: 'reports',   to: '/hisobotlar', icon: ChartColumn, permission: 'reports.view' },
    ],
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
      { key: 'employment',   to: '/buyruqlar',     icon: Briefcase },
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
      // `EMPLOYEE_FLAG_TYPES` tartibida oxirgisi — shu sababli shu yerda
      // ham uch ro'yxatdan keyin turadi.
      { key: 'capable',    to: '/qobiliyatli',  icon: ThumbsUp },
      // Ruxsat `soon`ning o'rnini oldi: sahifa endi qurilgan, lekin
      // hammada ham ko'rinmaydi — `users` bandidagi naqsh (yuqoridagi
      // `permission` izohiga qarang).
      { key: 'incidents',  to: '/hodisalar',    icon: HeartPulse,    permission: 'safety.view' },
      { key: 'feedback',   to: '/qayta-aloqa',  icon: MessageSquareQuote, permission: 'feedback.view' },
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
    /*
      Sarlavhasiz guruh, `sectionCatalog` bilan bir xil naqsh: bandning
      o'zi («Rekruting») sarlavha vazifasini bajaradi.

      NEGA ochiluvchi guruh, to'rtta bandning har biri alohida emas:
      PLAN.md (11-qator) 2-bosqichni «Rekruting: vakansiya → anketa →
      suhbat → job offer» deb belgilaydi — u hali BOSHLANMAGAN, bu
      to'rttala band ham `soon`. To'rtta doimiy o'chiq qatorni kundalik
      menyuga yoyish «Ma'lumotnomalar»ni yig'ib turgan sababning
      aksi bo'lardi: bu safar ular kundalik ish emasligidan tashqari,
      HALI UMUMAN ISHLAMAYDI ham — ikkalanchi sabab guruhlashni yanada
      kuchliroq asoslaydi. Yig'ilganda yon menyuda bitta qator ko'rinadi,
      xohlagan ochib yo'l xaritasini ko'radi.

      NEGA shu yerda: 1-bosqich (Xodimlar, Ro'yxatlar, Ma'lumotnomalar)
      tugagach, 2-bosqich boshlanishidan OLDIN — «Tizim» (administrator)
      doim eng pastda qoladi, PLAN.md dagi bosqich tartibi menyuda ham
      saqlanadi.
    */
    items: [
      {
        key: 'recruiting',
        // Faqat «shu yo'lakdamiz» tekshiruvi uchun (`sectionCatalog` dagi
        // bilan bir xil naqsh) — hech qaysi bola marshruti hali yo'q,
        // shu sababli bu ham hech qachon haqiqiy navigatsiyaga ishtirok
        // etmaydi, lekin nomlash uslubi saqlanadi.
        to: '/rekruting',
        icon: UserSearch,
        children: [
          { key: 'vacancies',  to: '/vakansiyalar', icon: BriefcaseBusiness, soon: true },
          { key: 'candidates', to: '/nomzodlar',     icon: UserPlus,         soon: true },
          { key: 'interviews', to: '/suhbatlar',     icon: MessageCircle,    soon: true },
          { key: 'jobOffers',  to: '/takliflar',     icon: FileSignature,    soon: true },
        ],
      },
    ],
  },
  {
    key: 'sectionSystem',
    items: [
      { key: 'users', to: '/foydalanuvchilar', icon: UserCog, permission: 'users.manage' },
    ],
  },
]
