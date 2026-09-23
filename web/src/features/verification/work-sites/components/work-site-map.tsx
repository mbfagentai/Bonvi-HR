import 'leaflet/dist/leaflet.css'

import {
  Marker as LeafletMarker,
  divIcon,
  latLng,
  latLngBounds,
  type DivIcon,
  type LatLngBounds,
  type LeafletEvent,
  type LeafletMouseEvent,
  type PathOptions,
} from 'leaflet'
import { useEffect, useMemo, useState } from 'react'
import {
  AttributionControl,
  Circle,
  CircleMarker,
  MapContainer,
  Marker,
  Polygon,
  Polyline,
  ScaleControl,
  TileLayer,
  useMap,
  useMapEvents,
  type MapContainerProps,
} from 'react-leaflet'

import { WorkSiteShape } from '../api/work-sites-api'
import { DEFAULT_ZOOM, FOCUS_ZOOM, TASHKENT, roundPoint, type LatLng } from '../lib/work-site-geometry'

/**
 * Leaflet map of the work-site editor — the ONLY module that imports
 * Leaflet. It is loaded with `React.lazy` from the form dialog, so the
 * ~150 KB of Leaflet never lands in the main chunk.
 *
 * Markers are `divIcon`s styled with Tailwind: Leaflet's default PNG marker
 * breaks under Vite (its asset URLs are resolved at runtime from the CSS
 * path), and a CSS marker also follows the theme tokens.
 *
 * Colours are passed to SVG as `var(--…)` (same technique as the dashboard
 * chart): the shapes repaint by themselves when the theme switches.
 */

export type BaseLayer = 'map' | 'satellite'

/** A one-off camera move; a new `key` re-triggers the same place. */
export interface MapFocus {
  key: number
  point: LatLng
  bounds: [[number, number], [number, number]] | null
  /** Frame a circle of this radius around `point` instead of a fixed zoom. */
  radius: number | null
}

export interface WorkSiteMapProps {
  shape: WorkSiteShape
  center: LatLng | null
  radius: number
  vertices: readonly LatLng[]
  baseLayer: BaseLayer
  focus: MapFocus | null
  /** «You are here» dot from the browser's geolocation. */
  here: LatLng | null
  /** Paint the polygon red — the client-side self-intersection hint. */
  invalid: boolean
  readOnly: boolean
  /** Accessible name of the map region. */
  label: string
  onCenterChange: (point: LatLng) => void
  onVerticesChange: (vertices: LatLng[]) => void
}

const MAX_ZOOM = 19

const TILES: Record<BaseLayer, { url: string; attribution: string; maxNativeZoom: number }> = {
  map: {
    url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution:
      '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a>',
    maxNativeZoom: 19,
  },
  satellite: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution:
      'Tiles &copy; <a href="https://www.esri.com" target="_blank" rel="noopener noreferrer">Esri</a> &mdash; Source: Esri, i-cubed, USDA, USGS, AEX, GeoEye, Getmapping, Aerogrid, IGN, IGP, UPR-EGP, and the GIS User Community',
    maxNativeZoom: 18,
  },
}

/** Leaflet's own prefix link, but opening in a new tab: the editor must not navigate away. */
const ATTRIBUTION_PREFIX =
  '<a href="https://leafletjs.com" target="_blank" rel="noopener noreferrer">Leaflet</a>'

const SHAPE_STYLE: PathOptions = {
  color: 'var(--accent)',
  fillColor: 'var(--accent)',
  weight: 2,
  fillOpacity: 0.15,
}

const INVALID_STYLE: PathOptions = {
  color: 'var(--color-ios-red)',
  fillColor: 'var(--color-ios-red)',
  weight: 2,
  fillOpacity: 0.12,
}

const HERE_STYLE: PathOptions = {
  color: 'var(--bg-elevated)',
  fillColor: 'var(--color-ios-blue)',
  weight: 3,
  fillOpacity: 1,
}

