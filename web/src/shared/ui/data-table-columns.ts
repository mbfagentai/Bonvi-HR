import {
  createColumnHelper,
  createSortedRowModel,
  metaHelper,
  rowSortingFeature,
  sortFn_alphanumeric,
  sortFn_basic,
  sortFn_text,
  tableFeatures,
  type ColumnDef,
  type RowData,
} from '@tanstack/react-table'

/**
 * Ustunning ko'rinish sozlamalari.
 * TanStack v9 da meta tipi global emas, `features` ichida e'lon qilinadi —
 * shu sababli u faqat shu jadval oilasiga tegishli bo'lib qoladi.
 */
export interface DataColumnMeta {
  /** Raqamli ustunlar o'ngga tekislanadi. */
  align?: 'start' | 'end'
  /**
   * CSS kengligi: `'120px'`, `'22%'`. QAT'IY o'lcham — ustun ORTGAN JOY
   * TAQSIMOTIDAN CHIQIB KETADI (brauzer uni proporsional kengaytirmaydi,
   * `table-layout: auto` shu ustunni har doim aynan shu kenglikda chizadi).
   */
  width?: string
  /**
   * Faqat PASTKI chegara: ustun `width`dan farqli, moslashuvchan bo'lib
   * qoladi va boshqa moslashuvchan ustunlar bilan birga ortgan joyni
   * o'zi oladi — faqat undan torroq bo'la olmaydi. Bitta jadvalda ko'pi
   * bilan bitta ustun kengliksiz/`minWidth`li qolishi kerak: aks holda
   * ortgan joy ular o'rtasida taqsimlanadi va tor ekranda har biri
   * o'zining `minWidth`iga qisiladi (bu ham maqbul, lekin kutilmagan).
   */
  minWidth?: string
  /** Tor ekranda yashiriladi — telefonda faqat eng muhim ustunlar qoladi. */
  hideOnMobile?: boolean
}

/**
 * Jadval imkoniyatlari: faqat saralash. Filtrlash va sahifalash ataylab yo'q —
 * ma'lumotnomalar kichik (o'nlab yozuv), qidiruv esa ro'yxat darajasida
 * bajariladi. Keraksiz imkoniyat bundle'ni ham, holatni ham og'irlashtiradi.
 *
 * NEGA alohida fayl: React Fast Refresh bitta modulda ham komponent, ham
 * boshqa eksport bo'lsa ishlamaydi va butun daraxtni qayta yuklaydi.
 * Ustun yordamchilari komponent emas — shu sababli ular shu yerda.
 */
export const dataTableFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
    basic: sortFn_basic,
    text: sortFn_text,
  },
  columnMeta: metaHelper<DataColumnMeta>(),
})

export type DataTableFeatures = typeof dataTableFeatures

/** Ustun ta'rifi. Chaqiruvchi tomon `dataColumnHelper` bilan yaratadi. */
export type DataColumn<TData extends RowData> = ColumnDef<DataTableFeatures, TData, any>

/** `createColumnHelper` ning shu jadval uchun oldindan bog'langan varianti. */
export function dataColumnHelper<TData extends RowData>() {
  return createColumnHelper<DataTableFeatures, TData>()
}
