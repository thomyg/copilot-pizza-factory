// Building blocks for the stage: cached materials, rounded boxes, people, pizzas, vehicles.
// Everything is low-poly and toy-like on purpose — the stage should read as a diorama.
import * as THREE from '../lib/three/three.module.min.js';
import { RoundedBoxGeometry } from '../lib/three/RoundedBoxGeometry.js';
import { CSS2DObject } from '../lib/three/CSS2DRenderer.js';

export { THREE };

const matCache = new Map();

/** A shared standard material. Same colour + options → same instance. */
export function M(color, opts = {}) {
    const key = color + JSON.stringify(opts);
    if (!matCache.has(key)) {
        matCache.set(key, new THREE.MeshStandardMaterial({ color, roughness: .82, metalness: 0, ...opts }));
    }
    return matCache.get(key);
}

export function mesh(geometry, material, cast = true) {
    const o = new THREE.Mesh(geometry, material);
    o.castShadow = cast;
    o.receiveShadow = true;
    return o;
}

export function box(w, h, d, material, r = .05) {
    const rr = Math.min(r, Math.min(w, h, d) / 2 - .001);
    return mesh(rr > .005 ? new RoundedBoxGeometry(w, h, d, 2, rr) : new THREE.BoxGeometry(w, h, d), material);
}

export const cyl = (rt, rb, h, material, seg = 24) => mesh(new THREE.CylinderGeometry(rt, rb, h, seg), material);

/**
 * Take an object off the stage for good. Materials are shared and cached; geometry is not, so
 * it is released here — a projector left running all day must not fill the GPU.
 */
export function discard(obj) {
    if (!obj) return;
    obj.removeFromParent();
    obj.traverse((o) => { if (o.isMesh) o.geometry?.dispose(); });
}

/** Discard every child of a group. */
export function empty(group) {
    for (const child of [...group.children]) discard(child);
}

export function at(o, x, y, z) {
    o.position.set(x, y, z);
    return o;
}

export function canvasTex(draw, size = 256, repeat = [1, 1]) {
    const c = document.createElement('canvas');
    c.width = c.height = size;
    draw(c.getContext('2d'), size);
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.repeat.set(...repeat);
    t.anisotropy = 4;
    return t;
}

/** An HTML label pinned to a 3D object. */
export function label(html, cls, y, parent, onClick) {
    const el = document.createElement('div');
    el.className = cls;
    el.innerHTML = html;
    if (onClick) {
        el.addEventListener('click', (e) => { e.stopPropagation(); onClick(); });
    }
    const o = new CSS2DObject(el);
    o.position.y = y;
    parent.add(o);
    return o;
}

export const SKIN = ['#f1c9a5', '#d9a77f', '#a8714b', '#7a4b2e', '#f4d6bf'];
export const CLOTH = ['#4a6440', '#3d6fa8', '#d9a13f', '#8f2415', '#7d6c55', '#2c4026', '#e8562f', '#5b4a6e'];

/** A stocky little person. hat: 'toque' | 'cap' | 'bun' | null. */
export function person({ body = '#ffffff', skin = '#f1c9a5', hat = null, apron = null, seated = false } = {}) {
    const g = new THREE.Group();
    const lift = seated ? -.28 : 0;
    if (!seated) {
        g.add(at(cyl(.07, .07, .42, M('#38291d'), 8), -.08, .21, 0));
        g.add(at(cyl(.07, .07, .42, M('#38291d'), 8), .08, .21, 0));
    }
    g.add(at(mesh(new THREE.CapsuleGeometry(.19, .34, 4, 12), M(body)), 0, .78 + lift, 0));
    if (apron) {
        g.add(at(box(.3, .36, .04, M(apron), .01), 0, .7 + lift, .18));
    }
    g.add(at(mesh(new THREE.SphereGeometry(.16, 16, 12), M(skin)), 0, 1.24 + lift, 0));
    if (hat === 'toque') {
        g.add(at(cyl(.14, .13, .2, M('#ffffff'), 16), 0, 1.44 + lift, 0));
        g.add(at(mesh(new THREE.SphereGeometry(.17, 16, 10), M('#ffffff')), 0, 1.58 + lift, 0));
    } else if (hat === 'bun') {
        g.add(at(mesh(new THREE.SphereGeometry(.165, 16, 10), M('#d6d3cc')), 0, 1.28 + lift, -.03));
        g.add(at(mesh(new THREE.SphereGeometry(.09, 12, 8), M('#d6d3cc')), 0, 1.42 + lift, -.1));
    } else if (hat === 'cap') {
        g.add(at(cyl(.165, .165, .07, M(apron || '#c93a21'), 16), 0, 1.36 + lift, 0));
    }
    return g;
}

