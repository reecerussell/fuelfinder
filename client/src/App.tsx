import { useEffect, useState, type FormEvent } from 'react'
import {
  FUELS,
  fuelLabel,
  getCurrentLocation,
  lookupPostcode,
  searchStations,
  type Coords,
  type FuelType,
  type Station,
} from './api'
import CarPanel from './CarPanel'
import { fillCost, formatPounds, loadCar, saveCar, type CarSettings } from './car'
import { readStored, writeStored } from './storage'
import { RADII, readUrlState, shareUrl, syncUrl, type SortOrder } from './url'

function savedFuel(): FuelType {
  const stored = readStored('fuel')
  return FUELS.find((f) => f.id === stored)?.id ?? 'E10'
}

function savedRadius(): number {
  const stored = readStored('radius')
  return typeof stored === 'number' && RADII.includes(stored) ? stored : 5
}

const miles = (km: number) => (km / 1.609).toFixed(1)

function timeAgo(iso: string | null) {
  if (!iso) return null
  const mins = Math.round((Date.now() - new Date(iso).getTime()) / 60000)
  if (mins < 60) return `${Math.max(mins, 1)} min ago`
  const hours = Math.round(mins / 60)
  if (hours < 48) return `${hours} hr ago`
  return `${Math.round(hours / 24)} days ago`
}

const shared = readUrlState()
const sharedPostcode = shared.location && 'postcode' in shared.location ? shared.location.postcode : null
const sharedCoords: Coords | null =
  shared.location && 'lat' in shared.location
    ? { lat: shared.location.lat, lng: shared.location.lng, label: 'the shared location' }
    : null

