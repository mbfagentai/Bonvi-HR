import { useEffect, useState } from 'react'

import { api } from '@/api/client'

/**
 * Xodimning O'Z rasmi — `GET /api/me/photo` dan.
 *
 * NEGA `<img src="/api/me/photo">` emas: rasm tokenga bog'langan va brauzer
 * `<img>` so'roviga `Authorization` sarlavhasini qo'ymaydi. Rasm `fetch`
 * bilan olinib, `blob:` manzilga aylantiriladi (iOS 12 ham buni biladi).
 *
 * Kesh MODUL darajasida, rasm kaliti bo'yicha: bosh ekran va profil bitta
 * rasmni ikki marta yuklamaydi, HR rasmni almashtirsa esa kalit o'zgaradi
 * va yangisi olinadi. Hisobdan chiqishda tozalanadi — bitta telefonda ikki
 * xodim navbat bilan kirishi mumkin.
 */
const cache = new Map<string, string>()
const pending = new Map<string, Promise<string | null>>()

export function clearPhotoCache(): void {
  cache.forEach((url) => URL.revokeObjectURL(url))
  cache.clear()
  pending.clear()
  // Jamoa rasmlari ham — bitta telefonda navbat bilan ikki xodim kirishi mumkin
  clearTeamPhotoCache()
}

function load(photoKey: string): Promise<string | null> {
  const known = pending.get(photoKey)
  if (known) return known

  const promise = api.getPhoto().then(
    (blob) => {
      if (!blob) return null
      const url = URL.createObjectURL(blob)
      cache.set(photoKey, url)
      return url
    },
    // Rasm yuklanmadi — bosh harflar qoladi; bu xato ekranga chiqmaydi.
    () => {
      pending.delete(photoKey)
      return null
    },
  )

  pending.set(photoKey, promise)
  return promise
}

/** Rasm manzili (`blob:`) yoki `null` — rasm yo'q yoki hali yuklanmagan. */
export function useMyPhoto(photoKey: string | null | undefined): string | null {
  const [url, setUrl] = useState<string | null>(() => (photoKey ? cache.get(photoKey) ?? null : null))

  useEffect(() => {
    if (!photoKey) {
      setUrl(null)
      return undefined
    }

    const cached = cache.get(photoKey)
    if (cached) {
      setUrl(cached)
      return undefined
    }

    let alive = true
    void load(photoKey).then((loaded) => {
      if (alive) setUrl(loaded)
    })

    return () => {
      alive = false
    }
  }, [photoKey])

  return url
}

/**
 * Jamoa a'zosining rasmi — `GET /api/me/team/members/{id}/photo` dan
 * (SPEC-jamoam §C4). Qoida `useMyPhoto` bilan bir xil: `fetch` → `blob:`,
 * xato ekranga chiqmaydi, bosh harflar qoladi.
 *
 * Kalit — xodim, rasm kaliti emas: panel rasm KALITINI ataylab bermaydi
 * (u ichki saqlash nomi, §C2). HR rasmni almashtirsa, yangisi ilova qayta
 * ochilganda ko'rinadi — boshliq uchun bu yetarli. Kesh `clearPhotoCache`
 * bilan birga tozalanadi.
 */
const teamCache = new Map<string, string>()
const teamPending = new Map<string, Promise<string | null>>()

export function clearTeamPhotoCache(): void {
  teamCache.forEach((url) => URL.revokeObjectURL(url))
  teamCache.clear()
  teamPending.clear()
}

function loadTeamPhoto(employeeId: string): Promise<string | null> {
  const known = teamPending.get(employeeId)
  if (known) return known

  const promise = api.getTeamMemberPhoto(employeeId).then(
    (blob) => {
      if (!blob) return null
      const url = URL.createObjectURL(blob)
      teamCache.set(employeeId, url)
      return url
    },
    () => {
      teamPending.delete(employeeId)
      return null
    },
  )

  teamPending.set(employeeId, promise)
  return promise
}

/** A'zo rasmi (`blob:`) yoki `null`. `hasPhoto: false` — so'rov umuman ketmaydi. */
export function useTeamPhoto(employeeId: string | null | undefined, hasPhoto: boolean): string | null {
  const id = employeeId && hasPhoto ? employeeId : null
  const [url, setUrl] = useState<string | null>(() => (id ? teamCache.get(id) ?? null : null))

  useEffect(() => {
    if (!id) {
      setUrl(null)
      return undefined
    }

    const cached = teamCache.get(id)
    if (cached) {
      setUrl(cached)
      return undefined
    }

    let alive = true
    void loadTeamPhoto(id).then((loaded) => {
      if (alive) setUrl(loaded)
    })

    return () => {
      alive = false
    }
  }, [id])

  return url
}
