import { defineConfig, globalIgnores } from 'eslint/config'
import { builtinRules } from 'eslint/use-at-your-own-risk'
import ts from 'typescript'

/**
 * ⭐ NEGA BU FAYL BOR
 *
 * `package.json` da `"lint": "eslint ."` bor edi, lekin `eslint.config.*`
 * yo'q edi. ESLint 9+ flat config'siz umuman ishga tushmaydi, shu sababli
 * `pnpm lint` har safar «couldn't find an eslint.config.* file» bilan
 * yiqilardi — ya'ni loyihada lint amalda MAVJUD EMAS edi.
 *
 * QAMROV
 *   • ESLint yadrosining «recommended» to'plami — barcha JS/TS fayllarda;
 *   • TypeScript qoidalari — `typescript-eslint` ning oddiy (tipsiz)
 *     `recommended` to'plami, LEKIN u ishlay olganda (pastdagi «TS 7» izohi).
 *
 * NEGA TIP-BILAN-TEKSHIRUV (`recommended-type-checked`) OLINMAGAN
 *   U har bir fayl uchun butun dastur tiplarini qayta quradi — lint
 *   sekundlardan daqiqalarga cho'ziladi. Bu yerda foyda ham kam: `tsconfig`
 *   allaqachon eng qat'iy rejimda (`strict`, `noUncheckedIndexedAccess`,
 *   `noUnusedLocals`, `verbatimModuleSyntax` …), ya'ni tip xatolarini
 *   `tsc` topadi. Lint esa tip bilmasdan ham topiladigan narsalar uchun.
 *
 * QOIDA O'CHIRILGAN JOYLAR
 *   Har bir `off`/`warn` ostida NEGA shunday qilingani yozilgan. Umumiy
 *   tamoyil: ishlab turgan kodni lint uchun qayta yozmaymiz — takrorlanuvchi
 *   yoki bu loyihada ma'nosiz qoida o'chiriladi, kod emas.
 */

/**
 * ESLint yadrosining «recommended» ro'yxatini o'zidan yig'amiz.
 *
 * Odatda bu `@eslint/js` paketidan olinadi. Bu loyihada u YO'Q va uni
 * qo'shish mumkin emas (bog'liqlik qo'shish bu ish doirasidan tashqarida),
 * qolaversa ESLint 10 endi `@eslint/js` ga bog'liq ham emas — u alohida
 * paket bo'lib qoldi.
 *
 * Ro'yxatni qo'lda ko'chirib yozish (~64 nom) eng yomon variant: ESLint
 * yangilanganda nom o'zgarsa yoki qoida olib tashlansa, lint «Definition
 * for rule … was not found» deb YIQILADI. Shu sababli ro'yxat ESLint'ning
 * o'z metama'lumotidan hosil qilinadi — `@eslint/js` ham aynan shu
 * `meta.docs.recommended` bayrog'idan foydalanadi. Natijada ro'yxat
 * hech qachon eskirmaydi.
 */
const jsRecommended = Object.fromEntries(
  [...builtinRules]
    .filter(([, rule]) => rule?.meta?.docs?.recommended)
    .map(([name]) => [name, 'error']),
)

/**
 * ⚠️ TS 7 — typescript-eslint hozircha ishlay olmaydi.
 *
 * Loyihada TypeScript 7 (mahalliy port) turibdi, `typescript-eslint@8.67`
 * esa TS >= 7 ni ko'rsa import paytining o'zidayoq ATAYLAB istisno tashlaydi
 * (qarang: typescript-eslint#10940). Uni shartsiz import qilsak, butun
 * `pnpm lint` yana ishga tushmaydigan bo'lib qoladi — ya'ni tuzatilgan
 * nosozlik qaytib keladi, faqat boshqa xabar bilan.
 *
 * Shu sababli versiya OLDINDAN tekshiriladi va paket faqat ishlay oladigan
 * bo'lsa yuklanadi. Bunda:
 *   • bugun — JS/MJS fayllari tekshiriladi, TS fayllari esa CHETLAB
 *     o'tiladi (ularni ochadigan parser yo'q; tip xavfsizligini `tsc -b`
 *     ta'minlab turibdi, `pnpm typecheck` shu ish uchun);
 *   • typescript-eslint TS 7 ni qo'llab-quvvatlagan kuni — hech narsa
 *     o'zgartirmasdan, o'zi yoqiladi.
 */
const tsMajor = Number.parseInt(ts.versionMajorMinor, 10)
const tseslint = tsMajor >= 7 ? null : (await import('typescript-eslint')).default

/**
 * TS fayllari faqat ularni o'qiy oladigan parser bo'lganda ro'yxatga
 * kiritiladi. Aks holda ESLint ularni espree bilan ochib, har bir faylda
 * «Parsing error» berardi — bu xatolar kod sifatiga umuman aloqasiz.
 */
