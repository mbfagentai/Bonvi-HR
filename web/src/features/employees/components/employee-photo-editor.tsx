import { Camera, Expand, LoaderCircle, Trash2, Upload } from 'lucide-react'
import { useRef, useState, type ChangeEvent } from 'react'

import { useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { PHOTO_ACCEPT, photoFileError, toMegabytes } from '@/shared/lib/photo'
import { croppedOrOriginal, type CropArea } from '@/shared/lib/photo-crop'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Alert, Button, Dialog, FileViewer, PhotoCropper } from '@/shared/ui'
import { useDeleteEmployeePhoto, useEmployeePhoto, useUploadEmployeePhoto } from '../api/employees-queries'
import { employeeErrorCode } from '../lib/employee-error'
import { EMPLOYEES_EDIT } from '../lib/employee-presentation'
import { EmployeePhoto } from './employee-photo'

/**
 * Profil sahifasidagi rasm — ko'rsatish va boshqarish.
 *
 * HR rasmlarni qo'lda, bittalab yuklaydi: Excel'da rasm ustuni umuman
 * bo'lmagan. Shu sababli yo'l imkon qadar qisqa — rasmning O'ZIGA bosiladi
 * (yonidagi kichkina qalamchani izlab yurish shart emas), fayl tanlanadi,
 * oynada ko'rib chiqiladi va yuklanadi.
 *
 * Ruxsati yo'q foydalanuvchi uchun bu oddiy rasm: hech qanday tugma,
 * kursor o'zgarishi yoki «bosib bo'lmadi» xabari yo'q.
 */
