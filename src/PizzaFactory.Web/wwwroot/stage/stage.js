// Entry point for the live stage. Blazor calls init() once, then apply() with each floor
// snapshot and event() the moment something happens. The director: when an action arrives
// through Microsoft 365 (or the presenter's levers), show who did it, light up where it landed,
// and — if Director is on — fly there, then back to where the audience was looking.
import { THREE, M, cyl, at } from './kit.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { CSS2DRenderer } from '../lib/three/CSS2DRenderer.js';
import { buildWorld } from './world.js';
import { Live } from './live.js';
import { Hud, ZONES } from './hud.js';

const SPOTLIT = ['Copilot', 'Teams', 'SharePoint', 'Web', 'Presenter'];
// Knock-on effects of a decision (approving a day off records the absence, fills the shift):
// they go in the feed, but the banner belongs to the decision itself.
const CONSEQUENCES = ['absence.reported', 'shift.assigned'];
const FRUST = 20;
const OFFSET = new THREE.Vector3(24, 27, 32);
const DIRECTOR_KEY = 'pf-stage-director';
const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

let S = null;

export function init(rootId, dotnet, snapshot, events) {
    dispose();
    const root = document.getElementById(rootId);
    const s = { root, dotnet, selected: { kind: 'table', id: 9 }, tween: null, returnTo: null, raf: 0 };
    S = s;

    s.hud = new Hud(root, {
        zone: (k) => { s.hud.setZone(k); flyTo(ZONES[k].cx, 0, fitZoom(ZONES[k])); },
        zoom: (k) => {
            if (k === 'reset') { s.camera.position.copy(s.controls.target).add(OFFSET); s.hud.setZone('all'); flyTo(ZONES.all.cx, 0, fitZoom(ZONES.all)); return; }
            flyTo(s.controls.target.x, s.controls.target.z, k === 'in' ? Math.min(4.5, s.camera.zoom * 1.35) : Math.max(.45, s.camera.zoom / 1.35));
        },
        director: (on) => { s.hud.setDirector(on); try { localStorage.setItem(DIRECTOR_KEY, on ? '1' : '0'); } catch { /* private window */ } },
        openService: () => dotnet.invokeMethodAsync('OpenService').catch(() => { }),
    });
    let director = true;
    try { director = localStorage.getItem(DIRECTOR_KEY) !== '0'; } catch { /* default on */ }
    s.hud.setDirector(director);

    const stage = s.hud.stage;
    // ?quality=low: no shadows, 1× pixels. For weak presentation laptops and GPU-less test browsers.
    const low = new URLSearchParams(location.search).get('quality') === 'low';
    s.renderer = new THREE.WebGLRenderer({ antialias: !low });
    s.renderer.setPixelRatio(low ? 1 : Math.min(window.devicePixelRatio, 2));
    s.renderer.shadowMap.enabled = !low;
    s.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    s.renderer.outputColorSpace = THREE.SRGBColorSpace;
    s.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    stage.prepend(s.renderer.domElement);
    s.labels = new CSS2DRenderer();
    s.labels.domElement.className = 'st-labels';
    stage.insertBefore(s.labels.domElement, s.renderer.domElement.nextSibling);

    s.scene = new THREE.Scene();
    s.camera = new THREE.OrthographicCamera(-10, 10, 10, -10, .1, 400);
    s.controls = new OrbitControls(s.camera, s.renderer.domElement);
    Object.assign(s.controls, { enableDamping: true, screenSpacePanning: false, minZoom: .45, maxZoom: 4.5, minPolarAngle: .45, maxPolarAngle: 1.2 });
    s.controls.mouseButtons = { LEFT: THREE.MOUSE.PAN, MIDDLE: THREE.MOUSE.DOLLY, RIGHT: THREE.MOUSE.ROTATE };
    s.controls.touches = { ONE: THREE.TOUCH.PAN, TWO: THREE.TOUCH.DOLLY_ROTATE };
    s.controls.addEventListener('start', () => { s.returnTo = null; });

    s.hemi = new THREE.HemisphereLight(0xfff6e6, 0xcdb88b, 1.6);
    s.sun = new THREE.DirectionalLight(0xfff1dc, 2.4);
    s.sun.position.set(-2, 34, 22);
    s.sun.castShadow = true;
    s.sun.shadow.mapSize.set(2048, 2048);
    Object.assign(s.sun.shadow.camera, { left: -54, right: 54, top: 32, bottom: -32, near: 1, far: 120 });
    s.sun.shadow.bias = -.0004;
    s.sun.shadow.normalBias = .03;
    s.sun.target.position.set(12, 0, 0);
    s.scene.add(s.hemi, s.sun, s.sun.target);

    s.world = buildWorld(s.scene);
    s.live = new Live(s.scene, s.world);

    s.ring = flatRing(.9, 1, 0xc93a21, .9);
    s.hover = flatRing(.94, 1, 0x7d6c55, .45);
    s.glow = flatRing(.6, 1, 0x5b5fc7, .55);

    wirePicking(s);
    // A trackpad pinch arrives as ctrl+wheel. Over the canvas the camera takes it; over a panel
    // or a label the browser would zoom the whole page and crop every edge of the stage. Take
    // it everywhere and turn it into a camera zoom instead.
    s.onWheel = (e) => {
        if (!e.ctrlKey) return;
        e.preventDefault();
        if (e.target === s.renderer.domElement) return;   // OrbitControls already handles it
        const z = Math.min(4.5, Math.max(.45, s.camera.zoom * Math.exp(-e.deltaY * .01)));
        s.camera.zoom = z;
        s.camera.updateProjectionMatrix();
    };
    s.onGesture = (e) => e.preventDefault();   // Safari's pinch
    // If the page is pinch-zoomed anyway (Chrome keeps it across reloads, and Cmd+0 does not undo
    // it), the stage follows what is actually visible: it moves to the visual viewport and scales
    // back by the zoom, so every panel stays inside the window instead of past its edges.
    s.fitToView = () => {
        const vv = window.visualViewport;
        const zoomed = vv && vv.scale > 1.001;
        root.style.transformOrigin = '0 0';
        root.style.transform = zoomed ? `translate(${vv.offsetLeft}px, ${vv.offsetTop}px) scale(${1 / vv.scale})` : '';
    };
    window.visualViewport?.addEventListener('resize', s.fitToView);
    window.visualViewport?.addEventListener('scroll', s.fitToView);
    s.fitToView();
    root.addEventListener('wheel', s.onWheel, { passive: false });
    root.addEventListener('gesturestart', s.onGesture);
    root.addEventListener('gesturechange', s.onGesture);
    s.resize = () => {
        const w = stage.clientWidth, h = stage.clientHeight, a = w / Math.max(h, 1);
        Object.assign(s.camera, { left: -FRUST * a / 2, right: FRUST * a / 2, top: FRUST / 2, bottom: -FRUST / 2 });
        s.camera.updateProjectionMatrix();
        s.renderer.setSize(w, h);
        s.labels.setSize(w, h);
    };
    window.addEventListener('resize', s.resize);
    s.resize();
    s.themeQuery = matchMedia('(prefers-color-scheme: dark)');
    s.themeListener = () => theme();
    s.themeQuery.addEventListener('change', s.themeListener);

    s.controls.target.set(ZONES.all.cx, 0, 0);
    s.camera.position.copy(s.controls.target).add(OFFSET);
    s.camera.zoom = fitZoom(ZONES.all);
    s.camera.updateProjectionMatrix();

    apply(snapshot);
    s.hud.addEvents(events ?? []);
    buildLamps();
    theme();
    select(s.selected);
    s.hudTimer = setInterval(() => renderHud(false), 500);
    s.clock = new THREE.Clock();
    s.raf = requestAnimationFrame(frame);
    root.dataset.ready = 'true';
}

