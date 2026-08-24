import { Camera, LoaderCircle, Trash2, Upload } from 'lucide-react'
import { useRef, useState, type ChangeEvent } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { problemCode } from '@/shared/api/problem-details'
import { cn } from '@/shared/lib/cn'
import { PHOTO_ACCEPT, photoFileError, toMegabytes } from '@/shared/lib/photo'
import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Alert, Button, CardDivider, DangerRow, Dialog } from '@/shared/ui'
import { useDeleteMyPhoto, useUploadMyPhoto } from '../api/account-photo-queries'
import { useAuth } from '../auth-context'
import { AccountPhoto } from './account-photo'

/**
 * Sozlamalar oynasidagi hisob rasmi — ko'rsatish va boshqarish.
 *
 * Yo'l imkon qadar qisqa: rasmning O'ZIGA bosiladi (yonidagi kichkina
 * qalamchani izlab yurish shart emas), fayl tanlanadi, oynada ko'rib
 * chiqiladi va yuklanadi. Xodim rasmi bilan bir xil naqsh — foydalanuvchi
 * bir marta o'rgansa, ikkinchi joyda o'ylab o'tirmaydi.
 *
 * NEGA ruxsat tekshirilmaydi: bu O'Z hisobi. Serverda ham chegara ruxsat
 * kodi bilan emas, tokendagi identifikator bilan qo'yilgan.
 */
export function AccountPhotoEditor({ name }: { name: string }) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { user, refreshUser } = useAuth()

  const inputRef = useRef<HTMLInputElement>(null)

  const [picked, setPicked] = useState<File | null>(null)
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

  const upload = useUploadMyPhoto()
  const remove = useDeleteMyPhoto()

  const preview = useObjectUrl(picked)

  const hasPhoto = (user?.photoKey ?? null) !== null

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
    setPicked(file)
  }

  const closeUpload = () => {
    setPicked(null)
    setUploadFailure(null)
  }

  const closeDelete = () => {
    setConfirmingDelete(false)
    setDeleteFailure(null)
  }

  /*
   * `refreshUser` har ikkala amaldan keyin chaqiriladi: yangi `photoKey`
   * ham holatga, ham brauzerdagi nusxaga tushishi kerak — usiz yuqori
   * paneldagi avatar eski suratda qolib ketardi.
   */
  const submit = async () => {
    if (picked === null) return

    setUploadFailure(null)
    try {
      await upload.mutateAsync(picked)
      await refreshUser()
      closeUpload()
    } catch (error) {
      setUploadFailure(problemCode(error))
    }
  }

  const confirmDelete = async () => {
    setDeleteFailure(null)
    try {
      await remove.mutateAsync()
      await refreshUser()
      closeDelete()
    } catch (error) {
      setDeleteFailure(problemCode(error))
    }
  }

  /*
   * Xato ham aynan shu oynada ko'rsatiladi: fayl tanlash oynasi yopilgach
   * ekranda hech narsa o'zgarmasa, foydalanuvchi «bosildimi?» deb o'ylab
   * yana bosaveradi — va sababini hech qachon bilmaydi.
   */
  const uploadOpen = picked !== null || uploadFailure !== null

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept={PHOTO_ACCEPT}
        onChange={onPick}
        className="hidden"
        tabIndex={-1}
        aria-hidden
      />

      {/*
        Hisob bloki: markazda avatar, ostida ism va nima qilish mumkinligi.
        iOS «Settings» dagi hisob varag'i shu tartibda ochiladi.
      */}
      <div className="flex flex-col items-center gap-3 px-6 pt-6 pb-5 text-center">
        <div className="relative">
          <button
            type="button"
            onClick={openPicker}
            aria-label={hasPhoto ? t('photo.change') : t('photo.choose')}
            title={hasPhoto ? t('photo.change') : t('photo.choose')}
            className="press group relative block cursor-pointer rounded-full"
          >
            <AccountPhoto name={name} size="xl" />

            {/* Sichqoncha ostidagi qatlam — nima bo'lishini oldindan aytadi */}
            <span
              aria-hidden
              className={cn(
                'absolute inset-0 grid place-items-center rounded-full bg-black/45',
                'opacity-0 transition-opacity duration-200 ease-[var(--ease-ios)]',
                'group-hover:opacity-100 group-focus-visible:opacity-100',
              )}
            >
              <Camera className="size-6 text-white" strokeWidth={2} />
            </span>
          </button>

          {/*
            Doimiy belgi. Sensorli ekranda «sichqoncha ostida» degan holat
            yo'q, ya'ni rasmni bosish mumkinligi boshqa yo'l bilan bilinishi
            kerak. Chegara fon rangida — belgi rasmdan «kesib» turadi.
          */}
          <span
            aria-hidden
            className={cn(
              'pointer-events-none absolute -right-0.5 -bottom-0.5 grid size-7 place-items-center',
              'rounded-full border-2 border-[var(--bg-elevated)]',
              'bg-[var(--accent)] text-[var(--fg-on-accent)]',
            )}
          >
            <Camera className="size-3.5" strokeWidth={2.5} />
          </span>
        </div>

        <div>
          <p className="text-title3 leading-tight font-semibold tracking-tight">{name}</p>
          <p className="mt-1 text-caption1 text-[var(--fg-tertiary)]">{t('settings.photoHint')}</p>
        </div>
      </div>

      {/* Rasm bor bo'lgandagina — bo'sh amalni ko'rsatishning ma'nosi yo'q */}
      {hasPhoto && (
        <>
          <CardDivider />
          <DangerRow
            icon={Trash2}
            label={t('photo.delete')}
            onClick={() => setConfirmingDelete(true)}
          />
        </>
      )}

      {/* ───────── Ko'rib chiqish va yuklash ───────── */}
      <Dialog
        open={uploadOpen}
        onClose={closeUpload}
        size="sm"
        dismissible={!upload.isPending}
        title={t('settings.photoTitle')}
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
                Oldindan ko'rish DOIRA shaklida: server rasmni 512×512
                kvadratga keltiradi va u yumaloq avatarda ko'rinadi. To'g'ri
                burchakli ko'rinish yolg'on va'da berardi.
              */}
              <img
                src={preview}
                alt={name}
                className="size-40 rounded-full border border-[var(--border)] object-cover"
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
              <AccountPhoto name={name} size="xl" className="size-40 text-largetitle" />

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
            {t('settings.photoDeleteText')}
          </p>

          {deleteFailure !== null && (
            <Alert tone="danger" title={t('photo.deleteFailed')}>
              {errorMessage(deleteFailure)}
            </Alert>
          )}
        </div>
      </Dialog>
    </>
  )
}