/** A pizza; userData exposes crust, sauce, cheese and plate for progress effects. */
export function pizzaMesh() {
    const g = new THREE.Group();
    const plate = cyl(.27, .25, .03, M('#ffffff'), 24);
    plate.position.y = -.02;
    plate.visible = false;
    const crust = cyl(.2, .2, .04, M('#e3b46b'), 24);
    const sauce = cyl(.165, .165, .012, M('#c93a21'), 24);
    sauce.position.y = .026;
    const cheese = new THREE.Group();
    cheese.position.y = .036;
    for (let i = 0; i < 5; i++) {
        const a = i / 5 * Math.PI * 2;
        cheese.add(at(cyl(.04, .04, .01, M('#fbf3dc'), 10), Math.cos(a) * .09, 0, Math.sin(a) * .09));
    }
    // a purple ring under the crust: this pizza was ordered through Microsoft 365, follow it
    const tag = cyl(.245, .245, .02, M('#5b5fc7', { emissive: '#5b5fc7', emissiveIntensity: .6 }), 28);
    tag.position.y = -.012;
    tag.visible = false;
    g.add(plate, crust, sauce, cheese, tag);
    g.userData = { plate, crust, sauce, cheese, tag };
    g.traverse((o) => { if (o.isMesh) { o.castShadow = false; } });
    return g;
}

/** Shape a pizza for its station: raw on the bench, golden on the pass, on a plate at the table. */
export function dressPizza(p, stage, progress = 1) {
    const { plate, crust, sauce, cheese } = p.userData;
    plate.visible = stage === 'table';
    crust.material = stage === 'bench' ? M('#f0dcb4') : M('#d99a4e');
    sauce.scale.setScalar(stage === 'bench' ? .2 + progress * .8 : 1);
    cheese.visible = stage !== 'bench' || progress > .55;
}

export function vespa(color) {
    const g = new THREE.Group();
    g.add(at(box(1.1, .35, .38, M(color), .15), 0, .45, 0));
    g.add(at(box(.35, .7, .36, M(color), .12), .45, .7, 0));
    g.add(at(box(.5, .1, .3, M('#38291d'), .04), -.2, .68, 0));
    for (const wx of [-.4, .5]) {
        const w = cyl(.2, .2, .12, M('#2a2118'), 14);
        w.rotation.x = Math.PI / 2;
        g.add(at(w, wx, .2, 0));
    }
    // the delivery box on the back, and a purple pennant when it carries a Microsoft 365 order
    g.add(at(box(.42, .36, .42, M('#c99a68'), .04), -.45, .85, 0));
    const flag = new THREE.Group();
    flag.add(at(cyl(.015, .015, 1.1, M('#38291d'), 6), 0, .55, 0));
    flag.add(at(box(.36, .22, .02, M('#5b5fc7', { emissive: '#5b5fc7', emissiveIntensity: .5 }), 0), .19, .98, 0));
    at(flag, -.62, .9, -.15);
    flag.visible = false;
    g.add(flag);
    g.userData.flag = flag;
    return g;
}

/** The supplier's van, facing +x. userData.wheels spin while it drives. */
export function van() {
    const g = new THREE.Group();
    g.add(at(box(2.8, 1.7, 1.6, M('#fbf6ea'), .12), -.4, 1.25, 0));
    g.add(at(box(1.1, 1.2, 1.55, M('#fbf6ea'), .15), 1.45, 1.0, 0));
    g.add(at(box(.05, .55, 1.3, M('#3a4a55', { roughness: .2 }), 0), 2.0, 1.25, 0));
    g.add(at(box(2.82, .22, 1.62, M('#4a6440'), 0), -.4, 1.2, 0));
    const wheels = [];
    for (const [x, z] of [[-1.2, -.8], [-1.2, .8], [1.3, -.8], [1.3, .8]]) {
        const w = cyl(.32, .32, .22, M('#2a2118'), 16);
        w.rotation.x = Math.PI / 2;
        g.add(at(w, x, .32, z));
        wheels.push(w);
    }
    g.userData.wheels = wheels;
    return g;
}

export function plant(scene, x, z, s = 1) {
    const g = new THREE.Group();
    g.add(at(cyl(.28 * s, .22 * s, .45 * s, M('#c93a21'), 14), 0, .22 * s, 0));
    g.add(at(mesh(new THREE.IcosahedronGeometry(.5 * s, 1), M('#4a6440', { flatShading: true })), 0, .8 * s, 0));
    at(g, x, 0, z);
    scene.add(g);
    return g;
}
