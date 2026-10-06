// The panels over the scene. Everything here reads the snapshot and the event tail; nothing
// here changes the house — decisions belong in Teams, Copilot and SharePoint, and the stage
// only shows their consequences.
import { INGREDIENTS, AGENTS } from './world.js';

const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const pct = (x) => `${Math.round(x * 100)}%`;
const eur = (x) => `€ ${Number(x).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const kg = (g) => `${(g / 1000).toFixed(1)} kg`;
const TIME = { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' };
const hhmm = (iso) => new Date(iso).toLocaleTimeString([], TIME);
const ingName = (key) => INGREDIENTS.find((i) => i.key === key)?.name ?? key;
const M365 = ['Copilot', 'Teams', 'SharePoint'];
const PARTY = { Seated: ['s-seated', 'Deciding'], WaitingForFood: ['s-waiting', 'Waiting for food'], Eating: ['s-eating', 'Eating'], Paying: ['s-paying', 'Paying'] };
const PIZZA = { OrderAccepted: 'Queued', Preparing: 'On the bench', Baking: 'In the oven', Ready: 'On the pass' };

export const ZONES = {
    all: { label: 'Whole house', cx: 12, w: 70, d: 16 },
    store: { label: 'Storeroom', cx: -17, w: 11, d: 15 },
    kitchen: { label: 'Kitchen', cx: -6, w: 13, d: 15 },
    sala: { label: 'Dining room', cx: 11, w: 23, d: 15 },
    break: { label: 'Break room', cx: 26, w: 9.5, d: 15 },
    office: { label: 'Office', cx: 34, w: 9.5, d: 15 },
    takeaway: { label: 'Takeaway', cx: 42, w: 9.5, d: 17 },
};

export class Hud {
    constructor(root, on) {
        this.root = root;
        this.on = on;
        this.feed = [];
        this.lastInspector = '';
        this.hold = false;
        this.bannerQueue = [];
        this.bannerTimer = null;
        root.classList.add('stage-root');
        root.innerHTML = `
          <header class="st-top">
            <div class="st-brand"><b>Trattoria Giuseppe</b><span>Live floor</span></div>
            <nav class="st-zones" aria-label="Zoom to area">${Object.entries(ZONES).map(([k, z]) =>
                `<button type="button" data-zone="${k}" aria-pressed="${k === 'all'}">${z.label}</button>`).join('')}</nav>
            <span class="st-spacer"></span>
            <nav class="st-nav" aria-label="Other views"><a href="/window">Window</a><a href="/engine-room">Engine Room</a><a href="/cinema">Cinema</a><a href="/storefront">Storefront</a></nav>
            <button type="button" class="st-pill toggle" data-director aria-pressed="false" title="Fly the camera to whatever Microsoft 365 just changed">Director</button>
            <span class="st-pill" data-service><span class="dot"></span><span data-service-text>…</span> · <span class="st-clock" data-clock>--:--</span></span>
            <button type="button" class="st-pill action" data-open hidden>Open service</button>
          </header>
          <div class="st-stage" data-stage>
            <section class="st-hud st-kpis" aria-label="Key figures">
              ${['oven:In the oven', 'tables:Tables taken', 'served:Guests served', 'ask:Needs a person', 'wait:Longest wait'].map((x) => {
                  const [k, l] = x.split(':');
                  return `<div class="st-kpi" data-kpi="${k}"><div class="lbl">${l}</div><div class="val">–</div><div class="bar"><i style="width:0%"></i></div></div>`;
              }).join('')}
            </section>
            <div class="st-hud st-mapctl" aria-label="Map controls">
              <button type="button" data-zoom="in" aria-label="Zoom in">+</button>
              <button type="button" data-zoom="out" aria-label="Zoom out">−</button>
              <button type="button" data-zoom="reset" aria-label="Reset view">⌂</button>
            </div>
            <div class="st-closed" data-closed hidden>Between services. The house is stocked, the books are closed.</div>
            <aside class="st-hud st-inspector" data-inspector aria-live="polite"></aside>
            <section class="st-hud st-tracker" aria-label="Tracking">
              <div class="st-tracker-head"><b data-tr-title>Order tracking</b><span data-tr-sub>—</span></div>
              <div class="st-steps" data-tr-steps></div>
            </section>
            <section class="st-hud st-feed" aria-label="What just happened">
              <div class="st-feed-head"><b>Kitchen chatter</b><span>agents · Copilot · Teams · SharePoint</span></div>
              <ol data-feed></ol>
            </section>
            <div class="st-banner" data-banner role="status"><div class="badge" data-banner-badge></div><div><div class="who" data-banner-who></div><div class="what" data-banner-what></div></div></div>
          </div>`;
        this.$ = (sel) => root.querySelector(sel);
        this.stage = this.$('[data-stage]');
        root.querySelectorAll('[data-zone]').forEach((b) => b.addEventListener('click', () => on.zone(b.dataset.zone)));
        root.querySelectorAll('[data-zoom]').forEach((b) => b.addEventListener('click', () => on.zoom(b.dataset.zoom)));
        this.$('[data-director]').addEventListener('click', () => on.director(!this.director));
        this.$('[data-open]').addEventListener('click', () => on.openService());
        const insp = this.$('[data-inspector]');
        insp.addEventListener('pointerdown', () => { this.hold = true; });
        this.release = () => setTimeout(() => { this.hold = false; }, 60);
        window.addEventListener('pointerup', this.release);
    }

    dispose() {
        window.removeEventListener('pointerup', this.release);
        clearTimeout(this.bannerTimer);
    }

    setZone(key) {
        this.root.querySelectorAll('[data-zone]').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.zone === key)));
    }

    setDirector(on) {
        this.director = on;
        this.$('[data-director]').setAttribute('aria-pressed', String(on));
    }

    /* ─────────────────────────── top + KPIs ─────────────────────────── */

    renderTop(snap, live) {
        const open = snap.service.open;
        const pill = this.$('[data-service]');
        pill.classList.toggle('closed', !open);
        this.$('[data-service-text]').textContent = open ? `Service open${snap.service.minutesLeft != null ? ` · ${Math.ceil(snap.service.minutesLeft)} min left` : ''}` : 'Between services';
        this.$('[data-clock]').textContent = new Date(live.now()).toLocaleTimeString([], TIME);
        this.$('[data-open]').hidden = open;
        this.$('[data-closed]').hidden = open;
    }

    kpi(key, value, sub, fill, cls) {
        const el = this.$(`[data-kpi="${key}"]`);
        el.querySelector('.val').innerHTML = `${value}${sub ? `<small> ${sub}</small>` : ''}`;
        el.querySelector('.bar i').style.width = pct(Math.max(0, Math.min(1, fill)));
        el.classList.toggle('warn', cls === 'warn');
        el.classList.toggle('ask', cls === 'ask');
    }

    renderKpis(snap, live) {
        const k = snap.kitchen;
        const baking = k.pizzas.filter((p) => p.state === 'Baking').length;
        const taken = snap.dining.tables.filter((t) => t.party).length;
        const pend = live.pending().length;
        const now = live.now();
        const waits = snap.dining.tables.filter((t) => t.party?.state === 'WaitingForFood' && t.party.orderedAt).map((t) => (now - Date.parse(t.party.orderedAt)) / 60000);
        const longest = Math.floor(Math.max(0, ...waits));
        this.kpi('oven', baking, `/ ${k.ovenCapacity}`, baking / k.ovenCapacity);
        this.kpi('tables', taken, `/ ${snap.dining.tables.length}`, taken / snap.dining.tables.length);
        this.kpi('served', snap.dining.guestsServed, '', Math.min(1, snap.dining.guestsServed / 120));
        this.kpi('ask', pend, '', Math.min(1, pend / 4), pend ? 'ask' : null);
        this.kpi('wait', longest, 'min', Math.min(1, longest / 10), longest >= 5 ? 'warn' : null);
    }

    /* ─────────────────────────── feed + banner ─────────────────────────── */

    addEvents(events) {
        for (const e of events) {
            if (this.feed.some((f) => f.seq === e.seq)) continue;
            this.feed.unshift(e);
        }
        this.feed.sort((a, b) => b.seq - a.seq);
        this.feed.length = Math.min(this.feed.length, 60);
        this.$('[data-feed]').innerHTML = this.feed.map((e) => {
            const tag = M365.includes(e.channel) ? 'm365' : e.channel === 'Presenter' ? 'presenter' : e.channel === 'Web' ? 'web' : '';
            const label = e.channel === 'Autonomous' ? 'agents' : e.channel;
            return `<li data-seq="${e.seq}"><time>${hhmm(e.at)}</time><span class="st-tag ${tag}">${esc(label)}</span><p><b>${esc(e.actor)}:</b> ${esc(e.text)}</p></li>`;
        }).join('');
    }

    banner(e) {
        this.bannerQueue.push(e);
        if (this.bannerQueue.length > 3) this.bannerQueue.shift();
        if (!this.bannerTimer) this.nextBanner();
    }

    nextBanner() {
        const el = this.$('[data-banner]');
        const e = this.bannerQueue.shift();
        if (!e) { el.classList.remove('show'); this.bannerTimer = null; return; }
        const presenter = e.channel === 'Presenter';
        el.classList.toggle('presenter', presenter);
        this.$('[data-banner-badge]').textContent = presenter ? 'Stage' : e.channel === 'SharePoint' ? 'SP' : e.channel;
        this.$('[data-banner-who]').textContent = presenter ? `${e.actor} · presenter` : `${e.actor} · via ${e.channel}`;
        this.$('[data-banner-what]').textContent = e.text;
        el.dataset.seq = e.seq;
        el.classList.add('show');
        this.bannerTimer = setTimeout(() => { el.classList.remove('show'); this.bannerTimer = setTimeout(() => this.nextBanner(), 400); }, 6000);
    }

    /* ─────────────────────────── inspector ─────────────────────────── */

    renderInspector(sel, snap, live, force = false) {
        const h = this.inspect(sel, snap, live);
        if (force || (h !== this.lastInspector && !this.hold)) {
            this.$('[data-inspector]').innerHTML = h;
            this.lastInspector = h;
        }
    }

    inspect(sel, snap, live) {
        const k = snap.kitchen, now = live.now();
        const mins = (iso) => Math.max(0, Math.floor((now - Date.parse(iso)) / 60000));
        const progress = (p) => {
            const total = p.state === 'Preparing' ? p.prepSeconds : p.state === 'Baking' ? p.bakeSeconds : 0;
            if (!total || !p.startedAt) return p.state === 'Ready' ? 1 : 0;
            return Math.min(1, (now - Date.parse(p.startedAt)) / (total * 1000));
        };
        const pizzaRows = (list) => list.length ? list.map((p) => {
            const g = progress(p);
            return `<div class="st-row"><span>${esc(p.name)}${p.tableId != null ? ` <span class="st-empty">· table ${p.tableId}</span>` : ' <span class="st-empty">· online</span>'}</span>
              <span class="st-state ${p.state === 'Ready' ? 's-ok' : p.state === 'OrderAccepted' ? 's-free' : 's-seated'}">${PIZZA[p.state]}</span>
              <span class="meter ${g >= 1 ? 'done' : ''}"><i style="width:${pct(g)}"></i></span></div>`;
        }).join('') : '<p class="st-empty">Nothing here right now.</p>';
        const recent = (actors) => {
            const list = this.feed.filter((e) => actors.includes(e.actor)).slice(0, 4);
            return list.length ? list.map((e) => `<div class="st-row"><span>${esc(e.text)}</span><span class="st-tag ${M365.includes(e.channel) ? 'm365' : ''}">${esc(e.channel === 'Autonomous' ? hhmm(e.at) : e.channel)}</span></div>`).join('')
                : '<p class="st-empty">Nothing reported yet this session.</p>';
        };

        switch (sel.kind) {
            case 'table': {
                const t = snap.dining.tables.find((x) => x.id === sel.id), p = t?.party;
                if (!t) return '';
                let h = `<div class="st-kicker"><span>Dining room · table</span><span>${t.seats} seats</span></div><div class="st-title">Table ${t.id}</div>`;
                if (!p) return h + `<div class="st-sub">${esc(t.shape)} table, free</div><span class="st-state s-free">Free</span>`;
                const [cls, txt] = PARTY[p.state] ?? ['s-free', p.state];
                const line = k.pizzas.filter((x) => x.tableId === t.id);
                const served = live.tables.get(t.id)?.plates.length ?? 0;
                return h + `<div class="st-sub">${esc(p.name)}, party of ${p.size}</div><span class="st-state ${cls}">${txt}</span>
                  <dl class="st-kv"><dt>In this state</dt><dd>${mins(p.since)} min</dd><dt>Ordered</dt><dd>${p.orderedAt ? hhmm(p.orderedAt) : 'not yet'}</dd><dt>Served so far</dt><dd>${served}</dd></dl>
                  ${p.wish ? `<div class="st-callout"><b>Wish:</b> ${esc(p.wish)}</div>` : ''}
                  <div class="st-sect">On the line for this table</div>${pizzaRows(line)}`;
            }
            case 'oven': case 'bench': case 'pass': {
                const [state, cap, title, sub] = {
                    oven: ['Baking', k.ovenCapacity, 'Forno a legna', 'Wood-fired oven'],
                    bench: ['Preparing', k.prepCapacity, 'Prep bench', 'Toppings go on here'],
                    pass: ['Ready', 8, 'The pass', 'Heat lamps; runners pick up here'],
                }[sel.kind];
                const list = k.pizzas.filter((p) => p.state === state);
                const queued = k.pizzas.filter((p) => p.state === 'OrderAccepted');
                return `<div class="st-kicker"><span>Kitchen · station</span><span>${list.length} / ${cap}</span></div><div class="st-title">${title}</div><div class="st-sub">${sub}</div>
                  <span class="st-state ${list.length >= cap ? 's-waiting' : 's-ok'}">${list.length >= cap ? 'At capacity' : 'Has room'}</span>
                  <div class="st-sect">Pizzas</div>${pizzaRows(list)}
                  ${sel.kind === 'bench' && queued.length ? `<div class="st-sect">Queued on the rail</div>${pizzaRows(queued.slice(0, 8))}` : ''}`;
            }
            case 'fridge':
                return `<div class="st-kicker"><span>Kitchen · station</span><span>${k.doughs.filter((d) => d.state === 'Ready').length} ready</span></div><div class="st-title">Dough fridge</div>
                  <div class="st-sub">${k.fridgeCapacity} resting trays</div>
                  ${k.doughs.length ? k.doughs.map((d, i) => `<div class="st-row"><span>Tray ${i + 1}</span><span class="st-state ${d.state === 'Ready' ? 's-ok' : 's-seated'}">${d.state}</span></div>`).join('') : '<p class="st-empty">Empty. The Dough Master portions more when service is open.</p>'}`;
            case 'stock': {
                const s = snap.stock.find((x) => x.ingredient === sel.id);
                const pos = snap.office.purchaseOrders.filter((p) => p.ingredient === sel.id).slice(0, 3);
                if (!s) return '';
                const low = s.level < .25;
                return `<div class="st-kicker"><span>Storeroom · ingredient</span><span>${esc(s.ingredient)}</span></div><div class="st-title">${esc(ingName(s.ingredient))}</div>
                  <div class="st-sub">${kg(s.grams)} of ${kg(s.opening)} opening stock</div>
                  <span class="st-state ${low ? 's-low' : 's-ok'}">${low ? 'Running low' : 'In stock'}</span>
                  <div class="st-row" style="margin-top:10px"><span>Level</span><span>${pct(s.level)}</span><span class="meter ${low ? 'low' : ''}"><i style="width:${pct(s.level)}"></i></span></div>
                  <div class="st-sect">Purchase orders</div>${pos.length ? pos.map((p) => `<div class="st-item"><div class="st-item-head"><b>${esc(p.id)}</b><span>${esc(p.state)}</span></div><p>${esc(p.why)}</p></div>`).join('') : '<p class="st-empty">None this session.</p>'}`;
            }
            case 'agent': {
                const a = AGENTS[sel.id];
                const actors = { giuseppe: ['Giuseppe'], nonna: ['Nonna'], procurement: ['Procurement', 'Supplier'] }[sel.id] ?? [];
                return `<div class="st-kicker"><span>Agent</span><span>${sel.id === 'nonna' || sel.id === 'giuseppe' ? 'Microsoft 365 + factory' : 'factory floor'}</span></div>
                  <div class="st-title">${a.name}</div><div class="st-sub">${esc(a.role)}</div>
                  <div class="st-sect">Recently</div>${actors.length ? recent(actors) : '<p class="st-empty">Works the line silently; watch the bench and the oven.</p>'}`;
            }
            case 'staff': {
                const m = snap.staff.find((x) => x.name === sel.id);
                const runner = live.runners.find((r) => r.name === sel.id);
                const shift = live.shift();
                const on = shift.service.includes(sel.id) || shift.others.some((o) => o.name === sel.id);
                if (!m) return `<div class="st-title">${esc(sel.id)}</div><p class="st-empty">Standing in until the rota names someone.</p>`;
                return `<div class="st-kicker"><span>Brigade · ${esc(m.role)}</span><span>human</span></div><div class="st-title">${esc(m.name)}</div><div class="st-sub">${esc(m.quirk)}</div>
                  <span class="st-state ${m.absentToday ? 's-low' : on ? 's-eating' : 's-free'}">${m.absentToday ? 'Out today' : on ? `On the ${shift.slot.toLowerCase()} shift` : 'Off this shift'}</span>
                  <dl class="st-kv"><dt>Oven-certified</dt><dd>${m.ovenCertified ? 'yes' : 'no'}</dd>${runner ? `<dt>Plates carried</dt><dd>${runner.delivered}</dd>` : ''}</dl>`;
            }
            case 'approvals': case 'capo': {
                const pend = live.pending();
                const head = sel.kind === 'capo'
                    ? `<div class="st-kicker"><span>Office · human in the loop</span><span>Teams</span></div><div class="st-title">The Capo</div><div class="st-sub">Signs what Nonna may not decide alone</div>`
                    : `<div class="st-kicker"><span>Office · Nonna’s in-tray</span><span>${pend.length} open</span></div><div class="st-title">Waiting for a person</div><div class="st-sub">Over Nonna’s limit, or over budget</div>`;
                return head + `<span class="st-state ${pend.length ? 's-seated' : 's-ok'}">${pend.length ? `${pend.length} waiting` : 'Desk is clear'}</span>
                  ${pend.map((x) => x.type === 'po'
                      ? `<div class="st-item"><div class="st-item-head"><b>${esc(x.id)} · ${esc(ingName(x.ingredient))}</b><span>${eur(x.cost)}</span></div><p>${esc(x.why)}</p></div>`
                      : `<div class="st-item"><div class="st-item-head"><b>${esc(x.id)} · ${esc(x.name)} wants time off</b><span>${esc(x.day)}</span></div><p>${esc(x.reason)}. ${x.cover.length ? `Cover: ${esc(x.cover.join(' or '))}.` : 'Nobody qualified is free.'}</p></div>`).join('')}
                  <div class="st-callout">Sign these in <b>Teams</b>, <b>Copilot</b> or the <b>SharePoint</b> desk. The stage only watches, and shows who did it.</div>`;
            }
            case 'budget': {
                const b = snap.office.budget;
                return `<div class="st-kicker"><span>Office · budget</span><span>${esc(b.period)}</span></div><div class="st-title">${eur(b.remainingEur)} left</div>
                  <div class="st-sub">${eur(b.committedEur)} committed of ${eur(b.budgetEur)}</div>
                  <span class="st-state ${b.isTight ? 's-low' : 's-ok'}">${b.isTight ? 'Tight: Nonna starts warning' : 'Comfortable'}</span>
                  <div class="st-row" style="margin-top:10px"><span>Used</span><span>${Math.round(b.usedPercent)}%</span><span class="meter ${b.isTight ? 'low' : ''}"><i style="width:${Math.min(100, b.usedPercent)}%"></i></span></div>
                  <dl class="st-kv"><dt>Nonna may approve alone</dt><dd>${kg(snap.office.autoApproveLimitGrams)}</dd></dl>
                  <div class="st-sect">Latest orders</div>${snap.office.purchaseOrders.slice(0, 5).map((p) => `<div class="st-row"><span>${esc(p.id)} · ${esc(ingName(p.ingredient))}</span><span>${eur(p.cost)}</span></div>`).join('') || '<p class="st-empty">None yet.</p>'}`;
            }
            case 'invoices':
                return `<div class="st-kicker"><span>Office · filing cabinet</span><span>${snap.office.invoices.length} filed</span></div><div class="st-title">Invoices</div><div class="st-sub">Each delivery’s paper, matched to its order</div>
                  ${snap.office.invoices.map((i) => `<div class="st-item"><div class="st-item-head"><b>${esc(i.id)}</b><span>${eur(i.cost)}</span></div><p>${esc(ingName(i.ingredient))}, ${kg(i.grams)} · ${esc(i.supplier)} · ${hhmm(i.at)}</p></div>`).join('') || '<p class="st-empty">Nothing filed yet.</p>'}`;
            case 'board': {
                const days = [...new Set(snap.office.rota.map((r) => r.day))];
                const cell = (d, s, role) => {
                    const names = snap.office.rota.filter((r) => r.day === d && r.slot === s && r.role === role).map((r) => r.assignedTo);
                    if (!names.length) return '<td>–</td>';
                    return `<td class="${names.includes(null) ? 'gap' : ''}">${names.map((n) => esc(n ?? 'open')).join(', ')}</td>`;
                };
                return `<div class="st-kicker"><span>Break room · bacheca</span><span>rota</span></div><div class="st-title">Who works when</div><div class="st-sub">Pinned by Nonna, read over espresso</div>
                  <div style="overflow-x:auto"><table class="st-rota"><tr><th>Shift</th><th>Pizzaiolo</th><th>Service</th><th>Courier</th></tr>
                  ${days.flatMap((d) => ['Lunch', 'Dinner'].map((s) => `<tr><td>${esc(d.split(' ')[0])} ${s.toLowerCase()}</td>${cell(d, s, 'Pizzaiolo')}${cell(d, s, 'Service')}${cell(d, s, 'Courier')}</tr>`)).join('')}</table></div>
                  <div class="st-sect">Time-off requests</div>${snap.office.timeOff.map((t) => `<div class="st-row"><span>${esc(t.name)} · ${esc(t.day)}${t.slot ? ' ' + esc(t.slot.toLowerCase()) : ''}</span><span class="st-state ${t.state === 'Pending' ? 's-seated' : t.state === 'Approved' ? 's-ok' : 's-low'}">${esc(t.state)}</span></div>`).join('') || '<p class="st-empty">No requests pinned.</p>'}`;
            }
            case 'lockers': {
                const shift = live.shift();
                return `<div class="st-kicker"><span>Break room · lockers</span><span>${snap.staff.length} people</span></div><div class="st-title">The brigade</div><div class="st-sub">One locker each</div>
                  ${snap.staff.map((m) => {
                      const on = shift.service.includes(m.name) || shift.others.some((o) => o.name === m.name);
                      return `<div class="st-row"><span><b>${esc(m.name)}</b> <span class="st-empty">· ${esc(m.role)}${m.ovenCertified ? ' · oven' : ''}</span></span><span class="st-state ${m.absentToday ? 's-low' : on ? 's-eating' : 's-free'}">${m.absentToday ? 'Out today' : on ? 'On shift' : 'Off'}</span></div>`;
                  }).join('')}`;
            }
            case 'takeaway': {
                const cooking = snap.tickets.filter((t) => t.state === 'Cooking');
                const done = snap.tickets.filter((t) => t.state === 'Done').slice(0, 4);
                const row = (t) => `<div class="st-row"><span>${esc(t.amount)}× ${esc(t.pizza)}${t.guest ? ` for <b>${esc(t.guest)}</b>` : ''} <span class="st-empty">· ${t.mode === 'Delivery' ? 'delivery' : 'takeaway'}</span></span>
                    <span class="st-tag ${t.surface ? 'm365' : ''}">${t.surface ? 'via ' + esc(t.surface) : 'online'}</span></div>`;
                return `<div class="st-kicker"><span>Asporto · takeaway & delivery</span><span>${cooking.length} cooking</span></div>
                  <div class="st-title">The pick-up counter</div><div class="st-sub">Orders from the web, Copilot and Teams: boxed here, handed to the couriers</div>
                  <div class="st-sect">On the board</div>${cooking.length ? cooking.map(row).join('') : '<p class="st-empty">Nothing cooking for the counter.</p>'}
                  <div class="st-sect">Just left</div>${done.length ? done.map(row).join('') : '<p class="st-empty">No deliveries yet this service.</p>'}
                  <div class="st-callout">Orders placed through <b>Copilot</b> or <b>Teams</b> are marked purple: on the board, under the pizza on the line, and on the courier's Vespa.</div>`;
            }
            case 'espresso':
                return `<div class="st-kicker"><span>Break room</span><span>the real engine</span></div><div class="st-title">La macchina</div><div class="st-sub">Lever espresso machine, older than the restaurant</div>
                  <div class="st-callout">Not connected to anything. Some things in a pizzeria should stay analogue.</div>`;
            case 'van': {
                const v = live.vanTrip;
                return `<div class="st-kicker"><span>External · supplier agent</span><span>A2A</span></div><div class="st-title">Fruttivendolo Marittimo</div>
                  <div class="st-sub">A separate company’s agent. We only talk to it over A2A.</div>
                  <span class="st-state ${v.state === 'home' ? 's-free' : 's-paying'}">${{ home: 'At the depot', driving: 'On the road', unloading: 'Unloading', leaving: 'Heading back' }[v.state]}</span>
                  <div class="st-sect">On board</div>${v.lines.length ? v.lines.map((l) => `<div class="st-row"><span>${esc(ingName(l))}</span></div>`).join('') : '<p class="st-empty">Nothing on board.</p>'}`;
            }
            default:
                return '';
        }
    }

    /* ─────────────────────────── tracker ─────────────────────────── */

    renderTracker(sel, snap, live) {
        const steps = (names, stage, notes = []) => names.map((n, i) =>
            `<div class="st-step ${i < stage ? 'done' : i === stage ? 'active' : ''}"><span class="node"></span><span>${n}</span><span class="n">${esc(notes[i] ?? (i < stage ? '✓' : '—'))}</span></div>`).join('');
        const set = (title, sub, html) => {
            this.$('[data-tr-title]').textContent = title;
            this.$('[data-tr-sub]').textContent = sub;
            this.$('[data-tr-steps]').innerHTML = html;
        };
        const office = snap.office;
        const officeKinds = ['approvals', 'capo', 'budget', 'invoices'];
        let po = null;
        if (sel.kind === 'stock') po = office.purchaseOrders.find((p) => p.ingredient === sel.id);
        else if (officeKinds.includes(sel.kind) || (sel.kind === 'agent' && ['nonna', 'procurement'].includes(sel.id)) || sel.kind === 'van') {
            po = office.purchaseOrders.find((p) => ['PendingApproval', 'BlockedByBudget'].includes(p.state)) ?? office.purchaseOrders[0];
        }
        if (po) {
            const stage = { PendingApproval: 2, BlockedByBudget: 2, Rejected: 2, Approved: 3, Delivered: 5 }[po.state] ?? 1;
            const note = { PendingApproval: 'needs a signature', BlockedByBudget: 'over budget', Rejected: 'rejected', Approved: 'signed' }[po.state];
            set('Purchase order', `${po.id} · ${kg(po.grams)} ${ingName(po.ingredient).toLowerCase()} · ${eur(po.cost)}`,
                steps(['Requested', 'Nonna checked', 'Approved', 'Delivered', 'Invoiced'], stage, [hhmm(po.at), po.decision === 'AutoApproved' ? 'under limit' : 'over limit', note]));
            return;
        }
        if (sel.kind === 'takeaway') {
            const t = snap.tickets.find((x) => x.surface && x.state === 'Cooking') ?? snap.tickets.find((x) => x.surface) ?? snap.tickets[0];
            if (t) {
                const line = snap.kitchen.pizzas.filter((p) => p.orderId === t.orderId);
                const stage = t.state === 'Done' ? 4 : line.some((p) => p.state === 'Ready') ? 2 : line.some((p) => p.state === 'Preparing' || p.state === 'Baking') ? 1 : 0;
                set('Takeaway order', `${t.amount}× ${t.pizza}${t.guest ? ' for ' + t.guest : ''} · ${t.surface ? 'via ' + t.surface : 'online'}`,
                    steps(['Ordered', 'In the kitchen', 'Boxed', t.mode === 'Delivery' ? 'With the courier' : 'Picked up'], stage, [hhmm(t.placedAt)]));
                return;
            }
        }
        if (['board', 'lockers', 'staff', 'approvals', 'capo'].includes(sel.kind) && office.timeOff.length) {
            const t = office.timeOff.find((x) => x.state === 'Pending') ?? office.timeOff[0];
            const stage = t.state === 'Pending' ? 2 : t.state === 'Approved' ? 4 : 2;
            set('Time off', `${t.id} · ${t.name} · ${t.day}`, steps(['Filed', 'Cover found', 'Approval', 'Rota updated'], stage,
                [hhmm(t.at), t.cover[0] ?? 'nobody', t.state === 'Pending' ? 'needs a signature' : t.state === 'Declined' ? 'declined' : undefined]));
            return;
        }
        let table = sel.kind === 'table' ? snap.dining.tables.find((x) => x.id === sel.id) : null;
        if (!table?.party?.orderId) {
            table = snap.dining.tables.filter((x) => x.party?.state === 'WaitingForFood' && x.party.orderedAt)
                .sort((a, b) => Date.parse(b.party.orderedAt) - Date.parse(a.party.orderedAt))[0] ?? table;
        }
        const names = ['Ordered', 'On the bench', 'In the oven', 'On the pass', 'Served'];
        if (!table?.party?.orderId) { set('Order tracking', 'No open order selected', steps(names, -1)); return; }
        const rig = live.tables.get(table.id);
        const line = snap.kitchen.pizzas.filter((p) => p.tableId === table.id);
        const served = rig?.plates.length ?? 0;
        const total = Math.max(line.length + served, 1);
        const rank = { OrderAccepted: 0, Preparing: 1, Baking: 2, Ready: 3 };
        const reached = (i) => line.filter((p) => rank[p.state] >= i).length + served;
        set('Order tracking', `Table ${table.id} · ${table.party.name}`, names.map((n, i) => {
            const r = Math.min(total, reached(i));
            return `<div class="st-step ${r === total ? 'done' : r > 0 ? 'active' : ''}"><span class="node"></span><span>${n}</span><span class="n">${r}/${total}</span></div>`;
        }).join(''));
    }
}
