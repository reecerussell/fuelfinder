import { FUELS, type Coords, type FuelType } from './api'

export type SortOrder = 'price' | 'distance'

export interface SearchState {
  coords: Coords | null
  fuel: FuelType
  radius: number
  sort: SortOrder
}

export type SharedLocation = { postcode: string } | { lat: number; lng: number }

export interface UrlState {
  location?: SharedLocation
  fuel?: FuelType
  radius?: number
  sort?: SortOrder
}

export const RADII = [2, 5, 10, 20]

/** ~1 km precision, so a shared "my location" link doesn't pinpoint someone's home. */
const COORD_DECIMALS = 2

export function readUrlState(search = window.location.search): UrlState {
  const params = new URLSearchParams(search)
  const state: UrlState = {}

  const postcode = params.get('postcode')?.trim()
  const lat = Number(params.get('lat'))
  const lng = Number(params.get('lng'))
  if (postcode) {
    state.location = { postcode }
  } else if (params.has('lat') && params.has('lng') && Math.abs(lat) <= 90 && Math.abs(lng) <= 180) {
    state.location = { lat, lng }
  }

  const fuel = FUELS.find((f) => f.slug === params.get('fuel')?.toLowerCase())
  if (fuel) state.fuel = fuel.id

  const radius = Number(params.get('radius'))
  if (RADII.includes(radius)) state.radius = radius

  const sort = params.get('sort')
  if (sort === 'price' || sort === 'distance') state.sort = sort

  return state
}

export function buildSearchParams({ coords, fuel, radius, sort }: SearchState): URLSearchParams {
  const params = new URLSearchParams()
  if (!coords) return params

  if (coords.postcode) {
    params.set('postcode', coords.postcode.replace(/\s+/g, ''))
  } else {
    params.set('lat', coords.lat.toFixed(COORD_DECIMALS))
    params.set('lng', coords.lng.toFixed(COORD_DECIMALS))
  }
  params.set('fuel', FUELS.find((f) => f.id === fuel)?.slug ?? fuel)
  params.set('radius', String(radius))
  if (sort !== 'price') params.set('sort', sort)

  return params
}

export function shareUrl(state: SearchState) {
  const query = buildSearchParams(state).toString()
  return `${window.location.origin}${window.location.pathname}${query ? `?${query}` : ''}`
}

/** Keeps the address bar in sync without adding a history entry per filter tap. */
export function syncUrl(state: SearchState) {
  const url = shareUrl(state)
  if (url !== window.location.href) window.history.replaceState(null, '', url)
}
