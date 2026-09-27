# Qabul qilingan farazlar

Foydalanuvchidan so'ralmagan, lekin qaror talab qilgan savollar.
Format: `- [sana] <savol> → <qaror> → <sabab>`

- [2026-09-06] Anketa holati qayerda saqlansin (20+ qadam, bot qayta ishga
  tushsa yo'qoladi)? → XOTIRADA qoladi, Redis qo'shilmaydi → botning
  «hech qanday saqlash tizimiga bog'lanmaslik» qoidasi kuchida
  (`ConversationStore` izohi); prod ishlab turganda yangi bog'liqlik
  qo'shish xavfi anketa yo'qolishi xavfidan katta. Anketa ~5 daqiqa,
  suhbat umri 60 daqiqa.
- [2026-09-06] Surat qadami majburiymi? → O'TKAZIB YUBORISH mumkin →
  selfi talab qilish eng ko'p tashlab ketiladigan qadam bo'lardi; HR
  uchun rasm foydali, lekin arizadan muhimroq emas.
- [2026-09-06] Anketa matnlari (uzunligi chegaradan oshsa) rad etilsinmi?
  → QIRQILADI, xato qaytarilmaydi → matnni yozgan odam ekran oldida
  turibdi va uzunlik xatosi butun anketani oxirgi qadamda yo'qotardi.
- [2026-09-06] Rus tili darajasi uchun yangi enum kerakmi? → YO'Q, mavjud
  `LanguageLevel` (xodim kompetensiyasi) qayta ishlatiladi → nomzod ishga
  qabul qilinganda daraja xodim kartasiga to'g'ridan-to'g'ri ko'chadi.
- [2026-09-06] Anketani HR tahrirlay oladimi? → YO'Q → bu nomzodning O'Z
  javoblari; tahrirlansa «nomzod nima deb aytdi» degan fakt yo'qolardi.
  HR fikri uchun `Candidate.Note` bor.
- [2026-09-06] «Umumiy anketa» ni bot ro'yxatidan qanday chiqarish kerak —
  `status=Open` so'ralganda HAR DOIM filtrlansinmi? → YO'Q, filtr ATAYLAB
  so'raladigan parametr bo'ldi (`?excludeGeneralPool=true`, uni faqat bot
  yuboradi) → HR ekranlari ham aynan shu `status=Open` ro'yxatidan
  foydalanadi (nomzod kiritish, boshqa vakansiyaga ko'chirish, vakansiyalar
  ekranidagi «Ochiq» filtri). Doimiy filtr HR ni nomzodni «Umumiy anketa» ga
  kirita olmaydigan qilib qo'yardi va sababi hech qayerda ko'rinmasdi.
- [2026-09-06] Anketa nomzod kartasida TO'RTINCHI tab bo'lsinmi? → YO'Q,
  «Umumiy» tabining ichida karta → anketa shaxsiy ma'lumotning davomi
  (SPEC 7-bo'limi «bo'lim» deydi, ekran emas); to'rtinchi tab HR ni ikkita
  ekran orasida yurishga majbur qilardi.
- [2026-09-06] Nomzod surati qayerda turadi? → SARLAVHA kartasida, xodim
  kartasidagi bilan aynan bir joyda → surat anketa javobi emas, nomzodning
  o'zi; anketasi yo'q nomzodda ham u ko'rinishi va yuklanishi kerak.
- [2026-09-06] Rekruting klienti `./bonvi gen` chiqargan hook'larga
  o'tkazilsinmi? → HOZIR EMAS → generatsiya ishga tushirildi va tiplar
  tekshirildi, lekin butun modulni ko'chirish anketa vazifasidan katta va
  ishlab turgan uchta ekranga tegardi. Anketa qo'lda yozilgan klientga
  QO'SHIMCHA sifatida kiritildi (o'sha fayldagi mavjud naqsh bilan).
- [2026-09-06] Nomzod surati keshining kaliti nima bo'ladi? → `hasPhoto`
  emas, faqat `candidateId` (`['candidate-photo', id]`), yangilash esa
  QO'LDA → `CandidateDto` saqlash kalitini ataylab qaytarmaydi, ya'ni
  xodim rasmidagi «yangi kalit — yangi kesh yozuvi» hiylasi ishlamaydi.
- [2026-09-06] `formatDate`/`todayIso` qaysi vaqt mintaqasida hisoblaydi?
  → BRAUZERNIKIDA EMAS, `Asia/Tashkent` da qat'iy → backend allaqachon
  `SystemClock.TashkentOffset` (UTC+5) bilan ishlaydi; boshqa mintaqadagi
  noutbukda yoki serverdagi brauzerda `todayIso()` forma orqali bazaga
  NOTO'G'RI kun yozardi.
- [2026-09-06] `employee-schemas.ts` dagi `toISOString().slice(0,10)` ham
  mintaqaga o'tkazilsinmi? → YO'Q, tegilmadi → u JSHSHIR ichidagi sanani
  TEKSHIRADI (`new Date('...T00:00:00Z')` bilan o'zi qurgan qiymatning
  aylanma nusxasi), ko'rsatmaydi. UTC ga qadalgani ataylab; mintaqaga
  o'tkazish yangi xato tug'dirardi.
- [2026-09-06] Ommaviy oferta havolasi sozlanmaganda rozilik vaqtiga nima
  yozilsin? → HECH NARSA, ustun `NULL` bo'ladi (migratsiya
  `OfertaRoziligiIxtiyoriy`) → dastlab bot yuborish lahzasini yozardi,
  ya'ni nomzod ko'rmagan ofertaga «rozilik berdi» degan huquqiy fakt
  bazaga tushardi. `NULL` = «so'ralmagan», qiymat = «aynan o'sha lahzada
  berilgan»; ikkalasi bir xil ko'rinmasligi kerak.
- [2026-09-07] Taklifsiz ishga olishda `taklif.lavozim_yoq` kabi mavjud xato
  kodlari qayta ishlatilsinmi? → YO'Q, `nomzod.lavozim_yoq` /
  `nomzod.yuridik_shaxs_yoq` / `nomzod.bolim_topilmadi` qo'shildi → xato kodi
  EKRANNING shartnomasi, bu esa boshqa ekran; qolaversa `taklif.*` kodlari ish
  taklifi bilan birga o'chiriladi va saqlanib qoladigan yo'l o'chirilayotgan
  funksiya nomiga tayanib qolmasligi kerak.
- [2026-09-07] Taklifsiz ishga olishda nomzodning `ClosedAt` iga qaysi sana
  yozilsin? → BUGUN, `hireDate` emas → qaror ayni hozir qabul qilinmoqda, ish
  boshlash esa kelajakda bo'lishi mumkin; birlashtirilsa «shu oyda voronkadan
  nechta odam chiqdi» hisoboti kelajakdagi sanani ko'rmasdi. Bu
  `AcceptJobOfferCommandHandler` dagi «javob sanasi ≠ ish boshlash sanasi»
  bo'linishining aynan o'zi.
- [2026-09-07] Rezervdagi nomzodni to'g'ridan ishga olsa bo'ladimi? → YO'Q,
  avval `/return-from-reserve` → rezerv ham YAKUN (nomzod voronkadan chiqqan);
  to'g'ridan olish yo'li ochilsa, «rezervdan qaytarish» amali ma'nosini
  yo'qotardi va nomzod voronkaga tushmasdan xodim bo'lardi.
- [2026-09-07] Rezervga o'tkazish sababi (`ClosingNote`) qaytarilganda
  saqlansinmi? → YO'Q, tozalanadi → u CHIQISH sababi; qoldirilsa faol nomzod
  kartasida «voronkadan chiqqan: …» turaverardi va `ClosedAt` bo'yicha
  hisoblaydigan hisobot uni yopilganlar qatorida sanardi. Uzoq muddatli tavsif
  uchun `Note` bor va unga tegilmaydi.
- [2026-09-07] Ishga olinganlar ro'yxatdan chiqarilishi `Reserve` ga ham
  tegsinmi? → YO'Q, faqat `Hired` → rezervning butun ma'nosi «kerak bo'lganda
  shu ro'yxatdan chaqiramiz» degani; yashirilsa u ma'no yo'qolardi.
- [2026-09-07] `Reserve` yorlig'i qaysi rangda? → KULRANG (`neutral`) → yashil
  «olindi», qizil «rad etildi», sariq «o'zi ketdi» allaqachon band va har biri
  o'z ma'nosini tashiydi; rezerv esa yakun emas, TO'XTASH — va undan
  qaytiladi. Yopilgan vakansiya ham aynan shu sababdan kulrang. To'rtinchi
  rang o'ylab topish tizimda «bu rang nimani anglatadi?» degan yangi savol
  hosil qilardi.
- [2026-09-07] Rezervdagi nomzod kartasida «Ishga olish» tugmasi bo'lsinmi,
  server uni 409 bilan rad etishini bilib turib? → HA, lekin oyna AVVAL
  `/return-from-reserve` ni chaqiradi, keyin `/hire` → serverning qoidasi
  buzilmaydi (nomzod haqiqatan voronkaga qaytadi va shundan keyin xodim
  bo'ladi), foydalanuvchi uchun esa bu bitta qaror: «bu odamni olamiz».
  Tugmani olib tashlash HR ni ikkita ekran kezishga majburlardi; uni shunchaki
  qo'yib, so'rovni chaqirish esa tushunarsiz 409 berardi. «Rezervdan
  qaytarish» tugmasi joyida qoladi — u boshqa qaror («qaytaramiz, lekin hali
  olmaymiz»).
- [2026-09-07] Taklifsiz ishga olish oynasida OKLAD maydoni bo'lsinmi (u
  topshiriqdagi maydonlar ro'yxatida yo'q edi)? → HA, «Qo'shimcha» ostida va
  `salary.view` ruxsatiga bog'liq → taklif orqali olishda summa TAKLIFDAN
  kelardi; taklifsiz yo'lda uni beradigan boshqa joy yo'q va
  `HireCandidateCommand.SalaryAmount` izohi to'g'ridan-to'g'ri «qabul
  formasidan» deydi. Maydonsiz har bir yangi xodim okladsiz ochilardi.
- [2026-09-07] Ish taklifi o'chirilganda `NotificationType` ning 8 va 9 qiymatlari
  bo'sh qoldi → qolgan turlar SURILMADI, `CandidateFromBot` hamon 10 → sanoq
  qiymatlarini surish mijoz bilan server orasidagi moslikni jimgina buzardi;
  klientda son bo'yicha o'girish shu sababli indeks emas, aniq jadval orqali
  ketadi (`notifications-api.ts` → `TYPE_BY_NUMBER`).
- [2026-09-08] «Гаплашилди» va «Кенгашга чақирилди» tugmalari KODDA sanab
  chiqilmadi → nomzod kartasidagi qator FAOL bosqichlar ma'lumotnomasidan
  chiziladi (`candidate-stage-picker.tsx`) → bosqich nomi kodga yozilsa, HR uni
  qayta nomlagan kuni tugma jimgina yo'qolardi; voronkani HR boshqarishi esa
  `PipelineStage` ning asosiy qarori. Bosqichlarning o'zi urug' fayliga va
  mavjud bazaga migratsiya bilan qo'shildi
  (`20260908050836_VoronkaBosqichlariToldirildi`).
- [2026-09-08] Bosqich qatori voronkadan chiqarish tugmalari bilan BIR qatorga
  qo'yilmadi → alohida kartada, ulardan yuqorida → «Гаплашилди» kuniga o'nlab
  marta bosiladigan odatiy qadam, «Рад этиш» esa qaytarib bo'lmaydigan yakun;
  yonma-yon turishi tasodifiy bosish ehtimolini oshirardi.
- [2026-09-08] Suhbat taklifi xabari qaysi tugmadan chiqishi — bosqich NOMIGA
  bog'lanmadi → `PipelineStage.InvitesToInterview` bayrog'i qo'shildi
  (`20260908061342_SuhbatgaChaqirish`) → nom ma'lumotnomada va HR uni istagan
  payt o'zgartiradi; kodga yozilsa tugma jimgina oddiy tugmaga aylanardi.
  Bayroq bittadan ko'p bosqichga qo'yilishi mumkin (kengash + rahbar suhbati).
- [2026-09-08] Nomzodga boradigan xabar IKKI tilda emas, BITTA tilda
  (foydalanuvchi qarori) → bot nomzod tanlagan tilni allaqachon biladi
  (`Candidate.TelegramLanguage`); Jobster ikki tilda yozardi, chunki tilni
  bilmasdi.
- [2026-09-08] Suhbat vaqtini nomzod TUGMA bilan tasdiqlamaydi (foydalanuvchi
  qarori) → xabar matni ko'rsatilgan raqamga qo'ng'iroq qilishni so'raydi;
  tugma bot suhbatiga yangi holat qo'shardi va «tasdiqladi, lekin kelmadi»
  degan yozuvni haqiqat deb ko'rsatardi.
- [2026-09-08] Taklifdagi telefon va manzil kodga emas, SOZLAMAGA chiqarildi
  (`bot.interview.phone`, `bot.interview.address`) → ofis ko'chsa yoki raqam
  o'zgarsa dasturchisiz tuzatiladi. Xaritadagi nuqta mavjud `bot.location`
  dan olinadi — ikkinchi nusxa yaratilmadi.
- [2026-09-08] Suhbat vaqti O'TGAN kunga qo'yilmaydi
  («nomzod.suhbat_vaqti_otgan»), lekin BUGUNGI kunning istalgan soati
  qabul qilinadi → HR ertalab «bugun soat 15:00» deb yozadi; soat darajasida
  tekshirish shu odatiy holatni to'sib qo'yardi, kun darajasidagi tekshiruv
  esa yil terish xatosini ushlaydi.
- [2026-09-08] RAD ETISH xabari yuqoridagi «bitta tilda» qoidasidan ISTISNO
  (foydalanuvchi qarori: «bir xil holatda borishi kerak») → matn hammaga bir
  xil, ikki tilda (o'zbekcha + ruscha) bitta xabarda va nomzod tanlagan tilga
  bog'liq emas. Sabab: bu kompaniyaning rasmiy javobi, HR da qog'ozda ham shu
  ko'rinishda turadi. Qolgan xabarlar (ariza qabul qilindi, suhbatga taklif)
  eski qoidada — nomzod tilida.
- [2026-09-08] Rad etish xabarida vakansiya nomi YO'Q (talab qilingan shablon
  shunday) → matn «vakansiyaga bo'lgan qiziqishingiz uchun» deydi; nom ikki
  tilda ikki marta takrorlanib xabarni uzaytirardi.
- [2026-09-08] Ikki tilni ajratuvchi chiziq 30 belgi, shablondagi 60 emas →
  Telegram matnni telefon ekrani bo'yicha o'raydi va uzun chiziq ikkiga
  bo'linib, ajratkich o'rniga ikkita sinuq qatorga aylanardi.
- [2026-09-08] Rad etilgan nomzod kundalik ro'yxatdan CHIQARILDI
  (`CandidateSearchFilter.HideRejected`) va menyuga alohida «Rad etilganlar»
  bandi qo'shildi (foydalanuvchi talabi) → `HideHired` bilan aynan bir xil
  qoida va bir xil sabab. «O'zi voz kechgan» va rezervdagilar bu qoidaga
  TUSHMADI: birinchisi kam uchraydi, ikkinchisining butun ma'nosi «kerak
  bo'lganda shu yerdan chaqiramiz» degani.
