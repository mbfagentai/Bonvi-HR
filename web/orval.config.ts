import { defineConfig } from 'orval'

/**
 * ⭐ IKKI TIL — BITTA SHARTNOMA
 *
 * Backend C# da yozilgan, frontend TypeScript'da. Ular orasida tip ulashish
 * imkoni yo'q edi — bu muammoni kod generatsiyasi hal qiladi:
 *
 *     C# Controller → OpenAPI spec → Orval → TS tip + React Query hook
 *
 * Backend'da DTO o'zgardi → `./bonvi gen` → frontend darhol qizaradi.
 * Shartnoma manbai qo'lda yozilgan tip emas, HAQIQIY API'ning o'zi.
 *
 * MUHIM: generatsiya qilingan kod qo'lda tahrirlanmaydi va git'ga tushmaydi
 * (.gitignore ga kiritilgan). U har doim backend'dan qayta hosil qilinadi.
 */
export default defineConfig({
  bonvi: {
    input: {
      // .NET 10 OpenAPI hujjatini shu manzilda beradi
      target: process.env['VITE_OPENAPI_URL'] ?? 'http://api:8080/openapi/v1.json',
    },
    output: {
      mode: 'tags-split',
      target: './src/shared/api/generated/endpoints.ts',
      schemas: './src/shared/api/generated/model',
      client: 'react-query',
      httpClient: 'axios',
      clean: true,
      override: {
        // Barcha so'rovlar bizning axios nusxamizdan o'tadi:
        // JWT qo'shish, token yangilash, xatoni tarjima qilish shu yerda
        mutator: {
          path: './src/shared/api/http-client.ts',
          name: 'httpClient',
        },
        query: {
          useQuery: true,
          useSuspenseQuery: true,
        },
      },
    },
  },
})