const CENTER_ICON: DivIcon = divIcon({
  className:
    'rounded-full border-[3px] border-[var(--bg-elevated)] bg-[var(--accent)] shadow-raised',
  iconSize: [22, 22],
})

/**
 * Numbered vertex icon. Cached per index: a NEW icon object makes
 * react-leaflet call `setIcon`, which swaps the DOM node under the cursor
 * and kills an ongoing drag.
 */
const vertexIcons = new Map<number, DivIcon>()

function vertexIcon(index: number): DivIcon {
  const cached = vertexIcons.get(index)
  if (cached !== undefined) return cached

  const icon = divIcon({
    className: [
      'grid! place-items-center rounded-full border-2 border-[var(--bg-elevated)]',
      'bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)] shadow-raised',
    ].join(' '),
    html: String(index + 1),
    iconSize: [22, 22],
  })
  vertexIcons.set(index, icon)
  return icon
}

/** Dragged marker position — `event.target` is untyped in `@types/leaflet`. */
function draggedPoint(event: LeafletEvent): LatLng | null {
  const target: unknown = event.target
  if (!(target instanceof LeafletMarker)) return null
  const position = target.getLatLng()
  return roundPoint({ lat: position.lat, lng: position.lng })
}

type StartView = Pick<MapContainerProps, 'bounds' | 'boundsOptions' | 'center' | 'zoom'>

/** Where the camera starts: the site's own shape, else Tashkent (SPEC §D). */
function initialView(
  shape: WorkSiteShape,
  center: LatLng | null,
  radius: number,
  vertices: readonly LatLng[],
): StartView {
  let bounds: LatLngBounds | null = null
  if (shape === WorkSiteShape.Polygon && vertices.length >= 3) {
    bounds = latLngBounds(vertices.map((vertex) => latLng(vertex.lat, vertex.lng)))
  } else if (shape === WorkSiteShape.Circle && center !== null) {
    bounds = latLng(center.lat, center.lng).toBounds(radius * 2)
  }

  if (bounds !== null) {
    return { bounds, boundsOptions: { padding: [24, 24], maxZoom: 18 } }
  }

  const start = vertices[0] ?? center ?? TASHKENT
  return { center: [start.lat, start.lng], zoom: DEFAULT_ZOOM }
}

