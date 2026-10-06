// Turns the real floor snapshot into movement. The server says WHAT is true (this pizza is
// baking, table 9 is waiting, PO-1004 needs a signature); this module decides how that looks
// and animates the transitions between two snapshots: a runner carries the pizza that just left
// the pass, the van brings what was just delivered, paper flies from Nonna to the Capo.
import { THREE, M, box, cyl, at, label, person, pizzaMesh, dressPizza, discard, empty, SKIN, CLOTH } from './kit.js';
import { INGREDIENTS, SPOTS, itemMesh, slotPos } from './world.js';

const LOW = .25;
const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const PENDING_PO = ['PendingApproval', 'BlockedByBudget'];
const hash = (s) => [...String(s)].reduce((h, c) => (h * 31 + c.charCodeAt(0)) | 0, 7) >>> 0;
const pickOf = (arr, key) => arr[hash(key) % arr.length];

export class Live {
    constructor(scene, world) {
        this.scene = scene;
        this.w = world;
        this.snap = null;
        this.offset = 0;              // server clock minus client clock, ms
        this.tables = new Map();      // id → table rig
        this.pizzas = new Map();      // id → { view, mesh, slot, where }
        this.jobs = [];               // pizzas that left the pass for a table
        this.runners = [];
        this.crew = new Map();        // name → seated figure in the break room
        this.crewKey = '';
        this.tickets = new Map();     // orderId → last state
        this.vanTrip = { state: 'home', x: -70, t: 0, lines: [] };
        this.holds = new Map();       // ingredient → level to keep showing until the van unloads
        this.lastLevels = new Map();
    }

    now() { return Date.now() + this.offset; }

    apply(snap) {
        this.offset = Date.parse(snap.at) - Date.now();
        const first = this.snap === null;
        this.snap = snap;
        if (first) {
            this.buildTables(snap.dining.tables);
        }
        this.syncTables(snap.dining.tables, first);
        this.syncKitchen(snap.kitchen, first);
        this.syncPantry(snap.stock, snap.office.purchaseOrders);
        this.syncOffice(snap.office);
        this.syncCrew(snap);
        this.syncTickets(snap.tickets, first);
        this.syncTakeaway(snap.tickets);
    }

    /* ─────────────────────────── dining room ─────────────────────────── */

    buildTables(list) {
        const sx = 22 / 100, sz = 14 / 100;
        for (const t of list) {
            const cx = 22 - (t.x + t.w / 2) * sx, cz = -7 + (t.y + t.h / 2) * sz;
            const w = t.w * sx * .62, d = t.h * sz * .62;
            const g = new THREE.Group();
            if (t.shape === 'Round') {
                const r = Math.min(w, d) / 2;
                g.add(at(cyl(r, r, .06, M('#f6f1e4'), 28), 0, .76, 0));
            } else {
                g.add(at(box(w, .07, d, M('#b9875a'), .03), 0, .76, 0));
                g.add(at(box(w * .9, .012, d * .55, M('#c93a21'), 0), 0, .8, 0));
            }
            g.add(at(cyl(.06, .06, .72, M('#38291d'), 8), 0, .37, 0));
            g.add(at(cyl(.28, .3, .04, M('#38291d'), 16), 0, .02, 0));
            const seats = [];
            if (t.shape === 'Round') {
                const r = Math.min(w, d) / 2 + .4;
                for (let i = 0; i < t.seats; i++) { const a = (i / t.seats) * Math.PI * 2 + Math.PI / 4; seats.push([Math.cos(a) * r, Math.sin(a) * r]); }
            } else {
                const along = w >= d, len = along ? w : d, cross = (along ? d : w) / 2 + .4;
                const a = Math.ceil(t.seats / 2), b = t.seats - a;
                for (let i = 0; i < a; i++) { const p = (i + .5) / a * len - len / 2; seats.push(along ? [p, -cross] : [-cross, p]); }
                for (let i = 0; i < b; i++) { const p = (i + .5) / b * len - len / 2; seats.push(along ? [p, cross] : [cross, p]); }
            }
            for (const [x, z] of seats) {
                const ch = new THREE.Group();
                ch.add(at(box(.38, .06, .38, M('#8f6b4a'), .02), 0, .44, 0));
                ch.add(at(box(.38, .42, .05, M('#8f6b4a'), .02), 0, .66, -.17));
                for (const [lx, lz] of [[-.15, -.15], [.15, -.15], [-.15, .15], [.15, .15]]) ch.add(at(box(.04, .42, .04, M('#38291d'), 0), lx, .21, lz));
                at(ch, x, 0, z);
                ch.lookAt(0, 0, 0);
                ch.rotation.y += Math.PI;
                g.add(ch);
            }
            const guests = new THREE.Group();
            g.add(guests);
            at(g, cx, 0, cz);
            this.w.pickable(g, { kind: 'table', id: t.id }, Math.max(w, d) / 2 + .7);
            const rig = { id: t.id, cx, cz, w, d, seats, group: g, guests, plates: [], partyKey: null };
            rig.chip = label('', 'chip3d', 1.9, g);
            rig.pin = label('', 'pin3d', 2.9, g);
            rig.pin.visible = false;
            this.tables.set(t.id, rig);
        }
    }

