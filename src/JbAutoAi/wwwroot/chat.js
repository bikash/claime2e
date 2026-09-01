// Claim assistant. Streams SSE from /api/chat and renders the legal passages the
// answer was grounded in.
//
// Runs once per [data-chat] panel: the floating bubble in the layout, plus the
// inline "Claim assistant" tab on the claim page. Each panel keeps its own
// history — they are separate conversations about the same claim.
(function () {
  const ctxEl = document.getElementById('claimCtx');
  const claim = ctxEl ? JSON.parse(ctxEl.textContent) : null;

  const toggle = document.getElementById('chatToggle');
  const floating = document.getElementById('chatPanel');
  if (toggle && floating) {
    toggle.addEventListener('click', () => {
      floating.hidden = !floating.hidden;
      toggle.setAttribute('aria-expanded', String(!floating.hidden));
      if (!floating.hidden) floating.querySelector('.chat-input').focus();
    });
  }

  document.querySelectorAll('[data-chat]').forEach(initChat);

  function initChat(panel) {
    const messages = panel.querySelector('.chat-messages');
    const form = panel.querySelector('.chat-form');
    const input = panel.querySelector('.chat-input');
    const send = panel.querySelector('.chat-send');
    const hint = panel.querySelector('.chat-hint');
    const history = [];

    if (claim && hint) {
      hint.textContent = 'Context: ' + claim.claim_number
        + (panel.dataset.drop ? ' · ' + panel.dataset.drop : '');
    }

    function bubble(role, text) {
      const el = document.createElement('div');
      el.className = 'bubble ' + (role === 'user' ? 'user' : 'bot');
      el.textContent = text || '';
      messages.appendChild(el);
      messages.scrollTop = messages.scrollHeight;
      return el;
    }

    function citations(list) {
      if (!list || !list.length) return;
      const el = document.createElement('div');
      el.className = 'tiny muted';
      el.append((messages.dataset.sources || 'Sources') + ': ');
      list.forEach((c, i) => {
        const a = document.createElement('a');
        a.className = 'cite';
        a.target = '_blank';
        a.rel = 'noopener';
        a.href = '/legal?cite=' + encodeURIComponent(c.chunk_id);
        a.textContent = c.citation;
        a.title = c.title;
        el.append(a);
        if (i < list.length - 1) el.append(' ');
      });
      messages.appendChild(el);
      messages.scrollTop = messages.scrollHeight;
    }

    async function ask(text) {
      history.push({ role: 'user', content: text });
      bubble('user', text);
      const out = bubble('bot', '…');
      send.disabled = true;
      let full = '';

      try {
        const resp = await fetch('/api/chat', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ claim_id: claim ? claim.id : null, messages: history }),
        });
        if (!resp.ok || !resp.body) throw new Error('chat failed: ' + resp.status);

        const reader = resp.body.getReader();
        const decoder = new TextDecoder();
        let buf = '';

        for (;;) {
          const { value, done } = await reader.read();
          if (done) break;
          buf += decoder.decode(value, { stream: true });

          let idx;
          while ((idx = buf.indexOf('\n\n')) !== -1) {
            const frame = buf.slice(0, idx);
            buf = buf.slice(idx + 2);
            const evLine = frame.split('\n').find((l) => l.startsWith('event:'));
            const dataLine = frame.split('\n').find((l) => l.startsWith('data:'));
            if (!dataLine) continue;
            const ev = evLine ? evLine.slice(6).trim() : 'message';
            let payload;
            try { payload = JSON.parse(dataLine.slice(5).trim()); } catch { continue; }

            if (ev === 'delta' && payload.text) {
              full += payload.text;
              out.textContent = full;
              messages.scrollTop = messages.scrollHeight;
            } else if (ev === 'citations') {
              citations(payload);
            } else if (ev === 'error') {
              out.textContent = '⚠ ' + (payload.message || 'error');
            }
          }
        }
        history.push({ role: 'assistant', content: full });
      } catch (e) {
        out.textContent = '⚠ ' + e.message;
      } finally {
        send.disabled = false;
        input.focus();
      }
    }

    form.addEventListener('submit', (e) => {
      e.preventDefault();
      const text = input.value.trim();
      if (!text) return;
      input.value = '';
      ask(text);
    });

    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); form.requestSubmit(); }
    });

    // Drop a file anywhere on the panel: ingest → analyse → decision → one-click
    // settle. Settling stays a button, not intent parsing — it moves money.
    if (claim) {
      const ds = panel.dataset;

      ['dragenter', 'dragover'].forEach((ev) => panel.addEventListener(ev, (e) => {
        e.preventDefault();
        panel.classList.add('over');
      }));
      ['dragleave', 'drop'].forEach((ev) => panel.addEventListener(ev, (e) => {
        e.preventDefault();
        if (ev === 'dragleave' && panel.contains(e.relatedTarget)) return;
        panel.classList.remove('over');
      }));
      panel.addEventListener('drop', (e) => {
        const files = Array.from(e.dataTransfer.files || []);
        if (files.length) runClaim(files);
      });

      async function runClaim(files) {
        bubble('user', '📎 ' + files.map((f) => f.name).join(', '));
        const out = bubble('bot', ds.uploading || 'Uploading…');
        send.disabled = true;
        try {
          const fd = new FormData();
          files.forEach((f) => fd.append('files', f));
          const up = await fetch('/claims/' + claim.id + '/upload', { method: 'POST', body: fd });
          if (!up.ok) throw new Error((await up.text()) || 'upload failed: ' + up.status);

          out.textContent = ds.analyzing || 'Analyzing…';
          const an = await fetch('/claims/' + claim.id + '/analyze', { method: 'POST' });
          if (!an.ok) throw new Error('analyze failed: ' + an.status);

          const st = await (await fetch('/api/claims/' + claim.id + '/state')).json();
          out.textContent = (ds.decision || 'Decision') + ': ' + (st.decision_outcome || '—')
            + (st.estimated_amount_eur != null ? ' · € ' + st.estimated_amount_eur : '');
          if (st.decision_outcome && st.status !== 'settled') offerSettle();
        } catch (err) {
          out.textContent = '⚠ ' + err.message;
        } finally {
          send.disabled = false;
        }
      }

      function offerSettle() {
        const el = bubble('bot', '');
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.textContent = ds.settle || 'Settle claim';
        el.appendChild(btn);
        btn.addEventListener('click', async () => {
          btn.disabled = true;
          const r = await fetch('/claims/' + claim.id + '/settle', { method: 'POST' });
          if (!r.ok) { btn.disabled = false; bubble('bot', '⚠ settle failed: ' + r.status); return; }
          bubble('bot', '✓ ' + (ds.settled || 'Claim settled'));
          setTimeout(() => location.reload(), 900);
        });
      }
    }
  }
})();
