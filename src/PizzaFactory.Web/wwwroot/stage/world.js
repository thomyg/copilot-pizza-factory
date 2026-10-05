// The house as a diorama: storeroom → kitchen → dining room → break room → office, with the
// street in front. Only things that never change live here; live.js moves everything else.
import { THREE, M, mesh, box, cyl, at, canvasTex, label, person, plant, vespa, van as makeVan } from './kit.js';

// x ranges of the rooms; every room runs z -7 (back wall) to +7 (open front).
export const ROOMS = { store: [-22, -12], kitchen: [-12, 0], sala: [0, 22], break: [22, 30], office: [30, 38] };

// Domain Ingredient names → how they sit in the storeroom.
export const INGREDIENTS = [
    { key: 'Flour', name: 'Flour, tipo 00', color: '#efe6d2', form: 'sack', place: 'pallet', slot: 0 },
    { key: 'Water', name: 'Water', color: '#9cc3d6', form: 'pack', place: 'pallet', slot: 1 },
    { key: 'TomatoSauce', name: 'Tomato sauce', color: '#c93a21', form: 'can', place: 'back', slot: 0 },
    { key: 'Mozzarella', name: 'Mozzarella', color: '#f6f1e4', form: 'tub', place: 'back', slot: 1 },
    { key: 'Ham', name: 'Prosciutto cotto', color: '#e7a5a0', form: 'crate', place: 'back', slot: 2 },
    { key: 'Salami', name: 'Salame piccante', color: '#8f2415', form: 'crate', place: 'back', slot: 3 },
    { key: 'Pineapple', name: 'Pineapple', color: '#e9c13f', form: 'can', place: 'back', slot: 4 },
    { key: 'Mushroom', name: 'Mushrooms', color: '#a07a55', form: 'crate', place: 'left', slot: 0 },
    { key: 'Tuna', name: 'Tuna', color: '#7f8f9c', form: 'can', place: 'left', slot: 1 },
    { key: 'Salt', name: 'Sea salt', color: '#ffffff', form: 'tub', place: 'left', slot: 2 },
    { key: 'Yeast', name: 'Fresh yeast', color: '#d9b46a', form: 'tub', place: 'left', slot: 3 },
];

export const AGENTS = {
    pizzaiolo: { name: 'Pizzaiolo', role: 'Prep & toppings', pos: [-4.6, 0, -.4], rot: 0, look: { body: '#ffffff', hat: 'toque', apron: '#c93a21' } },
    fornaio: { name: 'Fornaio', role: 'The wood-fired oven', pos: [-6.8, 0, -1.9], rot: Math.PI / 4, look: { body: '#ffffff', hat: 'toque', apron: '#38291d' } },
    dough: { name: 'Dough Master', role: 'Dough & resting', pos: [-10, 0, 3.8], rot: Math.PI / 4, look: { body: '#ffffff', hat: 'toque', apron: '#d9a13f' } },
    procurement: { name: 'Procurement', role: 'Stock & reorders', pos: [-15.5, 0, -2.6], rot: Math.PI / 4, look: { body: '#4a6440', hat: 'cap', apron: '#2c4026' } },
    giuseppe: { name: 'Giuseppe', role: 'Maître d’ and concierge. Lives in Copilot and Teams too.', pos: [20.2, 0, 5.4], rot: 0, look: { body: '#38291d', apron: '#c93a21' } },
    nonna: { name: 'Nonna', role: 'The back office: rota, purchase orders, invoices, time off. Lives in Microsoft 365.', pos: [33.6, 0, -3.1], rot: 0, look: { body: '#6b3b4a', hat: 'bun', seated: true } },
};

// Where things are, for flights, flyers and walkers.
export const SPOTS = {
    pass: new THREE.Vector3(.8, 0, 4.25),
    desk: new THREE.Vector3(33, 1.1, -2),
    capoDesk: new THREE.Vector3(34.4, 1.1, 4.1),
    board: new THREE.Vector3(27.4, 1.5, -3.3),
    cabinet: new THREE.Vector3(37.1, 1.5, -5.9),
    backDoor: new THREE.Vector3(-17, 1.2, 6),
    vanStop: -17,
    entrance: new THREE.Vector3(19, 0, 7.4),
    breakSpot: new THREE.Vector3(24.4, 0, -5.3),
    vespaBay: new THREE.Vector3(26, 0, 8.2),
};