    syncTables(list, first) {
        const now = this.now();
        for (const t of list) {
            const rig = this.tables.get(t.id);
            if (!rig) continue;
            const p = t.party;
            const key = p ? `${p.name}|${p.size}` : null;
            if (key !== rig.partyKey) {
                this.clearTable(rig);
                if (p) this.seatGuests(rig, p, first);
                rig.partyKey = key;
            }
            const state = p?.state;
            const cls = !p ? 's-free' : { Seated: 's-seated', WaitingForFood: 's-waiting', Eating: 's-eating', Paying: 's-paying' }[state] ?? 's-free';
            const waited = p?.orderedAt && state === 'WaitingForFood' ? (now - Date.parse(p.orderedAt)) / 60000 : 0;
            rig.chip.element.className = `chip3d ${cls}`;
            rig.chip.element.innerHTML = `<i></i>${t.id}${state === 'WaitingForFood' ? ` · ${Math.floor(waited)}m` : ''}`;
            rig.pin.visible = waited >= 3;
            if (rig.pin.visible) rig.pin.element.innerHTML = `<span>Table ${t.id} waiting ${Math.floor(waited)} min</span>`;
            rig.party = p;
        }
    }

    seatGuests(rig, p, instant) {
        empty(rig.guests);
        for (let i = 0; i < Math.min(p.size, rig.seats.length); i++) {
            const [x, z] = rig.seats[i];
            const g = person({ body: pickOf(CLOTH, p.name + i), skin: pickOf(SKIN, p.name + i * 7), seated: true });
            at(g, x, 0, z);
            g.rotation.y = Math.atan2(-x, -z);
            g.scale.setScalar(instant ? 1 : .01);
            rig.guests.add(g);
        }
    }

    clearTable(rig) {
        empty(rig.guests);
        for (const m of rig.plates) discard(m);
        rig.plates = [];
    }

    /* ─────────────────────────── kitchen ─────────────────────────── */