export default function WorkSiteMap({
  shape,
  center,
  radius,
  vertices,
  baseLayer,
  focus,
  here,
  invalid,
  readOnly,
  label,
  onCenterChange,
  onVerticesChange,
}: WorkSiteMapProps) {
  // Frozen at mount: MapContainer ignores later changes of `center`/`bounds` anyway
  const [start] = useState(() => initialView(shape, center, radius, vertices))
  const tiles = TILES[baseLayer]
  const positions = useMemo(
    () => vertices.map((vertex) => [vertex.lat, vertex.lng] as [number, number]),
    [vertices],
  )

  const addPoint = (point: LatLng) => {
    if (shape === WorkSiteShape.Circle) {
      onCenterChange(point)
      return
    }
    onVerticesChange([...vertices, point])
  }

  const moveVertex = (index: number, point: LatLng) => {
    onVerticesChange(vertices.map((vertex, at) => (at === index ? point : vertex)))
  }

  return (
    <div role="region" aria-label={label} className="isolate overflow-hidden rounded-lg border border-[var(--border)]">
      <MapContainer
        {...start}
        maxZoom={MAX_ZOOM}
        attributionControl={false}
        // A quick second click adds a second vertex; zooming on it would be a surprise
        doubleClickZoom={readOnly}
        className={
          readOnly
            ? 'h-[320px] w-full bg-[var(--bg-sunken)]! sm:h-[420px]'
            : 'h-[320px] w-full cursor-crosshair! bg-[var(--bg-sunken)]! sm:h-[420px]'
        }
      >
        {/*
          The control must exist BEFORE the first tile layer: Leaflet only
          removes the attribution of layers it saw being added, so the OSM
          credit would otherwise stay after switching to satellite.
        */}
        <AttributionControl position="bottomright" prefix={ATTRIBUTION_PREFIX} />
        <TileLayer
          key={baseLayer}
          url={tiles.url}
          attribution={tiles.attribution}
          maxZoom={MAX_ZOOM}
          maxNativeZoom={tiles.maxNativeZoom}
        />
        <ScaleControl position="bottomleft" imperial={false} />

        <SizeWatcher />
        <FocusController focus={focus} />
        {!readOnly && <ClickHandler onClick={addPoint} />}

        {shape === WorkSiteShape.Circle && center !== null && (
          <>
            <Circle
              center={[center.lat, center.lng]}
              radius={radius}
              pathOptions={SHAPE_STYLE}
              interactive={false}
            />
            <Marker
              position={[center.lat, center.lng]}
              icon={CENTER_ICON}
              draggable={!readOnly}
              interactive={!readOnly}
              keyboard={false}
              eventHandlers={{
                drag: (event) => {
                  const point = draggedPoint(event)
                  if (point !== null) onCenterChange(point)
                },
              }}
            />
          </>
        )}

        {shape === WorkSiteShape.Polygon && (
          <>
            {positions.length >= 3 && (
              <Polygon
                positions={positions}
                pathOptions={invalid ? INVALID_STYLE : SHAPE_STYLE}
                interactive={false}
              />
            )}
            {positions.length === 2 && (
              <Polyline positions={positions} pathOptions={SHAPE_STYLE} interactive={false} />
            )}
            {!readOnly &&
              vertices.map((vertex, index) => (
                <Marker
                  // Index is the identity here: vertices are only appended, moved or popped
                  key={index}
                  position={[vertex.lat, vertex.lng]}
                  icon={vertexIcon(index)}
                  draggable
                  keyboard={false}
                  eventHandlers={{
                    drag: (event) => {
                      const point = draggedPoint(event)
                      if (point !== null) moveVertex(index, point)
                    },
                  }}
                />
              ))}
          </>
        )}

        {here !== null && (
          <CircleMarker
            center={[here.lat, here.lng]}
            radius={7}
            pathOptions={HERE_STYLE}
            interactive={false}
          />
        )}
      </MapContainer>
    </div>
  )
}

/** Map click → new centre (circle) or a new vertex (polygon). */
function ClickHandler({ onClick }: { onClick: (point: LatLng) => void }) {
  useMapEvents({
    click: (event: LeafletMouseEvent) => {
      onClick(roundPoint({ lat: event.latlng.lat, lng: event.latlng.lng }))
    },
  })
  return null
}

/** Search hit or «my location» → fly there. */
function FocusController({ focus }: { focus: MapFocus | null }) {
  const map = useMap()

  useEffect(() => {
    if (focus === null) return
    if (focus.bounds !== null) {
      map.flyToBounds(focus.bounds, { maxZoom: FOCUS_ZOOM, duration: 0.8 })
    } else if (focus.radius !== null && focus.radius > 0) {
      const circle = latLng(focus.point.lat, focus.point.lng).toBounds(focus.radius * 2)
      map.flyToBounds(circle, { padding: [24, 24], maxZoom: 18, duration: 0.8 })
    } else {
      map.flyTo([focus.point.lat, focus.point.lng], FOCUS_ZOOM, { duration: 0.8 })
    }
  }, [map, focus])

  return null
}

/**
 * The dialog animates in and the sheet resizes on phones; Leaflet measures
 * its container once, so without this the tiles stop at the old edge.
 */
function SizeWatcher() {
  const map = useMap()

  useEffect(() => {
    const observer = new ResizeObserver(() => map.invalidateSize())
    observer.observe(map.getContainer())
    return () => observer.disconnect()
  }, [map])

  return null
}