export function buildWorld(scene) {
    const pickables = [];
    const pickable = (obj, pick, ringR = 1) => {
        obj.userData.pick = pick;
        obj.userData.ringR = ringR;
        pickables.push(obj);
        scene.add(obj);
        return obj;
    };
    const unpick = (obj) => {
        const i = pickables.indexOf(obj);
        if (i >= 0) pickables.splice(i, 1);
    };
    const w = { pickables, pickable, unpick };

    buildShell(scene, w);
    buildStoreroom(scene, w);
    buildKitchen(scene, w);
    buildDiningExtras(scene);
    buildBreakRoom(scene, w);
    buildOffice(scene, w);
    buildStreet(scene, w);
    return w;
}

function buildShell(scene, w) {
    const ground = mesh(new THREE.PlaneGeometry(220, 160), M('#e6dcc6'), false);
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -.21;
    scene.add(ground);
    w.ground = ground;

    const tile = canvasTex((g, s) => {
        const n = 4, q = s / n;
        for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) { g.fillStyle = (i + j) % 2 ? '#cf7a52' : '#e2a27a'; g.fillRect(i * q, j * q, q, q); }
        g.strokeStyle = 'rgba(80,40,20,.25)'; g.lineWidth = 3;
        for (let i = 0; i <= n; i++) { g.beginPath(); g.moveTo(i * q, 0); g.lineTo(i * q, s); g.stroke(); g.beginPath(); g.moveTo(0, i * q); g.lineTo(s, i * q); g.stroke(); }
    }, 256, [3, 3.5]);
    const plank = canvasTex((g, s) => {
        const rows = 8, h = s / rows;
        for (let r = 0; r < rows; r++) {
            const off = (r % 2) * s / 2;
            for (let k = -1; k < 2; k++) { g.fillStyle = `hsl(28, 42%, ${52 + ((r * 7 + k * 3) % 10)}%)`; g.fillRect(off + k * s, r * h, s, h); }
            g.fillStyle = 'rgba(70,40,20,.28)'; g.fillRect(0, r * h, s, 2); g.fillRect(off % s, r * h, 2, h);
        }
    }, 256, [5, 3]);
    const concrete = canvasTex((g, s) => {
        g.fillStyle = '#ddd3c1'; g.fillRect(0, 0, s, s);
        for (let i = 0; i < 1400; i++) { g.fillStyle = `rgba(90,70,50,${(i % 9) / 110})`; g.fillRect((i * 97) % s, (i * 61) % s, 2, 2); }
        g.strokeStyle = 'rgba(90,70,50,.18)'; g.lineWidth = 2; g.strokeRect(1, 1, s - 2, s - 2);
    }, 256, [3, 4]);
    const lino = canvasTex((g, s) => {
        const n = 8, q = s / n;
        for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) { g.fillStyle = (i + j) % 2 ? '#cfdcc0' : '#e4ecdb'; g.fillRect(i * q, j * q, q, q); }
    }, 256, [2, 3.5]);
    const herring = canvasTex((g, s) => {
        g.fillStyle = '#7a4b2e'; g.fillRect(0, 0, s, s);
        const u = s / 8;
        for (let y = -s; y < s * 2; y += u) for (let x = 0; x < s; x += u * 2) {
            g.fillStyle = `hsl(24, 38%, ${34 + ((x + y) % 8)}%)`;
            g.save(); g.translate(x, y + (x / u) * u / 2); g.rotate(Math.PI / 4); g.fillRect(0, 0, u * 1.9, u * .9); g.restore();
        }
    }, 256, [2, 3.5]);

    const floor = ([x0, x1], tex) => {
        const f = box(x1 - x0, .2, 14, M('#ffffff', { map: tex }), 0);
        at(f, (x0 + x1) / 2, -.1, 0);
        f.castShadow = false;
        scene.add(f);
    };
    floor(ROOMS.store, concrete);
    floor(ROOMS.kitchen, tile);
    floor(ROOMS.sala, plank);
    floor(ROOMS.break, lino);
    floor(ROOMS.office, herring);
    const rug = box(4.2, .02, 3, M('#8f2415'), 0);
    rug.castShadow = false;
    scene.add(at(rug, 34, .01, -1.6));

    const wallM = M('#fbf6ea'), capM = M('#c93a21');
    const wall = (x0, z0, x1, z1, h = 2.6, t = .24) => {
        const len = Math.hypot(x1 - x0, z1 - z0);
        const wl = box(len, h, t, wallM, .03);
        at(wl, (x0 + x1) / 2, h / 2, (z0 + z1) / 2);
        wl.rotation.y = -Math.atan2(z1 - z0, x1 - x0);
        scene.add(wl);
        const cap = box(len + .02, .08, t + .04, capM, 0);
        at(cap, (x0 + x1) / 2, h + .04, (z0 + z1) / 2);
        cap.rotation.y = wl.rotation.y;
        scene.add(cap);
    };
    wall(-22.12, -7, 38.12, -7);
    wall(-22, -7, -22, 7);
    wall(-12, -7, -12, 1.6, 1.1); wall(-12, 3.8, -12, 7, 1.1);
    wall(0, -7, 0, 1.6, 1.1); wall(0, 6.9, 0, 7, 1.1);
    wall(22, -7, 22, 1.4, 1.1); wall(22, 3.6, 22, 7, 1.1);
    wall(30, -7, 30, .4, 1.1); wall(30, 2.6, 30, 7, 1.1);
    wall(38, -7, 38, 7, .35);

    w.glassM = new THREE.MeshStandardMaterial({ color: 0xbcd3dc, roughness: .2, metalness: .1, emissive: 0xffc27a, emissiveIntensity: 0 });
    for (const x of [2.5, 6.1, 9.7, 13.3, 16.9, 20.5, 33.5]) {
        scene.add(at(box(2.2, 1.1, .06, w.glassM, 0), x, 1.3, -6.86));
        scene.add(at(box(2.4, .08, .2, M('#e2d1ac'), 0), x, .72, -6.8));
    }

    const signs = [];
    const roomSign = (text, x, width = 3.4) => {
        const c = document.createElement('canvas');
        c.width = 512; c.height = 96;
        const draw = () => {
            const g = c.getContext('2d');
            g.fillStyle = '#c93a21'; g.fillRect(0, 0, 512, 96);
            g.fillStyle = '#fbf6ea'; g.font = '700 54px Fraunces, Georgia, serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
            g.fillText(text, 256, 52);
        };
        draw();
        const t = new THREE.CanvasTexture(c);
        t.colorSpace = THREE.SRGBColorSpace;
        const m = new THREE.Mesh(new THREE.PlaneGeometry(width, width * 96 / 512), new THREE.MeshStandardMaterial({ map: t, roughness: .9 }));
        scene.add(at(m, x, 2.27, -6.87));
        signs.push(() => { draw(); t.needsUpdate = true; });
    };
    roomSign('DISPENSA', -17); roomSign('CUCINA', -6); roomSign('SALA', 7); roomSign('PAUSA', 26, 2.8); roomSign('UFFICIO', 34, 2.8);
    document.fonts?.ready.then(() => signs.forEach((redraw) => redraw()));
}