    syncKitchen(k, first) {
        const seen = new Set();
        const used = { Preparing: new Set(), Ready: new Set() };
        for (const e of this.pizzas.values()) if (e.slot >= 0 && used[e.view.state]) used[e.view.state].add(e.slot);
        const free = (state, cap) => { for (let i = 0; i < cap; i++) if (!used[state].has(i)) { used[state].add(i); return i; } return -1; };

        for (const v of k.pizzas) {
            seen.add(v.id);
            let e = this.pizzas.get(v.id);
            if (!e) {
                e = { view: v, mesh: pizzaMesh(), slot: -1 };
                e.mesh.visible = false;
                this.scene.add(e.mesh);
                this.pizzas.set(v.id, e);
            }
            if (e.view.state !== v.state || e.slot < 0) {
                if (e.slot >= 0 && used[e.view.state]) used[e.view.state].delete(e.slot);
                e.slot = -1;
                if (v.state === 'Preparing') e.slot = free('Preparing', 16);
                if (v.state === 'Ready') e.slot = free('Ready', 8);
            }
            e.view = v;
            e.mesh.visible = (v.state === 'Preparing' || v.state === 'Ready') && e.slot >= 0;
            e.mesh.userData.tag.visible = !!v.surface;
            if (v.state === 'Ready' && e.slot >= 0) { e.mesh.position.copy(this.w.passSlot(e.slot)); dressPizza(e.mesh, 'pass'); }
        }

        // pizzas that left the line since the last read went out to a table or a courier
        for (const [id, e] of this.pizzas) {
            if (seen.has(id)) continue;
            this.pizzas.delete(id);
            if (!first && e.view.state === 'Ready' && e.view.tableId != null && this.tables.has(e.view.tableId)) {
                e.mesh.visible = true;
                this.jobs.push({ mesh: e.mesh, tableId: e.view.tableId, from: e.mesh.position.clone() });
            } else if (!first && e.view.state === 'Ready' && e.view.tableId == null) {
                // an online order: it goes from the pass to the Asporto counter to be boxed
                e.mesh.visible = true;
                e.mesh.userData = { ...e.mesh.userData, t: 0, delay: 0, from: e.mesh.position.clone(), to: SPOTS.counter.clone(), dur: 1.6, arc: 2.4 };
                this.w.flyers.add(e.mesh);
            } else {
                discard(e.mesh);
            }
        }

        const queued = k.pizzas.filter((p) => p.state === 'OrderAccepted').length;
        this.w.railTickets.forEach((t, i) => { t.visible = i < queued; });
        const baking = k.pizzas.filter((p) => p.state === 'Baking').length;
        const preparing = k.pizzas.filter((p) => p.state === 'Preparing').length;
        this.w.ovenChip.element.innerHTML = `<i style="color:var(--st-accent)"></i>Oven ${baking}/${k.ovenCapacity}`;
        this.w.benchChip.element.className = `chip3d ${queued ? 's-seated' : 's-free'}`;
        this.w.benchChip.element.innerHTML = `<i></i>Bench ${preparing}/${k.prepCapacity}${queued ? ` · ${queued} queued` : ''}`;

        const doughs = k.doughs.slice(0, this.w.doughBalls.length);
        this.w.doughBalls.forEach((b, i) => {
            const d = doughs[i];
            b.visible = !!d && i < Math.max(k.fridgeCapacity, 1);
            b.userData.dough = d;
            b.material = d?.state === 'Ready' ? M('#f0d39c') : M('#f3e3c0');
        });
    }

    /* ─────────────────────────── storeroom ─────────────────────────── */

    syncPantry(stock, orders) {
        const open = new Map();
        for (const po of orders) if ([...PENDING_PO, 'Approved'].includes(po.state) && !open.has(po.ingredient)) open.set(po.ingredient, po);
        for (const s of stock) {
            const bay = this.w.bays.get(s.ingredient);
            if (!bay) continue;
            const held = this.holds.get(s.ingredient);
            bay.level = held ?? s.level;
            bay.grams = s.grams;
            bay.opening = s.opening;
            bay.po = open.get(s.ingredient) ?? null;
            this.lastLevels.set(s.ingredient, s.level);
            this.refreshBay(bay);
            const show = s.level < LOW || !!bay.po;
            bay.pin.visible = show;
            if (show) {
                const waiting = !bay.po || PENDING_PO.includes(bay.po.state);
                bay.pin.element.className = `pin3d${waiting ? '' : ' info'}`;
                const state = bay.po ? { PendingApproval: 'waiting for a signature', BlockedByBudget: 'blocked by budget', Approved: 'approved, on its way' }[bay.po.state] : null;
                bay.pin.element.innerHTML = `<span>${bay.ing.name} ${Math.round(s.level * 100)}%${state ? ` · ${state}` : ''}</span>`;
            }
        }
    }