export function EmployeePhotoEditor({
  employeeId,
  photoKey,
  name,
}: {
  employeeId: string
  photoKey: string | null
  name: string
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const inputRef = useRef<HTMLInputElement>(null)

  const [picked, setPicked] = useState<File | null>(null)
  /** Kvadratning joyi — rasmni surib-kattalashtirib belgilanadi (`PhotoCropper`). */
  const [crop, setCrop] = useState<CropArea | null>(null)
  /*
   * Ikkita alohida xato holati.
   *
   * NEGA bitta emas: ikkala oyna bir vaqtda mavjud (biri ochiq, biri
   * yopiq) va umumiy holat bo'lsa, o'chirishdagi xato yopiq turgan
   * yuklash oynasini ochib yuborardi.
   */
  const [uploadFailure, setUploadFailure] = useState<string | null>(null)
  const [deleteFailure, setDeleteFailure] = useState<string | null>(null)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [viewing, setViewing] = useState(false)
  // Ko'rish oynasi uchun — avatar bilan bir kesh (`useEmployeePhoto`), ikkinchi so'rov yo'q
  const viewingPhoto = useEmployeePhoto(employeeId, photoKey, viewing)

  const upload = useUploadEmployeePhoto(employeeId)
  const remove = useDeleteEmployeePhoto(employeeId)

  const preview = useObjectUrl(picked)

  if (!hasPermission(EMPLOYEES_EDIT)) {
    return <EmployeePhoto employeeId={employeeId} photoKey={photoKey} name={name} size="xl" />
  }

  const openPicker = () => inputRef.current?.click()

  const onPick = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null

    // Qiymat darhol tozalanadi: aks holda AYNAN o'sha faylni ikkinchi marta
    // tanlash hodisa hosil qilmaydi va oyna umuman ochilmay qoladi
    event.target.value = ''
    if (file === null) return

    const problem = photoFileError(file)
    if (problem !== null) {
      // Yaroqsiz fayl saqlanmaydi — «Yuklash» tugmasi faol bo'lib qolmasin
      setPicked(null)
      setUploadFailure(problem)
      return
    }

    setUploadFailure(null)
    setCrop(null)
    setPicked(file)
  }

  const closeUpload = () => {
    setPicked(null)
    setCrop(null)
    setUploadFailure(null)
  }

  const closeDelete = () => {
    setConfirmingDelete(false)
    setDeleteFailure(null)
  }

  const submit = async () => {
    if (picked === null) return

    setUploadFailure(null)
    try {
      await upload.mutateAsync(await croppedOrOriginal(picked, crop))
      closeUpload()
    } catch (error) {
      setUploadFailure(employeeErrorCode(error))
    }
  }

  const confirmDelete = async () => {
    setDeleteFailure(null)
    try {
      await remove.mutateAsync()
      closeDelete()
    } catch (error) {
      setDeleteFailure(employeeErrorCode(error))
    }
  }

  /*
   * Xato ham aynan shu oynada ko'rsatiladi: fayl tanlash oynasi yopilgach
   * ekranda hech narsa o'zgarmasa, foydalanuvchi «bosildimi?» deb o'ylab
   * yana bosaveradi — va sababini hech qachon bilmaydi.
   */
  const uploadOpen = picked !== null || uploadFailure !== null
  const hasPhoto = photoKey !== null

  return (
    <div className="flex shrink-0 flex-col items-center gap-1.5">
      <input
        ref={inputRef}
        type="file"
        accept={PHOTO_ACCEPT}
        onChange={onPick}
        className="hidden"
        tabIndex={-1}
        aria-hidden
      />

      <div className="relative">
        {/*
          ⭐ Suratning O'ZIGA bosish — uni KATTA KO'RISH, yuklash emas
          (foydalanuvchi talabi, 2026-09-08). Ilgari bu tugma fayl tanlash
          oynasini ochardi: HR suratni kattaroq ko'rmoqchi bo'lib bosardi
          va o'zini «yangi rasm qo'yish» oqimida topib olardi. Ya'ni eng
          ko'p uchraydigan niyatga eng xavfli javob berilardi.

          Surat YO'Q bo'lsa ko'rsatadigan narsa ham yo'q — o'shanda bosish
          avvalgidek fayl tanlashni ochadi.
        */}
        <button
          type="button"
          onClick={() => (hasPhoto ? setViewing(true) : openPicker())}
          aria-label={hasPhoto ? t('photo.view') : t('photo.choose')}
          title={hasPhoto ? t('photo.view') : t('photo.choose')}
          className="press group relative block cursor-pointer rounded-full"
        >
          <EmployeePhoto
            employeeId={employeeId}
            photoKey={photoKey}
            name={name}
            size="xl"
          />

          {/* Sichqoncha ostidagi qatlam — nima bo'lishini oldindan aytadi */}
          <span
            aria-hidden
            className={cn(
              'absolute inset-0 grid place-items-center rounded-full bg-black/45',
              'opacity-0 transition-opacity duration-200 ease-[var(--ease-ios)]',
              'group-hover:opacity-100 group-focus-visible:opacity-100',
            )}
          >
            {hasPhoto ? (
              <Expand className="size-6 text-white" strokeWidth={2} />
            ) : (
              <Camera className="size-6 text-white" strokeWidth={2} />
            )}
          </span>
        </button>

        {/*
          ⭐ Kamera belgisi endi BEZAK EMAS, haqiqiy tugma: u fayl
          tanlashni to'g'ridan-to'g'ri ochadi. Suratni bosish ko'rish
          oynasiga ketgach, yuklash yo'li ekranda ko'rinib turishi
          shart edi — foydalanuvchi talabi aynan shu: «yuklash tugmasi
          ham yo'qolmasin».

          Chegara fon rangida — belgi suratdan «kesib» turadi. Soyasiz:
          to'ldirilgan tint belgisi Button `primary` bilan bir qoidada
          (iOS HIG «Buttons») — ajralishni chegara beradi, ko'lanka emas.
        */}
        <button
          type="button"
          onClick={openPicker}
          aria-label={hasPhoto ? t('photo.change') : t('photo.choose')}
          title={hasPhoto ? t('photo.change') : t('photo.choose')}
          className={cn(
            'press absolute -right-0.5 -bottom-0.5 grid size-7 place-items-center',
            'rounded-full border-2 border-[var(--bg-elevated)]',
            'bg-[var(--accent)] text-[var(--fg-on-accent)]',
          )}
        >
          <Camera className="size-3.5" strokeWidth={2.5} />
        </button>
      </div>

      {hasPhoto && (
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={() => setConfirmingDelete(true)}
          className="text-ios-red hover:bg-ios-red/10 hover:text-ios-red"
        >
          <Trash2 />
          {t('photo.delete')}
        </Button>
      )}

      {/* ───────── Suratni katta ko'rish ─────────
          To'liq ekranli ko'rish oynasi (egasi, 2026-10-30: «yanada kattalashtirib ko'rish
          mumkin bo'lsin»): g'ildirak, chimchilash, ikki marta bosish va sudrash. Surat
          TO'RTBURCHAK, doira emas — oynaning maqsadi uni to'liq ko'rsatish. «Rasmni
          almashtirish» yuqori panelda qoldi: HR «bu boshqa odam ekan» desa, oynani yopib
          izlashi shart emas. Oyna avval yopiladi — fayl tanlash oynasi ustidan chiqmasin. */}
      <FileViewer
        open={viewing}
        onClose={() => setViewing(false)}
        title={name}
        subtitle={t('employees.photoTitle')}
        kind="image"
        blob={viewingPhoto.data ?? null}
        error={viewingPhoto.isError}
        actions={
          <button
            type="button"
            onClick={() => {
              setViewing(false)
              openPicker()
            }}
            className="press flex h-10 items-center gap-2 rounded-full bg-white/10 px-4 text-subhead font-medium transition-colors hover:bg-white/20"
          >
            <Camera className="size-4" />
            <span className="max-sm:sr-only">{t('photo.change')}</span>
          </button>
        }
      />

      {/* ───────── Ko'rib chiqish va yuklash ───────── */}
      <Dialog
        open={uploadOpen}
        onClose={closeUpload}
        size="sm"
        dismissible={!upload.isPending}
        title={t('employees.photoTitle')}
        description={t('photo.subtitle')}
        footer={
          <>
            <Button type="button" onClick={openPicker} disabled={upload.isPending}>
              {picked === null ? t('photo.choose') : t('photo.another')}
            </Button>

            <Button
              type="button"
              variant="primary"
              onClick={() => void submit()}
              disabled={picked === null || upload.isPending}
            >
              {upload.isPending ? <LoaderCircle className="animate-spin" /> : <Upload />}
              {upload.isPending ? t('photo.uploading') : t('photo.upload')}
            </Button>
          </>
        }
      >
        <div className="flex flex-col items-center gap-4">
          {uploadFailure !== null && (
            <Alert
              tone="danger"
              title={t('photo.failed')}
              className="w-full [animation:pop-in_180ms_var(--ease-ios-out)]"
            >
              {errorMessage(uploadFailure)}
            </Alert>
          )}

          {picked !== null && preview !== null ? (
            <>
              {/*
                Kvadratni HR o'zi joylashtiradi (2026-10-12): server rasmni
                markazdan kesardi va bo'yi baland rasmda bosh tushib qolardi —
                Face ID terminali ham yuzni topa olmasdi. Kvadrat — saqlanadigan
                qism, doira — kartadagi avatar chegarasi.
              */}
              <PhotoCropper
                key={preview}
                src={preview}
                onChange={setCrop}
                zoomLabel={t('photo.zoom')}
                hint={t('photo.cropHint')}
              />

              <p className="text-center text-footnote text-[var(--fg-secondary)]">
                <span className="truncate-1 block font-medium text-[var(--fg)]">
                  {picked.name}
                </span>
                <span className="tabular">{num(toMegabytes(picked.size))} MB</span>
              </p>
            </>
          ) : (
            <>
              <EmployeePhoto
                employeeId={employeeId}
                photoKey={photoKey}
                name={name}
                size="xl"
                className="size-40 text-largetitle"
              />

              <p className="text-center text-footnote text-[var(--fg-secondary)]">
                {hasPhoto ? t('photo.squareHint') : t('photo.none')}
              </p>
            </>
          )}
        </div>
      </Dialog>

      {/* ───────── O'chirishni tasdiqlash ───────── */}
      <Dialog
        open={confirmingDelete}
        onClose={closeDelete}
        size="sm"
        dismissible={!remove.isPending}
        title={t('photo.deleteTitle')}
        footer={
          <>
            <Button type="button" onClick={closeDelete} disabled={remove.isPending}>
              {t('common.cancel')}
            </Button>

            <Button
              type="button"
              variant="danger"
              onClick={() => void confirmDelete()}
              disabled={remove.isPending}
            >
              {remove.isPending ? <LoaderCircle className="animate-spin" /> : <Trash2 />}
              {remove.isPending ? t('photo.deleting') : t('common.delete')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-subhead text-[var(--fg-secondary)]">
            {t('employees.photoDeleteText')}
          </p>

          {deleteFailure !== null && (
            <Alert tone="danger" title={t('photo.deleteFailed')}>
              {errorMessage(deleteFailure)}
            </Alert>
          )}
        </div>
      </Dialog>
    </div>
  )
}
