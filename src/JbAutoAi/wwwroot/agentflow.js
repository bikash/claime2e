// Agent flow canvas: nodes you drag, ports you click to wire together.
//
// ponytail: hand-rolled on pointer events and one <svg>, not a graph library. The
// whole editor is add / move / connect / delete on a graph of at most a few dozen
// nodes — a library would be more code to configure than this is to write. Upgrade
// path if grouping, zoom or minimaps ever land: swap this file for one.
(function () {
  const form = document.getElementById('flowForm');
  if (!form) return;

  const canvas = document.getElementById('flowCanvas');
  const svg = document.getElementById('flowEdges');
  const empty = document.getElementById('flowEmpty');
  const count = document.getElementById('flowCount');
  const NODE_W = 190;
  const NODE_H = 54;

  let graph = { nodes: [], edges: [] };
  try {
    const stored = JSON.parse(form.dataset.flow || '{}');
    graph.nodes = Array.isArray(stored.nodes) ? stored.nodes : [];
    graph.edges = Array.isArray(stored.edges) ? stored.edges : [];
  } catch (e) { /* a corrupt column starts you on an empty canvas, not an error page */ }

  let linkingFrom = null;
  let seq = graph.nodes.length;

  // Inspector modal: click a node, a <dialog> opens to define its label, input,
  // output and prompt. The values live on the node itself ({input, output, prompt})
  // and travel inside the same flow JSON column. Native dialog — Esc closes it.
  let selected = null;
  const inspector = document.getElementById('flowInspector');
  const fiLabel = document.getElementById('fiLabel');
  const fiInput = document.getElementById('fiInput');
  const fiOutput = document.getElementById('fiOutput');
  const fiPrompt = document.getElementById('fiPrompt');

  function selectNode(id) {
    const n = nodeById(id);
    if (!n || !inspector) return;
    selected = id;
    fiLabel.value = n.label || '';
    fiInput.value = n.input || '';
    fiOutput.value = n.output || '';
    fiPrompt.value = n.prompt || '';
    render();
    if (!inspector.open) inspector.showModal();
  }

  function deselect() {
    if (inspector.open) inspector.close();
    selected = null;
    render();
  }

  // A drag ends in a click too (pointer capture retargets it to the node), so the
  // drag handler sets this to keep a move from popping the modal open.
  let suppressClick = false;

  if (inspector) {
    inspector.addEventListener('close', () => { selected = null; render(); });
    inspector.addEventListener('click', e => { if (e.target === inspector) inspector.close(); });
    document.getElementById('fiDone').addEventListener('click', () => inspector.close());
  }

  [[fiLabel, 'label'], [fiInput, 'input'], [fiOutput, 'output'], [fiPrompt, 'prompt']].forEach(([el, key]) => {
    el.addEventListener('input', () => {
      const n = nodeById(selected);
      if (!n) return;
      n[key] = el.value;
      render();
    });
  });

  function nextId() {
    let id;
    do { id = 'n' + (++seq); } while (graph.nodes.some(n => n.id === id));
    return id;
  }

  // x/y omitted (a click rather than a drop) tiles the node into the next free slot.
  function addNode(kind, label, x, y) {
    const row = graph.nodes.length;
    graph.nodes.push({
      id: nextId(), kind: kind, label: label,
      x: x === undefined ? 40 + (row % 3) * (NODE_W + 60) : Math.max(0, x),
      y: y === undefined ? 30 + Math.floor(row / 3) * (NODE_H + 50) : Math.max(0, y),
    });
    render();
  }

  function removeNode(id) {
    graph.nodes = graph.nodes.filter(n => n.id !== id);
    graph.edges = graph.edges.filter(e => e[0] !== id && e[1] !== id);
    if (selected === id) { deselect(); return; }
    render();
  }

  function connect(from, to) {
    if (from === to) return;
    if (graph.edges.some(e => e[0] === from && e[1] === to)) return;
    graph.edges.push([from, to]);
    render();
  }

  function nodeById(id) { return graph.nodes.find(n => n.id === id); }

  function drawEdges() {
    const ns = 'http://www.w3.org/2000/svg';
    svg.textContent = '';

    // Arrowhead marker, rebuilt with the rest of the svg on every draw.
    const defs = document.createElementNS(ns, 'defs');
    const marker = document.createElementNS(ns, 'marker');
    marker.setAttribute('id', 'flowArrow');
    marker.setAttribute('viewBox', '0 0 10 10');
    marker.setAttribute('refX', '9');
    marker.setAttribute('refY', '5');
    marker.setAttribute('markerWidth', '7');
    marker.setAttribute('markerHeight', '7');
    marker.setAttribute('orient', 'auto-start-reverse');
    const tip = document.createElementNS(ns, 'path');
    tip.setAttribute('d', 'M0 0 L10 5 L0 10 z');
    tip.setAttribute('class', 'flow-arrowhead');
    marker.appendChild(tip);
    defs.appendChild(marker);
    svg.appendChild(defs);

    graph.edges.forEach((e, i) => {
      const a = nodeById(e[0]), b = nodeById(e[1]);
      if (!a || !b) return;
      const x1 = a.x + NODE_W, y1 = a.y + NODE_H / 2;
      const x2 = b.x - 4, y2 = b.y + NODE_H / 2;
      const dx = Math.max(40, Math.abs(x2 - x1) / 2);
      const path = document.createElementNS(ns, 'path');
      path.setAttribute('d', 'M' + x1 + ' ' + y1 + ' C' + (x1 + dx) + ' ' + y1 +
                             ', ' + (x2 - dx) + ' ' + y2 + ', ' + x2 + ' ' + y2);
      path.setAttribute('class', 'flow-edge');
      path.setAttribute('marker-end', 'url(#flowArrow)');
      path.addEventListener('click', () => { graph.edges.splice(i, 1); render(); });
      svg.appendChild(path);
    });
  }

  function render() {
    canvas.querySelectorAll('.flow-node').forEach(el => el.remove());

    graph.nodes.forEach(n => {
      const el = document.createElement('div');
      el.className = 'flow-node ' + n.kind + (linkingFrom === n.id ? ' linking' : '')
                   + (selected === n.id ? ' selected' : '');
      el.style.left = n.x + 'px';
      el.style.top = n.y + 'px';
      el.dataset.id = n.id;
      const io = [n.input ? 'in: ' + n.input : '', n.output ? 'out: ' + n.output : '']
        .filter(Boolean).join(' · ');
      if (io) el.title = io;

      const inPort = document.createElement('button');
      inPort.type = 'button';
      inPort.className = 'port in';
      inPort.addEventListener('click', () => {
        if (linkingFrom) { connect(linkingFrom, n.id); linkingFrom = null; }
      });

      const outPort = document.createElement('button');
      outPort.type = 'button';
      outPort.className = 'port out';
      outPort.addEventListener('click', () => {
        linkingFrom = linkingFrom === n.id ? null : n.id;
        render();
      });

      const body = document.createElement('div');
      body.className = 'flow-node-body';
      const kind = document.createElement('span');
      kind.className = 'tiny muted';
      kind.textContent = n.kind;
      const label = document.createElement('span');
      label.className = 'small';
      label.textContent = n.label;
      body.append(kind, label);
      // On the node, not the body: pointer capture during a drag retargets the click
      // to the node element, so a listener further down would never fire.
      el.addEventListener('click', ev => {
        if (ev.target.closest('.port, .flow-del')) return;
        if (suppressClick) { suppressClick = false; return; }
        selectNode(n.id);
      });

      const del = document.createElement('button');
      del.type = 'button';
      del.className = 'flow-del';
      del.textContent = '✕';
      del.addEventListener('click', () => removeNode(n.id));

      el.append(inPort, body, del, outPort);
      drag(el, n);
      canvas.appendChild(el);
    });

    drawEdges();
    empty.hidden = graph.nodes.length > 0;
    count.textContent = (count.dataset.tpl || '{0} components · {1} connections')
      .replace('{0}', graph.nodes.length).replace('{1}', graph.edges.length);
    document.getElementById('flowJson').value = JSON.stringify(graph);
  }

  function drag(el, node) {
    el.addEventListener('pointerdown', ev => {
      if (ev.target.closest('.port, .flow-del')) return;
      const rect = canvas.getBoundingClientRect();
      // Offsets are canvas coordinates, so a scrolled canvas has to be accounted for.
      const dx = ev.clientX - rect.left + canvas.scrollLeft - node.x;
      const dy = ev.clientY - rect.top + canvas.scrollTop - node.y;
      const x0 = ev.clientX, y0 = ev.clientY;
      let moved = false;
      el.setPointerCapture(ev.pointerId);
      el.classList.add('dragging');

      function move(e) {
        if (Math.abs(e.clientX - x0) + Math.abs(e.clientY - y0) > 3) moved = true;
        node.x = Math.max(0, e.clientX - rect.left + canvas.scrollLeft - dx);
        node.y = Math.max(0, e.clientY - rect.top + canvas.scrollTop - dy);
        el.style.left = node.x + 'px';
        el.style.top = node.y + 'px';
        drawEdges();
      }
      function up() {
        suppressClick = moved;
        el.classList.remove('dragging');
        el.removeEventListener('pointermove', move);
        el.removeEventListener('pointerup', up);
        document.getElementById('flowJson').value = JSON.stringify(graph);
      }
      el.addEventListener('pointermove', move);
      el.addEventListener('pointerup', up);
    });
  }

  // Drag a component out of the palette and drop it where you want it. Native HTML5
  // drag-and-drop — the browser owns the ghost image and the cursor. Click still adds
  // one too, because HTML5 dragging does not fire on touch.
  document.querySelectorAll('.pal-item').forEach(item => {
    item.draggable = true;
    item.addEventListener('click', () => addNode(item.dataset.kind, item.dataset.label));
    item.addEventListener('dragstart', ev => {
      ev.dataTransfer.effectAllowed = 'copy';
      ev.dataTransfer.setData('text/plain',
        JSON.stringify({ kind: item.dataset.kind, label: item.dataset.label }));
      item.classList.add('dragging');
    });
    item.addEventListener('dragend', () => item.classList.remove('dragging'));
  });

  canvas.addEventListener('dragover', ev => {
    ev.preventDefault();
    ev.dataTransfer.dropEffect = 'copy';
    canvas.classList.add('over');
  });
  canvas.addEventListener('dragleave', ev => {
    if (!canvas.contains(ev.relatedTarget)) canvas.classList.remove('over');
  });
  canvas.addEventListener('drop', ev => {
    ev.preventDefault();
    canvas.classList.remove('over');
    let item;
    try { item = JSON.parse(ev.dataTransfer.getData('text/plain')); } catch (e) { return; }
    if (!item || !item.kind) return;
    const rect = canvas.getBoundingClientRect();
    addNode(item.kind, item.label,
            ev.clientX - rect.left + canvas.scrollLeft - NODE_W / 2,
            ev.clientY - rect.top + canvas.scrollTop - NODE_H / 2);
  });

  document.getElementById('flowClear').addEventListener('click', () => {
    graph = { nodes: [], edges: [] };
    linkingFrom = null;
    deselect();
  });

  render();
})();