    refreshBay(bay) {
        const n = Math.max(0, Math.min(12, Math.ceil((bay.level ?? 0) * 12)));
        if (n === bay.count) return;
        bay.count = n;
        empty(bay.items);
        for (let i = 0; i < n; i++) {
            const m = itemMesh(bay.ing);
            m.position.set(...slotPos(bay.ing, i));
            bay.items.add(m);
        }
    }

    /* ─────────────────────────── office & break room ─────────────────────────── */

    pending(office = this.snap?.office) {
        if (!office) return [];
        return [
            ...office.purchaseOrders.filter((p) => PENDING_PO.includes(p.state)).map((p) => ({ type: 'po', ...p })),
            ...office.timeOff.filter((t) => t.state === 'Pending').map((t) => ({ type: 'timeoff', ...t })),
        ];
    }

    syncOffice(office) {
        const pend = this.pending(office);
        if (this.w.trayPapers.children.length !== pend.length) {
            empty(this.w.trayPapers);
            pend.forEach((x, i) => this.w.trayPapers.add(at(box(.38, .015, .28, M(x.state === 'BlockedByBudget' ? '#f0dda9' : '#ffffff'), 0), 0, .035 + i * .02, 0)));
        }
        this.w.trayChip.element.className = `chip3d ${pend.length ? 's-seated' : 's-free'}`;
        this.w.trayChip.element.innerHTML = `<i></i>In-tray ${pend.length}`;
        this.w.capoPin.visible = pend.length > 0;
        if (pend.length) this.w.capoPin.element.innerHTML = `<span>${pend.length} waiting for a person</span>`;
        const used = office.budget.usedPercent;
        this.w.safeChip.element.className = `chip3d ${office.budget.isTight ? 's-waiting' : 's-eating'}`;
        this.w.safeChip.element.innerHTML = `<i></i>Budget ${Math.round(used)}%`;

        const pinned = office.timeOff.filter((t) => t.state === 'Pending');
        if (this.w.boardPapers.children.length !== pinned.length + 1) {
            while (this.w.boardPapers.children.length > 1) discard(this.w.boardPapers.children[1]);
            pinned.forEach((t, i) => this.w.boardPapers.add(at(box(.3, .36, .01, M('#f0dda9'), 0), .25 + (i % 2) * .35, 1.62 - Math.floor(i / 2) * .42, .045)));
        }
        this.w.boardPin.visible = pinned.length > 0;
        // the name is whatever someone typed into a time-off request: escape it
        if (pinned.length) this.w.boardPin.element.innerHTML = `<span>${esc(pinned[0].name)} asks: ${esc(pinned[0].day)}${pinned[0].slot ? ' ' + esc(pinned[0].slot.toLowerCase()) : ''} off</span>`;
    }

    /** Who is on the floor this shift: service runs the room, couriers wait by their Vespas. */
    shift(snap = this.snap) {
        const today = snap.office.rota[0]?.day;
        const slot = new Date(this.now()).getHours() < 16 ? 'Lunch' : 'Dinner';
        const absent = new Set(snap.staff.filter((s) => s.absentToday).map((s) => s.name));
        const on = snap.office.rota.filter((r) => r.day === today && r.slot === slot && r.assignedTo && !absent.has(r.assignedTo));
        return { slot, today, service: on.filter((r) => r.role === 'Service').map((r) => r.assignedTo), others: on.filter((r) => r.role !== 'Service').map((r) => ({ name: r.assignedTo, role: r.role })) };
    }