export function apply(snapshot) {
    if (!S || !snapshot) return;
    S.snap = snapshot;
    S.live.apply(snapshot);
    S.root.dataset.seq = String(snapshot.seq);
}

export function event(e) {
    if (!S || !e) return;
    S.hud.addEvents([e]);
    S.live.onEvent(e);
    S.root.dataset.lastEvent = String(e.seq);
    if (!SPOTLIT.includes(e.channel) || CONSEQUENCES.includes(e.kind)) return;
    S.hud.banner(e);
    const target = S.live.targetFor(e);
    if (!target) return;
    const p = target.getWorldPosition(new THREE.Vector3());
    S.glow.position.set(p.x, .05, p.z);
    S.glow.userData.until = performance.now() + 6000;
    S.glow.userData.r = (target.userData.ringR ?? 1) * 1.6;
    S.glow.visible = true;
    if (S.hud.director) {
        S.returnTo ??= { x: S.controls.target.x, z: S.controls.target.z, zoom: S.camera.zoom };
        S.returnAt = performance.now() + 7000;
        flyTo(p.x, p.z, Math.max(S.camera.zoom, 1.6));
    }
}

export function dispose() {
    if (!S) return;
    cancelAnimationFrame(S.raf);
    clearInterval(S.hudTimer);
    window.removeEventListener('resize', S.resize);
    S.themeQuery?.removeEventListener('change', S.themeListener);
    window.visualViewport?.removeEventListener('resize', S.fitToView);
    window.visualViewport?.removeEventListener('scroll', S.fitToView);
    S.root.style.transform = '';
    S.root.removeEventListener('wheel', S.onWheel);
    S.root.removeEventListener('gesturestart', S.onGesture);
    S.root.removeEventListener('gesturechange', S.onGesture);
    S.hud.dispose();
    S.controls.dispose();
    S.renderer.dispose();
    S.root.innerHTML = '';
    S = null;
}

