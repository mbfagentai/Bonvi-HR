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