- [2026-09-08] «Rad etilganlar» alohida SAHIFA emas, o'sha ro'yxatning
  filtri (`/nomzodlar?status=Rejected`) → «Ishdan ketganlar» bandidagi
  naqsh; alohida sahifa qidiruv, saralash, sahifalash va ustunlarni
  ikkinchi marta takrorlash bo'lardi. Ekran BO'LIMDEK ko'rinadi: sarlavha,
  ikona va bo'sh holat matni filtrga qarab o'zgaradi, «Nomzod qo'shish»
  tugmasi esa u yerda ko'rinmaydi.
- [2026-09-08] Rad etilganlarni yashirish qoidasi Excel ARXIVIDA ishlamaydi
  (`ArchiveRequested`) → kengash arxividagi 3110 yozuvning deyarli hammasi rad
  etilgan, ikkala qoida birga qo'llansa «Excel arxivi» tanlovi bo'sh ro'yxat
  berardi. Boshqa manba tanlovlarida (bot, qo'lda) qoida kuchda qoladi:
  manba tanlash holat tanlash emas.
- [2026-09-08] Takroriy ariza MUTLAQ taqiqlanmadi — sozlamadagi muddat
  (`recruiting.reapplyMonths`, sukut 6 oy) qo'yildi (foydalanuvchi tanlovi) →
  odamning holati o'zgaradi va bir yildan keyingi urinish spam emas. Mutlaq
  taqiq kerak bo'lsa qiymat 120 oyga qo'yiladi — dasturchisiz.
- [2026-09-08] Qayta ariza muddati VAKANSIYA bo'yicha ajratilmaydi → to'siq
  odamga qo'yiladi, arizaga emas; aks holda bir odam har kuni boshqa
  vakansiyaga anketa tashlab, o'sha muammoni qaytarardi. Yon ta'siri: bitta
  vakansiyaga rad etilgan odam olti oy davomida boshqasiga ham topshira
  olmaydi.
- [2026-09-08] Nomzodlar uchun QORA RO'YXAT qurilgan edi, keyin BUTUNLAY olib
  tashlandi (foydalanuvchi qarori: «nomzod uchun alohida qora ro'yxat degan
  bo'lim qo'shish shart emas, shunchaki botdan bir marta to'liq ariza
  tashlagan odam 6 oygacha ariza tashlolmasligi kerak xolos») → talab
  faqat takroriy arizani to'xtatish edi va uni muddat qoidasining o'zi
  hal qiladi. Migratsiya commit qilinmagan va prodga chiqmagan, shu
  sababli u ham o'chirildi — bazada bo'sh ustun qolmadi. XODIMLAR qora
  ro'yxati (`EmployeeFlagType.Blacklist`) tegilmagan, u boshqa narsa.
- [2026-09-08] To'siq telefon bilan birga TELEGRAM hisobi bo'yicha ham
  qo'yiladi → bir odamda ikkita SIM bo'lishi mumkin va u o'sha hisobdan
  yangi raqam bilan qaytib kelardi. Bu tekshiruv qora ro'yxat uchun
  yozilgan edi va u olib tashlangach ham saqlab qolindi — muddat
  qoidasiga ham xuddi shunday kerak.
- [2026-09-08] Takroriy ariza qoidasi SERVERDA, botda emas → qaror ikkita
  manbaga tayanadi (sozlamadagi muddat va qora ro'yxat); bot ularni
  takrorlaganda HR ekranda muddatni o'zgartirgach, bot eski qoida bilan
  ishlab qolardi.
- [2026-09-10] `Candidate.AddNote` bo'sh matn bilan chaqirilsa nima qaytaradi (SPEC aytmagan) → xato kodi emas, `ArgumentException` → SPEC ning «Definition of Done» i AYNAN ikkita yangi xato kodini sanaydi (`nomzod.rezyume_kop`, `nomzod.rezyume_joy`); uchinchisi `./bonvi check` ni yiqitardi va u foydalanuvchiga baribir yetib bormaydi — izoh hamma amalda ixtiyoriy, ishlovchi bo'sh matnda metodni umuman chaqirmaydi.
- [2026-09-10] Fayllar SONI chegarasi ishlovchida ham tekshiriladimi (SPEC «oldindan tekshir» degan, xato manbasini aytmagan) → `RecruitingErrors.ResumeTooMany` qo'shildi, kodi domendagi bilan AYNAN bir xil (`nomzod.rezyume_kop`) → invariant `Candidate.AttachFile` da qoladi, lekin u faylni diskka yozgandan keyin ishlaydi: o'nta fayli bor nomzodga 4 MB lik suratni o'qib, kichraytirib, saqlab, so'ng o'chirish behuda ish. Yangi xato kodi paydo bo'lmadi, ya'ni `./bonvi check` ga ta'siri yo'q.
- [2026-09-10] `hasResume` ni «kamida bitta fayl bormi» ga o'girish QAYERDA bajariladi (SPEC proyeksiyaning o'zini ko'rsatgan) → SQL ichida, `joined` ning maydoni bo'lib (`EXISTS`), materializatsiyadan keyingi `page.Select(...)` da EMAS → u yerda `Any(...)` har qator uchun alohida sinxron so'rov yuborardi (25 qatorlik sahifa — 25 ortiqcha murojaat). Natija bir xil, so'rov soni bitta.
- [2026-09-10] Keyingi aloqa sanasining uch holatli qoidasi (qo'y / tozala / tegma) ikkita amalda kerak — bosqich yozish va suhbatga chaqirish → bitta `internal static` metod (`RecordCandidateStageCommandHandler.ApplyNextContact`), ikki nusxa emas → nusxalar bir kun ajralib ketardi va farqi faqat «sudrab o'tkazgandan keyin sana yo'qoldi» degan shikoyatda ko'rinardi (naqsh `RegisterCandidateCommandHandler.ParsePhone` dan).
- [2026-09-10] Yangi sozlamaning TO'LIQ kaliti qanday (shartnoma faqat
  `interviewWarningHours` degan) → `notifications.interviewWarningHours` →
  guruhdagi qo'shnilari ham aynan shu prefiks bilan yuradi
  (`notifications.probationWarningDays`, `notifications.newCandidateDays`) va
  ekran maydonni KALIT bo'yicha topadi: server boshqa nom bersa, maydon
  shunchaki chizilmaydi (xato chiqmaydi, shu sababli backend bilan nom bir xil
  bo'lishi shart).
- [2026-09-10] Uchta yangi bildirishnoma turining SON qiymatlari (shartnomada
  faqat nomlar berilgan) → `CandidateContactDue: 11`,
  `CandidateContactOverdue: 12`, `CandidateInterviewSoon: 13` →
  `NotificationType` da yangisi oxiriga qo'shiladi va oxirgi band qiymat 10
  edi. Bu jadval faqat ZAXIRA yo'l: server `JsonStringEnumConverter` bilan nom
  yuboradi va `toType` avval nomga qaraydi, ya'ni taxmin xato bo'lsa ham ekran
  buzilmaydi.
- [2026-09-10] Nomzod ismi va suhbat vaqti parametri qaysi nom bilan keladi
  (shartnoma `name`/`time` deydi, mavjud turlar esa `candidateName` yuboradi) →
  ikkalasi ham qabul qilinadi (`candidateName ?? name`, `time ?? interviewTime`)
  → shartnomaning ikki tomoni parallel yozilyapti va nom bir harfga farq qilsa
  jumlada ism yoki vaqt o'rniga bo'shliq qolardi; qo'shimcha zaxira nom
  hech kimga xalaqit bermaydi.
- [2026-09-11] Yangi bildirishnomalarning parametr nomlari (shartnoma jadvali
  `name` va `time` deydi) → serverdan `candidateName` va `interviewTime`
  ketadi → jadvaldagi nomlar FRONTEND o'rinbosarlari (`{{name}}`,
  `{{time}}`), server nomlari esa har doim ma'no bilan aniqlangan
  (`employeeName`, `endDate`, `daysLeft` — `NotificationParams` izohi) va
  `CandidateNew` aynan `candidateName` yuboradi. Qisqa `name` bir kun
  xodimniki bilan aralashardi. Frontend ikkala nomni ham qabul qiladi.
- [2026-09-11] Aloqa/suhbat bildirishnomalari REZERVdagi nomzodga chiqadimi
  → ALOQA: `InProgress` VA `Reserve`; SUHBAT: faqat `InProgress` →
  foydalanuvchi qarori (2026-09-11). Domen rezervga o'tkazishda aloqa
  sanasini ATAYLAB saqlab qoladi (`Candidate.Close`) — «o'rin bo'shaganda
  chaqiramiz». Agar eslatma chiqmasa, sana yozilardi-yu hech qachon
  ogohlantirmasdi, ya'ni saqlashning butun sababi yo'qolardi.
  Asimmetriya ham ataylab: rezervdagi odamga suhbat belgilanmaydi
  (`InviteToInterview` → `IsActive`), ya'ni suhbat so'roviga rezervni
  qo'shish mavjud bo'lmagan holatni ekranga chiqarardi.
  ⚠️ Bu so'rov qo'shni `ListNewCandidatesAsync` dan ATAYLAB farq qiladi —
  u «faol voronka» haqidagi savol, bu esa HR ning bugungi ish ro'yxati.
- [2026-09-11] «Suhbat yaqin» kalitiga sana yetadimi → YO'Q, LAHZA
  (daqiqagacha, `NotificationKey.For(..., DateTimeOffset)`) → suhbat ko'p
  hollarda O'SHA kunning ichida ko'chiriladi; sana bilan kalit
  o'zgarmasdi va ertalab «o'qildi» qilingan HR yangi vaqt haqida
  ogohlantirishni umuman olmasdi (sinov muddati uzaytirilgandagi bilan bir
  xil mulohaza).
- [2026-09-11] `interview_at` uchun indeks qo'shilmadi (migratsiya ham yo'q) →
  jadval bir necha ming qator va so'rov faqat qo'ng'iroq bosilganda ketadi;
  indeks har bir suhbat taklifida yozish narxini oshirardi. Nomzodlar soni
  o'n minglarga chiqsa — qisman indeks (`interview_at IS NOT NULL`)
  qo'shiladigan birinchi joy `NotificationReadModel` da izoh bilan
  belgilangan.
- ~~[2026-09-17] Davomat: tanaffus ishlangan daqiqalardan ayiriladimi (SPEC
  talabi «tabel qoidalari» degan, javobini bermagan)? → YO'Q, tanafus faqat
  REJA daqiqalarini kamaytiradi → turniket tushlikka chiqishni allaqachon
  yozadi, ya'ni oraliq yig'indida yo'q; ikkinchi marta ayirish bir tushlik
  uchun ikki marta jazolash bo'lardi. Binodan chiqmagan odamda esa dalil
  umuman yo'q va 1-versiya taxmin qilmaydi.~~
  ✅ **BEKOR QILINDI (2026-09-17, egasining javobi).** Tanaffus AYIRILADI,
  lekin faqat belgilangan OYNAGA nisbatan: `work_schedule_days` ga
  `break_start`/`break_end` qo'shildi va
  `breakDeduction = max(0, break_minutes − breakTaken)`, bunda `breakTaken`
  — oyna bilan xodim BINODAN TASHQARIDA bo'lgan oraliqlarning kesishmasi.
  Oyna berilmagan kunda ayirish YO'Q — shu sababli amendment'dan oldin
  yaratilgan jadvallar avvalgidek ishlayveradi. Eski qarorning ruhi saqlandi
  («bir tushlik uchun ikki marta jazolanmaydi»), farqi shundaki endi bu
  TAXMIN emas, TEKSHIRILADI (`SPEC-verifikatsiya.md` §2.4.0, §5.5.1).
  ⚠️ **Formula keyin o'zgardi (§13 #14, 2026-09-17):** endi
  `max(0, min(break_minutes − breakTaken, presentInWindow))` — pastdagi
  yozuvga qarang.
- [2026-09-17] `attendance_days` mehnat munosabatiga emas, XODIMGA
  bog'landi → turniket shaxsni yozadi, bitta jismoniy hozir bo'lishni ikki
  yuridik shaxsga bo'lib bo'lmaydi; jadval va tashkiliy kesim ASOSIY
  munosabatdan olinadi va qatorga nusxa qilinadi (§5.1).
- [2026-09-17] `Employment.ExternalRef` (PLAN.md §8 dagi 3-bosqich ilgagi)
  qo'shilmaydi → o'rniga `attendance_identities` jadvali → bitta ustun bitta
  id saqlaydi va tarixni saqlamaydi, talab esa bitta xodimda bir nechta
  qurilma idsi va bog'lash/uzish tarixi. Id SHAXSGA tegishli, shartnomaga
  emas: qayta ishga kelgan odamda yangi `Employment` ochiladi, yuzi esa
  o'sha-o'sha (§2.11).
- [2026-09-17] `CrossesMidnight` ustun sifatida saqlanmaydi (taklifda
  «bayroq» deyilgan) → hisoblanadigan xossa (`EndTime <= StartTime`) →
  saqlangan bayroq o'zi tasvirlagan ikki vaqt bilan zid bo'lib qolishi
  mumkin va o'shanda tabel manfiy oraliq hisoblab, −16 soatni ma'lumot
  sifatida ko'rsatardi (§2.4.1).
- [2026-09-17] Jadval biriktirmasida «lavozim + bo'lim» juftligi darajasi
  yo'q (taklifda ierarxiya aniq aytilmagan) → to'rt daraja: Xodim →
  Lavozim → Bo'lim (daraxt bo'ylab yuqoriga) → Standart → beshinchi daraja
  ustunlik jadvalini ikki barobar qilardi va so'ralgan ehtiyoj yo'q; kerak
  bo'lsa `scope = 5` bo'lib qo'shiladi (§2.5, §5.2).
