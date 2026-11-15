import { readStored, writeStored } from './storage'

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
  const stored = readStored(STORAGE_KEY) as Partial<CarSettings> | null
  if (!stored || typeof stored.tankLitres !== 'number' || !isValidTank(stored.tankLitres)) return null

  const level = LEVELS.some((l) => l.value === stored.currentLevel) ? stored.currentLevel! : 0
  return { tankLitres: stored.tankLitres, currentLevel: level }
}

export function saveCar(car: CarSettings | null) {
  writeStored(STORAGE_KEY, car)
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
  const fraction = LEVELS.find((l) => l.value === level)
  return level === 0 || !fraction ? 'empty' : `${fraction.label} full`
}