function rack(len) {
    const g = new THREE.Group(), steel = M('#9aa0a6', { metalness: .4, roughness: .5 }), board = M('#c9a46e');
    for (const x of [-len / 2, len / 2]) for (const z of [-.32, .32]) g.add(at(box(.06, 2, .06, steel, 0), x, 1, z));
    for (const y of [.12, .78, 1.44]) g.add(at(box(len, .05, .7, board, 0), 0, y, 0));
    return g;
}

export function itemMesh(ing) {
    const c = M(ing.color);
    switch (ing.form) {
        case 'sack': return box(.56, .24, .4, c, .1);
        case 'pack': return box(.42, .32, .3, M(ing.color, { roughness: .3, transparent: true, opacity: .85 }), .05);
        case 'can': { const g = new THREE.Group(); g.add(at(cyl(.11, .11, .22, c, 14), -.12, .11, 0)); g.add(at(cyl(.11, .11, .22, c, 14), .12, .11, 0)); return g; }
        case 'tub': { const g = new THREE.Group(); g.add(at(cyl(.13, .12, .16, c, 14), 0, .08, 0)); g.add(at(cyl(.135, .135, .03, M('#d9a13f'), 14), 0, .17, 0)); return g; }
        default: return box(.34, .22, .42, c, .03);
    }
}

