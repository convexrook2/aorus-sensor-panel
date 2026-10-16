import * as THREE from 'three';
import { CSS2DObject, type CSS2DRenderer } from 'three/examples/jsm/renderers/CSS2DRenderer.js';
import { BODIES, BODY_BY_ID } from '../vendor/belter-charts/src/data/bodies';
import { loadEphemeris } from '../vendor/belter-charts/src/ephemeris';
import { Scene3D } from '../vendor/belter-charts/src/scene';
import type { BodyVisual } from '../vendor/belter-charts/src/scene/bodies3d';
import { store } from '../vendor/belter-charts/src/ui/store';
import { AU_KM, ERA, RING_SPACE_RADIUS_KM, MEDINA_POSITION, RING_SPACE_SOL_GATE,
  TYCHO_DIAMETER_KM, MEDINA_LENGTH_KM, ID_TO_NATIVE, normaliseId, destinationNote, checkLandmarks } from './landmarks';
import { PanelEphemeris } from './panel-ephemeris';
import { tychoModel, medinaModel, gateModel, alienStationModel } from './models';
import '../vendor/belter-charts/src/style.css';
import './style.css';

type Command = { type: string; id?: string; enabled?: boolean };
declare global {
  interface Window {
    chrome?: { webview?: { postMessage(value: unknown): void; addEventListener(name: string, callback: (event: { data: unknown }) => void): void } };
    x890Navigation?: { command(message: Command): void; diagnose(): unknown; check(): unknown };
  }
}

// The complete pinned Scene3D owns rendering, Sun lighting, planet shaders,
// sky and floating origin. Its internals are accessed only to add landmarks
// and collect diagnostics; the site's rendering pipeline remains intact.
type SceneAccess = {
  scene: THREE.Scene; camera: THREE.PerspectiveCamera; renderer: THREE.WebGLRenderer;
  labelRenderer: CSS2DRenderer; visuals: Map<string, BodyVisual>; sunLight: THREE.PointLight;
  orbitLines: Map<string, { line: THREE.Line }>; resize(container: HTMLElement): void;
};
const viewport = document.querySelector<HTMLElement>('#viewport')!;
const select = document.querySelector<HTMLSelectElement>('#destination')!;
const title = document.querySelector<HTMLElement>('#title')!;
const detail = document.querySelector<HTMLElement>('#detail')!;
const loading = document.querySelector<HTMLElement>('#loading')!;
const notify = (value: unknown) => window.chrome?.webview?.postMessage(value);
const localScene = new THREE.Group();
const localLabels: { id: string; object: CSS2DObject; text: HTMLElement }[] = [];
const localMarkers: { sprite: THREE.Sprite; radiusKm: number }[] = [];
const models = new Map<string, THREE.Object3D>();
const loadFailures: string[] = [];
let chart: Scene3D, full: SceneAccess, eph: PanelEphemeris;
let ready = false, selected = 'sun', cameraFocus = 'sun', viewMode = 'detail';
let motion = true, active = true, disposed = false, request = 0, lastRender = 0;
let frameCount = 0, lastFrameMs = 0, slowFrames = 0, lastStatus = 0, modelRotation = 0;
THREE.DefaultLoadingManager.onError = url => { if (!url.endsWith('.glb')) loadFailures.push(url); };

function replaceModel(id: string, model: THREE.Object3D) {
  const visual = full.visuals.get(id)!;
  for (const child of [...visual.group.children]) if (child !== visual.sprite && child !== visual.label) {
    visual.group.remove(child);
    child.traverse(object => {
      if (object instanceof THREE.Mesh) {
        object.geometry.dispose();
        for (const material of Array.isArray(object.material) ? object.material : [object.material]) material.dispose();
      }
    });
  }
  visual.group.add(model); visual.mesh = null; models.set(id, model);
}

