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
- [2026-09-18] Tashqi API: SPEC §5 «return the **active** employments» nimani
  anglatadi → YAKUNLANMAGAN munosabatlar (`Status != Terminated`), yangisidan
  eskisiga → aks holda bitta odamning 2011-yildagi tugagan shartnomasi ham
  javobga tushib, «hozir kim qayerda ishlaydi» degan savolga shovqin
  qo'shardi. ⚠️ Oqibati: ishdan KETGAN xodimda `employments` BO'SH bo'ladi.
  Ma'lumot yo'qolmaydi — yuqori darajadagi `status`, `hireDate` va
  `terminationDate` ASOSIY munosabatdan olinadi (u tugagan bo'lsa ham) va
  «asosiy» qoidasi ichki ro'yxat bilan aynan bir xil.
- [2026-09-18] Tashqi API `updatedAt` faqat `employees.updated_at` dan
  olinsinmi → YO'Q, xodim kartasi VA uning mehnat munosabatlaridan eng
  keyingisi → xodimni boshqa bo'limga ko'chirish `employments` ni
  o'zgartiradi, kartaga esa tegmaydi (alohida agregat). Faqat kartaga
  qarasak, `?updatedSince=` bilan sinxronlanayotgan iste'molchi ko'chirishni
  HECH QACHON ko'rmasdi — hech qanday xatosiz, jimgina eskirgan bo'lim bilan
  qolardi. Narxi: ro'yxat so'rovida munosabatlar bo'yicha korrelyatsiyalangan
  `MAX(...)` ichki so'rovi (2000+ qatorda bir necha ms).
- [2026-09-18] `UseRateLimiter()` avtorizatsiyadan OLDINGA ko'chirildi (ilgari
  keyin edi) → aks holda 401/403 bilan rad etilgan so'rovlar cheklovga umuman
  sanalmasdi, ya'ni tashqi API kalitini tanlab ko'rish cheksiz bo'lardi.
  Foydalanuvchi bo'yicha bo'linish buzilmadi: JWT `UseAuthentication` da
  allaqachon o'qilgan. Kalit prefiksi esa `UseBonviApiKeyAuthentication`
  bilan cheklovdan oldin aniqlanadi (natija so'rov davomida keshlanadi —
  baza so'rovi ikki marta ketmaydi). Qo'riqchi test:
  `PublicApiTests.Kalitsiz_urinishlar_ham_sanaladi`.
- [2026-09-18] Ommaviy `/employees/{id}/photo` da «xodim yo'q» va «rasmi yo'q»
  BIRLASHTIRILDI (ikkalasi `tashqi.surat_yoq`) → farqli javob xodimning
  mavjudligini tasdiqlab, identifikatorlarni tanlab ko'rish uchun arzon
  tekshirgich bo'lib qolardi. Xodim borligini bilish uchun `/employees/{id}`
  bor va u o'z ruxsati bilan cheklangan (kalit).
- [2026-09-18] `/employees/deletions` sahifalanmaydi (SPEC §4), lekin bitta
  javobda eng ko'pi 5000 yozuv beradi → cheksiz javob vaqt o'tib o'sib
  ketardi; `?since=` aynan shu uchun bor va tartib `deletedAt` bo'yicha
  o'sishda, ya'ni davom ettirish mumkin.
- [2026-09-18] Kalitlar jadvalida «oxirgi ishlatilgan» SPEC §6 aytganidek
  nisbiy («2 soat oldin») emas, ANIQ sana-vaqt bo'lib chiqadi (Toshkent,
  `formatDateTime`) → loyihada nisbiy vaqt formatlagichi umuman yo'q va uni
  shu bitta ustun uchun yaratish 4 tilda «oldin/kun/soat» qoidalarini
  qo'lda yozishni talab qilardi (`Intl.RelativeTimeFormat` esa `uz-Latn`/
  `uz-Cyrl` teglarini to'liq bilmaydi — `formatNumber` dagi bilan bir xil
  muammo). Aniq vaqt bu ustunda foydaliroq ham: u xavfsizlik tekshiruvida
  o'qiladi va IP bilan yonma-yon turadi. Narxi: «yaqinda ishlatilganmi?»
  degan savolga ko'z bilan javob berish bir qadam sekinroq.
- [2026-09-24] Where do the candidate's HR notes go on hire (the employee has no
  notes field)? → Appended to the competency profile's «Nomzodlik qo'shimcha
  ma'lumotlari» text after the questionnaire answers: card note, then stage
  conclusions and the note feed in date order; the column was widened to 10000.
  The notes are copied, and the candidate keeps its own. → The owner had already
  chosen this field for leftover questionnaire answers, and a separate field
  would have needed new UI for the same kind of read-only text.
- [2026-09-26] Should reserved candidates also be hidden under the "contact due
  today" filter? → No, that filter brings them back; only the default list hides
  them. → Notifications count reserved candidates too; hiding them would make the
  notification count larger than the list.
- [2026-09-26] One table for reserved candidates and reserved employees on the
  Reserve page? → A "Candidates | Employees" switch, candidates by default. → Their
  columns and actions differ, and it makes clear a candidate is not an employee.
- [2026-09-27] Should correcting an employment record without changing its
  amount ever change the current salary? → Never. An amount correction
  updates the current salary only when the record is the salary source AND
  the card was showing exactly the old amount. → For employees imported from
  Excel the current salary comes from «91 Умумий» and need not match the
  history; a silent salary change is worse than a card that needs one manual fix.
- [2026-09-27] Is a transfer that kept the same salary a salary decision? → No.
  Such a transfer only snapshots the salary, so it is not a salary source and
  correcting its date needs only `employment.correct`. An additional-duty pay
  amount is a pay decision (its date still needs `employment.salary`) but
  never a salary source. → `Employment.Transfer` always writes `toSalary`;
  treating that copy as a decision blocked correct-only roles and let the
  snapshot shadow later salary corrections.
- [2026-09-27] Does completing probation with a new position/department need
  `employment.transfer`? → Yes. The rate stays under probation completion. →
  This mirrors the salary rule; the owner split transfers (HR manager) from
  probation completion (head of HR), and the rate has no action of its own
  (same as at hire).
- [2026-09-27] Should manual candidate registration be blocked on a duplicate
  phone? → No. The form warns (new `GET /api/recruiting/candidates/phone-matches`,
  all statuses) and saving stays allowed. → SPEC-qayta-ariza: the owner chose
  no barrier for manual entry. Since 2026-09-26 reserve candidates are also
  missing from the list search, so search alone no longer reveals duplicates.
- [2026-09-27] Should reserve candidates show in the «Nomzodlar» list while
  searching? → No. A notice says how many matches are in Rezerv and links
  there. → The owner explicitly wants reserve candidates only in Rezerv.
- [2026-09-27] Can a role editor change a role with the same rights as their
  own? → Not when that role has `roles.manage` or `users.manage`: only
  super_admin can (`rol.teng_huquqli`). → This is the role-side twin of the
  existing users-side peer rule.
- [2026-09-27] What happens to an open notification stream when the account's
  access changes? → The stream ends and the client reconnects with fresh
  access (or gets 401). → Authentication runs once per connection, and the
  stream lives for hours.
- [2026-09-27] Multiple check materials: a new entity or employee documents
  with a special type? → A new `EmployeeCheckFile` (`employee_check_files`),
  with no type and soft delete. → A document type would have to be found by
  its catalog NAME, and HR can rename it (the «Номзодлик ҳужжатлари» lookup
  has the same fragility). Materials belong only to the «Tekshiruvlar» card.
- [2026-09-27] What happens to the existing single mehnat.uz screenshot? → It
  is copied into the list as the first material. The old
  `employees.mehnat_uz_screenshot_*` columns are NOT dropped in this release
  (expand/contract). → Rollback is done by redeploying the previous image, and
  that code reads those columns. Drop them in a later release with a
  hand-written migration.
- [2026-09-27] Where do the materials sit? → As their own block at the bottom
  of the «Tekshiruvlar» card, shared by all checks, not inside the mehnat.uz
  block. → The user's screenshot showed a debt-check page, not a mehnat.uz
  profile, so the evidence spans every check.
- [2026-09-27] "Visible only when clicked": expand inline or open a dialog? →
  A dialog with previous/next (and ←/→). The list shows name, date and size
  only. → The screenshot's value is its readable text, which needs full size;
  the old zoom already used a dialog.
- [2026-09-27] HR asked for "one record containing questionnaire + council
  conclusion + test answers" (and similar for rejected and reserve). What
  does that mean? → Document types whose NAME lists the contents:
  «Анкета + кенгаш хулосаси + тест жавоблари» and «Анкета + кенгаш хулосаси»
  («Анкета» already existed). They are added by migration and in the seed,
  and are not required. → The paper candidacy folder is scanned as one PDF,
  and HR wants to know what is inside without opening it. The owner chose
  combined names over multi-select. A type choice for candidate files
  (rejected and reserve) is deferred.
- [2026-09-27] Additional-duty pay: overwrite or add? → Add. The base
  salary stays untouched. The card shows each active duty and a total
  (salary + active duty pay). Duties get an end date (`duty_end_date`); an
  ended duty drops out of the total. → Owner's choice ("kartada alohida
  qo'shilsin"). Folding the pay into the salary field would corrupt the
  salary history when a duty ends.
- [2026-09-27] Who sees duty pay? → A separate permission,
  `salary.extra.view`, as an extra toggle in the role editor. super_admin
  gets it automatically; on existing databases main_boss does NOT get it
  automatically (the owner ticks it in «Rollar»). New databases give it to
  main_boss by default and not to hr_manager. Without it: duties are
  visible, amounts and the total are hidden, and the user cannot send,
  correct, date-move or end a paid duty. → The owner: "even HR managers may
  not get access; superadmin and the main director only". The existing rule
  is that new permissions never reach an existing role silently.
- [2026-09-27] Can `salary.extra.view` work without `salary.view`? → No.
  Duty pay is visible and editable only with both. → Duty pay is added to
  the salary. With the extra permission alone, the card, timeline and
  orders list showed it inconsistently (review finding).
- [2026-09-27] When is the card "Jami" shown? → Only when a base salary
  exists and every active duty pay is in the salary's currency; otherwise
  it is null (the lines are still listed). → A total made of extras alone
  under «Oklad: kiritilmagan», or one that silently skips another
  currency, misleads.
- [2026-09-27] Do duties outlive a termination? → No. `Terminate` and
  `ImportTermination` set `duty_end_date` = the termination date on open
  duties, and the card shows no active duties for a terminated employment.