export function slotPos(ing, i) {
    if (ing.place === 'pallet') {
        const lay = Math.floor(i / 6), k = i % 6;
        return [(k % 3 - 1) * .6, .2 + lay * .3, (Math.floor(k / 3) - .5) * .5];
    }
    const shelf = Math.floor(i / 4), k = i % 4;
    return [(k - 1.5) * .38, .15 + shelf * .66 + (ing.form === 'crate' || ing.form === 'pack' ? .11 : 0), 0];
}

function buildStoreroom(scene, w) {
    w.bays = new Map();
    for (const ing of INGREDIENTS) {
        const g = new THREE.Group();
        let x, z, rot = 0;
        if (ing.place === 'back') { x = -20.3 + ing.slot * 1.72; z = -6.3; }
        else if (ing.place === 'left') { x = -21.3; z = -4.5 + ing.slot * 1.72; rot = Math.PI / 2; }
        else { x = -18.2 + ing.slot * 2.6; z = 2.4; }
        if (ing.place === 'pallet') {
            for (const dz of [-.45, 0, .45]) g.add(at(box(1.9, .1, .3, M('#b88a55'), 0), 0, .05, dz));
        } else {
            g.add(rack(1.6));
        }
        const items = new THREE.Group();
        g.add(items);
        at(g, x, 0, z);
        g.rotation.y = rot;
        const bay = { ing, group: g, items, count: -1, level: null, shownLevel: null };
        w.pickable(g, { kind: 'stock', id: ing.key }, 1.1);
        bay.pin = label('', 'pin3d', 2.55, g);
        bay.pin.visible = false;
        w.bays.set(ing.key, bay);
    }
    scene.add(at(box(3.2, .03, 1.4, M('#d9a13f'), 0), -17, .01, 7.6));
}

function buildKitchen(scene, w) {
    const oven = new THREE.Group();
    oven.add(at(box(3.8, .9, 3.6, M('#8f6b4a'), .08), 0, .45, 0));
    oven.add(at(mesh(new THREE.SphereGeometry(1.75, 32, 16, 0, Math.PI * 2, 0, Math.PI / 2), M('#b8553a')), 0, .9, -.1));
    oven.add(at(box(1.3, .9, .7, M('#b8553a'), .1), 0, 1.3, 1.45));
    w.mouth = box(.8, .5, .1, new THREE.MeshStandardMaterial({ color: 0x2a1208, emissive: 0xff6a2a, emissiveIntensity: 1.6 }), .08);
    oven.add(at(w.mouth, 0, 1.2, 1.8));
    oven.add(at(cyl(.22, .26, 1.4, M('#7a4b2e'), 16), .6, 2.7, -.6));
    oven.add(at(box(1.2, .12, .5, M('#d9a13f'), .03), 0, .92, 1.95));
    at(oven, -8.6, 0, -4.4);
    w.oven = w.pickable(oven, { kind: 'oven' }, 2.4);
    w.ovenChip = label('', 'chip3d', 3.9, oven);
    w.ovenLight = new THREE.PointLight(0xff7a45, 6, 7, 1.6);
    w.ovenLight.position.set(-8.6, 1.4, -2.2);
    scene.add(w.ovenLight);

    const bench = new THREE.Group();
    bench.add(at(box(7.2, .86, 1.3, M('#8f6b4a'), .05), 0, .43, 0));
    bench.add(at(box(7.4, .08, 1.4, M('#f4efe6', { roughness: .4 }), .03), 0, .9, 0));
    at(bench, -4.6, 0, .6);
    w.pickable(bench, { kind: 'bench' }, 3.9);
    w.benchChip = label('', 'chip3d', 1.5, bench);
    w.benchSlot = (i) => new THREE.Vector3(-4.6 - 3.15 + (i % 8) * .9, .96, .75 - Math.floor(i / 8) * .4);

    const fridge = new THREE.Group();
    fridge.add(at(box(1.4, 2.1, 1, M('#c9cdd1', { metalness: .35, roughness: .35 }), .06), 0, 1.05, 0));
    fridge.add(at(box(1.1, 1.6, .04, new THREE.MeshStandardMaterial({ color: 0xcfe6ee, transparent: true, opacity: .45, roughness: .1 }), 0), 0, 1.15, .51));
    w.doughBalls = [];
    for (let i = 0; i < 6; i++) {
        const b = mesh(new THREE.SphereGeometry(.16, 14, 10), M('#f3e3c0'));
        at(b, (i % 2 - .5) * .45, .55 + Math.floor(i / 2) * .45, .3);
        b.visible = false;
        fridge.add(b);
        w.doughBalls.push(b);
    }
    at(fridge, -10.9, 0, 5.4);
    fridge.rotation.y = Math.PI / 2;
    w.pickable(fridge, { kind: 'fridge' }, 1.1);

    const pass = new THREE.Group();
    pass.add(at(box(.9, 1, 5.1, M('#c9cdd1', { metalness: .35, roughness: .4 }), .04), 0, .5, 0));
    for (let z = -2; z <= 2; z += 1.3) {
        pass.add(at(cyl(.02, .02, .9, M('#38291d'), 6), 0, 1.95, z));
        pass.add(at(cyl(.14, .2, .18, new THREE.MeshStandardMaterial({ color: 0x38291d, emissive: 0xff8a3a, emissiveIntensity: .9 }), 12), 0, 1.45, z));
    }
    at(pass, -.05, 0, 4.25);
    w.pickable(pass, { kind: 'pass' }, 2.7);
    w.passSlot = (i) => new THREE.Vector3(-.05, 1.04, 4.25 - 2.2 + (i % 8) * .62);

    // the ticket rail: orders accepted but not yet on the bench
    const rail = new THREE.Group();
    rail.add(at(box(2.6, .06, .06, M('#9aa0a6', { metalness: .5 }), 0), 0, 1.9, 0));
    w.railTickets = [];
    for (let i = 0; i < 10; i++) {
        const t = box(.18, .26, .01, M('#ffffff'), 0);
        at(t, -1.15 + i * .25, 1.74, .04);
        t.visible = false;
        rail.add(t);
        w.railTickets.push(t);
    }
    at(rail, -4.6, 0, -1.25);
    scene.add(rail);

    w.agents = {};
    for (const [id, a] of Object.entries(AGENTS)) {
        const p = person(a.look);
        at(p, ...a.pos);
        p.rotation.y = a.rot;
        w.agents[id] = w.pickable(p, { kind: 'agent', id }, .55);
    }
    scene.add(at(box(.5, 1.1, .7, M('#8f6b4a'), .05), 20.2, .55, 6.3));
    scene.add(at(box(2.4, .03, 1.4, M('#c93a21'), 0), 19, .01, 7.75));
}

