export interface CarSettings {
  tankLitres: number
  /** How full the tank is before filling, as a fraction (0 = empty). */
  currentLevel: number
}

export const LEVELS = [
  { value: 0, label: 'Empty' },
  { value: 0.25, label: '¼' },
  { value: 0.5, label: '½' },
  { value: 0.75, label: '¾' },
]

export const MIN_TANK_LITRES = 10
export const MAX_TANK_LITRES = 150

const STORAGE_KEY = 'car'

export function loadCar(): CarSettings | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const car = JSON.parse(raw) as CarSettings
    return isValidTank(car.tankLitres) ? car : null
  } catch {
    return null
  }
}

export function saveCar(car: CarSettings | null) {
  try {
    if (car) localStorage.setItem(STORAGE_KEY, JSON.stringify(car))
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    /* storage unavailable (private mode etc.) — the setting just won't persist */
  }
}

export function isValidTank(litres: number) {
  return Number.isFinite(litres) && litres >= MIN_TANK_LITRES && litres <= MAX_TANK_LITRES
}

export function litresToFill(car: CarSettings) {
  return car.tankLitres * (1 - car.currentLevel)
}

/** Cost in pounds to fill up, given a price in pence per litre. */
export function fillCost(car: CarSettings, pencePerLitre: number) {
  return (litresToFill(car) * pencePerLitre) / 100
}

const gbp = new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' })

export const formatPounds = (pounds: number) => gbp.format(pounds)

export function levelLabel(level: number) {
  return level === 0 ? 'empty' : `${LEVELS.find((l) => l.value === level)?.label ?? ''} full`
}
