import { Camera, LoaderCircle, Trash2 } from 'lucide-react'
import { useRef, useState, type ChangeEvent } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { PHOTO_ACCEPT, photoFileError } from '@/shared/lib/photo'
import { Alert, Button } from '@/shared/ui'

import { useDeleteUserPhoto, useUploadUserPhoto } from '../api/users-queries'
import { userErrorCode } from '../lib/user-error'
import { UserPhoto } from './user-photo'

/**
 * Tahrirlash oynasidagi rasm — ko'rsatish va boshqarish.
 *
 * <b>Nega bu yerda ichma-ich oyna YO'Q</b> (xodim kartasidagi
 * `EmployeePhotoEditor` dan asosiy farqi). U profil SAHIFASIDA turadi va
 * o'zining ko'rib chiqish oynasini ocha oladi; bu esa allaqachon ochiq
 * turgan tahrirlash oynasining ICHIDA. Oyna ustiga oyna ochish fokusni
 * chalkashtiradi va «Esc» qaysi oynani yopishini noaniq qiladi. Shu sababli
 * yo'l qisqartirildi: fayl tanlanadi va DARHOL yuklanadi, natija esa
 * o'sha zahoti avatarning o'zida ko'rinadi — oldindan ko'rish shu.
 *
 * <b>Nega faqat tahrirlashda.</b> Rasm hisobga `id` bo'yicha biriktiriladi
 * (`POST /api/users/{id}/photo`), yangi hisobda esa `id` hali yo'q.
 * Yaratish oynasida bu blok umuman chizilmaydi — hisob saqlangach, uni
 * qayta ochib rasm qo'yiladi.
 *
 * <b>Nega «tega olmaydigan» odamga faqat rasm ko'rinadi.</b> Bosh rahbar
 * super administratorni ro'yxatda KO'RADI (server ro'yxatni to'liq
 * qaytaradi), lekin unga tegolmaydi. Tugmalarni ko'rsatib, keyin 403
 * qaytarish yomon tajriba — shu sababli ular umuman chizilmaydi.
 */
export function UserPhotoEditor({
  userId,
  photoKey,
  name,
  canEdit,
  disabled,
}: {
  userId: string
  photoKey: string | null
  name: string
  /** «Kim kimga tega oladi» qoidasining natijasi — chaqiruvchi hisoblaydi. */
  canEdit: boolean
  /** Forma saqlanayotganda — barcha boshqaruv o'chiriladi. */
  disabled: boolean
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const inputRef = useRef<HTMLInputElement>(null)
  const [failure, setFailure] = useState<string | null>(null)

  const upload = useUploadUserPhoto(userId)
  const remove = useDeleteUserPhoto(userId)

  const band = disabled || upload.isPending || remove.isPending

  if (!canEdit) {
    return (
      <div className="flex justify-center">
        <UserPhoto userId={userId} photoKey={photoKey} name={name} size="xl" />
      </div>
    )
  }

  const openPicker = () => inputRef.current?.click()

  const onPick = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null

    // Qiymat darhol tozalanadi: aks holda AYNAN o'sha faylni ikkinchi marta
    // tanlash hodisa hosil qilmaydi va hech narsa bo'lmay qoladi
    event.target.value = ''
    if (file === null) return

    const problem = photoFileError(file)
    if (problem !== null) {
      setFailure(problem)
      return
    }

    setFailure(null)
    try {
      await upload.mutateAsync(file)
    } catch (error) {
      setFailure(userErrorCode(error))
    }
  }

  const onDelete = async () => {
    setFailure(null)
    try {
      await remove.mutateAsync()
    } catch (error) {
      setFailure(userErrorCode(error))
    }
  }

  const hasPhoto = photoKey !== null

  return (
    <div className="space-y-3">
      <input
        ref={inputRef}
        type="file"
        accept={PHOTO_ACCEPT}
        onChange={(event) => void onPick(event)}
        className="hidden"
        tabIndex={-1}
        aria-hidden
      />

      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={openPicker}
          disabled={band}
          aria-label={hasPhoto ? t('photo.change') : t('photo.choose')}
          title={hasPhoto ? t('photo.change') : t('photo.choose')}
          className="press group relative block shrink-0 cursor-pointer rounded-full disabled:cursor-default"
        >
          <UserPhoto userId={userId} photoKey={photoKey} name={name} size="xl" />

          {/* Sichqoncha ostidagi qatlam — nima bo'lishini oldindan aytadi */}
          <span
            aria-hidden
            className={cn(
              'absolute inset-0 grid place-items-center rounded-full bg-black/45',
              'opacity-0 transition-opacity duration-200 ease-[var(--ease-ios)]',
              'group-hover:opacity-100 group-focus-visible:opacity-100',
              upload.isPending && 'opacity-100',
            )}
          >
            {upload.isPending ? (
              <LoaderCircle className="size-6 animate-spin text-white" strokeWidth={2} />
            ) : (
              <Camera className="size-6 text-white" strokeWidth={2} />
            )}
          </span>

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
        </button>

        <div className="min-w-0 space-y-1.5">
          <p className="text-footnote text-[var(--fg-secondary)]">{t('photo.squareHint')}</p>

          {hasPhoto && (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              disabled={band}
              onClick={() => void onDelete()}
              className="text-ios-red hover:bg-ios-red/10 hover:text-ios-red"
            >
              {remove.isPending ? <LoaderCircle className="animate-spin" /> : <Trash2 />}
              {remove.isPending ? t('photo.deleting') : t('photo.delete')}
            </Button>
          )}
        </div>
      </div>

      {/*
        Xato aynan shu yerda ko'rsatiladi: fayl tanlash oynasi yopilgach
        ekranda hech narsa o'zgarmasa, foydalanuvchi «bosildimi?» deb
        o'ylab yana bosaveradi — va sababini hech qachon bilmaydi.
      */}
      {failure !== null && (
        <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
          {errorMessage(failure)}
        </Alert>
      )}
    </div>
  )
}