function buildDiningExtras(scene) {
    scene.add(at(box(4.4, 1.1, .8, M('#8f6b4a'), .06), 14.5, .55, 6.2));
    scene.add(at(box(4.6, .08, 1, M('#f4efe6', { roughness: .4 }), .03), 14.5, 1.14, 6.2));
    for (let i = 0; i < 4; i++) scene.add(at(cyl(.18, .14, .7, M('#38291d'), 12), 12.9 + i * 1.05, .35, 7.0));
    [[1, -6.2], [21.2, -6.2], [21.4, 1.6], [9.6, 6.4], [-.9, -6.2]].forEach(([x, z]) => plant(scene, x, z));
}

function buildBreakRoom(scene, w) {
    const espresso = new THREE.Group();
    espresso.add(at(box(3.2, 1, .8, M('#8f6b4a'), .05), 0, .5, 0));
    espresso.add(at(box(3.3, .07, .9, M('#f4efe6', { roughness: .4 }), .03), 0, 1.03, 0));
    espresso.add(at(box(.8, .55, .5, M('#c9cdd1', { metalness: .5, roughness: .3 }), .06), -.6, 1.34, 0));
    espresso.add(at(box(.82, .08, .52, M('#c93a21'), .03), -.6, 1.64, 0));
    for (let i = 0; i < 4; i++) espresso.add(at(cyl(.06, .05, .1, M(i % 2 ? '#c93a21' : '#ffffff'), 10), .5 + i * .2, 1.12, .1));
    at(espresso, 24.6, 0, -6.35);
    w.pickable(espresso, { kind: 'espresso' }, 1.8);

    const lockers = new THREE.Group();
    for (let i = 0; i < 4; i++) {
        lockers.add(at(box(.52, 1.9, .55, M(i % 2 ? '#a8c09a' : '#cfdcc0'), .03), (i - 1.5) * .54, .95, 0));
        lockers.add(at(box(.04, .16, .03, M('#38291d'), 0), (i - 1.5) * .54 + .18, 1.05, .29));
    }
    at(lockers, 28.4, 0, -6.4);
    w.pickable(lockers, { kind: 'lockers' }, 1.4);

    const board = new THREE.Group();
    for (const x of [-.75, .75]) board.add(at(box(.07, 1.9, .07, M('#5a3a24'), 0), x, .95, 0));
    board.add(at(box(1.7, 1.1, .06, M('#c99a68'), .02), 0, 1.4, 0));
    w.boardPapers = new THREE.Group();
    board.add(w.boardPapers);
    w.boardPapers.add(at(box(.55, .7, .01, M('#ffffff'), 0), -.45, 1.42, .04));
    at(board, 27.4, 0, -3.4);
    w.pickable(board, { kind: 'board' }, 1.1);
    w.boardPin = label('', 'pin3d', 2.3, board);
    w.boardPin.visible = false;

    const sofaM = M('#4a6440'), sofa = new THREE.Group();
    sofa.add(at(box(2.8, .45, .9, sofaM, .1), 0, .25, 0));
    sofa.add(at(box(2.8, .65, .25, sofaM, .1), 0, .62, -.38));
    for (const x of [-1.4, 1.4]) sofa.add(at(box(.22, .6, .9, sofaM, .08), x, .35, 0));
    scene.add(at(sofa, 24.6, 0, 2.6));
    scene.add(at(box(1.4, .4, .7, M('#c99a68'), .04), 24.6, .2, 4));
    const table = new THREE.Group();
    table.add(at(cyl(.55, .55, .05, M('#f6f1e4'), 24), 0, .74, 0));
    table.add(at(cyl(.05, .05, .72, M('#38291d'), 8), 0, .36, 0));
    scene.add(at(table, 27.6, 0, 1.4));
    plant(scene, 29.2, -5.2, .8);
    plant(scene, 22.8, 6.2, .9);
    // seats for whoever is in the break room: sofa first, then the round table
    w.breakSeats = [[23.7, 2.7], [24.6, 2.7], [25.5, 2.7], [27.6, .55], [28.3, 1.9], [26.9, 1.9]].map(([x, z]) => new THREE.Vector3(x, 0, z));
}

