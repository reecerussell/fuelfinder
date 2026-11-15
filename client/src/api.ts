export type FuelType = 'E10' | 'E5' | 'B7_STANDARD' | 'B7_PREMIUM'

/** `slug` is the readable name used in shareable URLs, e.g. `?fuel=diesel`. */
export const FUELS: { id: FuelType; label: string; slug: string }[] = [
  { id: 'E10', label: 'Unleaded', slug: 'unleaded' },
  { id: 'E5', label: 'Super unleaded', slug: 'super-unleaded' },
  { id: 'B7_STANDARD', label: 'Diesel', slug: 'diesel' },
  { id: 'B7_PREMIUM', label: 'Premium diesel', slug: 'premium-diesel' },
]

export const fuelLabel = (id: string) =>
  FUELS.find((f) => f.id === id)?.label ?? id.replace('_', ' ')

export interface FuelPrice {
  fuelType: string
  price: number
  updatedAt: string | null
}

export interface Station {
  id: string
  name: string
  brand: string | null
  address: string
  postcode: string | null
  phone: string | null
  latitude: number
  longitude: number
  isMotorway: boolean
  isSupermarket: boolean
  amenities: string[]
  prices: FuelPrice[]
  distanceKm: number
}

export interface SearchResponse {
  lastUpdated: string
  total: number
  results: Station[]
}

export interface Coords {
  lat: number
  lng: number
  label: string
  /** Set when the location came from a postcode search, so it can be shared as a postcode. */
  postcode?: string
}

export async function searchStations(
  coords: Coords,
  fuel: FuelType,
  radiusMiles: number,
  sort: 'price' | 'distance',
): Promise<SearchResponse> {
  const params = new URLSearchParams({
    lat: String(coords.lat),
    lng: String(coords.lng),
    radiusKm: String(radiusMiles * 1.609),
    fuel,
    sort,
  })
  const res = await fetch(`/api/stations?${params}`)
  if (res.status === 503) throw new Error('Prices are still loading — try again in a few seconds.')
  if (!res.ok) throw new Error('Something went wrong fetching prices.')
  return res.json()
}

/** Look up a full or partial UK postcode using the free postcodes.io service. */
export async function lookupPostcode(input: string): Promise<Coords> {
  const pc = input.trim().toUpperCase()
  const tryUrl = async (url: string) => {
    const res = await fetch(url)
    if (!res.ok) return null
    const body = await res.json()
    return body.result as { latitude: number; longitude: number; postcode?: string; outcode?: string } | null
  }
  const result =
    (await tryUrl(`https://api.postcodes.io/postcodes/${encodeURIComponent(pc)}`)) ??
    (await tryUrl(`https://api.postcodes.io/outcodes/${encodeURIComponent(pc)}`))
  if (!result?.latitude) throw new Error(`Couldn't find postcode "${input.trim()}".`)
  const formatted = result.postcode ?? result.outcode ?? pc
  return { lat: result.latitude, lng: result.longitude, label: formatted, postcode: formatted }
}

export function getCurrentLocation(): Promise<Coords> {
  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) return reject(new Error('Location isn’t available on this device.'))
    navigator.geolocation.getCurrentPosition(
      (pos) => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude, label: 'your location' }),
      () => reject(new Error('Couldn’t get your location. Try entering a postcode instead.')),
      { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 },
    )
  })
}