/* ─────────────────────────── helpers ─────────────────────────── */

function flatRing(inner, outer, color, opacity) {
    const m = new THREE.Mesh(new THREE.RingGeometry(inner, outer, 64), new THREE.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false }));
    m.rotation.x = -Math.PI / 2;
    m.visible = false;
    S.scene.add(m);
    return m;
}

function fitZoom(z) {
    const stage = S.hud.stage;
    const r = stage.clientWidth / Math.max(stage.clientHeight, 1);
    const wv = z.w * .8 + z.d * .6, hv = (z.w * .6 + z.d * .8) * .57 + 2.2;
    return Math.max(.45, Math.min(FRUST * r / (wv * 1.15), FRUST / (hv * 1.3)));
}

function flyTo(cx, cz, zoom) {
    S.tween = { from: S.controls.target.clone(), fz: S.camera.zoom, to: new THREE.Vector3(cx, 0, cz), tz: zoom, t0: performance.now(), dur: reduceMotion ? 1 : 900 };
}

function objFor(sel) {
    return S.world.pickables.find((o) => o.userData.pick.kind === sel.kind && (sel.id === undefined || o.userData.pick.id === sel.id));
}

function select(sel, focus = false) {
    S.selected = sel;
    S.selectedObj = objFor(sel);
    S.ring.visible = !!S.selectedObj;
    renderHud(true);
    if (focus && S.selectedObj) {
        const p = S.selectedObj.getWorldPosition(new THREE.Vector3());
        S.returnTo = null;
        flyTo(p.x, p.z, Math.max(S.camera.zoom, 1.5));
    }
}

function renderHud(force) {
    if (!S?.snap) return;
    S.hud.renderTop(S.snap, S.live);
    S.hud.renderKpis(S.snap, S.live);
    S.hud.renderInspector(S.selected, S.snap, S.live, force);
    S.hud.renderTracker(S.selected, S.snap, S.live);
}

function wirePicking(s) {
    const ray = new THREE.Raycaster(), ndc = new THREE.Vector2();
    const hit = (e) => {
        const r = s.renderer.domElement.getBoundingClientRect();
        ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
        ray.setFromCamera(ndc, s.camera);
        let o = ray.intersectObjects(s.world.pickables, true)[0]?.object;
        while (o && !o.userData.pick) o = o.parent;
        return o ?? null;
    };
    let down = null;
    const el = s.renderer.domElement;
    el.addEventListener('pointerdown', (e) => { down = [e.clientX, e.clientY]; });
    el.addEventListener('pointerup', (e) => {
        if (!down || Math.hypot(e.clientX - down[0], e.clientY - down[1]) > 5) return;
        const o = hit(e);
        if (o) select(o.userData.pick, true);
    });
    let pending = false;
    el.addEventListener('pointermove', (e) => {
        if (pending || e.buttons) return;
        pending = true;
        requestAnimationFrame(() => {
            pending = false;
            if (!S) return;
            const o = hit(e);
            el.style.cursor = o ? 'pointer' : 'grab';
            s.hover.visible = !!o && o !== s.selectedObj;
            if (o) {
                const p = o.getWorldPosition(new THREE.Vector3()), r = o.userData.ringR ?? 1;
                s.hover.position.set(p.x, .035, p.z);
                s.hover.scale.set(r, r, r);
            }
        });
    });
    // in-scene chips and pins select what they belong to
    s.root.addEventListener('click', (e) => {
        const chip = e.target.closest('.chip3d, .pin3d');
        if (!chip) return;
        const owner = s.world.pickables.find((o) => o.children.some((c) => c.element === chip));
        if (owner) select(owner.userData.pick, true);
    });
}