function buildOffice(scene, w) {
    const desk = (x, z, width = 2.4) => {
        const g = new THREE.Group(), wood = M('#5a3a24');
        g.add(at(box(width, .08, 1.1, wood, .03), 0, .76, 0));
        for (const dx of [-width / 2 + .3, width / 2 - .3]) g.add(at(box(.5, .72, 1, M('#6e4a30'), .03), dx, .36, 0));
        scene.add(at(g, x, 0, z));
    };
    desk(33.6, -2.2);
    const screenM = new THREE.MeshStandardMaterial({ color: 0x2a2118, emissive: 0xdfe6f5, emissiveIntensity: .7 });
    scene.add(at(box(.6, .03, .4, M('#9aa0a6', { metalness: .4 }), .01), 33.9, .82, -2.1));
    const lid = box(.6, .38, .02, screenM, .01);
    at(lid, 33.9, 1.0, -2.3);
    lid.rotation.x = -.15;
    scene.add(lid);
    for (let i = 0; i < 4; i++) scene.add(at(box(.34, .06, .46, M(['#8f2415', '#4a6440', '#d9a13f', '#3d6fa8'][i]), .01), 34.75, .83 + i * .065, -2.2));
    scene.add(at(cyl(.02, .02, .45, M('#38291d'), 6), 32.6, 1.02, -2.55));
    w.lampShadeM = new THREE.MeshStandardMaterial({ color: 0x4a6440, emissive: 0xffb45a, emissiveIntensity: 0 });
    scene.add(at(cyl(.06, .18, .16, w.lampShadeM, 14), 32.6, 1.26, -2.5));
    scene.add(at(box(.5, .5, .5, M('#38291d'), .05), 33.6, .25, -3.15));

    const tray = new THREE.Group();
    tray.add(box(.46, .05, .36, M('#c93a21'), .02));
    w.trayPapers = new THREE.Group();
    tray.add(w.trayPapers);
    at(tray, 32.95, .83, -2.0);
    w.pickable(tray, { kind: 'approvals' }, .7);
    w.trayChip = label('', 'chip3d', .9, tray);

    const cabinet = new THREE.Group();
    cabinet.add(at(box(.9, 1.4, .7, M('#7f8f7a'), .03), 0, .7, 0));
    for (let i = 0; i < 3; i++) {
        cabinet.add(at(box(.82, .38, .02, M('#93a38d'), .01), 0, .3 + i * .44, .36));
        cabinet.add(at(box(.2, .04, .04, M('#38291d'), 0), 0, .4 + i * .44, .38));
    }
    at(cabinet, 37.1, 0, -5.9);
    cabinet.rotation.y = -Math.PI / 2;
    w.pickable(cabinet, { kind: 'invoices' }, .8);

    const safe = new THREE.Group();
    safe.add(at(box(.8, .85, .7, M('#38291d'), .05), 0, .43, 0));
    const dial = cyl(.12, .12, .05, M('#d9a13f', { metalness: .6, roughness: .3 }), 18);
    dial.rotation.x = Math.PI / 2;
    safe.add(at(dial, 0, .5, .36));
    at(safe, 37.1, 0, -4.3);
    safe.rotation.y = -Math.PI / 2;
    w.pickable(safe, { kind: 'budget' }, .8);
    w.safeChip = label('', 'chip3d', 1.4, safe);

    const shelf = new THREE.Group();
    shelf.add(at(box(2.2, 1.6, .4, M('#6e4a30'), .02), 0, .8, 0));
    for (let r = 0; r < 3; r++) for (let i = 0; i < 9; i++) {
        shelf.add(at(box(.14, .36, .3, M(['#8f2415', '#4a6440', '#d9a13f', '#3d6fa8', '#efe3c8'][(i + r) % 5]), .01), -.9 + i * .22, .35 + r * .5, .08));
    }
    scene.add(at(shelf, 31.4, 0, -6.6));
    plant(scene, 37.2, -1.6, .9);

    desk(34, 4.3, 2);
    scene.add(at(box(.5, .5, .5, M('#38291d'), .05), 34, .25, 3.45));
    w.phoneM = new THREE.MeshStandardMaterial({ color: 0x2a2118, emissive: 0x5b5fc7, emissiveIntensity: 0 });
    scene.add(at(box(.18, .02, .32, w.phoneM, .01), 34.5, .82, 4.15));
    const capo = person({ body: '#3d6fa8', seated: true });
    at(capo, 34, 0, 3.45);
    w.pickable(capo, { kind: 'capo' }, .9);
    w.capoPin = label('', 'pin3d m365', 2.2, capo);
    w.capoPin.visible = false;
}