export default function App() {
  // Values from a shared link apply to this visit only; the saved preferences change only when the user picks.
  const [fuel, setFuel] = useState<FuelType>(() => shared.fuel ?? savedFuel())
  const [radius, setRadius] = useState<number>(() => shared.radius ?? savedRadius())
  const [sort, setSort] = useState<SortOrder>(shared.sort ?? 'price')
  const [postcode, setPostcode] = useState(sharedPostcode ?? '')
  const [coords, setCoords] = useState<Coords | null>(sharedCoords)
  const [shareMessage, setShareMessage] = useState<string | null>(null)
  const [stations, setStations] = useState<Station[] | null>(null)
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [openId, setOpenId] = useState<string | null>(null)
  const [car, setCar] = useState<CarSettings | null>(loadCar)

  useEffect(() => saveCar(car), [car])

  useEffect(() => {
    if (coords) syncUrl({ coords, fuel, radius, sort })
  }, [coords, fuel, radius, sort])

  const chooseFuel = (value: FuelType) => {
    setFuel(value)
    writeStored('fuel', value)
  }

  const chooseRadius = (value: number) => {
    setRadius(value)
    writeStored('radius', value)
  }

  useEffect(() => {
    if (!coords) return
    let cancelled = false
    setLoading(true)
    setError(null)
    searchStations(coords, fuel, radius, sort)
      .then((r) => {
        if (cancelled) return
        setStations(r.results)
        setTotal(r.total)
      })
      .catch((e: Error) => !cancelled && setError(e.message))
      .finally(() => !cancelled && setLoading(false))
    return () => {
      cancelled = true
    }
  }, [coords, fuel, radius, sort])

  const locate = async (get: () => Promise<Coords>) => {
    setLoading(true)
    setError(null)
    try {
      setCoords(await get())
    } catch (e) {
      setError((e as Error).message)
      setLoading(false)
    }
  }

  const onPostcode = (e: FormEvent) => {
    e.preventDefault()
    if (postcode.trim()) locate(() => lookupPostcode(postcode))
  }

  useEffect(() => {
    if (sharedPostcode) locate(() => lookupPostcode(sharedPostcode))
    // Only on first load, for links shared with a postcode.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const share = async () => {
    if (!coords) return
    const url = shareUrl({ coords, fuel, radius, sort })
    const title = `${fuelLabel(fuel)} prices near ${coords.postcode ?? 'here'} – Fuel Finder`
    try {
      if (navigator.share) {
        await navigator.share({ title, url })
        return
      }
      await navigator.clipboard.writeText(url)
      setShareMessage('Link copied')
    } catch (e) {
      if ((e as Error).name === 'AbortError') return
      setShareMessage('Couldn’t share – copy the address bar instead')
    }
    window.setTimeout(() => setShareMessage(null), 2500)
  }

  const cheapest = stations?.length
    ? Math.min(...stations.map((s) => priceOf(s, fuel)?.price ?? Infinity))
    : null

  return (
    <div className="app">
      <header className="header">
        <h1>
          <img src="/logo.svg" alt="" width="36" height="36" />
          Fuel Finder
        </h1>
        <p>Pump prices reported to the UK Fuel Finder scheme</p>
      </header>

      <section className="search">
        <button className="btn primary" onClick={() => locate(getCurrentLocation)} disabled={loading}>
          Use my location
        </button>
        <div className="or">or</div>
        <form onSubmit={onPostcode} className="postcode">
          <input
            value={postcode}
            onChange={(e) => setPostcode(e.target.value)}
            placeholder="Enter a postcode"
            autoComplete="postal-code"
            autoCapitalize="characters"
            enterKeyHint="search"
            aria-label="Postcode"
          />
          <button className="btn" type="submit" disabled={loading || !postcode.trim()}>
            Search
          </button>
        </form>

        <div className="chips" role="radiogroup" aria-label="Fuel type">
          {FUELS.map((f) => (
            <button
              key={f.id}
              role="radio"
              aria-checked={fuel === f.id}
              className={`chip ${fuel === f.id ? 'on' : ''}`}
              onClick={() => chooseFuel(f.id)}
            >
              {f.label}
            </button>
          ))}
        </div>

        <CarPanel car={car} onChange={setCar} />

        <div className="row">
          <label>
            Within
            <select value={radius} onChange={(e) => chooseRadius(Number(e.target.value))}>
              {RADII.map((r) => (
                <option key={r} value={r}>
                  {r} miles
                </option>
              ))}
            </select>
          </label>
          <div className="toggle" role="radiogroup" aria-label="Sort by">
            {(['price', 'distance'] as const).map((s) => (
              <button
                key={s}
                role="radio"
                aria-checked={sort === s}
                className={sort === s ? 'on' : ''}
                onClick={() => setSort(s)}
              >
                {s === 'price' ? 'Cheapest' : 'Nearest'}
              </button>
            ))}
          </div>
        </div>
      </section>

      <main>
        {error && <div className="notice error">{error}</div>}
        {loading && <div className="notice">Finding stations…</div>}

        {!loading && !error && stations && coords && (
          <div className="summary">
            <p>{resultsSummary(stations.length, total, coords.label, fuel, radius, sort)}</p>
            <button type="button" className="link" onClick={share}>
              {shareMessage ?? 'Share'}
            </button>
          </div>
        )}

        {!coords && !loading && !error && (
          <div className="notice">Use your location or enter a postcode to see prices.</div>
        )}

        <ul className="list">
          {!loading &&
            stations?.map((s) => {
              const current = priceOf(s, fuel)
              const price = current?.price ?? null
              const fill = car && price !== null ? formatPounds(fillCost(car, price)) : null
              const isOpen = openId === s.id
              return (
                <li key={s.id} className={`card ${isOpen ? 'open' : ''}`}>
                  <button className="card-main" onClick={() => setOpenId(isOpen ? null : s.id)} aria-expanded={isOpen}>
                    <div className="price-col">
                      <div className={`price ${price === cheapest ? 'best' : ''}`}>
                        {price?.toFixed(1)}
                        <small>p</small>
                      </div>
                      {fill && (
                        <div className="fill" aria-label={`${fill} to fill up`}>
                          {fill}
                          <small>to fill</small>
                        </div>
                      )}
                    </div>
                    <div className="info">
                      <strong>{stationTitle(s)}</strong>
                      <span className="addr">
                        {s.address}
                        {s.postcode ? `, ${s.postcode}` : ''}
                      </span>
                      <span className="meta">
                        {miles(s.distanceKm)} mi
                        {current?.updatedAt && ` · updated ${timeAgo(current.updatedAt)}`}
                        {s.isMotorway && ' · Motorway'}
                      </span>
                    </div>
                  </button>
                  {isOpen && (
                    <div className="details">
                      <table>
                        <tbody>
                          {s.prices.map((p) => (
                            <tr key={p.fuelType}>
                              <td>{fuelLabel(p.fuelType)}</td>
                              <td className="num">{p.price.toFixed(1)}p</td>
                              {car && <td className="num">{formatPounds(fillCost(car, p.price))}</td>}
                              <td className="muted">{timeAgo(p.updatedAt)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                      <div className="actions">
                        <a
                          className="btn primary"
                          href={`https://www.google.com/maps/dir/?api=1&destination=${s.latitude},${s.longitude}`}
                          target="_blank"
                          rel="noreferrer"
                        >
                          Directions
                        </a>
                        {s.phone && (
                          <a className="btn" href={`tel:${s.phone}`}>
                            Call
                          </a>
                        )}
                      </div>
                    </div>
                  )}
                </li>
              )
            })}
        </ul>
      </main>

      <footer>
        Data from the GOV.UK Fuel Finder scheme. Prices in pence per litre and may change at any time.
      </footer>
    </div>
  )
}

function priceOf(station: Station, fuel: FuelType) {
  return station.prices.find((p) => p.fuelType === fuel)
}

function stationTitle(station: Station) {
  const { brand, name } = station
  return brand && !name.toLowerCase().includes(brand.toLowerCase()) ? `${brand} · ${name}` : name
}

function resultsSummary(
  shown: number,
  total: number,
  place: string,
  fuel: FuelType,
  radius: number,
  sort: SortOrder,
) {
  if (shown === 0) return `No stations selling ${fuelLabel(fuel).toLowerCase()} within ${radius} miles of ${place}.`

  const found = `${total} station${total === 1 ? '' : 's'} near ${place}`
  return total > shown ? `${found} · showing ${sort === 'price' ? 'cheapest' : 'nearest'} ${shown}` : found
}