- [2026-09-17] Ingest endpoint'i HMAC imzosini TALAB QILMAYDI (xavfsizlik
  talabi imzoni so'ragan) → Hikvision «HTTP Listening» faqat URL beradi,
  sarlavha ham, imzo ham qo'ya olmaydi → qurilmada `auth_mode = Token`
  (yo'l ichidagi 256-bitli token), imzo esa `TokenAndHmac` rejimida tayyor
  turadi va kelajakdagi agent/ZKTeco adapteri uchun sozlama bo'lib qoladi
  (§3.2).
- [2026-09-17] Yo'ldagi token log'ga tushib ketadimi → `ErrorLogFormatter`
  ga bitta qoida qo'shiladi: `/api/attendance/ingest/**` →
  `/api/attendance/ingest/***` → so'rov yo'li Serilog'da yoziladi va
  `IncludeQueryInRequestPath = false` faqat qidiruv qismini tozalaydi,
  ya'ni tokenni to'xtatmaydi (§3.3).
- [2026-09-17] Ingest autentifikatsiyasining TO'RT xil xatosi bitta kod
  bilan qaytadi (`davomat.ingest_ruxsat_yoq`, 401) → tekshiruvchi qaysi
  bosqichda yiqilganini bilmasligi kerak; haqiqiy sabab faqat log'da.
  Faolsiz qurilma esa ATAYLAB ajratilgan (403) — chaqiruvchida yaroqli
  token bor va o'rnatuvchiga «o'chirilgan» degan javob kerak (§3.2).
- [2026-09-17] Dedupe kaliti qanday (qurilma `serialNo` bermasligi mumkin)
  → bitta `dedupe_key` ustuni ikki shaklda: `s:<serialNo>` yoki
  `t:<shaxs>|<soniyagacha vaqt>|<yo'nalish>`, unikal indeks
  `(device_id, dedupe_key)` → ikkita alohida indeks bir xil hodisani ikki
  marta o'tkazib yuborardi; soniyagacha qisqartirish ataylab — ba'zi
  proshivkalar millisekund farqi bilan qayta yuboradi (§3.6).
- [2026-09-17] Soat xatosi katta bo'lgan o'tish tashlab yuboriladimi →
  YO'Q, 30 kungacha SAQLANADI va belgilanadi, 30 kundan oshsa rad etiladi
  (400) → besh daqiqa shoshayotgan qurilma kim o'tgani haqida rost gapiradi
  va uni tashlash odamni «kelmagan» hisobotiga tushirardi; 1970-yilni
  ko'rsatayotgani esa xato emas, buzuq — u `attendance_days` ni ma'nosiz
  sanalarga sochib yuborardi (§3.8).
- [2026-09-17] Hangfire faqat paket havolasi edi, hech qayerda ulanmagan →
  shu modul ulaydi (`Startup/BackgroundJobs.cs`, `hangfire` sxemasi,
  `WorkerCount = 2`), dashboard marshruti YO'Q va `Testing` muhitida server
  umuman ishga tushmaydi → dashboard sukut bo'yicha himoyasiz admin sahifa;
  test muhitida fon oqimi bazani test o'rtasida o'zgartirib turardi (§9.1).
- [2026-09-17] Hisoblangan kunni HR qo'lda tuzata oladimi → YO'Q, va
  bunday endpoint umuman yo'q → tabel tahrirlanadigan bo'lsa dalil
  bo'lishdan to'xtaydi va har bir bahs «kim o'zgartirgan» ga aylanadi;
  noto'g'ri ma'lumot MANBADA tuzatiladi (moslik, jadval, qurilma soati,
  ro'yxatdan o'tmagan qurilma) va qayta hisoblanadi (§1.2 #5, §5.8).
  ⚠️ **QISMAN o'zgardi (2026-09-17, egasining javobi):** o'tish yozuvi
  QO'LDA o'chirilmaydi va endpoint ham yo'q — bu o'z kuchida. Lekin arxiv
  endi MUDDAT bilan eskiradi (`attendance.passRetentionDays`, standart 365
  kun, `0` — hech qachon). Farqi printsipial: muddat siyosati hamma uchun
  bir xil, oldindan e'lon qilingan va sozlamalar ekranida ko'rinib turadi;
  qo'lda o'chirish esa tanlab, jimgina va voqeadan keyin bo'ladi. Birinchisi
  — oddiy arxiv boshqaruvi, ikkinchisi yozuvning ishonchliligini yo'q
  qiladi. `attendance_days` (tabel, soatlar, kechikish) HECH QACHON
  o'chirilmaydi (§1.2 #5, §2.9, §9.2.1).
- [2026-09-17] ~~Faqat~~ `attendance.photoRetentionDays` tizim sozlamasiga
  chiqariladi, qolgan chegaralar (bounce, skew, oyna) domen konstantasi
  (`AttendanceRules`) → har bir sozlama ekranda qator, ta'rif, chegara va
  to'rtta tarjima talab qiladi; boshqasi so'ralmagan va konstantani keyin
  sozlamaga ko'chirish bir qatorlik ish (§2.8).
  ✅ **TUZATILDI (2026-09-17):** sozlama endi IKKITA — unga
  `attendance.passRetentionDays` qo'shildi (egasining qarori). Qolgan
  chegaralar haqidagi qoida o'zgarmadi.
- [2026-09-17] Tanaffus oynasi kesib o'tuvchi (tungi) smenada qanday
  tekshiriladi (egasi «smenaning o'z o'qida» degan, formulani bermagan) →
  uchala vaqt ham smena boshidan boshlab daqiqaga o'giriladi
  (`offset(t) = (t − start_time + 1440) mod 1440`) va oyna
  `0 <= offset(start) < offset(end) <= shiftLength` shartiga tekshiriladi →
  xom soat qiymatlari solishtirilsa, 20:00→08:00 smenadagi 00:30 tanaffus
  «smenadan oldin» bo'lib ko'rinardi va yaroqli jadval rad etilardi
  (`SPEC-verifikatsiya.md` §2.4.0.1).
- [2026-09-17] `last_out_at` dan keyingi vaqt tanaffus deb hisoblanadimi →
  YO'Q, u «tashqarida bo'lgan oraliq» emas → aks holda soat 12:55 da uyga
  ketgan odamga butun tanaffus hisoblanib, ayirish KAMAYARDI, ya'ni erta
  ketish natijani YAXSHILARDI. Bu yaxlitlash farqi emas, teskari rag'bat —
  ayirish qila oladigan eng yomon ish (§5.5.1).
  ⚠️ **Misol eskirdi (2026-09-17):** `presentInWindow` cheklovidan keyin
  12:55 dagi odamdan ikkala o'qishda ham 0 ayiriladi. Qoida o'z kuchida, chunki
  tanaffus oynadan QISQA bo'lganda farq qoladi: 13:00–14:00 oynada 30 daqiqalik
  tanaffus, 13:45 da uyga ketgan — qabul qilingan o'qishda 30, «dum» hisoblansa
  15 ayirilardi (§5.5.1 da misol yangilandi).
- [2026-09-17] Tanaffus oynasi belgilanmagan kunda nima bo'ladi →
  AYIRISH UMUMAN YO'Q → davomiylikni oynasiz kunning biror qismiga bog'lab
  bo'lmaydi: 13:10 dagi chiqish tushlikmi yoki yumushmi — bilib bo'lmaydi, va
  taxmin qilish aynan egasi rad etgan narsa. Yon foydasi: amendment'dan
  oldingi jadvallar hech qanday migratsiyasiz avvalgidek ishlaydi (§5.5.1).
- [2026-09-17] `passRetentionDays` va `photoRetentionDays` nomuvofiq
  qo'yilsa (masalan 30 va 60) → saqlash rad etiladi
  (`davomat.saqlash_muddati_nomuvofiq`), ish esa baribir
  `max(pass, photo)` bilan ishlaydi → surat FAYLI o'tish qatori orqali
  topiladi; qator oldin o'chsa fayl abadiy qolib ketardi va uni hech kim
  topa olmasdi. Ikki qatlam ataylab: bu xato diskni JIMGINA to'ldiradi
  (§2.9, §9.2.1).
- [2026-09-17] `passRetentionDays = 0` nimani anglatadi (egasi «0 = never
  delete» degan, `photoRetentionDays` da esa min 7) → `0` faqat O'TISH
  muddatida qabul qilinadi va «hech qachon o'chirma» degani; surat
  muddatida `0` umuman yo'q → ikkalasiga qarama-qarshi himoya kerak:
  suratda `0` bugungi rasmlarni o'chirardi (u aynan o'sha hafta kerak),
  o'tishda esa `0` — auditor qo'yishi mumkin bo'lgan qonuniy siyosat (§2.9).
- [2026-09-17] Ishdan ketgan xodimning o'tishi qayerda ko'rinadi (egasi
  «jadvalda ko'milgan holat qiymati bo'lmasin» degan, joyini aytmagan) →
  UCHTA joyda: jonli taxtaning ENG TEPASIDA yopib bo'lmaydigan qizil
  ogohlantirish, qurilmalar sahifasida qisqa qator, yon menyuda xavf
  rangidagi sanoq → tuzatish qurilmada bajariladi, ya'ni ogohlantirish
  qurilmalar sahifasidan ham ko'rinishi kerak; sanoq esa tizimning istalgan
  ekranidan bilinadi. Hisoblash `attendance_passes` dan, tungi ishdan EMAS —
  talab «sodir bo'lgan ONDA» edi (§6.5, §8.4, §8.7).
