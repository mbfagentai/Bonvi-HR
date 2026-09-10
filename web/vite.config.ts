import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig(({ mode }) => ({
  plugins: [
    // Marshrutlarni src/app/routes papkasidan avtomatik yig'adi
    tanstackRouter({
      target: 'react',
      autoCodeSplitting: true,
      routesDirectory: './src/app/routes',
      generatedRouteTree: './src/app/routeTree.gen.ts',
    }),
    react(),
    tailwindcss(),
  ],

  /**
   * ⚠️ XAVFSIZLIK: dev-rejimdagi namunaviy hisob ma'lumotlari production
   * bundle'iga tushmasligi kafolatlanadi.
   *
   * Muhitda `VITE_DEV_PASSWORD` bo'lsa ham, production build'da u
   * `undefined` bilan almashtiriladi va bog'liq kod o'lik kodga aylanadi.
   * Ya'ni himoya build jarayonining tozaligiga bog'liq emas.
   */
  define:
    mode === 'production'
      ? {
          'import.meta.env.VITE_DEV_LOGIN': 'undefined',
          'import.meta.env.VITE_DEV_PASSWORD': 'undefined',
          'import.meta.env.VITE_DEV_LABEL': 'undefined',
        }
      : {},

  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },

  server: {
    host: '0.0.0.0',
    port: 7173,   // 7xxx — BonviHR yo'lagi (boshqa loyihalar bilan to'qnashmaydi)
    // Docker bind-mount'da fayl o'zgarishini sezish uchun
    watch: {
      usePolling: true,
      interval: 1000,
      binaryInterval: 3000,
      ignored: ['**/node_modules/**', '**/dist/**', '**/.git/**'],
    },

    /**
     * Tashqi domendan kirishga ruxsat (Cloudflare tunnel va shunga o'xshash).
     *
     * Vite noma'lum `Host` sarlavhasi bilan kelgan so'rovni ATAYLAB rad
     * etadi — bu DNS rebinding hujumidan himoya. Tunnel ochilganda domen
     * boshqa bo'ladi, shu sababli u ochiq ro'yxatga qo'shilishi kerak.
     *
     * Muhitdan olinadi va sukut bo'yicha BO'SH: oddiy ishda himoya
     * kuchida qoladi, faqat tunnel rejimida yumshatiladi.
     */
    allowedHosts: (process.env['VITE_ALLOWED_HOSTS'] ?? '')
      .split(',')
      .map((host) => host.trim())
      .filter(Boolean),

    /**
     * `/api` va `/health` ni backend'ga uzatadi.
     *
     * NEGA kerak: tunnel ortidagi brauzer uchun `http://localhost:7080`
     * — bu O'ZINING kompyuteri, bizning API emas. Shu sababli tunnel
     * rejimida `VITE_API_URL` bo'sh qoldiriladi va mijoz nisbiy manzilga
     * murojaat qiladi — u shu proksi orqali API'ga tushadi. Natijada
     * frontend ham, API ham BITTA manzil ostida bo'ladi va CORS umuman
     * kerak bo'lmaydi.
     *
     * Oddiy ishda `VITE_API_URL` to'liq manzil bo'lgani uchun bu qoida
     * hech qachon ishga tushmaydi.
     */
    proxy: {
      '/api': { target: 'http://api:8080', changeOrigin: true },
      '/health': { target: 'http://api:8080', changeOrigin: true },
    },
  },

  build: {
    outDir: 'dist',

    /**
     * Manba xaritalari FAQAT development build'ida.
     *
     * NEGA production'da yo'q: `dist/` ichidagi `.map` fayllari butun
     * TypeScript manbasini — izohlari, o'zgaruvchi nomlari va biznes
     * qoidalari bilan — ochiq holda tarqatadi. Tizim internetga chiqarilsa
     * (`./bonvi tunnel` yoki haqiqiy domen), brauzerni ochgan har kim uni
     * yuklab olardi. Bundan tashqari xaritalar bundle hajmidan kattaroq:
     * 1,2 MB kod uchun 4 MB xarita.
     *
     * Ishlab chiqishda esa ular SHART: xaritasiz brauzerdagi xato
     * `index-Bz3Eg3uu.js:1:48211` bo'lib ko'rinadi.
     */
    sourcemap: mode !== 'production',

    // Ogohlantirish chegarasi: bundle kattalashib ketsa darhol bilinsin
    chunkSizeWarningLimit: 600,
  },
}))