function localBody(id: string, name: string, radiusKm: number, model: THREE.Object3D, pos: { x: number; y: number; z: number }) {
  BODY_BY_ID.set(id, { id, name, radiusKm, periodDays: 0, kind: 'station', color: '#eac18c' });
  const group = new THREE.Group(); group.position.set(pos.x, pos.y, pos.z); group.add(model);
  models.set(id, model); localScene.add(group);
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: full.visuals.get('earth')!.sprite.material.map, color: 0xb6e4f2, depthWrite: false }));
  group.add(sprite); localMarkers.push({ sprite, radiusKm });
  const element = document.createElement('div'); element.className = 'body-label kind-station'; element.textContent = name;
  element.onclick = () => focus(id === 'sol-gate-local' ? 'ring' : id);
  // CSS2DRenderer owns the anchor's transform on every render. Keep the text
  // offset on an inner element so nearby physical locations remain readable.
  const anchor = document.createElement('div'); anchor.className = 'local-label-anchor'; anchor.append(element);
  const label = new CSS2DObject(anchor); group.add(label); localLabels.push({ id, object: label, text: element });
}

function chooseCamera(id: string, distanceKm: number, yaw?: number, pitch = .5) {
  cameraFocus = id; chart.focus(id); chart.controls.setDistTarget(distanceKm);
  if (yaw !== undefined) chart.controls.setOrientation(yaw, pitch);
  chart.controls.settle();
}

function focus(id: string, announce = true) {
  selected = normaliseId(id);
  if (!ready) return;
  id = selected;
  eph.region = ['medina', 'ring-station', 'ring-space'].includes(id) ? 'RingSpace' : 'Sol';
  viewMode = id === 'ring-space' ? 'ring-space' : 'detail';
  const body = BODY_BY_ID.get(id);
  const pos = eph.stateOf(id === 'ring-space' ? 'ring-station' : id, ERA).pos;
  const sunYaw = Math.atan2(-pos.y, -pos.x);
  const distance = id === 'ring-space' ? RING_SPACE_RADIUS_KM * 2.8 :
    id === 'tycho' ? 1.05 : id === 'medina' ? MEDINA_LENGTH_KM * 1.5 :
    id === 'ring' ? 1800 : id === 'ring-station' ? 8 :
    id === 'saturn' ? 380_000 : Math.max((body?.radiusKm ?? 1) * 3.4, .1);
  chooseCamera(id === 'ring-space' ? 'ring-station' : id, distance,
    id === 'ring' || id === 'ring-space' ? -Math.PI / 2 : eph.region === 'RingSpace' ? -1.1 : sunYaw + 1.1,
    id === 'ring' || id === 'ring-space' ? 1.3 : id === 'saturn' ? .65 : .38);
  select.value = id;
  title.textContent = id === 'ring-space' ? 'RING SPACE' : (body?.name ?? 'Sol').toUpperCase();
  detail.textContent = destinationNote(id);
  document.querySelector('#region')!.textContent = eph.region === 'Sol' ? 'SOL SYSTEM' : 'RING SPACE';
  document.querySelector('#compass')!.textContent = eph.region === 'Sol' ? 'ECLIPTIC J2000 / 2356' : 'RING SPACE / LOCAL KM';
  if (announce) notify({ type: 'selected', id: ID_TO_NATIVE[id] });
  invalidate();
}

function system(inner = false) {
  focus('sun'); viewMode = inner ? 'inner' : 'system';
  const aspect = Math.max(.3, viewport.clientWidth / Math.max(1, viewport.clientHeight));
  chooseCamera('sun', (inner ? 5 : 80) * AU_KM / Math.min(1, aspect), -1.1, 1.2);
  title.textContent = inner ? 'INNER SYSTEM' : 'SOL SYSTEM'; invalidate();
}

function context() {
  if (eph.region === 'RingSpace') { focus('ring-space'); return; }
  const body = BODY_BY_ID.get(selected);
  viewMode = 'context';
  if (body?.moon) chooseCamera(body.moon.parent, BODY_BY_ID.get(body.moon.parent)?.focusDistKm ?? 500_000, -1.1, .55);
  else { const pos = eph.stateOf(selected, ERA).pos; chooseCamera('sun', Math.max(Math.hypot(pos.x, pos.y, pos.z) * 3.2, 5 * AU_KM), -1.1, 1.1); }
  invalidate();
}

