import { useState, type FormEvent } from 'react'
import {
  LEVELS,
  MAX_TANK_LITRES,
  MIN_TANK_LITRES,
  isValidTank,
  levelLabel,
  litresToFill,
  type CarSettings,
} from './car'

interface Props {
  car: CarSettings | null
  onChange: (car: CarSettings | null) => void
}

export default function CarPanel({ car, onChange }: Props) {
  const [editing, setEditing] = useState(false)
  const [tank, setTank] = useState(car ? String(car.tankLitres) : '')
  const [level, setLevel] = useState(car?.currentLevel ?? 0)

  const startEditing = () => {
    setTank(car ? String(car.tankLitres) : '')
    setLevel(car?.currentLevel ?? 0)
    setEditing(true)
  }

  const litres = Number(tank)
  const valid = isValidTank(litres)

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!valid) return
    onChange({ tankLitres: litres, currentLevel: level })
    setEditing(false)
  }

  const remove = () => {
    onChange(null)
    setEditing(false)
  }

  if (!editing) {
    return car ? (
      <div className="car-summary">
        <span>
          <strong>Your car:</strong> {car.tankLitres} L tank, filling {Math.round(litresToFill(car))} L from{' '}
          {levelLabel(car.currentLevel)}
        </span>
        <button type="button" className="link" onClick={startEditing}>
          Edit
        </button>
      </div>
    ) : (
      <button type="button" className="btn car-add" onClick={startEditing}>
        Add your tank size to see fill-up costs
      </button>
    )
  }

  return (
    <form className="car-form" onSubmit={onSubmit}>
      <label className="field">
        <span>Tank size</span>
        <span className="with-unit">
          <input
            type="number"
            inputMode="numeric"
            min={MIN_TANK_LITRES}
            max={MAX_TANK_LITRES}
            step="1"
            value={tank}
            onChange={(e) => setTank(e.target.value)}
            placeholder="e.g. 50"
            autoFocus
          />
          <span className="unit">litres</span>
        </span>
      </label>
      <p className="hint">
        It's in your handbook, or search for your model. Typical sizes: small car 40–45 L, family car 50–60 L, SUV
        60–75 L.
      </p>

      <div className="field">
        <span id="level-label">Tank is currently</span>
        <div className="toggle" role="radiogroup" aria-labelledby="level-label">
          {LEVELS.map((l) => (
            <button
              key={l.value}
              type="button"
              role="radio"
              aria-checked={level === l.value}
              className={level === l.value ? 'on' : ''}
              onClick={() => setLevel(l.value)}
            >
              {l.label}
            </button>
          ))}
        </div>
      </div>

      <div className="actions">
        <button className="btn primary" type="submit" disabled={!valid}>
          Save
        </button>
        {car ? (
          <button className="btn" type="button" onClick={remove}>
            Remove
          </button>
        ) : (
          <button className="btn" type="button" onClick={() => setEditing(false)}>
            Cancel
          </button>
        )}
      </div>
    </form>
  )
}
