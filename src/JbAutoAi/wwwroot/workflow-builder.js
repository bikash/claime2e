// Workflow builder: palette → canvas → inspector.
//
// ponytail: hand-rolled on pointer events and one <svg>, same as the agent flow
// canvas. Nodes are absolutely positioned divs, wires are bezier paths, and the
// whole graph is one JSON blob posted by the Save button. No graph library, no
// virtual DOM — the editor is add / move / wire / configure / delete.
(function () {
  const form = document.getElementById('wfForm');
  if (!form) return;

  const inner = document.getElementById('inner');
  const wires = document.getElementById('wires');
  const canvas = document.getElementById('canvas');
  const inspector = document.getElementById('inspector');
  const palList = document.getElementById('palList');
  const NW = 196, PORTY = 31;
  const L = (nl, en) => (document.documentElement.lang === 'nl' ? nl : en);

  const CAT = form.dataset.cat ? JSON.parse(form.dataset.cat) : {};
  const TYPES = JSON.parse(form.dataset.types);
  const FIELDS = JSON.parse(form.dataset.fields);
  const CATLBL = JSON.parse(form.dataset.catlbl);

  let graph = { nodes: [], conns: [] };
  try {
    const g = JSON.parse(form.dataset.graph || '{}');
    graph.nodes = Array.isArray(g.nodes) ? g.nodes : [];
    graph.conns = Array.isArray(g.conns) ? g.conns : [];
  } catch (e) { /* a corrupt config opens an empty canvas, not an error page */ }

  let seq = graph.nodes.length, sel = null, drag = null, link = null;
  const nid = () => { let i; do { i = 'n' + (++seq); } while (graph.nodes.some(n => n.id === i)); return i; };
  const cid = () => 'c' + (Math.round(performance.now() * 1000) % 100000).toString(36) + graph.conns.length;
  const nodeById = id => graph.nodes.find(n => n.id === id);
  const nodeTitle = n => n.title || (TYPES[n.type] ? TYPES[n.type].label : n.type);

  /* ---------- palette ---------- */
  function buildPalette() {
    const cats = {};
    Object.entries(TYPES).forEach(([k, t]) => { (cats[t.cat] = cats[t.cat] || []).push([k, t]); });
    const order = ['trigger', 'ingest', 'enrich', 'ai', 'decision', 'human', 'action', 'mcp'];
    palList.textContent = '';
    order.forEach(cat => {
      if (!cats[cat]) return;
      const h = document.createElement('div');
      h.className = 'pal-cat';
      h.textContent = CATLBL[cat] || cat;
      palList.appendChild(h);
      cats[cat].forEach(([k, t]) => {
        const el = document.createElement('div');
        el.className = 'pal-item';
        el.draggable = true;
        el.dataset.type = k;
        el.innerHTML = '<span class="g"></span><span><span class="pl"></span><span class="pc"></span></span>';
        el.querySelector('.g').style.background = CAT[t.cat];
        el.querySelector('.g').textContent = t.g;
        el.querySelector('.pl').textContent = t.label;
        el.querySelector('.pc').textContent = t.sub;
        el.addEventListener('dragstart', e => e.dataTransfer.setData('text/plain', k));
        el.addEventListener('dblclick', () => addNode(k, canvas.scrollLeft + 120, canvas.scrollTop + 120));
        palList.appendChild(el);
      });
    });
  }

  /* ---------- nodes ---------- */
  function cfgSummary(n) {
    const t = TYPES[n.type];
    if (!t) return n.type;
    if (t.mcp) return (n.cfg.srv || 'mcp.reduzer.com') + ' · ' + (n.cfg.tool || 'search_products');
    if (n.type === 'agent') return n.cfg.agentId || 'agentId?';
    if (n.type === 'decision') return '≤ €' + (n.cfg.amt || '5000') + ' · fraud ≤ ' + (n.cfg.fr || '20');
    if (n.type === 'ai_liability') return 'corpus ' + (n.cfg.corpus || 'v2026.2');
    return t.sub;
  }

  function buildNode(n) {
    const t = TYPES[n.type] || { cat: 'ingest', g: '??', sub: '' };
    const el = document.createElement('div');
    el.className = 'node' + (t.mcp ? ' mcp' : '') + (t.exec ? '' : ' design');
    el.id = n.id;
    el.style.left = n.x + 'px';
    el.style.top = n.y + 'px';
    el.innerHTML =
      '<div class="node-h"><span class="g"></span><span class="nt"></span>' +
      (t.mcp ? '<span class="badge">MCP</span>' : '') + '</div>' +
      '<div class="node-b"></div>' +
      '<button type="button" class="port in" data-port="in"></button>' +
      '<button type="button" class="port out" data-port="out"></button>';
    el.querySelector('.g').style.background = CAT[t.cat];
    el.querySelector('.g').textContent = t.g;
    el.querySelector('.nt').textContent = nodeTitle(n);
    el.querySelector('.node-b').textContent = cfgSummary(n);
    el.querySelectorAll('.port').forEach(p => { p.dataset.node = n.id; });
    inner.appendChild(el);

    el.querySelector('.node-h').addEventListener('pointerdown', e => startDrag(e, n, el));
    el.addEventListener('pointerdown', () => select(n.id));
    el.querySelector('.port.out').addEventListener('pointerdown', e => {
      e.stopPropagation();
      link = { id: n.id, a: portPos(n.id, 'out') };
    });
    return el;
  }

  function renderNodes() {
    inner.querySelectorAll('.node').forEach(e => e.remove());
    graph.nodes.forEach(buildNode);
    applySel();
  }

  /* ---------- wires ---------- */
  function portPos(id, which) {
    const n = nodeById(id);
    return n ? { x: n.x + (which === 'out' ? NW : 0), y: n.y + PORTY } : { x: 0, y: 0 };
  }
  function bezier(a, b) {
    const dx = Math.max(40, Math.abs(b.x - a.x) / 2);
    return 'M' + a.x + ',' + a.y + ' C' + (a.x + dx) + ',' + a.y +
           ' ' + (b.x - dx) + ',' + b.y + ' ' + b.x + ',' + b.y;
  }
  function renderWires(temp) {
    const ns = 'http://www.w3.org/2000/svg';
    wires.textContent = '';
    graph.conns.forEach(c => {
      if (!nodeById(c.from) || !nodeById(c.to)) return;
      const p = document.createElementNS(ns, 'path');
      p.setAttribute('d', bezier(portPos(c.from, 'out'), portPos(c.to, 'in')));
      p.addEventListener('click', () => {
        graph.conns = graph.conns.filter(x => x.id !== c.id);
        renderWires(); sync(); ping(L('Verbinding verwijderd', 'Connection removed'));
      });
      wires.appendChild(p);
    });
    if (temp) {
      const p = document.createElementNS(ns, 'path');
      p.setAttribute('class', 'temp');
      p.setAttribute('d', bezier(temp.a, temp.b));
      wires.appendChild(p);
    }
  }

  /* ---------- drag / link ---------- */
  function startDrag(e, n, el) {
    e.stopPropagation();
    const r = inner.getBoundingClientRect();
    drag = { n, el, dx: e.clientX - r.left - n.x, dy: e.clientY - r.top - n.y };
    el.setPointerCapture(e.pointerId);
  }
  document.addEventListener('pointermove', e => {
    const r = inner.getBoundingClientRect();
    if (drag) {
      drag.n.x = Math.max(0, e.clientX - r.left - drag.dx);
      drag.n.y = Math.max(0, e.clientY - r.top - drag.dy);
      drag.el.style.left = drag.n.x + 'px';
      drag.el.style.top = drag.n.y + 'px';
      renderWires();
    }
    if (link) renderWires({ a: link.a, b: { x: e.clientX - r.left, y: e.clientY - r.top } });
  });
  document.addEventListener('pointerup', e => {
    if (link) {
      const t = document.elementFromPoint(e.clientX, e.clientY);
      if (t && t.classList.contains('port') && t.dataset.port === 'in' && t.dataset.node !== link.id) {
        if (!graph.conns.some(c => c.from === link.id && c.to === t.dataset.node)) {
          graph.conns.push({ id: cid(), from: link.id, to: t.dataset.node });
          ping(L('Verbonden', 'Connected'));
        }
      }
      link = null;
      renderWires();
    }
    if (drag) { drag = null; }
    sync();
  });

  /* ---------- add ---------- */
  canvas.addEventListener('dragover', e => { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; });
  canvas.addEventListener('drop', e => {
    e.preventDefault();
    const type = e.dataTransfer.getData('text/plain');
    if (!type || !TYPES[type]) return;
    const r = inner.getBoundingClientRect();
    addNode(type, e.clientX - r.left - NW / 2, e.clientY - r.top - 20);
  });
  function addNode(type, x, y) {
    const n = { id: nid(), type, x: Math.max(0, Math.round(x)), y: Math.max(0, Math.round(y)), title: null, cfg: {} };
    graph.nodes.push(n);
    buildNode(n);
    renderWires();
    select(n.id);
    ping(L('Module toegevoegd', 'Module added'));
  }

  /* ---------- selection + inspector ---------- */
  function select(id) { sel = id; applySel(); refreshInspector(); }
  function applySel() { inner.querySelectorAll('.node').forEach(e => e.classList.toggle('sel', e.id === sel)); }

  function refreshInspector() { sel ? inspNode() : inspWorkflow(); }

  function inspWorkflow() {
    const systems = graph.nodes.filter(n => TYPES[n.type] && (TYPES[n.type].cat === 'enrich' || TYPES[n.type].cat === 'mcp'));
    const design = graph.nodes.filter(n => TYPES[n.type] && !TYPES[n.type].exec).length;
    inspector.textContent = '';
    const h = document.createElement('div');
    h.innerHTML =
      '<div class="insp-h">' + L('Workflow', 'Workflow') + '</div>' +
      '<div class="insp-sub">' + L('selecteer een module', 'select a module') + '</div>' +
      '<div class="metric"><span class="k">' + L('Modules', 'Modules') + '</span><span class="v">' + graph.nodes.length + '</span></div>' +
      '<div class="metric"><span class="k">' + L('Verbindingen', 'Connections') + '</span><span class="v">' + graph.conns.length + '</span></div>' +
      '<div class="metric"><span class="k">' + L('Uitvoerbaar', 'Executable') + '</span><span class="v">' + (graph.nodes.length - design) + '/' + graph.nodes.length + '</span></div>';
    inspector.appendChild(h);

    const lbl = document.createElement('div');
    lbl.className = 'sect-lbl';
    lbl.textContent = L('Gekoppelde systemen & MCP', 'Connected systems & MCP');
    inspector.appendChild(lbl);
    systems.forEach(n => {
      const t = TYPES[n.type];
      const row = document.createElement('div');
      row.className = 'sys-row';
      row.innerHTML = '<span class="g"></span><span><span class="sn"></span><span class="ss"></span></span><span class="cdot"></span>';
      row.querySelector('.g').style.background = CAT[t.cat];
      row.querySelector('.g').textContent = t.g;
      row.querySelector('.sn').textContent = nodeTitle(n);
      row.querySelector('.ss').textContent = t.mcp ? (n.cfg.srv || 'mcp.reduzer.com') : t.sub;
      inspector.appendChild(row);
    });

    const hint = document.createElement('div');
    hint.className = 'hint';
    hint.textContent = L(
      'Modules zonder uitvoerder worden als notitie vastgelegd, niet uitgevoerd — de runner kent classify, extract, email, crm_push, webhook_call en decision.',
      'Modules without an executor are recorded as a note, not run — the runner knows classify, extract, email, crm_push, webhook_call and decision.');
    inspector.appendChild(hint);
  }

  function inspNode() {
    const n = nodeById(sel);
    if (!n) { sel = null; inspWorkflow(); return; }
    const t = TYPES[n.type] || { cat: 'ingest', g: '??', sub: '', label: n.type };
    const flds = FIELDS[n.type] || [];

    inspector.textContent = '';
    const head = document.createElement('div');
    head.innerHTML = '<div class="insp-h"><span class="g"></span><span class="ttl"></span></div>' +
                     '<div class="insp-sub"></div>';
    head.querySelector('.g').style.background = CAT[t.cat];
    head.querySelector('.g').textContent = t.g;
    head.querySelector('.ttl').textContent = nodeTitle(n);
    head.querySelector('.insp-sub').textContent =
      (CATLBL[t.cat] || t.cat) + ' · ' + t.sub + (t.exec ? '' : ' · ' + L('alleen ontwerp', 'design only'));
    inspector.appendChild(head);

    const title = field(L('Naam', 'Label'), 'text', nodeTitle(n));
    title.querySelector('input').addEventListener('input', e => {
      n.title = e.target.value || null;
      document.querySelector('#' + n.id + ' .nt').textContent = nodeTitle(n);
      sync();
    });
    inspector.appendChild(title);

    flds.forEach(f => {
      const val = n.cfg[f.k] !== undefined ? n.cfg[f.k] : (f.v !== undefined ? f.v : '');
      const wrap = f.t === 'select' ? selectField(f.l, f.o, val)
                 : f.t === 'toggle' ? toggleField(f.l, !!val)
                 : f.t === 'textarea' ? textareaField(f.l, val)
                 : field(f.l, f.t === 'number' ? 'number' : 'text', val);
      const input = wrap.querySelector('input, select, textarea');
      input.addEventListener('change', () => {
        n.cfg[f.k] = input.type === 'checkbox' ? input.checked : input.value;
        document.querySelector('#' + n.id + ' .node-b').textContent = cfgSummary(n);
        sync();
      });
      inspector.appendChild(wrap);
    });

    const actions = document.createElement('div');
    actions.className = 'insp-actions';
    if (t.mcp) {
      const test = document.createElement('button');
      test.type = 'button';
      test.className = 'ibtn mcp';
      test.textContent = '⚡ ' + L('Verbinding testen', 'Test connection');
      test.addEventListener('click', () => ping(L('MCP-server is niet gekoppeld — configuratie opgeslagen.',
                                                  'MCP server is not connected — configuration saved.')));
      actions.appendChild(test);
    }
    if (flds.some(f => f.k === 'prompt')) {
      const prev = document.createElement('button');
      prev.type = 'button';
      prev.className = 'ibtn';
      prev.textContent = '👁 ' + L('Prompt bekijken', 'Preview prompt');
      prev.addEventListener('click', () => {
        const lines = [];
        if (n.type === 'agent')
          lines.push(L('Agent: ', 'Agent: ') + (n.cfg.agentId || '—') + '\n' +
                     L('De basis-prompt van de agent staat op de Agents-pagina; onderstaande stap-instructies komen daar bovenop.',
                       'The agent\'s base prompt lives on the Agents page; the step instructions below are added on top.') + '\n');
        lines.push(n.cfg.prompt || L('Nog geen prompt ingesteld voor deze module.',
                                     'No prompt set for this module yet.'));
        promptPreview(nodeTitle(n), lines.join('\n'));
      });
      actions.appendChild(prev);
    }
    const del = document.createElement('button');
    del.type = 'button';
    del.className = 'ibtn del';
    del.textContent = L('Module verwijderen', 'Delete module');
    del.addEventListener('click', () => {
      graph.conns = graph.conns.filter(c => c.from !== sel && c.to !== sel);
      graph.nodes = graph.nodes.filter(x => x.id !== sel);
      document.getElementById(sel)?.remove();
      sel = null;
      renderWires(); sync(); inspWorkflow();
      ping(L('Module verwijderd', 'Module deleted'));
    });
    actions.appendChild(del);
    inspector.appendChild(actions);
  }

  // Singleton preview dialog — native <dialog>, reuses the .flow-modal styling.
  let previewDlg = null;
  function promptPreview(title, text) {
    if (!previewDlg) {
      previewDlg = document.createElement('dialog');
      previewDlg.className = 'flow-modal';
      previewDlg.innerHTML =
        '<div class="label" style="margin-bottom:10px"></div>' +
        '<pre style="white-space:pre-wrap;font-size:13px;margin:0"></pre>' +
        '<div style="display:flex;justify-content:flex-end;margin-top:12px">' +
        '<button type="button" class="ibtn"></button></div>';
      previewDlg.querySelector('button').textContent = L('Sluiten', 'Close');
      previewDlg.querySelector('button').addEventListener('click', () => previewDlg.close());
      previewDlg.addEventListener('click', e => { if (e.target === previewDlg) previewDlg.close(); });
      document.body.appendChild(previewDlg);
    }
    previewDlg.querySelector('.label').textContent = title;
    previewDlg.querySelector('pre').textContent = text;
    previewDlg.showModal();
  }

  function field(label, type, value) {
    const d = document.createElement('div');
    d.className = 'fld';
    d.innerHTML = '<label></label><input>';
    d.querySelector('label').textContent = label;
    const i = d.querySelector('input');
    i.type = type;
    i.value = value;
    return d;
  }
  function textareaField(label, value) {
    const d = document.createElement('div');
    d.className = 'fld';
    d.innerHTML = '<label></label><textarea rows="5"></textarea>';
    d.querySelector('label').textContent = label;
    d.querySelector('textarea').value = value;
    return d;
  }
  function selectField(label, options, value) {
    const d = document.createElement('div');
    d.className = 'fld';
    d.innerHTML = '<label></label><select></select>';
    d.querySelector('label').textContent = label;
    const s = d.querySelector('select');
    options.forEach(o => {
      const opt = document.createElement('option');
      opt.textContent = o;
      opt.selected = o === value;
      s.appendChild(opt);
    });
    return d;
  }
  function toggleField(label, checked) {
    const d = document.createElement('div');
    d.className = 'fld';
    d.innerHTML = '<label class="toggle"><input type="checkbox"><span></span></label>';
    d.querySelector('input').checked = checked;
    d.querySelector('span').textContent = label;
    return d;
  }

  canvas.addEventListener('pointerdown', e => {
    if (e.target === canvas || e.target === inner || e.target === wires) {
      sel = null; applySel(); inspWorkflow();
    }
  });

  /* ---------- toolbar ---------- */
  function sync() { document.getElementById('graphJson').value = JSON.stringify(graph); }

  document.getElementById('wfValidate').addEventListener('click', () => {
    const noTrigger = !graph.nodes.some(n => TYPES[n.type] && TYPES[n.type].cat === 'trigger');
    const orphans = graph.nodes.filter(n => !graph.conns.some(c => c.from === n.id || c.to === n.id)).length;
    if (noTrigger) ping(L('⚠ Geen trigger gevonden', '⚠ No trigger found'));
    else if (orphans) ping(L('⚠ ' + orphans + ' losse module(s)', '⚠ ' + orphans + ' disconnected module(s)'));
    else ping(L('✓ Workflow geldig', '✓ Workflow valid'));
  });

  document.getElementById('wfSimulate').addEventListener('click', () => {
    // Walk the wired order and light each module. A dry run of the shape, not of the
    // work — the real execution is /workflows/{id}/run against a claim.
    const order = topo();
    let i = 0;
    ping(L('Simulatie gestart…', 'Simulation started…'));
    (function step() {
      if (i > 0) document.getElementById(order[i - 1])?.classList.remove('run');
      if (i >= order.length) { ping(L('✓ Doorlopen (gesimuleerd)', '✓ Walked through (simulated)')); return; }
      document.getElementById(order[i])?.classList.add('run');
      i++;
      setTimeout(step, 170);
    })();
  });

  function topo() {
    const incoming = {}, out = {};
    graph.nodes.forEach(n => { incoming[n.id] = 0; out[n.id] = []; });
    graph.conns.forEach(c => {
      if (incoming[c.to] === undefined || out[c.from] === undefined) return;
      out[c.from].push(c.to); incoming[c.to]++;
    });
    const q = Object.keys(incoming).filter(k => incoming[k] === 0), order = [];
    while (q.length) {
      const id = q.shift();
      order.push(id);
      out[id].forEach(n => { if (--incoming[n] === 0) q.push(n); });
    }
    graph.nodes.forEach(n => { if (!order.includes(n.id)) order.push(n.id); });
    return order;
  }

  const toast = document.getElementById('toast');
  const tmsg = document.getElementById('toastMsg');
  function ping(m) {
    tmsg.textContent = m;
    toast.classList.add('show');
    clearTimeout(toast._t);
    toast._t = setTimeout(() => toast.classList.remove('show'), 2200);
  }

  form.addEventListener('submit', sync);

  buildPalette();
  renderNodes();
  renderWires();
  inspWorkflow();
  sync();
})();