function heightKm() { return 2 * chart.controls.dist * Math.tan(full.camera.fov * Math.PI / 360); }
function updateScale() {
  const kmPerPixel = heightKm() / Math.max(1, viewport.clientHeight);
  const ideal = kmPerPixel * Math.min(70, viewport.clientWidth * .15);
  const magnitude = Math.pow(10, Math.floor(Math.log10(ideal)));
  const km = [1, 2, 5, 10].map(n => n * magnitude).filter(n => n <= ideal).at(-1) ?? magnitude;
  (document.querySelector('#scale i') as HTMLElement).style.width = `${Math.max(1, km / kmPerPixel)}px`;
  document.querySelector('#scale span')!.textContent = km >= AU_KM * .05 ? `${+(km / AU_KM).toPrecision(2)} AU` :
    km < 1 ? `${Math.round(km * 1000)} m` : `${km.toLocaleString('en', { maximumFractionDigits: 1 })} km`;
}

function render(time = performance.now(), dt = 0) {
  if (!ready || disposed) return;
  const start = performance.now();
  if (chart.controls.focusId !== cameraFocus && BODY_BY_ID.has(chart.controls.focusId)) focus(chart.controls.focusId);
  const origin = eph.stateOf(cameraFocus, ERA).pos;
  localScene.position.set(-origin.x, -origin.y, -origin.z);
  localScene.visible = eph.region === 'RingSpace';
  full.sunLight.intensity = eph.region === 'Sol' ? 2.4 : 0;
  viewport.classList.toggle('panel-detail', viewMode === 'detail');
  for (const label of localLabels) label.object.visible = eph.region === 'RingSpace';
  const closeup = viewMode === 'detail';
  for (const entry of full.orbitLines.values()) (entry.line.material as THREE.Material).visible = !closeup;
  for (const visual of full.visuals.values()) visual.sprite.material.visible = !closeup;
  for (const marker of localMarkers) {
    const pixelKm = heightKm() / Math.max(1, viewport.clientHeight);
    marker.sprite.visible = !closeup && marker.radiusKm * 2 / pixelKm < 5;
    marker.sprite.scale.set(pixelKm * 5, pixelKm * 5, 1);
  }
  for (const label of localLabels) label.text.style.transform = viewMode !== 'ring-space' ? '' :
    label.id === 'medina' ? 'translate(12px,-22px)' :
    label.id === 'ring-station' ? 'translate(-93px,12px)' : 'translate(-70px,-20px)';
  if (dt > 0 && ['tycho', 'medina'].includes(selected)) {
    modelRotation += dt * .05; models.get(selected)!.rotation.z = modelRotation;
  }
  // The original site updates Sun direction, night lights, clouds, atmosphere
  // shells, body meshes, orbit paths, labels and the floating origin here.
  chart.render(store.getState(), dt);
  for (const visual of full.visuals.values()) visual.timeEl.style.display = 'none';
  updateScale(); frameCount++; lastFrameMs = performance.now() - start;
  if (lastFrameMs > 33.3) slowFrames++;
  if (time - lastStatus > 5000) { lastStatus = time; notify({ type: 'status', ...diagnose() }); }
}

function tick(time: number) {
  request = 0;
  if (!active || !motion || document.hidden || disposed) return;
  if (time - lastRender >= 33.3) { const dt = Math.min((time - lastRender) / 1000, .1); lastRender = time; render(time, dt); }
  request = requestAnimationFrame(tick);
}
function schedule() { if (ready && active && motion && !document.hidden && !disposed && !request) request = requestAnimationFrame(tick); }
function invalidate() { if (!ready || disposed) return; render(); schedule(); }
function setMotion(enabled: boolean) { motion = enabled; if (!motion && request) { cancelAnimationFrame(request); request = 0; } invalidate(); }
function setActive(enabled: boolean) { active = enabled; if (!active && request) { cancelAnimationFrame(request); request = 0; } if (active) invalidate(); }

