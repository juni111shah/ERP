// HigherSquare 360 ad stage. Everything on screen is a pure function of time: setup(config) builds the scenes once, then
// render(t) poses every element for second t and resolves when the footage frames it shows are decoded. ads.mjs captures
// one screenshot per frame, so the animation is exact and smooth whatever the machine speed.
(() => {
  const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
  const seg = (t, a, b) => clamp((t - a) / (b - a));
  const lerp = (a, b, x) => a + (b - a) * x;
  const eOut = (x) => 1 - Math.pow(1 - x, 3);
  const eIn = (x) => x * x * x;
  const eInOut = (x) => (x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2);
  const eBack = (x) => { const c1 = 1.9, c3 = c1 + 1; return x <= 0 ? 0 : x >= 1 ? 1 : 1 + c3 * Math.pow(x - 1, 3) + c1 * Math.pow(x - 1, 2); };
  const rnd = (i) => { const x = Math.sin(i * 127.1 + 311.7) * 43758.5453; return x - Math.floor(x); };
  const T = 0.55;   // transition length

  let W, H, U, A, cfg, root, scenes = [], corner, sweep, flashw, events = [];
  const pending = [];
  const el = (tag, cls, parent, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; (parent || root).appendChild(e); return e; };
  const css = (e, o) => { for (const k in o) e.style[k] = typeof o[k] === 'number' && !/opacity|zIndex|lineHeight|fontWeight|flex/.test(k) ? o[k] + 'px' : o[k]; };
  const pick = (v, s, l) => (A === 'v' ? v : A === 's' ? s : l);
  const ev = (t, kind) => events.push({ t: +t.toFixed(3), kind });

  // ---------------------------------------------------------------------------------------------- the brand mark (vector)
  const MARK = `<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg" style="width:100%;height:100%;overflow:visible">
    <defs><linearGradient id="mg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#4FA077"/><stop offset="1" stop-color="#3A8A62"/></linearGradient>
    <clipPath id="mc"><circle cx="50" cy="50" r="35"/></clipPath></defs>
    <g class="m-tile"><rect x="0" y="0" width="100" height="100" rx="22" fill="url(#mg)"/></g>
    <g class="m-disc"><circle cx="50" cy="50" r="45" fill="#FFFFFF"/></g>
    <path class="m-a1" pathLength="1" d="M39.91 87.67 A39 39 0 1 1 67.10 14.95" fill="none" stroke="#4FA077" stroke-width="5" stroke-linecap="round"/>
    <path class="m-a2" pathLength="1" d="M84.43 31.69 A39 39 0 0 1 56.77 88.41" fill="none" stroke="#4FA077" stroke-width="5" stroke-linecap="round"/>
    <g clip-path="url(#mc)">
      <path class="m-b" d="M24 90 V64.5 Q24 63 25.5 63 H31.5 Q33 63 33 64.5 V90 Z" fill="#2A8A93"/>
      <path class="m-b" d="M37 90 V56.5 Q37 55 38.5 55 H44.5 Q46 55 46 56.5 V90 Z" fill="#2A8A93"/>
      <path class="m-b" d="M50 90 V47.5 Q50 46 51.5 46 H57.5 Q59 46 59 47.5 V90 Z" fill="#4FA077"/>
      <path class="m-b" d="M63 90 V36.5 Q63 35 64.5 35 H70.5 Q72 35 72 36.5 V90 Z" fill="#1F3A44"/>
    </g>
    <polyline class="m-line" pathLength="1" points="22,56 36,44 45,49 58,38 74.19,24.24" fill="none" stroke="#4FA077" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round"/>
    <circle class="m-d" cx="22" cy="56" r="2.9" fill="#4FA077"/><circle class="m-d" cx="36" cy="44" r="2.9" fill="#4FA077"/>
    <circle class="m-d" cx="45" cy="49" r="2.9" fill="#4FA077"/><circle class="m-d" cx="58" cy="38" r="2.9" fill="#4FA077"/>
    <polygon class="m-arrow" points="78.00,21.00 74.88,29.69 68.93,22.68" fill="#4FA077" stroke="#4FA077" stroke-width="1" stroke-linejoin="round"/>
  </svg>`;
  const MARK_URL = 'data:image/svg+xml;base64,' + btoa(MARK.replace(/class="[^"]*"/g, '').replace('style="width:100%;height:100%;overflow:visible"', ''));

  /** Poses the vector mark for local time lt (seconds since the build started). k scales the build speed. */
  function poseMark(svg, lt, k = 1) {
    const s = (a, b) => seg(lt, a * k, b * k);
    const tile = svg.querySelector('.m-tile'), disc = svg.querySelector('.m-disc');
    const pt = eBack(s(0, 0.5));
    tile.setAttribute('transform', `translate(50 50) rotate(${(1 - pt) * -14}) scale(${pt}) translate(-50 -50)`);
    const pd = eBack(s(0.12, 0.5));
    disc.setAttribute('transform', `translate(50 50) scale(${pd}) translate(-50 -50)`);
    const pr = eInOut(s(0.25, 0.95));
    for (const a of svg.querySelectorAll('.m-a1, .m-a2')) { a.style.strokeDasharray = '1 1'; a.style.strokeDashoffset = 1 - pr; a.style.opacity = pr > 0 ? 1 : 0; }
    svg.querySelectorAll('.m-b').forEach((b, i) => {
      const p = eBack(s(0.5 + i * 0.08, 0.85 + i * 0.08));
      b.setAttribute('transform', `translate(0 90) scale(1 ${p}) translate(0 -90)`);
    });
    const pl = eInOut(s(0.85, 1.3));
    const line = svg.querySelector('.m-line'); line.style.strokeDasharray = '1 1'; line.style.strokeDashoffset = 1 - pl; line.style.opacity = pl > 0 ? 1 : 0;
    svg.querySelectorAll('.m-d').forEach((d, i) => {
      const p = eBack(s(0.85 + i * 0.1, 1.1 + i * 0.1));
      const cx = d.getAttribute('cx'), cy = d.getAttribute('cy');
      d.setAttribute('transform', `translate(${cx} ${cy}) scale(${p}) translate(${-cx} ${-cy})`);
    });
    const pa = eBack(s(1.25, 1.55));
    svg.querySelector('.m-arrow').setAttribute('transform', `translate(78 21) scale(${pa}) translate(-78 -21)`);
  }

  // ---------------------------------------------------------------------------------------------- text helpers
  /** "Invoices in *seconds*" -> word spans; starred words are highlighted. */
  function words(parent, text, cls = '') {
    const out = [];
    const parts = text.split(/(\*[^*]+\*)/).filter(Boolean);
    for (const part of parts) {
      const hl = part.startsWith('*');
      const t = hl ? part.slice(1, -1) : part;
      for (const w of t.split(/(\s+)/)) {
        if (!w) continue;
        if (/^\s+$/.test(w)) { parent.appendChild(document.createTextNode(' ')); continue; }
        const span = el('span', 'w' + (hl ? ' ' + (cls || 'hl') : ''), parent, w);
        out.push(span);
      }
    }
    return out;
  }
  function poseWords(ws, lt, start, stagger = 0.065, dur = 0.55) {
    ws.forEach((w, i) => {
      const a = start + i * stagger;
      const p = eOut(seg(lt, a, a + dur));
      w.style.transform = `translateY(${(1 - p) * 0.55}em) scale(${lerp(0.92, 1, p)})`;
      w.style.opacity = p;
      w.style.filter = p < 1 ? `blur(${(1 - p) * 10}px)` : 'none';
      if (w.classList.contains('hl')) w.style.backgroundSize = `${eOut(seg(lt, a + 0.3, a + 0.75)) * 100}% 36%`;
    });
  }

  // ---------------------------------------------------------------------------------------------- footage
  /** A footage <img> inside a box; shows frame for clip time ct at camera [cx, cy, z] (z = zoom of a 16:9 or native box). */
  function footage(box, clip) {
    const img = el('img', 'f', box);
    img.dataset.clip = clip;
    return img;
  }
  function poseFootage(img, ct, cam, bw, bh) {
    const c = cfg.clips[img.dataset.clip];
    const idx = clamp(Math.floor(ct * 30), 0, c.frames - 1) + 1;
    const src = `${c.url}/f${String(idx).padStart(4, '0')}.jpg`;
    if (img.dataset.src !== src) { img.dataset.src = src; img.src = src; pending.push(img.decode().catch(() => {})); }
    const [cx, cy, z] = cam;
    // Cover the box with the frame at zoom z, centred on (cx, cy), never showing past the frame's edges.
    const base = Math.max(bw / c.w, bh / c.h);
    const iw = c.w * base * z, ih = c.h * base * z;
    const x = clamp(bw / 2 - cx * iw, bw - iw, 0), y = clamp(bh / 2 - cy * ih, bh - ih, 0);
    img.style.width = iw + 'px'; img.style.height = ih + 'px';
    img.style.transform = `translate(${x}px, ${y}px)`;
  }

  function browser(parent, w, aspect = 16 / 9) {
    const d = el('div', 'device', parent);
    const bar = el('div', 'bar', d, `<i style="background:#FF5F57"></i><i style="background:#FEBC2E"></i><i style="background:#28C840"></i><div class="url"><img src="${MARK_URL}">HigherSquare 360</div>`);
    const sh = w / aspect;
    const scr = el('div', 'screen', d);
    css(d, { width: w, height: sh + bar.offsetHeight || sh + U * 4.2 });
    css(scr, { width: w, height: sh });
    return { d, scr, w, h: sh, barH: U * 4.2 };
  }

  // ---------------------------------------------------------------------------------------------- scene builders
  const builders = {
    hook(s, box) {
      const sheet = el('div', 'sheet', box);
      const cw = U * 16, ch = U * 5.2, cols = 9, rows = 22;
      const junk = ['=SUM(B2:B9)', '#REF!', '1,250.00', '??', '=VLOOKUP(', '#N/A', 'Q3 final v7', 'TOTAL', '3,420', '=B4*1.05', 'stock?', '#DIV/0!', 'invoice_old', '12/11', 'CHECK'];
      s.cells = [];
      for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
        const i = r * cols + c;
        const cell = el('div', 'cell', sheet, rnd(i) < 0.33 ? junk[Math.floor(rnd(i + 99) * junk.length)] : '');
        css(cell, { left: (c - cols / 2) * cw, top: (r - rows / 2) * ch, width: cw, height: ch });
        if (/#|\?\?/.test(cell.textContent)) cell.style.color = 'rgba(255,138,128,.55)';
        s.cells.push({ cell, i, x: (c - cols / 2 + 0.5) * cw, y: (r - rows / 2 + 0.5) * ch });
      }
      s.sheet = sheet;
      const txt = el('div', 'title', box);
      css(txt, { left: 0, right: 0, top: 0, bottom: 0, display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center', textAlign: 'center', fontSize: U * pick(10.5, 9.4, 8.4), lineHeight: 1.08, position: 'absolute' });
      s.ws = [];
      for (const line of s.lines) { const l = el('div', '', txt); s.ws.push(...words(l, line, 'bad')); }
      s.bad = s.ws.find((w) => w.classList.contains('bad'));
      if (s.bad) { s.strike = el('span', 'strike', s.bad); }
      ev(s.start + 0.05, 'whoosh');
      ev(s.start + 1.75, 'scratch');
    },
    brand(s, box) {
      const k = s.fast ? 0.7 : 1; s.k = k;
      const size = U * pick(36, 30, 30);
      const cy = H * pick(0.4, 0.4, 0.38);
      s.rays = el('div', 'rays', box); css(s.rays, { width: size * 3.6, height: size * 3.6, left: W / 2 - size * 1.8, top: cy - size * 1.8 });
      s.rings = [0, 1, 2].map(() => { const r = el('div', 'ring', box); css(r, { width: size, height: size, left: W / 2 - size / 2, top: cy - size / 2 }); return r; });
      s.mark = el('div', '', box, MARK); css(s.mark, { position: 'absolute', width: size, height: size, left: W / 2 - size / 2, top: cy - size / 2, filter: 'drop-shadow(0 2vmin 4vmin rgba(0,0,0,.45))' });
      s.svg = s.mark.querySelector('svg');
      const wm = el('div', 'title', box); css(wm, { position: 'absolute', left: 0, right: 0, top: cy + size * 0.68, textAlign: 'center', fontSize: U * pick(10.5, 9, 8.2) });
      s.letters = [];
      const add = (t, color) => { for (const ch of t) { const m = el('span', '', wm); css(m, { display: 'inline-block', overflow: 'hidden', verticalAlign: 'bottom', paddingBottom: '0.08em' }); const c = el('span', 'w', m, ch === ' ' ? '&nbsp;' : ch); c.style.color = color; s.letters.push(c); } };
      add('HigherSquare', '#4ADE80'); add(' 360', '#38BDF8');
      const tag = el('div', '', box); css(tag, { position: 'absolute', left: U * 6, right: U * 6, top: cy + size * 0.68 + U * pick(15, 13, 11.5), textAlign: 'center', fontSize: U * pick(4.6, 4.2, 3.6), fontWeight: 500, color: '#CFEFE0' });
      s.tag = words(tag, s.tagline);
      ev(s.start + 0.02, 'boom');
      ev(s.start + 1.0 * k, 'shimmer');
    },
    feature(s, box) {
      const dw = W * pick(0.86, 0.8, 0.56);
      const dev = browser(box, dw, A === 'v' ? 4 / 3 : 16 / 9);
      s.dev = dev;
      const devH = dev.h + dev.barH;
      s.dx = pick((W - dw) / 2, (W - dw) / 2, W * 0.4);
      s.dy = pick(H * 0.255, H * 0.31, (H - devH) / 2 + U * 2);
      css(dev.d, { left: s.dx, top: s.dy });
      s.glow = el('div', 'glow', box); css(s.glow, { width: dw * 1.1, height: devH * 0.5, left: s.dx - dw * 0.05, top: s.dy + devH * 0.7 });
      box.insertBefore(s.glow, dev.d);
      s.img = footage(dev.scr, s.clip);
      const head = el('div', '', box);
      const hx = pick(U * 6, U * 7, W * 0.055), hw = pick(W - U * 12, W - U * 14, W * 0.32);
      css(head, { position: 'absolute', left: hx, width: hw, textAlign: A === 'l' ? 'left' : 'center' });
      if (A === 'l') css(head, { top: H * 0.3 }); else css(head, { top: pick(H * 0.115, H * 0.075, 0) });
      const kick = el('div', 'kicker', head, s.kicker); css(kick, { fontSize: U * pick(3, 2.6, 2.3), marginBottom: U * 1.6 });
      s.kick = kick;
      const title = el('div', 'title', head); css(title, { fontSize: U * pick(9.2, 7.6, 6.8), position: 'relative' });
      s.titleBox = title;
      const t1 = el('div', '', title); s.ws = words(t1, s.title);
      if (s.alt) {
        const t2 = el('div', '', title); css(t2, { position: 'absolute', left: 0, right: 0, top: 0, fontFamily: "'Noto Sans Arabic', 'Inter'", direction: 'rtl', color: '#7FE0A8' });
        t2.textContent = s.alt; s.t1 = t1; s.t2 = t2;
        ev(s.start + s.altAt, 'whoosh-soft');
      }
      s.chipEls = (s.chips || []).map(([at, text], i) => {
        const c = el('div', 'chip', box, `<span class="ok">✓</span>${text}`); css(c, { fontSize: U * pick(3.4, 2.9, 2.5) });
        ev(s.start + s.enter + at, 'pop');
        return { c, at, i };
      });
      if (s.pop) {
        const pw = W * pick(0.94, 0.88, 0.5);
        const [rx, ry, rw, rh] = A === 'v' && s.pop.rectV ? s.pop.rectV : s.pop.rect; s.popRect = [rx, ry, rw, rh];
        const ph = pw * (rh * 1080) / (rw * 1920);
        const p = el('div', 'pop', box); css(p, { width: pw, height: ph });
        s.popEl = p; s.pw = pw; s.ph = ph;
        s.popImg = footage(p, s.clip);
        s.popPos = pick([(W - pw) / 2, H * 0.72], [(W - pw) / 2, H * 0.74], [W * 0.46, H * 0.72]);
        ev(s.start + s.enter + s.pop.at, 'pop-big');
      }
    },
    trio(s, box) {
      const n = s.items.length;
      const cw = pick(W * 0.64, W * 0.42, W * 0.285);
      const ch = cw * 9 / 16 + U * 4.2;
      const title = el('div', 'title', box); css(title, { position: 'absolute', left: 0, right: 0, top: pick(H * 0.075, H * 0.07, H * 0.1), textAlign: 'center', fontSize: U * pick(9.2, 7.6, 6.8) });
      s.ws = words(title, s.title);
      const pos = (i) => A === 'v' ? [W / 2 - cw / 2 + (i - 1) * U * 7, H * 0.22 + i * (ch + U * 7)]
        : A === 's' ? [W * (0.06 + i * 0.255), H * 0.36 + (i === 1 ? -U * 4 : U * 2)]
          : [W * 0.04 + i * (cw + W * 0.035), H * 0.4];
      const rot = (i) => A === 'v' ? (i - 1) * -2.5 : A === 's' ? (i - 1) * 7 : (i - 1) * 2;
      s.line = el('div', '', box, `<svg width="${W}" height="${H}" style="position:absolute;left:0;top:0;overflow:visible"><path pathLength="1" fill="none" stroke="#7FE0A8" stroke-width="${U * 0.5}" stroke-dasharray="1 1" stroke-linecap="round" opacity=".8"/></svg>`);
      const pts = s.items.map((_, i) => { const [x, y] = pos(i); return [x + cw / 2, y + ch / 2]; });
      s.line.querySelector('path').setAttribute('d', `M${pts.map((p) => p.join(' ')).join(' L')}`);
      s.dots = [0, 1, 2].map(() => { const d = el('div', '', box); css(d, { position: 'absolute', width: U * 2, height: U * 2, borderRadius: '50%', background: '#BFF5D8', boxShadow: '0 0 2vmin #7FE0A8' }); return d; });
      s.cards = s.items.map((it, i) => {
        const dev = browser(box, cw);
        const [x, y] = pos(i);
        css(dev.d, { left: x, top: y });
        const img = footage(dev.scr, it.clip);
        const lab = null;
        ev(s.start + s.enter + 0.15 + i * 0.28, 'pop');
        return { dev, img, lab, x, y, rot: rot(i), it, i };
      });
      for (const c of s.cards) { c.lab = el('div', 'label', box, c.it.label); css(c.lab, { fontSize: U * pick(3.6, 2.8, 2.6) }); }
      s.cw = cw; s.ch = ch;
    },
    phone(s, box) {
      const pw = pick(W * 0.5, W * 0.33, W * 0.19);
      const bez = U * 1.5;
      const c = cfg.clips[s.clip];
      const sw = pw - bez * 2, sh = sw * c.h / c.w;
      const ph = sh + bez * 2;
      s.px = pick(W / 2 - pw / 2, W * 0.6, W * 0.64);
      s.py = pick(H * 0.3, H * 0.15, (H - ph) / 2);
      const glow = el('div', 'glow', box); css(glow, { width: pw * 1.8, height: pw * 1.2, left: s.px - pw * 0.4, top: s.py + ph * 0.55 });
      const p = el('div', 'phone', box); css(p, { width: pw, height: ph, left: s.px, top: s.py });
      el('div', 'cam', p);
      const scr = el('div', 'screen', p); css(scr, { left: bez, top: bez, width: sw, height: sh });
      s.phoneEl = p; s.sw = sw; s.sh = sh; s.pw = pw; s.ph = ph;
      s.img = footage(scr, s.clip);
      const head = el('div', '', box);
      if (A === 'v') css(head, { position: 'absolute', left: U * 6, right: U * 6, top: H * 0.085, textAlign: 'center' });
      else if (A === 's') css(head, { position: 'absolute', left: U * 7, width: W * 0.5, top: H * 0.17 });
      else css(head, { position: 'absolute', left: W * 0.055, width: W * 0.36, top: H * 0.3 });
      const kick = el('div', 'kicker', head, s.kicker); css(kick, { fontSize: U * pick(3, 2.6, 2.3), marginBottom: U * 1.6 });
      const title = el('div', 'title', head); css(title, { fontSize: U * pick(9.2, 7.6, 6.8) });
      s.ws = words(title, s.title);
      s.kick = kick;
      if (s.note) {
        const n = el('div', 'note', box, `<img src="${MARK_URL}"><div><b>${s.note.head}</b><span>${s.note.body}</span></div><em>now</em>`);
        const nw = pick(W * 0.8, W * 0.56, W * 0.3);
        css(n, { width: nw });
        s.noteEl = n;
        s.notePos = pick([W * 0.1, H * 0.5], [U * 7, H * 0.55], [W * 0.055, H * 0.66]);
        ev(s.start + s.enter + s.note.at, 'pop');
      }
      if (s.done) {
        const d = el('div', 'done', box, `<span class="ok">✓</span>${s.done.text}`); css(d, { fontSize: U * pick(4.6, 3.8, 3.2) });
        s.doneEl = d;
        s.donePos = pick([W * 0.5, H * 0.64], [U * 7 + W * 0.02, H * 0.78], [W * 0.055, H * 0.82]);
        s.doneRings = [0, 1].map(() => el('div', 'ring', box));
        ev(s.start + s.enter + s.done.at, 'ding');
      }
    },
    flash(s, box) {
      const bl = s.dur / s.beats.length;
      s.bl = bl;
      s.items = s.beats.map((b, i) => {
        const g = el('div', '', box); css(g, { position: 'absolute', inset: 0, display: 'none' });
        let img, frame;
        if (b.phone) {
          const c = cfg.clips[b.clip];
          const pw = pick(W * 0.52, W * 0.34, W * 0.2), bez = U * 1.5, sw = pw - bez * 2, sh = sw * c.h / c.w;
          const p = el('div', 'phone', g); css(p, { width: pw, height: sh + bez * 2, left: W / 2 - pw / 2, top: pick(H * 0.3, H * 0.2, H * 0.12) });
          el('div', 'cam', p);
          const scr = el('div', 'screen', p); css(scr, { left: bez, top: bez, width: sw, height: sh });
          img = footage(scr, b.clip); frame = { el: p, bw: sw, bh: sh };
        } else {
          const dev = browser(g, W * pick(0.9, 0.86, 0.7), A === 'v' ? 4 / 3 : 16 / 9);
          css(dev.d, { left: (W - dev.w) / 2, top: pick(H * 0.3, H * 0.34, H * 0.26) });
          img = footage(dev.scr, b.clip); frame = { el: dev.d, bw: dev.w, bh: dev.h };
        }
        const word = el('div', 'word', g, b.word); css(word, { top: pick(H * 0.14, H * 0.08, H * 0.04), fontSize: U * pick(15, 12, 10.5) });
        if (i > 0) ev(s.start + s.enter + i * bl, 'hit');
        return { g, img, frame, word, b, i };
      });
      ev(s.start + s.enter, 'hit');
    },
    cta(s, box) {
      const size = U * pick(26, 20, 19);
      const cy = H * pick(0.31, 0.25, 0.24);
      s.rays = el('div', 'rays', box); css(s.rays, { width: size * 4, height: size * 4, left: W / 2 - size * 2, top: cy - size * 2 });
      s.rings = [0, 1].map(() => { const r = el('div', 'ring', box); css(r, { width: size, height: size, left: W / 2 - size / 2, top: cy - size / 2 }); return r; });
      s.mark = el('div', '', box, MARK); css(s.mark, { position: 'absolute', width: size, height: size, left: W / 2 - size / 2, top: cy - size / 2, filter: 'drop-shadow(0 2vmin 4vmin rgba(0,0,0,.45))' });
      s.svg = s.mark.querySelector('svg');
      const name = el('div', 'title', box, '<span style="color:#4ADE80">HigherSquare</span> <span style="color:#38BDF8">360</span>');
      css(name, { position: 'absolute', left: 0, right: 0, top: cy + size * 0.62, textAlign: 'center', fontSize: U * pick(6, 5.2, 4.6) });
      s.name = name;
      const title = el('div', 'title', box); css(title, { position: 'absolute', left: U * 5, right: U * 5, top: cy + size * 0.62 + U * pick(11, 9, 7.6), textAlign: 'center', fontSize: U * pick(10, 8.4, 7.4) });
      s.ws = words(title, s.title);
      const sub = el('div', '', box); css(sub, { position: 'absolute', left: 0, right: 0, top: cy + size * 0.62 + U * pick(36, 29, 24), textAlign: 'center', fontSize: U * pick(4.2, 3.6, 3.2), color: '#CFEFE0', fontWeight: 500 });
      s.sub = words(sub, s.sub);
      const bw = U * pick(52, 42, 36), bh = U * pick(12, 10, 9);
      s.bx = W / 2 - bw / 2; s.by = cy + size * 0.62 + U * pick(46, 37.5, 31.5);
      const btn = el('div', 'btn', box, `<span class="shine"></span><span style="position:relative">${s.button}  →</span>`);
      css(btn, { width: bw, height: bh, left: s.bx, top: s.by, fontSize: U * pick(5, 4.2, 3.7) });
      s.btn = btn; s.bw = bw; s.bh = bh;
      s.cursor = el('div', 'cursor', box, '<svg viewBox="0 0 28 28" width="100%" height="100%"><path d="M3 2 L3 22 L8.5 17 L12.5 26 L16 24.5 L12 15.8 L19.5 15.8 Z" fill="#fff" stroke="#1F3A44" stroke-width="1.6" stroke-linejoin="round"/></svg>');
      s.ripple = el('div', 'ripple', box);
      ev(s.start + s.enter + 2.15, 'click');
      ev(s.start + 0.02, 'boom-soft');
    },
  };

  // ---------------------------------------------------------------------------------------------- scene posers (lt = seconds since the scene's content started)
  const posers = {
    hook(s, lt) {
      const out = seg(lt, s.dur - 0.75, s.dur + 0.2);
      s.sheet.style.transform = `translate(-50%,-50%) perspective(${U * 160}px) rotateX(${48 + lt * 2}deg) rotateZ(${-24 + lt * 2.5}deg) translateY(${-lt * U * 4}px)`;
      s.sheet.style.opacity = 0.9 * seg(lt, 0, 0.6);
      for (const { cell, i, x, y } of s.cells) {
        if (out <= 0) { cell.style.transform = ''; cell.style.opacity = ''; continue; }
        const p = eIn(out);
        const d = U * 180 * p * (0.4 + rnd(i + 7));
        const len = Math.hypot(x, y) || 1;
        cell.style.transform = `translate3d(${(x / len) * d}px, ${(y / len) * d}px, ${p * U * 120 * rnd(i + 3)}px) rotate(${(rnd(i + 5) - 0.5) * 220 * p}deg)`;
        cell.style.opacity = 1 - p;
      }
      poseWords(s.ws, lt, 0.1, 0.11, 0.55);
      if (s.strike) {
        const p = eOut(seg(lt, 1.7, 2.05));
        s.strike.style.width = p * 104 + '%';
        const sh = seg(lt, 1.95, 2.35);
        s.bad.style.transform = (s.bad.style.transform || '') + ` translateX(${Math.sin(sh * 30) * (1 - sh) * U * 1.2}px)`;
      }
    },
    brand(s, lt) {
      const k = s.k;
      poseMark(s.svg, lt, k);
      s.mark.style.transform = `scale(${1 + lt * 0.012})`;
      s.rays.style.opacity = 0.9 * seg(lt, 0.3 * k, 1.2 * k);
      s.rays.style.transform = `rotate(${lt * 9}deg) scale(${lerp(0.6, 1, eOut(seg(lt, 0.2 * k, 1.4 * k)))})`;
      s.rings.forEach((r, i) => {
        const p = seg(lt, (0.45 + i * 0.32) * k, (1.65 + i * 0.32) * k);
        r.style.transform = `scale(${lerp(0.7, 2.6, eOut(p))})`; r.style.opacity = p > 0 && p < 1 ? (1 - p) * 0.8 : 0;
      });
      s.letters.forEach((c, i) => {
        const p = eOut(seg(lt, (1.0 + i * 0.03) * k, (1.0 + i * 0.03) * k + 0.45));
        c.style.transform = `translateY(${(1 - p) * 110}%)`;
      });
      poseWords(s.tag, lt, 1.55 * k, 0.07);
    },
    feature(s, lt) {
      const p = eOut(seg(lt, 0, 0.95));
      const drift = Math.sin(lt * 0.9) * 1.2;
      const ry = A === 'l' ? lerp(-28, -9, p) + drift : A === 'v' ? lerp(-16, -3, p) + drift * 0.6 : lerp(-22, -5, p) + drift;
      const rx = lerp(26, 7, p);
      s.dev.d.style.transform = `perspective(${U * 220}px) translateY(${(1 - p) * U * 28}px) rotateX(${rx}deg) rotateY(${ry}deg) scale(${lerp(0.84, 1, p)})`;
      s.dev.d.style.opacity = seg(lt, 0, 0.35);
      s.glow.style.opacity = 0.9 * p;
      const k = eInOut(seg(lt, 0.25, s.dur));
      const f0 = (A === 'v' && s.fromV) || s.from, f1 = (A === 'v' && s.toV) || s.to;
      const cam = [0, 1, 2].map((j) => lerp(f0[j], f1[j], k));
      poseFootage(s.img, Math.max(0, lt - 0.1) * s.speed, cam, s.dev.w, s.dev.h);
      s.kick.style.opacity = seg(lt, 0.05, 0.4); s.kick.style.transform = `translateY(${(1 - eOut(seg(lt, 0.05, 0.5))) * U * 3}px)`;
      poseWords(s.ws, lt, 0.15);
      if (s.t2) {
        const q = eInOut(seg(lt, s.altAt, s.altAt + 0.5));
        s.t1.style.transform = `translateY(${-q * 60}%)`; s.t1.style.opacity = 1 - q;
        s.t2.style.transform = `translateY(${(1 - q) * 60}%)`; s.t2.style.opacity = q;
      }
      for (const { c, at, i } of s.chipEls) {
        const q = eBack(seg(lt, at, at + 0.45));
        const w = c.offsetWidth, h = c.offsetHeight;
        let x, y;
        if (A === 'v') { x = W / 2 - w / 2; y = H * 0.705 + i * (h + U * 2.6); }
        else if (A === 's') { x = W - w - U * 5; y = H * 0.66 + i * (h + U * 1.8) - U * 6; }
        else { x = W * 0.055; y = H * 0.56 + i * (h + U * 2); }
        css(c, { left: x, top: y });
        c.style.transform = `translateY(${(1 - q) * U * 6}px) scale(${lerp(0.6, 1, q)})`;
        c.style.opacity = seg(lt, at, at + 0.2);
      }
      if (s.popEl) {
        const at = s.pop.at, q = eBack(seg(lt, at, at + 0.6));
        const [x, y] = s.popPos;
        css(s.popEl, { left: x, top: y });
        s.popEl.style.transform = `perspective(${U * 200}px) translateY(${(1 - q) * -U * 22}px) scale(${lerp(0.55, 1, q)}) rotateX(${(1 - q) * 30}deg)`;
        s.popEl.style.opacity = seg(lt, at, at + 0.15);
        const [rx2, ry2, rw, rh] = s.popRect;
        const zoom = 1 + eInOut(seg(lt, at + 0.6, s.dur)) * 0.06;
        const iw = (s.pw / rw) * zoom, ih = iw * 9 / 16;
        const c = cfg.clips[s.clip];
        const idx = clamp(Math.floor(Math.max(0, lt - 0.1) * s.speed * 30), 0, c.frames - 1) + 1;
        const src = `${c.url}/f${String(idx).padStart(4, '0')}.jpg`;
        if (s.popImg.dataset.src !== src) { s.popImg.dataset.src = src; s.popImg.src = src; pending.push(s.popImg.decode().catch(() => {})); }
        s.popImg.style.width = iw + 'px'; s.popImg.style.height = ih + 'px';
        s.popImg.style.transform = `translate(${-(rx2 + (rw / 2) * (1 - 1 / zoom)) * iw}px, ${-(ry2 + (rh / 2) * (1 - 1 / zoom)) * ih}px)`;
      }
    },
    trio(s, lt) {
      poseWords(s.ws, lt, 0.05);
      const path = s.line.querySelector('path');
      const pl = eInOut(seg(lt, 0.9, 1.8));
      path.style.strokeDashoffset = 1 - pl;
      const len = path.getTotalLength();
      s.dots.forEach((d, i) => {
        const f = ((lt - 1.4) * 0.45 + i / 3) % 1;
        const on = lt > 1.4;
        const pt = path.getPointAtLength(Math.max(0, f) * len);
        css(d, { left: pt.x - U, top: pt.y - U });
        d.style.opacity = on ? Math.sin(Math.max(0, f) * Math.PI) : 0;
      });
      for (const c of s.cards) {
        const a = 0.15 + c.i * 0.28;
        const p = eBack(seg(lt, a, a + 0.6));
        const fl = Math.sin(lt * 1.3 + c.i) * U * 0.6;
        c.dev.d.style.transform = `perspective(${U * 200}px) translateY(${(1 - p) * U * 25 + fl}px) rotate(${c.rot * (2 - p)}deg) rotateY(${(1 - p) * 30}deg) scale(${lerp(0.7, 1, p)})`;
        c.dev.d.style.opacity = seg(lt, a, a + 0.25);
        poseFootage(c.img, Math.max(0, lt - a) * c.it.speed, [c.it.focus[0], c.it.focus[1], c.it.focus[2] * (1 + lt * 0.03)], c.dev.w, c.dev.h);
        const q = eBack(seg(lt, a + 0.35, a + 0.75));
        const lw = c.lab.offsetWidth, lh = c.lab.offsetHeight;
        const lx = A === 'v' ? (c.i % 2 ? c.x + s.cw - lw * 0.7 : c.x - lw * 0.3) : c.x + s.cw / 2 - lw / 2;
        const ly = A === 'v' ? c.y - lh * 0.45 : c.y + s.ch - lh * 0.4 + (A === 's' ? U * 1 : 0);
        css(c.lab, { left: clamp(lx, U * 2, W - lw - U * 2), top: ly });
        c.lab.style.transform = `scale(${q})`; c.lab.style.opacity = seg(lt, a + 0.35, a + 0.5);
      }
    },
    phone(s, lt) {
      const p = eOut(seg(lt, 0, 1.0));
      const fl = Math.sin(lt * 1.1) * U * 0.8;
      s.phoneEl.style.transform = `perspective(${U * 200}px) translateY(${(1 - p) * U * 45 + fl}px) rotateY(${lerp(-30, -7, p) + Math.sin(lt * 0.8) * 2}deg) rotateX(${lerp(18, 4, p)}deg) rotateZ(${lerp(8, 2, p)}deg)`;
      s.phoneEl.style.opacity = seg(lt, 0, 0.3);
      const c = cfg.clips[s.clip];
      poseFootage(s.img, Math.max(0, lt - 0.1) * s.speed, [0.5, 0.5, 1], s.sw, s.sh);
      s.kick.style.opacity = seg(lt, 0.05, 0.4);
      poseWords(s.ws, lt, 0.15);
      if (s.noteEl) {
        const at = s.note.at, q = eBack(seg(lt, at, at + 0.55));
        const [x, y] = s.notePos;
        css(s.noteEl, { left: x, top: y });
        s.noteEl.style.transform = `translateX(${(1 - q) * -U * 30}px) scale(${lerp(0.8, 1, q)}) rotate(${(1 - q) * -6}deg)`;
        s.noteEl.style.opacity = seg(lt, at, at + 0.2);
      }
      if (s.doneEl) {
        const at = s.done.at, q = eBack(seg(lt, at, at + 0.5));
        const w = s.doneEl.offsetWidth, h = s.doneEl.offsetHeight;
        const [x0, y] = s.donePos;
        const x = A === 'v' ? x0 - w / 2 : x0;
        css(s.doneEl, { left: x, top: y });
        s.doneEl.style.transform = `scale(${q})`; s.doneEl.style.opacity = seg(lt, at, at + 0.12);
        s.doneRings.forEach((r, i) => {
          const rp = seg(lt, at + 0.1 + i * 0.18, at + 1.0 + i * 0.18);
          css(r, { width: h, height: h, left: x + h * 0.0, top: y });
          r.style.transform = `scale(${lerp(0.6, 3.2, eOut(rp))})`; r.style.opacity = rp > 0 && rp < 1 ? 1 - rp : 0;
        });
      }
    },
    flash(s, lt) {
      for (const it of s.items) {
        const a = it.i * s.bl, b = a + s.bl;
        const on = lt >= a && (lt < b || it.i === s.items.length - 1);
        it.g.style.display = on ? 'block' : 'none';
        if (!on) continue;
        const l = lt - a;
        const punch = eOut(seg(l, 0, 0.35));
        it.frame.el.style.transform = `perspective(${U * 200}px) scale(${lerp(1.18, 1, punch) + l * 0.02}) rotateY(${(it.i % 2 ? 1 : -1) * lerp(10, 4, punch)}deg)`;
        const f = it.b.focus || [0.5, 0.5, 1];
        poseFootage(it.img, l * it.b.speed, it.b.phone ? [0.5, 0.5, 1] : [f[0], f[1], f[2] * (1 + l * 0.05)], it.frame.bw, it.frame.bh);
        const q = eBack(seg(l, 0.02, 0.32));
        it.word.style.transform = `scale(${lerp(1.6, 1, q)})`; it.word.style.opacity = seg(l, 0, 0.1);
      }
    },
    cta(s, lt) {
      poseMark(s.svg, lt + 0.4, 0.75);
      s.mark.style.transform = `translateY(${Math.sin(lt * 1.6) * U * 0.8}px) rotate(${Math.sin(lt * 1.1) * 2}deg)`;
      s.rays.style.opacity = 0.8 * seg(lt, 0.2, 1);
      s.rays.style.transform = `rotate(${-lt * 12}deg)`;
      s.rings.forEach((r, i) => {
        const p = ((lt + i * 0.9) % 1.8) / 1.8;
        r.style.transform = `scale(${lerp(0.9, 2.4, eOut(p))})`; r.style.opacity = lt > 0.3 ? (1 - p) * 0.6 : 0;
      });
      s.name.style.opacity = seg(lt, 0.3, 0.7); s.name.style.transform = `translateY(${(1 - eOut(seg(lt, 0.3, 0.8))) * U * 3}px)`;
      poseWords(s.ws, lt, 0.45, 0.08);
      poseWords(s.sub, lt, 1.0, 0.05);
      const bq = eBack(seg(lt, 1.15, 1.65));
      const press = 1 - 0.07 * Math.sin(clamp((lt - 2.12) / 0.22) * Math.PI);
      const pulse = lt > 2.4 ? 1 + Math.sin((lt - 2.4) * 5) * 0.025 : 1;
      s.btn.style.transform = `scale(${bq * press * pulse})`;
      s.btn.style.opacity = seg(lt, 1.15, 1.3);
      s.btn.querySelector('.shine').style.left = `${lerp(-40, 140, ((lt - 1.5) % 1.6) / 1.6)}%`;
      const cp = eInOut(seg(lt, 1.3, 2.05));
      const tx = s.bx + s.bw * 0.62, ty = s.by + s.bh * 0.55;
      const cx = lerp(W * 0.86, tx, cp), cy = lerp(H * 0.98, ty, cp);
      css(s.cursor, { left: cx, top: cy });
      s.cursor.style.opacity = seg(lt, 1.3, 1.45) * (1 - seg(lt, 3.0, 3.3));
      s.cursor.style.transform = `scale(${1 - 0.15 * Math.sin(clamp((lt - 2.12) / 0.2) * Math.PI)})`;
      const rp = seg(lt, 2.15, 2.75);
      const rs = U * 4;
      css(s.ripple, { width: rs, height: rs, left: tx - rs / 2, top: ty - rs / 2 });
      s.ripple.style.transform = `scale(${lerp(0.4, 4, eOut(rp))})`; s.ripple.style.opacity = rp > 0 && rp < 1 ? 1 - rp : 0;
    },
  };

  // ---------------------------------------------------------------------------------------------- setup and render
  window.setup = (config) => {
    cfg = config; W = config.w; H = config.h; U = Math.min(W, H) / 100;
    A = H / W > 1.3 ? 'v' : W / H > 1.3 ? 'l' : 's';
    root = document.getElementById('root');
    css(root, { width: W, height: H });
    // Background: gradient, drifting brand-coloured light, a dot grid and slow particles.
    const bg = el('div', '', root); bg.id = 'bg';
    cfg.blobs = [['rgba(79,160,119,.55)', 0.2, 0.25, 95], ['rgba(42,138,147,.5)', 0.85, 0.7, 110], ['rgba(127,224,168,.25)', 0.6, 0.05, 70]].map(([c, x, y, s], i) => {
      const b = el('div', 'blob', bg); css(b, { width: U * s, height: U * s, background: `radial-gradient(closest-side, ${c}, transparent)` }); return { b, x, y, s, i };
    });
    cfg.dotsEl = el('div', 'dots', bg); css(cfg.dotsEl, { backgroundSize: `${U * 3.4}px ${U * 3.4}px` });
    cfg.parts = Array.from({ length: 28 }, (_, i) => { const p = el('div', 'p', bg); const s = U * (0.25 + rnd(i) * 0.5); css(p, { width: s, height: s }); return { p, i }; });
    el('div', 'vig', bg);
    let t = 0;
    for (const sc of config.scenes) {
      const s = { ...sc, start: t, enter: sc.in === 'wipe' || sc.in === 'circle' ? T / 2 : 0 };
      s.box = el('div', 'scene', root);
      scenes.push(s);
      t += sc.dur;
    }
    cfg.total = t + 0.6;
    for (const s of scenes) { s.box.style.display = 'block'; builders[s.type](s, s.box); s.box.style.display = 'none'; }
    // Small lockup in the corner between the brand reveal and the closing card.
    corner = el('div', '', root, `<img src="${MARK_URL}"><span><span class="a">HigherSquare</span> <span class="b">360</span></span>`); corner.id = 'corner';
    const cs = U * pick(5.6, 4.6, 4.2);
    css(corner, { fontSize: cs * 0.62, top: U * pick(4.2, 3.4, 3.2) });
    corner.querySelector('img').style.width = cs + 'px'; corner.querySelector('img').style.height = cs + 'px';
    if (A === 'v') css(corner, { left: 0, right: 0, justifyContent: 'center' }); else css(corner, { left: U * 3.6 });
    sweep = el('div', '', root); sweep.id = 'sweep';
    flashw = el('div', '', root); flashw.id = 'flashw';
    for (const s of scenes.slice(1)) if (s.in === 'wipe' || s.in === 'circle') ev(s.start, 'whoosh'); else if (s.in) ev(s.start, s.in === 'zoom' ? 'whoosh' : 'whoosh-soft');
    events.sort((a, b) => a.t - b.t);
    return { total: cfg.total, events, aspect: A };
  };

  window.render = async (t) => {
    pending.length = 0;
    // background motion
    for (const { b, x, y, s, i } of cfg.blobs) {
      css(b, { left: W * x - (U * s) / 2 + Math.sin(t * 0.35 + i * 2) * U * 12, top: H * y - (U * s) / 2 + Math.cos(t * 0.27 + i) * U * 10 });
    }
    cfg.dotsEl.style.transform = `translate(${(t * U * 0.8) % (U * 3.4)}px, ${(-t * U * 1.2) % (U * 3.4)}px)`;
    for (const { p, i } of cfg.parts) {
      const sp = 0.025 + rnd(i + 40) * 0.05;
      const y = ((rnd(i + 20) - t * sp) % 1 + 1) % 1;
      css(p, { left: W * rnd(i + 10) + Math.sin(t * 0.8 + i) * U * 2, top: H * y });
      p.style.opacity = 0.15 + 0.35 * Math.sin(y * Math.PI);
    }
    sweep.style.display = 'none'; flashw.style.display = 'none';
    for (let i = 0; i < scenes.length; i++) {
      const s = scenes[i], next = scenes[i + 1];
      const lt = t - s.start - s.enter;
      let visible = t >= s.start + s.enter;
      let exitP = 0;
      if (next) {
        const nin = next.in || 'cut';
        if (nin === 'wipe' || nin === 'circle') { if (t >= next.start + T / 2) visible = false; }
        else if (nin === 'cut') { if (t >= next.start) visible = false; }
        else { exitP = seg(t, next.start, next.start + T); if (exitP >= 1) visible = false; }
      }
      if (i === scenes.length - 1 && t > cfg.total) visible = true;
      s.box.style.display = visible ? 'block' : 'none';
      if (!visible) continue;
      posers[s.type](s, lt);
      // entrance
      const ip = seg(t, s.start, s.start + T);
      let tf = '', op = 1, fl = '';
      if (s.in === 'rise' && i > 0) { const e = eOut(ip); tf += `translateY(${(1 - e) * H * 0.5}px) `; op *= seg(ip, 0, 0.4); }
      if (s.in === 'zoom' && i > 0) { const e = eOut(seg(t, s.start + 0.1, s.start + T)); tf += `scale(${lerp(1.3, 1, e)}) `; op *= e; fl = e < 1 ? `blur(${(1 - e) * 16}px)` : ''; }
      // exit
      if (exitP > 0) {
        const e = eIn(exitP);
        if (next.in === 'rise') { tf += `translateY(${-e * H * 0.22}px) scale(${1 - e * 0.06}) `; op *= 1 - e; }
        else { tf += `scale(${1 + e * 0.12}) `; op *= 1 - e; fl = `blur(${e * 14}px)`; }
      }
      s.box.style.transform = tf; s.box.style.opacity = op; s.box.style.filter = fl || 'none';
      // the last scene fades to black at the very end
      if (i === scenes.length - 1) s.box.style.opacity = op * (1 - seg(t, cfg.total - 0.5, cfg.total));
    }
    // wipe / circle transition panels
    for (const s of scenes) {
      if (s.in !== 'wipe' && s.in !== 'circle') continue;
      const p = seg(t, s.start, s.start + T);
      if (p <= 0 || p >= 1) continue;
      sweep.style.display = 'block';
      if (s.in === 'wipe') {
        const x = lerp(-1.4, 1.4, eInOut(p)) * W;
        css(sweep, { left: x - W * 0.2, top: -H * 0.2, width: W * 1.4, height: H * 1.4, borderRadius: 0, background: 'linear-gradient(100deg, transparent 0%, #3A8A62 12%, #4FA077 50%, #2A8A93 88%, transparent 100%)', transform: 'skewX(-14deg)', opacity: 1 });
      } else {
        const r = Math.hypot(W, H) * (p < 0.5 ? eOut(p * 2) : 1);
        css(sweep, { left: W / 2 - r, top: H * 0.55 - r, width: r * 2, height: r * 2, borderRadius: '50%', background: 'radial-gradient(circle, #4FA077 0%, #2A8A93 70%)', transform: 'none', opacity: p < 0.5 ? 1 : 1 - eOut((p - 0.5) * 2) });
      }
    }
    // corner lockup
    const b = scenes.find((s) => s.type === 'brand'), c = scenes.find((s) => s.type === 'cta');
    const cOn = b && c ? seg(t, b.start + b.dur + 0.2, b.start + b.dur + 0.6) * (1 - seg(t, c.start, c.start + 0.3)) : 0;
    corner.style.opacity = cOn; corner.style.transform = `translateY(${(1 - cOn) * -U * 2}px)`;
    await Promise.all(pending);
    return true;
  };
  window.MARK_URL = MARK_URL;
})();
