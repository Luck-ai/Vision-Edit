(() => {
  const KEY = 'visioneditcv.mobile.v2';
  const now = () => new Date().toISOString();

  const defaults = () => ({
    fileName: 'IMG_0427.jpg',
    server: { status: 'Connected', url: 'http://localhost:8000', at: now() },
    segmentation: { tool: 'Bounding Box', boxes: 0, promptText: '', masks: 0, masksHidden: false },
    // Matches MainWindowViewModel.SelectedEffect strings
    selectedEffect: 'Color Grading',
    activeEffectCategory: 'Color Grading',
    history: [{ name: 'Opened image', at: now(), detail: 'Original' }],
    // Matches MainWindowViewModel parameter names (cg/art/st/pb/pt/gs)
    params: {
      cgBrightness: 0,
      cgContrast: 10,
      cgTintStrength: 0,
      cgTintColor: '#22d3ee',
      cgIsForeground: true,

      artIsStylize: true,
      artSigmaS: 60,
      artSigmaR: 45,
      artShadeFactor: 5,

      stScale: 10,
      stRotation: 0,
      stBorderColor: '#22c55e',
      stBackgroundColor: '#000000',
      stThickness: 3,
      stShadowBlur: 32,
      stBackgroundMode: 0,
      stBackgroundImagePath: '',

      pbIntensity: 40,
      pbIsPixelate: true,
      pbIsForeground: true,

      ptBlurStrength: 51,
      ptFeatherAmount: 21,

      gsIsForeground: true
    }
  });

  const migrate = (s) => {
    const d = defaults();
    if (!s || typeof s !== 'object') return d;

    // Old prototype keys
    if (s.activeTool && !s.segmentation) {
      s.segmentation = { ...d.segmentation, tool: s.activeTool };
      delete s.activeTool;
    }

    s.server = { ...d.server, ...(s.server || {}) };
    s.segmentation = { ...d.segmentation, ...(s.segmentation || {}) };
    s.params = { ...d.params, ...(s.params || {}) };

    if (!('selectedEffect' in s)) s.selectedEffect = s.activeEffectCategory || d.selectedEffect;
    if (!('activeEffectCategory' in s)) s.activeEffectCategory = s.selectedEffect || d.activeEffectCategory;

    s.history = Array.isArray(s.history) && s.history.length ? s.history : d.history;
    s.fileName = s.fileName || d.fileName;

    return s;
  };

  const load = () => {
    try {
      const raw = localStorage.getItem(KEY);
      if (!raw) return defaults();
      return migrate(JSON.parse(raw));
    } catch {
      return defaults();
    }
  };

  const state = load();
  const save = () => localStorage.setItem(KEY, JSON.stringify(state));

  const qs = (s, el = document) => el.querySelector(s);
  const qsa = (s, el = document) => Array.from(el.querySelectorAll(s));

  const screen = document.body.getAttribute('data-screen');

  const isConnected = () => (state.server?.status || 'Connected') === 'Connected';

  const getValue = (k) => {
    if (k === 'promptText') return state.segmentation.promptText || '';
    if (k === 'serverUrl') return state.server.url || '';
    return state.params?.[k];
  };

  const setValue = (k, v) => {
    if (k === 'promptText') state.segmentation.promptText = String(v || '');
    else if (k === 'serverUrl') state.server.url = String(v || '');
    else {
      state.params = state.params || {};
      state.params[k] = v;
    }
  };

  // Populate shared bits
  qsa('.js-file-name').forEach(el => (el.textContent = state.fileName || 'Untitled'));
  qsa('.js-server-pill').forEach(el => {
    const dot = el.querySelector('.dot');
    const label = el.querySelector('.txt');
    if (!label) return;
    const st = state.server?.status || 'Connected';
    label.textContent = st.toUpperCase();
    if (dot) dot.style.background = st === 'Connected' ? 'var(--success)' : 'var(--warn)';
  });

  // Tabs
  qsa('[data-tab]').forEach(btn => {
    if (btn.getAttribute('data-tab') === screen) btn.classList.add('active');
  });

  // Reset prototype
  qsa('[data-action="reset-prototype"]').forEach(btn => {
    btn.addEventListener('click', () => {
      localStorage.removeItem(KEY);
      location.reload();
    });
  });

  // Press-and-hold Before/After
  const holdEls = qsa('[data-hold="before"]');
  const setBefore = (on) => document.body.classList.toggle('show-before', !!on);
  holdEls.forEach(el => {
    el.addEventListener('pointerdown', (e) => {
      el.setPointerCapture?.(e.pointerId);
      setBefore(true);
    });
    const off = () => setBefore(false);
    el.addEventListener('pointerup', off);
    el.addEventListener('pointercancel', off);
    el.addEventListener('pointerleave', off);
  });

  // Generic binding for inputs
  const numericKeys = new Set([
    'cgBrightness','cgContrast','cgTintStrength',
    'artSigmaS','artSigmaR','artShadeFactor',
    'stScale','stRotation','stThickness','stShadowBlur','stBackgroundMode',
    'pbIntensity','ptBlurStrength','ptFeatherAmount'
  ]);

  qsa('[data-param]').forEach(el => {
    const k = el.getAttribute('data-param');
    const t = (el.getAttribute('type') || '').toLowerCase();
    const isCheckbox = t === 'checkbox';
    const isRange = t === 'range';
    const isColor = t === 'color';
    const isSelect = el.tagName === 'SELECT';

    const current = getValue(k);

    if (isCheckbox) el.checked = current !== undefined ? !!current : el.checked;
    else if (isRange) el.value = String(current !== undefined ? Number(current) : Number(el.value));
    else if (isColor) el.value = String(current || el.value || '#22d3ee');
    else if (isSelect) el.value = String(current !== undefined ? current : el.value);
    else el.value = String(current !== undefined ? current : (el.value || ''));

    if (isRange) {
      const out = el.closest('.slider')?.querySelector('.js-val');
      if (out) out.textContent = String(el.value);
    }

    const handler = () => {
      let v;
      if (isCheckbox) v = !!el.checked;
      else if (isRange) v = Number(el.value);
      else if (isSelect) v = numericKeys.has(k) ? Number(el.value) : el.value;
      else v = el.value;

      if (numericKeys.has(k) && typeof v === 'string') v = Number(v);
      setValue(k, v);

      if (isRange) {
        const out = el.closest('.slider')?.querySelector('.js-val');
        if (out) out.textContent = String(v);
      }

      save();
      refreshUi();
    };

    el.addEventListener(isSelect || isCheckbox ? 'change' : 'input', handler);
  });

  const setPanelTitle = (title, sub) => {
    const t = qs('.js-panel-title');
    const s = qs('.js-panel-sub');
    if (t) t.textContent = title;
    if (s) s.textContent = sub;
  };

  const effectHelp = (name) => {
    switch (name) {
      case 'Color Grading':
        return 'Brightness/contrast + optional tint.';
      case 'Artistic Style':
        return 'Stylize or Pencil Sketch on the masked region.';
      case 'Sticker Generation':
        return 'Extract masked subject to a sticker; composite onto background.';
      case 'Pixelation & Blur':
        return 'Pixelate or blur the foreground/background (invert mask supported).';
      case 'Portrait Effect':
        return 'Depth-of-field style background blur with feathering.';
      case 'Grayscale':
        return 'Masked grayscale (foreground/background toggle).';
      default:
        return 'Pick an effect.';
    }
  };

  const showToolPanel = () => {
    const tool = state.segmentation.tool;
    qsa('[data-tool-panel]').forEach(p => {
      p.hidden = p.getAttribute('data-tool-panel') !== tool;
    });

    const boxCount = qs('[data-box-count]');
    if (boxCount) boxCount.textContent = String(state.segmentation.boxes || 0);

    const maskCount = qs('[data-mask-count]');
    if (maskCount) maskCount.textContent = String(state.segmentation.masks || 0);

    const hideBtn = qs('[data-action="toggle-hide-masks"]');
    if (hideBtn) hideBtn.textContent = state.segmentation.masksHidden ? 'Show masks' : 'Hide masks';

    setPanelTitle(tool, tool === 'Prompt' ? 'Text-prompt segmentation.' : 'Bounding-box segmentation.');
  };

  const setSelectedEffect = (name) => {
    state.selectedEffect = name || '';
    state.activeEffectCategory = name || state.activeEffectCategory || 'Color Grading';
    save();
    refreshUi();
  };

  const showEffectPanel = () => {
    const effect = state.selectedEffect;

    qsa('[data-effect-cat]').forEach(c => {
      c.classList.toggle('active', c.getAttribute('data-effect-cat') === effect);
    });

    const empty = qs('[data-effect-empty]');
    if (empty) empty.hidden = !!effect;

    qsa('[data-effect-panel]').forEach(p => {
      p.hidden = p.getAttribute('data-effect-panel') !== effect;
    });

    setPanelTitle(effect || 'Pick an effect', effect ? effectHelp(effect) : 'Select an effect chip to start.');
  };

  const canSegment = () => {
    if (!isConnected()) return false;
    const tool = state.segmentation.tool;
    if (tool === 'Bounding Box') return (state.segmentation.boxes || 0) > 0;
    if (tool === 'Prompt') return !!(state.segmentation.promptText || '').trim();
    return false;
  };

  const doSegment = () => {
    if (!isConnected()) {
      setPanelTitle(state.segmentation.tool, 'Not connected — set the API URL in Settings.');
      return;
    }

    if (!canSegment()) {
      setPanelTitle(state.segmentation.tool, state.segmentation.tool === 'Prompt' ? 'Enter a prompt first.' : 'Add at least one box first.');
      return;
    }

    state.segmentation.masks = Math.max(0, Number(state.segmentation.masks || 0)) + 1;

    // Matches desktop behaviour: new segmentation starts with no effect selected.
    state.selectedEffect = '';

    state.history = state.history || [];
    state.history.push({
      name: `Segmented (${state.segmentation.tool})`,
      at: now(),
      detail: state.segmentation.tool === 'Prompt'
        ? `prompt:"${state.segmentation.promptText.trim().slice(0, 50)}"`
        : `boxes:${state.segmentation.boxes}`
    });

    save();
    refreshUi();
  };

  const effectDetail = (name) => {
    const p = state.params || {};
    switch (name) {
      case 'Color Grading':
        return `brightness:${p.cgBrightness} · contrast:${(Number(p.cgContrast) / 10).toFixed(1)}x · tint:${p.cgTintStrength}% · fg:${p.cgIsForeground ? 'yes' : 'no'}`;
      case 'Artistic Style':
        return `${p.artIsStylize ? 'stylize' : 'pencil'} · sigmaS:${p.artSigmaS} · sigmaR:${p.artSigmaR} · shade:${p.artShadeFactor}`;
      case 'Sticker Generation':
        return `scale:${(Number(p.stScale) / 10).toFixed(1)}x · rot:${p.stRotation}° · thick:${p.stThickness} · bgMode:${p.stBackgroundMode}`;
      case 'Pixelation & Blur':
        return `${p.pbIsPixelate ? 'pixelate' : 'blur'} · intensity:${p.pbIntensity} · fg:${p.pbIsForeground ? 'yes' : 'no'}`;
      case 'Portrait Effect':
        return `blur:${p.ptBlurStrength} · feather:${p.ptFeatherAmount}`;
      case 'Grayscale':
        return `fg:${p.gsIsForeground ? 'yes' : 'no'}`;
      default:
        return '—';
    }
  };

  const doApply = (btn) => {
    if ((state.segmentation.masks || 0) <= 0) {
      btn.textContent = 'Need mask';
      setTimeout(() => (btn.textContent = 'Apply'), 900);
      return;
    }

    if (!state.selectedEffect) {
      btn.textContent = 'Pick effect';
      setTimeout(() => (btn.textContent = 'Apply'), 900);
      return;
    }

    state.history = state.history || [];
    state.history.push({
      name: `Applied ${state.selectedEffect}`,
      at: now(),
      detail: effectDetail(state.selectedEffect)
    });

    save();
    btn.textContent = 'Applied';
    setTimeout(() => (btn.textContent = 'Apply'), 900);
  };

  // Screen-specific wiring
  if (screen === 'editor') {
    qsa('[data-tool]').forEach(t => {
      const tool = t.getAttribute('data-tool');
      t.classList.toggle('active', tool === state.segmentation.tool);
      t.addEventListener('click', () => {
        state.segmentation.tool = tool;
        save();
        qsa('[data-tool]').forEach(x => x.classList.remove('active'));
        t.classList.add('active');
        showToolPanel();
      });
    });

    qs('[data-action="add-box"]')?.addEventListener('click', () => {
      state.segmentation.boxes = Math.max(0, Number(state.segmentation.boxes || 0)) + 1;
      save();
      showToolPanel();
    });

    qs('[data-action="clear-boxes"]')?.addEventListener('click', () => {
      state.segmentation.boxes = 0;
      save();
      showToolPanel();
    });

    qsa('[data-action="segment"]').forEach(b => b.addEventListener('click', doSegment));

    qs('[data-action="toggle-hide-masks"]')?.addEventListener('click', () => {
      state.segmentation.masksHidden = !state.segmentation.masksHidden;
      save();
      showToolPanel();
    });

    qs('[data-action="clear-masks"]')?.addEventListener('click', () => {
      state.segmentation.masks = 0;
      save();
      showToolPanel();
    });

    showToolPanel();
  }

  if (screen === 'effects') {
    qsa('[data-effect-cat]').forEach(c => {
      const name = c.getAttribute('data-effect-cat');
      c.addEventListener('click', () => setSelectedEffect(name));
    });

    const apply = qs('[data-action="apply"]');
    if (apply) apply.addEventListener('click', () => doApply(apply));

    showEffectPanel();
  }

  if (screen === 'settings') {
    qs('[data-action="toggle-connection"]')?.addEventListener('click', () => {
      state.server.status = isConnected() ? 'Disconnected' : 'Connected';
      state.server.at = now();
      save();
      refreshUi();
      location.reload();
    });
  }

  if (screen === 'history') {
    const historyHost = qs('[data-history]');
    if (historyHost) {
      const items = (state.history || []).slice().reverse();
      historyHost.innerHTML = items.map((it, idx) => {
        const t = it.at ? new Date(it.at) : null;
        const ts = t ? t.toLocaleString([], { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' }) : '—';
        return `
          <div class="card">
            <div class="row2">
              <div>
                <div class="k">STEP ${String(items.length - idx).padStart(2, '0')}</div>
                <div class="h">${escapeHtml(it.name || '—')}</div>
              </div>
              <div class="mono" style="color:rgba(255,255,255,.55);font-size:11px">${escapeHtml(ts)}</div>
            </div>
            <div class="p">${escapeHtml(it.detail || '—')}</div>
            <div class="sep"></div>
            <div class="row2">
              <button class="btn small ghost" data-hold="before">Hold to compare</button>
              <a class="a" href="editor.html">Open in Masks →</a>
            </div>
          </div>
        `;
      }).join('');
    }

    qs('[data-action="clear-history"]')?.addEventListener('click', () => {
      state.history = [{ name: 'Opened image', at: now(), detail: 'Original' }];
      save();
      location.reload();
    });
  }

  function refreshUi() {
    // keep server pill in sync across screens
    qsa('.js-server-pill').forEach(el => {
      const dot = el.querySelector('.dot');
      const label = el.querySelector('.txt');
      if (!label) return;
      const st = state.server?.status || 'Connected';
      label.textContent = st.toUpperCase();
      if (dot) dot.style.background = st === 'Connected' ? 'var(--success)' : 'var(--warn)';
    });

    if (screen === 'editor') showToolPanel();
    if (screen === 'effects') showEffectPanel();
  }

  function escapeHtml(s) {
    return String(s)
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }
})();