function features() {
  const earth = full.visuals.get('earth')!;
  const material = earth.mesh?.material as THREE.ShaderMaterial;
  return { pipeline: 'Belter Charts Scene3D', sunPointLight: full.sunLight.isPointLight,
    texturedSun: !!(full.visuals.get('sun')!.mesh?.material as THREE.MeshBasicMaterial).map,
    earthNightLights: material.userData.isDayNight === true && material.uniforms.tNight.value instanceof THREE.Texture,
    earthClouds: earth.clouds instanceof THREE.Mesh, milkyWay: full.scene.background instanceof THREE.Texture,
    physicalPlanetMeshes: BODIES.filter(b => b.kind === 'planet').every(b => full.visuals.get(b.id)!.group.scale.equals(new THREE.Vector3(1, 1, 1))) };
}
function localLabelLayout() {
  if (eph.region !== 'RingSpace' || viewMode !== 'ring-space')
    return { localLabelCount: 0, localLabelOverlaps: 0, localLabelsInsideViewport: true };
  const bounds = viewport.getBoundingClientRect();
  const rects = localLabels.map(label => label.text.getBoundingClientRect()).filter(rect => rect.width > 0 && rect.height > 0);
  let overlaps = 0;
  for (let i = 0; i < rects.length; i++) for (let j = i + 1; j < rects.length; j++) {
    const a = rects[i], b = rects[j];
    if (a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top) overlaps++;
  }
  return { localLabelCount: rects.length, localLabelOverlaps: overlaps,
    localLabelsInsideViewport: rects.every(rect => rect.left >= bounds.left && rect.right <= bounds.right && rect.top >= bounds.top && rect.bottom <= bounds.bottom) };
}
function diagnose() {
  return { ready, selected: ID_TO_NATIVE[selected], scene: eph.region, viewMode, physicalMeshScale: 1,
    epoch: ERA.toISOString(), viewport: { width: viewport.clientWidth, height: viewport.clientHeight }, heightKm: heightKm(),
    renderActive: active && motion && !document.hidden, frameCount, lastFrameMs, slowFrames,
    drawCalls: full.renderer.info.render.calls, triangles: full.renderer.info.render.triangles,
    textures: full.renderer.info.memory.textures, geometryCount: full.renderer.info.memory.geometries,
    loadFailures: [...new Set(loadFailures)], ...features(), ...localLabelLayout() };
}
function check() {
  const visualFeatures = features();
  if (Object.values(visualFeatures).some(value => value === false)) throw new Error('The full Belter Charts visual pipeline is incomplete.');
  if (loadFailures.length) throw new Error(`Local chart assets failed: ${loadFailures.join(', ')}`);
  return { passed: true, ...checkLandmarks(eph), ...diagnose() };
}
function command(message: Command) {
  if (message.type === 'focus' && message.id) focus(message.id, false);
  else if (message.type === 'motion') setMotion(message.enabled === true);
  else if (message.type === 'active') setActive(message.enabled === true);
  else if (message.type === 'context' && ready) context();
  else if (message.type === 'inner' && ready) system(true);
}
window.x890Navigation = { command, diagnose, check };
window.chrome?.webview?.addEventListener('message', event => command(event.data as Command));
const resizeObserver = new ResizeObserver(() => { if (ready) { full.resize(viewport); invalidate(); } });
resizeObserver.observe(viewport);
document.addEventListener('visibilitychange', () => { if (document.hidden && request) { cancelAnimationFrame(request); request = 0; } else invalidate(); });
select.addEventListener('change', () => focus(select.value));
document.querySelector('#system')!.addEventListener('click', () => system());
document.querySelector('#inner')!.addEventListener('click', () => system(true));
document.querySelector('#ring-space')!.addEventListener('click', () => focus('ring-space'));
document.querySelector('#inspect')!.addEventListener('click', () => focus(selected));
document.querySelector('#context')!.addEventListener('click', context);
for (const event of ['click', 'pointermove', 'wheel', 'keyup']) viewport.addEventListener(event, () => {
  if (!motion && ready) { chart.controls.settle(); invalidate(); }
});
viewport.addEventListener('keydown', event => {
  if (event.target instanceof HTMLSelectElement) return;
  if (event.key === 'Home') { system(); event.preventDefault(); }
  else if (event.key.toLowerCase() === 'i') system(true);
  else if (event.key.toLowerCase() === 'd') focus(selected);
});
window.addEventListener('unhandledrejection', event => { loadFailures.push(String(event.reason)); notify({ type: 'error', message: String(event.reason) }); });
window.addEventListener('pagehide', () => {
  disposed = true; if (request) cancelAnimationFrame(request); resizeObserver.disconnect();
  if (!full) return;
  const geometries = new Set<THREE.BufferGeometry>(), materials = new Set<THREE.Material>(), textures = new Set<THREE.Texture>();
  full.scene.traverse(object => {
    if (object instanceof THREE.Mesh || object instanceof THREE.Line || object instanceof THREE.Points || object instanceof THREE.Sprite) {
      if ('geometry' in object) geometries.add(object.geometry);
      for (const material of Array.isArray(object.material) ? object.material : [object.material]) {
        materials.add(material); for (const value of Object.values(material)) if (value instanceof THREE.Texture) textures.add(value);
        if (material instanceof THREE.ShaderMaterial) for (const uniform of Object.values(material.uniforms)) if (uniform.value instanceof THREE.Texture) textures.add(uniform.value);
      }
    }
  });
  if (full.scene.background instanceof THREE.Texture) textures.add(full.scene.background);
  for (const geometry of geometries) geometry.dispose(); for (const texture of textures) texture.dispose(); for (const material of materials) material.dispose();
  full.renderer.dispose();
});