function buildLamps() {
    S.lampM = new THREE.MeshStandardMaterial({ color: 0xf0dda9, emissive: 0xffb45a, emissiveIntensity: 0 });
    S.lamps = [];
    for (const id of [4, 9, 10, 15, 17]) {
        const t = S.live.tables.get(id);
        if (!t) continue;
        S.scene.add(at(cyl(.01, .01, 1, M('#38291d'), 4), t.cx, 2.75, t.cz));
        S.scene.add(at(cyl(.08, .3, .22, S.lampM, 16), t.cx, 2.2, t.cz));
        const pl = new THREE.PointLight(0xffb45a, 0, 5, 1.5);
        pl.position.set(t.cx, 2, t.cz);
        S.scene.add(pl);
        S.lamps.push(pl);
    }
    const desk = new THREE.PointLight(0xffb45a, 0, 5, 1.5);
    desk.position.set(32.6, 1.5, -2.2);
    S.scene.add(desk);
    S.lamps.push(desk);
}

function theme() {
    const night = S.themeQuery.matches;
    S.night = night;
    S.scene.background = new THREE.Color(night ? 0x1d1611 : 0xf1e7d1);
    S.hemi.intensity = night ? .55 : 1.6;
    S.hemi.color.set(night ? 0x9fb0d0 : 0xfff6e6);
    S.hemi.groundColor.set(night ? 0x281e16 : 0xcdb88b);
    S.sun.intensity = night ? .9 : 2.4;
    S.sun.color.set(night ? 0xb8c8ff : 0xfff1dc);
    S.world.glassM.emissiveIntensity = night ? .9 : 0;
    S.world.ground.material.color.set(night ? 0x3a3027 : 0xe6dcc6);
    S.world.lampShadeM.emissiveIntensity = night ? 1.2 : 0;
    S.lampM.emissiveIntensity = night ? 1.5 : 0;
    S.lamps.forEach((l) => { l.intensity = night ? 3 : 0; });
    S.renderer.toneMappingExposure = night ? 1.15 : 1;
}

function frame(now) {
    if (!S) return;
    const dt = Math.min(.1, S.clock.getDelta());
    if (S.tween) {
        const t = S.tween, k = Math.min(1, (now - t.t0) / t.dur), e = k < .5 ? 2 * k * k : 1 - (-2 * k + 2) ** 2 / 2;
        const off = S.camera.position.clone().sub(S.controls.target);
        S.controls.target.lerpVectors(t.from, t.to, e);
        S.camera.position.copy(S.controls.target).add(off);
        S.camera.zoom = t.fz + (t.tz - t.fz) * e;
        S.camera.updateProjectionMatrix();
        if (k >= 1) S.tween = null;
    }
    if (S.returnTo && now > S.returnAt && !S.tween) {
        const r = S.returnTo;
        S.returnTo = null;
        flyTo(r.x, r.z, r.zoom);
    }
    S.controls.update();
    S.live.tick(dt, now, S.night);
    if (S.ring.visible && S.selectedObj) {
        const p = S.selectedObj.getWorldPosition(new THREE.Vector3()), r = (S.selectedObj.userData.ringR ?? 1) * (1 + Math.sin(now * .005) * .04);
        S.ring.position.set(p.x, .04, p.z);
        S.ring.scale.set(r, r, r);
    }
    if (S.glow.visible) {
        const left = S.glow.userData.until - now;
        if (left <= 0) S.glow.visible = false;
        else {
            const r = S.glow.userData.r * (1 + ((now / 900) % 1) * .6);
            S.glow.scale.set(r, r, r);
            S.glow.material.opacity = .6 * Math.min(1, left / 1500) * (1 - ((now / 900) % 1) * .7);
        }
    }
    S.renderer.render(S.scene, S.camera);
    S.labels.render(S.scene, S.camera);
    S.raf = requestAnimationFrame(frame);
}