- [2026-09-17] Ogohlantirishga filtr (bo'lim, qidiruv) qo'llanadimi → YO'Q →
  ishdan ketgan odamning joriy bo'limi yo'q, va bo'lim filtri yashira
  oladigan xavfsizlik ogohlantirishi — ogohlantirish emas (§6.5).
- [2026-09-17] O'tishlarni tozalash ishi `attendance_days` ni «iflos» deb
  belgilaydimi → YO'Q, va bu bosh harflar bilan yozilgan taqiq →
  keyin ishga tushgan qayta hisoblash kunni TIRIK QOLGAN o'tishlardan
  qaytadan yig'ardi va bir yillik tabelni jimgina nolga aylantirardi. Bu
  moduldagi eng zararli xato (§9.2.1, §11.1 #67).
- [2026-09-17] SPEC dagi tanaffus xato kodlari (oltita, o'ylab topilgan) B1
  yozgan koddan farq qiladi → SPEC KODGA moslashtirildi: uchta kod
  (`davomat.jadval_tanaffus_toliq_emas`, `…_oynasi`, `…_uzunligi`), dam olish
  kunidagi oyna rad etilmaydi — jimgina tozalanadi, `break_minutes = 0` li oyna
  qabul qilinadi → B1 ning tanlovi asosli (operator uchun «oyna bo'sh» va
  «oyna smenadan tashqarida» bitta xato), kod tayyor va tekshirilgan; SPEC ni
  ushlab turish faqat qayta ish talab qilardi (`SPEC-verifikatsiya.md` §2.4.0,
  §6.9).
- [2026-09-17] `ck_work_schedule_days_break_window` da `break_minutes > 0`
  sharti yo'q (SPEC loyihasida bor edi) → SPEC migratsiyaga moslashtirildi,
  shart QO'SHILMAYDI → uni qo'shish ikkinchi migratsiya demak, qoida esa
  zararsiz: davomiyligi 0 bo'lgan oyna hech narsa ayirmaydi (§2.4.0).
- [2026-09-17] `davomat.xodim_topilmadi` va `davomat.jadval_topilmadi` B1
  kodida BO'SH id uchun `Error.Validation` (400) bo'lib chiqadi, SPEC da esa
  «topilmadi» (404) → bu yerda KOD noto'g'ri, SPEC emas; bo'sh id uchun
  `davomat.xodim_yoq` / `davomat.jadval_yoq` talab qilinadi → butun
  `api/src` da `*_yoq` = Validation «ko'rsatilmagan», `*_topilmadi` = NotFound
  «mavjud emas» (`mehnat.xodim_yoq`, `baxtsiz_hodisa.xodim_yoq` /
  `baxtsiz_hodisa.xodim_topilmadi`); bu ikki qator yagona istisno. Qoldirilsa
  bitta kod ikki HTTP holati va ikki ma'no oladi, `./bonvi check` esa buni
  ushlay olmaydi (§6.9.1).
- [2026-09-17] B1 ning `WorkScheduleDay.ValidateBreakWindow` tungi smenada
  xato qiladi degan gumon → GUMON NOTO'G'RI chiqdi: `TimeOnly` ayirmasi 24
  soat bo'yicha aylanadi va natija har doim [0, 24 soat) da, ya'ni kod §2.4.0.1
  ga aynan mos. Haqiqiy nuqson SPEC ning O'Z matnida edi: §5.5.1 «§2.4.0.1
  oynani allaqachon lahzaga aylantirgan» deb yozgandi, holbuki u faqat
  tekshiruv siljishlarini beradi → formula qo'shildi:
  `breakWindowStart = plan_start_at + (break_start − start_time)` → aks holda
  quruvchi sanani soat bilan qo'shib (`D + break_start`) dushanba kechasidagi
  00:30 tanaffusni DUSHANBA 00:30 ga qo'yardi va har bir tungi smena xodimidan
  har kecha butun tanaffus jimgina ayirilardi (§5.5.1, §11.1 #61).
- [2026-09-17] Davomat B4: hech bir kun oynasiga tushmagan o'tish (masalan
  ofis xodimining 01:00 dagi chiqishi) qaysi kunga yoziladi (§5.3 aytmaydi)
  → o'z `local_date` iga → SPEC «jadval yo'q ⇒ o'z sanasi» deydi; eng
  yaqin smenaga «yopishtirish» hech kim ko'rmaydigan taxmin bo'lardi.
- [2026-09-17] Davomat B4: ochiq oraliq ustiga yana kirish (sakrash
  oynasidan tashqarida) → oldingi oraliq tashlab ketiladi, daqiqa bermaydi,
  `MissingExit` belgilanadi → ketma-ket ikki chiqishdagi `MissingEntry` bilan
  simmetrik; oraliqni birinchi kirishdan hisoblash biz bilmagan vaqtni
  ishlangan deb yozardi.
- [2026-09-17] Davomat B4: sakrash chegarasi → roppa-rosa 60 s ham sakrash
  (`<=`), solishtirish oxirgi SAQLANGAN o'tish bilan; `pass_count` sakrashlarni
  HAM sanaydi → «10 ta o'tishdan 8 tasi sanaldi» savoliga javob shu ikki son
  (`AttendanceRules.BounceSeconds` izohi).
- [2026-09-17] Davomat B4: ikki yopilgan oraliq orasida yetim chiqish yoki
  tashlab ketilgan kirish bo'lsa, «tashqarida o'tgan vaqt» qayerdan boshlanadi
  → oldingi YOPILGAN oraliqning oxiridan keyingi YOPILGAN oraliqning boshigacha;
  orasidagi o'tishlar chegarani siljitmaydi → §5.5.1 ta'rifi aynan «ketma-ket
  yopilgan oraliqlar orasi»; oynadagi borliq faqat yopilgan oraliqlardan
  sanalgani uchun vaqt ikki marta hisoblanmaydi.
- [2026-09-17] Davomat B4: munosabati yo'q sanada jadval hal qilinadimi →
  YO'Q (reja, kechikish, erta ketish 0; holat `NotEmployed`) → §5.2 hal
  qilishni asosiy munosabat orqali ta'riflaydi; ishdan ketgan odamga
  «kechikdi» deyish ma'nosiz.
- [2026-09-17] Davomat B4: `WorkSchedule.IsActive = false` jadval hal
  qilishda o'tkazib yuboriladimi → YO'Q → §5.2 bunday shart qo'ymaydi;
  o'tkazib yuborish xodimlarni jimgina pastki pog'onaga tushirib, soatlarini
  o'zgartirardi. Amaldagi biriktirma biriktirmaning o'zini yopish bilan
  to'xtatiladi.
- [2026-09-17] Davomat B4: tirik biriktirma ko'rsatgan jadval yuklanmagan
  yoki jadval kunlari yuklanmagan bo'lsa → ISTISNO, jim natija emas → jim
  yo'l xodimni pastki pog'onaga yoki «dam olish kuni» ga tushirib, butun
  korxonaning `Absent` qatorlarini sezdirmasdan yo'qotardi.
- [2026-09-17] Davomat B4: bir sanani bir nechta `is_primary` munosabat
  qamrasa → ular orasida eng erta ishga kirgan, keyin kichik id → §5.1
  ning 2–3-qoidalari aynan shu tanlov uchun yozilgan; natija hech qachon
  o'qish tartibiga bog'liq bo'lmaydi.
- [2026-09-17] Davomat B4: `planned_minutes` manfiy chiqishi mumkinmi (domen
  smenadan uzun tanaffusni rad etmaydi) → 0 dan pastga tushmaydi → manfiy
  reja ortiqcha ishni ishlangan vaqtdan ham katta qilib ko'rsatardi.
- [2026-09-17] §13 #14: tanaffus ayirmasi xodim oyna ICHIDA binoda bo'lgan
  vaqt bilan cheklanadimi → HA, koordinator qarori (mening tavsiyam «yo'q»
  edi): `presentInWindow` = yopiq oraliqlarning oyna bilan kesishmasi,
  `breakDeduction = max(0, min(break_minutes − breakTaken, presentInWindow))`
  → binoda bo'lmagan odamga tushlik hisoblash egasiga aytilgan «hech kim ikki
  marta jazolanmaydi» qoidasiga zid. Oqibati: ayirma HECH QACHON ishlangan
  daqiqadan oshmaydi (`breakDeduction ≤ presentInWindow ≤ yopiq yig'indi`).
  Qaror egasining o'zi tomonidan emas, uning qoidasi asosida koordinator
  tomonidan qabul qilingan (`SPEC-verifikatsiya.md` §5.5.1, §13 #14).
- [2026-09-17] Daqiqaga qisqartirish qanday (formulada aytilmagan) → uchala
  miqdor (yopiq yig'indi, `breakTaken`, `presentInWindow`) aniq soniyalardan
  hisoblanib, HAR BIRI BIR MARTA oxirida qisqartiriladi, oraliqma-oraliq emas →
  aks holda `presentInWindow` bir daqiqaga yopiq yig'indidan oshib, «ayirma
  ishlangan vaqtdan oshmaydi» degan kafolat ba'zi kunlarda bir daqiqaga
  buzilardi (§5.5.1).
- [2026-09-17] `MissingExit` kunida (09:00 kirdi, 13:00 chiqdi, 14:00 kirdi,
  chiqmadi) `breakTaken` nechaga teng → 0, avvalgi 60 emas → ta'rif «YOPIQ
  oraliqlar orasidagi bo'shliqlar» deydi va bunda yopiq oraliq bitta; SPEC ning
  eski qatori va #59 test o'z ta'rifiga zid edi. Natija (240 daqiqa) o'zgarmaydi,
  lekin endi saqlanadigan `break_taken_minutes` ustuni to'g'ri qiymat oladi
  (§5.7, §11.1 #59).
- [2026-09-17] Tabel katagidagi tanaffus izohi nimaga tayanadi → FAQAT
  saqlangan ikki ustunga (`breakDeductionMinutes`, `breakTakenMinutes`);
  «tanaffus oynasi belgilanmagan» shakli olib tashlandi → cheklovdan keyin
  «0 olingan, 0 ayirilgan» ham «oyna yo'q», ham «oyna paytida binoda emas»
  degani bo'lishi mumkin; bugungi jadvalni o'qib hal qilish esa raqam
  hisoblanmagan jadvalni tasvirlashi mumkin edi (§8.6).
- [2026-09-17] Ishdan ketgan xodim ogohlantirishi — har bir O'TISH uchunmi
  yoki har bir ODAM uchunmi (SPEC §6.5 va §8.4 bir-biriga zid edi) → har bir
  ODAM uchun bitta yozuv, eng oxirgi o'tishi bilan, `passCount` qo'shildi →
  §8.4 «har bir odam uchun bitta qator» va qurilmalar sahifasidagi
  «{{n}} xodim» matni aynan shuni talab qiladi (§6.5).
- [2026-09-17] `GET /passes/{id}` topilmaganda qaysi kod → yangi
  `davomat.otish_topilmadi` (404), jami kodlar 41 → uy qoidasi: `*_topilmadi`
  = NotFound; muddati o'tib o'chirilgan o'tish uchun ham shu kod, chunki
  ogohlantirishdagi havola eski bo'lishi mumkin (§6.9).
- [2026-09-17] Qurilma yaratilganda/token almashtirilganda javob shakli (SPEC
  «qurilma va token» degan, tekis yoki ichma-ichligini aytmagan) → ichma-ich
  `{ device, token }`, F1 aynan shunday yozgan → qurilma DTO si bitta qayta
  ishlatiladigan tur bo'lib qoladi va bir martalik sir undan ko'rinib ajralib
  turadi (§6.2).
- [2026-09-17] `GET /schedules/resolve` javobida nima bo'lishi kerak (SPEC
  faqat «jadval va g'olib pog'ona» degan) → F1 maydonlariga qo'shimcha:
  xodim nomi, asosiy mehnat munosabati va uning yuridik shaxs/bo'lim/lavozimi,
  `matchedDepartment*` (g'olib bo'lim ota bo'lim bo'lishi mumkin) va sanadagi
  jadval kuni → «nega unda zavodning jadvali» degan savolga faqat shu maydonlar
  javob beradi; natija faqat ikki null bilan aniqlanadi: `employmentId` yo'q —
  ishlamaydi, `scope` yo'q — jadval yo'q (§6.4).
- [2026-09-17] O'tish DTO sida soat xatosi bayrog'i kim hisoblaydi → server
  (`skewWarning`), mijoz `clockSkewSeconds` dan qayta hisoblamaydi → aks
  holda 300 soniyalik chegara TypeScript da ikkinchi nusxa bo'lib qolardi (§6.5).
- [2026-09-17] Jadvalsiz kunda (NoSchedule/NotEmployed) ortiqcha ish nechaga teng? → 0; jadval bor dam olish kunida esa ishlangan vaqtning hammasi → normasi noma'lum kunda har daqiqa «ortiqcha» bo'lib, xulosa hisobotini shishirardi
- [2026-09-17] Qayta hisoblashda kun endi saqlanmasligi kerak bo'lsa (jadval o'zgarib dam olish kuniga aylandi, o'tish yo'q), eski qator nima bo'ladi? → o'chiriladi → attendance_days hosila ma'lumot; qayta hisoblash noldan hisoblash bilan bir xil holatga kelishi shart, aks holda dam olish kunida «Kelmadi» qolib ketardi
- [2026-09-17] Biriktirilgan jadvalni faolsizlantirish mumkinmi? → yo'q, biriktirmalari bor jadval faolsizlantirilmaydi (xato kodi bilan rad etiladi) → hisob mexanizmi IsActive ga qaramaydi; ruxsat berilsa, ekran «o'chdi» deydi, tabel esa uni ishlatishda davom etardi
- [2026-09-17] O'tishlar jurnalida davr berilmasa nima ko'rsatiladi va 92 kunlik chegara unga ham qo'llanadimi → sukut: bitta kun (`to`, berilmasa bugun); ha, 92 kun → jadval yiliga ~2 mln qator o'sadi, chegarasiz davr butun arxivni saralashga olib kelardi; hisobotlar (chetlanish, xulosa) sukut bo'yicha «shu oy boshidan bugungacha»
- [2026-09-17] Tabel va xulosadagi `presentDays` «chiqish yozilmagan» kunni ham sanaydimi → yo'q, faqat `Present` holati; kun ustunlari bir-birini kesmaydi → xulosada `problemDays` alohida, ikki joyda sanalsa yig'indi oydagi kunlardan oshib ketardi
- [2026-09-17] Tabelda kim ro'yxatda turadi → oyda filtrga mos munosabati bo'lgan YOKI filtrga mos kun yozuvi bor xodim; bo'lim filtri ichki bo'limlar bilan → oy o'rtasida ko'chgan odam eski bo'lim tabelida qoladi va ishdan ketib turniketdan o'tgan odam (`NotEmployed` yozuvi) ko'rinadi; xodimlar ro'yxati ham bo'limni ichki bo'limlari bilan filtrlaydi
- [2026-09-17] Chetlanish va xulosa hisobotlari hosil qilingan kataklarni ham oladimi → yo'q, faqat saqlangan kun yozuvlari, filtr kun SURATI bo'yicha → hosil qilingan «kelmagan» faqat qayta hisoblash hali yetmagan kunda bo'ladi; hisobot ko'chirishdan keyin o'zgarmasligi kerak (§2.6)
- [2026-09-17] Taxtada yo'nalishi noma'lum o'tish «kelmagan»/«kechikkan» sanog'iga ta'sir qiladimi → ha, faqat «ichkarida» ro'yxatidan chiqariladi; birinchi kelish §5.5 3-qadami bilan olinadi; «bugungi o'tish» = reja boshlanishidan 6 soat oldindan (§5.3 oynasi) → yo'nalishsiz turniketdan kelgan odamni «kelmagan» deyish taxtani yolg'onchi qiladi; tungi smenachining ertalabki chiqishi kechki smenaga «kelgan» bo'lib sanalmasin
- [2026-09-17] Taxtadagi odamlar kartasi qaysi tartibda → ism bo'yicha → 30 soniyalik yangilanishda kartalar joyida qoladi, stoldan qarab odamni topish mumkin
- [2026-09-17] Davomat eksportining chegarasi → 20 000 qator (o'tishlar, chetlanishlar, xulosa) va tabelda 2 000 xodim, oshsa yangi `davomat.eksport_juda_kop` → `.xlsx` xotirada to'liq quriladi, server 2 GB; 20 000 qator butun korxonaning ~4 kuni yoki bo'limning bir oyi; umumiy `eksport.juda_kop` tarjimasida «5000 ta» yozilgan
- [2026-09-17] Tabel Excel fayli qanday shaklda → ikki varaq: «Tabel» (qator — xodim, ustun — kun, katakda ishlangan SOAT soni son bo'lib, ishlanmagan kunda qisqa belgi) va «Kunlar» (har xodim-kun alohida qator, daqiqa son + «soat:daq» matn) → SPEC ning «ikki ustun» qoidasini 31 kunlik katakda qo'llab bo'lmaydi (62 ustun); katak ekrandagidek soat, yig'indi va pivot uchun batafsil varaq
- [2026-09-17] Qabul yo'lida moslik keshi «topilmadi» javobini ham
  saqlaydimi (SPEC §4.1 «har bir qidiruv 60 s keshlanadi» degan) → YO'Q, faqat
  TOPILGAN moslik keshlanadi (`AttendanceIdentityCache`) → «topilmadi» keshga
  yozilgan lahzada HR moslikni yaratib, to'ldirish o'tib bo'lsa, keyingi 60
  soniyadagi har bir o'tish bog'lanmagan qolib ketardi va ularni hech narsa
  qayta ko'rmasdi; salbiy javobsiz poyga oynasi bitta o'tishga qisqaradi.
- [2026-09-17] Hikvision `subEventType = 76` nima (SPEC «76 — barmoq izi»
  degan) → Hikvision ISAPI jadvalida 76 = `MINOR_FACE_VERIFY_FAIL` (yuz
  tasdiqlanMADI), barmoq izi 38 → 76 rad etilganlar ro'yxatida, 38 o'tishlar
  ro'yxatida → 76 ni o'tish deb olish rad etilgan har bir yuzni «kirdi» qilib
  yozardi (`HikvisionEventParser`).
- [2026-09-17] Shaxs raqamisiz Hikvision hodisasi — har doim 400
  `davomat.shaxs_raqami_bosh` mi (SPEC jadvali shunday) → faqat kichik turi
  TANISH o'tish turi bo'lsa 400; turi noma'lum/yo'q bo'lsa 200 bilan
  tashlanadi; `majorEventType` 1/2/3 (signal, nosozlik, amal) ham tashlanadi
  → terminal eshik ochildi/yopildi kabi shaxssiz hodisalarni ham yuboradi;
  ularga 400 qaytarilsa terminal ularni cheksiz qayta yuborardi, ulardan esa
  hech kimning davomatini tiklab bo'lmaydi.
- [2026-09-17] Ko'p hodisali so'rovda bittasi buzuq bo'lsa → HAMMASI rad
  etiladi, hech biri yozilmaydi (avval hammasi tekshiriladi, keyin yoziladi)
  → aks holda 150-hodisada 400 qaytib, 149 tasi yozilgan bo'lardi va agent
  qayta yuborganda tushunarsiz yarim holat chiqardi.
- [2026-09-17] Takror kalitidagi yo'nalish — qurilma AYTGANImi yoki rol
  bo'yicha TO'LDIRILGANImi (SPEC `(int)Direction` degan) → qurilma aytgani →
  HR qurilma rolini ikki yuborish orasida o'zgartirsa, to'ldirilgan yo'nalish
  boshqacha chiqib, bitta hodisa ikki marta yozilardi.
- [2026-09-17] `last_seen_at` qachon yoziladi (SPEC «autentifikatsiyaga
  yetib kelgan har qanday so'rov» degan) → faqat to'liq tasdiqdan (token,
  imzo, IP) O'TGAN va FAOL qurilma so'rovida; o'tish rad etilsa ham yoziladi →
  noto'g'ri token qaysi qurilmaniki ekani noma'lum, imzosi yo'q so'rov esa
  qurilma kuzatuv ustunlarini yozishga haqli emas; SPEC §11.1 #3 faolsiz
  qurilmada «hech narsa yozilmaydi» deydi.
- [2026-09-17] Qabul tokenining xeshi nimadan olinadi → token SATRINING UTF-8
  baytlaridan (base64url dan ochilmaydi), format bitta joyda —
  `AttendanceDeviceToken` → ro'yxatga olish va qabul ikki xil xeshlasa hech
  bir qurilma o'tmaydi va xato faqat «401» bo'lib ko'rinadi; noto'g'ri
  base64 satr ham istisnosiz «topilmadi» bo'lishi kerak.
- [2026-09-17] Application qatlamida log yo'q, qabul yo'li esa 401 sababini
  logga yozishi SHART (§3.2) → `IAttendanceIngestJournal` porti, amalga
  oshirish Api da (`AttendanceIngestJournal`, EventId 5200–5204) → yozuvga
  so'rov usuli va tokensiz yo'l kerak — bular HTTP tushunchalari
  (`ICurrentUser` precedenti); Application ga log kutubxonasini qo'shish esa
  butun qatlamning qoidasini o'zgartirardi.
- [2026-09-17] Yo'ldagi token faqat `ErrorLogFormatter` da tozalansa yetadimi
  (SPEC §3.3 shunday degan) → YO'Q, Serilog boyituvchisi `IngestTokenScrubber`
  hamma chiqishdan OLDIN tozalaydi, formatter esa istisno matnini → formatter
  faqat FAYLNI yozadi; `docker logs api` dagi «Request starting …» va
  «HTTP POST … responded» satrlarida token ochiq qolardi (C3 da tekshirildi).
- [2026-09-17] B3 fayllar ro'yxatidan tashqari ikki fayl → `Application/Common/DependencyInjection.cs`
  ga `services.AddAttendanceIngest()` (B6 uni `AddAttendance()` bilan
  almashtiradi; `TryAdd*` tufayli ikkalasi qolsa ham xavfsiz) va
  `Infrastructure/DependencyInjection.cs` ga oltita davomat repozitoriysi
  (B1 ulamagan edi) → usiz qabul endpointi birinchi so'rovdayoq DI xatosi
  bilan yiqilardi va C3 ni tekshirib bo'lmasdi. `Program.cs` da ham bitta
  emas, UCHTA qator: endpoint, jurnal ro'yxati va log boyituvchisi.
- [2026-09-17] Yetim surat fayllarini «attendance/**» ostidan supurish kerakmi? → v1 da yo'q; surat faqat qatordagi `photo_key` bo'yicha o'chiriladi → `LocalFileStorage` kalitlari `yyyy/MM/<guid>.ext` va o'tish suratlari xodim rasmlari hamda hujjatlari bilan BIR jildda turadi; u yerni supurish HR fayllarini o'chirib yuborishi mumkin edi
- [2026-09-17] Taxtada nechta raqam: SPEC to'rtta (ichkarida/kutilgan/kelmagan/kechikkan), egasi Verifix'dagi beshta (binoda/kelgan/kechikkan/kelmagan/vaqtida %) → beshta; «kutilgan» «kelganlar» kartasining izohida → egasining talabi aniqroq, SPEC ning to'rtala raqami ham ekranda qoladi.
- [2026-09-17] «Vaqtida %» qanday hisoblanadi → `(expectedCount − lateCount) / expectedCount`, kutilgan 0 bo'lsa «—» → `lateCount` smenasi boshlangan kelmaganlarni ham sanaydi, shuning uchun hamma smena boshlangach bu aynan «vaqtida kelganlar»; «kelganlardan vaqtida kelganlar» esa serverda yo'q ikki raqamni taxmin qilishni talab qilardi.
- [2026-09-17] O'tishlar oqimida «vaqtida/kechikdi» holati (o'tish DTO'sida yo'q) → faqat «kechikdi»: bugungi `kind=Late` hisobotidan, `firstInAt` o'tish lahzasiga aynan teng bo'lsa; «vaqtida» belgisi qo'yilmaydi → holat kunning xususiyati, rejasi o'tishda yo'q; server hisoblamagan kunni «vaqtida» deyish yolg'on bo'lardi.
- [2026-09-17] Taxtadagi qizil blok qaysi manbadan: taxta javobidagi `formerEmployeeAlerts` yoki `/former-employee-alerts` → alohida endpoint → menyu nishoni bilan bitta kesh (son va qatorlar hech qachon farq qilmaydi) va taxtaning og'ir hisobi yiqilsa ham xavfsizlik ogohlantirishi qoladi; server ikkalasini bitta metoddan beradi.
- [2026-09-17] Taxta ko'rinishi: SPEC «ichkaridagilar kartalari», egasi «o'tishlar oqimi» → ikkalasi, segment bilan, sukut — oqim → egasining so'nggi aniq talabi; kartalar SPEC bo'yicha saqlandi.
- [2026-09-17] Soat banneri rangi: §8.4 bir joyda «qizil», keyinroq «soat og'ishi sariq qoladi» → sariq (`warning`) → tuzatish matni keyinroq va aniqroq; qizil faqat ishdan ketgan xodim uchun.
- [2026-09-17] Tabel katagi oynachasi qanday ochiladi (SPEC «hover/tap») → bosish/tegish (Radix menyusi, `role="dialog"`), sichqoncha ustiga kelganda emas → zich gridda hover har katakda miltillaydi, planshetda hover yo'q; Radix portal chetda qirqilmaydi va inline `style` bilan koordinata yozishni talab qilmaydi.
- [2026-09-17] Ishlanmagan kun katagida nima turadi (SPEC «bitta raqam», egasi «qisqa holat kodi») → ishlangan kunda soat, 0 daqiqali kunda holat kodi (K, D, JY…), `NotEmployed` bo'sh → raqam 0 hech narsa aytmaydi; legenda kod va rangni birga tushuntiradi.
- [2026-09-17] Tanaffus satri qaysi kataklarda → odam binoda bo'lgan kunda (kirish/chiqish yoki daqiqa bor) → «Kelmadi» yoki «Dam olish» katagida «tanaffus ayirilmadi» to'g'ri, lekin hech qanday raqamni tushuntirmaydi.
- [2026-09-17] Jurnal va hisobotlarda sana maydonlari bo'sh qoldirilsinmi (server bo'shni bugun / oy boshidan bugungacha deb oladi) → ochiq to'ldiriladi → HR ekranda qaysi kunlarni ko'rayotganini o'qishi kerak.
- [2026-09-17] Jurnaldagi qurilma filtri faqat `attendance.view` egasiga → ko'rsatilmaydi, faqat `attendance.manage` da → `GET /devices` `manage` ostida (§7.3); bo'sh ro'yxatli tanlov «qurilma yo'q» deb yolg'on gapirardi.
- [2026-09-17] Tabelda «qayta hisoblash» tugmasi (SPEC sahifalar ro'yxatida aytilmagan) → bor, faqat `attendance.manage`, oy uchun, bugundan narisiz → §5.8 dagi tuzatishlar shu sahifada yoziladi va jadval tuzatilgach keyingi qadam aynan shu; kelajak kunni hisoblash hali kelmagan ish kunini «kelmadi» deb yozardi.
- [2026-09-17] Jadvalni «faolsizlantirish» tugmasi qayerdan (§6.4 da activate/deactivate endpoint'i yo'q, `isActive` esa DTO'da bor) → ekranda faqat O'CHIRISH (`DELETE /schedules/{id}`), `isActive=false` jadval «Nofaol» yorlig'i bilan ko'rinadi → shartnomada yo'q endpoint o'ylab topilmadi; biriktirmasi bor jadvalda tugma oldindan o'chiq va server rad etsa (`davomat.jadval_biriktirilgan`) xuddi shu tushuntirish chiqadi.
- [2026-09-17] Biriktirmaning xodimi va importdagi aniq moslik taklifi uchun rasm kaliti DTO'da yo'q → rasm `EMPLOYEE_PHOTO_PROBE` belgisi bilan so'rab ko'riladi (404 → bosh harflar) → egasining qoidasi «rasm bor bo'lsa — rasm»; B6 `photoKey` qo'shsa, belgi olib tashlanadi.
- [2026-09-17] Import tasdig'i noaniq/topilmagan qatorlar hal qilinmaguncha bloklanadimi → ha, lekin «tanlanmaganlarni o'tkazib yuborish» tugmasi bor → noaniq qatorni ko'rmasdan tasdiqlash noto'g'ri odamni turniketga bog'lashning eng oson yo'li; ko'p qatorni o'tkazib yuborish esa ongli bir bosish.
- [2026-09-17] Moslik uchun xodim tanlashda ishdan ketganlar ham ko'rinadimi → ha, kulrang «Ishdan ketgan» yorlig'i bilan → Verifix ro'yxatida ketganlar ham bor va ularning eski o'tishlari aynan shu bog'lanish orqali tabelga tushadi.
- [2026-09-17] Jadval muharririda tanaffus oynasi smena ichidami — klient ham tekshiradimi (F1 izohi «takrorlanmaydi» degan edi) → ha, domen ko'zgusi `attendance-schedule-rules.ts` da, server kodlari bilan; «Saqlash» klient xatosi bor paytda o'chiq → egasining talabi: operator muammoni saqlashdan OLDIN ko'rsin; haqiqat manbasi server qoladi.
- [2026-09-17] Davomat B6: `IBackgroundJobQueue` SPEC dagi `void Enqueue(Expression<Action<TJob>>)` shaklidami → yo'q, `Task<string> EnqueueAsync(Expression<Func<TJob, Task>>)` → vazifalar bazaga yozadi (asinxron), `Action` sinxron variantni `async void` yoki bloklab kutishga majbur qilardi; `/recompute` javobida `jobId` bor; Hangfire aynan shu shaklni qabul qiladi
- [2026-09-17] Davomat B6: moslik to'ldirishi kunlarni o'zi qayta quradimi yoki iflos navbatni (B7) kutadimi → o'zi: bog'langan o'tishlar sanalari ±1 kun qayta hisoblanadi va FAQAT o'sha o'tishlar `processed_at` oladi → C6 qo'lda turtkisiz ishlashi shart; (2)-qadamda yiqilsa o'tishlar iflos qoladi va navbat tiklaydi
- [2026-09-17] Davomat B6: qo'lda `/recompute` `processed_at` ni belgilaydimi → yo'q → davr chetidagi o'tish davrdan tashqaridagi qo'shni sanaga tegishli bo'lishi mumkin; belgilash iflos navbatdan haqiqiy ishni yashirardi, belgilamaslik esa faqat bir marta ortiqcha (bir xil) hisoblash
- [2026-09-17] Davomat B6: `/recompute` javobidagi `dayCount` nima → davrdagi kalendar kunlar (ikki chekka bilan), xodim × kun emas → mijoz «N xodim × M kun» deb o'zi ko'paytiradi; `employeeCount` alohida
- [2026-09-17] Davomat B6: `FindEmployeeCandidatesAsync` SPEC dagidek bitta qator uchunmi → yo'q, barcha import qatorlari uchun bitta chaqiruv, xodimlar (~2 400, faqat id/ism/JSHSHIR/rasm) bir marta xotiraga → 5 000 qatorlik faylda qatorma-qator 5 000 so'rov bo'lardi; yig'ilgan ism ustida indeks yo'q
- [2026-09-17] Davomat B6: importdagi ism otasining ismisiz (ikki so'z) bo'lsa moslanadimi → ha, «familiya ism» bo'yicha ANIQ; uch va undan ko'p so'z — to'liq F.I.Sh. bo'yicha → Verifix ro'yxatlarida otasining ismi ko'pincha yo'q; ikkala holatda ham «ichida qidirish» emas, keng tarqalgan ism «noaniq» (2…5) yoki «topilmadi» bo'ladi
- [2026-09-17] Davomat B6: import faylida shaxs raqami bo'sh qator (yakun, izoh) nima bo'ladi → tashlanadi, `totalRows` ga kirmaydi → uni hech kimga bog'lab bo'lmaydi va oldindan ko'rishda faqat shovqin
- [2026-09-17] Davomat B6: faolsiz jadvalga yangi biriktirma qilish mumkinmi → yo'q, `davomat.jadval_faolsiz` (409); jadvalni yoqish/o'chirish uchun `POST /schedules/{id}/activate|deactivate` qo'shildi (§6.4 da yo'q) → koordinator qarori (biriktirilgan jadval faolsizlantirilmaydi) ikkala yo'ldan ham buzilmasin: «faolsiz jadvalda tirik biriktirma yo'q»
- [2026-09-17] Davomat B6: jadvalni o'chirish/faolsizlantirishni to'sadigan «tirik biriktirma» — muddati o'tgani ham sanaladimi → ha, o'chirilmagan har qanday biriktirma → o'tgan davrni qayta hisoblash uni hali ishlatadi; mijoz ham shunday sanaydi
- [2026-09-17] Davomat B6: o'tish bor-u surati yo'q (yoki tozalangan) bo'lsa `GET /passes/{id}/photo` qaysi kod → yangi `davomat.surat_yoq` (404), `otish_topilmadi` emas → «havola eskirgan» va «o'tish bor, rasm yo'q» ekranda ikki xil holat
- [2026-09-17] Davomat B6: qurilmaning `allowed_ips` xatosi qanday kod → yangi `davomat.ip_royxati_notogri` (400), saqlashda; bo'sh element («a,,b», oxirgi vergul) ham xato, garchi qabul yo'li uni tashlab yuborsa ham → qabul o'qilmaydigan elementni «hech narsani qamramaydi» deb qurilmani jimgina qulflaydi; saqlashda qat'iyroq bo'lish xavfsiz tomon
- [2026-09-17] Davomat B6: 64 belgidan uzun shaxs raqami (moslik/import) → yangi `davomat.shaxs_raqami_uzun` (400) → domen uzunlikni tekshirmaydi va bazadagi `varchar(64)` 500 qaytarardi; bunday raqamni hech bir terminal bermaydi — odatda faylda noto'g'ri ustun
- [2026-09-17] Davomat B6: so'rov tanasidagi enum maydonlari (qurilma provayderi, fazo, yo'nalish roli, tasdiq usuli; moslik/import `provider`) majburiymi → ixtiyoriy, berilmasa domen sukuti (Hikvision / Verifix / ReportsDirection / Token) → tushib qolgan maydon `0` — sanoqda yo'q qiymat — bo'lib bazaga yozilib, qabul yo'lida tahlilchi tanlashni buzardi
- [2026-09-17] Davomat B6: jadvalni yumshoq o'chirishda uning yetti kuni nima bo'ladi → bazada qoladi (`WorkScheduleRepository.Remove` kunlarni «o'zgarmagan» holatga qaytaradi) → EF `Cascade` ni darhol qo'llab kunlarni HAQIQATAN o'chirardi, o'qish modeli va qayta hisoblash esa o'chirilgan jadvalni kunlari bilan yuklaydi
- [2026-09-17] Davomat B6: jurnaldagi `lateMinutes` qaysi o'tishga qo'yiladi → faqat `first_in_at` lahzasi o'tishnikiga teng kunning kechikkan birinchi kirishiga; saqlangan qator bo'lsa undan, bugungi qatorsiz kun — taxtadagi AYNAN o'sha hisoblashdan, eski qatorsiz kun — `null` → sana/yo'nalish bo'yicha qayta taxmin qilinmaydi; jurnal bir sahifa uchun oylab o'tish yuklamaydi (so'rovlar soni sahifaga bog'liq emas)
- [2026-09-17] Davomat B6: moslanmagan guruhning «birinchi/oxirgi ko'rilgan» vaqti → `occurred_at` (odam qachon o'tgan), `received_at` emas; mosliklar ro'yxati xodim familiyasi bo'yicha → HR odamni topadi, raqamni emas
- [2026-09-17] Davomat F1 gen almashtiruvi: OpenAPI butun sonlarni `integer | string` deb beradi — sahifalarga shundayligicha o'tkazilsinmi → yo'q, generatsiya qilingan tipdan hosil qilingan `AsSent<T>` (`number | string` → `number`) va bitta `asSent` tasdig'i → server yozishda doim JSON son yuboradi (jonli API 15 javobda tekshirildi); runtime'da umumiy o'girish mumkin emas, `rawPersonId` ham raqamga o'xshaydi
- [2026-09-17] Davomat F1 gen almashtiruvi: import oldindan ko'rish generatsiya qilingan chaqiruv bilanmi → yo'q, axios + generatsiya qilingan javob tipi → generatsiya qilingan kod `Content-Type: multipart/form-data` ni qo'lda qo'yadi, `http-client.ts` esa aynan shu sarlavha chegara (boundary) ni yo'qotib yuklashni buzganini qayd etgan
- [2026-09-17] Davomat F1: jadvalni yoqish/o'chirish qanday ko'rinishda → kartada «Faol» almashtirgichi; yoqish darhol, o'chirish tasdiq oynasi bilan (biriktirmasi bor jadvalda tugma o'chiq), faolsiz jadvalga «Biriktirish» taklif qilinmaydi → qurilmalar sahifasidagi naqsh; server ikkalasini ham 409 bilan rad etadi, ekran buni bosishdan oldin aytadi
- [2026-09-17] Davomat B7: o'tish tozalash chegarasi SPEC dagidek `received_at` bo'yichami → yo'q, `local_date` bo'yicha (surat tozalash esa SPEC dagidek `received_at`) → `received_at` da suratsiz qatorlar uchun indeks yo'q — har 2 000 lik to'plam ~2 mln qatorli jadvalni to'liq ko'zdan kechirardi; kun aynan `local_date` bo'yicha quriladi, ya'ni muzlatish chegarasi ham shu sana bilan aniq bo'ladi (30 kungacha soat og'ishi uni noaniq qilmaydi)
- [2026-09-17] Davomat B7: o'tishlari tozalangan kun keyin qayta hisoblansa (qo'lda `/recompute`, iflos navbatning ±1 qo'shnisi, eski o'tishli kechikkan moslik) → hisoblanmaydi: tozalash birinchi qatorni o'chirishdan OLDIN yashirin `attendance.frozenThrough` ni ko'taradi, `AttendanceDayRecomputer` shu sanagacha (o'zi ham) kunlarga tegmaydi → aks holda bir yillik tabel omon qolgan o'tishlardan jimgina nolga qayta qurilardi (§9.2.1); chegara sozlamadan hisoblanmaydi — muddat keyin uzaytirilsa allaqachon o'chirilgan kunlar yana «ochilib» qolardi
- [2026-09-17] Davomat B7: bog'lanmagan (moslik kutayotgan) eski o'tish ham `processed_at IS NULL` — o'chiriladimi → HA, surat fayli bilan (koordinator qarori, B7 dagi «yo'q» bekor qilindi; SPEC §9.2.1 AMENDED); faqat bog'langan-u hisobga olinmagan o'tish qoladi va ogohlantirish oladi → egasi xom yozuvlar uchun bir yilni tanlagan; bog'lanmagan o'tish sanasi o'chirishdan oldin muzlatiladi, ya'ni kechikkan moslik ham uni tabelga qo'sha olmaydi — qoldirish jadvalni mehmon/sinov kartalari bilan cheksiz o'stirardi; ro'yxat va o'chirish orasida to'ldirish bog'lab qo'ysa, `DELETE` shartni qayta tekshirib qatorni qoldiradi
- [2026-09-17] Davomat B7: tor oynada bog'lanmay qolgan o'tish (moslik bor, o'tish bog'lanmagan) qaysi vazifada tuzatiladi → har 15 daqiqalik iflos navbatning BIRINCHI qadami, `AttendanceBackfillJob.RunAsync` ning o'zi chaqiriladi → yangi yozuvchi yo'q; o'tish ko'pi bilan 15 daqiqada tabelga tushadi
- [2026-09-17] Davomat B7: parallel qayta hisoblash to'qnashuvi qanday yopiladi → `ON CONFLICT` upsert emas: to'plam kuzatuvdan unutilib, bazadan qayta o'qib yoziladi (3 urinishgacha), taniladi `IDatabaseFault.IsConcurrentDayWrite` bilan → upsert xom SQL da `AttendanceDay.Compute` domen xaritasini takrorlardi; ikkalasi bitta eskirgan qatorni o'chirganda (`DbUpdateConcurrencyException`, nol qator) ham xuddi shu yo'l
- [2026-09-17] Davomat B7: iflos navbatda turli sanali xodimlar bitta qayta hisoblash chaqiruviga tushadimi → yo'q, xodimlar ±1 kengaytirilgan sana TO'PLAMI bo'yicha guruhlanadi → yozuvchi «xodimlar × sanalar» ni hisoblaydi va o'tishlarni chekka sanalar oralig'ida yuklaydi: bugungi xodim bilan bir yillik o'tishi kechikib bog'langan xodim bitta chaqiruvda butun yilni yuklab, bir-birining sanalarida hisoblanardi
- [2026-09-17] Davomat B7: tozalashda bitta fayl o'chmasa (ruxsat, disk xatosi) → o'sha qator tegilmaydi, qolgani davom etadi, jurnalga ogohlantirish (5307) → vazifa yiqilsa har kecha qaytarilib, disk to'lardi; o'tish qatorini fayli o'chmasdan o'chirish esa faylni yetim qilardi
- [2026-09-17] Davomat B7: bazada qo'lda buzilgan muddat qiymati → surat muddati quyi chegaradan (7) past bo'lsa 7, manfiy o'tish muddati — «hech qachon»; `frozenThrough` sana bo'lmasa — istisno (qayta hisoblash to'xtaydi, jurnalda ko'rinadi) → uchala holatda ham o'chirmaslik / qayta yozmaslik tomoniga
- [2026-09-17] Davomat B7: tungi qayta hisoblash oralig'i → bugun − 2 … bugun (B6 taklifi, SPEC «oxirgi 3 kun») → 03:10 da bugungi ish kuni «Kelmadi» bo'lib saqlanadi — tabel hosil qilingan katakda ham xuddi shuni ko'rsatardi; kelganlar iflos navbatda 15 daqiqa ichida tuzaladi
- [2026-09-17] Davomat paneli (`GET /dashboard`): firma/bo'lim/lavozim filtri asosiy munosabatgami (taxta) yoki sanani qamraydigan istalgan munosabatgami (tabel) → tabeldagidek, istalgan qamraydigan munosabat, bo'lim ichki bo'limlari bilan; ko'rsatiladigan bo'lim/lavozim va jadval esa asosiy munosabatdan → topshiriq «/timesheet dagidek» degan; ikki firmadagi odam ikkala firmaning panelida ham turadi
- [2026-09-17] Davomat paneli: `arrivedAt`/`leftAt` faqat DayOff da to'ldiriladimi → yo'q, o'tish bo'lsa har qanday holatda (NoSchedule ham), `lateMinutes` esa faqat Late da → shartnoma ularni holatga bog'lamagan; jadvalsiz odam kelgan bo'lsa, HR buni ko'rishi kerak
- [2026-09-17] Davomat paneli: kechikish chegarasi lahza bo'yicha (09:10:30 — Late, 10 daqiqa), taxtaning `lateCount` i esa butun daqiqa bo'yicha (0 → kechikmagan) va hali kelmaganlarni ham kechikkan sanaydi → taxta o'zgartirilmadi (egasi: «/board stays as is»), panel egasi tasdiqlagan qoidada → ikkala ekranning «kechikdi» soni bir daqiqalik oynada va hali kelmaganlar bo'yicha farq qilishi mumkin — bu ataylab
- [2026-09-17] Davomat paneli: kirish yo'q-u faqat chiqish bor kunning «birinchi o'tishi» qaysi kunga tegishli → `PassAttribution` bilan, hisoblash taqvimidagi AYNAN o'sha tarkibda (asosiy munosabat → hal qiluvchi → reja) → `AttendanceDayBuilder` birinchi o'tish lahzasini tashqariga bermaydi va dvigatel fayllariga tegilmasin degan qoida bor
- [2026-09-17] Davomat statistikasi: rasmdagi «✕» (diagramma turini tozalash) doim ko'rinadimi → yo'q, faqat «Ustunli diagramma» tanlanganda → sukut holatida u hech narsani o'zgartirmaydigan tugma bo'lib qolardi
- [2026-09-17] Davomat statistikasi: «qadash» (pin) nimani eslab qoladi → firma, bo'lim, lavozim, saralash va sahifa hajmi; kun, qidiruv, holat va sahifa raqami EMAS → ular bir martalik savol, ertaga kechagi qidiruv yoki «Kechikkanlar» bo'lagi bilan ochilgan ro'yxat chalg'itardi; qo'llanishi — faqat manzilda shu maydonlarning birortasi ham bo'lmaganda, sahifaga kirishda bir marta
- [2026-09-17] Davomat statistikasi: qadalgan filtr va ustunlar qayerda → brauzerda (localStorage), kalitda foydalanuvchi id si; ustunlardan YASHIRILGANLAR saqlanadi → bitta kompyuterda ikki HR; keyin qo'shilgan ustun eski sozlamada ham ko'rinsin
- [2026-09-17] Davomat statistikasi: bugungi kun manzilga yoziladimi → yo'q, `date` faqat o'tgan kun uchun → sahifa kun bo'yi ochiq turadi va yarim tundan keyin o'zi yangi kunga o'tishi kerak (tabeldagi oydan farqli qaror)
- [2026-09-17] Davomat statistikasi: diagramma animatsiyasi → o'chiq → recharts har 30 soniyalik yangi javobda animatsiyani qayta boshlab, son yorliqlarini ~2 soniyaga yashirardi (SPEC §8.5 «no spinner churn»)
- [2026-09-17] Davomat statistikasi: xodim ismi havola bo'ladimi → faqat `employees.view` bo'lsa; aks holda oddiy matn va qator bosilmaydi → xodim kartasi serverda shu ruxsat bilan yopiq
- [2026-09-17] Davomat statistikasi: «ogohlantirishni tekshirib bo'lmadi» degan qizil blok qoladimi → yo'q, faqat ogohlantirish BOR bo'lsa qizil panel → egasi: panel faqat ogohlantirish bo'lganda; eski taxta mantiqi talab bo'yicha tashlandi
- [2026-09-17] Davomat statistikasi: legenda «qatorda 4 ta» → `sm`–`lg` oralig'ida va `2xl` da 4 ta, `lg`–`xl` da 2 ta → yonma-yon ko'rinishda diagramma ustuni 300–340 px, to'rt ustunda «Ish kuni boshlanmagan» uch qatorga bo'linardi
- [2026-09-17] Saqlash muddatlari kartasi (Qurilmalar): sozlamalar sahifasidagi tarjima kalitlari o'chiriladimi → faqat ishlatilmay qolganlari (`settings.system.groupAttendance*`, `attendance.passRetentionBelowPhoto`); `attendance.photoRetention*` / `passRetention*` (yorliq, izoh, «0 — hech qachon», qisqartirish ogohlantirishi) kartada QAYTA ishlatildi → bir xil matnni yangi nom bilan nusxalash ikki manba bo'lardi; «surat muddatidan qisqa» qoidasini endi faqat server aytadi (xato kodi tarjimasi bor)
- [2026-09-18] Davomat paneli, davr presetlari: «Joriy oy» oy oxirigachami yoki bugungachami → 1-sanadan BUGUNGACHA; «Oyning ikkinchi yarmi» esa ataylab to'liq (16 — oy oxiri, hatto hammasi kelajakda bo'lsa ham) → kelajak kun davomat emas va «joriy oy» grafigini «hali boshlanmagan» bilan cho'zib qo'yardi; yarim oyni esa foydalanuvchi ONGLI tanlaydi va qisqartirilgani nomiga to'g'ri kelmasdi
- [2026-09-18] Davomat paneli: chegaralari TENG «Ixtiyoriy oraliq» (from = to) qaysi rejimda ochiladi → bitta kun rejimida (jadval, chiplar, diagramma bilan) → bir kunlik chiziq bitta nuqta bo'lardi, jadval esa aynan shunda kerak; kun tanlagichi ham faqat shu rejimda ma'noli
- [2026-09-18] Davomat paneli: ko'rinish turi (`donut`/`bars`/`list`) sahifa holatidami yoki manzildami → MANZILDA (`?view=`) → SPEC C6 uni qadaladigan sozlamaga aylantirdi, qadash esa manzil maydonlari ustida ishlaydi; yon foydasi — «ro'yxat ko'rinishida yubor» degan havola ishlaydi
- [2026-09-18] Davomat paneli: davr grafigidagi qaysi qatorlar yoqilgani saqlanadimi (manzil yoki qadash) → yo'q, sahifa holatida; sukut — Vaqtida/Kechikdi/Kelmadi → bu chizmani o'qishning bir martalik usuli, ko'rsatiladigan ma'lumot emas; SPEC C6 ro'yxatida ham yo'q
- [2026-09-18] Davomat paneli: asboblar qatoridagi olib tashlanadigan holat chipi qoladimi → yo'q, uning o'rnini jadval tepasidagi chiplar qatori (SPEC C4) egalladi → bitta holat uchun ikkita boshqaruv qoldirish ekranda ziddiyat bo'lardi; «Hammasi» chipi filtrni aynan o'sha «✕» kabi tozalaydi
- [2026-09-18] Davomat paneli: `xl` dan tor ekranda filtr ustuni yashiriladimi yoki umuman chizilmaydimi → UMUMAN chizilmaydi → ichida uchta `<select>` va ikkita sana maydoni bor; yashirin holatda ular fokus tartibida qolib, klaviatura bilan yurishni buzardi
- [2026-09-18] Davomat paneli, `/dashboard/trend`: 92 kunlik davrning o'tishlari qanday o'qiladi → 14 kunlik BO'LAKLARDA (92 kunga 7 so'rov), munosabatlar va jadval hal qiluvchisi bir marta → kun bo'yicha aylanish ~14 soniya bo'lardi (byudjet 1,5 s), butun oraliqni bir marta o'qish esa ~yarim million qatorni 2 GB li serverning xotirasida ushlab turardi
- [2026-09-18] Davomat paneli: SPEC C1 dagi «xl dan filtr ustuni» jadvalni ezib qo'ydi (egasi ushladi; C9 aynan shundan ogohlantirgan) — nima qilinadi → ustun EKRAN kengligiga emas, sahifa MAYDONIGA qarab chiqadi (container query, maydon ≥ 1100 px), va ustun bor paytda diagramma jadvalning chapidan TEPASIGA tasma bo'lib o'tadi → 1512 px ekranda uch ustun yonma-yon jadvalga ~525 px qoldirardi, jadval esa ~704 px so'raydi; media query sidebar yig'ilganini (±188 px) ko'rmaydi. C1 ning «o'ngda doimiy filtr ustuni» mazmuni saqlandi, faqat chegara o'zgardi
- [2026-09-19] Tug'ilgan kunlar paneli: sana qanday ko'rsatiladi (Verifix «20 сентябрь» deb yozadi) → `20.09`, oy nomisiz; bugun va ertaga esa so'z bilan → `format.ts` qoidasi: sana barcha tillarda bir xil ko'rinishda (`dd.MM.yyyy`), oy nomi to'rtta til uchun to'rtta yangi ro'yxat va yangi format demak bo'lardi; yil bu ustunda shovqin, «Bugun/Ertaga» esa aynan harakatga chorlaydigan ikkitasi
- [2026-09-19] Tug'ilgan kunlar paneli: panel ham 30 soniyada yangilanadimi (panel va grafik kabi) → yo'q, umumiy `staleTime` (60 s) → tug'ilgan kun sanasi kun davomida ko'chib ketmaydi; har 30 soniyada uchinchi so'rov yuborish 2 GB li serverda tekinga tushmaydi
- [2026-09-19] Tug'ilgan kunlar paneli: chegaradan tashqaridagi `limit` (0, 500) → 400 emas, QISILADI (1…50, sukut 10) → bu ko'rinish tafsiloti, javobning ma'nosi emas; xato ekrani yon ustunni sababsiz bo'shatardi (hisobotlardagi davr tekshiruvidan farqi shu)
- [2026-09-19] Tug'ilgan kunlar paneli: bosh sahifadagi `BirthdayQuery` ishlatiladimi → yo'q, o'qish modelida alohida so'rov → u FAQAT berilgan kunlarni biladi va org-filtri yo'q; panelning to'plami esa `/dashboard` ning bugungi to'plami bo'lishi kerak (filtr tanlanganda ikki ro'yxat farq qiladi — bu ataylab)
- [2026-09-19] Xodim kartasidagi davomat: alohida verifikatsiya profili yoki karta ichidagi tab → TAB (egasining qarori, taklif qilingan variant) → sarlavhadagi rasm, ism, lavozim va holat allaqachon bor; ikkinchi profil §5.1 («asosiy munosabat») qarorining ikkinchi nusxasini yaratardi va bitta odam ikki kartaga bo'linardi
- [2026-09-19] Karta bo'limi (tab) va kalendar oyi qayerda saqlanadi → MANZILDA (ilgari `useState`) → verifikatsiya ro'yxatlari endi to'g'ridan-to'g'ri `?tab=attendance` ga havola qiladi; yon foydasi — «orqaga» ochilgan bo'limga qaytaradi
- [2026-09-19] Kalendar katagidagi reja qayerdan → kun yozuvidagi SURATDAN (`plan_start_at`), bugungi jadvaldan qayta hisoblanmaydi → aks holda jadval tahrirlansa o'tgan oyning kalendari ham o'zgarib ketardi; hosil qilingan (hali hisoblanmagan) katakda reja YO'Q
- [2026-09-19] Kalendarda ikkinchi rang palitrasi yasaladimi (Verifix'dagidek yashil «haqiqiy» lavha) → yo'q: katak FONI holatni aytadi va u tabel gridi bilan bitta xaritadan (`DAY_STATUS_LOOK`), haqiqiy vaqt esa oddiy qalin matn → ikkinchi palitra bir holatni ikki ekranda ikki xil rangda ko'rsatish xavfini tug'dirardi
- [2026-09-19] Tabel gridida ismni bosganda qaysi oy ochiladi → EKRANDAGI oy (qator kataklaridan olinadi), joriy oy emas → avgust tabelidan chiqib sentabr kalendarini ko'rish chalkashlik bo'lardi
- [2026-09-19] Xodim kartasidagi «O'tishlar»: kartochkali ro'yxatmi yoki jadvalmi → JADVAL (egasi, Verifix «Отметки» ekraniga qarab) → bu yerda qatorlar bir-biriga solishtiriladi (qaysi qurilma, qanday identifikatsiya); ustunlar tikka turganda ko'z pastga yuguradi
- [2026-09-19] Jadvalda «Lokatsiya» ustuni qayerdan → qurilmaning erkin matnli `LocationNote` idan (`AttendancePassDto.DeviceLocation`) → tizimda lokatsiya ma'lumotnomasi yo'q (SPEC-panel §A) va yaratilmadi ham; maydon faqat ko'rsatish uchun, filtr sifatida ishlatilmaydi
- [2026-09-19] `DeviceNamesAsync` → `DeviceLabelsAsync` (nom + joy + turi) → to'rtta ekran qurilma nomini shu bitta joydan o'qiydi; ikkinchi so'rov yozish o'sha jadvalga ikkinchi marta borish bo'lardi
- [2026-09-19] Yo'qlik so'rovlari: xodim ilovasi qanday (PWA / Telegram / native) va kirish → egasi PWA + HR bergan login-parolni tanladi; kamera va joylashuv ruxsati kerak, vazifasi keyin aniqlanadi → ⚠️ bu «xodimlar tizimga kirmaydi» degan boshlang'ich qoidani BEKOR QILADI: ~950 hisob, parol tiklash va sessiya masalalari 2-bosqichda hal qilinadi
- [2026-09-19] Tasdiqlangan so'rov davomatga qanday ta'sir qiladi → faqat «Kelmadi» → «Sababli» (yangi 9-holat); ishlangan daqiqa, kechikish va reja soati TEGILMAYDI → kengroq qoida HR kiritmagan raqamni tizim o'zi yasashiga olib kelardi; kelgan kun (kechikkan bo'lsa ham) oqlanmaydi
- [2026-09-19] «Sababli» panelga ham qo'shiladimi (ilgari A5 da «ettinchi holat qo'shilmasin» deb yozilgan edi) → HA, o'sha to'lqinda → aks holda kalendar «Sababli», panel esa «Kelmadi» deb ko'rsatib, modulning eng og'riqli xatosi takrorlanardi
- [2026-09-19] Qaror ruxsati `attendance.manage` ga qo'shiladimi → yo'q, alohida `attendance.requests.manage` → biri qurilma/jadval sozlash (texnik), ikkinchisi kadrlar qarori; HR menejer rolida ikkalasi ham bor, lekin ular ajratiladigan bo'lib qoldi
- [2026-09-19] Yarim kunlik so'rov (vaqt oralig'i) hisobga qanday kiradi → HOZIRCHA hech qanday: u ham butun kunlik kabi faqat «kelmadi» ni oqlaydi → vaqt bo'yicha hisob (§A2 ning 3-varianti) egasi tomonidan tanlanmadi; maydonlar saqlanadi va kelajakda ishlatiladi
- [2026-09-20] Tabel katta monitorda kichik ko'rinardi (jadval tabiiy kengligi ~1500 px) → jadval butun kenglikka cho'ziladi VA kataklar konteyner kengligiga qarab o'sadi (36 → 40 → 44 px, 1500/1750 px chegaralarida); ortiqcha joy avval jadvalga, qolgani xodim ustuniga → faqat cho'zish bo'sh joyni bitta ustunga berardi, jadvalning o'zi kichik bo'lib qolaverardi (egasi shuni aytdi). Media emas, KONTEYNER so'rovi — yon menyu yig'ilganda kenglik ±188 px o'zgaradi
- [2026-09-20] Yillik to'r: Verifix'dagidek katakda faqat SOAT saqlanadimi → yo'q, katakda VAQT ORALIG'I saqlanadi va ekranda soat bo'lib chiqadi → kechikish va erta ketish kunning boshlanish vaqtidan hisoblanadi; faqat soat saqlansa «Kechikkanlar» hisoboti va paneldagi «Kechikdi» ishlamay qolardi (egasiga aytilgan va qabul qilingan)
- [2026-09-20] Yillik to'r haftalik naqshni ALMASHTIRADIMI → yo'q, ustidagi ISTISNOLAR qatlami: katak yozilgan sana naqshdan ustun, yozilmagani naqshdan keladi → Verifix'da jadval yilga bog'langan va 31-dekabrdan keyin hamma «jadvalsiz» bo'lib qolardi; bu yechimda ekran bir xil ko'rinadi, lekin yil chegarasida hech narsa sinmaydi
- [2026-09-20] To'rga yozgandan keyin qaysi kunlar qayta hisoblanadi → faqat BUGUNGACHA bo'lganlari → kelajakdagi kunning saqlangan yozuvi yo'q (tabel uni o'qiyotganda hosil qiladi), ya'ni butun yilni to'ldirish 365 kunlik og'ir vazifa tug'dirmaydi
- [2026-09-20] Bayramlar ma'lumotnomasi qo'shiladimi → YO'Q (egasi: «bayram kunlariyam odatda bizda dam bo'lmaydi») → kerak bo'lgan kun to'rda qo'lda dam olish kuniga aylantiriladi; uzluksiz ishlaydigan jadvallar tegilmaydi
- [2026-09-21] Agent ro'yxatiga rasmi yo'q xodim kiradimi? → YO'Q, chetlatiladi → yuz terminaliga rasmsiz yozib bo'lmaydi; HR ekranida «Rasm yo'q» holati serverda hisoblanadi
- [2026-09-21] Shaxs raqami qayerdan? → PostgreSQL ketma-ketligi `device_person_no_seq`, format `9` + 8 xona → ikkita agent bir vaqtda so'raganda takror raqam bo'lmasin
- [2026-09-21] Vaqtinchalik skript yozgan `9`+hex raqamlar qayta raqamlanadimi? → YO'Q → faza tekshiruvi uzunlik va birinchi belgi bo'yicha; qayta raqamlash qurilmada ikki nusxa odam yaratardi
- [2026-09-21] «Hammasini sinxronlash» nimani qiladi? → faqat XATO qatorlarni qayta urinishga qo'yadi → egasi o'chirib qayta yozishni istamadi; yetishmagani baribir har tekshiruvda yoziladi
- [2026-09-21] Agent rasmni qayta siqadimi? → YO'Q → saqlangan rasm allaqachon 512×512 JPEG; chegaradan oshgani XATO sifatida ko'rinishi kerak
- [2026-09-21] Kelajakdagi ish kuni qanday ko'rsatiladi? → yangi `Planned` holati, katak bo'sh, qator saqlanmaydi → «kelmadi» ni bugun aytib bo'lmaydi; sanoqlar shishmasin
- [2026-09-21] Kelajakdagi tasdiqlangan so'rov-chi? → «Sababli» bo'lib ko'rinadi va saqlanadi → qaror bugun qabul qilingan, uni yashirishning ma'nosi yo'q
- [2026-09-21] Soat og'ishi qanday o'lchanadi? → agent terminalning joriy vaqtini yuboradi, server o'z vaqti bilan solishtiradi → o'tishdan olingan ayirmaga yetkazib berish kechikishi qo'shilib, soxta ogohlantirish berardi
- [2026-09-22] O'tish surati qayerdan? → agent olgan jonli kadr, terminalning `saveCapPic` sozlamasi EMAS → u butun qurilmaga tegishli va Verifix'ga boradigan hodisalarni o'zgartirardi; egasi Verifix uzilmasin dedi
- [2026-09-22] Eski (kechikib kelgan) o'tishga kadr biriktiriladimi? → YO'Q, faqat 6 soniyadan yangisiga → kadr o'qish lahzasida olinadi; eski o'tishga u boshqa odamni biriktirib qo'yardi
- [2026-09-22] Xodim ilovasida «Yangiliklar» bo'limi bo'ladimi? → YO'Q (egasi) → ma'lumoti yo'q va kerak emas; «To'lov» va xodim uchun «Hisobotlar» ham shu sababdan qo'shilmaydi
- [2026-09-22] Verifix'ning ma'lumot mantiqi ko'chiriladimi? → YO'Q, faqat TUZILISHI → kalendar ranglari bizning holatlarimizdan, so'rov turlari «yo'qlik turlari» ma'lumotnomasidan
- [2026-09-23] Joylashuvlar: doira belgisining dalil masofasi chegaragachami? → YO'Q, MARKAZGACHA (ko'pburchakda — chetgacha, ichida 0); `site_shape` qaysi ekanini aytadi → chegaragacha bo'lsa har bir qabul qilingan doira belgisida 0 yozilardi; shunda ikkala shaklda ham `distance_meters ≤ site_radius_meters` bajariladi
- [2026-09-23] Joylashuvlar: hech bir obyekt qabul qilmasa qaysi nom aytiladi? → zaxira ayirilgandan keyingi kaliti eng kichik obyekt va uning CHEGARASIGACHA masofasi → tanlov va xabar bitta tartibdan chiqsin
- [2026-09-23] Joylashuvlar: ko'pburchak o'lchamiga chegara bormi? → HA, har bir uch markazdan ≤ 5000 m (`obyekt.polygon_katta`) → aks holda doiraning 5 km shifti ko'pburchak orqali aylanib o'tilardi (SPEC da yo'q — egasi ko'rib chiqsin)
- [2026-09-23] Joylashuvlar: yopiq halqa (birinchi = oxirgi) tuzatiladimi? → YO'Q, rad etiladi (`obyekt.polygon_koordinata`); yuzasiz/bir chiziqdagi shakl — `obyekt.polygon_kesishadi` → jimgina «tuzatish» boshqa manbadagi xatoni yashirardi
- [2026-09-23] Joylashuvlar: doira radiusi berilmasa 200 qo'yiladimi? → YO'Q, yaratishda ham tahrirda ham majburiy (SPEC §C) → ko'pburchakdan doiraga o'tishda meros radius hech kim tanlamagan qiymat
- [2026-09-23] Joylashuvlar: tahrirda zaxira berilmasa? → hozirgi qiymati qoladi (yaratishda 50) → HR qo'ygan 0 jimgina 50 ga kengaymasin
- [2026-09-23] Joylashuvlar: o'qish ruxsati? → `attendance.view` YOKI `catalog.view` (ilgari faqat `catalog.view`) → SPEC §F «attendance.view ro'yxatni o'qiydi» va sahifa Verifikatsiya ostida
- [2026-09-23] Joylashuvlar: `shape`/`edge_buffer_meters`/`site_shape` ustun sukutlari qoladimi? → YO'Q, qatorlarni to'ldirgach olib tashlandi (`AddEmployeeMobileFlag` qoidasi) → modelga `HasDefaultValue(50)` qo'shilsa ataylab qo'yilgan 0 zaxira jimgina 50 bo'lardi
- [2026-09-23] Joylashuvlar: `WorkSiteShapes` Down ko'pburchak bor bazada? → to'xtaydi (RAISE), hech narsa o'zgarmaydi → aks holda ko'pburchak jimgina «markazdan 200 m» doiraga aylanardi
- [2026-09-23] Joylashuvlar: API da shakl qiymati? → «Circle»/«Polygon» (loyihadagi barcha enumlar kabi), bazada 'circle'/'polygon' → yagona istisno enum yozuvi bo'lmasin
- [2026-09-23] Joylashuvlar: who sees the page? → `attendance.view` opens sites read-only; create/edit/(de)activate only with `attendance.manage` → viewers need to see where a site is without being able to widen it
- [2026-09-23] Joylashuvlar: client-side polygon self-intersection check → warns (red outline + hint) but does not block save; the server is the authority → one rule, one owner
- [2026-09-23] Joylashuvlar: production CSP → `img-src` allows tile.openstreetmap.org + server.arcgisonline.com, `connect-src` allows nominatim.openstreetmap.org only → map is otherwise blank in prod; Nominatim receives only the search text
- [2026-09-27] Yillik to'r: «yilni naqshdan to'ldirish» qo'lda yozilgan kataklarni ham qayta yozadimi? → yo'q, faqat katagi YOZILMAGAN sanalar naqsh qiymati (tanaffus oynasi bilan) bilan yoziladi; hammasini qaytadan olish = avval «Yilni tozalash» → to'ldirish o'chirmaydigan amal bo'lib qoladi, bayram/qisqa kunlar bir bosishda yo'qolmaydi; hisob o'zgarmaydi (katak = naqsh), shu sababli qayta hisoblash navbatga qo'yilmaydi
- [2026-09-27] Yillik to'r: «yilni tozalash» uchun alohida endpoint kerakmi? → yo'q, yilning QO'LDA yozilgan sanalariga `PUT /schedules/{id}/dates` + `reset: true` (≤366 < 400 sana, bitta chaqiruv) → mavjud yo'l qayta hisoblashni ham o'zi navbatga qo'yadi (B qismi uni bo'laklayapti), ikkinchi o'chirish yo'li kerak emas
- [2026-09-27] Yillik to'r: sichqoncha bilan tortib tanlash telefonda ham ishlaydimi? → yo'q, faqat sichqoncha/qalam; barmoq bosishi bitta katakni almashtiradi → telefonda to'r gorizontal suriladi (SPEC §D), barmoq bilan tortish tanlov bo'lsa to'rni surib bo'lmasdi
- [2026-09-27] Jadvallar sahifasi `attendance.view` ga ochilgach «Kim uchun amal qiladi» kartasi kimga ko'rinadi? → faqat `employees.view` borlarga → kartadagi qidiruv `GET /api/employees` ni chaqiradi; davomat mas'uli kabi xodimlar ro'yxati yopiq odamga 403 bilan tugaydigan qidiruv ko'rsatilmaydi
- [2026-09-27] Ishdan ketgan xodim ogohlantirishida kechagi o'tish qanday yoziladi? → bugungi o'tish — faqat soat, boshqa kunniki — «dd.MM.yyyy HH:mm» (Toshkent) → oyna 24 soatlik, kechagi 23:40 «23:40» bo'lib hozirgi hodisadek o'qilardi
- [2026-09-27] Agent terminal binding: which serial is the token bound to? → a separate machine-written `terminal_serial` from `/ISAPI/System/deviceInfo`, recorded on first agent call (TOFU), cleared by token rotation; HR's `serial_number` is not used → the HR field is typed from the case label and differs from the ISAPI string; one typo would stop the agent for good
- [2026-09-27] Which Bonvi-range people may the agent delete? → only numbers this server allocated (`removeNumbers` in the roster); other `9xxxxxxxx` people are "foreign", left alone and counted on the device card → a dev and a prod agent on one terminal were deleting each other's people every minute; number collisions between two servers remain possible and are only made visible
- [2026-09-27] Mass-removal guard thresholds → hold when the roster is empty or removals > max(3, 20 % of managed people on the terminal); HR confirm button opens it for 10 minutes, no permanent switch → the owner's ">20 %" rule would block firing 1 of 3 people on a small terminal without the floor; a permanent switch would let the next empty roster wipe the terminal
- [2026-09-27] Which agent failures stop retries of the same photo ("rejected")? → terminal 4xx except 401/403/408/429, photo missing, photo > 200 KB; network errors, timeouts, 5xx, 429 stay retryable; 401/403 abort the cycle and back off 10 minutes → a rejected face was retried ~1440 times a day; a transient blip must not park a person until HR presses a button
- [2026-09-27] Clock verdict of a buffered pass: measured offset only if fresh? → the device's last measured offset regardless of age, copied onto the pass at ingest → buffered passes after an outage arrive before the next clock measurement, and a terminal clock does not drift minutes in hours; the same rule as `HasClockDrift`
- [2026-09-27] Terminal-unreachable threshold: finding said ~5 min, owner's alert said 10 min → one threshold, 10 minutes (`AttendanceRules.AgentSilenceMinutes`), for the devices-page state and the red alert → two thresholds would show a device as broken on one screen and fine on another; the agent polls every 60 s, so 10 missed polls is real
- [2026-09-27] Colour of the agent/terminal outage alert → red panel (danger), on Statistika and Qurilmalar → owner's explicit request; SPEC §8.4 reserved red for the former-employee alert, this is the second deliberate exception (an outage means departed people are not being removed from the door)
- [2026-09-27] Device health on Statistika: follow the page filters? → no, always company-wide and "now", in both day and period mode (`deviceHealth` on `/dashboard` and `/dashboard/trend`) → devices belong to no department and an outage is not a property of the chosen date
- [2026-09-27] Board → Statistika replacement: were the clock banner and "no device connected" state dropped on purpose? → no record of it (the 2026-09-17 "red block" entry is about the former-employee alert's "could not check" state) → restored on Statistika
- [2026-09-27] Enrollment version now includes the name (8-hex SHA-256 of the full name) → every already-enrolled person is rewritten once, in place, after deploy → harmless and one-off; a full name in the version would not fit the 200-char column
- [2026-09-27] Off-roster person that was never written to the terminal (no applied version) → shown as «O'chirilgan», not «Navbatda» → there is nothing on the door to remove; before it stayed "pending" forever
- [2026-09-27] Serialising person-number allocation: advisory lock or constraint? → partial unique index `ux_attendance_identities_managed_employee` + 409 `davomat.agent_royxat_band` for the loser; dev DB checked: no existing duplicates → no transaction plumbing through `IUnitOfWork`; the index also guards manual identity creation
- [2026-09-27] Live snapshot in a burst of passes → attached only to the newest fresh pass → the frame shows whoever stands at the terminal now; attaching it to ten passes both mislabelled nine people and exceeded the 2 MB ingest body limit
- [2026-09-27] Davomat audit B: ko'chirishdan oldingi kun qaysi lavozim/bo'lim bilan hisoblanadi (`Transfer` maydonni joyida almashtiradi, sana faqat hodisada) → shu SANADAGI joy, `Transfer`/`Promotion` hodisalaridan (`EmploymentPlacement`): har maydon alohida, oxirgi o'zgarishdan keyin — joriy qiymat, o'zgarishlar orasida — sanagacha kuchga kirganining «yangi»si, birinchisidan oldin — uning «eski»si (bo'sh bo'lsa joriy); jadval pog'onasi ham, kun surati ham shu BITTA qiymatdan; hodisalar hamma yuklovchida `Include` → mehnat buyruqlariga tegilmaydi; oldinga sanalangan ko'chirish ham o'z sanasidan
- [2026-09-27] Davomat audit B: bugungi smena boshlanmasidan oldin o'tishsiz ish kuni (2026-09-17 dagi «03:10 da bugungi ish kuni Kelmadi bo'lib saqlanadi» qarori BEKOR) → `Planned`, qator yo'q; qoida panelning «Hali boshlanmagan»i bilan bitta (`DashboardStatusRule.HasNotStarted`, reja boshlanishidan, yon berishsiz) → tabel, ilova va hisobotlar ertalab hammani «Kelmadi» ko'rsatardi, panel esa «boshlanmagan»; tasdiqlangan so'rov baribir «Sababli» bo'lib saqlanadi
- [2026-09-27] Davomat audit B: reja boshlangach «Kelmadi» qatorini kim yozadi → yangi `attendance:started-shifts` (har 15 daqiqa, :05 dan), kecha + bugun, FAQAT yo'q qatorni qo'shadi → iflos navbat o'tishsiz kunni ko'rmaydi; bor qatorni har 15 daqiqada qayta yozish bazani aylantirib, tabeldagi «oxirgi hisoblash»ni ma'nosiz qilardi
- [2026-09-27] Davomat audit B: orqaga sanali ishdan bo'shatish/qabul/ko'chirish tabelga qanday yetadi (mehnat buyruqlariga tegmaslik sharti bilan) → `attendance:employment-changes` (har 15 daqiqa, :12 dan): yashirin `attendance.employmentChangesCheckedAt` dan (10 daqiqa ustma-ust) beri yaratilgan/o'zgargan/o'chirilgan munosabat va hodisalarni topib, xodimni ta'sir sanasidan bugungacha (≤ 92 kun) qayta hisoblaydi; munosabat qatori yoki mavjud hodisa o'zgarsa — ishga kirgan sanadan (eski sana saqlanmaydi, qaysi maydon o'zgargani noma'lum); birinchi yurish oxirgi chorakni ko'radi → vaqt ustunlarida indeks yo'q, lekin jadval kichik — migratsiya qo'shilmadi
- [2026-09-27] Davomat audit B: yillik to'r tahriridan keyin nima qayta hisoblanadi (2026-09-20 dagi «faqat bugungacha» qarori BEKOR) → `attendance:schedule-dates`: faqat jadval tushadigan xodimlar (hal qiluvchi bilan, sanama-sana), faqat o'zgargan sanalar ±1 qo'shni bilan, kelajak sanalar ham → «butun yilni to'ldirish» 270 kun × 540 odamni qayta yozardi, tungi smenaga aylangan kun ertangi chiqishni ikki kunda sanardi, kelajakdagi «Sababli» qator esa dam olish kunida qolib ketardi
- [2026-09-27] Davomat audit B: yagona yozuvchi katta chaqiruvni qanday ko'taradi → sanalar ≤ 92 kunlik oynalarga, xodimlar 100 talik to'plamlarga, har saqlangan to'plam kuzatuvdan unutiladi → 2 GB li serverda xotira bitta to'plam × bitta oyna
- [2026-09-27] Davomat audit B: so'rov tasdiqlangach/bekor qilingach kun qachon yangilanadi → shu so'rovning o'zida, javobdan oldin (bitta xodim, ≤ 92 kun); uzunroq davr — navbatga → veb kartani javob kelishi bilan qayta so'raydi, Hangfire orqali karta bir daqiqagacha «Kelmadi» ko'rsatardi
- [2026-09-27] Davomat: yo'nalishi NOMA'LUM ketma-ket o'qishlar (bitta umumiy terminal) birlashtiriladimi → YO'Q, 20 soniya farq bilan ham navbat bilan kirish/chiqish (egasining so'zi: «bitta terminal bo'lsa mayli kirish chiqish bo'laversin») → egasining qarori; `PassPairingTests.Nomalum_yonalish_hech_narsani_yutmaydi` shuni qotiradi
- [2026-09-27] Davomat: yo'nalishi MA'LUM ketma-ket ikki kirish yoki ikki chiqish (alohida kirish/chiqish terminallari, telefon) → vaqtdan qat'i nazar bitta voqea: birinchi kirish, OXIRGI chiqish; tashlangani jurnalda qoladi, `ignored_pass_count` ga tushadi; 60 soniyalik chegara kun hisobida endi yo'q (faqat telefon belgisida) → egasining so'zi: «ikkita kirish orqama-orqa bo'lmaydi»; SPEC §5.5 2-qadam va §5.7 qatorlari shunga o'zgartirildi, eski testlar ataylab yangilandi
- [2026-09-27] Davomat, OCHIQ SAVOL: ma'lum va noma'lum yo'nalish aralash (telefonda «Kirish» 08:55, keyin umumiy terminal 09:00, 18:00) → hozircha ESKI xatti-harakat: navbat ma'lum yo'nalishdan davom etadi (09:00 chiqish, 18:00 kirish — ishlangan 5, «Chiqish yo'q») → egasi hal qilmagan; `UnknownDirectionEdgeCaseTests.Aralash_yonalish_joriy_xatti_harakati_qotirilgan` shuni qotiradi, qaror kelsa ataylab o'zgartiriladi
- [2026-09-27] Davomat: hech bir kun oynasiga tushmagan o'tish (09:00–18:00 da seshanba 00:20) → kechagi kunning chiqishi — agar u kirish bo'lmasa, kecha ish kuni va oynasi tugagan, bugungi oyna (reja − 6 soat) hali boshlanmagan va kecha OCHIQ kirish bilan tugagan bo'lsa; aks holda o'z sanasi → egasi «oldingi kunga» variantini tanladi; bugun dam olish kuni bo'lsa chegara — kechagi oyna boshidan bir sutka (aks holda dam olish kunining kechki o'tishi ham kechaga yopishardi); «ochiqmi» faqat kechaning `local_date ≥ kecha` oyna o'tishlari va unga qo'shilgan kech chiqishlardan — natija ±1 kunlik yuklashda qaysi oraliq hisoblanayotganiga bog'liq bo'lmasin, aks holda bitta o'tish ikki saqlangan kunda sanalardi
- [2026-09-27] Davomat audit B: `GET /schedules/resolve` kuni qayerdan → tabel ishlatgan manbadan: yillik to'r katagi bo'lsa katak, aks holda naqsh; yangi `daySource` (`Date`/`Pattern`) va ekranda kichik belgi; bo'lim/lavozim ham shu sanadagi → ilgari to'rda dam olishga aylantirilgan kunda ekran «09:00–18:00», tabel «Dam olish» derdi
- [2026-09-27] Audit #4: while a login is locked, should the password still be verified and a wrong one answered generically? → NO — the lock is keyed by (typed login, source IP), checked before any lookup, and while locked the answer is `xodim.bloklangan` for every password and for unknown numbers alike → verifying during the lock turns it into a password oracle (correct password ≠ generic answer); keying by login+IP already removes the existence leak and the targeted-lockout vector the finding named
- [2026-09-27] Audit #4/#5: distributed brute force from many IPs on one account? → accepted risk, bounded by 5 tries per login per IP per 15 min + 60 logins/min per IP → an account-wide lock is exactly the griefing vector the finding asked to remove
- [2026-09-27] Audit #9: grace window for a replayed refresh token → 60 s, same successor pair returned only if the successor is still unused → longer windows widen the replay of a stolen token; the pair is kept in process memory only
- [2026-09-27] Audit #10: code for a check-in by an employee who no longer works today → `davomat.mobil_ruxsat_yoq` (403) → SPEC §E fixes the four check-in codes; «you may not mark» is the true answer
- [2026-09-27] Audit #6: HR cancelling an already-decided request → unchanged (HR may still cancel an approved request, recompute follows) → the finding is about the employee path; only the employee path was restricted to Pending
- [2026-09-27] Audit #7: where to log rejected check-ins → in the API endpoint (`MeEndpoints`), not the handler → the Application layer must not depend on logging (architecture test); the handler returns the code and values, the endpoint logs them
- [2026-09-27] Audit #8: which cell does a mark belong to in the app → the stored plan snapshot of `attendance_days` + `PassAttribution` (same rule as the timesheet); an un-computed day has no plan, so its marks stay on their calendar date → re-resolving schedules in the app read model would be a second copy of the rule
- [2026-09-27] Audit #16: `checkIn.occurredAt` offset → UTC (`+00:00`), like every other instant of the employee API; the app never reads the offset → the pass stores UTC; the old doc comment was right, a test now pins it
- [2026-09-27] App dev API access → Vite `/api` proxy (`VITE_API_PROXY`, compose: `http://api:8080`), no absolute `VITE_API_URL` → same-origin like production; CORS allows only 7173
- [2026-09-27] Owner decision: how are employee-app accounts created? → automatically for every non-deleted employee (Hangfire job at startup + every 15 min, plus ensure-on-read on the «Ilova» segment); no manual «Hisob ochish» → owner rejected per-employee manual opening; Employee/Employment/recruiting code left untouched because main diverged there
- [2026-09-27] Owner decision: what is the login? → the PRIMARY profile phone (`employees.phone`), looked up at sign-in; `employee_accounts.login` dropped → one source of truth, a profile edit takes effect immediately
- [2026-09-27] Owner decision: what password, and can HR see it? → 6 random digits (CSPRNG, trivially weak ones rejected), HR can view it any time via an encrypted copy (`ISecretProtector`), every view audited (`AuditAction.Export`, `password_viewed`) → owner chose "always visible"; audit is the cheap safeguard
- [2026-09-27] Owner decision: who changes the password? → only HR (regenerate 6 digits or a custom 6–64 chars); forced first-login change, `/api/me` password gate, `POST /api/employee-auth/password` and the app's password screens removed → employees must not change it
- [2026-09-27] Owner decision: how is an account switched off? → only by termination (no employment today) — login, refresh and every `/api/me/*` call refused at once with `xodim.hisob_faol_emas`; «Hisobni yopish» and `is_active` removed; rehire works again with the same phone/password → termination is the single off switch
- [2026-09-27] Shared or missing profile phone → sign-in refused with the generic `xodim.notogri_malumot`, reason shown only in HR's «Ilova» segment; the account and password still exist → no oracle for outsiders, and the fix is one profile edit
- [2026-09-27] Existing dev accounts after the migration → kept with their hash, `password_protected` NULL (UI: «Parolni ko‘rsatib bo‘lmaydi» + «Yangi parol yasash») → the plain value cannot be recovered and silently changing working passwords would lock people out
- [2026-09-27] `failed_login_attempts` / `locked_until` → dropped with the password-change endpoint they served → the login lock is per (login, IP) in `EmployeeLoginThrottle` since the audit; dead columns would mislead
- [2026-09-27] Custom HR password with leading/trailing spaces → trimmed before hashing, length checked after trimming → HR sees the stored value when revealing, so what is shown is exactly what works
- [2026-09-27] Does HR setting a password end existing sessions? → yes, all refresh tokens revoked (as the old reset did) → a new password is normally given because the old one leaked or was forgotten
- [2026-09-27] Employee token on HR «Ilova» endpoints → 401 (audience mismatch), view-only HR user → 403 → existing isolation rule; the RBAC test asserts both
- [2026-09-27] Check-in by a terminated employee → now `xodim.hisob_faol_emas` from the `/api/me` access gate (was `davomat.mobil_ruxsat_yoq` from the handler) → the gate runs first for every `/api/me/*` call; the handler check stays as a second guard
