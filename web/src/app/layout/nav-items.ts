import {
  Award,
  Briefcase,
  BriefcaseBusiness,
  Building2,
  CalendarCheck,
  CalendarDays,
  ChartColumn,
  ChartPie,
  ClipboardCheck,
  ClipboardList,
  Clock,
  Compass,
  Cpu,
  DoorOpen,
  FileStack,
  FileText,
  Gauge,
  Gavel,
  HeartPulse,
  History,
  IdCard,
  LayoutGrid,
  Library,
  Link2,
  LogOut,
  MapPin,
  Megaphone,
  MessageSquareQuote,
  Network,
  ScanFace,
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
   * Sanoq belgisining RANGI. Berilmasa — `'warning'` (to'q sariq), ya'ni
   * «qilinadigan ish bor»: sinovdagilar, moslanmagan o'tishlar.
   *
   * NEGA alohida maydon (SPEC §8.7): menyuda ikki xil ma'noli sanoq yonma-yon
   * turadi. Moslanmagan o'tishlar — navbatdagi ish; ishdan ketgan odamning
   * turniketdan o'tgani esa XAVFSIZLIK hodisasi. Ikkalasi bir xil rangda
   * chizilsa, qizil raqam kulrang (sariq) raqam bilan bir narsani anglatib
   * qolardi va HR uni «keyinroq qarayman» ro'yxatiga qo'shib yuborardi.
   *
   * `count` dan farqli o'laroq bu qiymat STATIK — u bandning o'zida yoziladi:
   * sanoq jonli so'rovdan keladi, uning MA'NOSI esa band bilan birga
   * o'zgarmaydi.
   */
  countTone?: 'warning' | 'danger'
  /**
   * Ochiluvchi guruhning BOLALARI ham `permission` bo'yicha filtrlansinmi.
   * Sukut — yo'q: bolalar ruxsatdan qat'i nazar chiziladi (menyuning asl
   * xatti-harakati).
   *
   * NEGA ixtiyoriy va faqat «Verifikatsiya» da yoqilgan: u guruhda ikki xil
   * ruxsatli bandlar bor (`attendance.view` — ko'rish, `attendance.manage` —
   * sozlash) va faqat ko'radigan foydalanuvchi sozlash bandlarini umuman
   * ko'rmasligi kerak (ularning `GET` lari ham yopiq). Filtrni HAMMA
   * guruhlarga birdaniga yoqish esa «Rekruting» va «Ma'lumotnomalar»
   * menyusining ko'rinishini jimgina o'zgartirardi — egasi davomat moduli
   * boshqa bo'limlarga tegmasligini aniq talab qilgan. Bayroq guruhning
   * O'ZIDA yoziladi, shuning uchun uni yoqmagan guruh aynan avvalgidek qoladi.
   */
  filterChildrenByPermission?: boolean
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
      «Mening ishlarim» (docs/SPEC-mening-ishlarim.md §11.1): the viewer's own tasks, so it sits above everything. The
      red count is #2 of that SPEC. Its own section, because a viewer without `processes.view` then loses the section
      with the item (`sidebar.tsx` skips an empty one) and sees the menu exactly as before.
    */
    items: [
      { key: 'myWork', to: '/mening-ishlarim', icon: ClipboardCheck,
        permission: 'processes.view', countTone: 'danger' },
    ],
  },
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
          // Bo'lim boshliqlarining so'rovlari (egasi, 2026-10-30) — vakansiyalar yonida:
          // tasdiqlangan so'rov aynan vakansiyaga aylanadi.
          { key: 'vacancyRequests', to: '/vakansiya-sorovlari', icon: ClipboardList,
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
    /*
      Verifikatsiya — davomat (3-bosqich, Verifix o'rnini bosadi). Sarlavhasiz
      guruh, `recruiting` va `sectionCatalog` bilan bir xil naqsh: bandning
      o'zi («Verifikatsiya») sarlavha vazifasini bajaradi.

      ⭐ O'RNI — «Rekruting» DAN KEYIN, «Xodimlar» dan OLDIN (SPEC §8.1;
      egasiga ham aynan shunday aytilgan). Sabab — rekruting guruhidagi
      qoidaning o'zi: tartib mantiqan emas, AMALDA qancha ishlatilishiga
      qarab qo'yiladi. «Kim keldi, kim kechikdi, hozir binoda kim bor» —
      HR ertalab birinchi ochadigan ekran, ya'ni u kundalik ish qatorida,
      pastdagi ma'lumotnoma va sozlamalar yonida emas.

      NEGA «Xodimlar» guruhining ichida emas: davomat — alohida jarayon
      (turniket → o'tish → tabel) va o'zining yettita sahifasi bor. Ularni
      xodimlar bandlari orasiga qo'shish kundalik «Xodimlar / Buyruqlar»
      ro'yxatini ikki baravar uzaytirardi.

      Bola bandlar tartibi — yana foydalanish chastotasi: avval har kuni
      ochiladigan to'rttasi (taxta, jurnal, tabel, hisobotlar), keyin bir
      marta sozlanib, kamdan-kam qaytiladigan uchtasi (moslashtirish,
      jadvallar, qurilmalar).

      `permission` HAR BIR BOLADA, guruhning o'zida EMAS: ikki xil ruxsat
      bor. `attendance.view` — ko'rish (taxta, jurnal, tabel, hisobotlar),
      `attendance.manage` — sozlash (moslik, jadval, qurilma). Faqat
      ko'radigan foydalanuvchi sozlash bandlarini umuman ko'rmaydi — ularning
      `GET` lari ham `attendance.manage` bilan himoyalangan, ya'ni ma'lumotni
      SERVERDAN OLA OLMAYDI (yuqoridagi `permission` izohi). Buning uchun
      guruhda `filterChildrenByPermission` YOQILGAN (maydon izohi): boshqa
      guruhlarda u yo'q va ular avvalgidek chiziladi. Ikkala ruxsati ham
      yo'q foydalanuvchida bironta bola qolmaydi va guruh butunlay
      chizilmaydi (`sidebar.tsx` dagi `visibleItem`).

      Sanoq belgilari (SPEC §8.7) — `sidebar.tsx` da, jonli so'rovdan:
      «Statistika» yonida QIZIL raqam — so'nggi 24 soatda turniketdan
      o'tgan ishdan ketgan odamlar soni; «Moslashtirish» yonida oddiy
      (sariq) raqam — hali xodimga bog'lanmagan shaxs raqamlari.
    */
    items: [
      {
        key: 'verification',
        // Faqat «shu yo'lakdamiz» tekshiruvi uchun (`sectionCatalog` dagi
        // bilan bir xil naqsh): bu band bosilganda hech qayerga o'tmaydi,
        // guruhni ochadi. `/verifikatsiya` marshruti ATAYLAB yo'q (SPEC §8.1).
        to: '/verifikatsiya',
        icon: ScanFace,
        filterChildrenByPermission: true,
        children: [
          /*
            Qizil belgi AYNAN shu bandda: ogohlantirishning to'liq ko'rinishi
            statistika sahifasining eng tepasida turadi (SPEC §8.4), ya'ni
            raqamni bosgan odam to'g'ri o'sha blokka tushadi. Yo'l eskicha
            (`/jonli`) — saqlangan havolalar buzilmasin.
          */
          { key: 'attendanceBoard',     to: '/verifikatsiya/jonli',         icon: ChartPie,
            permission: 'attendance.view', countTone: 'danger' },
          { key: 'attendancePasses',    to: '/verifikatsiya/otishlar',      icon: DoorOpen,
            permission: 'attendance.view' },
          { key: 'attendanceTimesheet', to: '/verifikatsiya/tabel',         icon: CalendarDays,
            permission: 'attendance.view' },
          { key: 'attendanceReports',   to: '/verifikatsiya/hisobotlar',    icon: ChartColumn,
            permission: 'attendance.view' },
          /*
            Yo'qlik so'rovlari — sariq sanoq: qaror KUTAYOTGANLAR soni
            (SPEC-so'rovlar §B5). Qizil emas: bu xavfsizlik hodisasi emas,
            HR ning navbatdagi ishi.
          */
          { key: 'attendanceRequests',  to: '/verifikatsiya/sorovlar',      icon: CalendarCheck,
            permission: 'attendance.view', countTone: 'warning' },
          { key: 'attendanceMapping',   to: '/verifikatsiya/moslashtirish', icon: Link2,
            permission: 'attendance.manage' },
          { key: 'attendanceSchedules', to: '/verifikatsiya/jadvallar',     icon: Clock,
            permission: 'attendance.view' }, // o'qish — view, yozish sahifada `manage` bilan (SPEC-jadval-to'ri §C)
          { key: 'attendanceDevices',   to: '/verifikatsiya/qurilmalar',    icon: Cpu,
            permission: 'attendance.manage' },
          // Work sites for phone check-ins (SPEC-joylashuvlar §D). `view`, not
          // `manage`: viewers open a site read-only to see where it is.
          { key: 'attendanceSites',     to: '/verifikatsiya/joylashuvlar',  icon: MapPin,
            permission: 'attendance.view' },
        ],
      },
    ],
  },
  {
    /*
      Blankalar — HR paper forms F01–F19 moved into the platform (docs/SPEC-blankalar.md §12).
      Own untitled group like «Verifikatsiya»: the item itself is the heading. `blanks.view`
      gates it — without it the server refuses the list anyway.
      The `countTone: 'warning'` count = `waitingForMe` total (`useBlankWaitingCount` in `sidebar.tsx`).
    */
    items: [
      { key: 'blanks', to: '/blankalar', icon: FileText, permission: 'blanks.view', countTone: 'warning' },
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
      // HRMentor dan keladigan blankalar — o'qish va yuklab olish (SPEC-blankalar-hrmentor §6)
      { key: 'hrMentorBlanks', to: '/blanka-fayllari', icon: FileStack, permission: 'employees.view' },
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
        // Faqat «Yo'qlik turlari» uchun: u davomat bo'limi yoqilmagan (egasi
        // `attendance.view` bermagan) foydalanuvchida ko'rinmasin. Ruxsati
        // yozilmagan qolgan bandlar avvalgidek chiziladi (`visibleItem`).
        filterChildrenByPermission: true,
        children: [
          { key: 'legalEntities',      to: '/malumotnomalar/yuridik-shaxslar', icon: Building2 },
          { key: 'directions',         to: '/malumotnomalar/yonalishlar',      icon: Compass },
          { key: 'departments',        to: '/malumotnomalar/bolimlar',         icon: Network },
          { key: 'positions',          to: '/malumotnomalar/lavozimlar',       icon: IdCard },
          { key: 'terminationReasons', to: '/malumotnomalar/sabablar',         icon: LogOut },
          { key: 'awardTypes',         to: '/malumotnomalar/ragbat-turlari',   icon: Award },
          { key: 'penaltyTypes',       to: '/malumotnomalar/jarima-turlari',   icon: Gavel },
          { key: 'documentTypes',      to: '/malumotnomalar/hujjat-turlari',   icon: FileText },
          // Where candidates heard about the job — the bot, the online anketa
          // and HR's «Nomzod qo'shish» choose from it (SPEC-qayerdan-bilgan §3).
          { key: 'referralChannels',   to: '/malumotnomalar/qayerdan-bilgan',  icon: Megaphone },
          // Yo'qlik turlari — davomat so'rovlaridagi sabablar
          // (SPEC-so'rovlar §B1). Davomat moduliniki bo'lsa ham, HR uni
          // «Ma'lumotnomalar» ichidan qidiradi: u boshqa ma'lumotnomalar
          // bilan bir xil ishlaydi (faqat nom va faollik).
          { key: 'absenceTypes',       to: '/malumotnomalar/yoqlik-turlari',   icon: CalendarCheck,
            permission: 'attendance.view' },
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
        Audit jurnali — kim, qachon, nimani o'zgartirdi (SPEC-rollar-va-audit
        §9.1, §9.5). «Rollar» dan KEYIN: ikkalasi «kim nima qila oladi / kim
        nima qildi» degan bitta savolning ikki yarmi. `GET /api/audit` ham
        `audit.view` bilan himoyalangan — ruxsatsiz odamga band ko'rinmaydi.
      */
      { key: 'audit', to: '/audit', icon: History, permission: 'audit.view' },
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

/**
 * Foydalanuvchiga ko'rinadigan band (`[]` — ko'rinmaydi), ruxsat bo'yicha.
 *
 * Oddiy band va guruhning o'zi — o'z `permission` i bilan (`nav-items.ts`
 * dagi izoh), avvalgidek.
 *
 * Guruh BOLALARI esa faqat `filterChildrenByPermission` yoqilgan guruhda
 * filtrlanadi — «Verifikatsiya» va «Ma'lumotnomalar» (faqat «Yo'qlik turlari»
 * bandi ruxsatli; qolganlari ruxsatsiz, ya'ni o'zgarmaydi). Filtr ixtiyoriy qilindi,
 * chunki uni hammaga yoqish «Rekruting» va «Ma'lumotnomalar» menyusini
 * jimgina o'zgartirardi (egasi davomat moduli boshqa bo'limlarga
 * tegmasligini talab qilgan). Yoqilgan guruhda birorta bola qolmasa,
 * guruhning O'ZI ham yo'qoladi: hech narsa ochmaydigan sarlavha tugmasi
 * bo'lim uchun yuqorida qo'llangan qoidaning aynan o'zi.
 */
export function visibleItem(item: NavItem, hasPermission: (code: string) => boolean): NavItem[] {
  if (item.permission !== undefined && !hasPermission(item.permission)) return []
  if (item.children === undefined || item.filterChildrenByPermission !== true) return [item]

  const children = item.children.filter(
    (child) => child.permission === undefined || hasPermission(child.permission),
  )

  return children.length === 0 ? [] : [{ ...item, children }]
}