async function main() {
  BODY_BY_ID.get('tycho')!.radiusKm = TYCHO_DIAMETER_KM / 2;
  store.setState({ timeMs: ERA.getTime(), playing: false, muted: true, trafficOn: false, tourOpen: true, plan: null, ride: false });
  eph = new PanelEphemeris(await loadEphemeris('./ephem'));
  const assetsReady = new Promise<void>(resolve => { THREE.DefaultLoadingManager.onLoad = () => resolve(); });
  chart = new Scene3D(viewport, eph, './'); full = chart as unknown as SceneAccess;
  replaceModel('tycho', tychoModel()); replaceModel('ring', gateModel());
  // A stationary portal has no solar orbit. Keep the renderer's map entry,
  // detaching only the upstream circular-orbit line from the actual scene.
  full.scene.remove(full.orbitLines.get('ring')!.line);
  localBody('medina', 'Medina Station', MEDINA_LENGTH_KM / 2, medinaModel(), MEDINA_POSITION);
  localBody('ring-station', 'Ring Station', 2.5, alienStationModel(), { x: 0, y: 0, z: 0 });
  const gate = gateModel(); gate.rotation.y = Math.PI / 2;
  localBody('sol-gate-local', 'Sol gate', 500, gate, RING_SPACE_SOL_GATE);
  localScene.add(new THREE.AmbientLight(0xc6dcfa, .4));
  const localLight = new THREE.PointLight(0xc5dbf6, 2.2, 0, 0); localLight.position.set(-1000, -1000, 2000); localScene.add(localLight);
  const boundary = new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(Array.from({ length: 256 }, (_, i) =>
    new THREE.Vector3(Math.cos(i * Math.PI / 128) * RING_SPACE_RADIUS_KM, Math.sin(i * Math.PI / 128) * RING_SPACE_RADIUS_KM, 0))),
    new THREE.LineBasicMaterial({ color: 0x416f8a, transparent: true, opacity: .6 }));
  localScene.add(boundary); full.scene.add(localScene);
  for (const body of [...BODIES, BODY_BY_ID.get('medina')!, BODY_BY_ID.get('ring-station')!]) {
    const option = document.createElement('option'); option.value = body.id; option.textContent = body.name; select.append(option);
  }
  const option = document.createElement('option'); option.value = 'ring-space'; option.textContent = 'Ring Space overview'; select.append(option);
  await chart.warmup((stage, fraction) => { loading.textContent = `${stage.toUpperCase()} ${Math.round(fraction * 100)}%`; });
  await assetsReady;
  if (loadFailures.length) throw new Error(`Local chart assets failed: ${loadFailures.join(', ')}`);
  ready = true; loading.hidden = true; full.resize(viewport);
  focus(new URLSearchParams(location.search).get('focus') ?? selected, false);
  full.renderer.domElement.addEventListener('webglcontextlost', event => { event.preventDefault(); setActive(false); loading.hidden = false; loading.textContent = '3D CONTEXT LOST · REOPEN THE THEME'; notify({ type: 'error', message: 'WebGL context lost' }); });
  notify({ type: 'ready', ...diagnose() });
}
main().catch(error => { loading.hidden = false; loading.textContent = 'LOCAL 3D CHARTS UNAVAILABLE'; detail.textContent = String(error); notify({ type: 'error', message: String(error) }); });