    syncCrew(snap) {
        const { service, others } = this.shift(snap);
        const runners = service.length ? service.slice(0, 3) : ['Runner'];
        const crew = [...service.slice(3).map((name) => ({ name, role: 'Service' })), ...others];
        // the counter is never left without someone to ride: a stand-in when the rota names no courier
        if (!crew.some((c) => c.role === 'Courier')) crew.push({ name: 'Rider', role: 'Courier' });
        const key = runners.join(',') + '|' + crew.map((c) => c.name).join(',');
        if (key === this.crewKey) return;
        this.crewKey = key;

        // runners: keep the ones still on shift where they are, add newcomers at the break-room door
        const keep = this.runners.filter((r) => runners.includes(r.name));
        for (const r of this.runners) if (!keep.includes(r)) { this.drop(r); this.w.unpick(r.obj); discard(r.obj); }
        this.runners = runners.map((name, i) => {
            const home = new THREE.Vector3(1.3, 0, 2.2 + i * 1.9);
            const existing = keep.find((r) => r.name === name);
            if (existing) { existing.home = home; return existing; }
            const obj = person({ body: '#38291d', apron: '#fbf6ea', skin: pickOf(SKIN, name) });
            obj.position.set(22.6, 0, 2.5);
            this.w.pickable(obj, { kind: 'staff', id: name }, .55);
            return { name, obj, home, state: 'toHome', target: home.clone(), load: [], delivered: 0 };
        });

        for (const [name, fig] of this.crew) if (!crew.some((c) => c.name === name)) { this.w.unpick(fig); discard(fig); this.crew.delete(name); }
        // couriers wait in Asporto by their Vespas; everyone else off the floor sits in the break room
        let seat = 0, spot = 0;
        crew.forEach((c) => {
            if (this.crew.has(c.name)) return;
            const courier = c.role === 'Courier';
            const place = courier ? this.w.courierSpots[spot++] : this.w.breakSeats[seat++];
            if (!place) return;
            const look = c.role === 'Pizzaiolo' ? { body: '#ffffff', apron: '#c93a21' } : courier ? { body: pickOf(['#3d6fa8', '#d9a13f'], c.name), hat: 'cap', apron: '#c93a21' } : { body: '#38291d', apron: '#fbf6ea' };
            const fig = person({ ...look, skin: pickOf(SKIN, c.name), seated: !courier });
            fig.position.copy(place);
            if (courier) fig.rotation.y = Math.PI * .75;
            this.w.pickable(fig, { kind: 'staff', id: c.name }, .55);
            this.crew.set(c.name, fig);
        });
    }

    drop(runner) {
        for (const j of runner.load) this.serve(j);
        runner.load = [];
    }

    /* ─────────────────────────── street ─────────────────────────── */

    syncTickets(tickets, first) {
        const current = new Set();
        for (const t of tickets) {
            current.add(t.orderId);
            const before = this.tickets.get(t.orderId);
            this.tickets.set(t.orderId, t.state);
            if (!first && before === 'Cooking' && t.state === 'Done' && t.mode === 'Delivery') this.ride(!!t.surface);
        }
        // the counter keeps its last 20 tickets; remember no more than it does
        for (const id of [...this.tickets.keys()]) if (!current.has(id)) this.tickets.delete(id);
    }

    ride(fromM365) {
        const v = this.w.vespas.find((x) => !x.busy);
        if (!v) return;
        v.busy = true;
        v.t = 0;
        v.obj.userData.flag.visible = fromM365;
    }