const linted = tseslint ? '**/*.{js,mjs,cjs,ts,tsx}' : '**/*.{js,mjs,cjs}'

if (!tseslint) {
  console.warn(
    `[eslint] TypeScript ${ts.version} — typescript-eslint hali TS 7 ni qo'llamaydi.\n` +
      '         .ts/.tsx fayllari lintdan chetlab o\'tildi; tiplarni "pnpm typecheck" tekshiradi.',
  )
}

/**
 * Node muhitidagi skriptlar uchun global nomlar.
 *
 * Odatda `globals` paketidan olinadi — u ham o'rnatilmagan. Bu yerda
 * kerakli global juda kam (`scripts/*.mjs` va konfiguratsiya fayllari),
 * shu sababli ro'yxat qo'lda yozilgan: bog'liqlik qo'shishdan afzal.
 */
const nodeGlobals = {
  console: 'readonly',
  process: 'readonly',
  URL: 'readonly',
  URLSearchParams: 'readonly',
  TextEncoder: 'readonly',
  TextDecoder: 'readonly',
  fetch: 'readonly',
  setTimeout: 'readonly',
  clearTimeout: 'readonly',
  setInterval: 'readonly',
  clearInterval: 'readonly',
  queueMicrotask: 'readonly',
  structuredClone: 'readonly',
}

export default defineConfig([
  /**
   * Tekshirilmaydigan yo'llar.
   *
   * `generated/` va `routeTree.gen.ts` — MASHINA yozadigan kod
   * (`./bonvi gen` va TanStack Router plugin'i). Ularni lint qilish
   * ma'nosiz: xatoni tuzatib bo'lmaydi, keyingi generatsiya baribir
   * hammasini qayta yozadi.
   *
   * `.pnpm-store/` — konteyner ichidagi paket keshi, o'n minglab fayl.
   * ESLint uni ham kezib chiqardi va lint bir necha barobar sekinlashardi.
   */
  globalIgnores([
    'dist/**',
    'node_modules/**',
    '.pnpm-store/**',
    '.tanstack/**',
    'src/shared/api/generated/**',
    'src/app/routeTree.gen.ts',
    '**/*.tsbuildinfo',
  ]),

  {
    name: 'bonvi/til-sozlamalari',
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
    },
    linterOptions: {
      /**
       * Kodda `// eslint-disable-next-line react-hooks/exhaustive-deps`
       * izohlari bor. `eslint-plugin-react-hooks` bu loyihaga ATAYLAB
       * o'rnatilmagan, shu sababli ESLint bu izohlarni «keraksiz» deb
       * belgilardi. Ularni o'chirish esa xato bo'lardi: qoida o'zi
       * muharrirning React vositalarida ishlaydi va izohlar o'sha yerda
       * kerak. Shovqinni yo'qotamiz, izohlar joyida qoladi.
       */
      reportUnusedDisableDirectives: 'off',
    },
  },

  {
    name: 'bonvi/js-recommended',
    files: [linted],
    rules: jsRecommended,
  },

  {
    name: 'bonvi/node-skriptlari',
    files: ['**/*.{js,mjs,cjs}', 'vite.config.ts', 'orval.config.ts'],
    languageOptions: { globals: nodeGlobals },
  },

  /**
   * TypeScript. typescript-eslint yuklanmagan bo'lsa — bo'sh ro'yxat,
   * ya'ni `.ts`/`.tsx` uchun hech qanday `files` yozuvi qo'shilmaydi va
   * ESLint ularni umuman ochmaydi (parser yo'qligi sabab har bir fayl
   * «Parsing error» berardi).
   */
  ...(tseslint
    ? [
        ...tseslint.configs.recommended.map((config) => ({
          ...config,
          files: ['**/*.{ts,tsx}'],
        })),
        {
          name: 'bonvi/ts-tuzatishlar',
          files: ['**/*.{ts,tsx}'],
          rules: {
            /**
             * `no-undef` TypeScript'ni tushunmaydi: tip nomlarini,
             * `declare` e'lonlarini va DOM/Node tiplarini «aniqlanmagan»
             * deb hisoblaydi. Haqiqiy aniqlanmagan nomni `tsc` allaqachon
             * xato deb beradi — bu qoida faqat yolg'on ogohlantirish qo'shadi.
             */
            'no-undef': 'off',

            /**
             * `tsconfig.app.json` da `noUnusedLocals` va
             * `noUnusedParameters` yoqilgan — ishlatilmagan o'zgaruvchini
             * `tsc` topadi va build'ni to'xtatadi. Lint buni takrorlasa,
             * bitta muammo ikki marta chiqadi, xolos.
             */
            '@typescript-eslint/no-unused-vars': 'off',
          },
        },
      ]
    : []),
])