function buildStreet(scene, w) {
    scene.add(at(box(220, .05, 4.2, M('#bfb39c'), 0), 0, -.18, 11.4));
    for (let x = -100; x < 100; x += 3) scene.add(at(box(1.4, .02, .14, M('#f6efdf'), 0), x, -.15, 11.4));
    scene.add(at(box(220, .12, .5, M('#d6cab3'), 0), 0, -.14, 9.1));
    for (let x = -40; x <= 48; x += 8) {
        if (x > -26 && x < -8) continue;
        const g = new THREE.Group();
        g.add(at(cyl(.12, .16, 1.2, M('#7a4b2e'), 8), 0, .6, 0));
        g.add(at(mesh(new THREE.IcosahedronGeometry(.9, 1), M('#6f8f5a', { flatShading: true })), 0, 1.7, 0));
        scene.add(at(g, x, -.2, 14.3));
    }
    w.vespas = ['#a8c09a', '#c93a21'].map((color, i) => {
        const v = vespa(color);
        at(v, 25.3 + i * 1.7, -.2, 8.2);
        v.rotation.y = -.3;
        scene.add(v);
        return { obj: v, home: v.position.clone(), busy: false };
    });
    w.van = makeVan();
    at(w.van, -70, -.2, 11.2);
    w.pickable(w.van, { kind: 'van' }, 2.2);
    w.vanPin = label('<span>Supplier · Fruttivendolo Marittimo</span>', 'pin3d info', 3.5, w.van);
    w.vanPin.visible = false;
    w.flyers = new THREE.Group();
    scene.add(w.flyers);
}