- [2026-09-29] Vacancy titles catalog merged into vacancies: what happens to
  names that have no vacancy yet? → They become CLOSED vacancies (opened =
  closed = the title's creation day, Tashkent). → Opening them would publish
  them to candidates in the Telegram bot immediately; closed keeps behaviour
  unchanged and HR reopens with one click. Inactive titles migrate too
  (they were visible in the catalog); soft-deleted titles do not.
- [2026-09-29] Can two vacancies share a name? → No, case- and
  whitespace-insensitive, closed ones included (HR reopens the old one
  instead — keeps candidate history in one row). Enforced in the use case,
  not by a DB index: legacy data may already hold duplicates (several
  vacancies per title were allowed), which a unique index would reject
  mid-migration. Reopen only checks OPEN duplicates so legacy closed twins
  do not block each other.
- [2026-09-29] Closing a vacancy → one click, closed date = today, no reason
  prompt. → Owner asked for the simplest form; a reason can still go into
  the note.
- [2026-09-29] «Umumiy anketa» (general pool) can it be closed? → No: close
  and delete return 409 `vakansiya.tizim_vakansiyasi`, the UI shows a lock,
  and startup reopens it if found closed. → Prod had it closed by HR on
  2026-09-11; every bot «Anketa to'ldirish» submission failed for 18 days
  (67 rejections in 5 days of logs). If the owner ever wants to stop
  general applications, the bot button must be hidden instead.
- [2026-09-29] Chosen vacancy closed while the candidate filled the form →
  the application goes to the general pool and the candidate is told so.
  → 24h conversations + one-click close make this common; losing a finished
  22-step form is worse than filing it under the general pool.
- [2026-09-29] Bot retries a submission only when the request surely did not
  reach the API (connection/name-resolution error, 503), ~2 min total. →
  Retrying on timeouts could create a duplicate candidate.
- [2026-09-29] Foreign Telegram number at the phone step → the candidate may
  TYPE an Uzbek number (the only case where typing is accepted). → The server
  accepts only +998; otherwise such a person could never apply.
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
- [2026-09-27] Xodim kartasidagi «Qurilmalar» kartasi («Qayta yozish») kerakmi? → yo'q, olib tashlandi (egasi) → Qurilmalar sahifasida xato bergan xodimlar rasmi bilan ko'rinadi va «Hammasini sinxronlash» bor; rasm/ism o'zgarsa agent o'zi qayta yozadi. Server endpointlari qoldi (faqat manage), ekran chaqirmaydi.
- [2026-09-30] verification → main: davomat ruxsatlari MAVJUD bazadagi HR menejeriga beriladimi? → yo'q; seeder oddiy rolga tegmaydi, egasi «Rollar» dan beradi (SPEC-rollar-va-audit §14.3). Verification'dagi «urug' HR menejeriga davomatni qaytaradi» testi teskarisini isbotlaydigan testga aylantirildi → 2026-09-26 dagi egasining qarori keyinroq va umumiy; prod'da bo'lim egasi ruxsat bergandagina ko'rinadi.
- [2026-09-30] Rollar muharririda davomat bo'limi qanday? → SPEC §14.2 bo'yicha: Ko'rish = `attendance.view`, O'zgartirish += `attendance.requests.manage`, To'liq += `attendance.manage`; nomi menyudagidek «Verifikatsiya» (SPEC'da «Davomat») → muharrirdagi bo'lim nomi va ikonasi menyudagi bo'lim bilan bir xil bo'lishi kerak (§3.3 dagi boshqa bo'limlar ham shunday).
- [2026-09-30] Verification testlaridagi «admin tokeniga o'ylab topilgan ruxsatlar» yordamchilari → `TestAccounts.TokenAsync` ga o'tkazildi → 2026-09-26 dan ruxsatlar har so'rovda bazadan o'qiladi, eski usulda testlar administrator bo'lib o'tib ketardi (15 ta RBAC testi shu sababdan yiqilgan).
- [2026-09-30] Birlashtirishda main olib tashlagan «Vakansiya nomlari» (menyu, ma'lumotnoma, tarjimalar) verification tomondan qaytmaydi → 2026-09-29 dagi `VakansiyaNomiVakansiyaningOzida` migratsiyasi jadvalni o'chirgan.
- [2026-09-30] Image rollback'da eski image `employees.mobile_check_in_allowed` ni bilmay xodim qo'shishda 500 berardi → yangi migratsiya `EmployeeMobileFlagSukuti` bazaga `DEFAULT false` qo'yadi (model/suratga tegmaydi) → egasining talabi: qaytish oson bo'lsin; qo'lda SQL qadami stress paytida unutilardi. Repetitsiyada eski image sukut bilan xodim qo'shdi (200).
- [2026-09-30] `/api/me/*` tezlik cheklovi IP bo'yicha bo'lib qolgan edi (main `UseRateLimiter` ni avtorizatsiyadan oldinga ko'chirgan, xodim sxemasi esa faqat avtorizatsiyada o'qilardi) → `UseBonviEmployeeAuthentication("/api/me")` cheklovdan oldin xodim tokenini o'qiydi (tashqi API kaliti bilan bir xil naqsh) → zavoddagi bitta IP dagi xodimlar bir-birini va HR ni 429 bilan to'smasin. Test: `Ilova_sorovlari_xodim_boyicha_cheklanadi_IP_boyicha_emas`.
- [2026-09-30] «Yo'qlik turlari» ma'lumotnomasi `attendance.view` siz foydalanuvchiga ko'rinadimi? → yo'q (menyuda `permission`, guruhda `filterChildrenByPermission`) → deploy'dan keyin HR menejerining ekrani o'zgarmasin; bo'lim egasi davomatni yoqqanda paydo bo'ladi. Server endpointi `catalog.*` ostida qoldi.
- [2026-09-30] Xodim ilovasi shu deploy bilan internetga ochiladimi? → YO'Q (egasi): `EmployeeApp:Enabled`, prod'da sukut `false` — `/api/employee-auth`, `/api/me` marshrutlari umuman ro'yxatga olinmaydi; HR «Ilova» bo'limi va hisoblar ishlaydi → xavfsizlik tekshiruvi: 6 xonali PIN, hisob har xodimga deploy'dayoq ochiladi, (telefon, IP) blokini ko'p IP'dan aylanib o'tish mumkin, PBKDF2 login oqimi 2 GB serverni band qiladi. Ochishdan oldin: HR bermagan hisob kira olmasin, telefon bo'yicha umumiy xato chegarasi, anonim so'rov hajmi cheklovi, xeshlashga umumiy navbat.
- [2026-10-01] MCP: JSHSHIR «pasport ma'lumoti» hisoblanadimi? → ha, yopiq (egasi faqat «pasport» dedi) → JSHSHIR pasportda yozilgan shaxsiy identifikator; tahlil uchun kerak emas, sizsa xavfi pasport raqami bilan bir xil
- [2026-10-01] MCP: telefon va manzil ochiqmi? → ha (2026-09-24 dagi taklif o'rniga egasining yangi qarori: «qolgan istalgan ma'lumot») → faqat pasport va fayllar yopiq
- [2026-10-01] MCP: hujjatlar haqida nima ko'rinadi? → faqat hujjat TURI va yuklangan sanasi; fayl, fayl nomi va hujjat izohi yopiq (baza ustun darajasida) → nom va izohda pasport raqami yozilgan bo'lishi mumkin; «hujjati yetishmaydiganlar» tahlili esa tur bilan ishlaydi
- [2026-10-01] MCP: `audit_logs` AI'ga ochiqmi? → yo'q → eski/yangi qiymatlarda pasport va JSHSHIR o'zgarishlari bor
- [2026-10-01] MCP kimga ochiq? → `mcp.connect` ruxsati bor rol («Rollar» → «AI orqali o'qish (MCP)»); super administrator avtomatik, bosh rahbar faqat YANGI bazada, HR menejeri yo'q → mavjud bazada oddiy rollarga seeder tegmaydi (2026-09-26 qoidasi); egasi o'zi beradi
- [2026-10-01] MCP deploy'dan keyin yoqilganmi? → yo'q, «Tizim sozlamalari» dagi o'chirgich sukut bo'yicha O'CHIQ (`mcp.enabled` = false) → ma'lumotni tashqi AI'ga chiqarish egasining ongli qarori bo'lsin
- [2026-10-01] MCP OAuth kirish sahifasi uchun tezlik cheklovi → alohida `mcp-authorize` (IP bo'yicha 10/daqiqa), veb-kirishning `login` (5/daqiqa) siyosati bilan umumiy EMAS; hisob qulfi (5 xato → 15 daqiqa) esa umumiy → bitta ofis IP ortidagi HR'lar va rahbar bir-birini 429 bilan to'smasin; parol tanlashga asosiy to'siq — hisob qulfi
- [2026-10-01] MCP jurnali va tokenlari qaysi ulanish bilan yoziladi? → alohida `McpDatabase.CreateStoreContext` (interceptorlarsiz) → so'rovning o'z konteksti `DataChangeInterceptor` orqali har yozuvda bildirishnoma oqimlarini uyg'otardi; AI daqiqasiga o'nlab chaqiruv qiladi
- [2026-10-01] MCP sozlamalar bo'limi: «Qayta tekshirish» qachon chiqadi? → faqat `Faulty` holatda → `NotConfigured` da sozlama ishga tushishda o'qiladi (tekshiruv hech narsa bermaydi), `On`/`Off` da tekshiradigan narsa yo'q
- [2026-10-01] MCP o'chirgichi server sozlanmagan/nosoz bo'lsa? → nofaol, lekin SAQLANGAN qiymatni ko'rsatadi; `Pending` da faol → tanlov serverda baribir saqlanadi va server tayyor bo'lgach kuchga kiradi
- [2026-10-01] Rollar muharririda `mcp.connect` uchun izoh (hint)? → yo'q, faqat yorliq → «Alohida ruxsatlar» qatorlarida izoh maydoni hozir umuman yo'q; bitta qator uchun muharrirni o'zgartirish shart emas
- [2026-10-01] Jurnaldagi vosita nomlari yorlig'i qayerda? → `MCP_TOOL_NAMES` (`mcp-api.ts`) + `check-locales.mjs` dagi manba, komponentdagi xarita emas → `role-keys.ts` bilan bir xil naqsh: tarjimasi unutilgan vosita tekshiruvni yiqitadi; noma'lum nom xom chiqadi
- [2026-10-01] MCP qaytish manzillari — xost bo'yicha yoki aniq manzil? → ANIQ manzil (claude.ai/claude.com `/api/mcp/auth_callback`, chatgpt.com `/connector_platform_oauth_redirect`), `Mcp:AllowedRedirectUris` bilan kengaytiriladi; loopback faqat Development/Testing'da → xavfsizlik tekshiruvi: DCR anonim, xost tekshiruvi uchinchi tomon saytidagi har qanday ochiq yo'naltirishga bog'liq bo'lib qolardi; provayder manzilni o'zgartirsa ulanish aniq xato bilan to'xtaydi
- [2026-10-01] Parol tiklash / «barcha sessiyalardan chiqish» / hisobni faolsizlantirish MCP ulanishlarini ham uzadimi? → ha (`IMcpGrantRevoker`) → sizib chiqqan parol bilan ochilgan AI ulanishi odatiy chora (parolni tiklash) dan keyin ham 90 kun ishlab turardi; rol ruxsati olinganda esa ulanish faqat bloklanadi (qayta berilsa tiklanadi) — SPEC §12
- [2026-10-01] MCP chiqishida qiymat bo'yicha yashirish → JSHSHIR (14 raqam) va pasport raqamiga (2 bosh harf + 7 raqam) o'xshagan matn bo'laklari «[yashirilgan]» → erkin matn (izoh, xulosa) ochiq, lekin HR u yerga pasport raqamini yozgan bo'lishi mumkin; kamdan-kam soxta moslik (14 xonali boshqa raqam) qabul qilinadi
- [2026-10-01] Hisob qulfi hisoblagichi parallel urinishlarda atomar emas → MCP uchun alohida tuzatilmadi, SPEC §12 ga yozildi → bu veb-kirishdagi mavjud xatti-harakat; ikkalasi birga tuzatilishi kerak, MCP sahifasi esa IP bo'yicha 10/daqiqa bilan cheklangan
- [2026-10-05] «Umumiy anketa» yopiladimi? → HA, boshqa vakansiyalar kabi ochiladi/yopiladi (egasi: «qaysi yopiq bo'lsa anketa topshirolmasin»); o'chirish taqiqlangan holicha qoladi; API ishga tushganda uni endi QAYTA OCHMAYDI → 2026-09-29 dagi «yopilmaydi» qarori bekor. O'sha safargi xato (bot 22 savoldan keyin «yuborilmadi» degan) takrorlanmasligi uchun bot «Anketa to'ldirish» bosilishi bilanoq `GET /api/recruiting/vacancies/general-pool` ni so'raydi va yopiq bo'lsa aytadi; ekran yopishdan oldin ogohlantiradi
- [2026-10-05] Tanlangan vakansiya anketa to'ldirilayotganda yopilsa — ariza «Umumiy anketa»ga o'tadimi? → faqat «Umumiy anketa» OCHIQ bo'lsa (ariza ochiq joyga tushadi); u ham yopiq bo'lsa ariza qabul qilinmaydi va nomzodga aniq sabab aytiladi → 2026-09-29 dagi «22 savollik anketa bekorga ketmasin» qarori ochiq anketa sharti bilan saqlandi
- [2026-10-05] Bot «Umumiy anketa» holatini bila olmasa (API javob bermadi) → anketa davom etadi, qaror serverda → vaqtincha uzilish ochiq anketani yopiq deb ko'rsatmasin; yopiq bo'lsa server yuborishda rad etadi va bot `ApplicationsClosed` deydi
- [2026-10-05] «Umumiy anketa» yopiq bo'lsa botdagi «📋 Anketa to'ldirish» tugmasi ko'rinadimi? → yo'q, menyudan olib tashlanadi (egasi: «vakansiya bo'lmasa ko'rinmaydigan qilish»); holat 30 soniya keshlanadi, noma'lum bo'lsa tugma ko'rinadi → nomzodning ekranidagi eski menyu keyingi xabargacha qoladi, shu sababli tugma bosilganda jonli tekshiruv saqlangan; «💼 Vakansiyalar» tugmasi doim qoladi (ochiq vakansiya bo'lmasa shuni aytadi)
- [2026-10-05] Rad xabari / suhbat taklifi yetkazilmasa (nomzod botni bloklagan) — qayta urinamizmi? → YO'Q, har nomzodga bitta urinish (egasi: «1 marta borsa yetadi, qayta-qayta jo'natishi shart emas»); natija `candidates.*_notice_delivered` da, yozuv navbatdan chiqadi; HR kartasida suhbat taklifi «Yetkazilmadi — telefon qiling» bo'ladi → ilgari kuniga ~6000 marta qayta urinardi va 20+ bloklagan nomzod navbatni (20 tadan) tiqib, yangi rad xabarlarini to'xtatardi. Istisno: Telegram'ga umuman yetib bo'lmasa (tarmoq, 429, 5xx) xabar KETMAGAN — u navbatda qoladi va keyin bir marta yuboriladi
- [2026-10-07] Nomzod qora ro'yxati qayerda? → yangi menyu EMAS: «Qora ro'yxat» sahifasiga «Nomzodlar | Xodimlar» tablari (egasi: «xuddi rezerv bo'limi kabi»); yangi holat `Blacklisted = 6`, sabab majburiy → `docs/SPEC-nomzod-qora-royxat.md`
- [2026-10-07] Qora ro'yxatdagi odam botdan qayta yozilsa nima deyiladi? → umumiy muloyim rad javobi, sabab va sana AYTILMAYDI; to'siq muddatsiz, telefon VA Telegram hisobi bo'yicha → qora ro'yxatni oshkor qilish nizoga sabab bo'lardi
- [2026-10-07] Jarayondagi bot nomzodi qora ro'yxatga olinsa xabar ketadimi? → ha, oddiy rad xabari bir marta (rad etilganlar navbati) → nomzod javobsiz qolmaydi, qora ro'yxat oshkor bo'lmaydi
- [2026-10-07] Yopilgan (rad etilgan/rezerv/voz kechgan) nomzodni qora ro'yxatga olish mumkinmi? → ha; faqat ishga olingan va allaqachon qora ro'yxatdagiga emas → amalda HR avval rad etadi, keyin qora ro'yxatga oladi
- [2026-10-07] Jobster importi: dublikat qanday aniqlanadi? → bitta odam = telefon, so'ng to'liq ism + tug'ilgan sana; holat — oxirgi TO'LIQ arizadan (ismsiz-lavozimsiz chala anketa holat bermaydi); bazada bor odamga yangi karta ochilmaydi, izohiga «Jobster тарихи» qo'shiladi; uning yopiq kartasi Jobster qora ro'yxatida bo'lsa «Qora ro'yxat» ga o'tadi, FAOL kartaga tegilmaydi (HR qaror qiladi) → egasi: «sifatli, duplikatlarsiz»
- [2026-10-07] Jobster manbasi qanday ko'rinadi? → manba «Qo'lda» (1), vakansiya «… (Jobster)» yopiq, izohda «Jobster №kod» → egasi «kod yozma, to'g'ridan-to'g'ri bazaga» dedi; yangi manba qiymati kod o'zgarishini talab qilardi
- [2026-10-08] Verifix importi (dev): ish grafiklarining dam olish kuni qaysi? → yakshanba, dushanba–shanba ish kuni, tanaffus 0, yon berish 10/10 daqiqa (egasi «yaratib biriktir» dedi) → Verifix eksportida faqat grafik nomi va vaqti bor, kunlari yo'q; jadval izohida «текширилсин» deb turibdi. Vaqti noma'lum grafiklar («Вело савдо», «Фаррош 2», «24/7», «12:30 -13:30» va h.k.) yaratilmadi — 24 soatlik smena haftalik naqshda ifodalanmaydi, qolganlari faqat nom.
- [2026-10-08] Verifix importi (dev): xodim izohida nima qoladi? → faqat HR to'ldiradigan ishlar ro'yxati; bo'lim/lavozim/telefon/grafik o'z maydonida, rahbar (egasi: kerak emas), tab №, Verifix roli va rasm sha olib tashlandi → egasi «alohida izoh bo'lib chiqmasin, kerakli joyga tushsin» dedi; tab № uchun tizimda maydon yo'q, manba Excel'da qoladi.
- [2026-10-10] ISUP shlyuzida o'tishlar qaysi yo'l bilan keladi? → ISUP tunneli orqali AcsEvent so'rovi (LAN agentidagi kursor mantig'i); HTTPS push ham, signal (alarm) kanali ham yoqilmaydi → terminal jurnali uzilishdan keyin hech narsa yo'qotmaydi, push esa «yubordim-unutdim»; bitta port (7660), kamroq qism. Push uyasi bo'sh qoladi — kerak bo'lsa keyin real-vaqt qo'shimchasi.
- [2026-10-10] Yuz rasmi ISUP orqali qanday yetadi? → JSON + faceURL (bir martalik, 5 daqiqalik havola, https://hr.bonvi.uz/isup-face/) → tunnel faqat matn o'tkazadi (Content-Type yo'q, multipart yo'q); Hikvision'ning ISUP namunasi ham shunday. HTTPS'ni terminal qabul qilmasa — xuddi shu yo'l oddiy HTTP bilan (sozlama).
- [2026-10-10] Shlyuz terminallarni qanday biladi? → .env.isup da har terminalga 3 qator (DeviceId, Key, Token), ekran yo'q → eng tez va oddiy; UI keyin. Kalit har terminalga alohida, 16 belgi (terminal chegarasi 8–16).
- [2026-10-10] ISUP agentlari bitta jarayondami yoki har terminalga konteynermi? → bitta jarayon, har terminalga bittadan AgentWorker → zavodlar ko'paysa ham bitta .NET jarayoni (har konteyner ~60 MB bo'lardi); agent kodi o'zgarmadi.
- [2026-10-10] №91 «Ходимлар1» (541 joriy xodim) local bazaga: Verifix bilan qanday birlashtiriladi? → Verifix xodimi telefon/ism bo'yicha №91 qatoriga bog'landi (154), unga №91 dagi ism, JSHSHIR, tug'ilgan sana yozildi va vaqtinchalik Verifix munosabati olib tashlandi; keyin ETL (kod o'zgarmadi) bo'sh maydonlarni to'ldirdi va haqiqiy munosabatni ochdi → ETL'ni to'g'ridan-to'g'ri ishlatish 96+ takror xodim ochardi. №91 rasmiy reyestr: ism yozilishi, sana, firma, bo'lim, lavozim undan; rasm va grafik Verifix'dan.
- [2026-10-10] №91 dagi xatolar qanday tuzatildi? → faqat ikki dalil bir-birini tasdiqlasa avtomatik: JSHSHIR (nazorat raqami + Excel tug'ilgan sanasi, yagona yechim), tug'ilgan sana JSHSHIR bo'yicha (farq ≤2 raqam yoki kun/oy almashgan); uzoq farqda JSHSHIR boshqa odamniki deb olib tashlandi; tiklanmaydigani bo'sh + HR izohi → o'ylab topilgan qiymat yo'q. Jurnal: data/f91-tuzatishlar-2026-10-10.xlsx.
- [2026-10-10] Ikki sanali «ishga kirgan sana» (A/B) → oxirgi sana joriy ish boshlanishi, munosabatda HR izohi; faqat yillar («2009-2011, 2015-») → oxirgi yilning 1-yanvari, vaqtincha + izoh.
- [2026-10-10] «Мехнат дафтарчаси» bo'sh (275) → ETL sukuti MBF + «текширилсин» izohi (Verifix importidagi qaror bilan bir xil); «Trevon/TREVON» → «Тревон», «Велога» → «Velo union» (+izoh).
- [2026-10-10] «Синов муддати: Тугатилган» (440) → munosabat holati Faol (ETL sanasiz sinovda «Sinovda» qoldirardi); «buyruq topilmadi» belgisi (540) olib tashlandi — №91 da buyruqlar varag'i yo'q.
- [2026-10-10] Mintaqali lavozim «Вилоят савдо бошқарувчиси(Навоий)» (8 ta) → ma'lumotnomadagi asosiy nom, mintaqa jurnalda; 7 yangi lavozim ma'lumotnomaga qo'shildi, daraja o'xshash lavozimnikidek; ismdagi «П/З» (10) olib tashlandi + HR izohi.
- [2026-10-11] №91 eski varaqlari (tarix) local'ga: kim hozir ishlaydi? → faqat 10.10.2026 dagi «Ходимлар1» ro'yxati (540 kishi); eski varaqlar faqat tarix qo'shadi, ulardagi «faol» yozuv joriy ro'yxatda bo'lmasa yopiladi → egasi: «xodimlarda yo'q, Verifix'da bor — ketgan». Ketgan sanasi yo'qlarga taxminiy sana (avgust ro'yxatida bo'lganlarga 22.08.2026, qolganiga oxirgi ma'lum sana) + «HR аниқласин» (egasining qarori). Eslatma: 91.7 Архив'da ketgan sana ustuni umuman yo'q — sana faqat 91.6 da bor.
- [2026-10-11] Bir odamning eski varaqlardagi bir necha kartasi qachon birlashtiriladi? → JSHSHIR zid bo'lmasa va: to'liq ism bir xil; yoki telefon bir xil + ism mos (lotin/kirill harf, «ъ/ь», «-жон/-бек» farqi); yoki tug'ilgan sana AYNAN bir xil + familiya va otasining ismi imlo farqi bilan mos → 112 karta birlashtirildi. Egizaklar (Хасан/Хусан, Мадина/Мохина — ism o'xshashligi ≤0.8) va ismi bir xil, tug'ilgan yili boshqa (Рахмонов Мухаммадали 1995/1987) ATAYLAB alohida, kartasida «текширилсин». Tug'ilgan sana yoki otasining ismi farqli birlashmalar ham izohli.
- [2026-10-11] Qora ro'yxatdagi kishi bazada faqat familiya+ism bo'yicha topilsa (sana mos emas)? → xodimga belgi QO'YILMAYDI; nomzod sifatida yoziladi, o'xshash xodim kartasiga «шу кишими — текширилсин» → ishlayotgan yoki boshqa odamni adashib qora ro'yxatga qo'yish eng qimmat xato (Юсупов Зуриддин ≠ Юсупов Мухриддин, Усмонов ≠ Нўмонов).
- [2026-10-11] Bizda xodim bo'lmagan qora ro'yxat/rezerv odamlari qayerga? → nomzod, yopiq idish-vakansiya «№91 архиви (Excel)», manba Excel (3), holati varaqdan: qora ro'yxat (6, sababi bilan), rezerv (5), «ўзи отказ қилди» → voz kechgan (4), «тушмайди» → rad etilgan (2) → xodimlar ro'yxati yarim bo'sh kartalar bilan to'lmaydi; Telegram bot qora ro'yxatni nomzod bo'yicha tekshiradi. Yangi enum/kod yo'q.
- [2026-10-11] Hozir ishlayotgan odam qora ro'yxatda (Фазилов Сардор) → belgi ochiq qoladi + kartada «текширилсин» → ETL qoidasi bilan bir xil: jimgina yopish qayta ishga olish to'sig'ini o'chirardi.
- [2026-10-11] Local bazadagi sinov yozuvlari → o'chirildi (egasi «Ha, o'chir»): vakansiyalar «2 линия назратчиси» (36 soxta nomzod), «Sinov e'loni 0922», «Намуна оператор», «Tekshiruv 1790664061»; Jobster'dan kelgan «Тест Sanjar», «Тест Job», «Test —»; qurilmalar «C3/C6/C7 sinov»; «UI Smoke» bo'lim/lavozim/firma; «qqqqqqqq» foydalanuvchi. Zaxira: local 2026-10-11_100643. Prod'da «2 линия назратчиси» haqiqiy vakansiya bo'lishi mumkin — u yerda alohida tekshiriladi.
- [2026-10-11] Egasi: «muammolilarini qo'shmay tur, alohida eslab qol; qolganini qo'shaver; xato bo'lmasligi kerak; JSHSHIR yo'q — muammo emas». Qaysi yozuv «muammoli»? → qiymati ikki manbada zid, o'ylab topilgan yoki «bu kim / ishlaydimi» noaniq bo'lgan ODAM butunlay ushlab turiladi (70 kishi, 12 tasi hozir ishlaydi: qora ro'yxatda-yu ishlayapti 1, «bo'shatilgan-u ishlayapti» 10, ishga kirgan sanasi faqat yil 1; ketganlardan: ish davrlari ustma-ust 17, ishga kirgan sana o'rniga tug'ilgan sana 16, Verifix'da bor/№91 da yo'q 12, bir odammi—ikki karta 8, tug'ilgan sana ikki xil 4, otasining ismi ikki xil 1) + 3 shubhali qora ro'yxat nomzodi + davrdan tashqaridagi 4 hodisa. Yetishmayotgan maydon (JSHSHIR, pasport, telefon, rasm, jins) — muammo emas → egasining so'zi. Ro'yxat: data/f91-kutilayotganlar-2026-10-11.xlsx.
- [2026-10-11] Ilgari tasdiqlangan taxminiy qiymatlar ham «muammoli»mi? → yo'q, qoladi: ketish sanasi taxminiy + «HR аниқласин» (egasi 2026-10-10 da tanlagan), «Мехнат дафтарчаси» bo'sh → MBF + izoh (maydon bo'sh bo'la olmaydi) → ularsiz yarim shtat qo'shilmay qolardi.
- [2026-10-11] Eski varaqlardagi aniq xatolar qanday tuzatildi? → faqat ikkinchi dalil bo'lsa: tug'ilgan sana JSHSHIR ichidagi sanaga (20 ta, farq ≤2 raqam yoki kun/oy almashgan), imkonsiz tug'ilgan sana (ishga kirishdan keyin) bo'shatildi (5), JSHSHIR sanasi butunlay boshqa → JSHSHIR bo'shatildi (2), bitta davr ikki varaqda ≤31 kun farq bilan → bittaga (6), taxminiy tugash keyingi davr boshlanishidan oshsa → qisqartirildi (3), ismdagi «П/З», nuqta, sana, qavs olib tashlandi (8), hodisa yili «0205» → 2025 (1) → o'ylab topilgan qiymat yo'q; qolgan to'plam (2027 kishi) barcha tekshiruvdan 0 xato bilan o'tdi.
- [2026-10-11] Kutilayotganlar local bazadan ham o'chiriladimi? → yo'q, localda qoladi (auto-rejim o'chirishni «qaytarib bo'lmaydi» deb to'xtatdi); prod'ga ko'chirishda ro'yxat bo'yicha chiqarib tashlanadi. To'liq nusxa: bonvi_hr_f91_toliq.
- [2026-10-11] Prod'ga qanday yuklanadi (reja)? → local'da tekshirilgan to'plamning o'zi ko'chiriladi (zanjir qayta yurgizilmaydi): yozuv ID'lari saqlanadi, bo'lim/lavozim/firma NOM bo'yicha bog'lanadi (prod'da yo'qlari qo'shiladi), HR prod'da kiritgan 28 xodimga tegilmaydi va ular takror ochilmaydi (mos kelgani — alohida ro'yxatga) → «tekshirilgan narsa = prod'ga tushgan narsa». «91.1 Кенгаш хулосаси» arxivi bu bosqichda ko'chirilmaydi: prod'da o'sha odamlar Jobster kartasi sifatida bor, ikki nusxa bo'lib qolardi — alohida ish.
- [2026-10-11] Prod nusxasini localga olish ruxsat tizimida to'xtadi — sinov qayerda? → prod'ning o'zida, bitta tranzaksiyada: import → hisobot → orqaga qaytarish SQL → jadval sanoqlari va ma'lumotnomalar nazorat yig'indisi solishtiriladi → ROLLBACK (natija: farq 0). Undan oldin local'da prod'ga o'xshatilgan bazada (ID'lari boshqa, «сервиз/сервис», o'chirilgan bo'lim/lavozim) import, takroriy himoya va orqaga qaytarish sinaldi → prod ma'lumoti mashinadan chiqmaydi, sinov esa aynan jonli holatda.
- [2026-10-11] HR prod'da kiritgan xodim №91 da ham bor — qanday aniqlanadi? → JSHSHIR, yoki familiya+ism (ҳ/қ/ғ/ў, ъ/ь, lotin harflari tenglashtirilgan) + tug'ilgan sana yoki telefon; faqat familiya+ism mos kelsa ham qo'shilmaydi (aniqmas) → 26 kishi qo'shilmadi (24 si HR kartasi, 2 tasi ismdosh — «Прод'да бор» varag'ida). HR kartasiga tegilmadi, №91 dagi oldingi davrlari ham qo'shilmadi.
- [2026-10-11] №91 arxiv nomzodining telefoni prod'dagi nomzodda bor → ikkinchi karta ochilmaydi, mavjud kartaga ham tegilmaydi (9 ta, hammasi rezerv) → «duplikatsiz» qoidasi; HR kerak bo'lsa mavjud kartada belgilaydi.
- [2026-10-11] Xodim ilovasi hisoblari (employee_accounts) ko'chiriladimi? → yo'q: local paroli local kalit bilan shifrlangan, prod'da ochilmaydi; prod fon vazifasi har 15 daqiqada hisobsiz xodimga o'zi ochadi. Ish grafiklari va davomat ham ko'chirilmaydi — ular bilan «Kelmadi» hisoblana boshlardi, terminallar hali Verifix'da.
- [2026-10-11] Yangi ma'lumotnoma yozuvlari → faqat kerak bo'lgani: 1 bo'lim («1-Департамент», prod'da «1 департамент» ham yo'q — 3-/7-Департамент bilan bir xil yozilish) va 5 lavozim (oktabr ro'yxatidagi yangi nomlar); «VIKOR сервиз/Метан сервиз» → prod'dagi «…сервис» ga bog'landi.
- [2026-10-11] Rahbarning AI yordamchisi (Telegram bot, «Shef API» kaliti) «barcha ma'lumotni» olsin — MCP emas, API bilan (egasi tanladi) → kalitga doira qo'shildi: Basic (eski — xodimlar va ma'lumotnomalar, holding loyihalari) va FullRead (`/api/public/v1/full` — MCP'ning 21 ta o'qish vositasi oddiy GET sifatida) → yangi endpoint'lar yozilmadi: vositalar allaqachon hamma bo'limni qamraydi va uch qulf ortida (faqat-o'qish baza roli, EF faqat-SELECT, javob tozalagichi), ikkinchi nusxa himoyani ikkiga bo'lardi. Pasport, JSHSHIR, fayllar yopiq qoladi (egasi «yopiq qolsin» dedi). Oklad ochiq (FullRead barcha «ko'rish» ruxsatlari bilan ishlaydi). Doira yaratilgandan keyin o'zgarmaydi — «Shef API» uchun yangi to'liq kalit yaratiladi, eskisi bekor qilinadi. Har chaqiruv MCP jurnaliga («api-key:<prefiks>»). MCP o'chirgichiga bog'liq emas — faqat o'qish roli tayyorligiga.
- [2026-10-11] To'liq o'qish kalitini kim yarata oladi? → faqat kalit beradigan barcha «ko'rish» ruxsatlari + `mcp.connect` bor odam (xavfsizlik tekshiruvi: «sozlamalarni boshqarish»ning o'zi oklad ko'rmaydigan odamga butun bazani ochib berardi) → «o'zingda yo'q huquqni bera olmaysan» (rol muharriridagi qoida). Jurnalga yozilmasa natija qaytmaydi (503, MCP bilan bir xil). Parallel chaqiruv chegarasi MCP bilan umumiy (kalitga 2, jami 6) — bitta faqat-o'qish hovuzi. Muddat/IP cheklovi qo'yilmadi (egasi keyin xohlasa) — to'xtatish = kalitni bekor qilish.
- [2026-10-12] Terminal xodim rasmini yuklay olmadi (`urlDownloadFail`) — HTTP'ga o'tkazilsinmi? → yo'q (ruxsat tizimi ham «TLS zaiflashadi» deb to'xtatdi): sabab sertifikat zanjiri — `hr.bonvi.uz` Let's Encrypt ECDSA, ildizi ISRG Root X2/Root YE, terminal ro'yxatida yo'q (Verifix — RSA DigiCert, ishonadi) → rasm uchun alohida `isup.bonvi.uz` nomi, RSA kalit + ISRG Root X1 zanjiri; HR sayti sertifikatiga tegilmaydi, shifrlash saqlanadi, zavodlarda ichki tarmoq kerak emas. Terminalga CA yuklash (SYSLOG bo'limi) yordam bermadi — u faqat syslog uchun.
- [2026-10-12] RSA sertifikat ham yetmadi — terminalning TLS'i (tcpdump'dagi ClientHello) faqat RSA imzo va RSA/DHE-RSA kalit almashuvini taklif qiladi, ECDHE yo'q; Caddy (Go) DHE ham, RSA kalit almashuvi ham bermaydi → rasm uchun alohida nginx (7661, Mozilla «intermediate»: ECDHE + DHE-RSA-AES-GCM, ffdhe2048) — oldinga maxfiylik saqlanadi, oddiy RSA kalit almashuvi yoqilmagan (Verifix'nikidan kuchliroq). Sertifikatni Caddy oladi va yangilaydi, nginx faqat o'sha papkani o'qiydi. Har terminalda DNS 8.8.8.8/1.1.1.1 tavsiya (mahalliy router «nom yo'q» javobini 30 daqiqagacha eslab qoladi), Device ID'da chiziqcha bo'lmaydi.
- [2026-10-12] Yo'nalishi berilmagan terminalda yo'nalish navbat bilan aniqlansin (egasi) → «O'tishlar» ro'yxati hisob-kitobdagi juftlash qoidasini (`PassPairing.EffectiveDirections`) qo'llaydi: xodimning kalendar kunidagi o'tishlari, 1-chi kirish, 2-chi chiqish…; natija FAQAT ko'rsatiladi (`DirectionSource.Alternation`), o'tishga yozilmaydi; qurilma aytgan yo'nalish o'zgarmaydi. Yo'nalish filtri hali saqlangan qiymat bo'yicha.
- [2026-10-12] Grafigi yo'q xodimning kirdi-chiqdisi ham olinsin, 24 soatlik kun (egasi) → statistikada yangi holat «Keldi (grafiksiz)» (`AttendanceDashboardStatus.Arrived`): kelgan = kunning birinchi o'tishi, ketgan = navbat bo'yicha oxirgi chiqish, kechikish yo'q. O'tishi yo'q grafiksiz — avvalgidek «Grafik yo'q».
- [2026-10-12] Rasm qo'yishda bosh kesilib qolmoqda → rasmni brauzerda kvadratga surib-kattalashtirib joylashtirish (`PhotoCropper`, nomzod/xodim/o'z hisob rasmi); serverga tayyor kvadrat ketadi (server markazdan kesishi o'zgarmadi). Foydalanuvchi rasmi (tizim foydalanuvchilari) oynasiz yuklanadi — hozircha o'zgartirilmadi.
- [2026-10-12] O'tish surati ISUP orqali kelmaydi (tunnel faqat matn) — qanday olinadi? → Verifix kabi: terminalning o'z HTTP push'i (Hikvision multipart, surat bilan) `https://isup.bonvi.uz:7661/api/attendance/ingest/<token>` ga; agent qurilmasi (Generic) endi push'ni ham qabul qiladi, hodisa kaliti agentniki bilan bir xil shaklga keltiriladi («minmoe-{serialNo}-{vaqt}») — bitta o'tish ikki marta sanalmaydi, surat esa agent yozgan o'tishga biriktiriladi (tartibdan qat'i nazar). ISUP — xodim yozish va zaxira hodisa kanali bo'lib qoladi. Ofis terminallarida HTTP push uyasi Verifix'da — u yerda bu yo'l Verifix o'chguncha ishlamaydi.
- [2026-10-12] Grafigi yo'q xodim terminaldan o'tmay ketsa, 24 soatlik standart bo'yicha 22:00 da avtomatik «ketdi» (egasi) → `AttendanceDayBuilder` jadvalsiz kunning OXIRGI ochiq kirishini 22:00 (Toshkent) da yopadi (`AttendanceRules.NoScheduleAutoExitTime`), oraliq ishlangan vaqtga QO'SHILADI (egasi «ketdi deb belgilansin» dedi — ya'ni 22:00 gacha ishda deb hisoblanadi); bugun faqat 22:00 o'tgach; 22:00 dan keyin ochilgan kirish yopilmaydi; jadvali bor kunga tegmaydi; o'tishga hech narsa yozilmaydi (faqat hisoblash) → haqiqiy chiqish kelsa yoki jadval biriktirilsa o'z-o'zidan bekor bo'ladi. Ekranda vaqt yonida «avto» (panel, tabel katagi, xodim kalendari); belgi saqlanmaydi — roppa-rosa 22:00:00 dan taniladi. Saqlangan tabel qatori 03:10 dagi tungi qayta hisoblashda yangilanadi, panel esa darhol.
- [2026-10-12] So'rov qaysi IP'dan kelgani deploy'dan keyin yo'qolmoqda → Caddy so'rovlar jurnali faylga (`LOG_DIR/caddy/access.log`, JSON, 20 MB'dan bo'linadi, 30 nusxa / 30 kun): IP, vaqt, usul, yo'l, holat, User-Agent; qurilma tokeni (`/api/attendance/ingest/<token>`) va bir martalik rasm tokeni yashiriladi, `Authorization`/`Cookie`/`X-Api-Key` sarlavhalari va barcha javob sarlavhalari yozilmaydi, so'rov tanasi hech qachon → «kim» savoliga audit javob beradi, «qaysi IP» ga ikkalasi; sirsiz jurnal (mahalliy Caddy 2.11.4 da sinaldi: SECRET so'zi 0 marta).
- [2026-10-12] Zaxira faqat serverning o'zida, server o'lsa birga yo'qoladi → pulsiz birinchi qadam: `infra/backup/pull-offsite.sh` egasining kompyuterida eng so'nggi to'liq zaxirani (baza + fayllar, bitta stamp) tortadi, sha256 bilan tekshiradi, 14 tasini saqlaydi; sukut papka `~/BonviHR-zaxira` (iCloud'li `~/Documents` emas), 700/600 ruxsat; serverga faqat o'qiydi → S3/boshqa server pullik, egasining tanlovi; avtomatik jadval (launchd) o'rnatilmadi — xostga hech narsa o'rnatilmaydi qoidasi.
- [2026-10-12] Excel'da avtomatik «ketdi» ko'rinmaydi → «Kunlar» varag'ida oxirgi chiqish katagi SON bo'lib qoladi (formulalar buzilmasin), «avto» — kulrang kursiv, ma'nosi ustun sarlavhasidagi BITTA izohda (har katakka izoh — alohida shakl, grafiksiz yuzlab xodimning oyida faylni og'irlashtiradi); chetlanishlar varag'iga qo'shilmadi (kechikish/erta ketish/kelmagan ro'yxatida jadvalsiz kun bo'lmaydi).
- [2026-10-12] Audit A1: SPEC §5.3 dagi `AccessModuleKeys` yo'q edi, R1 (`PermissionCatalog`) bo'lim kalitlarini satr bilan yozgan → `Bonvi.Domain/Identity/AccessModuleKeys.cs` yaratildi va `PermissionCatalog` shu konstantalarga o'tkazildi (qiymatlar o'zgarmadi) → audit katalogi va rol muharriri bitta ro'yxatdan o'qiydi, harf xatosi bilan «hech kimga ko'rinmaydigan» bo'lim paydo bo'lmaydi.
- [2026-10-12] Audit A1: `AuditActorKind.EmployeeAccount = 4` va `Device = 5` hozir qo'shiladimi (SPEC §14.6)? → ha, verification main'da: PWA so'rovi (`ICurrentEmployee`) → EmployeeAccount, `user_id` BO'SH, `actor_name` = xodim F.I.Sh.; qurilma endpointlari (`DeviceEndpointMetadata` belgisi, ingest va agent guruhlari) → Device, qaysi qurilma ekani yozilmaydi → `user_id` faqat `users` jadvaliga ishora qilsin (A2 «aktorlar» ro'yxati adashmasin); qurilmani ishlovchi token bo'yicha topadi va qatlamga qaytarmaydi, o'zgargan qatorning o'zi (qurilma yozuvi, moslik) va amal nomi qurilmani ko'rsatadi.
- [2026-10-12] Audit A1: SPEC §14.5 klassifikatsiyasi → yoziladi: AbsenceRequest, WorkSchedule/Day/Date, ScheduleAssignment, AttendanceDevice, AttendanceIdentity, WorkSite, EmployeeAccount, AbsenceType (hammasi `attendance` bo'limi, AbsenceType — `catalog`); yozilmaydi: AttendancePass, AttendanceDay, DeviceEnrollment, MobileCheckIn, EmployeeRefreshToken, NotificationState, RefreshToken, MCP jadvallari (`mcp_*` — interceptorsiz «store» kontekst orqali yoziladi, `mcp_audit` o'zi jurnal) → mashina yasaydigan va juda ko'p qatorli jadvallar jurnalni to'ldirmasin; `AuditCatalogCoverageTests` har yangi entity'ni klassifikatsiyaga majburlaydi.
- [2026-10-12] Audit A1: fon yozuvchilari jurnalni to'ldirmasligi uchun nima tashlab ketiladi? → AttendanceDevice: LastSeenAt, LastEventAt, LastSeenIp, LastSkewSeconds, ClockOffsetSeconds, ClockCheckedAt, AgentSeenAt, TerminalSeenAt, LastRejectedAt, ReconciledAt, HeldRemovals, ForeignPersons, TokenIssuedAt/By; User: LastLoginAt, FailedLoginAttempts, LockedUntil; ApiKey: LastUsedAt/LastUsedIp; EmployeeAccount: LastLoginAt; SystemSetting qatorlari `attendance.employmentChangesCheckedAt` va `attendance.frozenThrough` (vazifa kursorlari) BUTUNLAY → tahrir faqat shularga tegsa qator yozilmaydi; terminal seriyasining birinchi bog'lanishi, HR ning «o'chirishlarga ruxsat» i, «Umumiy anketa» va MCP o'chirgichi esa yoziladi (kamdan-kam va ma'noli). `AuditBackgroundWritersTests` butun jadval sanog'i bilan qadaydi.
- [2026-10-12] Audit A1: nomida Token/Password bor, lekin sir bo'lmagan maydonlar (AttendanceDevice.TokenPrefix, EmployeeAccount.PasswordSetAt) → yashiriladi («***», nomi qoladi) → SPEC qamrov qoidasi nom bo'yicha; istisno ro'yxati yuritishdan ko'ra «parol qo'yildi» faktini nomi bilan ko'rsatish yetarli.
- [2026-10-12] Audit A1: bot hisobining kirishi jurnalga yoziladimi? → muvaffaqiyatli kirishi YOZILMAYDI, muvaffaqiyatsizi yoziladi → bot har daqiqada tokenni o'qiydi, access token 15 daqiqada eskiradi va u kuniga ~96 marta qayta kiradi — «xavfsizlik» ko'rinishi jarayon shovqiniga to'lardi; parol tanlash esa aynan xato urinishda ko'rinadi.
- [2026-10-12] Audit A1: qo'lda `_auditLogger.Record` chaqiruvlari qaysi biri qoladi? → olib tashlandi (interceptor bilan takrorlanardi): Users Create/Update/Activate/Deactivate/ResetPassword, RoleCommands Create/Update/Delete (va keraksiz `RoleEditorRules.Snapshot`), EmployeeAccount parol berish/qo'yish, «telefondan belgilash» o'chirgichi; QOLDI: `RevealEmployeePassword` (Export, «parol ko'rildi») — bu entity o'zgarishi emas, hodisa, interceptor uni ko'rmaydi → har o'zgarish jurnalda bir marta; olib tashlangan ikki unit test (parol audit yozuviga tushmasligi) `AuditTrailApiTests` dagi haqiqiy baza testlariga ko'chdi.
- [2026-10-12] Audit A1: `IAuditLogger.Record` sinxron, bajaruvchi ismi va rol nomi esa bazadan → hodisa navbatga (`PendingAuditEvents`) qo'yiladi va qatorni saqlash paytida `AuditTrailInterceptor` yasaydi (asinxron yo'lda asinxron so'rov) → sinxron I/O yo'q, hodisa va entity qatorlari bir xil bajaruvchi/amal/guruh/IP oladi; saqlash yiqilsa hodisa ham ketadi.
- [2026-10-12] Audit A1: LoginFailed uchun noma'lum login → bajaruvchi Anonymous, `user_login` = normallashgan raqam; raqamga aylanmagan matn esa YOZILMAYDI (`reason: format`, login bo'sh — odam parolini login maydoniga yozgan bo'lishi mumkin; SPEC §7.6 dagi «32 belgi» o'rniga); bloklangan va faolsiz hisob urinishi ham endi saqlanadi (ilgari saqlash yo'q edi) → har urinishning izi qolsin.
- [2026-10-12] Audit A1: eski xeshni kirishda jimgina yangilash (`needsRehash`) `User.PasswordHash` tahririni yozadi, bajaruvchisi Anonymous (kirish so'rovida token yo'q) → qabul qilindi: xeshlagich almashganda bir martalik, qiymat «***», amal nomi `Login` va o'sha guruhda shu foydalanuvchining Login hodisasi turadi.
- [2026-10-12] Audit A1: Excel eksporti — qaysilari yoziladi? → faqat SPEC §7.6 dagi xodimlar eksporti (`ExportEmployeesToExcel`: filtrlar, qidiruv MATNISIZ — faqat `hasSearch`, qatorlar soni); davomat eksportlari (tabel, o'tishlar, chetlanishlar, xulosa) KEYINGA → ularning ishlovchilari va `AttendanceReadModelTests` parallel tahrirda edi; qo'shish — har ishlovchiga `IAuditLogger` + `IUnitOfWork` va bitta `Record(Export…)`.
- [2026-10-12] Audit A1: `PermissionDenied` tafsilotidagi yo'l → marshrut NAQSHI, oxirgi «/» siz (`/api/users`, `/api/employments/{id:guid}/salary`), guruh endpointlarida naqsh bo'laklardan yig'iladi; saqlash bekor qilish belgisiga bog'lanmagan, yozib bo'lmasa — log, 403 javobi o'zgarmaydi → so'rov satri (ism, JSHSHIR) hech qachon jurnalga tushmasin, audit ruxsat qarorini buzmasin.
- [2026-10-12] Audit A1: fon vazifalari va urug'larning amal nomi → urug'lar `startup:*` (SPEC §7.4), hisob ochish vazifasi `job:employee-app:provision-accounts`; qolgan davomat vazifalari qamrovsiz (amal nomi bo'sh, bajaruvchi System) → ular faqat yozilmaydigan jadvallarga (kun yozuvlari, o'tishlar, kursorlar) yozadi.
- [2026-10-12] Audit A1: SPEC §5.4 indekslarni «xom SQL» bilan deydi → EF konfiguratsiyasida (`HasFilter`, `IsDescending(false, true)`) e'lon qilindi, migratsiya ularni o'zi yaratadi, trigger esa xom SQL bilan, ENG OXIRIDA → model snapshot indekslarni biladi, keyingi `migrations add` ularni «yo'qolgan» deb o'chirmoqchi bo'lmaydi; natijaviy SQL SPEC dagi bilan bir xil.
- [2026-10-12] Audit A1: o'chirishda (yumshoq ham) eski qiymatlarda bo'sh maydonlar → yaratishdagi kabi tashlab ketiladi, birlamchi kalit hech qayerga yozilmaydi (u `entity_id` da); murakkab xossalar nuqtali (`Salary.Amount`) → qator ixcham, tarix to'liq.
- [2026-10-12] Audit A1: `audit_logs` ni o'chiradigan/tozalaydigan joy bormi (testlar, ETL, restore)? → yo'q: integratsiya testlari har sinfga yangi baza yaratib DROP qiladi, `restore.sh` — `pg_restore --clean` (DROP TABLE + COPY, trigger to'smaydi, trigger post-data'da qayta yaratiladi), ETL faqat qo'shadi → test infratuzilmasini o'zgartirish kerak bo'lmadi.
- [2026-10-12] Audit A1: ETL qadami hisobotidagi «yozildi» soni endi audit qatorlarini ham o'z ichiga oladi → qoldirildi (ETL faqat dev'da, Excel prod'ga ko'chirilmaydi) → sonni ajratish interceptorga qo'shimcha holat qo'shardi.
- [2026-10-12] SPEC-rollar-va-audit §17.1 (bitta «Bosh rahbar») va §17.2 (boshqa amallar ichidagi pul) — A1 ga tegishli emas, egasi javob bermaguncha SPEC sukutlari o'z kuchida (R1 da qo'llanadi).
- [2026-10-12] Audit A1: SPEC §7.4 dagi `IAuditActorSource` → `{Kind, UserId, Login, FullName, RoleName}` → port faqat SHAXSNI beradi (`AuditPrincipal`: tur, foydalanuvchi, login, xodim), ism va rol NOMINI Infrastructure saqlash paytida bitta AsNoTracking so'rov bilan qo'shadi va qamrov davomida eslab qoladi → port bazani bilmasin; interceptor `AppDbContext` ga DI orqali bog'lansa, kontekst opsiyalari interceptorga, interceptor kontekstga bog'lanib aylana hosil bo'lardi.
- [2026-10-12] Audit: bir xil `PermissionDenied` (hisob + usul + marshrut naqshi) har safar yoziladimi → 10 daqiqalik oynada BIR MARTA (jarayon xotirasidagi kesh) → yangilanadigan panel ruxsati yo'q so'rovni har 30 soniyada qaytarsa, jurnal minglab bir xil qator bilan shishirdi; birinchi qator «kim, nimaga, qachon urindi» ga javob beradi.
- [2026-10-12] Xodim ilovasi prod'da ochilgach (egasi), 2026-09-30 ko'rigidagi bandlar: HR yozgan arzimas raqamli parol (000000, 123456, 121212…) rad etiladimi → HA, `xodim.parol_oddiy` (avtomatik parol bilan bir xil `IsTriviallyWeak`); harfli parol tekshirilmaydi → ilgari «HR ning ongli qarori» deb o'tkazilardi, lekin bunday PIN birinchi terib ko'riladi va internetga ochiq.
- [2026-10-12] Xodim kirishida ko'p IP'dan aylanish → login bo'yicha UMUMIY chegara: hamma IP'lardagi 20 xato (oxirgisidan 1 soat o'tmagan) → login 1 soatga hamma joyda bloklanadi (juftlik bloki 5/15 daqiqa o'zgarmadi); loginning OXIRGI muvaffaqiyatli IP'si umumiy blokdan ozod → bitta IP ham ~45 daqiqada 20 ga yetadi (har 15 daqiqada 5), shuning uchun xodim odatdagi tarmog'idan kiraveradi, begona manzillar to'siladi; oddiy odam 20 ga yetmaydi; notanish raqam ham xuddi shunday (oracle yo'q); HR'da qo'lda yechish yo'q — API qayta ishga tushsa tozalanadi.
- [2026-10-12] Tokensiz kirish yo'llarining tana hajmi → 16 KB (`/api/auth/login|refresh|logout`, `/api/employee-auth/*`), `/api/auth` butunligicha EMAS (`/api/auth/me/photo` rasm yuklaydi) → MCP OAuth'dagi bilan bir xil son va usul.
- [2026-10-12] PBKDF2 yuklamasi → HR va xodim kirishi uchun BITTA umumiy navbat (bir vaqtda 2, navbatda 20, ortig'i 429), global cheklov zanjirida (Testing'da o'chiq) → IP oynalari ko'p manzildan kelgan oqimni to'xtatmaydi, 2 GB serverda HR ham sekinlashardi.
- [2026-10-12] Audit A2: `employees.personal.view` kodda yo'q edi (R1 bu loyihada `PermissionCatalog` + rolda kod ro'yxati, P1 qilinmagan) → `Permissions.All` ga qo'shildi va muharrirda «xodimlar» darajasiga EMAS, `PermissionCatalog.Extras` ga joylandi; qoida `PersonalDataAccess.CanSeePersonalData` = `employees.personal.view` YOKI `employees.edit`; prod'dagi rollarga ma'lumot migratsiyasi yo'q → SPEC §3.4 (ko'rishda alohida beriladi, tahrirlashda o'z-o'zidan bor): bugungi «HR menejeri»/«Bosh rahbar» deploy kuni jurnalda shaxsiy maydonlarni ko'radi, jimgina huquq berilmaydi; darajaga qo'yilsa mavjud rollar muharrirda chala ko'rinardi. Frontend: `role-keys.ts` `ROLE_EXTRA_CODES` ga kod qo'shilishi kerak (tarjimalar `roles.extra.*` da tayyor).
- [2026-10-12] Audit A2: qo'shimcha vazifa haqi jurnalda qaysi ruxsat bilan? → buyruqning `ToSalary` si `Type = AdditionalDuty` bo'lsa `salary.view` VA `salary.extra.view` (`SalaryAccess.CanSeeExtraPay`, karta bilan bir xil); tuzatish qatorida tur yo'q — `employment_events` dan bitta so'rov bilan olinadi, topilmasa qo'shimcha haq deb yashiriladi → buyruqlar ro'yxati va karta qaysi qoidada bo'lsa, jurnal ham shu qoidada; shubhada yashirgan ma'qul.
- [2026-10-12] Audit A2: rag'bat/jarima summasi (`Incentive.Amount`) va nomzod anketasidagi maosh — oklad sinfidami? → yo'q, ochiq (`OpenMoney`), qamrov testi har `Money` maydonni oklad yoki ochiq deb belgilashni talab qiladi → ilovaning o'zida ular `salary.view` siz ko'rinadi (`IncentiveDto`, anketa); jurnal ilovadan qattiqroq bo'lsa, «nega tarixda ko'rinmaydi» savoli tug'ilardi.
- [2026-10-12] Audit A2: bo'limning kirish kodi → `PermissionCatalog` dagi bo'limning ENG QUYI darajasining HAMMA kodlari (qo'lda ro'yxat yo'q); bu loyihada `roles` → `roles.manage` (`roles.view` yo'q), `users` → `users.manage`, davomat → `attendance.view` → SPEC §7.10 dan keyin qo'shilgan bo'lim ham o'z-o'zidan to'g'ri ishlasin.
- [2026-10-12] Audit A2: Excel eksporti qatorlari qaysi ko'rinishda? → «O'zgarishlar» (`kind=changes`), bo'limi `reports`, `reports.view` bilan (xodim ilovasi parolini ko'rish ham — u ham `Export`); «Kirish va xavfsizlik» — faqat Login/LoginFailed/Logout/TokenReuse/PermissionDenied, `users.manage` bilan → SPEC §7.3 eksportni hisobotlarga beradi; xavfsizlik segmentiga qo'yilsa, hisobot egasi o'z eksportlarini ko'rmasdi.
- [2026-10-12] Audit A2: davrning bitta chegarasi berilsa? → `to` yo'q — bugun (Toshkent), `from` yo'q — `to` dan 29 kun oldin (30 kun, ikkalasi kiradi); xodim tarixida davr yo'q (xodim indeksi chegaralaydi) → jurnal ro'yxati hech qachon boshidan oxirigacha o'qilmasin (SPEC §7.8).
- [2026-10-12] Audit A2: notanish `module`/`action`/`entity`/`kind`/`actor` → bitta yangi kod `audit.filtr_notogri` (400, `parameter` tafsiloti bilan), to'rt tilga tarjima; `action` faqat nomi bilan (raqam emas), registrsiz → minimal API bog'lash xatosi tarjimasiz «So'rov noto'g'ri» beradi, SPEC esa 400 deydi; bitta kod — interfeys uchun yetarli.
- [2026-10-12] Audit A2: `GET /api/audit/actors` so'rovchining ko'rinishi bilan toraytiriladimi? → yo'q: jurnalda kamida bir marta uchragan HAMMA foydalanuvchi (`users` dan, `user_id` indeksi bo'yicha EXISTS) → 3–4 HR va bot hisobining ismi sir emas, qatorlar baribir ko'rinish bo'yicha filtrlanadi; toraytirish har ochilishda butun jurnalni o'qirdi.
- [2026-10-12] Audit A2: «Shu amal ichida» (`related`) → qatorning o'zisiz, yozilish tartibida, eng ko'pi 50, faqat ko'rinadiganlar → ETL qadami bitta guruhda minglab qator yozadi.
- [2026-10-12] Audit A2: kengaytirishdan oldingi qatorlar (21 ta, dev) qanday chiziladi? → `changed_fields` yo'q — eski va yangi TO'LIQ nusxa solishtirilib farq qilgan kalitlar olinadi, maydon nomi kichik harfli (`name`), turi JSON qiymatidan; bajaruvchi ismi nusxasi yo'q — foydalanuvchining HOZIRGI ismi, rol nomi esa qo'yilmaydi → «xatosiz chizilsin» talabi; rol nomini o'qishda qo'yish tarixni qayta yozardi (SPEC §5.3).
- [2026-10-12] Audit A2: pul juftini birlashtirishda valyuta qatorda yo'q bo'lsa (tahrirda faqat summa o'zgargan) → ikkinchi tomondagi valyuta, u ham yo'q bo'lsa `currency: null` (interfeys summani valyutasiz yoki so'm deb ko'rsatadi) → qatorda yo'q qiymatni bugungi holatdan to'ldirish tarixni taxmin bilan yozish bo'lardi; bazada hamma oklad UZS.
- [2026-10-12] Audit A2: yozishda yashirilgan qiymatni o'qishda qanday taniymiz? → istalgan maydonda qiymat aynan `"***"` bo'lsa — `masked: true`, qiymatsiz (foydalanuvchi matnga «***» yozgan kamdan-kam holat ham yashirinadi) → literal hech qachon javobga chiqmasin; `MaskWhen` (maxfiy sozlama) ni o'qishda qayta hisoblab bo'lmaydi.
- [2026-10-12] Audit A2: havola nomlari (bo'lim, lavozim, xodim, foydalanuvchi) havola qilingan jadvalning bo'limi bo'yicha yashiriladimi? → yo'q, nom ko'rinadigan qator ichida ochiq; nom o'qish paytida, o'chirilganlari bilan (`IgnoreQueryFilters`) → SPEC §3.10: ism, bo'lim, lavozim shaxsiy ma'lumot emas; qattiq o'chirilgan rolning nomi topilmaydi — identifikator qoladi.
- [2026-10-12] Audit A2: notanish yoki o'chirilgan xodimning `/history` si → 404 emas, bo'sh (yoki saqlangan) sahifa → o'chirilgan kartaning tarixi ham shu yo'ldan o'qiladi.
- [2026-10-12] Audit A2: davomat Excel eksportlarini jurnalga yozish (SPEC §7.6, A1 keyinga qoldirgan) → yana KEYINGA: to'rt ishlovchining konstruktori `AttendanceQueryValidationTests` va parallel tahrirdagi `AttendanceReadModelTests` da to'g'ridan-to'g'ri chaqiriladi — «4 ishlovchi + DI» dan ko'p fayl; qo'shish — har ishlovchiga `IAuditLogger` + `IUnitOfWork` va bitta `Record(Export…)`.
- [2026-10-12] «HR bermagan hisob kira olmasin» qanday aniqlanadi → yangi ustun `employee_accounts.password_handed_out_at`: HR parolni KO'RGAN (birinchi marta) yoki O'ZI YOZGAN lahza; avtomatik yasalgan parol (hisob ochilishi, «Yangi parol yasash») uni tozalaydi; bo'sh bo'lsa kirish notanish raqam bilan aynan bir xil rad etiladi (soxta xesh, sanoq, umumiy xato) → ~2000 avtomatik hisobning parolini hech kim bilmaydi, ularni internetdan taxmin qilish yuzasi yopiladi; ekranda «Yangi parol yasash» parolni darhol ko'rsatadi, ya'ni HR uchun qo'shimcha qadam yo'q.
- [2026-10-12] Migratsiyada eski hisoblar → audit izidan: JORIY paroldan keyingi birinchi `password_viewed`/`password_set` (5 s zaxira) va shifrlangan nusxasiz eski parol (2026-09-27 dan oldin, HR o'zi bergan) hamda joriy parol bilan allaqachon kirilgan hisob (`last_login_at >= password_set_at`) «berilgan»; qolganlari bo'sh → ilovadan allaqachon foydalanayotgan xodim deploy'dan keyin ham kiradi, ko'rilmagan parollar yopiladi.
- [2026-10-12] Audit A2 (frontend): maydon va amal nomlari qaysi kalitlardan? → SPEC §9.9 dagi «mavjud kalitlarni qayta ishlatish» o'rniga o'z lug'atlari: `audit.fields.*` (118), `audit.operations.*` (114), `audit.entities.*` (43, davomat entity'lari va `Endpoint` bilan), `audit.reasons.*`; ro'yxatda yo'q maydon xom nomi bilan ikkinchi darajali rangda, notanish amal nomi — amal turi («O'zgartirildi»), `etl:`/`startup:`/`job:` va `Activate*`/`Deactivate*` — umumiy nom; sanoq qiymatlari (buyruq turi, sinov holati, belgi, bandlik holati, jins, nomzod/vakansiya holati) mavjud kalitlar orqali → tugma yorliqlari («Ishga olish») jurnalda noto'g'ri o'qilardi; massivlar `check-locales.mjs` ga ulandi, ya'ni yetishmagan tarjima tekshiruvni yiqitadi.
- [2026-10-12] Audit A2: «Bo'lim» filtrida qaysi bo'limlar? → faqat so'rovchi ko'ra oladiganlar; kirish kodlari frontendda ko'zgu (`AUDIT_MODULE_ENTRY`, `PermissionCatalog` ning eng quyi darajasi: `roles` → `roles.manage`, `users` → `users.manage`, `attendance` → `attendance.view`) → `GET /api/roles/catalog` faqat `roles.manage` bilan ochiq, jurnalni esa torroq rol ham ko'radi; server baribir ko'rinishni qo'llaydi (yanglishsa — bo'sh sahifa, xato emas).
- [2026-10-12] Audit A2: «Maydon | Oldin | Keyin» har doimmi? → faqat tahrirda; yaratish, o'chirish va kirish/eksport hodisalarida ikki ustun «Maydon | Qiymat» (o'chirishda eski qiymat) → aks holda bitta ustun har qatorda «—» bo'lib turardi; rol ruxsatlari («Permissions») kodlar emas, muharrir tilida: «Xodimlar: To'liq → O'zgartirish», «+ …», «− …» (`deriveLevel`, katalog `roles.manage` bilan — rol qatorlarini ham faqat u ko'radi).
- [2026-10-12] Audit A2: xodim ilovasi parolini ko'rish ham `Export` bo'lib yoziladi → yorlig'i «Parol ko'rildi» (`audit.actionRevealed`), «Excelga yuklandi» emas → bu qatorga «Excel» deyish yolg'on bo'lardi.
- [2026-10-12] Audit A2: xodim kartasidagi «Tuzatilgan: {{date}}» qaysi maydondan? → `EmploymentEventDto.updatedAt` (alohida tuzatish belgisi DTO'da yo'q; u tuzatishda va vazifa tugash sanasini o'zgartirishda to'ladi); «Tuzatishlarni ko'rish» havolasi `/audit?entity=EmploymentEvent&entityId=…&from=<yozuv kiritilgan kun, Toshkent>` → jurnalning standart «oxirgi 30 kun»i eski tuzatishni yashirardi. `employees.recordedBy` «Kim kiritdi» edi (`{{name}}` siz — ism ekranga chiqmasdi) → SPEC dagi «Kiritdi: {{name}}» ga almashtirildi (4 til); `employees.eventEdited` endi ishlatilmaydi, o'chirilmadi.
- [2026-10-12] Audit A2: ro'yxat ko'rinishi → `DataTable` faqat `xl` dan (ustunlar ≈955px, 1280px ekranda yon menyu bilan sig'adi), torroqda kartalar; bir xil `correlationId` li ketma-ket qatorlar sarlavha QATORI ostida (umumiy `DataTable` o'zgartirilmadi — sarlavha kataklari boshqacha chiziladigan oddiy qator); bajaruvchi rasmi yuklanmaydi, bosh harflar → foydalanuvchi rasmi `users.manage` bilan ochiq, torroq rolda har qatorda 403 bo'lardi.
- [2026-10-12] Audit A2: davr maydonlari → berilmagan chegara server qoidasi bilan to'ldirib ko'rsatiladi (oxiri — bugun, boshi — 29 kun oldin); chala yozilgan sana manzilga yozilmaydi; boshlanish > tugash bo'lsa so'rov yuborilmaydi, xabar toolbar ostida (`audit.davr_notogri`) → maydon har harfda standart sanaga sakramasin, server 400 ga bormasin. Xodim kartasidagi tarixda bo'lim chipi va sahifa manzilga yozilmaydi (kartaning ichki holati).
- [2026-10-12] Audit A2: frontend unit testlari → yo'q: `web/package.json` da test yuritgichi yo'q, qo'shilmadi (topshiriq shu); qo'llanmadagi «Audit jurnali» bo'limi (SPEC §11 A2-W) keyinga qoldi.
- [2026-10-14] Ism qidiruvi lotin/kirill farqsiz (egasining talabi) → bazada `search_key()` + `employees/candidates.search_key` (GENERATED STORED, trigramma indeksi); o = u birlashtirildi (Жўраев/Joraev/Juraev bitta odam; narxi: Умаров ↔ Омаров), eski ё = е saqlandi (shu sababli apostrofsiz «Yoldoshev» «Йўлдошев» ni topmaydi), a/o birlashtirilmadi (Тошматов ↔ Tashmatov topilmaydi) → dev'dagi 113 juft lotin/kirill yozuvdan 109 tasi topiladi, begona natija eng kam. Vakansiya nomi, ma'lumotnomalar va davomat faylini moslash eski qoidada qoldi — ular ism emas / aniq moslash.
- [2026-10-14] Sinov natijasi va menyular (egasining qarorlari): «Sinov» sahifasi sukut bo'yicha faqat «Sinovda»; «O'tmadi»/«O'zi ketdi» → shu oynada ishdan bo'shatish (bitta tranzaksiya, `employment.terminate` ham kerak); «Bekor qilindi» natijalardan olib tashlandi (server ham rad etadi); muddati kiritilgandayoq tugagan qabul → darhol «O'tdi» (muddat tugagan sana bilan) va «Faol» → qonun: muddat tugab ishlashda davom etgan xodim sinovdan o'tgan hisoblanadi.
- [2026-10-15] Global qidiruv (egasining talabi: «barcha bo'limlardan») → yuqori paneldagi maydon yozish bilan birga guruhlangan oyna ochadi (xodimlar 6, nomzodlar 6, vakansiyalar 3 + umumiy son), alohida natijalar sahifasi QILINMADI → kartaga bir bosishda o'tiladi; ko'p topilgan xodimlar uchun «Hammasini ro'yxatda ko'rish» (`/xodimlar?status=all&q=`). Nomzodlarda barcha holatni bir joyda ko'rsatadigan ro'yxat yo'q, shu sababli u yerda havola emas, «aniqroq yozing» yozuvi. Server o'zining qidiruv qoidasini YOZMAYDI — bo'limlarning o'qish modellarini ishlatadi (lotin/kirill, JSHSHIR, telefon bir xil), faqat «yashirish» qoidalari o'chiriladi (`AllStatuses`, Excel arxivi ham). Tartib: ishlayotgan xodimlar → ishdan ketganlar → munosabatsizlar, har biri familiya bo'yicha; «familiyasi so'rov bilan boshlanadigan birinchi» saralashi yo'q (EF'da `search_key` funksiyasini ulash kerak bo'lardi) — kerak bo'lsa keyingi qadam.
- [2026-10-15] Sahifa hajmi tanlovi va ro'yxat ramkasi (egasining talabi: «20, 50, 100, barchasi; butun sahifa scroll bo'lib ketmasin, listning o'zi scroll bo'lsin») → ro'yxat ostida «Sahifada: 50 ⌄» (sukut 50, vakansiyalar ham 25 dan 50 ga), tanlov har ro'yxatga alohida brauzerda eslab qolinadi (`usePageSize`), manzilda emas; «Barchasi» = `pageSize=10000`, server chegarasi xodimlar/rag'bat/buyruqlar/nomzodlar/vakansiyalar/sinov uchun 200 → 10 000 (tashqi API 200 da qoldi); audit, davomat o'tishlari va hisobotlarda «Barchasi» YO'Q (chegarasiz o'sadigan jurnallar), faqat 20/50/100; «Barchasi» faqat ramka ishlaganda (oyna ≥ 1024×640 px) va jadval ko'rinishida (kartochkalar virtual chizilmaydi → 100 ga tushadi). Shu o'lchamdan (`frame` varianti; past noutbukda oddiy oqim — u yerda ramka 3–4 qatorlik joy qoldirardi) ro'yxat sahifasi ekranga sig'adi: sarlavha, filtrlar, ustun nomlari va sahifalash joyida, qatorlar kartaning ichida aylanadi (`data-list-frame` + `DataTable fill`); 150 dan ko'p qator virtual chiziladi (`@tanstack/react-virtual`), virtual qatordagi xodim rasmi ro'yxat to'xtab, qator 0.3 s turgandagina so'raladi — aylantirishda 120 so'rov/daqiqa chegarasi yeb qo'yilmasin (rasm uchun alohida server chegarasi qilinmadi). Sahifa raqami manzilda, hajmi brauzerda bo'lgani uchun «Barchasi» dan keyin eski `?page=N` oxirgi sahifaga tushiriladi (`useClampPage`). Voronka ko'rinishi ramkaga olinmadi (ustunlar kesilib qolardi).
- [2026-10-15] «Nomzodlar» qidiruvi rad etilganlardan ham (egasining talabi) → qidiruv matni bo'lsa (holat tanlanmagan, «Rad etilganlar» bo'limi emas) so'rovga `includeRejected=true` qo'shiladi: rad etilganlar VA o'zi voz kechganlar ham chiqadi (menyudagi «Rad etilganlar» bo'limi ikkalasini birga ko'rsatadi, HR uchun bitta guruh), jarayondagilar qoladi. Rezerv va qora ro'yxat o'z bo'limida qoldi (ular uchun ro'yxat ustida «Rezervda/Qora ro'yxatda ham bor» izohi bor); ishga olinganlar va Excel arxivi qoidasi o'zgarmadi. Yangi bayroq server qoidasini o'zgartirmaydi — bayroqsiz so'rov (MCP, boshqa ekranlar) avvalgidek.
- [2026-10-16] Jarimada buyruq (egasi: «jarima uchun buyruq chiqarilmaydi, maydon ham, bazadagi ma'lumot ham kerak emas») → forma jarimada buyruq raqami/sanasini UMUMAN chizmaydi; server ularni yashirincha tashlamaydi, balki `ragbat_jarima.buyruq_jarimada` bilan RAD etadi (eski klient yoki MCP xato yozuv qilmasin); bazada `ck_incentives_penalty_no_order`. `order_no`/`order_date` ustunlari QOLDI — rag'bat ishlatadi. Migratsiya mavjud jarimalardagi buyruqni tozalaydi (prod'da 2026-10-16 da jarima yozuvi 0 ta edi).
- [2026-10-16] Jarima summa yoki foiz (egasi: «300 000 so'm yoki 4 foiz, faqat raqam») → alohida `percent numeric(5,2)` ustuni (summa ustuniga «belgi» qo'shish o'rniga: valyuta va foiz aralashmasin, jamlar buzilmasin); faqat PULLI JARIMA turida «Summa | Foiz» tanlovi; 0 < foiz ≤ 100, ikki kasr xonagacha, 100 dan kattasi maydonga umuman yozilmaydi. Foiz nimadan olinishi saqlanmaydi va hisoblanmaydi — turning nomidan anglashiladi («Жарима» — oylikdan, «Мукофотдан маҳрум қилиш» — mukofotdan); tizim ish haqini hisoblamaydi. Rag'bat faqat summa bilan (`foiz_ragbatda`). MCP jamlari foizli jarimani summaga qo'shmaydi. Mehnat kodeksidagi jarima chegarasi (o'rtacha oylikning ma'lum foizi) tizimda TEKSHIRILMAYDI — HR qarori.
- [2026-10-16] «Rag'bat va jarima» Excel eksporti (egasi: «filtrlar ta'sir qilgan holatda») → `GET /api/incentives/export`, parametrlari ro'yxat bilan AYNAN bir xil (`ListIncentivesQueryHandler.BuildFilter` bitta yo'l), sahifasiz, ko'pi bilan 5000 qator (xodimlar eksporti chegarasi); ruxsat `incentives.view` VA `reports.export` (davomat eksporti kabi); Excel'ga ekrandagi ro'yxatda yo'q SABAB va SOVG'A ham qo'shildi (HR uchun to'liq yozuv), bo'lim ustuni QO'SHILMADI (ro'yxat qatorida yo'q, sana bo'yicha joylashuvni hisoblash kerak bo'lardi); foiz — haqiqiy Excel foizi (0,04 + «0%» formati); har eksport auditga (qidiruv matnisiz).
- [2026-10-16] Rag'batdan ham buyruq (egasi: «rag'bat va jarima uchun buyruq umuman chiqarilmaydi») → `incentives.order_no`/`order_date` ustunlari va `ck_incentives_penalty_no_order` OLIB TASHLANDI (migratsiya `RagbatJarimaBuyruqsiz`; prod'da 24 rag'batning birortasida buyruq yo'q edi — ma'lumot yo'qolmaydi), forma/ro'yxat/Excel/MCP'dan ham. Eski brauzer oynasi `orderNo` yuborsa — server jimgina e'tiborsiz qoldiradi (JSON'da noma'lum maydon), xato bermaydi. `employee.orderNo` tarjimasi qoldi — mehnat buyruqlari ishlatadi.
- [2026-10-17] Ilovada «Yo'qlik turi» varag'i bo'sh ochiladi — sabab? → prod'da (va dev'da) `absence_types` 0 qator: SPEC §B1 dagi urug' hech qachon yozilmagan edi → `CatalogSeeder` ga `absence-types.json` (Ishxona ishi bilan / Kasallik / Shaxsiy sabab, SPEC dagi kabi lotinda) qo'shildi; umumiy qoida — faqat ma'lumotnoma butunlay bo'sh bo'lsa quyiladi, ya'ni HR deploy'dan oldin o'zi kiritsa urug' tegmaydi. Ilova varag'i va HR oynasi endi bo'sh ro'yxatda tushuntirish chiqaradi
- [2026-10-17] Ilova so'rov formasida vaqt oralig'i yo'q edi (faqat «faqat bir kunlik so'rovda» degan izoh qolgan) → bir kunlik so'rovda ixtiyoriy «Boshlanish vaqti / Tugash vaqti»; bittasini «Butun kun» bilan tozalash ikkalasini tozalaydi; ko'p kunlikda maydonlar yashirinadi va yuborilmaydi → SPEC-so'rovlar A3 (egasining qarori), server allaqachon qabul qilardi
- [2026-10-17] Tasdiqlangan so'rov ish vaqtiga qanday ta'sir qiladi (egasi Verifix bilan solishtirdi) → TUR bo'yicha: `absence_types.counts_as_work_time`; bayroqli turda so'rov vaqti binoda bo'lgandek hisoblanadi (rejadagi smena ichida, hozirgacha, rejadan oshmaydi), qolganlari avvalgidek faqat «Sababli» → egasi tanladi (AskUserQuestion); «Belgilash» (unutilgan kirish/chiqish) so'rovi hozircha QILINMAYDI — egasi
- [2026-10-17] So'rov kunni rejadan oshirishi mumkinmi (tanaffus oynasi yo'q jadvalda butun kun = smena uzunligi) → yo'q, qo'shilgan daqiqa `planned − ishlangan` bilan cheklanadi → so'rov ortiqcha ish yasamasligi kerak; ortiqcha ish faqat terminaldan
- [2026-10-17] «Ish vaqti» so'rovi bor, o'tishsiz ish kuni holati → `Present` (yangi holat qo'shilmadi); terminal yozmagan vaqt `FirstInAt`/`LastOutAt` ga yozilmaydi, ulush `credited_minutes` da va katakda «so'rov bo'yicha» → yangi holat butun tabel/hisobot/Excel xaritalarini o'zgartirardi; panelda «keldi» lahzasi so'rov boshidan, ro'yxatdagi vaqt esa faqat o'tishdan
- [2026-10-17] Jadvalsiz kunga «ish vaqti» so'rovi → hech narsa qo'shilmaydi (reja oynasi yo'q) → prod'da hali birorta jadval biriktirilmagan (2026-10-17 tekshirildi), ya'ni jadval biriktirilmaguncha so'rov vaqti tabelga qo'shilmaydi — egasiga aytildi
- [2026-10-17] Bayroq o'zgarganda o'tgan kunlar → o'sha turdagi tasdiqlangan so'rovlar kunlari fon navbatida qayta hisoblanadi (`IReferenceSavedHook`), muzlatilgan davrni hisoblovchi o'zi himoya qiladi → aks holda tabel ikki xil qoidada qolardi
- [2026-10-17] Oldindan tasdiqlangan «ish vaqti» so'rovi o'z kunida qachon qo'llanadi (qa-review topilmasi) → `AttendanceStartedShiftsJob` har 15 daqiqada «ish vaqti» so'rovi bor xodimlarning kecha/bugun kunlarini QAYTA yozadi (qolganlarga — avvalgidek faqat yo'q qatorlar) → usiz qator smenadan oldin «Sababli · 0» bo'lib yozilib, keyingi tungacha qolardi; bunday xodimlar kam, yuk sezilmaydi
- [2026-10-17] Verifix'dan nimalar olinadi (egasi Verifix'ni brauzerda ochib berdi, faqat o'qildi) → egasi tanladi: jadvalni o'zgartirish so'rovi, so'rov formasi «Qism kun / Butun kun / Bir necha kun» + filtrlar, «Xodim o'zi so'ray oladi» belgisi; tsiklli jadval va «Belgilash» — hozircha yo'q → `docs/SPEC-verifikatsiya-jadval-sorovi.md`, `docs/INTAKE-verifix-web.md`
- [2026-10-17] Jadval so'rovi jadvali yo'q xodimga qo'llanadimi → yo'q (hal qiluvchi jadval topmasa `null`, so'rov qatlami jadvalning ustida) → almashtiradigan reja va yon berish daqiqalari jadvaldan; prod'da jadvallar biriktirilgach ishlaydi
- [2026-10-17] Kun almashtirishda reja qachon olinadi → so'rov YARATILGANDA (ikkala sananing amaldagi rejasi suratga olinib saqlanadi) → HR tasdiqlagan narsa u ko'rgan narsa; keyin jadval o'zgarsa so'rov o'zgarmaydi
- [2026-10-17] Jadval so'rovi kimga qaror beradi → faqat HR (`attendance.requests.manage`), yo'qlik so'rovi bilan bir xil; Verifix'dagi ikki bosqich («Подтвердил» → «Завершил») olinmadi → SPEC-so'rovlar A5 (faqat HR qaror qiladi)
- [2026-10-17] Navigatsiya nishoni → yo'qlik va jadval so'rovlari BITTA sonda (`/requests/pending-count`) → HR uchun ikkalasi ham «qaror kutayotgan so'rov»
- [2026-10-17] Ilovada yo'qlik formasi qaysi rejimda ochiladi → «Qism kun» → Verifix'da ham sukut «Soatlik»; amalda so'rovlarning ko'pi soatlik
- [2026-10-17] Kun almashtirish juftlari telefonda qanday → ikki sana ustma-ust (sana, ⇅, sana) → 320–360 px ekranda yonma-yon sig'maydi
- [2026-10-17] Ilova Bosh sahifadagi «So'rovlarim» kartasi va taqvim kun paneli jadval so'rovini ko'rsatadimi → hozircha yo'q, faqat «So'rovlar» ro'yxati va tafsiloti → keyingi qadam
- [2026-10-17] HR «Jadval o'zgarishi» navbatida filtr bormi → yo'q (holat bo'yicha ikki jadval: kutayotgan / qaror qabul qilingan) → API'da tur filtri yo'q, yo'qlik filtrlari unga tegishli emas; navbat almashtirgichi URL'da emas, komponent holatida
- [2026-10-17] Jadval so'rovi kunida tanaffus daqiqasi oynadan alohidami → ha, saqlanadi (`break_minutes`); almashtirish jadvalnikini ko'chiradi, forma o'zgartirilmagan tanaffusda rejaning qiymatini qaytaradi, aks holda oyna uzunligi → qa-review: 60 daq 12:00–15:00 oynasida 180 ga aylanardi
- [2026-10-17] Xodim ilovadan qaysi sanalarni so'ray oladi → bugundan −31 … +92 kun; HR uchun chegara yo'q (faqat 2000–2100 yil va bitta so'rovda 92 kunlik davr) → qa-security H1: cheklanmagan ro'yxat 2 GB serverni og'irlashtirardi; o'tgan oy kechikkan so'rov uchun yetarli
- [2026-10-17] Jadval so'rovini yaratish/tasdiqlash qaysi ruxsat → `attendance.requests.manage` («O'zgartirish»), `attendance.manage` emas → yo'qlik so'rovlari bilan bitta HR navbati; tasdiq bitta xodimning bitta sanasini o'zgartiradi, jadvalni emas; prod'da faqat tizim rollari (hr_manager, super_admin — ikkalasida ham ikkala ruxsat), ya'ni bugun hech kimga ta'sir qilmaydi. Egasiga aytildi
- [2026-10-17] qa-security L3/L4/L5 (HR o'z so'rovini tasdiqlashi, bekor/qaror poygasi, qidiruvda JSHSHIR/pasport bo'yicha qism moslik) → hozircha qoldirildi → yo'qlik so'rovlarida ham bor eski naqsh; alohida to'lqinda ikkala so'rov turi uchun birga tuzatiladi
- [2026-10-17] Faqat kunlari o'zgargan tahrir auditda kimga bog'lanadi → kun qatorlari ota so'rovning xodimiga (`SubjectVia`), xodim tarixida ko'rinadi; ota qatorining o'zi (UpdatedBy) bunday tahrirda yozilmaydi → qo'shimcha «revision» ustuni hozircha kerak emas
- [2026-10-17] Formada qaysi harakat jadvaldagi tanaffus daqiqasini tushiradi → tanaffus boshi/oxiri VA «Tanaffus» belgisini o'zgartirish (o'chirish = tanaffusni tozalash); smena vaqti, kun turi va sana emas; jadvali yo'q sanaga o'tilsa daqiqa tushiriladi; HR tahririda saqlangan kunning daqiqasi qaytariladi → aks holda PUT 60 daqiqani 180 ga aylantirardi; ilovada varaqni o'sha vaqt bilan tasdiqlash tegish hisoblanmaydi
- [2026-10-20] «Erkin ish jadvali» qayerda saqlanadi → har bir KUN darajasida: haftalik naqsh kuni (`work_schedule_days`), yillik to'r katagi (`work_schedule_dates`), jadval so'rovi kuni (`schedule_change_request_days`); jadval darajasida emas → Verifix formasi sana bo'yicha; dushanba–juma erkin, shanba qat'iy kabi aralash jadval ham mumkin (`docs/SPEC-verifikatsiya-erkin-jadval.md` §3)
- [2026-10-20] Erkin kunda kechikish va erta ketish → hisoblanmaydi (0); kun ishlangan vaqt va «Ish vaqti» (talab) bo'yicha baholanadi, kami `shortfall_minutes` («Kam ishlangan») → egasining Verifix formasi bo'yicha taklifi
- [2026-10-20] Erkin kunda qaysi borliq hisoblanadi → EGASI HAL QILDI: BUTUN vaqt hisoblanadi, oynaga kesilmaydi (08:30–18:00, 7:30 talab, 08:00–17:30 ishladi → kamchilik yo'q); oyna faqat ma'lumot uchun → egasining javobi (AskUserQuestion), SPEC boshidagi «Owner override» (avvalgi taxmin: oynaga kesish — bekor)
- [2026-10-20] Erkin kunda tanaffus → bugungi qoidaning o'zi (oyna bo'lsa binoda bo'lgan qismi ushlanadi, oynasiz — ushlanmaydi), haqiqiy (kesilmagan) oraliqlar ustida → qat'iy kun bilan bir xil yo'l, «ushlanma ≤ ishlangan» kafolati saqlanadi
- [2026-10-20] «Ish vaqti» (talab) sukuti va chegarasi → sukut = smena uzunligi − tanaffus daqiqasi (08:30–18:00, 120 daq → 7:30); ruxsat 1 … (smena − tanaffus) daqiqa, aks holda `davomat.jadval_erkin_vaqti` (400) → talab smena − tanaffusdan oshmasin (jadvaldagi ish kuni normasi shu; ko'prog'i — boshqa jadval); berilmasa server sukutni o'zi yozadi
- [2026-10-20] Dam olish kunida «erkin» belgisi → jimgina tozalanadi (xato emas); erkin bo'lmagan kunga yuborilgan `requiredMinutes` e'tiborsiz → dam olish kunidagi tanaffus oynasi bilan bir xil qoida (SPEC §2.4.0)
- [2026-10-20] «Kam ishlangan» qachon hisoblanadi → faqat erkin ish kuni, yakuniy holat «Ishladi» (Present) va reja oynasi tugagach; Kelmadi/Sababli/Chiqish yo'q/Kirish yo'q kunlarda 0; oyna tugashidan oldin yozilgan qatorda 0 (tungi hisoblash to'ldiradi) → erta ketishdagi «qachon ketgani noma'lum bo'lsa ayblanmaydi» qoidasi; kelmagan kun holati bilan sanaladi, ikki marta emas
- [2026-10-20] Erkin kunda «Hali boshlanmagan» qachongacha → eng kech boshlanishgacha = oyna tugashi − «Ish vaqti» (08:30–18:00, 7:30 → 10:30), keyin «Kelmadi»; tanaffus hisobga olinmaydi → kech kelishi mumkin bo'lgan xodim ertalabdan «Kelmadi» bo'lib turmasin; optimistik chegara erta ayblamaydi; panel, tabel (`Planned`), boshlangan smenalar vazifasi va trend — bitta qoida (`DashboardStatusRule.HasNotStarted`)
- [2026-10-20] Panelda erkin kun → kelgan bo'lsa doim «Vaqtida» (OnTime), «Kechikdi» hech qachon; yangi panel holati qo'shilmadi → «Keldi (grafiksiz)» jadvalsizlar uchun band; panel donut'i va trend o'zgarmaydi
- [2026-10-20] Tasdiqlangan jadval so'rovi erkin kunda → so'rov kuni rejani TO'LIQ almashtiradi, erkinlik ham so'rov kunidan (birlashtirilmaydi); formalar rejadan to'ldiradi, ya'ni erkin kun belgi olib tashlanmasa erkin qoladi; kun almashtirishda juftining erkinligi ko'chadi → mavjud qatlam qoidasi (SPEC-jadval-sorovi §C2)
- [2026-10-20] «Kam ishlangan» qayerda ko'rinadi → tabel katagi va jami, xodim kartasi kalendari, xodim ilovasi, xulosa hisoboti ustuni, chetlanishlarda yangi «Kam ishlaganlar» (`ExceptionKind.Shortfall = 5`), Excel; MCP vositalari va tashqi API — bu to'lqinda yo'q → aks holda erkin xodim hisobotlarda doim «toza» ko'rinardi; rahbar boti kerak bo'lsa alohida
- [2026-10-20] Excel'da yangi ustunlar qayerga → har varaqning OXIRIGA → mavjud ustun o'rinlari (testlar, HR formulalari) o'zgarmaydi
- [2026-10-20] Erkin kun CHECK cheklovida `is_working` sharti bormi → yo'q: faqat «erkin ⇔ talab bor, 1…1440» → rollback'da eski image erkin kunni dam olish kuniga aylantirsa saqlash yiqilmasin; yangi kod ish kuni bo'lmagan kunda erkinlikni o'qimaydi va keyingi saqlashda tozalaydi
- [2026-10-20] Yo'qlik turlari prod'da qanday moslanadi → migratsiya ham, SQL ham YO'Q: deploy'dan keyin egasi (yoki catalog.manage'li HR) «Ma'lumotnomalar → Yo'qlik turlari» da «Shaxsiy sabab» → «Shaxsiy» (nom), «Kasallik» → faolsizlantirish; urug' fayli faqat bo'sh bazaga ta'sir qiladi → ma'lumotnoma — ma'lumot (CatalogSeeder qoidasi), amal auditga tushadi va o'sha sahifadan qaytariladi
- [2026-10-20] «Kasallik» bo'yicha mavjud so'rovlar → turi va nomi saqlanadi, filtrda ko'rinadi, qaror/bekor qilish ishlaydi; kutayotganini HR tahrirlasa turni almashtirishi kerak (`davomat.yoqlik_turi_faolsiz`) → faolsiz tur yangi yozuvda tanlanmaydi (mavjud qoida); kunlar qayta hisoblanmaydi (bayroqlar o'zgarmadi)
- [2026-10-20] «Shaxsiy» nomi allaqachon bor bo'lsa (409 `malumotnoma.nom_band`) → birlashtirilmaydi, to'xtab egasiga aytiladi → ikki turni qo'shish so'rovlar tarixini o'zgartiradi
- [2026-10-20] So'rov turi varag'idagi 5 yangi tur (Belgilash, Joylashuv so'rovi, Qo'shimcha ish vaqti, Ishdan bo'shatish, Ta'til) → ro'yxatda Verifix tartibida turadi, lekin nofaol, «Tez orada» yozuvi bilan → egasi «hammasi bo'lsin» dedi, formalari skrinshotlar kelgach loyihalanadi; yashirish varaqni Verifix'dan farqli qilardi
- [2026-10-20] Varaqdagi tavsif satri → olib tashlandi (nishon + nom, faol qatorda chevron) → Verifix'da ham yo'q; sakkizta ikki qatorli qator 320×568 ekranga sig'maydi
- [2026-10-20] «Kun almashtirish» va «Jadval o'zgartirish» uchun alohida forma kerakmi → yo'q, mavjud forma; tur manzildan oldindan tanlanadi (`?tur=almashtirish` → Swap, qolgani → Change), segment joyida qoladi, sarlavha turga qarab → qayta yozish yo'q
- [2026-10-20] Ilovada «Ish vaqti» (7:30) qanday tanlanadi → yangi `DurationRow`: varaqda soat (0–23) va daqiqa (00–59) uchun ikkita native `select`; `input type=time` emas → 12 soatlik telefonda u «7:30 AM» ko'rsatardi; webda «soat:daqiqa» matn maydoni
- [2026-10-20] «Ish vaqti» smena/tanaffus o'zgarganda qayta hisoblanadimi → qiymat sukutga teng bo'lsa (yoki foydalanuvchi tegmagan bo'lsa) — sukut bilan birga o'zgaradi va `null` yuboriladi (server hisoblaydi); foydalanuvchi boshqa qiymat tanlasa — qotadi → Verifix formasidagi kabi tayyor qiymatdan boshlanadi, keyingi tahrir uni buzmaydi
- [2026-10-20] Yillik to'r katagida erkinlik qanday ko'rinadi → katakda soat (talab), erkinlik faqat izohda (tooltip) va tahrir panelida; jadval kartasida «Erkin jadval» belgisi → Verifix to'ri ham faqat soat ko'rsatadi; yangi vizual uslub qo'shilmaydi
- [2026-10-20] «Belgilash» so'rov turi nomi xodim ilovasidagi «Belgilash» (telefondan kirish/chiqish) so'zi bilan to'qnashadi → varaqda egasi bergan nom qoladi; forma loyihalanganda sarlavha «Belgilash so'rovi» bo'ladi → UX lug'ati (§F.7) bitta so'z — bitta tushuncha
- [2026-10-20] №91 (10) bilan solishtirish: Excel «ЙЎНАЛИШИ» ustunidagi «"Viсor metan"» da kirill «с» bor → yo'nalish nomi lotin «"Vicor metan" МЧЖ ҚК» qilib ochildi; «"Velo union" МЧЖ» oxiridagi bo'sh joy olib tashlandi → tizimdagi nom toza bo'lsin
- [2026-10-20] Propan bo'limida turgan, lekin Excel'da yo'nalishi «Viсor metan» deb yozilgan Уринбоев Улмасжон → Excel'dagidek «Vicor metan» → egasi «tizim Excel'dagi kabi bo'lsin» dedi
- [2026-10-20] 91.6 dagi ketish sababi katalogda aynan yo'q bo'lsa → sabab bo'sh, matn izohga (Зиятбоев «12 соатлик иш режимида ишламас экан»); «Бошқа иш топган» → «Бошқа ишга ўтиб кетди» → HR ham bo'shatishda sababni bo'sh qoldirib, izoh yozadi (prod'dagi namunalar)
- [2026-10-20] Жўраева Нафисада ikki faol ish joyi (import) → tarixi bor asosiysi qoladi, o'zining 2026-08-06 o'tkazmasiga mos «Пропан бўлими» + Excel'dagi «Velo union»; bo'sh takrori yumshoq o'chirildi → tarix yo'qolmasin
- [2026-10-20] Ўктамов Хасанжonda ikki karta (import + HR) → Excel'ga mos HR kartasi qoladi, import kartasi (ilova hisobi bilan) yumshoq o'chirildi, undagi oklad 5 000 000 HR kartasiga hodisa bilan ko'chirildi → bitta telefonga ikki ilova hisobi bo'lmasin, oklad yo'qolmasin
- [2026-10-20] Metan bo'limlaridagi 22 Propan xodimi → bo'limi O'ZGARTIRILMADI, faqat yo'nalish → egasi «hozircha shu 2 umumiy yo'nalishni o'zgartir» dedi; bo'lim ajratish keyin, egasi aytsa
- [2026-10-20] Propan zavodi uchun bo'lim nomlari (tizimda nom noyob) → «Краска бўлими (Пропан)» kabi 7 ta yangi bo'lim «Вело унион (Пропан) заводи» ostida; Metandagilar o'zgarmadi → egasi «22 tasi Propanga o'tsin, bo'limga ham teg» dedi, Metan nomlari tegilmasin
- [2026-10-20] Ko'chirilgan 22 kishining mehnat yo'lidagi eski (Metan) bo'limga ishoralari ham yangi bo'limga → tuzatish ko'chirish emas, joylashuv xatosi; aks holda kartada bo'lmagan «o'tkazma» ko'rinardi
- [2026-10-20] Excel'da bor, tizimda yo'q 5 kishi (11.10 da ushlab qolingan) → egasi «Excel bo'yicha bo'lishi kerak» → qo'shildi: faqat joriy ish joyi + «Ишга қабул», eski davrlar qo'shilmadi; Фазилов «tekshirilsin» belgisi bilan (eski qora ro'yxat); Абдуллаев Б. mehnat.uz havolasidagi JSHSHIR boshqa odamniki → havola qo'yilmadi; yuridik shaxs bo'sh → MBF (11.10 qoidasi)
- [2026-10-20] Уринбоев Улмасжон yo'nalishi → «Velo union» (Excel ustunida «Vicor metan», lekin Propan bo'limida) → egasining keyingi aniq ko'rsatmasi «22 tasi Velo union Propanga»
- [2026-10-20] Excel'dagi Drive rasmlari → faqat rasmi YO'Q xodimlarga qo'yildi (284), bor 164 rasmga tegilmadi → HR qo'lda qirqib qo'ygan rasm terminal uchun yaxshiroq bo'lishi mumkin; almashtirish kerak bo'lsa egasi aytadi
- [2026-10-20] Webdagi «Ish vaqti» maydonida `inputMode="numeric"` (SPEC-erkin-jadval §7.1) → qo'yilmadi, oddiy matn klaviaturasi → raqamli klaviaturada «:» yo'q, planshetda «7:30» ni yozib bo'lmasdi
- [2026-10-20] Tabel oynachasidagi erkin kun satri (SPEC'dagi «{{range}} oralig'idagi vaqt hisoblanadi») → neytral matn: «Erkin jadval: 08:30–18:00 · kechikish va erta ketish hisoblanmaydi, kun davomidagi butun vaqt hisoblanadi» → egasining 2026-10-20 qarori: vaqt oraliq bilan qirqilmaydi
- [2026-10-20] «Kam ishlaganlar» tabining ustunlari → xodim, sana, birinchi kirish, oxirgi chiqish, kam ishlangan, ishlangan, holat (reja ustunlari yo'q) → erkin kunda boshlanish–tugash faqat ma'lumot, savol «qancha kam»
- [2026-10-20] Xodim kartasidagi oylik «Kam ishlangan» → faqat oyda erkin kun bo'lsa yoki qiymat > 0 bo'lsa ko'rinadi; tabel oynachasida doim → oddiy jadvaldagi har xodimda «0:00» shovqin bo'lardi
- [2026-10-20] Ilovadagi erkin kun matnlari (`schedule.requiredHint`, `calendar.scheduleFlexibleRange`, `checkin.hintFlexible`) SPEC §7.3 dagi «{{range}} oralig'ida» o'rniga «Jadval {{range}}» → egasining «butun vaqt hisoblanadi» qarori: oraliq cheklov emas, ma'lumot
- [2026-10-20] Xodim qaysi so'rovni o'zgartira oladi (`docs/SPEC-verifikatsiya-sorov-tahriri.md`) → faqat o'ziniki va faqat «Kutilmoqda» holatida, HR uning nomidan kiritgani ham; qaror qabul qilingan yoki bekor qilinganda 409 `davomat.sorov_qaror_qabul_qilingan` (domenning mavjud kodi) → egasi «qaror qabul qilinguncha»; xodimning bekor qilish qoidasi bilan bir xil
- [2026-10-20] Tahrir HR qarori bilan poygalashsa → so'rov qatorida `version` (EF concurrency token, har bir o'zgarishda +1) + mijoz yuborgan `version` (xodim tahririda majburiy, HR tahriri va qarorida ixtiyoriy); mos kelmasa yoki parallel yozuv yutqazsa 409 `davomat.sorov_ozgargan`, hech qachon 500 → HR eskirgan nusxa ustida qaror qilmasin; qa-security L4 (bekor/qaror poygasi) ham shu bilan yopiladi
- [2026-10-20] Tahrir haqida HR ga bildirishnoma → yangi bildirishnoma yo'q; navbat qatori va qaror oynasida «Ilovada o'zgartirilgan · sana» (`edited_at`, `edited_by_account_id`), qolgani auditda → yaratishda ham bildirishnoma yo'q, nav nishoni kutayotganlarni jonli sanaydi; yangi kanal so'ralmagan
- [2026-10-20] HR tomonda tahrir kerakmi → yangi narsa yo'q: HR kutayotgan ikkala turdagi so'rovni allaqachon tahrirlaydi; faqat `version` bilan himoyalanadi va `edited_at` yoziladi → so'ralmagan; har qanday turni tasdiqlashga to'siq yo'qligi tekshirildi
- [2026-10-20] Ilovada «O'zgartirish» tugmasi qayerda → tafsilot ekranida, «So'rovni bekor qilish» ustida, ikkinchi darajali to'liq kenglikdagi tugma (navigatsiya panelidagi matn emas); forma — mavjud yaratish formasi tahrir rejimida, asosiy tugma «Saqlash» → UX §E.3 (to'liq so'z, 48 px); ru/uz-Cyrl da sarlavhani siqmaydi
- [2026-10-20] So'rov turi HR uchungina yoki faolsiz bo'lsa xodim tahrirlay oladimi → tugma ko'rinmaydi, faqat bekor qilish → server yaratishdagi qoidani qo'llaydi va forma HR tanlagan turni almashtirishga majburlardi
- [2026-10-20] Tanaffus qoidasining 4 soni qayerda → tizim sozlamalari `attendance.requestBreakFrom/To/Minutes/FridayMaxMinutes` (sukut 720/900/60/120), tahrir — «Ish jadvallari» sahifasidagi karta (`settings.manage`); xodim ilovasi `GET /api/me`, HR formasi `GET /schedule-requests/break-rule` (`attendance.view`) orqali oladi → davomatning mavjud sozlama naqshi (saqlash muddatlari kartasi), migratsiya kerak emas
- [2026-10-20] Jumada tanaffus uzunligi → 60–120 daqiqa (pastki chegara — oddiy kunning uzunligi), 12:00–15:00 ichida → egasi «120 gacha» dedi
- [2026-10-20] Qoida tekshiradigan «amaldagi tanaffus uzunligi» → saqlanadigan `break_minutes` ning o'zi: berilgan `breakMinutes`, bo'lmasa oyna uzunligi, oynasiz 0; 0 — tanaffussiz kun (tekshirilmaydi); oyna bo'lsa u 12:00–15:00 ichida va uzunlikdan qisqa bo'lmasin; oynasiz uzunlik faqat uzunlik bo'yicha → jadvaldan tegilmagan oddiy kun (12:00–15:00 oynasi, 60 daq) o'tadi, hisob aynan shu sonni ishlatadi
- [2026-10-20] Tanaffus qoidasi qaysi so'rovlarga → «Kunni o'zgartirish» ning ish kunlari, ilova VA HR, yaratish va tahrirda; «Kun almashtirish» (kunni server jadvaldan oladi), dam olish kuni, qaror va bekor qilishda — yo'q; mavjud so'rovlar qayta tekshirilmaydi → egasi; almashtirishda xodim tanaffus tanlamaydi
- [2026-10-20] Tungi smena tanaffusi (00:30–01:30) → qoida istisnosiz: bunday kun faqat tanaffussiz so'raladi → egasi qoidani istisnosiz aytdi; kerak bo'lsa 12:00–15:00 ga tegmaydigan smenalar ozod qilinadi (SPEC §12 Q2)
- [2026-10-20] Mavjud integratsiya testlari 120 daqiqalik tanaffusli so'rov yuboradi va sanasi bugunga nisbatan → test sanasi jumaga suriladi yoki tanaffus 60 daqiqa qilinadi, tasdiq mazmuni saqlanadi; qoida testda o'chirilmaydi → aks holda testlar hafta kuniga qarab yiqilardi
- [2026-10-20] Qaysi turlar «shaxsiy vaqt» → `counts_as_work_time = false` bo'lgan har bir tur, faqat VAQTLI (qism kun) tasdiqlangan so'rov; butun/ko'p kunlik so'rov avvalgidek faqat «Sababli» → egasi: nom emas, bayroq bo'yicha; katakda yorliq doim «Shaxsiy vaqt»
- [2026-10-20] Shaxsiy oraliqdagi borliq (chiqmagan yoki erta qaytgan) → ishlangan vaqtdan olib tashlanadi; oraliqdagi bo'shliq «tanaffus olingan» hisoblanmaydi → egasining misoli (chiqishsiz 17:35); noto'g'ri oraliqni HR bekor qiladi, xodim qayta yuboradi
- [2026-10-20] Egasining misolidagi 08:25–08:30 va 17:30–17:35 → qat'iy kunda shaxsiy vaqt bo'lsa ishlangan vaqt «reja − shaxsiy» dan oshmaydi (6:00, ortiqcha ish 0) → egasi aynan 6:00 dedi; reja tashqarisidagi borliq haq to'lanmaydigan vaqtni «sotib olmaydi» (Verifix'da ortiqcha ish so'rov bilan); oynasiz jadvalda bu tushlikni ham ushlaydi (SPEC E20)
- [2026-10-20] Erkin kunda shaxsiy vaqt → ishlangan vaqt cheklanmaydi; «Kam ishlangan» = norma − shaxsiy − ishlangan; eng kech kelish = oyna oxiri − (norma − shaxsiy) → egasi: erkin kunda butun borliq hisoblanadi, kam ishlangan shaxsiy vaqt uchun jazolamasin
- [2026-10-20] Tushlik shaxsiy oraliq ichida → tanaffus oynasining shaxsiy bo'lmagan qismi tanaffus uzunligidan qisqa bo'lsa, yetmagani shaxsiy vaqt ichida deb olinadi va shaxsiy daqiqadan ayriladi (12:00–15:00 shaxsiy, 60 daq tushlik → 2:00) → tushlik baribir haq to'lanmaydi; «ishlangan + shaxsiy = reja» saqlanadi
- [2026-10-20] Shaxsiy vaqt va kechikish/erta ketish → oraliq reja boshini qoplasa kutilgan kelish oraliq oxiriga suriladi (yon berish shundan), oxirini qoplasa kutilgan ketish oraliq boshiga; kun o'rtasidagi shaxsiy vaqt kechikish/erta ketish daqiqasidan ayriladi → «ish vaqti» so'rovidagi kechikish natijasi bilan bir xil
- [2026-10-20] Umuman kelmagan, shaxsiy oraliq rejaning bir qismida → «Kelmadi» (avval «Sababli» edi); butun rejani qoplasa (erkin kunda: shaxsiy ≥ norma) — «Sababli» → shaxsiy so'rov faqat o'z soatlarini oqlaydi; buning uchun vaqtli shaxsiy so'rov `ExcusedDatesAsync` dan chiqariladi
- [2026-10-20] Reja boshidagi shaxsiy oraliq tugamagan, xodim hali kelmagan → oraliq tugaguncha «Hali boshlanmagan» (tabelda qator yo'q), keyin «Kelmadi» → mavjud «Hali boshlanmagan» mexanizmi (erkin kundagi kabi), yangi panel holati yo'q; «Sababli» varianti SPEC §12 Q3
- [2026-10-20] «Ish vaqti» so'rovi va shaxsiy vaqt bir smenada kesishsa (tungi smena) → shaxsiy ustun: kredit shaxsiy oraliqni qoplamaydi → aniqroq so'rov; ziddiyatli ma'lumot pul yasamasin
- [2026-10-20] `personal_minutes` hozirgi lahzaga kesiladimi → yo'q, tasdiqlangan oraliqning rejaga tushgan qismi → reja ma'lumoti; ishlangan vaqtga faqat haqiqiy borliq ta'sir qiladi
- [2026-10-20] Shaxsiy vaqt qayerda ko'rinadi → tabel katagi va jami, xodim kartasi, ilova kun paneli, «Bugungi holat» va oylik natija, xulosa hisoboti, Excel (oxirgi ustunlar); MCP va tashqi API — yo'q → erkin jadvaldagi «Kam ishlangan» bilan bir xil qamrov
- [2026-10-20] Yo'qlik so'rovi izohi uzunligi → 2000 belgi, `davomat.izoh_uzun` (yaratish, tahrir, qaror izohi) → ustun varchar(2000), avval uzun izoh 500 berardi; jadval so'rovidagi qoida bilan bir xil
- [2026-10-20] Formada yo'qlik turi tanlanganda izoh → ilovada `MyAbsenceTypeDto.countsAsWorkTime` bo'yicha «Ish vaqti hisoblanadi» / «Sababli, lekin ish vaqtiga kirmaydi», HR formasida ham → xodim va HR ishlangan vaqt nega kamayishini oldindan bilsin
- [2026-10-20] Ma'lumotnomadagi «Ish vaqti hisoblanmaydi» izohi (`catalog.workTimeHintOff`) → yangilanadi: vaqtli so'rov ishlangan vaqtdan ayriladi, butun kunlik «Sababli» qiladi → eski matn endi noto'g'ri
- [2026-10-20] Eski tasdiqlangan vaqtli «Shaxsiy sabab» so'rovlari → avtomatik qayta hisoblanmaydi (tungi hisob oxirgi 3 kunni, qolgani `POST /recompute`) → prod'da jadval biriktirilmagan (2026-10-20: 0), ta'siri yo'q
- [2026-10-20] Ikki tahrir bitta jadval so'rovining kunlarini bir vaqtda almashtirsa (bir xil sana) ikkinchisining yangi kuni `ix_schedule_change_request_days_request_id_date` ga uriladi — EF uni versiya tekshiruvidan oldin yozadi → bu unikallik buzilishi ham «eskirgan yozuv» deb taniladi (`PostgresFault.IsStaleRequestWrite`, 409 `davomat.sorov_ozgargan`) → domen takror sanani rad etadi, ya'ni bu indeksga faqat parallel tahrir uriladi; aks holda 500 bo'lardi (SPEC §2.3 faqat `DbUpdateConcurrencyException` ni sanagan; `RequestEditApiTests` da isbotlangan)
- [2026-10-20] HR webida qaror/tahrir 409 `davomat.sorov_ozgargan` yoki `…_qaror_qabul_qilingan` olsa → ogohlantirish (sariq) Alert: sarlavha — xato matni, ostida yangi `attendance.requestStaleHint` («Ro'yxat allaqachon yangilandi…»); qaror oynasida faqat «Yopish», tahrir oynasida «Saqlash» o'chiq; navbatlar (ikkala tur + nishon) `onError` da yangilanadi, bekor qilish mutatsiyalarida ham → `providers.tsx` keshni faqat muvaffaqiyatda bekor qiladi; HR nima qilishni bilsin
- [2026-10-20] Yo'qlik so'rovini «Tasdiqlash» oynasidagi umumiy «kunlar Sababli bo'ladi» matni → tur ma'lumotnomadan topilsa `requestKindWorkTime` / `requestKindPersonalTime` bilan almashtiriladi (topilmasa — eski matn) → vaqtli shaxsiy so'rov uchun eski matn endi noto'g'ri; SPEC §7.2 da faqat forma uchun aytilgan, qaror oynasi ham shu kalitlarni ishlatadi
- [2026-10-20] «Jadval so'rovidagi tanaffus» kartasi → kalitlararo qoidani (`from < to`, `daqiqa ≤ juma`, `juma ≤ to − from`) oldindan tekshiradi va «Saqlash» ni o'chiradi (matn — `davomat.tanaffus_qoidasi_nomuvofiq`), bo'shatilgan vaqt maydoniga yangi `attendance.breakRuleTimeEmpty`; to'g'ri to'rtlikda formalar talab qiladigan qoida (`requestBreakRuleHint`) ko'rsatiladi → server baribir tekshiradi, HR esa xatoni bosishdan oldin ko'radi
- [2026-10-20] Web `ru` dagi `davomat.sorov_ozgargan` → «Запрос изменили…» (ilovadagi «Заявку…» emas) → web ru da so'rov doim «запрос» (`davomat.sorov_qaror_qabul_qilingan` va boshqalar); uz/en matnlari ilovadagi bilan aynan bir xil
- [2026-10-20] HR webidagi izoh maydonlari (yo'qlik so'rovi izohi, ikkala qaror izohi) → `maxLength` 2000 (jadval so'rovi izohidagi kabi) → `davomat.izoh_uzun` endi yaratish/tahrir/qarorda ham bor; server baribir tekshiradi
- [2026-10-20] Ro'yxatlarda «from > to» uchun qaysi xato kodi (rag'bat va buyruqlarda tekshiruv umuman yo'q) → bitta umumiy `royxat.sana_oraligi_notogri` (xodimlar, ularning Excel eksporti, nomzodlar, sinov, belgilar), 4 tilda; davomat so'rovlari o'zining mavjud `davomat.davr_notogri` kodida qoladi → xato filtr shakliga tegishli, matni hamma joyda bir xil; davomatdagi kod oldindan bor va web uni biladi
- [2026-10-20] «Xodimlar» sahifasida qaysi holatda qaysi sana → `status=Terminated` — ishdan ketish sanasi, qolgan har qanday holat (employed, Probation, Active, bo'sh) — ishga kirish sanasi; ikkalasi ham ro'yxatda KO'RINADIGAN munosabatniki (asosiysi, bo'lmasa oxirgisi), shart munosabat tanlanganidan keyin → qatordagi sana doim oraliq ichida bo'lsin; o'rindoshlikdagi yangi munosabat 2015-yildan ishlayotgan odamni «oktabrda kirganlar»ga olib kirmasin
- [2026-10-20] Nomzodlarda sana ma'nosini nima almashtiradi → faqat `rejected=true` (Rad etilganlar bo'limi) — `closed_at` (rad etilgan / voz kechgan kun); boshqa hamma holatda, holat tanlovi qanday bo'lmasin — ariza sanasi (`createdAt`, Toshkent kuni) → «Nomzodlar» sahifasidagi holat tanlovi sana ma'nosini jimgina almashtirmasin; HR ikkala savolni ikki sahifada so'ray oladi
- [2026-10-20] Rezerv / Qora ro'yxat sahifalarining NOMZODLAR tabi (`/api/recruiting/candidates?status=Reserve|Blacklisted`) → ariza sanasi bo'yicha (yuqoridagi qoida), belgi boshlanishi kabi «rezervga olingan kun» emas → egasining jadvali xodim belgilari haqida; bitta endpoint uchun bitta qoida. Kerak bo'lsa `SearchCandidatesQueryHandler` dagi `byClosedDate` ga shu ikki holat qo'shiladi
- [2026-10-20] Sinov ro'yxatida «tugash sanasi» → rejadagi `end_date` (qaror sanasi `resolved_at` emas) → ro'yxat shu sana bo'yicha tartiblangan va HR «kimning sinovi qachon tugaydi» deb so'raydi
- [2026-10-20] Belgilar ro'yxatida sana oralig'i va «faqat kuchdagilar» → ikkalasi VA bilan birlashadi (sukut `onlyActive=true` o'zgarmadi) → mavjud filtr buzilmasin; tarix uchun web `onlyActive=false` yuboradi
- [2026-10-20] Jadval so'rovlarining «from/to» si ikki alohida `Any` edi (1- va 30-oktabr kunli so'rov 10–20-oktabrda ham chiqardi) → bitta `Any(kun ichida)` ga tuzatildi; almashtirishning ikkala sanasi kun bo'lib saqlanadi, ya'ni ikkalasi ham hisoblanadi → egasining «birorta kuni oraliq ichida» talabi
- [2026-10-20] Chekka sanalar (`0001-01-01`, `9999-12-31`) → 400 emas, ochiq chegara deb olinadi (lahza ustunida Toshkent yarim tunini hisoblash shu ikki kunda 500 berardi) → audit jurnalidagi 2000–2100 cheklovi bu yerda yaramaydi: ishga kirish sanalari o'n yillab orqaga ketadi
- [2026-10-20] Yangi indeks kerakmi → yo'q, migratsiya yo'q: munosabat (`status, hire_date`, `termination_date`), sinov (`status, end_date`), belgi (`type, start_date, end_date`) indekslari bor; nomzodlarda `created_at`/`closed_at` indekssiz, lekin ~5 100 qator va sukut saralash ham shu ustunda → indeks yozishni sekinlatadi, o'qishda sezilarli foyda yo'q
- [2026-10-20] Xodimlar eksportining audit tafsiloti → filtrga `dateFrom`/`dateTo` qo'shildi (shaxsiy ma'lumot emas) → «kim qaysi filtr bilan yukladi» savoliga to'liq javob bo'lsin
- [2026-10-21] Web: teskari oraliq («from > to») qanday to'xtatiladi → sana maydonlari taqvimda bir-birini cheklaydi (`min`/`max`), qo'lda terilgani esa so'rov YUBORMAYDI: ro'yxat ustida ogohlantirish (`royxat.sana_oraligi_notogri`, davomatda `davomat.davr_notogri`), jadval bo'sh, Excel tugmasi o'chiq → `attendance-passes-page.tsx` dagi mavjud naqsh; server baribir tekshiradi
- [2026-10-21] Web: ikki bo'limli sahifalarda (Rezerv / Qora ro'yxat: nomzodlar–xodimlar; So'rovlar: yo'qlik–jadval) oraliq bo'limlarga umumiy → manzilda bitta `from`/`to`, qidiruv kabi; yorliq bo'limga qarab ma'nosini aytadi (ariza kelgan / belgi qo'yilgan sana) → HR bo'lim almashtirganda savol qolsin; bo'limga xos oraliq ikki marta terishni talab qilardi
- [2026-10-21] Web: davomat so'rovlaridagi yorliq → umumiy «Sanadan / Sanagacha» + ostida izoh («davri shu oraliqqa tushgan, qisman bo'lsa ham») → «qisman tegadi» degan ma'no bitta yorliqqa sig'maydi; hisobotlar sahifasidagi yorliqlar bilan bir xil. Segment sanoqlari (kutayotganlar) oraliqqa BOG'LANMAYDI — ular menyu nishoni bilan teng
- [2026-10-21] Web: «Nomzodlar» sahifasidagi «Rezervda ham bor» / «Qora ro'yxatda ham bor» izohlari → sana oralig'ini OLMAYDI → ular takroriy kiritishdan ogohlantiradi: odam boshqa oyda kelgan bo'lsa ham bor-yo'qligini aytishi kerak
- [2026-10-22] Web: sahifa sarlavhasi yuqori panelga ko'chganda sahifa HARAKATLARI qayerga → panelga faqat TUGMALAR («Qo'shish», «Eksport», «Qayta hisoblash»); davr, sana, tablar, ko'rinish almashtirgichi sahifada, o'ng tomonda (`toolbar`) → egasi: «sana bo'yicha filterlar joyida qolsin»; uzun tavsif (`description`) panelda ko'rinmaydi — «ortiqcha ma'lumot»
- [2026-10-22] Web: filtr panelidagi sana oralig'i → hamma ro'yxatda bitta katak, «yorliq + [sanadan] – [sanagacha]» (`DateRangeField`), panel maydonlari ixcham (`size="sm"`, h-10) → egasi: «bittasining o'rniga 2 tasi sig'sin», «hamma qismlarda bir xil»; izohlar (nomzod holati, «bugun aloqa kerak») sichqoncha ostiga
- [2026-10-22] Web: paneldagi ixcham sarlavhada sahifa ikonkasi → yashiriladi → o'sha ikonka yon menyuda faol band ustida turibdi; ikki qatorli matn yonida faqat joy olardi
- [2026-10-22] Web: tor ekranda (<1024 px) sarlavha → avvalgidek sahifa ichida → panelda qidiruv va boshqaruvlarga ham joy zo'rg'a yetadi
- [2026-10-22] Web: bir qatorli jadvalda xodim rasmi → 40 px (`md`), 48 emas → qator 57 px, 1080p ekranda ~14 odam; rasm tanib olinadigan darajada qoladi (egasi: ro'yxatda haqiqiy rasm)
- [2026-10-22] Web: ro'yxat sahifalari (`data-list-frame`) 1400 px chegarasiz, ekranning butun eni → egasi «eniga keng bo'lishi muammo emas»; karta/forma sahifalari 1400 px da qoladi
- [2026-10-22] Web: «Filtrlarni tozalash» qayerda → yuqori qatorda «Filtrlar» yonida, faqat filtr qo'yilganda (keng ekranda matn bilan, torda faqat ×) → egasi: panel ostidagi alohida qator ortiqcha joy oladi; hamma ro'yxatda bir xil
- [2026-10-22] Web: bitta holatli ro'yxatda holat yorlig'i («Ishdan ketganlar»da «Ishdan ketgan», sinovda «Davom etmoqda» ustuni) → ko'rsatilmaydi, faqat aralash ro'yxatda → har qatorda bir xil yorliq «ortiqcha ma'lumot» va jadvalni kengaytiradi
- [2026-10-22] Excel eksport qaysi ro'yxatlarga («deyarli har bir qism») → hisobotga kerakli 11 ro'yxat (SPEC-excel-eksport §2); sozlama sahifalari (foydalanuvchilar, rollar, ma'lumotnomalar, qurilmalar, jadvallar, joylashuvlar) YO'Q → ular hisobot ma'lumoti emas; egasi so'rasa qo'shiladi
- [2026-10-22] Eksport ruxsati → ro'yxatni ko'rish ruxsati VA `reports.export` (xodimlar/rag'bat eksporti bilan bir xil); fayl ekranda ko'rinadiganidan ortiq ma'lumot bermaydi → tizimdan ma'lumot olib chiqish alohida ruxsat
- [2026-10-22] Excel eksport: Nomzodlar «Voronka» ko'rinishida tugma nimani yuklaydi → taxtaning o'z filtri (bosqich tanlovisiz, holat tanlanmasa faqat «Jarayonda»); jadvalda — jadval filtri saralash bilan → fayl ekranda ko'ringan to'plam bilan bir xil bo'lsin
- [2026-10-22] Excel eksport: So'rovlar (yo'qlik / jadval) ikki jadvalli ekran → bitta fayl, o'sha filtr, holatsiz (kutayotganlar + qaror qabul qilinganlar); tugma faqat umumiy navbatda, xodim kartasida yo'q → ro'yxat endpoint'i ikkala jadvalga bitta, filtr qatori faqat navbatda
- [2026-10-22] Excel eksport: belgi ro'yxatlari fayl nomi (server bermasa) → turidan: `rezerv_xodimlar`, `qora_royxat_xodimlar`, `nazoratdagilar`, `qobiliyatli` → SPEC §2 «slug from the flag type»; odatda nom serverdan keladi
- [2026-10-22] Excel eksport: Davomat statistikasi tugmasi → faqat kun rejimida (davr grafigida jadval yo'q); yorlig'i 2xl dan, torroqda faqat ikona → zich asboblar qatori 1440 px da ikkiga bo'linmasin
- [2026-10-22] `cn` (tailwind-merge) shrift shkalasini tanigach → tugma va yorliqlarning MATN RANGI ham tiklandi (eski konfiguratsiya o'lcham sinfini rang deb, oldingi rang sinfini tashlab yuborardi) → dizayndagi asl ko'rinish: asosiy tugmada oq matn, yorliqda o'z ohangi; yorug'/qorong'i rejimda tekshirildi
- [2026-10-22] Eksport fayl nomi → qaysi ekrandan: `rezerv_nomzodlar`, `qora_royxat_nomzodlar`, `rad_etilgan_nomzodlar`, `rezerv_xodimlar`, `qora_royxat_xodimlar`; Statistika — jadval sanasi bilan → bir kunda ikki fayl yuklansa brauzer «(1)» qo'shmasin
- [2026-10-22] Eksport sarlavhasi SPEC matnidan farq qilsa → ekrandagi ustun nomi → SPEC §1 qoidasi; audit fayli serverda o'qiladigan matn bilan (`AuditLabels.cs` — uz-Latn `audit.*` nusxasi, yangi yorliq ikkala joyga qo'shiladi)
- [2026-10-22] Voronka ko'rinishidagi eksport → doskaning filtri (faol bo'lmagan bosqichdagi nomzodlar ham faylga tushadi, doskada ko'rinmasa ham); So'rovlar eksporti — «qaror kutmoqda» va «qabul qilingan» bitta faylda → hisobot uchun to'liq ro'yxat foydaliroq
- [2026-10-22] Sinov avtomatik «o'tdi» qachon → oxirgi kuni (EndDate) to'liq o'tgach, har kuni 00:05 Toshkent (`probations:auto-pass`); natija sanasi = tugash kuni; lavozim/oklad o'zgarmaydi; bo'shatilgan munosabat qayta faollashmaydi; sozlama (yoqish/o'chirish) YO'Q → egasi «sinov muddat tugasa avtomatik active xodimga aylansin» deb aniq aytdi; HR «o'tmadi» ni muddat ichida kiritadi (ogohlantirish matni shuni aytadi)
- ~~[2026-10-24] How does the tablet get access without an HR session lying on it? → HR signs in once on the tablet's `/anketa` page and pairs it; the tablet keeps only a kiosk-scope API key (`ApiKeyScope.Kiosk`), HR revokes it from «Planshetlar» → a candidate holding the tablet can reach nothing but the form, and reusing `ApiKey` avoids a second key system.~~ superseded 2026-10-26 (public link)
- [2026-10-24] Candidate who applied via the bot and now fills the paper-replacement form in the office → the form attaches to the InProgress candidate with the same phone AND the same last+first name (Latin/Cyrillic-insensitive; phone alone would file a mistyped walk-in's PDF under a stranger — security review M2); otherwise a new candidate (source «Planshet») is registered, blacklist/cooldown do not block (HR is in the room) → the common flow is bot → interview → office anketa, and a duplicate card would split one person.
- [2026-10-24] B part of HR-F02 (documents, tests, interview decision) → printed blank on the PDF now, data entry in the system is a later phase → the owner asked for the candidate's part; the field list's «HR qismi» sheet is ready for the next step.
- [2026-10-24] Signature on a tablet → typed full name (owner's field list) plus a finger-drawn signature printed on the PDF → the PDF must look like the signed paper.
- [2026-10-24] PDF library → PDFsharp-MigraDoc 6.2.4 (MIT), pinned; 6.5.0+ is AGPL-3.0 and QuestPDF needs a paid licence above $1M revenue; Gotenberg/Chromium rejected because prod has 1.9 GB RAM → no money, no new container.
- [2026-10-24] Expected salary on the tablet → exact amount (owner's note), mapped to the questionnaire range; ranges «4–5 mln» and «7 mln+» appended to `SalaryExpectation` → the old enum had a hole at 4–5 mln.
- [2026-10-24] PDF language → labels always Uzbek Latin (the company blank HR-F02), answers as typed; the tablet UI offers uz-Latn / uz-Cyrl / ru → one official layout for the personal file.
- ~~[2026-10-24] Kiosk key hardening beyond the security review fixes (bind to the office IP, expire after N days) → not done, owner decides → the office's outgoing IP and re-pairing cadence are operational choices; the submit is already limited to 5 per 10 minutes per tablet and HR can revoke at once.~~ superseded 2026-10-26 (public link)
- [2026-10-24] Who may open the anketa PDF (PINFL, passport, convictions, health) → `recruiting.view`, same as candidate files (passport scans live there already) → a stricter permission would hide the candidate's own documents from the recruiter who interviews them; owner may tighten later.
- [2026-10-26] Access to the anketa → public anonymous link like Google Forms (owner chose variant C: «juda ko'p nomzodlar keladi … bitta wifida qilish juda muammoli»); kiosk key, pairing, tablet names and the «Planshetlar» dialog removed (never deployed) → owner's decision.
- [2026-10-26] Spam protection for a public form → per-real-IP limits (20 submits/hour, 60 session calls/min), 500 submits/day global cap, honeypot field; Turnstile only if spam appears → keeps the form frictionless while a flood cannot fill the funnel or the disk.
- [2026-10-26] Real client IP behind Cloudflare → Caddy trusts Cloudflare ranges, reads `Cf-Connecting-Ip`, passes it as `X-Bonvi-Client-Ip`; the API's `X-Forwarded-For` handling is untouched → per-IP limits need the real IP, and leaving XFF alone means no change for login limits, audit or device endpoints.
- [2026-10-26] Office tablet vs. candidate's own phone → one link; tablet mode is a per-device switch (`/anketa?planshet=1`, stored locally) that turns on the strict idle/draft reset; personal devices keep the draft for 2 hours in the tab → the server cannot tell the two apart and does not need to.
- [2026-10-26] Source label → `CandidateSource.Tablet` renamed `OnlineForm` (value 4, never deployed), shown «Onlayn anketa» → the form is no longer only on the tablet.
- [2026-10-26] Short address → constant `hr.bonvi.uz/anketa`; optional `anketa.bonvi.uz` redirect block in Caddy, active only when the owner adds the DNS record and `ANKETA_SITE` → owner: «havolalar … eslab qolish uchun qiyin va uzun, doimiy bir xil bo'lsa yaxshi».
- [2026-10-26] Wizard layout → 16 short one-topic screens, larger labels, single column on phones, repeating rows as summary lines with a bottom sheet → owner found the 6 long steps «rasvoroq», and the public link brings phones into scope.
- [2026-10-26] What the daily anketa cap (`RateLimiting:QuestionnaireDaily`, 500) counts → only ACCEPTED forms (201); rejected and repeated submits are not counted, the check runs before the handler, the permit is taken after → otherwise a few hundred cheap invalid requests from many IPs could close the public form for the whole day; two forms racing past the last permit overshoot by one, harmless.
- [2026-10-26] Honeypot `website` → checked first in the handler, before the `clientSubmissionId` lookup; 400 `anketa.belgi` with `field: website` (no separate code) → a bot learns nothing and gets no idempotent replay.
- [2026-10-26] Same `clientSubmissionId` → 200 with the same body from any IP; `submittedAt` is truncated to microseconds before saving → PostgreSQL stores microseconds, and the replay reads the stored value, so the first and repeated bodies are byte-identical.
- [2026-10-26] Per-IP key for IPv6 clients → their /64 network (IPv4 as is; IPv4-mapped addresses folded to IPv4) → one IPv6 subscriber owns a whole /64 and could otherwise rotate addresses past «20 per hour per IP».
- [2026-10-26] Audit actor on the anketa endpoints → always Anonymous «Onlayn anketa», even if the browser sends an HR bearer token (endpoint marker wins over the token); stored in `actor_name`, so the audit screen (`audit-format.ts`) and the Excel copy (`AuditEntryText.cs`) now prefer `name` for Anonymous rows → an HR session left on a shared tablet must not sign a candidate's form with the HR's name.
- [2026-10-26] Global request limiter (120/min per user or connection IP) on `/api/kiosk/*` → left unchanged; on prod anonymous requests share one bucket per Cloudflare edge, as all anonymous endpoints already do → excluding the path would leave a distributed flood on the session endpoint bounded only per IP; real anketa traffic (a few requests per form) is far below it. Revisit if the bot sends the link to many applicants at once.
- [2026-10-26] `ANKETA_SITE` dead default port → `:9998`, not the SPEC's `:9996` → Caddy 2.11.4 refuses to start when a host-less plain site sorts below the host-less ISUP TLS site (`:9997`, when `ISUP_FACE_SITE` is unset): «automation policy … is also default/catch-all policy». Tested both.
- [2026-10-26] Short-address redirect placeholder → `{?query}` instead of the SPEC's `{query}` → `{query}` has no leading «?» and would redirect to `/anketavakansiya=…`; `{?query}` adds «?» only when there is a query (checked against a running Caddy 2.11.4).
- [2026-10-26] Wizard split → 26 short screens, not the SPEC's suggested 16 (marital/military and licence/car apart, education and school/courses apart, conditions in four, health and convictions apart, the 17 open questions on four screens, consents and signature apart) → measured at 390 × 844: the 16-screen grouping still needed one to two screen heights of scrolling on several screens; each screen now fits or nearly fits a phone.
- [2026-10-26] Language levels and computer skills on the public form → one summary line per language / skill group, the level buttons in a bottom sheet (like the repeating rows) → a 4 × 3 grid of 0–5 buttons and 31 skill rows were the two longest blocks of the old step; the line shows what is set («So'zlashuv 3 · O'qish 3 · Yozish 2»).
- [2026-10-26] Family table on a new form → starts empty with «+ Oila a'zosi qo'shish» (the `familyMin` rule asks for one) instead of one blank row → a blank summary line would read as a filled entry; answers JSON and rules unchanged.
- [2026-10-26] Tablet-mode link in HR's «Anketa havolasi» → always this site's own `/anketa?planshet=1`, copy only (no «open» button); the short `VITE_ANKETA_URL` is shown only as the candidate link → opening the tablet link on HR's own computer would switch that browser into tablet mode; the switch must not depend on the optional redirect.
- [2026-10-26] Browser autofill on the public form → allowed on a personal device (`family-name`, `email`, …), `off` in tablet mode → on the shared tablet autofill would offer the previous candidate's name and address to the next one.
- [2026-10-26] Passport, PINFL and gender in the anketa → removed from the form, the answers schema and the PDF; military service becomes optional for everyone → owner: the passport is taken only after the council interview, too personal for the anketa; gender was in the same rows the owner marked.
- [2026-10-26] Computer skills table (39 items, 4 columns incl. programming) → 11 items in 3 groups (Windows, smartphone; Word, Excel, PowerPoint, Google Docs/Sheets; 1C, Didox/soliq.uz, CRM, AutoCAD, Photoshop/Canva/CorelDraw) + free text «Boshqa dasturlar va dasturlash tillari» → owner asked to shorten it; the company hires mostly operators, welders and office staff, so the rare items and the programming column go to free text.
- [2026-10-26] «Qayerdan bilgan» list in HR's candidate dialog and the candidates filter → `GET /api/recruiting/referral-channels?includeInactive=true` (`recruiting.view`), not the catalog list (`catalog.view`) the contract named → the backend added it so a recruiter without catalog access can still add candidates; the catalog page keeps the catalog endpoints.
- [2026-10-26] Candidates list «Qayerdan bilgan» filter in the address → `?referral=<channel id>` or `?referral=none` («Ko'rsatilmagan» → `referralMissing=true`) → same pattern as `vacancy`/`stage`; one key, because the two choices are exclusive.
- [2026-10-26] Reports «Nomzodlar qayerdan bilgan» filters → own address keys `referralFrom`/`referralTo`/`referralVacancy`, empty = all time → the page-level `months` belongs to the turnover charts; an unprefixed `from/to` would read as a filter of the whole page.
- [2026-10-26] Referral bar on the Reports page → length = the channel's share of all candidates in the period, green part = its hired share of all candidates; table shows candidates, hired and hired % of the channel → both numbers of the row are visible in the bar (the turnover bar's rule: the bar must not argue with the numbers).
- [2026-10-26] Online anketa «Vakansiya haqida» screen → channels as two-column tiles up to 12, a select beyond → 8 seeded channels fit one phone screen as tiles; HR may add many more.
- [2026-10-26] «Nomzodlar qayerdan bilgan» report permissions → `reports.view` AND `recruiting.view`; its Excel → those two AND `reports.export` → turnover has only `reports.view` and no export; candidate numbers must not open to a user without `recruiting.view` (dashboard «recruiting» block rule); list exports are «view rights + reports.export».
- [2026-10-26] «Hired» in the referral report → candidate status `Hired` today, cohort = applied in the period (MCP `recruiting_funnel` definition); Excel council archive excluded (funnel default) → its «application date» is the import day and it has no channel.
- [2026-10-26] Referral detail on a channel that does not ask for it → dropped on a NEW choice (HR add/edit to another channel, bot, anketa); on an UNCHANGED channel HR edit stores what is sent; an existing detail on an asking channel cannot be blanked; a legacy row (bot «Tanishlar orqali» moved without detail) may stay without → no «Telegram — <old name>» lines; legacy rows stay editable.
- [2026-10-26] Detail longer than 200 → 400 `qayerdan.izoh_uzun` (new code, not in the contract's list) instead of a silent cut → the HR form and anketa enforce the limit; the bot cuts to 200 itself.
- [2026-10-26] Anketa referral errors → missing channel / missing detail use the anketa's generic `anketa.majburiy` + `field`; unknown or inactive channel → `qayerdan.topilmadi` + `field: referralChannelId` → the anketa reports per-field with few generic codes (QuestionnaireFormErrors rule).
- [2026-10-26] Bot conversations in flight at deploy → state property renamed (`referralChannelId`), the old `referralSource` key is ignored on load; on «Yuborish» only the referral question is re-asked and the offer is not asked again; a pre-deploy «src:Telegram» button (or a channel deactivated meanwhile) re-draws the question with fresh buttons; server `qayerdan.*` errors on submit also re-ask only that question → nothing crashes, no answers lost, no enum mapping kept in the bot.
- [2026-10-26] Bot buttons for `uz-Cyrl` → channel names as stored (Latin) → no Latin→Cyrillic helper exists in the bot or shared code (SPEC fallback).
- [2026-10-26] Candidates Excel «Qayerdan bilgan» → appended as the LAST column → existing column positions stay where HR expects them.
- [2026-10-26] MCP read-only role → `referral_channels` (whole table) and `candidates.referral_channel_id/referral_detail` granted; `mcp_apply_ro_grants` re-created in `QayerdanBilgan`, Down restores the previous one → MCP tools now print the candidate-level channel; the grant list is a migration-only change by design.
- [2026-10-26] Referral report row order → candidates desc, ties by channel name, «Ko'rsatilmagan» (`channelId: null`, `isActive: false`) after equal channels → SPEC only fixes «candidates desc».
- [2026-10-26] Hand-over note → «Ish haqida qayerdan bilgan: <kanal> — <aniqlik>» also for candidates without a questionnaire → the field is candidate-level now (SPEC §6).
- [2026-10-27] Hardening the public anketa → form-only site `anketa.bonvi.uz` (everything but the form and `/api/kiosk/*` is 404), optional Cloudflare Turnstile (off until the owner adds keys), real client IP for login limits, audit and session IPs → owner asked for maximum protection against people who remember the link.
- [2026-10-27] Turnstile verification when Cloudflare is unreachable → fail closed (`anketa.bot_tekshiruvi`) → owner asked for maximum protection; the draft survives, so the candidate only retries.
- [2026-10-27] Turnstile widget language and retries on /anketa → `ru` for Russian, `auto` (browser language) for both Uzbek scripts; Turnstile's own retry/refresh off, the page retries a failed check twice (4 s apart), then on the next «Yuborish», and resets after expiry and after every failed submit → Turnstile has no Uzbek (falls back to English for `uz`); bounded retries avoid an endless loop for a blocked browser, and a token is spent by any server check.
- [2026-10-27] `anketa.bonvi.uz` and path tricks → `/api/kiosk/*` is proxied only when the request-target the API will receive (`{http.request.uri}`) is literally `/api/kiosk/` + `[A-Za-z0-9/_-]` (no dot, no `%`); anything else 404 → Caddy matches on the decoded, cleaned path but forwards the raw one, and Kestrel resolves `..` without splitting `%2F`: `/api/kiosk%2Fx/../auth/login` matched `/api/kiosk/*` in Caddy and reached `/api/auth/login` in the API (checked against a real Caddy 2.11.4 and the dev API).
- [2026-10-27] Shared Caddy rules → snippets `site_common` (encode, client-IP strip, access log, security headers, CSP), `api_proxy`, `spa_files`, imported by both sites; both sites log to the same `access.log` (lines differ by `request>host`) → SPEC asks for identical headers/CSP/redaction; one copy cannot drift.
- [2026-10-27] CSP `frame-src` → `'self' https://challenges.cloudflare.com` (keeps `'self'`, which `default-src` gave before) → adding Turnstile must not change what already loaded; blob: iframes stay blocked as before.
- [2026-10-27] Turnstile `idempotency_key` → the form's `clientSubmissionId` → re-verifying the same submission (two racing requests, a retry of a request whose verdict was lost) is not rejected as `timeout-or-duplicate`; a different submission with the same token still is.
- [2026-10-27] Turnstile error logging → Cloudflare error codes only (sanitised to `[a-z0-9-]`, max 5), `invalid-input-secret` at Error level, unreachable/timeout at Warning, the HttpClient's own request logs removed → token and secret never reach a log; the owner sees a misconfigured secret at once (every form would fail).
- [2026-10-27] Half-configured Turnstile (one key only) → check stays off, startup Warning (EventId 4702) from a small hosted service in Api (`TurnstileStartupCheck`) → Infrastructure has no hosting abstractions; the warning must appear at startup, not on the first form.
- [2026-10-27] `ClientIp` call sites beyond the SPEC list → also the MCP/OAuth IP partitions (`/oauth/authorize` is HR's password form; register/token/pre-auth come from the AI platform via Cloudflare), the MCP audit IP and the 429 log line; kept on `RemoteIpAddress`: ingest/agent/ISUP (SPEC), the key-less public-API bucket and the API key «last used IP» (server-to-server callers, not people) → per-edge buckets let one caller at an edge 429 everyone else at it; the public API is out of the SPEC's «about a person» scope.
- [2026-10-27] Audit IP on device endpoints → `RemoteIpAddress` by the `DeviceEndpointMetadata` marker, `ClientIp` everywhere else → same marker that already decides the Device actor; terminals keep their behaviour.
- [2026-10-27] Anketa HR-F02 3.1: candidate who never worked → required `hasWorkExperience`; «Ha» → at least 1 job + 1 referee and both job-related «success» answers (`lastJobAchievement`, `bestIdea`) required; «Yo‘q» → those four are neither checked nor stored (`jobs: []`, `references: []`, texts absent), `motivators` and `successDefinition` stay required for everyone → owner chose this among the HR manager's changes; a draft the candidate filled before switching to «Yo‘q» must not land in the signed form.
- [2026-10-27] Legacy `availableFrom` (3.0 date) → kept on `QuestionnaireFormAnswers` only so stored 3.0 forms re-render with their date; the validator neither requires nor copies it (explicit `AvailableFrom = null` in the cleaned record), the PDF prints it only when `startAfter` is absent → 3.0 forms are in prod since 2026-10-27 11:02 and must keep rendering; no rewrite of stored answers.
- [2026-10-27] Total experience (years/months) removed → gone from the record, validator, mapping and PDF; on a re-rendered 3.0 form the old values are not printed (unknown key, ignored by design) → owner/HR decision; the PDF attached to the candidate's files at submit time keeps the original rendering.
- [2026-10-27] PDF header version on a re-rendered stored form → the row's own `form_version` (new optional `QuestionnairePdfModel.FormVersion`, defaults to the current `CandidateForm.HrF02Version`) → a 3.0 form opened later must not claim to be a 3.1 blank.
- [2026-10-27] `CandidateQuestionnaire.DriverLicenseCategories` from the anketa → categories joined with «, » («B, C»); existing rows (3.0 anketa «BC» = B + C, bot buttons «BC»/«BCE»/«BCED») are not migrated → «BC» is a category of its own from 3.1; the free-text field is display/filter only (MCP filter is a substring match, so a single category still finds both styles).
- [2026-10-27] `loanMonthlyPayment` when `hasLoan = true` → missing → `anketa.majburiy`; 0, negative or over 1 000 000 000 → `anketa.notogri` → «> 0» rule: 0 is a value, just not a valid one.
- [2026-10-27] Unknown `startAfter` name or a number in the JSON → 400 `anketa.json` (whole document unreadable, no `field`), like every other enum of the anketa; `anketa.notogri` at `startAfter` only for an out-of-range value that reaches the validator → one JSON rule for all enums (`allowIntegerValues: false`); the form only sends the four names.
- [2026-10-27] «Avval ishlaganmisiz?» = «Yo‘q» in the HR «Anketa» card → `WorkExperience` text «Avval ishlamagan» → otherwise the card shows an empty work-experience line for a deliberate answer.
- [2026-10-27] A 3.0 form page left open during the 3.1 deploy (the anketa was already in use on prod that day) → accepted with the 3.0 rules and stored as `3.0` (`IsLegacy`: no `hasWorkExperience`/`startAfter`, has `availableFrom`) → the owner asked for no interruption; rejecting it would strand a candidate after twenty minutes of answers on fields the old page cannot show.
- [2026-10-27] The form learns about a new release from the session's `formVersion` → reload once per server version, only on the start screen (nobody mid-answer; a saved draft survives in sessionStorage), and the session is re-read when a candidate finishes → the office tablet is never refreshed by hand; a once-per-version flag in sessionStorage prevents a reload loop on a stale cache.
- [2026-10-27] Department heads' access to their people's attendance — HR system role or employee app? → employee app («Jamoa» tab, `/api/me/team/*`), owner «ok qil» to the recommendation → the HR system holds passports, salary and candidates; a scoped HR role needs a filter on every list/search/export/report/MCP tool, the app is a separate token audience with a few read-only endpoints (SPEC-jamoam §A).
- [2026-10-27] Does the head see team members' phone numbers? → yes → to call someone who did not come; offered as the recommendation, accepted with «ok qil».
- [2026-10-27] Separate password policy for heads? → not in this wave, the 2026-09-27 rule stays (6 digits, HR sees it) → the owner set that rule explicitly; a change is his call, raised in the proposal.
- [2026-10-27] 09:30 «who did not come» notification for heads → not in this wave → keeps the first wave small; listed as a follow-up.
- [2026-10-27] Team scope = subtrees of every department the employee heads, read from the DB on each request; head assignment is today's, not historised → a new head sees the department's past days too → it is their department's history; HR revokes by clearing the head.
- [2026-10-27] Who may assign a department head → whoever may edit departments (`catalog.manage`), audited by the existing immutable audit → assigning a head now grants data access, but a separate permission would be one more box for HR with the same people behind it.
- [2026-10-27] Team board search → name and phone only (`MatchingPublic`, the external-API rule), not PINFL/passport → a head could otherwise confirm whether a PINFL or passport number belongs to someone in their team; the match itself is the leak.
- [2026-10-27] Department head picker → working employees only; the server still accepts a fired one, and manager resolution skips them → a fired head is meaningless and the list used to start with fired people (alphabetical).
- [2026-10-27] Department save → `bolim.boshliq_topilmadi` only when `headEmployeeId` changes (create always checks) → an unchanged head that was later deleted must not block a rename; the dialog always sends the current head.
- [2026-10-27] The department dialog now always sends `legalEntityId` too → the save overwrote it with `null` on every rename, exactly like the head.
- [2026-10-27] Team view audit → `AuditAction.Export` rows, `team_member_viewed` (entity = member, once per head+member per 10 min) and `team_board_viewed` (entity = head's app account, once per 30 min), in-memory dedup → the `password_viewed` precedent; a board can span several departments.
- [2026-10-27] «Rahbar» → today's primary employment's department, walk up, first head that is not the employee and is employed today → self-head goes to the parent; a fired or deleted head is skipped.
- [2026-10-27] App team cards → «Vaqtida» and «Jadval yo‘q» show only when non-zero or selected; «Keldi» = `Arrived` (came, no schedule), as on the HR dashboard → the SPEC's six cards would hide on-time and no-schedule people; one day must not read two ways.
- [2026-10-27] Team member month (C3) header position/department → as of min(today, month end); team photo (C4) → in scope today → the month view follows the member's history, the photo is a live lookup.
- [2026-10-28] Arrival/departure threshold upper bound → 240 minutes each → grace is capped at 120; four hours on either side covers any realistic «sat in the office» window without letting a typo erase a whole shift.
- [2026-10-28] Thresholds on flexible days, days off, unscheduled days → not applied → a flexible day is not clipped to its window (2026-10-20) and a day off has no shift to measure from.
- [2026-10-28] Thresholds on a schedule-change (override) day → the schedule's own thresholds are applied to the overridden shift → the request changes the hours, not the counting rule.
- [2026-10-28] Existing schedules → thresholds default to 0 → nothing changes until HR sets them; editing a schedule does not recompute past days, HR runs «Qayta hisoblash» (same as every other schedule edit).
- [2026-10-29] «O‘zingiz haqingizda» blank answers → all 17 required, an answer needs ≥ 2 letters («.», «-», «5» rejected; «Ha», «Yo‘q» pass) → the owner asked for every point to be filled; candidates were typing «.» past the already-required ones, and a stricter rule (word count) would reject honest short answers.
- [2026-10-29] Q14 «Avvalgi ish joyingizdagi jamoa va rahbariyat» → asked only of those who worked before (with Q7, Q8); Q9 «eng katta qiyinchilik» stays for everyone → Q14 is meaningless without a previous job; a first-job candidate can still answer Q9 from study or life.
- [2026-10-29] Letterless answer error code → server reuses `anketa.majburiy` (no new code), the form shows its own «Javobni so‘z bilan yozing» → a page left open before the release knows `majburiy` and opens the question; form version stays 3.1 (the questions did not change, so the PDF header does not either).
- [2026-10-30] Bot candidate later fills the online form → card gets `reapplied_at` (form moment); list default sort and from/to filter use `reapplied_at ?? created_at` (the range matches EITHER date); `created_at` and source never change → the owner could not find today's office visitors among 10-day-old bot rows; reports (channel, period) still count the first application.
- [2026-10-30] Sort key name stays `createdAt` although it now means «last application» → saved links and the export parameter keep working; the column header says «Murojaat», the first date is in the tooltip.
- [2026-10-30] Form picks another vacancy → card is reassigned automatically (owner's choice) with a `Reassign` note; the general pool («Umumiy anketa») never replaces a specific vacancy → no choice is not a choice.
- [2026-10-30] Form photo ALWAYS replaces the profile photo (owner: HR takes a proper photo when starting the form); the form keeps its own copy; the old profile file is deleted after save unless one of the candidate's forms points at it → old rows where the copy failed share one file.
- [2026-10-30] «Anketa to'ldirdi» notification → Info (blue), window = `notifications.newCandidateDays`, key bound to the form moment → HR starts the form in the office, so it is a «find the card» aid, not an unanswered application; a second form is a new notification.
- [2026-10-30] Backfill: `reapplied_at` = latest form submitted more than 2 minutes after the card was created → a card opened BY the form has its first form in the same transaction (ms apart).
- [2026-10-30] Document/photo preview → one full-screen dark `FileViewer`; images zoom 100–800% (wheel, pinch, double tap, drag, +/−/0), PDFs render with pdf.js (lazy chunk, own worker file), ← / → between files → an `<iframe>` PDF is blocked by prod CSP (`frame-src 'self'`, `object-src 'none'`) and Android browsers do not render PDF in frames; pdf.js needs no CSP change.
- [2026-10-30] Profile photos stay 512×512 → the face terminals reject faces over 200 KB (`AgentWorker.MaxFaceBytes`) and existing ~2000 photos are already 512; zoom beyond ~300% shows pixels — a separate high-resolution copy would be a new feature.
- [2026-10-30] Preview supports PDF and images only → those are the only types the server accepts for documents and candidate files.
- [2026-10-30] Unknown-direction terminal (Propan-Rispetto): a repeat read of the same person within 15 s of the previous read (both unknown) is ONE pass — dropped, kept in the log, counted in `ignored_pass_count`; chains compare each read with the one before → owner's number (asked 60 s / 5 min, answered 15 s); 20-26 people a day were read twice 3-10 s apart and the alternation turned the evening exit into an entry. The 2026-09-27 rule («unknown never merges») stays outside 15 s; mixed known/unknown is untouched.
- [2026-10-30] Employee app marks → effective direction per tabel cell (`PassPairing.EffectiveDirections`, same as HR «O'tishlar»); a dropped repeat shows its predecessor's direction → the app drew raw `Unknown` as «Chiqish» («Chiqish, Chiqish»). Past days keep their computed totals until recomputed.
- [2026-10-30] Department head's bot → new key scope `DepartmentAttendance` bound to one department subtree (`api_keys.department_id`), reusing the «Jamoa» rules and DTOs (board with phone, member month, photo) plus a pass feed with a `received_at` cursor → the owner asked for «what the head needs» incl. phones; FullRead would expose all departments and salaries. Polling, no webhooks (simpler, no outbound calls); issuing needs `attendance.view` + `employees.view`; the key gets 403 on every other public route.
- [2026-10-30] Bot duplicate check by name + birth date: how strict? → last + first name via `search_key()` (Latin/Cyrillic-insensitive, swapped order accepted) AND exact birth date; patronymic ignored; partial key not used; bot only (HR manual entry and the office form keep their own rules) → prod: repeat applicants changed both phone and Telegram account; half of the prod name+date pairs differ only in the patronymic.
- [2026-10-30] Probation auto-pass → removed from the schedule (`RemoveIfExists("probations:auto-pass")`), class kept; the overdue warning (`ProbationOverdue`, red) stays until HR records the result → owner: «sinov muddati tugasa ham tugadi deb ogohlantirib chiqib tursa yetarli»; probations already auto-passed before this are NOT reverted (they are real results in the history).
- [2026-10-30] Vacancy requests → HR panel menu only («Vakansiya so'rovlari»), the employee app is untouched → owner: «verifix qismidagi o'zgarishlarni qilmay qo'yaver, menu yaratsang yetadi». Anyone with `recruiting.view` records a request; deciding needs `recruiting.manage`. Requester defaults to the department head.
- [2026-10-30] «Draft vacancy» on approval → no new vacancy status; HR gets the open-vacancy form pre-filled from the request and the vacancy opens when HR saves → a Draft status would ripple through the bot, filters and badges; the owner's goal (HR checks before it goes live) is met by the pre-filled form.
- [2026-10-31] Vacancy requests from heads themselves → added on top of the HR menu: the employee app «Jamoa» tab (`/api/me/team/vacancy-requests`, requester = the head) and the department attendance key (`/api/public/v1/team/vacancy-requests`, requester = head of the department or the nearest ancestor head inside the key's subtree; none → `tashqi.boshliq_yoq`) → owner: «2 usulni ham qilsak … bot orqali api bilan so'rov jo'natish ham mumkin bo'lsin». The key scope stays named `DepartmentAttendance` (no new scope, no migration); list/submit/cancel only, no edit (same as HR). Audit actor for API-key writes is «Tashqi API: <key name>».
- [2026-10-31] Assign a department head from the employee card → in the card's «Davomat» → «Ilova» segment (not the edit dialog), via a dedicated `PUT /api/catalog/departments/{id}/head` → headship is what opens the app's «Jamoa» tab, so it sits next to the app login/password; the edit dialog is personal data. A dedicated endpoint avoids re-sending the whole department form (stale name/legal entity overwrite). Same rules as the department form: `catalog.manage` + `attendance.view`, one head per department (assigning replaces the current one, the card warns first). «Bo'limlar» menu keeps working as before.
- [2026-10-31] Reference deactivation: does a deactivated department lose its head or its bound positions? → neither is touched → an inactive department has no working employees (guarded), and positions are a separate reference HR can deactivate on its own
- [2026-10-31] Reactivating a department: does it reactivate the sub-departments closed with it? → no, only itself; a child cannot be activated under an inactive parent → some children may have been closed separately before, a cascade would reopen them silently
- [2026-10-31] Online form found no phone match but an InProgress candidate has the same name + birth date → attach to it (bot's third key), replace the card phone with the form's and keep the old number in an `OtherPhone` note → owner: «online anketa ham tekshirsin», then chose «anketadagi yangi raqam». The note doubles as HR's signal if a stranger who knows the name and date attached a form.
- [2026-10-31] HRMentor blanks and HR-F02 texts: which way do the systems talk? → hr.bonvi.uz pushes its catalog and pulls overrides and files with one HRMentor key holding only `forms:sync`; HRMentor never calls us (`docs/SPEC-blankalar-hrmentor.md`) → the form's structure stays developer work here, HR owns the wording there; one secret, and hr.bonvi.uz keeps working on its last copy when HRMentor is down
- [2026-10-31] Re-rendering an old HR-F02 PDF after HR changed the wording → rendered with the texts revision in force at submission (`candidate_forms.texts_revision`, all snapshots kept) → a signed form must never change its questions afterwards
- [2026-10-31] Who sees the Blanks page → `employees.view`; «Hozir yangilash» → `settings.manage` → blanks are HR-wide paperwork, not recruiting-only; no new permission means no role migration
- [2026-10-31] PDF option labels with no HR override → the PDF keeps its own literals (typographic apostrophes), not the web labels → with nothing changed in HRMentor the PDF stays byte-for-byte as before
- [2026-10-31] Closed option lists (marital, education, …) → HR may rename, reorder and hide, never add values; only the computer skills list is open → closed values are enums mapped into `CandidateQuestionnaire` and reports; a new value needs a mapping decision (developer)
- [2026-10-31] Telegram bot questionnaire → not driven by HRMentor texts → it is a separate, shorter questionnaire with its own buttons; the owner named the hr.bonvi.uz form and its PDF
- [2026-10-31] Two syncs at once (job + «Hozir yangilash») → one in-process semaphore, the second gets `hrmentor.busy` → one API process runs the jobs; Hangfire's DisableConcurrentExecution would block the HTTP caller instead of answering
- [2026-10-31] Blank file limits → 50 MB cap, redirects off, sha256 checked before storing, file kept under an IFileStorage key → a wrong or swapped file never replaces the last good copy
- [2026-10-31] HRMentor down or key revoked → keep the last texts and blanks, record `lastError`, keep `lastSuccessAt` → the kiosk and the Blanks page must not go blank because a second system is down
- [2026-10-31] Integration switched off (address or key cleared) → kiosk and PDF fall back to the built-in texts; old PDFs keep their own revision → switching off must not change a signed form
- [2026-10-31] Kiosk wording from HRMentor → laid over the bundled `kiosk.*` i18n resources and put back when the form is left; order/hidden read through a context → no call site changes, the HR app sharing the i18n instance never sees kiosk overrides
- [2026-10-31] Submit with a texts revision → the client's revision if it exists in the database, else the current one → a candidate who opened the form before HR saved keeps the wording they saw
- [2026-10-31] HR removed a computer skill the draft still holds → dropped at submit (draft keeps any `x-…` key by shape until the session is known) → the server rejects keys outside HR's list
- [2026-10-31] Language rows → shown in HR's order, the draft keeps its own order → handlers keep working with the draft index; the PDF prints the same order
- [2026-10-31] HRMentor over plain http → allowed only for this machine (localhost, 127.0.0.1, [::1], host.docker.internal), elsewhere https only → the key travels in a header; local testing and two apps on one server need http without leaving the machine
- [2026-10-31] HR changed only the Latin text → HRMentor serves Cyrillic as its transliteration and Russian as the Latin text (its SPEC §18.5); BonviHR shows what is served → a changed meaning must not hide behind the old Russian default
- [2026-11-01] Candidate form structure owned by HRMentor (SPEC-anketa-konstruktori) → BonviHR renders, validates, maps and prints from the published definition; the built-in 3.1 form stays as the fallback (nothing published, integration off, pages opened before) → the owner changes only HRMentor; no interruption on the day HR publishes
- [2026-11-01] Which BonviHR data an HR-built form may feed → only through the profile's bindings (names, birth date, phone, vacancy, photo, signature, consent, address, referral, questionnaire fields); other answers live in the form and its PDF → matching, the candidate card and filters keep working whatever HR builds
- [2026-11-01] Required bindings (vacancy, photo, last/first name, birth date, phone, signature, personal-data consent) → a definition without them is refused on both sides → without them a submission cannot become a candidate
- [2026-11-01] Retiring the built-in form → not now; reconsider 60 days after the first definition is live (debt); the built-in PDF renderer stays for 3.0/3.1 forms forever → signed forms must re-print unchanged
- [2026-11-01] Preview of a draft → `/anketa/korinish` on hr.bonvi.uz with an HR session (`recruiting.view`), draft fetched live from HRMentor, nothing stored → HR sees exactly what candidates will, without HRMentor carrying a second renderer
- [2026-11-01] Bound answers of an HR-built form that the questionnaire stores as text (work history, other languages, office skills) → formatted exactly as the built-in 3.1 mapping does (same separators, the `office` group only for office skills) → AC-AK9: one person, two paths, the same candidate card and questionnaire values (integration test compares every column)
- [2026-11-01] Candidate phone and birth date in an HR-built form → the bound phone is always an Uzbek mobile and the bound birth date always 14–100 years, whatever the field says → the card, duplicate matching and Telegram depend on them; HR may tighten, never loosen
- [2026-11-01] Empty rows in repeat fields and all-empty rows in levels tables → dropped before validation of `min_rows` / `required_rows` → a row the candidate added and left blank is not an answer (built-in behaviour)
- [2026-11-01] Numbers in answers built in code (JsonValue<int>) vs parsed from JSON → both accepted as whole numbers, fractions refused → the preview and tests build answers in memory; parsing must not depend on how the value was made
- [2026-11-01] Session `formVersion` while a definition is live → stays the app's built-in blank version (3.1), the definition's label travels in `definition.label` → `formVersion` drives the one-time page reload after a deploy; putting the label there would reload old pages for nothing
- [2026-11-01] Preview of a draft → `definition.version` = 0 (never a stored version), label «draft» when HR has not set one; not connected → 409, any HRMentor-side failure → 502 with its `hrmentor.*` code → the HR user must see whether to fix settings or the draft
- [2026-11-01] Sample PDF of the preview → validated exactly like a submission, photo and signature optional, «Namuna» instead of the form number, nothing stored; 30 per hour per HR account → HR checks the paper look before publishing; a script cannot hold the shared render queue
- [2026-11-01] Dynamic PDF details → a levels field with empty `print_rows` prints every option; a checklist item with `auto` is ticked only when the form really has that image; built-in and dynamic renderers share one render gate (2 at a time) → paper never claims a photo that is not there; memory stays bounded
- [2026-11-01] A new definition version published while `/anketa` is open → no page reload: the start screen takes the session's current definition on «Boshlash», a walk in progress keeps the one it started with (pinned) and sends its version; a removed version → `anketa.versiya` with a «reload» button → the definition is data, not code; a candidate never sees the form change under them
- [2026-11-01] Constructor-only words (`kiosk.dyn.*`, the 12 new `kiosk.error.*` codes) and the HR preview's words (`kiosk.preview.*`) → kept out of the HRMentor texts catalogue (`export-kiosk-texts.mjs`) → HR texts never apply while a definition is live (§5.3); an editable text that shows nowhere would mislead HR
- [2026-11-01] Preview walk → «Keyingi» checks like the candidate's, but the ribbon has a screen picker to open any screen without filling the ones before; «Yangilash» refetches the draft and keeps typed answers; «Bekor qilish» starts over; no storage, no bot check, photo and signature optional → HR checks the look of every screen in seconds and the rules on the screens it fills
- [2026-11-01] Vacancy hint of an HR-built form → shown only while the general pool takes forms (the hint speaks of «Umumiy») → the built-in screen's rule
- [2026-11-01] Choice rendering of an HR-built form → more than 12 options a picker, else tiles (`style: list` one column, `tiles` with short labels two, otherwise automatic); multi-choice with labels up to 4 letters a compact grid (licence categories), else ticked rows; one-aspect levels inline per row, several aspects in a sheet → the built-in form's look for the same question types
- [2026-11-01] Which list the «Blankalar» step pulls → HRMentor's platform list (/platform/documents), falling back to /blanks only on 403 key_not_bound / missing permission / 404 → the owner's rule «har dastur o'ziga tegishli hujjatni oladi»; the fallback keeps the page full until the key is bound in HRMentor
- [2026-11-01] A type column for non-form documents on «Blankalar» → not now; added with the first non-form assignment → today only the 19 blanks are assigned; a column nobody sees is a defect
- [2026-11-01] How S1 («no document template in our code») is guarded → Bonvi.Architecture.Tests: PdfSharp/MigraDoc dependents and office/PDF files on pinned lists, debt items ratcheted → structural checks catch a new blank before merge; a rule only in a document gets broken
- [2026-11-01] A switched-off platform in HRMentor (403 platforms.platform_inactive) → the «Blankalar» step's error `hrmentor.platform_off`, last copy kept, NO fallback to /blanks → HR switched it off on purpose; the old list would silently undo that
- [2026-11-01] How the guard sees web/ and app/ when tests run in Docker → `./bonvi test` mounts the repo root read-only at /repo and sets BONVI_REPO_DIR; without a root the test fails loudly → a guard that skips when it cannot see is no guard
- [2026-11-01] Which source lines count as «uses PdfSharp/MigraDoc» → code lines matching `PdfSharp.` / `MigraDoc.`, comment lines skipped → an interface's doc comment names MigraDoc; prose is not a dependency
- [2026-11-01] Platform answers an empty list before it ever served one → fall back to /blanks like «not ready»; after the first platform list an empty one removes the blanks → granting `platform:sync` before assigning the folder would otherwise wipe every blank for up to five minutes; on a live platform «nothing assigned» is HR's call
- [2026-11-01] How hr.bonvi.uz hears of an HRMentor change at once → a hosted loop keeps `GET /platform/changes?since=` waiting and syncs on a new revision; the five-minute job stays as the net → no inbound route, no secret kept by HRMentor, nothing missed after an outage (states compared, not events)
- [2026-11-01] When the loop's `since` advances → only after a clean sync; a failed or busy sync keeps the old revision and backs off 5 s doubling to 5 min → advancing on a failed sync would hide the change until the next HR edit
- [2026-11-01] Where the loop lives → Api hosted service (`HrMentorChangeWatcher`), registered beside Hangfire, never in `Testing` → the step (`HrMentorChangeWatch`) is plain Application code the tests call; one process, the sync gate stays the in-process semaphore
- [2026-11-01] HRMentor serves HR-F02 texts with `revision: 0` (HR never saved texts; HRMentor SPEC §18 starts revisions at 0) → a valid answer that stores nothing, built-in texts stay; only a negative or missing revision is `texts_invalid` → refusing 0 failed every sync, so the change watch never advanced `since` and every change waited up to five minutes
- [2026-11-01] Constructor PDF header vs the paper HR-F02 v2.0 (Anketa № | Sana | So'rov № (F01) | Lavozim | Yo'nalish) → print the built-in PDF's second row: «So'rov № (HR-F01)» and «Bo'lim», both written by hand → no request number exists in the data, and a constructor PDF takes no HRMentor «Matnlar» words (texts_revision NULL), so «Yo'nalish» needs a new `pdf` key in the definition contract of both systems — left for the CEO's word
- [2026-11-01] Where HR names the constructor PDF's «Bo'lim» header cell (paper HR-F02 v2.0: «Yo'nalish») → a new optional definition key `pdf.head_department` (≤ 60), unset → «Bo'lim» → a constructor PDF takes no «Matnlar» words (texts_revision NULL) and the built-in default may not change (built-in PDFs never change); HRMentor stays the one source of the word
- [2026-11-01] When the change watch moves `since` after a failed step → also when every failed step failed on what HR saved (`HrMentorErrors.IsLasting`: texts, definition, blank checksum or size) — outcome `SyncedWithError`; network, timeout, 5xx, other 4xx and busy still keep the old revision → the same state answers the same, so waiting only delayed HR's next change by up to five minutes; the five-minute job re-checks the failed step
- [2026-11-02] HRMentor answers 404 on texts while the catalog hash says «sent» (its database was restored or the form re-linked) → push the catalog and the profile again in the same run and pull the texts once more; only when the catalog was not sent in this run, so a 404 after a fresh push never loops → HRMentor's texts endpoint is 404 only for an unknown form (`get_served_texts`), and without this the constructor never saw HR-F02 again (found restoring the server's HRMentor locally)
- [2026-11-02] A published definition whose version is below the one hr.bonvi.uz already holds (HRMentor restored or re-linked, its counter starts again at 1) → left as is: still ignored silently, versions only grow (SPEC-anketa-konstruktori §5.2) → on the server neither side holds a version yet; a real fix needs an identity for the HRMentor form (an epoch), not an error HR cannot clear — open for the owner
- [2026-11-04] origin/main's generic «Blankalar» engine (docs/SPEC-blankalar.md) took the same names as the HRMentor blank-file list (`/blankalar`, `/api/blanks`, `BlankEndpoints`, `BlanksRegistration`, i18n `blanks`, error `blanka.topilmadi`) → the HRMentor list moves aside before the merge: route `/blanka-fayllari` «Blanka fayllari», API `/api/hrmentor/blanks`, types `HrMentorBlank*`, i18n `hrMentorBlanks`, error `hrmentor.blank_not_found`; the engine keeps every name it has → additive: the engine is the newer, wider module on main and nothing of it is rewritten; the HRMentor list was never released, so no caller of the old path exists
- [2026-11-03] Unscheduled day: what replaces the 22:00 auto «ketdi»? → nothing; the open entry stays open, its interval is not counted, status stays «Grafiksiz» (no new «chiqish yozilmagan» status) → owner chose option B: night workers get a night-shift schedule, whose day window already covers the morning exit; a continuous 16 h chain would let one missed entry on a single terminal flip directions until the next day off. Days stored 2026-10-12..11-01 keep their 22:00 «avto» exit until recomputed (nightly job covers only 2 days back) — recompute is the owner's call.
- [2026-11-03] Who may add/delete a hand mark? → `attendance.requests.manage` (adding), `attendance.view` (day dialog) → it is an HR decision about a person's day, the same permission that approves absence/schedule requests; `attendance.manage` is system setup (devices, schedules) and no new permission means no role migration.
- [2026-11-03] Can HR delete a hand mark? → yes, only `method = Manual` rows; terminal and phone passes stay untouchable (400 `davomat.belgi_qolda_emas`) → owner: «kirish chiqishi fix qilinmaydi, yangi qo'shiladi»; without delete a mistyped mark would stay forever (an opposite mark breaks pairing instead of undoing it). Audit keeps who/when/why via `attendance_manual_passes`.
- [2026-11-03] Hand mark date range → not in the future, not older than 92 days (`MaxRecomputeDays`); the 30-day device clock-skew guard does not apply (`AttendancePass.RecordByHr`) → HR corrects the previous month in the first days of the next one; 92 days is the recompute limit.
- [2026-11-03] Day dialog default tab → «Belgilar», not «Kun statistikasi» (Verifix opens on statistics) → the dialog exists to see and add marks; statistics duplicate the cell.
- [2026-11-03] «Fayl yuklashda maksimal moslashuvchanlik» — which meaning? → both: (a) every file field configurable (types, count, size, required, camera) in phase 1; (b) draft template from an uploaded Word/PDF blank in a later phase → owner did not pick yet; (a) is cheap inside the engine, (b) needs an AI extraction step and can wait.
- [2026-11-03] Blankalar: per-blank code or one engine? → one generic engine (template definition JSON + submission jsonb), wave-1 templates seeded from reviewed JSON files; F02 stays hard-coded and untouched → 19 blanks with the same building blocks; additive rule (docs/SPEC-blankalar.md).
- [2026-11-03] F07/F08/F09/F13 vs Probation/ExitInterview/Employment/Incentive → link by employee/employment, never sync or gate existing modules; gaps reported by MCP `blank_gaps` → writing into existing modules breaks the additive rule; AI agents need the mismatch, not an automatic write.
- [2026-11-03] Who acts for HR-1…HR-4, payroll, chief accountant, director → configurable `blank_role_assignments` (role → users); no assignment = any `blanks.approve` holder → works on day one; owner fills names later without a deploy.
- [2026-11-03] Heads, mentors, employees, witnesses sign how in wave 1 → in person on the HR device (signature pad) or electronically if their HR-panel user is linked to their employee card; employee-app channel = Phase 3 → no PWA changes now; resolution lives in one service so the app plugs in later.
- [2026-11-03] Blank permissions → `blanks.view` / `blanks.fill` / `blanks.manage` levels + `blanks.approve` action, module key `blanks`; `blanks.view` added to FullRead → mirrors the Employment module shape; AI agents must read blanks through existing FullRead keys.
- [2026-11-03] Blank № → per template, gapless counter table, assigned at first submit, never reset per year → drafts must not burn numbers; paper «№ (HR-4 beradi)» is issued on hand-over.
- [2026-11-03] Progressive filling (F06 90 days, F07 four actors) → sections owned by steps (`fill.at = step`) plus `open` sections; required-check per step → one record follows the paper blank from first to last signature.
- [2026-11-03] File kinds for blanks → pdf, jpeg/png/webp, docx/xlsx/pptx; «any» = these, never arbitrary bytes; macro-enabled, encrypted and legacy .doc/.xls rejected; own `IBlankFileInspector` delegating to the existing inspector → no antivirus in the stack; existing upload rules stay unchanged.
- [2026-11-03] F07 band gap 3.41–3.49 (INTAKE Q11) → bands are a computed suggestion (≥ 3.5 o'tdi, ≥ 3.0 shartli, else o'tmadi), never blocking; HR-1 decides → the owner's blank leaves the gap; a hard rule would block real decisions.
- [2026-11-03] F13 witnesses (INTAKE Q14) → 2 witness steps, only when the explanation is empty → F11, R06, R07 all say «2 guvoh».
- [2026-11-03] F06 day-60 score (INTAKE Q22) → stored as score 1–5 → R05 «rahbar bahosi ≥ 3,5» needs a number.
- [2026-11-03] F08 reason codes → fixed K01–K12 options in the template, not the TerminationReason dictionary → official reason (order) and interview reason are different facts (ExitInterview domain note).
- [2026-11-03] Blank answers in the audit journal → excluded; each step stores its own `answers_patch` as the legal trail → answers hold discipline and exit details; `audit.view` is a different audience.
- [2026-11-03] Final PDF → rendered on demand always; snapshot stored at Approved in blank storage, NOT attached to employee documents → attaching would write into the employee documents module.
- [2026-11-03] F13 «draft» legal status → published with `legalStatus: draft` (PDF prints «Loyiha») pending the owner's answer → the record is internal procedure; the order itself is still issued by HR-2.
- [2026-11-03] Blanks S1: SPEC §12 says hr_manager defaults go in `IdentitySeeder.cs` → added to `SystemRoles.Defaults` in `Permissions.cs` instead (view + fill + approve), IdentitySeeder untouched → the seeder reads defaults from there and only uses them on a new DB, so this is the one place.
- [2026-11-03] Blanks S1: enum columns `smallint` (SPEC §3) while older tables use `integer` → blank enums are `short`-based → SPEC is explicit; nothing else changes.
- [2026-11-03] Blanks S1: `completed_at` meaning → set on Approved AND Rejected (decision reached), not on Cancelled (`cancelled_at`) → one column answers «when did the approval chain end».
- [2026-11-03] Blanks S1: `SetFinalPdf` does not bump `version` → technical write after Approved, not a user change; bumping would 409 a user who still has the page open for nothing.
- [2026-11-03] Blanks S1: domain attachment kind enum named `BlankAttachmentKind` (Domain) → S4's `Application/Blanks/Files/BlankFileKind` must map to it (same numbers 1..7) or reuse it → Domain cannot reference Application.
- [2026-11-03] Blanks S1: `BlankSubmission` audit label → «HR-F07 №12» (code only while no number), subject = EmployeeId → readable in the journal without exposing answers.
- [2026-11-03] Blanks: `IBlankFileInspector` return type (§8 says `BlankFileKind?`) → `BlankFileInspection` (kind + canonical content type + extension, or `BlankFileRejection`: Unrecognized→`blanka.fayl_turi`, MacroEnabled→`fayl_makro`, LegacyOrEncrypted→`fayl_eski_format`, ArchiveTooLarge (zip bomb)→`fayl_katta`) → a bare nullable cannot tell the macro / legacy / zip-bomb codes of §7 apart; `[Content_Types].xml` read is capped at 1 MB, templates (.dotx/.xltx/.potx) refused as `fayl_turi`.
- [2026-11-03] Blanks web: no frontend test runner existed → added `vitest` 4.1.12 (devDependency, `pnpm test` = `vitest run`) → SPEC §4.5/§14 S9 require vitest for the shared computed vectors.
- [2026-11-03] Blanks computed: aggregates of nothing → `sum`/`count` = 0, `avg`/`min`/`max` = null; no rounding inside the evaluator (2-decimal rounding is display-only) → SPEC §4.5 only fixes «avg of nothing = null»; the shared vectors file decides at merge.
- [2026-11-03] Blanks computed table columns → bare refs resolve against the same row first, then top level; results keyed `table[i].col` in `computed` → F07 §2 has a per-row computed «pass» column and SPEC §4.5 is silent on row scope.
- [2026-11-03] Blanks conditions → choice compares `.value`, ref compares `.id`, multi matches if ANY selected value matches; a hidden field counts as empty for later conditions → mirrors «hidden values are dropped on save» (§4.4).
- [2026-11-03] Blanks web validation → `readOnly` fields are never «required» on the client (prefill may be missing and the user cannot fix it); the server's `blanka.majburiy` still shows inline if it disagrees.
- [2026-11-03] Blanks renderer `ref` field → page-injected `searchRef(kind, query)` instead of reusing `AttendanceEmployeePicker` → that picker fetches via react-query internally; the renderer is props-only by brief.
- [2026-11-03] Blanks `datetime` answers → written as `yyyy-MM-ddTHH:mm:00+05:00` from a native datetime-local input → SPEC §4.3 «ISO+offset» and the project's Tashkent +5 display rule.
- [2026-11-03] Blank computed ops beyond SPEC §4.5 (INTAKE 3b: working days, quorum, Σ weights, F07 «2 dec») → added `round` (half away from zero), `addMonths` (clamps to month end), `addWorkingDays`/`diffWorkingDays` (Mon–Fri, no holidays), `gt`/`lte`, `and`/`or`/`not`; Σ weights = `eq(sum, 100)`; sum/count of nothing = 0 → covered by computed-vectors.json so the TS evaluator matches.
- [2026-11-03] Blank conditions/computed evaluation → type-agnostic on JSON (choice → .value, ref → .id, multi → any selected; `filled:true` on `false` bool is true; `check=false` counts as empty only for required/completeness) → one rule set for C# and TS.
- [2026-11-03] Blank answers validator → first error only (detail = path, codes blanka.notogri/majburiy/json), extra on a non-extra option and hidden/static/computed keys silently dropped, unknown keys rejected, entirely empty free-table rows dropped, choice shorthand "x" → {"value":"x"} → mirrors QuestionnaireFormValidator.
- [2026-11-03] MCP `blank_gaps` scope of `from`/`to` → only hire/termination rules use the range; probation-ending, F07-approved-but-probation-open, overdue and incomplete-Approved are «as of today»; a blank «exists» if not Cancelled (F09 must be Approved); matched by employment_id, else employee_id → SPEC §11 names ranges only for hired/terminated; the rest are current-state checks.
- [2026-11-03] MCP `blank` output → step `answers_patch` and signature presence are not returned (patch duplicates the labelled answers; signature key column is closed to the read-only role); candidate subject names are shown to `blanks.view` like the web list → SPEC §11 exclusions + §7 list shape.
- [2026-11-03] Blankalar: may `fill.at = open` sections be edited while the submission is still Draft? → Yes, open sections are editable in Draft as well as in InApproval → the filler usually knows these notes up front; blocking them until approval adds a round-trip with no control benefit.
- [2026-11-03] Blankalar: does the `waitingForMe` list filter match `field:x` step actors? → No, it ignores `field:x` actors (only role/permission/subject actors resolved without reading answers) → resolving field actors per row needs the answers JSON of every submission; would turn one indexed query into an N-row scan.
- [2026-11-03] Blankalar: which department does a candidate-subject blank belong to? → null → Vacancy has no department column; guessing one would leak the blank into the wrong department scope.
- [2026-11-03] Blankalar: a prefill value fails field validation on create — block creation or drop it? → Drop the value (field stays empty, shows in `missing`) → stale reference data must not make a blank impossible to start; the filler sees the gap and fills it.
- [2026-11-03] Blank template seeder on an invalid file, a same-version hash change, or a file version older than the Published one → Error log, skip that template, startup continues → a template file must never take the whole API down; `WaveOneTemplatesTests` catches invalid files at build time.
- [2026-11-03] Blank `definition_sha256` "canonical JSON" → ordinal-sorted object keys, compact, UTF-8; the canonical text is also what is stored in `definition` → formatting-only edits to a seeded file do not raise a conflict.
- [2026-11-03] Blank PDF API shape (SPEC §10 says `Render → byte[]`) → `IBlankPdfRenderer.RenderAsync(BlankPdfModel, ct) → Task<byte[]>` → mirrors `IQuestionnairePdfRenderer`: an async gate keeps at most two renders at once on the 2-core server.
- [2026-11-03] Blank PDF `legalStatus: draft` marking → «Loyiha — yurist tasdig'idan o'tmagan» line under the title + «LOYIHA ·» in every page header, no rotated watermark → readable on every page, no extra drawing code on the pinned MigraDoc 6.2.4.
- [2026-11-03] Blank PDF `retention` → printed verbatim as the last line, no «Saqlash:» prefix → authored wave-1 strings already carry their own lead («Nusxalar: …»).
- [2026-11-03] Blank PDF signatures block with no materialized steps (Draft) → definition steps whose `when` holds are printed with empty name/date/signature cells → reads like the paper form; Skipped steps print «O'tkazib yuborildi».
- [2026-11-03] Blank PDF long text → a `longtext` answer over 600 chars leaves the label/value grid and flows as a bordered paragraph → MigraDoc never splits a table row across pages, so a huge cell would be clipped.
- [2026-11-03] Blank attachment upload vs the file field answer (SPEC §4.3 stores attachment ids in answers; §7 #17 takes only fieldPath+file) → upload appends the new id to the field's answer and remove strips it, completeness recomputed without re-validating other answers; `version` rises (by 2: attachment + answers), client re-reads the detail → a required file field leaves `missing` at once, and a removed file can never leave a dangling id that would fail the next save.
- [2026-11-03] Blank file in a table cell (`handover[2].doc`) → the row must already be saved, else 400 `blanka.notogri` → the server never invents table rows; the form saves the row before uploading into it.
- [2026-11-03] Blank upload/remove permission gate → endpoint requires `blanks.view`, the handler requires an editable section (`blanks.fill` or Open-step actor, incl. in-person operator for `inPerson`/`either` steps) → same rule as #9 save answers; no editable section → 403 `blanka.ruxsat_yoq`, field's section not editable → 409 `blanka.maydon_tahrirlanmaydi`.
- [2026-11-03] Blankalar web: an attachment upload/removal bumps `version` — does the fill page reload answers? → No: it re-reads the submission, takes only the new `version` and attachment list, keeps the local answers → the bump is the user's own action; reloading answers would drop unsaved typing.
- [2026-11-03] Blankalar web: candidate-subject templates in the create dialog → listed but disabled → candidate subject is Phase 2 (SPEC §14); `ref` search for kind `candidate` returns nothing until then.
- [2026-11-03] Blankalar web: unknown `status` string from the server → treated as `InApproval` → read-only everywhere is the harmless fallback; an unknown value never becomes an editable draft.
- [2026-11-03] Blanks S6: where is `decisionExtra` stored (no column in §3.4)? → inside `answers_patch` under the reserved key `$decisionExtra` → field ids never start with `$`; no schema change; S7 PDF reads it from there.
- [2026-11-03] Blanks S6: who may return/reject a step? → only an ELECTRONIC actor of the Open step (§6.2); an in-person operator (`blanks.fill`) cannot → spec says "step actor"; wave-1 canReturn/canReject steps are electronic office steps anyway.
- [2026-11-03] Blanks S6: `answers_patch` content → snapshot of the step's own section values (answers + computed) at signing time, even if they were saved earlier via #9 → it is the legal trail of what the signer saw/confirmed.
- [2026-11-03] Blanks S6: in-person signature larger than 1200×600 → rejected (`blanka.imzo_yaroqsiz`), not downscaled → mirrors `IImageProcessor.ToFlatPngAsync` / F02 rule exactly; the tablet canvas must stay within bounds.
- [2026-11-03] Blanks S6: final-PDF seam → `IBlankApprovedHook.OnApprovedAsync(submissionId)` called after COMMIT when a step act makes the blank Approved; default no-op (TryAdd); S7 registers its implementation. Submit-with-no-steps → Approved (S5 path) does NOT call it yet → S7 wires that too.
- [2026-11-03] Blankalar S12: where does an actor fill the step's own sections? → inside the approve / sign dialog (`BlankForm show="editable"`), sent as `answers` with #12/#13; the view page edits only `open` sections inline with an explicit «Saqlash» (PUT #9, saved before any step dialog opens) → one place per act, the version never races between page and dialog.
- [2026-11-03] Blankalar S12: «Qaytarish»/«Rad etish» buttons → only when `actionableSteps[].electronic` (return: definition `canReturn !== false`; reject: `canReject === true` AND `blanks.approve`, the static permission of #15) → in-person operators sign for someone else and must not return/reject in their name.
- [2026-11-03] Blankalar S12: step decision extra in the dialogs → text / longtext / date / int inputs only (ref/docRef extras fall back to text) → wave-1 templates use only `text` extras.
- [2026-11-03] Blankalar S12: `SubjectBlanksTab` takes `employee` (id, name, position, department) instead of SPEC's bare `employeeId` → the create dialog preset needs the picked-employee shape; no extra request.
- [2026-11-04] Unscheduled night shift on directional terminals: how long may an evening entry wait for the next-morning exit? → 16 h (AttendanceRules.OvernightExitMaxHours); unknown-direction terminals excluded → owner: «entry→exit one-to-one with or without a schedule» at Metan (separate entry/exit terminals); 19:00→08:00 is 13 h, a forgotten weekend exit must not become 60 h
- [2026-11-04] Main's «Blankalar» engine (merged 2026-11-03) ships its own blank definitions (`Blanks/Templates/*.json`) and PDF renderer, against the HRMentor-only order of 2026-11-01 → recorded as debt D6 in `HujjatManbaiQulfiTests` and SPEC-hujjat-manbai §4, frozen by a new ratchet (no new definition file), release still waits for the CEO's word → the engine is a teammate's merged work: deleting it is not this branch's call, leaving the guard red blocks every release, widening it silently would hide the conflict.
- [2026-11-04] «Mening ishlarim»: F01 and F12 — new blanks or extend «Vakansiya so'rovlari» / «Rag'bat va jarima»? → link, don't duplicate: the process step uses the existing record (vacancy request, incentive) and the blank only carries the extra fields → owner: «do not change the existing system inside»; one source of truth
- [2026-11-04] «Mening ishlarim»: active gates vs «don't change existing modules» → gates are enforced through a small set of named hook points (a guard interface the existing command calls), listed in the SPEC; no other existing logic changes → owner chose active gates (2026-11-04) and additive work
- [2026-11-04] Mening ishlarim: how do steps learn that work happened? → reconcile from facts (Hangfire `processes:reconcile` 1-59/5 + inline on reads), not domain events → no events are raised anywhere; adding them edits aggregates; imports/ETL bypass aggregates
- [2026-11-04] Mening ishlarim: how to keep existing tests green with active gates? → engine dormant until `processes.startedOn` is written by the seeder (never in Testing); older subjects are `legacy` and only warn → existing tests hire without a candidate
- [2026-11-04] Mening ishlarim: gate override without changing command signatures → single-use override row valid 24 h, consumed by `IProcessGate` inside the command → additive
- [2026-11-04] Mening ishlarim: AKT (empty F09 sections) → warning only → blocking would change the blank engine
- [2026-11-04] Mening ishlarim: pipeline steps bind to stages → by owner-configured stage id (`process_stage_bindings`), never by name → stages are HR-editable
- [2026-11-04] Mening ishlarim: F03 before or after council → after (prototype, owner 2026-11-01) → newer than INTAKE/R02
- [2026-11-04] Mening ishlarim: key of `process_stage_bindings` / `process_holidays` → Guid `id` PK plus UNIQUE `binding_code` / `date` → audit coverage needs a single Guid key on every audited entity
- [2026-11-04] Mening ishlarim: what Complete / Reopen do to open steps → Complete marks every open step Skipped(caseEnded), Reopen relocks every caseEnded step and the next pass recomputes it → one rule for both end paths (all steps closed, or an end fact), Reopen is its exact inverse
- [2026-11-04] Mening ishlarim: where the reopen reason lives → in the reopen audit event, no column on `process_cases` → it is history only; `cancel_reason` is cleared on reopen
- [2026-11-04] Mening ishlarim: error codes for a wrong case status and an unusable override → new `jarayon.holat_notogri` and `jarayon.istisno_yaroqsiz` (409) → distinct from `jarayon.qadam_holati_notogri` so the UI can say which one failed
- [2026-11-05] Mening ishlarim: a holiday HR deleted (endpoint #17) must not come back on restart, but the SPEC has no marker → a seed year with ANY row is left alone, per year so a later release can add a year → same idea as `CatalogSeeder`, no new setting; only a year emptied completely is re-seeded
- [2026-11-05] Mening ishlarim: where the `process_holidays` seed lives → startup seeder `ProcessHolidaySeeder`, not a migration → the P1 migration needs no change, and rows land in the audit log as `startup:process-templates`
- [2026-11-05] Mening ishlarim: which seeder writes `processes.startedOn` (§6.6 says "the seeder") → `ProcessTemplateSeeder`, after the templates, only if the run had no invalid file → a broken release stays dormant instead of running a partial engine; any existing row, even empty, is respected so an operator can hold the engine dormant
- [2026-11-05] Mening ishlarim: invalid template "startup error in Development" (§3.1) → all files are validated first and the error names every bad file, before any write; Testing behaves like Production (logged and skipped) → the SPEC names Development only, and `ProcessTemplatesTests` already fails the build on a bad file
- [2026-11-05] Mening ishlarim: §7.5 says ex-11 Done ends a dismissal, but ex-11 closes on the same facts as ex-7 + ex-8, so in the pass that records the termination it would skip ex-8b, ex-9 and ex-10 (the final settlement and its Q12 block) → `terminated` applies once every non-optional step is closed, ex-11 Done among them → same reasoning as hire-candidate and r02-13; the steps' dues and gates in §3.7, §8.1 only make sense after the termination
- [2026-11-05] Mening ishlarim: «employment back to Active» of a withdrawn dismissal (§7.5) → status Active or Probation; an unknown status never withdraws → an employee back on probation is working too, and a missing value must not close a case
- [2026-11-05] Mening ishlarim: which subjects count as a legacy dismissal (§7.3) → only one with a live F08 or dismissal F09 older than `startedOn` → the other reading (employee hired before `startedOn`) would reduce Q12/Q13 to warnings for all current staff, against «gates active». Riskiest call of the engine; the owner should confirm it.
- [2026-11-05] Mening ishlarim: does a hire end hire-candidate (§7.5 lists it as an end fact)? → no, outcome `hired` applies once r02-13 is also Done → ending at the hire would skip r02-13 and its Q04 gate.
- [2026-11-05] Mening ishlarim: waiting state 3 («waiting for another case», §7.4) → never written; r02-13 is Blocked by Q04 instead (§8.3) → one mechanism for the one case that needs it.
- [2026-11-05] Mening ishlarim: when does a dismissal end `withdrawn` (§7.5)? → only when ex-2 or ex-8 was fact-Done before the pass → otherwise a fresh manual or F08 start, on an employee who is still Active with no open F09, would end immediately.
- [2026-11-05] Mening ishlarim: hire-formalise (a) whose candidate never gets an employment (§7.5 has no rule) → `cand.status` 2/4/6 with no employment completes it with that status as outcome → otherwise it could never close.
- [2026-11-05] Mening ishlarim: `appliedAt` for candidate facts (§7.2) → start of the Tashkent day taken from `subject_ref` → stage entries recorded earlier on the application day count.
- [2026-11-05] Mening ishlarim: gate overrides in the pass (§8) → `ProcessEngineGates` does not read them yet → overrides arrive in P5, which extends that class.
- [2026-11-05] Mening ishlarim: which blanks start a dismissal (§3.2 says non-Cancelled) → `BlankFactRows.LiveStatuses`, so Rejected is out too → a rejected blank proves nothing, and the facts read the same statuses.
- [2026-11-05] Mening ishlarim: imported hire events in the hire-formalise (b) scan → excluded (`is_imported`) → they are historic Excel rows, not hires made under the engine.
- [2026-11-05] Mening ishlarim: a newer start fact on a `withdrawn` dismissal (§7.5 reopen) → `DismissalStart.LatestFactAt` (added beyond the §7.3 scan), and for a termination it is the recording time, not the date → a dismissal withdrawn in the morning and terminated that afternoon has a termination date older than its withdrawal.
- [2026-11-05] Mening ishlarim: an unreadable `processes.startedOn` → counts as absent, engine dormant → only the seeder writes it, and dormant leaves every existing flow as it was.
- [2026-11-05] Mening ishlarim: a dismissal blank with no `employment_id` on an employee with two live employments → starts a case on both, the fact source's ownership rule copied as is → the scan and the facts must agree; narrowing it is one change in both places if it ever shows up.
- [2026-11-05] Mening ishlarim: §7.3 `INSERT … ON CONFLICT DO NOTHING` → a `subject_ref` check before adding the aggregate, inside the engine's lock → a case is inserted with its steps through EF (audit, change signal), and only a pass inserts. A manual start (`POST /cases`) has to take the same lock.
- [2026-11-05] Mening ishlarim: a reapplication and the old application's formalise case with no employment (beyond §6.9) → completed `reapplied` with its parent → nothing could close it, and it would claim the new application's hire.
- [2026-11-05] Mening ishlarim: `cand.hired` naming an employment another case of that process already holds → not linked, journalled as a case problem → the partial UNIQUE would otherwise fail every pass for good.
- [2026-11-05] Mening ishlarim: accepted offer on a parent that ended otherwise than `hired` → no hire-formalise (a) → nobody is left to formalise, and the child would only close again on `cand.status`.
- [2026-11-05] Mening ishlarim: one-case reconcile (§7.4) and an accepted offer → starts hire-formalise (a) there too → if that run also completes the parent, no later full pass looks at it again.
- [2026-11-05] Mening ishlarim: cron of `processes:reconcile` (§7.4, H15 say `1-59/5`) → `4-59/5` → `hrmentor:sync` already holds `1-59/5` and the Hangfire server has two workers; it shares :12/:27/:42/:57 with `attendance:employment-changes`.
- [2026-11-05] Mening ishlarim: who an inline pass (§7.4) is recorded as → nobody, the pass runs with the execution context suppressed → otherwise the request's `HttpContext` flows in and whoever opened the page becomes the author of the engine's rows and audit lines.
- [2026-11-05] Mening ishlarim: a case detail read (§7.4) and the 15 s throttle → no throttle → §7.4 throttles the reads' full pass; opening a case reconciles that case, and the lock still guards it.
- [2026-11-05] Mening ishlarim: acting user in `GateContext` (§8.2 lists it) → the gate reads `ICurrentUser` itself → the handlers have no current user, and H13/H14 allow one dependency and one call.
- [2026-11-05] Mening ishlarim: a gate warning on Terminate and the Confirm flow (§8.2) → added to the result's `warnings`, no extra 409 → the web offers a confirm only for the flag codes, so a 409 on another code would leave HR with no way forward; `confirm=true` never passes a Block.
- [2026-11-05] Mening ishlarim: two commands racing on one override (§6.4 single use) → `consumed_at` is a concurrency token, the loser gets 409 `jarayon.istisno_yaroqsiz` from `GlobalExceptionHandler` (outside the §13 list) → the handlers have no transaction to lock in, and a 500 would read as a broken system.
- [2026-11-05] Mening ishlarim: who consumes an engine gate's override (§6.4 names only hire / terminate) → the pass, once the guarded step closes, with new `consumed_operation` value `step`; until then the step stays unblocked while the override is usable → the column has no CHECK, and otherwise an undo within 24 h would pass the gate a second time.
- [2026-11-05] Mening ishlarim: #14 names a gate, and Q12 sits on ex-8 and ex-10 → the step the gate holds now, else the first open step carrying it; ex-10's override is the engine's and Terminate ignores it → the route has no step parameter.
