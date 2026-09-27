import { useCallback } from 'react'
import { useTranslation } from 'react-i18next'

import { ApiError } from '@/api/errors'
import { useFormat } from './format'

/**
 * Backend xato kodini joriy tilga o'giradi — HR ilovasidagi qoidaning
 * aynan o'zi, nusxa qilib ko'chirilgan.
 *
 * ⚠️ Tarjimasi topilmasa KODNING O'ZI ko'rsatiladi. Yashirish mumkin
 * emas: bir vaqtlar HR ilovasida 39 ta kod tarjimasiz qolgan va
 * ekranda «mehnat.tugagan» degan matn chiqqan, buni esa hech kim
 * sezmagan, chunki hech qanday xato ko'tarilmagan.
 */
export interface ErrorText {
  title: string | null
  body: string
}

export function useErrorText(): (error: ApiError | null) => ErrorText {
  const { t } = useTranslation()
  const format = useFormat()

  return useCallback(
    (error: ApiError | null): ErrorText => {
      if (!error) return { title: null, body: t('common.error') }

      if (error.kind === 'network') {
        return { title: t('checkinError.serverTitle'), body: t('state.loadFailedText') }
      }
      if (error.kind === 'server') {
        return { title: t('checkinError.serverTitle'), body: t('checkinError.serverText') }
      }
      if (error.kind === 'unauthorized') {
        return { title: null, body: t('auth.sessionExpired') }
      }

      const code = error.code
      if (!code) return { title: null, body: t('common.error') }

      // Xato bilan kelgan sonlar shu yerda O'QILADIGAN matnga aylanadi:
      // «±35 m», «3.4 km», «08:35». Raqam — xodim amal qila oladigan
      // ma'lumot, «qayta urinib ko'ring» esa emas. Nomlar serverdagi
      // ProblemDetails kengaytma maydonlari bilan AYNAN bir xil.
      const accuracy = error.details['accuracyMeters']
      const distance = error.details['distanceMeters']
      const site = error.details['siteName']
      const lastAt = error.details['lastAt']
      const minutes = error.details['retryAfterMinutes']
      const seconds = error.details['retryAfterSeconds']

      // «Faol obyekt umuman yo'q» — o'sha kod, lekin nom va masofa yo'q:
      // «Eng yaqin obyekt: — » degan bo'sh jumla o'rniga boshqa matn.
      const variant =
        code === 'davomat.obyektdan_uzoq' && typeof site !== 'string' ? `${code}_obyektsiz` : code

      const bodyKey = `errors.${variant}`
      const titleKey = `errors.${code}_sarlavha`

      const body = t(bodyKey, {
        accuracy: typeof accuracy === 'number' ? format.accuracy(accuracy) : '',
        distance: typeof distance === 'number' ? format.distance(distance) : '',
        site: typeof site === 'string' ? site : '',
        time: typeof lastAt === 'string' ? format.time(lastAt) : '',
        minutes: typeof minutes === 'number' ? format.number(minutes) : '',
        seconds: typeof seconds === 'number' ? format.number(seconds) : '',
      })

      const title = t(titleKey)

      return {
        title: title === titleKey ? null : title,
        body: body === bodyKey ? code : body,
      }
    },
    [t, format],
  )
}
