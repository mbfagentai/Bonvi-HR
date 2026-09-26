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
  HeartPulse,
  IdCard,
  LayoutGrid,
  Library,
  LogOut,
  Megaphone,
  MessageSquareQuote,
  Network,
  ShieldAlert,
  ShieldCheck,
  SlidersHorizontal,
  ThumbsUp,
  Timer,
  UserCog,
  UserMinus,
  UserPlus,
  UserRoundX,
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
    /*
      Sarlavhasiz guruh, `sectionCatalog` bilan bir xil naqsh: bandning
      o'zi («Rekruting») sarlavha vazifasini bajaradi.

      ⭐ O'RNI — «Hisobotlar» DAN KEYIN, menyuning TEPASIDA
      (foydalanuvchi talabi, 2026-09-12). Ilgari u eng pastda, hatto
      «Ma'lumotnomalar» dan ham keyin turardi — ya'ni menyudagi eng
      ko'p bosiladigan band eng uzoq joyda edi va HR uni har safar
      izlab topishga majbur bo'lardi. Tartib mantiqan emas, AMALDA
      qancha ishlatilishiga qarab qo'yiladi: kundalik ish tepada,
      kamdan-kam ochiladigan ma'lumotnoma va sozlama pastda.

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

      ⚠️ «TAKLIFLAR» BANDI HAM YO'Q — ish taklifi funksiyasining o'zi
      butunlay o'chirildi (`docs/SPEC-nomzod-yakuni.md`, 3-bo'lim):
      nomzod endi to'g'ridan-to'g'ri xodimga aylanadi.
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
          /*
            Rad etilganlar — arxiv. `formerEmployees` bandining AYNAN
            naqshi: alohida sahifa emas, o'sha ro'yxatning boshqa kesimi
            (`?rejected=true`). Sabab o'sha yerda yozilgan — alohida
            sahifa qidiruv, saralash, sahifalash va jadval ustunlarini
            ikkinchi marta takrorlash demak.

            NEGA ALOHIDA BAND KERAK (foydalanuvchi talabi, 2026-09-08):
            rad etilganlar endi kundalik ro'yxatda KO'RINMAYDI
            (`CandidateSearchFilter.HideRejected`). Filtrning o'zi
            qoldirilsa, ular tizimda BUTUNLAY ko'rinmas bo'lib qolardi —
            «ishdan ketganlar» bandidagi bilan bir xil xavf: topib
            bo'lmagan odam qayta kiritiladi.

            ⭐ BAND IKKITA HOLATNI OCHADI (foydalanuvchi talabi,
            2026-09-10): «Biz rad etdik» (`Rejected`) va «O'zi voz kechdi»
            (`Withdrawn`) — ikkalasi ham kundalik ro'yxatdan yashirilgan,
            ikkalasi ham shu yerda. Holatlarning O'ZI esa avvalgidek
            AJRALGAN qoladi: yorliqlar boshqa rangda (qizil va sariq) va
            har bir hisobot ularni ajratadi. Shu sababli manzil `status`
            filtri emas, alohida `rejected` bayrog'i — bitta `<select>`
            ikkita qiymatni ifodalay olmaydi, holat filtri esa bo'lim
            ichida ham ishlashi kerak (SPEC §4.7). Nomi ham «Rad
            etilganlar» bo'lib qoldi: qatordagi yorliq kimning ketganini
            aytib turadi.

            Eski xatcho'p (`?status=Rejected`) ISHLASHDA DAVOM ETADI va
            faqat rad etilganlarni ko'rsatadi — torroq, lekin to'g'ri
            javob. Yo'naltirish ATAYLAB qo'yilmadi: u aynan bitta holatga
            filtrlash imkonini yo'q qilardi.

            ⚠️ Qiymat `'true'` — SATR, mantiqiy `true` EMAS. Bu ATAYLAB,
            «tuzatish» kerak emas: `NavItem.search` tipi
            `Readonly<Record<string, string>>`, `sidebar.tsx` dagi
            `matches()` esa `String(actual[key] ?? '') === value` bilan
            solishtiradi. Router `{ rejected: 'true' }` ni
            `?rejected=true` qilib yozadi va o'qiganda (har bir qiymat
            `JSON.parse` dan o'tadi) MANTIQIY `true` qaytaradi — ya'ni
            `validateCandidatesSearch` mantiqiy qiymat oladi, `matches()`
            esa `String(true) === 'true'` bo'lib mos keladi. `page`
            parametri ham xuddi shu o'qigichga tayanadi (`readPage` da
            `typeof value !== 'number'`). Mantiqiy `true` yozilsa tip
            xatosi chiqadi, tipni kengaytirish esa ikkita UMUMIY faylni
            («nav-items», «sidebar») hech qanday foyda uchun o'zgartirish
            bo'lardi — buning o'rniga menyudagi yonish tekshiruvi
            avvalgidek satr bilan ishlaydi.
          */
          { key: 'rejectedCandidates', to: '/nomzodlar', icon: UserRoundX,
            search: { rejected: 'true' }, permission: 'recruiting.view' },
        ],
      },
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
            Vakansiya nomlari — e'londa turadigan qisqa nomlar
            («Сотувчи-консультант», «Ҳайдовчи»).

            ⚠️ Bu «Lavozimlar» ning takrori EMAS va ular ataylab ikkita
            alohida band. Lavozim — kompaniyaning SHTAT birligi: u
            buyruqda, mehnat shartnomasida turadi va unga vazifalar
            biriktirilgan. Vakansiya nomi — E'LON: u tashqariga qaraydi
            va nomzod tushunadigan tilda yoziladi. Ilgari vakansiya
            lavozimlar ma'lumotnomasidan tanlanardi va natijada e'lon
            uchun o'ylab topilgan har bir nom shtat jadvaliga tushib
            ketardi — Excel'dagi 228 ta lavozimning kelib chiqishi aynan
            shunday.

            NEGA `permission` YO'Q: yozuv `/api/catalog/vacancy-titles`
            da yashaydi va u qolgan ma'lumotnomalar bilan bir xil
            `catalog.view` bilan himoyalangan.
          */
          { key: 'vacancyTitles',      to: '/malumotnomalar/vakansiya-nomlari', icon: Megaphone },
        ],
      },
    ],
  },
  {
    key: 'sectionSystem',
    items: [
      { key: 'users', to: '/foydalanuvchilar', icon: UserCog, permission: 'users.manage' },
      /*
        Rollar — egasi o'zi yaratadi va ruxsatlarini belgilaydi. «Foydalanuvchilar»
        ning YONIDA: ikkalasi bitta savolga javob beradi — kim tizimda nima
        qila oladi. Ruxsati alohida (`roles.manage`), `users.manage` emas:
        hisob ochish va huquq chegarasini belgilash — ikki xil vakolat.
        `GET /api/roles/catalog` ham shu ruxsat bilan himoyalangan, ya'ni
        bandni ruxsatsiz odamga ko'rsatish yopiq eshikka boshlash bo'lardi.
      */
      { key: 'roles', to: '/rollar', icon: ShieldCheck, permission: 'roles.manage' },
      /*
        Tizim sozlamalari — bot matni va bildirishnoma chegaralari.

        NEGA ALOHIDA BAND, shaxsiy sozlamalar oynasining bo'limi emas:
        u oyna «MENING hisobim» (rasm, til, mavzu, chiqish), bu yerdagi
        qiymatlar esa BUTUN tizimga — boshqa foydalanuvchilarga va nomzod
        ko'radigan bot matniga — ta'sir qiladi. Ikkalasi bir joyda
        turganda sozlama amalda yashirin bo'lib qolgandi.

        NEGA aynan `sectionSystem`: `/foydalanuvchilar` bilan bitta savolga
        javob beradi — tizimning o'zi qanday ishlaydi. Ikkalasining ruxsati
        ham boshqalardan qattiqroq.

        `permission` `soon` kabi «o'chiq ko'rinish» EMAS (maydon izohiga
        qarang): `GET /api/settings` ham `settings.manage` bilan
        himoyalangan, ya'ni bosh rahbar ham bu ro'yxatni SERVERDAN OLA
        OLMAYDI.
      */
      { key: 'systemSettings', to: '/sozlamalar', icon: SlidersHorizontal,
        permission: 'settings.manage' },
    ],
  },
]