    /** Asporto: slips on the board for what is cooking, boxes on the counter for what just left. */
    syncTakeaway(tickets) {
        const cooking = tickets.filter((t) => t.state === 'Cooking');
        const recent = tickets.filter((t) => t.state === 'Done' && t.readyAt && this.now() - Date.parse(t.readyAt) < 90000);
        this.w.boardSlips.forEach((slip, i) => {
            const t = cooking[i];
            slip.visible = !!t;
            if (t) slip.material = t.surface ? M('#8b8ff0', { emissive: '#5b5fc7', emissiveIntensity: .4 }) : M('#ffffff');
        });
        this.w.counterBoxes.forEach((b, i) => {
            const t = recent[i];
            b.visible = !!t;
            b.userData.ribbon.visible = !!t?.surface;
        });
        const mine = cooking.find((t) => t.surface);
        this.w.takeawayPin.visible = !!mine;
        if (mine) this.w.takeawayPin.element.innerHTML = `<span>${esc(mine.amount)}× ${esc(mine.pizza)} · via ${esc(mine.surface)}</span>`;
    }

    /* ─────────────────────────── events ─────────────────────────── */

    onEvent(e) {
        const W = this.w;
        switch (e.kind) {
            case 'stock.received':
                if (e.actor !== 'Engine Room' && W.bays.has(e.subject)) this.deliver(e.subject);
                break;
            case 'po.requested':
            case 'po.blocked':
                this.paper(SPOTS.desk, SPOTS.capoDesk, e.kind === 'po.blocked' ? '#f0dda9' : '#ffffff');
                break;
            case 'po.approved':
                if (e.actor !== 'Nonna' || e.channel !== 'Autonomous') this.paper(SPOTS.capoDesk, SPOTS.desk, '#a8c09a');
                break;
            case 'po.rejected':
                this.paper(SPOTS.capoDesk, SPOTS.desk, '#e7a5a0');
                break;
            case 'invoice.filed':
                this.paper(SPOTS.backDoor, SPOTS.cabinet, '#ffffff', 2.2, 5);
                break;
            case 'timeoff.requested':
                this.paper(SPOTS.board, SPOTS.desk, '#f0dda9', 1.4, 2);
                break;
            case 'timeoff.approved':
            case 'timeoff.declined':
                this.paper(SPOTS.capoDesk, SPOTS.board, e.kind === 'timeoff.approved' ? '#a8c09a' : '#e7a5a0', 1.4, 2);
                break;
            default:
                break;
        }
    }

    deliver(ingredient) {
        const bay = this.w.bays.get(ingredient);
        // keep showing the old level until the van has actually unloaded it
        this.holds.set(ingredient, this.holds.get(ingredient) ?? Math.max(0, (this.lastLevels.get(ingredient) ?? .3) - .5));
        bay.level = this.holds.get(ingredient);
        this.refreshBay(bay);
        const v = this.vanTrip;
        if (!v.lines.includes(ingredient)) v.lines.push(ingredient);
        if (v.state === 'home') { v.state = 'driving'; v.x = -70; }
        else if (v.state === 'leaving') v.again = true;   // turn round at the depot and come straight back
    }

    paper(from, to, color, dur = 1.4, arc = 2.2) {
        const p = box(.32, .02, .24, M(color), 0);
        p.userData = { t: 0, delay: 0, from: from.clone(), to: to.clone(), dur, arc, spin: true };
        p.position.copy(from);
        p.visible = false;
        this.w.flyers.add(p);
    }

    /** The object an event happened to, for the director's camera and the glow. */
    targetFor(e) {
        const W = this.w;
        const byKind = (k) => W.pickables.find((o) => o.userData.pick.kind === k);
        if (e.kind.startsWith('po.') || e.kind === 'invoice.filed') return e.kind === 'invoice.filed' ? byKind('invoices') : byKind('approvals');
        if (e.kind.startsWith('timeoff.') || e.kind === 'shift.assigned' || e.kind === 'absence.reported') return byKind('board');
        if (e.kind.startsWith('stock.')) return W.bays.get(e.subject)?.group ?? byKind('stock');
        if (e.kind === 'order.placed') return e.area === 'Street' ? byKind('takeaway') : W.agents.giuseppe;
        if (e.kind === 'escalation') return W.agents.procurement;
        return null;
    }

    /* ─────────────────────────── frame ─────────────────────────── */

