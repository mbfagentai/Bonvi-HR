import {
  Award,
  Briefcase,
  BriefcaseBusiness,
  Building2,
  ChartColumn,
  ClipboardList,
  Compass,
  FileText,
  Gauge,
  Gavel,
  GitBranch,
  HeartPulse,
  IdCard,
  LayoutGrid,
  Library,
  LogOut,
  MessageSquareQuote,
  Network,
  ShieldAlert,
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
      /*
        Rag'bat va jarima — endi haqiqiy sahifa: `soon` ning o'rnini
        `permission` oldi (`incidents`/`users` bandlaridagi naqsh).
        Backend `GET /api/incentives` ni `incentives.view` bilan
        himoyalagan, ya'ni bu ruxsatsiz odam ro'yxatni SERVERDAN OLA
        OLMAYDI — bandni ko'rsatib turish ortiqcha chalkashlik bo'lardi.

        NEGA «Qobiliyatlar» va «Hujjatlar» bandlari BUTUNLAY o'chirildi:
        ikkalasi ham xodim kartasining tab'i sifatida ALLAQACHON ishlaydi
        (`employee-competency-tab.tsx`, `employee-documents-tab.tsx`).
        Ular uchun alohida umumiy ro'yxat hech qachon so'ralmagan, ya'ni
        bu bandlar «hali qurilmagan bo'lim» emas edi — ular qurilmaydigan
        bo'limlar edi. `soon` esa foydalanuvchiga «tez orada bo'ladi»
        deb va'da beradi; bajarilmaydigan va'dani menyuda saqlab turish
        yo'l xaritasi emas, shovqin.
      */
      { key: 'incentives',   to: '/ragbat',        icon: Award,
        permission: 'incentives.view' },
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
          /*
            Voronka bosqichlari — to'qqizinchi ma'lumotnoma (2-bosqich).

            NEGA aynan shu guruhda: u boshqa sakkiztasi bilan bir xil
            vazifani bajaradi — tizimning POYDEVORI, kundalik ish emas.
            Bosqichlar yiliga bir-ikki marta o'zgaradi, nomzod esa
            har kuni qo'shiladi.

            NEGA `permission` boshqalarnikidan farqli (`recruiting.view`,
            `catalog.manage` emas): yozuv `/api/recruiting/stages` da
            yashaydi va uning `GET`i `recruiting.view` bilan
            himoyalangan. Ma'lumotnomalarni boshqara oladigan, lekin
            rekrutingga ruxsati yo'q odam bu ro'yxatni SERVERDAN OLA
            OLMAYDI — bandni ko'rsatib, sahifani «bo'sh/xatoli» holatda
            ochish ortiqcha chalkashlik bo'lardi (`users` bandidagi
            naqsh).
          */
          { key: 'pipelineStages',     to: '/malumotnomalar/bosqichlar',       icon: GitBranch,
            permission: 'recruiting.view' },
        ],
      },
    ],
  },
  {
    /*
      Sarlavhasiz guruh, `sectionCatalog` bilan bir xil naqsh: bandning
      o'zi («Rekruting») sarlavha vazifasini bajaradi.

      2-bosqich QURILDI: `soon` olib tashlandi va o'rnini `permission`
      egalladi (`incentives`/`incidents` bandlaridagi naqsh). Backend
      barcha `GET` larni `recruiting.view` bilan himoyalagan, ya'ni bu
      ruxsatsiz odam ro'yxatni SERVERDAN OLA OLMAYDI — bandni
      ko'rsatib, sahifani «bo'sh/xatoli» holatda ochish ortiqcha
      chalkashlik bo'lardi.

      Guruhning O'ZI qoldi: ikkita band ham kundalik ish, lekin ular
      bitta jarayonning (voronkaning) ikki qadami — yonma-yon turishi
      va bitta nom ostida yig'ilishi ularni «xodimlar» bo'limidan
      ajratib turadi. Rekruting hali ISHGA OLINMAGAN odamlar bilan
      ishlaydi, xodimlar bo'limi esa olinganlar bilan.

      ⚠️ «SUHBATLAR» BANDI O'CHIRILDI. Rejada u alohida bo'lim edi,
      lekin backendda alohida `Interview` agregati YO'Q: suhbat ham
      shunchaki voronka bosqichi (`CandidateStageEntry` domen izohi —
      «uning kim, qachon, qanday xulosa dan boshqa maydoni yo'q»).
      Bosqichlar esa SOZLANADIGAN ma'lumotnoma, ya'ni qaysi bosqich
      «suhbat» ekanini kod hech qachon bila olmaydi: HR uni «Суҳбат 1»
      deb ham, «Техник интервью» deb ham atashi mumkin. O'rniga
      nomzodlar ro'yxatida BOSQICH bo'yicha filtr bor
      (`/nomzodlar?stage=…`) — HR o'zi «suhbat» deb hisoblagan
      bosqichni tanlaydi. Bu `/qobiliyatlar` va `/hujjatlar` bandlari
      bilan qilingan ishning aynan o'zi: bajarilmaydigan va'dani
      menyuda saqlab turish yo'l xaritasi emas, shovqin.

      ⚠️ «TAKLIFLAR» BANDI HAM O'CHIRILDI. Sabab boshqacha: taklif
      HAR DOIM aniq bir nomzodga tegishli va backend `candidateId` ni
      MAJBURIY parametr qilgan (`GET /api/recruiting/offers` —
      «barcha takliflar» degan so'rov umuman yo'q). Uning izohida
      sabab ochiq yozilgan: bunday ro'yxat HR ga hech qanday savolga
      javob bermaydi. Takliflar nomzod kartasining tabida boshqariladi
      — u yerda ular muzokara tarixi bo'lib o'qiladi.
    */
    items: [
      {
        key: 'recruiting',
        // Faqat «shu yo'lakdamiz» tekshiruvi uchun (`sectionCatalog` dagi
        // bilan bir xil naqsh): bu band bosilganda hech qayerga
        // o'tmaydi, guruhni ochadi. Bola marshrutlar esa yuqori
        // darajada (`/vakansiyalar`, `/nomzodlar`) — ular kundalik ish
        // va manzili qisqa bo'lgani ma'qul.
        to: '/rekruting',
        icon: UserSearch,
        children: [
          { key: 'vacancies',  to: '/vakansiyalar', icon: BriefcaseBusiness,
            permission: 'recruiting.view' },
          { key: 'candidates', to: '/nomzodlar',    icon: UserPlus,
            permission: 'recruiting.view' },
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