    tick(dt, nowMs, night) {
        const now = this.now();
        // bench pizzas grow their toppings as preparation runs
        for (const e of this.pizzas.values()) {
            if (e.view.state !== 'Preparing' || e.slot < 0) continue;
            const k = e.view.startedAt && e.view.prepSeconds ? Math.min(1, (now - Date.parse(e.view.startedAt)) / (e.view.prepSeconds * 1000)) : .5;
            e.mesh.position.lerp(this.w.benchSlot(e.slot), .3);
            dressPizza(e.mesh, 'bench', k);
        }
        for (const b of this.w.doughBalls) {
            const d = b.userData.dough;
            if (!b.visible || !d) continue;
            const k = d.state === 'Ready' || !d.startedAt || !d.finishesAt ? 1
                : Math.min(1, (now - Date.parse(d.startedAt)) / Math.max(1, Date.parse(d.finishesAt) - Date.parse(d.startedAt)));
            b.scale.set(.75 + k * .35, .6 + k * .3, .75 + k * .35);
        }
        for (const t of this.tables.values()) {
            t.guests.children.forEach((g) => { if (g.scale.x < 1) g.scale.setScalar(Math.min(1, g.scale.x + dt * 3)); });
            const eating = t.party?.state === 'Eating' ? Math.min(1, (now - Date.parse(t.party.since)) / 30000) : t.party?.state === 'Paying' ? 1 : 0;
            for (const p of t.plates) { p.userData.crust.scale.setScalar(1 - eating * .7); p.userData.sauce.scale.setScalar(1 - eating * .75); p.userData.cheese.visible = eating < .5; }
        }
        this.tickRunners(dt, nowMs);
        this.tickVespas(dt);
        this.tickVan(dt);
        this.tickFlyers(dt);
        const f = 1 + Math.sin(nowMs * .011) * .12 + Math.sin(nowMs * .027) * .08;
        this.w.ovenLight.intensity = (night ? 9 : 5) * f;
        this.w.mouth.material.emissiveIntensity = 1.4 * f;
        this.w.phoneM.emissiveIntensity = this.w.capoPin.visible ? 1.2 + Math.sin(nowMs * .008) * .6 : 0;
    }

    tickRunners(dt, nowMs) {
        for (const r of this.runners) {
            if (r.state === 'idle' && this.jobs.length) {
                const first = this.jobs[0];
                r.load = this.jobs.filter((j) => j.tableId === first.tableId).slice(0, 3);
                this.jobs = this.jobs.filter((j) => !r.load.includes(j));
                r.state = 'toPass';
                r.target = new THREE.Vector3(.8, 0, first.from.z);
            }
            if (!r.target) continue;
            const d = r.target.clone().sub(r.obj.position);
            d.y = 0;
            const step = 4 * dt;
            if (d.length() > step) {
                r.obj.position.addScaledVector(d.normalize(), step);
                r.obj.rotation.y = Math.atan2(d.x, d.z);
                r.obj.position.y = Math.abs(Math.sin(nowMs * .012)) * .05;
                const fwd = new THREE.Vector3(Math.sin(r.obj.rotation.y), 0, Math.cos(r.obj.rotation.y));
                if (r.state === 'toTable') r.load.forEach((j, i) => j.mesh.position.copy(r.obj.position).add(new THREE.Vector3(0, 1.05 + i * .07, 0)).addScaledVector(fwd, .3));
                continue;
            }
            r.obj.position.copy(r.target);
            r.obj.position.y = 0;
            if (r.state === 'toPass') {
                const t = this.tables.get(r.load[0]?.tableId);
                if (!t) { this.drop(r); r.state = 'back'; r.target = r.home.clone(); continue; }
                const center = new THREE.Vector3(t.cx, 0, t.cz);
                const dir = r.obj.position.clone().sub(center).setY(0).normalize();
                r.state = 'toTable';
                r.target = center.addScaledVector(dir, Math.max(t.w, t.d) / 2 + .55);
            } else if (r.state === 'toTable') {
                r.delivered += r.load.length;
                this.drop(r);
                r.state = 'back';
                r.target = r.home.clone();
            } else {
                r.state = 'idle';
                r.target = null;
            }
        }
        // no runner on shift: food still has to arrive
        if (!this.runners.length) while (this.jobs.length) this.serve(this.jobs.shift());
    }

    serve(job) {
        const t = this.tables.get(job.tableId);
        if (!t || !t.party) { discard(job.mesh); return; }
        const k = t.plates.length;
        const [sx, sz] = t.seats[k % t.seats.length];
        dressPizza(job.mesh, 'table');
        job.mesh.position.set(t.cx + sx * .45, .83, t.cz + sz * .45);
        t.plates.push(job.mesh);
    }

    tickVespas(dt) {
        for (const v of this.w.vespas) {
            if (!v.busy) continue;
            v.t += dt;
            const o = v.obj;
            if (v.t < 1) { o.position.z = v.home.z + (11.2 - v.home.z) * v.t; o.rotation.y = -.3 + v.t * .3; }
            else if (v.t < 6) { o.position.z = 11.2; o.position.x = v.home.x + (v.t - 1) * 12; }
            else if (v.t < 10) { o.visible = false; }
            else if (v.t < 13) { o.visible = true; o.rotation.y = Math.PI; o.position.x = v.home.x + 36 - (v.t - 10) * 12; }
            else { o.position.copy(v.home); o.rotation.y = -.3; v.busy = false; o.userData.flag.visible = false; }
        }
    }

    tickVan(dt) {
        const v = this.vanTrip, van = this.w.van;
        if (v.state === 'driving') {
            v.x = Math.min(SPOTS.vanStop, v.x + 9 * dt);
            if (v.x >= SPOTS.vanStop) { v.state = 'unloading'; v.t = 0; this.crates(v.lines); }
        } else if (v.state === 'unloading' && (v.t += dt) > 3.2) {
            for (const ing of v.lines) { this.holds.delete(ing); }
            v.lines = [];
            v.state = 'leaving';
            if (this.snap) this.syncPantry(this.snap.stock, this.snap.office.purchaseOrders);
        } else if (v.state === 'leaving') {
            v.x += 9 * dt;
            if (v.x > 70) {
                v.x = -70;
                v.state = v.again ? 'driving' : 'home';
                v.again = false;
            }
        }
        van.position.x = v.x;
        this.w.vanPin.visible = v.state !== 'home';
        const spin = v.state === 'driving' || v.state === 'leaving' ? dt * 8 : 0;
        van.userData.wheels.forEach((wh) => { wh.rotation.y += spin; });
    }

    crates(lines) {
        lines.forEach((key, li) => {
            const bay = this.w.bays.get(key);
            const ing = INGREDIENTS.find((x) => x.key === key);
            for (let i = 0; i < 3; i++) {
                const c = box(.42, .3, .42, M(ing.color), .04);
                c.userData = { delay: li * .35 + i * .45, t: 0, from: new THREE.Vector3(SPOTS.vanStop + (i - 1) * .5, 1.1, 10.4), to: bay.group.position.clone().setY(.4), dur: 1.1, arc: 1.6 };
                c.position.copy(c.userData.from);
                c.visible = false;
                this.w.flyers.add(c);
            }
        });
    }

    tickFlyers(dt) {
        for (const c of [...this.w.flyers.children]) {
            const u = c.userData;
            u.t += dt;
            const k = (u.t - u.delay) / (u.dur || 1.1);
            if (k < 0) continue;
            c.visible = true;
            if (k >= 1) { discard(c); continue; }
            c.position.lerpVectors(u.from, u.to, k);
            c.position.y += Math.sin(k * Math.PI) * (u.arc || 1.6);
            if (u.spin) c.rotation.y += dt * 6;
        }
    }
}
