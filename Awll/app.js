    let currentView = 'audit';
    let currentTheme = 'atelier-suisse';
    let allItems = [];
    let protectedItems = [];
    let drivesList = [];
    let activeDrives = new Set();     // Multi-sélection affichage
    let scannedDrives = new Set();    // Disques inclus dans le dernier audit
    let currentAuditCat = 'all';
    let quickFilter = 'all';
    let hideRunning = false;
    let duplicateGroups = [];
    let selectedDuplicateIds = new Set();
    let duplicateCustomFolders = [];
    let auditCustomFolders = [];
    let auditScanDrives = new Set();
    let dupScanDrives = new Set();
    let auditInProgress = false;
    let dupInProgress = false;
    let unseenResults = { audit: false, duplicates: false };
    let sortField = 'recommended';
    let sortAsc = true;
    let mainExpanded = true;
    let extraExpanded = false;
    let personalExpanded = false;
    let personalSubExpanded = {};
    let currentSettings = { geminiApiKey: '', enableAI: true };
    let activeResearchItem = null;
    let activeResearchRequestId = 0;
    let aiStatusByPath = {};
    let aiErrorByPath = {};
    let activeTasksProgress = {};
    let aiReportCache = {};
    let localReportCache = {};
    let selectedDeleteIds = new Set();
    let pendingDirectDeleteItems = null; // Suppression depuis Explorateur ou Doublons

    // Space Explorer state & cache
    let currentTreeNode = null;
    let currentTreePath = '';
    let explorerLoading = false;
    let treeCache = {};
    let pendingTreeScans = {};
    let explorerHistory = [];
    let explorerRequestCounter = 0;
    let activeExplorerRequestId = 0;
    let explorerPreindexing = false;
    let indexedDriveRoots = new Set();

    // Palette équilibrée haute lisibilité (clair & sombre) pour la barre Treemap
    const TREEMAP_COLORS = [
      '#B84B2A', // terracotta
      '#2D6A4F', // vert pin
      '#2451A6', // bleu cobalt
      '#B57310', // ambre mat
      '#5C7080', // acier
      '#8C4A32', // brun terre
      '#4B6E5B', // sauge
      '#736B62'  // pierre
    ];

    /* === ICONES VECTORIELLES GEOMETRIQUES (1.65px, ZERO EMOJI) === */
    const SVG_PATHS = {
      'hard-drive': '<rect x="2" y="14" width="20" height="7" rx="2"/><path d="M5.45 5.11 2 14h20l-3.45-8.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/><line x1="6" y1="17.5" x2="6.01" y2="17.5"/><line x1="10" y1="17.5" x2="10.01" y2="17.5"/>',
      'folder': '<path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z"/>',
      'file': '<path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/>',
      'sparkles': '<path d="M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z"/><path d="M20 3v4"/><path d="M22 5h-4"/>',
      'check': '<path d="M20 6 9 17l-5-5"/>',
      'spinner': '<path d="M21 12a9 9 0 1 1-6.219-8.56"/>',
      'sun': '<circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/>',
      'moon': '<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/>',
      'search': '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
      'trash': '<path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/>',
      'rotate-ccw': '<path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/>',
      'alert-triangle': '<path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
      'chevron-down': '<path d="m6 9 6 6 6-6"/>',
      'chevron-right': '<path d="m9 18 6-6-6-6"/>',
      'play': '<polygon points="6 3 20 12 6 21 6 3"/>',
      'shield-check': '<path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/><path d="m9 12 2 2 4-4"/>',
      'shield': '<path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/>',
      'layers': '<path d="m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z"/><path d="m22 17.65-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65"/><path d="m22 12.65-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65"/>',
      'info': '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/>',
      'lock': '<rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>',
      'clock': '<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>'
    };

    function icon(name, cls = 'w-3.5 h-3.5') {
      const p = SVG_PATHS[name] || '';
      const spin = name === 'spinner' ? ' icon-spin' : '';
      return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" class="${cls} shrink-0${spin}">${p}</svg>`;
    }

    /* === GESTION THEME (ATELIER SUISSE / SUISSE NOCTURNE + SYNC WINDOWS) === */
    function initTheme() {
      let saved = '';
      try {
        saved = localStorage.getItem('awll-theme') || '';
      } catch { }

      if (saved === 'suisse-nocturne' || saved === 'atelier-suisse') {
        applyTheme(saved, false);
        return;
      }

      const prefersDark = !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
      applyTheme(prefersDark ? 'suisse-nocturne' : 'atelier-suisse', false);
    }

    function applyTheme(themeId, persist = true) {
      currentTheme = themeId === 'suisse-nocturne' ? 'suisse-nocturne' : 'atelier-suisse';
      const isDark = currentTheme === 'suisse-nocturne';
      const body = document.getElementById('app-body') || document.body;
      if (body) {
        body.classList.remove('theme-atelier-suisse', 'theme-suisse-nocturne');
        body.classList.add(isDark ? 'theme-suisse-nocturne' : 'theme-atelier-suisse');
      }
      if (persist) {
        try { localStorage.setItem('awll-theme', currentTheme); } catch { }
      }
      renderSidebarThemeToggle();
      postToBridge({ action: 'setTheme', isDark });
    }

    function toggleTheme() {
      applyTheme(currentTheme === 'atelier-suisse' ? 'suisse-nocturne' : 'atelier-suisse', true);
    }

    function renderSidebarThemeToggle() {
      const el = document.getElementById('sidebar-theme-toggle');
      if (!el) return;
      const isDark = currentTheme === 'suisse-nocturne';
      const label = isDark ? 'Suisse Nocturne (Sombre) — cliquer pour passer en mode Clair' : 'Atelier Suisse (Clair) — cliquer pour passer en mode Sombre';
      el.innerHTML = `
        <button onclick="toggleTheme()" title="${label}" class="spring p-0.5 rounded-lg inline-flex items-center cursor-pointer" style="background: var(--app-surface-2); border: 1px solid var(--app-border);">
          <span class="spring p-1.5 rounded-md flex items-center justify-center" style="background: ${!isDark ? 'var(--app-surface)' : 'transparent'}; color: ${!isDark ? 'var(--app-text)' : 'var(--app-muted)'}; box-shadow: ${!isDark ? '0 1px 2px rgba(0,0,0,0.08)' : 'none'};">
            ${icon('sun', 'w-3.5 h-3.5')}
          </span>
          <span class="spring p-1.5 rounded-md flex items-center justify-center" style="background: ${isDark ? 'var(--app-surface)' : 'transparent'}; color: ${isDark ? 'var(--app-text)' : 'var(--app-muted)'}; box-shadow: ${isDark ? '0 1px 2px rgba(0,0,0,0.22)' : 'none'};">
            ${icon('moon', 'w-3.5 h-3.5')}
          </span>
        </button>
      `;
    }

    /* === INITIALISATION & RACCOURCIS CLAVIER === */
    window.addEventListener('DOMContentLoaded', () => {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener('message', onBridgeMessage);
      }
      initTheme();
      if (window.chrome && window.chrome.webview) {
        postToBridge({ action: 'getDrives' });
        postToBridge({ action: 'getProtected' });
        postToBridge({ action: 'getSettings' });
        postToBridge({ action: 'getHistory' });
        postToBridge({ action: 'getAuditSnapshots' });
        postToBridge({ action: 'getAiCachedPaths' });
      }
    });

    document.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        if (currentView !== 'audit') switchView('audit');
        const input = document.getElementById('search-input');
        if (input) { input.focus(); input.select(); }
        return;
      }
      const tag = (document.activeElement?.tagName || '').toUpperCase();
      if (e.key === '/' && tag !== 'INPUT' && tag !== 'TEXTAREA' && tag !== 'SELECT') {
        if (currentView === 'audit') {
          e.preventDefault();
          const input = document.getElementById('search-input');
          if (input) { input.focus(); input.select(); }
        }
        return;
      }
      if (e.key === 'Escape') {
        closeResearchModal();
        closeCleanModal();
        closeAiSettingsModal();
      }
    });

    function postToBridge(msg) {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(msg);
      }
    }

    function safeEnc(s) {
      return encodeURIComponent(s || '').replace(/'/g, '%27');
    }

    function escapeHtml(str) {
      return String(str || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }

    function onBridgeMessage(event) {
      const data = event.data;
      if (!data) return;
      const type = data.type || data.Type;

      if (type === 'drivesUpdate') {
        drivesList = data.drives || data.Drives || [];
        if (activeDrives.size === 0) {
          drivesList.forEach(d => activeDrives.add((d.name || d.Name || '').toUpperCase()));
        }
        if (auditScanDrives.size === 0) {
          drivesList.forEach(d => auditScanDrives.add((d.name || d.Name || '').toUpperCase()));
        }
        if (dupScanDrives.size === 0) {
          drivesList.forEach(d => dupScanDrives.add((d.name || d.Name || '').toUpperCase()));
        }
        renderSidebarDrives();
        renderExplorerDrivePills();
        renderScanDrivePills('audit');
        renderScanDrivePills('duplicates');
        updateHeaderSubtitle();
      } else if (type === 'auditProgress') {
        showProgress(data.text || data.Text || 'Analyse en cours...', data.pct ?? data.Pct ?? 0);
      } else if (type === 'auditResult') {
        hideProgress();
        allItems = data.items || data.Items || [];
        selectedDeleteIds.clear();
        syncProtectedStatus();
        document.getElementById('analytics-bar').classList.remove('hidden');
        document.getElementById('audit-tabs-bar').classList.remove('hidden');
        document.getElementById('audit-tabs-bar').classList.add('flex');
        document.getElementById('toolbar-controls').classList.remove('hidden');
        document.getElementById('toolbar-controls').classList.add('flex');
        document.getElementById('sidebar-audit-sections').classList.remove('hidden');
        renderSidebarDrives();
        refreshAuditUI();
        renderAuditEvolutionBanner(data.previousSnapshot || data.PreviousSnapshot);
        if (currentView !== 'audit') {
          unseenResults.audit = true;
          updateNavUnseenBadges();
        }
        showToast(`Audit terminé : ${allItems.length} éléments analysés.`, 'success');
      } else if (type === 'duplicatesProgress') {
        showDupProgress(data.text || data.Text || 'Analyse des doublons...', data.pct ?? data.Pct ?? 0);
      } else if (type === 'duplicatesResult') {
        hideDupProgress();
        duplicateGroups = data.groups || data.Groups || [];
        renderDuplicatesView();
        if (currentView !== 'duplicates') {
          unseenResults.duplicates = true;
          updateNavUnseenBadges();
        }
        showToast(`Doublons : ${duplicateGroups.length} groupe(s) détecté(s).`, 'success');
      } else if (type === 'foldersPicked') {
        const target = data.target || data.Target || 'duplicates';
        const paths = data.paths || data.Paths || [];
        const arr = target === 'audit' ? auditCustomFolders : duplicateCustomFolders;
        paths.forEach(p => {
          if (p && !arr.some(x => x.toLowerCase() === p.toLowerCase())) {
            arr.push(p);
          }
        });
        renderCustomFolders(target);
      } else if (type === 'duplicateFolderPicked') {
        const p = data.path || data.Path;
        if (p && !duplicateCustomFolders.some(x => x.toLowerCase() === p.toLowerCase())) {
          duplicateCustomFolders.push(p);
          renderCustomFolders('duplicates');
        }
      } else if (type === 'winSxSResult') {
        renderWinSxSResult(data.report || data.Report);
      } else if (type === 'auditSnapshots') {
        renderHomeLastSnapshot(data.snapshots || data.Snapshots || []);
      } else if (type === 'aiCachedPaths') {
        const paths = data.paths || data.Paths || [];
        paths.forEach(p => {
          if (p) {
            const k = p.toLowerCase();
            if (!aiStatusByPath[k]) aiStatusByPath[k] = 'done';
          }
        });
        if (currentView === 'audit' && allItems.length > 0) renderItems();
      } else if (type === 'protectedList') {
        protectedItems = data.items || data.Items || [];
        syncProtectedStatus();
        document.getElementById('nav-count-protected').innerText = protectedItems.length;
        renderProtectedView();
        if (allItems.length > 0) refreshAuditUI();
      } else if (type === 'researchResult') {
        const rep = data.report || data.Report;
        const rKey = (rep && (rep.itemPath || rep.ItemPath)) ? (rep.itemPath || rep.ItemPath).toLowerCase() : '';
        const itemName = rep?.itemName || rep?.ItemName || 'Élément';
        const isBackgroundAi = data.source === 'aiBackground';
        if (rKey) {
          if (rep.isAiEnriched || rep.IsAiEnriched) {
            aiReportCache[rKey] = rep;
            aiStatusByPath[rKey] = 'done';
            delete aiErrorByPath[rKey];
            const aiRisk = rep.riskPct ?? rep.RiskPct;
            if (typeof aiRisk === 'number') {
              allItems.forEach(it => {
                if ((it.path || it.Path || '').toLowerCase() === rKey) {
                  it.riskPct = aiRisk;
                  it.riskLevel = aiRisk <= 15 ? 'Quasi Nul' : (aiRisk <= 35 ? 'Faible' : (aiRisk <= 65 ? 'Modéré' : 'Élevé'));
                }
              });
            }
            if (currentView === 'audit' && allItems.length > 0) renderItems();
            if (isBackgroundAi) {
              showToast(`Analyse IA terminée : ${itemName}`, 'success');
            }
          } else if (data.aiError) {
            aiStatusByPath[rKey] = 'error';
            aiErrorByPath[rKey] = data.aiError;
            if (currentView === 'audit' && allItems.length > 0) renderItems();
          } else {
            localReportCache[rKey] = rep;
          }
        }

        const modalOpen = !document.getElementById('modal-research').classList.contains('hidden');
        const curKey = activeResearchItem?.path?.toLowerCase() || '';
        const reqId = data.requestId ?? 0;
        const isCurrentItem = activeResearchItem && (!rKey || !curKey || rKey === curKey);

        if (!isCurrentItem) {
          if (data.aiError) showToast(`IA (${itemName}) : ${data.aiError}`, 'warn');
          return;
        }
        if (isBackgroundAi && !modalOpen && (rep?.isAiEnriched || rep?.IsAiEnriched || data.aiError)) {
          if (data.aiError) showToast(`IA (${itemName}) : ${data.aiError}`, 'warn');
          return;
        }
        if (reqId > 0 && reqId !== activeResearchRequestId && !modalOpen) {
          return;
        }

        if (data.aiError) {
          resetAiDeepButton(!!aiReportCache[rKey]);
          showToast(data.aiError, 'warn');
          return;
        }

        showResearchModal(rep);
      } else if (type === 'folderTreeProgress') {
        const pKey = normalizeTreeKey(data.path || data.Path || '');
        if (pKey) {
          pendingTreeScans[pKey] = { requestId: data.requestId, text: data.text || '', pct: data.pct ?? 0 };
        }
        if (data.requestId === activeExplorerRequestId || pKey === normalizeTreeKey(currentTreePath)) {
          updateExplorerProgressUI(data.text || 'Analyse en cours...', data.pct ?? 0);
        }
      } else if (type === 'folderTreeResult') {
        const node = data.node || data.Node;
        if (Array.isArray(data.cachedDrives)) {
          indexedDriveRoots = new Set(data.cachedDrives.map(d => normalizeTreeKey(d)));
        }
        if (node) {
          const nKey = normalizeTreeKey(node.path || node.Path || '');
          delete pendingTreeScans[nKey];
          treeCache[nKey] = node;
          renderExplorerDrivePills();
          if (!data.isPreindex && (data.requestId === activeExplorerRequestId || nKey === normalizeTreeKey(currentTreePath))) {
            explorerLoading = false;
            currentTreeNode = node;
            currentTreePath = node.path || node.Path || currentTreePath;
            renderExplorerTree();
          }
        }
      } else if (type === 'explorerPreindexProgress') {
        const pKey = normalizeTreeKey(data.path || data.Path || '');
        if (pKey) {
          pendingTreeScans[pKey] = { requestId: 0, text: data.text || '', pct: data.pct ?? 0 };
        }
        renderExplorerDrivePills();
        const btn = document.getElementById('btn-explorer-preindex');
        if (btn) {
          const drv = data.drive || pKey.slice(0, 2);
          btn.disabled = true;
          btn.innerHTML = `${icon('spinner', 'w-3 h-3')} <span>Indexation ${escapeHtml(drv)} ${Math.round(data.pct ?? 0)}%</span>`;
        }
      } else if (type === 'explorerPreindexDone') {
        explorerPreindexing = false;
        if (Array.isArray(data.cachedDrives)) {
          indexedDriveRoots = new Set(data.cachedDrives.map(d => normalizeTreeKey(d)));
        }
        const btn = document.getElementById('btn-explorer-preindex');
        if (btn) {
          btn.disabled = false;
          btn.innerHTML = `${icon('layers', 'w-3 h-3')} <span>Pré-indexer les disques</span>`;
        }
        renderExplorerDrivePills();
        showToast('Tous les disques sont indexés en mémoire (navigation instantanée).', 'success');
      } else if (type === 'cleanResult') {
        onCleanCompleted(data);
      } else if (type === 'appSettings') {
        currentSettings = data.settings || data.Settings || { geminiApiKey: '', enableAI: true };
        updateAiHeaderUI();
      } else if (type === 'apiKeyTestResult') {
        handleApiKeyTestResult(data.success, data.message);
      } else if (type === 'cleanHistory') {
        renderHistoryView(data.history || data.History || []);
      }
    }

    function showToast(message, kind = 'info') {
      const container = document.getElementById('toast-container');
      if (!container) return;
      const accent = kind === 'success'
        ? 'var(--app-safe)'
        : (kind === 'warn' ? 'var(--app-warn)' : 'var(--app-accent)');
      const icName = kind === 'success' ? 'check' : (kind === 'warn' ? 'alert-triangle' : 'info');
      const el = document.createElement('div');
      el.className = 'toast-anim surface-card pointer-events-auto px-4 py-2.5 rounded-xl text-xs font-medium flex items-center gap-2.5';
      el.style.borderColor = accent;
      el.style.color = 'var(--app-text)';
      el.style.boxShadow = 'var(--app-shadow)';
      el.innerHTML = `<span style="color:${accent}; display:flex; align-items:center;">${icon(icName, 'w-3.5 h-3.5')}</span><span>${escapeHtml(message)}</span>`;
      container.appendChild(el);
      setTimeout(() => {
        el.style.opacity = '0';
        el.style.transition = 'opacity 0.25s';
        setTimeout(() => el.remove(), 260);
      }, 3600);
    }

    // ================= VIEW SWITCHING =================
    function updateNavUnseenBadges() {
      ['audit', 'duplicates'].forEach(key => {
        const navBtn = document.getElementById(`nav-${key}`);
        const dotEl = document.getElementById(`nav-dot-${key}`);
        const countEl = document.getElementById(`nav-count-${key}`);
        const activeUnseen = !!unseenResults[key] && currentView !== key;
        if (navBtn) {
          navBtn.classList.toggle('has-unseen', activeUnseen);
          if (activeUnseen) {
            navBtn.setAttribute('title', 'Nouveaux résultats prêts à consulter');
          } else {
            navBtn.removeAttribute('title');
          }
        }
        if (dotEl) dotEl.classList.toggle('hidden', !activeUnseen);
        if (countEl) countEl.classList.toggle('unseen', activeUnseen);
      });
    }

    function switchView(view) {
      currentView = view;
      if (unseenResults[view]) {
        unseenResults[view] = false;
      }
      updateNavUnseenBadges();
      ['audit', 'explorer', 'duplicates', 'protected', 'history'].forEach(v => {
        document.getElementById(`view-${v}`).classList.toggle('hidden', v !== view);
        document.getElementById(`nav-${v}`).classList.toggle('active', v === view);
      });

      const titles = {
        audit: 'Audit Intelligent Multi-Disques',
        explorer: "Explorateur d'Espace & Arborescence",
        duplicates: 'Détecteur de Doublons',
        protected: 'Liste Protégée Permanente',
        history: 'Historique des Nettoyages'
      };
      document.getElementById('view-title').innerText = titles[view] || 'Awll';
      document.getElementById('topbar-search-wrap').classList.toggle('hidden', view !== 'audit');
      updateHeaderSubtitle();

      if (view === 'explorer' && !currentTreeNode && drivesList.length > 0) {
        const firstActive = Array.from(activeDrives)[0] || (drivesList[0].name || drivesList[0].Name || 'C:');
        navigateExplorer(firstActive.endsWith('\\') ? firstActive : `${firstActive}\\`);
      } else if (view === 'history') {
        postToBridge({ action: 'getHistory' });
        postToBridge({ action: 'getAuditSnapshots' });
        postToBridge({ action: 'getAiCachedPaths' });
      }
      updateSelectionCalc();
    }

    function updateHeaderSubtitle() {
      const el = document.getElementById('view-subtitle');
      if (!el) return;
      if (currentView === 'audit') {
        const list = Array.from(activeDrives).sort().join(', ');
        el.innerText = list ? `Disques affichés : ${list}` : 'Aucun disque actif';
      } else if (currentView === 'explorer') {
        el.innerText = currentTreePath ? `Dossier analysé : ${currentTreePath}` : "Visualisez quels dossiers consomment le plus d'espace";
      } else if (currentView === 'duplicates') {
        const list = Array.from(activeDrives).sort().join(', ');
        el.innerText = list
          ? `Comparaison binaire exacte SHA-256 • Disques affichés : ${list}`
          : 'Comparaison binaire exacte par empreinte SHA-256';
      } else if (currentView === 'protected') {
        el.innerText = 'Éléments protégés contre toute suppression accidentelle';
      } else if (currentView === 'history') {
        el.innerText = 'Journal cumulatif des sessions de nettoyage';
      }
    }

    // ================= MULTI-DRIVE SIDEBAR SELECTION =================
    function renderSidebarDrives() {
      const container = document.getElementById('sidebar-drives');
      if (!drivesList || drivesList.length === 0) return;

      container.innerHTML = drivesList.map(d => {
        const name = (d.name || d.Name || 'C:').toUpperCase();
        const label = d.label || d.Label || 'Disque Local';
        const freeGB = d.freeGB ?? d.FreeGB ?? 0;
        const totalGB = d.totalGB ?? d.TotalGB ?? 0;
        const pctUsed = d.pctUsed ?? d.PctUsed ?? 0;
        const isOn = activeDrives.has(name);
        const notYetScanned = allItems.length > 0 && isOn && scannedDrives.size > 0 && !scannedDrives.has(name);
        const barColor = pctUsed > 85 ? 'var(--app-danger)' : (pctUsed > 70 ? 'var(--app-warn)' : 'var(--app-safe)');

        return `
          <div onclick="toggleDrive('${safeEnc(name)}')" class="drive-pill ${isOn ? 'active' : ''} cursor-pointer rounded-xl p-2.5 space-y-1.5">
            <div class="flex items-center justify-between gap-1.5">
              <div class="flex items-center gap-2 min-w-0">
                <div class="w-4 h-4 rounded flex items-center justify-center shrink-0" style="background:${isOn ? 'var(--app-accent)' : 'var(--app-surface-2)'}; border:1px solid ${isOn ? 'var(--app-accent)' : 'var(--app-border-strong)'}; color:#FFFFFF;">
                  ${isOn ? icon('check', 'w-2.5 h-2.5') : ''}
                </div>
                <span class="font-mono text-xs font-bold" style="color:var(--app-text);">${escapeHtml(name)}</span>
                <span class="text-[10px] truncate" style="color:var(--app-muted);">${escapeHtml(label)}</span>
              </div>
              <button onclick="event.stopPropagation(); isolateDrive('${safeEnc(name)}')" title="Afficher uniquement ${escapeHtml(name)}" class="text-[10px] px-1.5 py-0.5 rounded spring hover:opacity-80" style="background:var(--app-surface-2); color:var(--app-muted); border:1px solid var(--app-border);">Seul</button>
            </div>
            <div class="w-full h-1.5 rounded-full overflow-hidden" style="background:var(--app-ring-track);">
              <div class="h-full rounded-full spring" style="width:${pctUsed}%; background:${barColor};"></div>
            </div>
            <div class="flex items-center justify-between text-[10px] font-mono tabular-nums">
              <span style="color:var(--app-muted);">${freeGB.toFixed(0)} / ${totalGB.toFixed(0)} Go libres</span>
              ${notYetScanned ? '<span style="color:var(--app-warn);">Non scanné</span>' : `<span class="font-semibold" style="color:${barColor};">${pctUsed}%</span>`}
            </div>
          </div>
        `;
      }).join('');
    }

    function toggleDrive(encName) {
      const name = decodeURIComponent(encName).toUpperCase();
      if (activeDrives.has(name)) {
        if (activeDrives.size === 1) {
          showToast('Au moins un disque doit rester sélectionné.', 'warn');
          return;
        }
        activeDrives.delete(name);
      } else {
        activeDrives.add(name);
      }
      onActiveDrivesChanged();
    }

    function isolateDrive(encName) {
      const name = decodeURIComponent(encName).toUpperCase();
      activeDrives.clear();
      activeDrives.add(name);
      onActiveDrivesChanged();
    }

    function selectAllDrives() {
      drivesList.forEach(d => activeDrives.add((d.name || d.Name || '').toUpperCase()));
      onActiveDrivesChanged();
    }

    function onActiveDrivesChanged() {
      renderSidebarDrives();
      updateHeaderSubtitle();
      if (allItems.length > 0) {
        refreshAuditUI();
      }
      if (duplicateGroups.length > 0) {
        renderDuplicatesView();
      }
    }

    // ================= AUDIT =================
    function startAudit() {
      if (auditInProgress) return;
      if (auditCustomFolders.length === 0 && auditScanDrives.size === 0) {
        showToast('Sélectionnez au moins un disque ou un dossier à analyser.', 'warn');
        return;
      }
      auditInProgress = true;
      unseenResults.audit = false;
      updateNavUnseenBadges();
      if (auditCustomFolders.length === 0) {
        scannedDrives = new Set(auditScanDrives);
        activeDrives = new Set(auditScanDrives);
      } else {
        scannedDrives = new Set(drivesList.map(d => (d.name || d.Name || '').toUpperCase()));
        activeDrives = new Set(scannedDrives);
      }
      renderSidebarDrives();
      renderScanDrivePills('audit');
      const btn = document.getElementById('btn-audit-launch');
      const pickBtn = document.getElementById('btn-audit-pick-folders');
      if (btn) {
        btn.disabled = true;
        btn.innerHTML = `${icon('spinner', 'w-3.5 h-3.5')} <span>Audit en cours...</span>`;
      }
      if (pickBtn) pickBtn.disabled = true;
      const scopeLabel = auditCustomFolders.length > 0
        ? `${auditCustomFolders.length} dossier(s) ciblé(s)`
        : Array.from(auditScanDrives).sort().join(', ');
      showProgress(`Scan en cours (${scopeLabel})...`, 5);
      if (currentView !== 'audit') switchView('audit');
      postToBridge({
        action: 'runAudit',
        drives: Array.from(auditScanDrives),
        paths: auditCustomFolders
      });
    }

    function showProgress(text, pct) {
      const banner = document.getElementById('progress-banner');
      if (!banner) return;
      banner.classList.remove('hidden');
      document.getElementById('progress-text').innerHTML = `
        <span class="progress-tag progress-tag-audit">Audit</span>
        <span style="color:var(--app-accent); display:flex; align-items:center;">${icon('spinner', 'w-3.5 h-3.5')}</span>
        <span class="truncate">${escapeHtml(text)}</span>`;
      document.getElementById('progress-pct').innerText = `${Math.round(pct)}%`;
      document.getElementById('progress-bar').style.width = `${Math.min(100, Math.max(0, pct))}%`;
    }

    function hideProgress() {
      auditInProgress = false;
      const banner = document.getElementById('progress-banner');
      if (banner) banner.classList.add('hidden');
      const pickBtn = document.getElementById('btn-audit-pick-folders');
      if (pickBtn) pickBtn.disabled = false;
      renderScanDrivePills('audit');
    }

    function syncProtectedStatus() {
      if (!allItems || !protectedItems) return;
      allItems.forEach(raw => {
        const p = (raw.path || raw.Path || '').toLowerCase();
        const n = (raw.name || raw.Name || '').toLowerCase();
        raw.isProtected = protectedItems.some(x => {
          const xp = (x.path || x.Path || '').toLowerCase();
          const xn = (x.name || x.Name || '').toLowerCase();
          return (p && xp === p) || (n && xn === n);
        });
      });
    }

    function getItemSizeGB(x) {
      const gb = x.sizeGB ?? x.SizeGB ?? 0;
      const mb = x.sizeMB ?? x.SizeMB ?? 0;
      return gb > 0 ? gb : (mb / 1024.0);
    }

    function getDriveFilteredItems() {
      return allItems.filter(raw => {
        if (raw.isProtected ?? raw.IsProtected) return false;
        const drv = (raw.drive || raw.Drive || '').toUpperCase();
        if (!drv) return true;
        return activeDrives.has(drv);
      });
    }

    function refreshAuditUI() {
      const driveScoped = getDriveFilteredItems();
      updateAnalytics(driveScoped);
      updateCounters(driveScoped);
      renderItems();
      updateSelectionCalc();
    }

    function updateAnalytics(driveScoped) {
      let safeGB = 0, oldGB = 0, recGB = 0;
      let catTotals = { games: 0, system: 0, downloads: 0, apps: 0 };

      driveScoped.forEach(x => {
        const sz = getItemSizeGB(x);
        const risk = x.riskPct ?? x.RiskPct ?? 0;
        const age = x.ageDays ?? x.AgeDays ?? 0;
        const isExtra = x.isExtra ?? x.IsExtra ?? false;
        const cat = (x.category || x.Category || 'system').toLowerCase();

        if (!x.isProtected) {
          if (risk <= 10) safeGB += sz;
          if (age >= 365) oldGB += sz;
          if (!isExtra) recGB += sz;
        }
        if (catTotals[cat] !== undefined) catTotals[cat] += sz;
      });

      document.getElementById('kpi-safe-gb').innerText = `${safeGB.toFixed(2)} Go`;
      document.getElementById('kpi-old-gb').innerText = `${oldGB.toFixed(2)} Go`;
      document.getElementById('kpi-rec-gb').innerText = `${recGB.toFixed(2)} Go`;

      const totalCat = catTotals.games + catTotals.system + catTotals.downloads + catTotals.apps;
      const pct = v => totalCat > 0 ? Math.max(0, (v / totalCat) * 100) : 0;

      document.getElementById('dist-games').innerText = `${catTotals.games.toFixed(1)} Go`;
      document.getElementById('dist-system').innerText = `${catTotals.system.toFixed(1)} Go`;
      document.getElementById('dist-downloads').innerText = `${catTotals.downloads.toFixed(1)} Go`;
      document.getElementById('dist-apps').innerText = `${catTotals.apps.toFixed(1)} Go`;

      document.getElementById('bar-games').style.width = `${pct(catTotals.games)}%`;
      document.getElementById('bar-system').style.width = `${pct(catTotals.system)}%`;
      document.getElementById('bar-downloads').style.width = `${pct(catTotals.downloads)}%`;
      document.getElementById('bar-apps').style.width = `${pct(catTotals.apps)}%`;
    }

    function updateCounters(driveScoped) {
      const getCat = x => (x.category || x.Category || '').toLowerCase();
      document.getElementById('nav-count-audit').innerText = driveScoped.length;
      document.getElementById('count-all').innerText = driveScoped.length;
      document.getElementById('count-games').innerText = driveScoped.filter(x => getCat(x) === 'games').length;
      document.getElementById('count-system').innerText = driveScoped.filter(x => getCat(x) === 'system').length;
      document.getElementById('count-downloads').innerText = driveScoped.filter(x => getCat(x) === 'downloads').length;
      document.getElementById('count-apps').innerText = driveScoped.filter(x => getCat(x) === 'apps').length;
    }

    function switchAuditCat(cat) {
      currentAuditCat = cat;
      ['all', 'games', 'system', 'downloads', 'apps'].forEach(c => {
        document.getElementById(`cat-${c}`).classList.toggle('active', c === cat);
      });
      renderItems();
    }

    function setQuickFilter(q) {
      quickFilter = q;
      ['all', 'safe', '6m', 'old', 'large'].forEach(k => {
        const el = document.getElementById(`chip-${k}`);
        if (el) el.classList.toggle('active', k === q);
      });
      renderItems();
    }

    function toggleHideRunning() {
      hideRunning = !hideRunning;
      const el = document.getElementById('chip-hide-running');
      if (el) el.classList.toggle('active', hideRunning);
      renderItems();
    }

    function onSortChange() {
      sortField = document.getElementById('sort-select').value;
      sortAsc = (sortField === 'recommended' || sortField === 'risk' || sortField === 'name' || sortField === 'drive');
      updateSortDirBtn();
      renderItems();
    }

    function toggleSortDir() {
      sortAsc = !sortAsc;
      updateSortDirBtn();
      renderItems();
    }

    function updateSortDirBtn() {
      document.getElementById('btn-sort-dir').innerText = sortAsc ? '↑ Croissant' : '↓ Décroissant';
    }

    function sortItemsArray(arr) {
      return [...arr].sort((a, b) => {
        let cmp = 0;
        if (sortField === 'recommended') {
          cmp = (a.riskPct ?? a.RiskPct ?? 0) - (b.riskPct ?? b.RiskPct ?? 0);
          if (cmp === 0) cmp = getItemSizeGB(b) - getItemSizeGB(a);
          return sortAsc ? cmp : -cmp;
        } else if (sortField === 'size') {
          cmp = getItemSizeGB(a) - getItemSizeGB(b);
        } else if (sortField === 'risk') {
          cmp = (a.riskPct ?? a.RiskPct ?? 0) - (b.riskPct ?? b.RiskPct ?? 0);
        } else if (sortField === 'age') {
          cmp = (a.ageDays ?? a.AgeDays ?? 0) - (b.ageDays ?? b.AgeDays ?? 0);
        } else if (sortField === 'name') {
          cmp = (a.name || a.Name || '').localeCompare(b.name || b.Name || '', 'fr');
        } else if (sortField === 'drive') {
          cmp = (a.drive || a.Drive || '').localeCompare(b.drive || b.Drive || '');
        }
        return sortAsc ? cmp : -cmp;
      });
    }

    function filterItemsList() {
      const q = (document.getElementById('search-input').value || '').toLowerCase();
      return getDriveFilteredItems().filter(raw => {
        const name = (raw.name || raw.Name || '').toLowerCase();
        const path = (raw.path || raw.Path || '').toLowerCase();
        const desc = (raw.description || raw.Description || '').toLowerCase();
        const cat = (raw.category || raw.Category || '').toLowerCase();
        const risk = raw.riskPct ?? raw.RiskPct ?? 0;
        const age = raw.ageDays ?? raw.AgeDays ?? 0;
        const sz = getItemSizeGB(raw);

        if (currentAuditCat !== 'all' && cat !== currentAuditCat) return false;
        if (quickFilter === 'safe' && risk > 10) return false;
        if (quickFilter === '6m' && age < 180) return false;
        if (quickFilter === 'old' && age < 365) return false;
        if (quickFilter === 'large' && sz < 1.0) return false;
        if (hideRunning && desc.includes('en cours')) return false;
        if (q && !name.includes(q) && !path.includes(q) && !desc.includes(q)) return false;
        return true;
      });
    }

    function riskMeta(riskPct) {
      if (riskPct === 0) return { label: 'Sûr 0%', color: 'var(--app-safe)', bg: 'var(--app-safe-soft)' };
      if (riskPct <= 15) return { label: `Faible ${riskPct}%`, color: 'var(--app-safe)', bg: 'var(--app-safe-soft)' };
      if (riskPct <= 45) return { label: `Modéré ${riskPct}%`, color: 'var(--app-warn)', bg: 'var(--app-warn-soft)' };
      return { label: `Élevé ${riskPct}%`, color: 'var(--app-danger)', bg: 'var(--app-danger-soft)' };
    }

    function formatAgeText(ageDays, lastMod) {
      if (ageDays >= 365) {
        const yrs = Math.floor(ageDays / 365);
        return `Inactif ${yrs} an${yrs > 1 ? 's' : ''}`;
      }
      if (ageDays >= 120) {
        return `${Math.floor(ageDays / 30)} mois`;
      }
      return lastMod || '';
    }

    function renderItems() {
      const container = document.getElementById('items-container');
      const filtered = filterItemsList();
      document.getElementById('kpi-visible-count').innerText = filtered.length;

      if (allItems.length === 0) return;

      if (filtered.length === 0) {
        container.innerHTML = `<div class="surface-card rounded-xl p-10 text-center text-xs" style="color:var(--app-muted);">Aucun élément trouvé pour les disques et filtres sélectionnés.</div>`;
        return;
      }

      const mainItems = sortItemsArray(filtered.filter(x => !(x.isExtra ?? x.IsExtra) && !(x.isPersonal ?? x.IsPersonal)));
      const personalItems = sortItemsArray(filtered.filter(x => (x.isPersonal ?? x.IsPersonal)));
      const extraItems = sortItemsArray(filtered.filter(x => (x.isExtra ?? x.IsExtra) && !(x.isPersonal ?? x.IsPersonal)));

      updateSidebarSectionsUI(mainItems.length, extraItems.length, personalItems.length);

      let html = '';
      if (mainItems.length > 0) {
        const mainTotalGB = mainItems.reduce((acc, x) => acc + getItemSizeGB(x), 0);
        html += `
          <div class="space-y-2">
            <div class="surface-card w-full flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 rounded-xl">
              <button onclick="toggleMainSection()" class="flex items-center gap-2 text-left hover:opacity-80">
                <span style="color:var(--app-text);">${icon(mainExpanded ? 'chevron-down' : 'chevron-right', 'w-4 h-4')}</span>
                <div>
                  <span class="text-xs font-bold" style="color:var(--app-text);">Audit Intelligent Recommandé</span>
                  <span class="text-[11px] font-mono tabular-nums ml-2" style="color:var(--app-muted);">(${mainItems.length} éléments • ${mainTotalGB.toFixed(1)} Go)</span>
                </div>
              </button>
              <div class="flex items-center gap-3">
                <button onclick="selectBulk('zero')" class="btn-safe px-2.5 py-1 rounded-lg text-[11px] font-semibold inline-flex items-center gap-1.5">
                  ${icon('shield-check', 'w-3.5 h-3.5')}
                  <span>Sélectionner sûrs (0%)</span>
                </button>
                <button onclick="toggleMainSection()" class="text-[11px] font-semibold hover:underline" style="color:var(--app-muted);">
                  ${mainExpanded ? 'Replier' : 'Déplier'}
                </button>
              </div>
            </div>
            <div class="${mainExpanded ? 'ml-4 pl-3 space-y-2' : 'hidden'}" style="border-left:2px solid var(--app-border-strong);">
              ${mainItems.map(renderCardHtml).join('')}
            </div>
          </div>
        `;
      } else {
        html += `<div class="surface-card rounded-xl p-4 text-center text-xs" style="color:var(--app-muted);">Aucun reliquat ou élément inactif recommandé pour ces disques.</div>`;
      }

      if (extraItems.length > 0) {
        const extraTotalGB = extraItems.reduce((acc, x) => acc + getItemSizeGB(x), 0);
        html += `
          <div class="mt-4 pt-4" style="border-top:1px solid var(--app-border);">
            <button onclick="toggleExtraSection()" class="surface-card w-full flex items-center justify-between px-4 py-2.5 rounded-xl text-left">
              <div class="flex items-center gap-2">
                <span style="color:var(--app-muted);">${icon(extraExpanded ? 'chevron-down' : 'chevron-right', 'w-4 h-4')}</span>
                <div>
                  <span class="text-xs font-bold" style="color:var(--app-text);">Logiciels &amp; jeux récents / actifs</span>
                  <span class="text-[11px] font-mono tabular-nums ml-2" style="color:var(--app-muted);">(${extraItems.length} éléments • ${extraTotalGB.toFixed(1)} Go)</span>
                </div>
              </div>
              <span class="text-[11px] font-semibold" style="color:var(--app-muted);">${extraExpanded ? 'Replier' : 'Déplier'}</span>
            </button>
            <div class="${extraExpanded ? 'mt-2.5 ml-4 pl-3 space-y-2' : 'hidden'}" style="border-left:2px solid var(--app-border);">
              ${extraItems.map(renderCardHtml).join('')}
            </div>
          </div>
        `;
      }

      if (personalItems.length > 0) {
        const personalTotalGB = personalItems.reduce((acc, x) => acc + getItemSizeGB(x), 0);
        const groupMap = {};
        const groupIconNames = {
          'Documents': 'file',
          'Vidéos': 'play',
          'Images': 'layers',
          'Musique': 'sparkles',
          'Bureau': 'hard-drive',
          'Profil Utilisateur': 'shield',
          'Sauvegardes & Dossiers Racines': 'folder'
        };
        personalItems.forEach(it => {
          const g = (it.personalGroup || it.PersonalGroup || 'Autres');
          if (!groupMap[g]) groupMap[g] = [];
          groupMap[g].push(it);
        });

        const subListsHtml = Object.entries(groupMap).map(([grp, list]) => {
          const isOpen = !!personalSubExpanded[grp];
          const grpGB = list.reduce((acc, x) => acc + getItemSizeGB(x), 0);
          const icName = groupIconNames[grp] || 'folder';
          return `
            <div class="space-y-2">
              <button onclick="togglePersonalSubGroup('${safeEnc(grp)}')" class="surface-card w-full flex items-center justify-between px-3.5 py-2 rounded-xl text-left">
                <div class="flex items-center gap-2">
                  <span style="color:var(--app-muted);">${icon(isOpen ? 'chevron-down' : 'chevron-right', 'w-3.5 h-3.5')}</span>
                  <span style="color:var(--app-muted);">${icon(icName, 'w-3.5 h-3.5')}</span>
                  <span class="text-xs font-semibold" style="color:var(--app-text);">${escapeHtml(grp)}</span>
                  <span class="text-[11px] font-mono tabular-nums" style="color:var(--app-muted);">(${list.length} • ${grpGB.toFixed(1)} Go)</span>
                </div>
                <span class="text-[11px] font-medium" style="color:var(--app-muted);">${isOpen ? 'Replier' : 'Déplier'}</span>
              </button>
              <div class="${isOpen ? 'ml-5 pl-3 space-y-2' : 'hidden'}" style="border-left:2px solid var(--app-border-strong);">
                ${list.map(renderCardHtml).join('')}
              </div>
            </div>
          `;
        }).join('');

        html += `
          <div class="mt-4 pt-4" style="border-top:1px solid var(--app-border);">
            <button onclick="togglePersonalSection()" class="surface-card w-full flex items-center justify-between px-4 py-2.5 rounded-xl text-left">
              <div class="flex items-center gap-2">
                <span style="color:var(--app-muted);">${icon(personalExpanded ? 'chevron-down' : 'chevron-right', 'w-4 h-4')}</span>
                <span style="color:var(--app-muted);">${icon('folder', 'w-4 h-4')}</span>
                <div>
                  <span class="text-xs font-bold" style="color:var(--app-text);">Fichiers &amp; Dossiers Personnels (${Object.keys(groupMap).length} catégories)</span>
                  <span class="text-[11px] font-mono tabular-nums ml-2" style="color:var(--app-muted);">(${personalItems.length} éléments • ${personalTotalGB.toFixed(1)} Go)</span>
                </div>
              </div>
              <span class="text-[11px] font-semibold" style="color:var(--app-muted);">${personalExpanded ? 'Replier' : 'Déplier'}</span>
            </button>
            <div class="${personalExpanded ? 'mt-2.5 ml-4 pl-3 space-y-2.5' : 'hidden'}" style="border-left:2px solid var(--app-border-strong);">
              ${subListsHtml}
            </div>
          </div>
        `;
      }

      container.innerHTML = html;
    }

    function updateSidebarSectionsUI(mainCount, extraCount, personalCount = 0) {
      const mCnt = document.getElementById('side-main-count');
      const eCnt = document.getElementById('side-extra-count');
      const pCnt = document.getElementById('side-personal-count');
      const mArr = document.getElementById('side-main-arrow');
      const eArr = document.getElementById('side-extra-arrow');
      const pArr = document.getElementById('side-personal-arrow');
      const mBtn = document.getElementById('btn-side-main');
      const eBtn = document.getElementById('btn-side-extra');
      const pBtn = document.getElementById('btn-side-personal');
      if (mCnt) mCnt.innerText = mainCount;
      if (eCnt) eCnt.innerText = extraCount;
      if (pCnt) pCnt.innerText = personalCount;
      if (mArr) mArr.innerHTML = icon(mainExpanded ? 'chevron-down' : 'chevron-right', 'w-3 h-3');
      if (eArr) eArr.innerHTML = icon(extraExpanded ? 'chevron-down' : 'chevron-right', 'w-3 h-3');
      if (pArr) pArr.innerHTML = icon(personalExpanded ? 'chevron-down' : 'chevron-right', 'w-3 h-3');

      if (mBtn) mBtn.classList.toggle('is-open', mainExpanded);
      if (eBtn) eBtn.classList.toggle('is-open', extraExpanded);
      if (pBtn) pBtn.classList.toggle('is-open', personalExpanded);
    }

    function toggleMainSection() {
      if (currentView !== 'audit') switchView('audit');
      mainExpanded = !mainExpanded;
      renderItems();
    }

    function toggleExtraSection() {
      if (currentView !== 'audit') switchView('audit');
      extraExpanded = !extraExpanded;
      renderItems();
    }

    function togglePersonalSection() {
      if (currentView !== 'audit') switchView('audit');
      personalExpanded = !personalExpanded;
      renderItems();
    }

    function togglePersonalSubGroup(encGroup) {
      const grp = decodeURIComponent(encGroup);
      personalSubExpanded[grp] = !personalSubExpanded[grp];
      renderItems();
    }

    function renderCardHtml(raw) {
      const id = raw.id || raw.Id;
      const name = raw.name || raw.Name;
      const path = raw.path || raw.Path;
      const drive = raw.drive || raw.Drive || '';
      const sizeGB = raw.sizeGB ?? raw.SizeGB ?? 0;
      const sizeMB = raw.sizeMB ?? raw.SizeMB ?? 0;
      const riskPct = raw.riskPct ?? raw.RiskPct ?? 0;
      const desc = raw.description || raw.Description || '';
      const ageDays = raw.ageDays ?? raw.AgeDays ?? 0;
      const lastMod = raw.lastModified || raw.LastModified || '';
      const isSanct = raw.isSanctuarized ?? raw.IsSanctuarized ?? false;
      const isProt = raw.isProtected ?? raw.IsProtected ?? false;
      const isDelChecked = !isSanct && !isProt && selectedDeleteIds.has(id);

      const isRegTrace = path.startsWith('HKLM\\') || path.startsWith('HKCU\\');
      const szText = isRegTrace && sizeMB === 0 ? 'Registre' : (sizeGB >= 0.1 ? `${sizeGB.toFixed(2)} Go` : `${sizeMB.toFixed(1)} Mo`);
      const rm = riskMeta(riskPct);
      const ageLabel = formatAgeText(ageDays, lastMod);

      return `
        <div class="item-row rounded-xl px-4 py-3 flex flex-col lg:flex-row lg:items-center justify-between gap-3 ${isDelChecked ? 'selected-delete' : ''}">
          <div class="flex-1 min-w-0 space-y-1">
            <div class="flex items-center gap-2 flex-wrap">
              <span class="text-xs font-bold tracking-tight truncate max-w-md" style="color:var(--app-text);">${escapeHtml(name)}</span>
              <span class="inline-flex items-center gap-1.5 text-[11px] font-medium px-2 py-0.5 rounded-full tabular-nums" style="background:${rm.bg}; color:${rm.color};">
                <span class="w-1.5 h-1.5 rounded-full" style="background:${rm.color};"></span>
                <span>${rm.label}</span>
              </span>
              ${drive ? `<span class="text-[10px] font-mono px-1.5 py-0.5 rounded" style="background:var(--app-surface-2); color:var(--app-muted);">${escapeHtml(drive)}</span>` : ''}
              ${isSanct ? `<span title="Dossier système ou personnel protégé par défaut" class="inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-md font-medium" style="background:var(--app-warn-soft); color:var(--app-warn);">${icon('lock', 'w-3 h-3')} Protégé</span>` : ''}
              ${isProt ? `<span class="inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-md font-medium" style="background:var(--app-safe-soft); color:var(--app-safe);">${icon('shield-check', 'w-3 h-3')} Sanctuarisé</span>` : ''}
            </div>
            <p class="text-[11px] truncate" style="color:var(--app-muted);">${escapeHtml(desc)}</p>
            <p class="text-[10px] font-mono truncate opacity-75" style="color:var(--app-muted);">${escapeHtml(path)}</p>
          </div>

          <div class="flex items-center gap-2.5 shrink-0 flex-wrap">
            <!-- Swiss Tabular Metric Column -->
            <div class="w-20 text-right font-mono tabular-nums mr-1">
              <div class="text-xs font-bold" style="color:var(--app-text);">${szText}</div>
              ${ageLabel ? `<div class="text-[10px] truncate" style="color:var(--app-muted);">${escapeHtml(ageLabel)}</div>` : ''}
            </div>

            <!-- Tactile Segmented Switch: Garder | Supprimer -->
            <div class="seg-group">
              <button onclick="setItemAction('${safeEnc(id)}', 'keep')" class="seg-btn ${!isDelChecked && !isProt ? 'active-keep' : ''}">Garder</button>
              <button onclick="setItemAction('${safeEnc(id)}', 'delete')" ${isSanct || isProt ? 'disabled' : ''} class="seg-btn ${isDelChecked ? 'active-delete' : ''} disabled:opacity-35 disabled:pointer-events-none">
                ${isDelChecked ? icon('check', 'w-3 h-3') : ''}
                <span>Supprimer</span>
              </button>
            </div>

            <button onclick="protectItem('${safeEnc(id)}')" ${isProt ? 'disabled' : ''} title="Protéger définitivement" class="btn-secondary px-2 py-1.5 rounded-lg text-[11px] font-medium inline-flex items-center gap-1 disabled:opacity-35 disabled:pointer-events-none">
              ${icon('shield', 'w-3.5 h-3.5')}
              <span class="hidden xl:inline">Protéger</span>
            </button>
            <button onclick="askResearch('${safeEnc(id)}')" title="Enquête & Détails" class="btn-secondary px-2 py-1.5 rounded-lg text-[11px] font-medium inline-flex items-center gap-1">
              ${icon('info', 'w-3.5 h-3.5')}
              <span>Infos</span>
            </button>
            ${renderAiCardButton(name, path, riskPct)}
            <button onclick="openFolder('${safeEnc(path)}')" title="Ouvrir dans l'Explorateur Windows" class="btn-secondary p-1.5 rounded-lg inline-flex items-center justify-center">
              ${icon('folder', 'w-3.5 h-3.5')}
            </button>
          </div>
        </div>
      `;
    }

    function setItemAction(encId, action) {
      const id = decodeURIComponent(encId);
      if (action === 'delete') selectedDeleteIds.add(id);
      else selectedDeleteIds.delete(id);
      updateSelectionCalc();
      renderItems();
    }

    function selectBulk(mode) {
      const visible = filterItemsList();
      if (mode === 'none') {
        selectedDeleteIds.clear();
      } else if (mode === 'zero') {
        visible.forEach(x => {
          const id = x.id || x.Id;
          const risk = x.riskPct ?? x.RiskPct ?? 0;
          if (risk === 0 && !(x.isSanctuarized ?? x.IsSanctuarized) && !(x.isProtected ?? x.IsProtected)) {
            selectedDeleteIds.add(id);
          }
        });
      } else if (mode === 'old') {
        visible.forEach(x => {
          const id = x.id || x.Id;
          const age = x.ageDays ?? x.AgeDays ?? 0;
          const risk = x.riskPct ?? x.RiskPct ?? 0;
          if (age >= 365 && risk <= 35 && !(x.isSanctuarized ?? x.IsSanctuarized) && !(x.isProtected ?? x.IsProtected)) {
            selectedDeleteIds.add(id);
          }
        });
      }
      updateSelectionCalc();
      renderItems();
    }

    function updateSelectionCalc() {
      let totalGB = 0;
      let count = 0;
      if (currentView === 'duplicates') {
        duplicateGroups.forEach(g => {
          (g.files || g.Files || []).forEach(f => {
            if (selectedDuplicateIds.has(f.id || f.Id)) {
              totalGB += (f.sizeGB ?? f.SizeGB ?? 0);
              count++;
            }
          });
        });
      } else {
        allItems.forEach(x => {
          if (selectedDeleteIds.has(x.id || x.Id)) {
            totalGB += getItemSizeGB(x);
            count++;
          }
        });
      }
      const liveGbEl = document.getElementById('live-selected-gb');
      if (liveGbEl) {
        liveGbEl.innerText = `${totalGB.toFixed(2)} Go`;
        liveGbEl.style.color = count > 0 ? 'var(--app-accent)' : 'var(--app-text)';
      }
      document.getElementById('btn-clean').disabled = count === 0;
    }

    // ================= SPACE EXPLORER (ARBORESCENCE) =================
    function normalizeTreeKey(p) {
      if (!p) return '';
      let k = p.trim().toUpperCase().replace(/\//g, '\\');
      if (k.length === 2 && k[1] === ':') k += '\\';
      else if (k.length > 3 && k.endsWith('\\')) k = k.slice(0, -1);
      return k;
    }

    function renderExplorerDrivePills() {
      const el = document.getElementById('explorer-drive-pills');
      if (!el || !drivesList) return;
      const curRoot = (currentTreePath || '').slice(0, 2).toUpperCase();
      el.innerHTML = drivesList.map(d => {
        const name = (d.name || d.Name || 'C:').toUpperCase();
        const label = d.label || d.Label || 'Disque';
        const isCurrent = curRoot === name;
        const rootKey = normalizeTreeKey(name + '\\');
        const isCached = !!treeCache[rootKey] || indexedDriveRoots.has(rootKey);
        const isScanning = !!pendingTreeScans[rootKey];
        return `
          <button onclick="navigateExplorerEnc('${safeEnc(name + '\\')}')" class="explorer-drive-pill ${isCurrent ? 'active' : ''} px-3 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5">
            ${isScanning ? `<span class="w-1.5 h-1.5 rounded-full animate-ping" style="background:var(--app-warn);"></span>` : (isCached ? `<span class="w-1.5 h-1.5 rounded-full" style="background:var(--app-safe);" title="En mémoire (accès instantané)"></span>` : '')}
            <span class="font-mono">${escapeHtml(name)}</span>
            <span class="text-[10px] font-normal opacity-75">(${escapeHtml(label)})</span>
          </button>
        `;
      }).join('');
    }

    function navigateExplorerEnc(encPath) {
      navigateExplorer(decodeURIComponent(encPath));
    }

    function navigateExplorer(targetPath, forceRefresh = false, skipHistory = false) {
      if (!skipHistory && currentTreePath && normalizeTreeKey(currentTreePath) !== normalizeTreeKey(targetPath)) {
        explorerHistory.push(currentTreePath);
        if (explorerHistory.length > 40) explorerHistory.shift();
      }
      const backBtn = document.getElementById('btn-explorer-back');
      if (backBtn) backBtn.disabled = explorerHistory.length === 0;

      currentTreePath = targetPath;
      const key = normalizeTreeKey(targetPath);

      if (!forceRefresh && treeCache[key]) {
        explorerLoading = false;
        activeExplorerRequestId = ++explorerRequestCounter;
        currentTreeNode = treeCache[key];
        renderExplorerTree();
        return;
      }

      if (!forceRefresh && pendingTreeScans[key]) {
        explorerLoading = true;
        activeExplorerRequestId = pendingTreeScans[key].requestId;
        renderExplorerDrivePills();
        updateHeaderSubtitle();
        renderExplorerLoading(pendingTreeScans[key].text, pendingTreeScans[key].pct);
        return;
      }

      explorerLoading = true;
      activeExplorerRequestId = ++explorerRequestCounter;
      pendingTreeScans[key] = { requestId: activeExplorerRequestId, text: `Lecture de ${targetPath}...`, pct: 5 };
      renderExplorerDrivePills();
      updateHeaderSubtitle();
      renderExplorerLoading(`Lecture de ${targetPath}...`, 5);
      postToBridge({ action: 'scanFolderTree', path: targetPath, requestId: activeExplorerRequestId, forceRefresh });
    }

    function explorerBack() {
      if (explorerHistory.length === 0) return;
      const prev = explorerHistory.pop();
      const backBtn = document.getElementById('btn-explorer-back');
      if (backBtn) backBtn.disabled = explorerHistory.length === 0;
      if (prev) navigateExplorer(prev, false, true);
    }

    function explorerGoParent() {
      if (!currentTreePath) return;
      const trimmed = currentTreePath.replace(/[\\\/]+$/, '');
      if (trimmed.length <= 2) return;
      const lastSlash = trimmed.lastIndexOf('\\');
      if (lastSlash <= 0) return;
      let parent = trimmed.slice(0, lastSlash);
      if (parent.length === 2 && parent[1] === ':') parent += '\\';
      navigateExplorer(parent);
    }

    function explorerRefresh() {
      if (currentTreePath) {
        const key = normalizeTreeKey(currentTreePath);
        const prefix = key.endsWith('\\') ? key : key + '\\';
        Object.keys(treeCache).forEach(k => {
          if (k === key || k.startsWith(prefix)) delete treeCache[k];
        });
        indexedDriveRoots.delete(normalizeTreeKey(key.slice(0, 2) + '\\'));
        navigateExplorer(currentTreePath, true);
      }
    }

    function preindexAllDrives() {
      if (explorerPreindexing) return;
      explorerPreindexing = true;
      const btn = document.getElementById('btn-explorer-preindex');
      if (btn) {
        btn.disabled = true;
        btn.innerHTML = `${icon('spinner', 'w-3 h-3')} <span>Pré-indexation...</span>`;
      }
      postToBridge({ action: 'preindexDrives' });
    }

    function explorerOpenShortcut(kind) {
      const drive = (Array.from(activeDrives)[0] || 'C:').toUpperCase();
      if (kind === 'profile') {
        navigateExplorer('C:\\Users');
      } else if (kind === 'downloads') {
        navigateExplorer(drive === 'C:' ? 'C:\\Users' : `${drive}\\Téléchargements`);
      }
    }

    function renderExplorerLoading(statusText = '', pct = 5) {
      document.getElementById('explorer-visual-wrap').classList.add('hidden');
      document.getElementById('explorer-breadcrumb').innerHTML = `<span class="font-bold" style="color:var(--app-accent);">${escapeHtml(currentTreePath)}</span>`;
      document.getElementById('explorer-tree-container').innerHTML = `
        <div class="surface-card rounded-xl p-10 text-center space-y-3 max-w-xl mx-auto">
          <div class="flex justify-center" style="color:var(--app-accent);">${icon('spinner', 'w-6 h-6')}</div>
          <div id="explorer-progress-text" class="text-xs font-medium" style="color:var(--app-text);">${escapeHtml(statusText || `Calcul de l'arborescence pour ${currentTreePath}...`)}</div>
          <div class="w-full rounded-full h-2 overflow-hidden" style="background:var(--app-ring-track);">
            <div id="explorer-progress-bar" class="h-full rounded-full spring" style="width:${pct}%; background:var(--app-accent);"></div>
          </div>
          <div id="explorer-progress-pct" class="text-[11px] font-mono tabular-nums font-bold" style="color:var(--app-accent);">${pct}%</div>
        </div>
      `;
    }

    function updateExplorerProgressUI(text, pct) {
      const tEl = document.getElementById('explorer-progress-text');
      const bEl = document.getElementById('explorer-progress-bar');
      const pEl = document.getElementById('explorer-progress-pct');
      if (tEl) tEl.innerText = text;
      if (bEl) bEl.style.width = `${pct}%`;
      if (pEl) pEl.innerText = `${pct}%`;
    }

    function formatSizeNode(node) {
      const gb = node.sizeGB ?? node.SizeGB ?? 0;
      const mb = node.sizeMB ?? node.SizeMB ?? 0;
      if (gb >= 1.0) return `${gb.toFixed(2)} Go`;
      if (mb >= 1.0) return `${mb.toFixed(1)} Mo`;
      const kb = (node.sizeBytes ?? node.SizeBytes ?? 0) / 1024.0;
      return `${kb.toFixed(0)} Ko`;
    }

    function renderExplorerTree() {
      renderExplorerDrivePills();
      updateHeaderSubtitle();
      const node = currentTreeNode;
      if (!node) return;

      const cleanPath = (node.path || node.Path || 'C:\\').replace(/[\\\/]+$/, '');
      const parts = cleanPath.split('\\').filter(Boolean);
      let acc = '';
      const bcHtml = parts.map((part, idx) => {
        acc = idx === 0 ? `${part}\\` : `${acc}${acc.endsWith('\\') ? '' : '\\'}${part}`;
        const target = acc;
        const isLast = idx === parts.length - 1;
        return `
          ${idx > 0 ? '<span style="color:var(--app-muted); opacity:0.5;">›</span>' : ''}
          <button onclick="navigateExplorerEnc('${safeEnc(target)}')" class="px-1.5 py-0.5 rounded hover:opacity-80 ${isLast ? 'font-bold' : ''}" style="color:${isLast ? 'var(--app-accent)' : 'var(--app-text)'}; background:${isLast ? 'var(--app-accent-soft)' : 'transparent'};">${escapeHtml(idx === 0 ? part + '\\' : part)}</button>
        `;
      }).join('');
      document.getElementById('explorer-breadcrumb').innerHTML = bcHtml;

      const children = node.children || node.Children || [];
      const totalFiles = node.fileCount ?? node.FileCount ?? 0;
      document.getElementById('explorer-total-meta').innerText = `${formatSizeNode(node)} • ${totalFiles.toLocaleString('fr-FR')} fichiers`;

      const visualWrap = document.getElementById('explorer-visual-wrap');
      const barEl = document.getElementById('explorer-treemap-bar');
      const legEl = document.getElementById('explorer-treemap-legend');

      const topForBar = children.filter(c => (c.pctOfParent ?? c.PctOfParent ?? 0) >= 1.0).slice(0, 8);
      if (topForBar.length > 0) {
        visualWrap.classList.remove('hidden');
        const usedPct = topForBar.reduce((a, c) => a + (c.pctOfParent ?? c.PctOfParent ?? 0), 0);
        const restPct = Math.max(0, 100 - usedPct);

        barEl.innerHTML = topForBar.map((c, idx) => {
          const col = TREEMAP_COLORS[idx % TREEMAP_COLORS.length];
          const p = c.pctOfParent ?? c.PctOfParent ?? 0;
          const cname = c.name || c.Name;
          const cpath = c.path || c.Path;
          const isFolder = c.isFolder ?? c.IsFolder ?? true;
          return `<div onclick="${isFolder ? `navigateExplorerEnc('${safeEnc(cpath)}')` : `askResearchPath('${safeEnc(cname)}','${safeEnc(cpath)}')`}" title="${escapeHtml(cname)} (${formatSizeNode(c)} — ${p}%)" class="h-full rounded-md cursor-pointer hover:opacity-85 spring" style="width:${Math.max(p, 1.5)}%; background:${col};"></div>`;
        }).join('') + (restPct > 1 ? `<div title="Autres éléments (${restPct.toFixed(1)}%)" class="h-full rounded-md opacity-40" style="width:${restPct}%; background:var(--app-muted);"></div>` : '');

        legEl.innerHTML = topForBar.map((c, idx) => {
          const col = TREEMAP_COLORS[idx % TREEMAP_COLORS.length];
          const p = c.pctOfParent ?? c.PctOfParent ?? 0;
          const cname = c.name || c.Name;
          const cpath = c.path || c.Path;
          const isFolder = c.isFolder ?? c.IsFolder ?? true;
          return `
            <button onclick="${isFolder ? `navigateExplorerEnc('${safeEnc(cpath)}')` : `askResearchPath('${safeEnc(cname)}','${safeEnc(cpath)}')`}" class="btn-secondary flex items-center gap-1.5 text-[11px] px-2 py-1 rounded-lg">
              <span class="w-2 h-2 rounded-full shrink-0" style="background:${col};"></span>
              <span class="max-w-[140px] truncate" style="color:var(--app-text);">${escapeHtml(cname)}</span>
              <span class="font-mono font-semibold tabular-nums" style="color:var(--app-muted);">${p}%</span>
            </button>
          `;
        }).join('');
      } else {
        visualWrap.classList.add('hidden');
      }

      const container = document.getElementById('explorer-tree-container');
      if (children.length === 0) {
        container.innerHTML = `<div class="surface-card rounded-xl p-8 text-center text-xs" style="color:var(--app-muted);">Ce dossier est vide ou inaccessible.</div>`;
        return;
      }

      container.innerHTML = children.map((c, idx) => {
        const cname = c.name || c.Name;
        const cpath = c.path || c.Path;
        const isFolder = c.isFolder ?? c.IsFolder ?? false;
        const pct = c.pctOfParent ?? c.PctOfParent ?? 0;
        const fCount = c.fileCount ?? c.FileCount ?? 0;
        const lastMod = c.lastModified || c.LastModified || '';
        const isSanct = c.isSanctuarized ?? c.IsSanctuarized ?? false;
        const isPersonalSub = c.isPersonalSubItem ?? c.IsPersonalSubItem ?? false;
        const isProt = c.isProtected ?? c.IsProtected ?? false;
        const barAccent = idx < 8 ? TREEMAP_COLORS[idx % TREEMAP_COLORS.length] : 'var(--app-muted)';

        return `
          <div ${isFolder ? `onclick="navigateExplorerEnc('${safeEnc(cpath)}')"` : ''} class="item-row relative overflow-hidden rounded-xl px-4 py-2.5 flex items-center justify-between gap-3 ${isFolder ? 'cursor-pointer' : ''}">
            <div class="absolute inset-y-0 left-0 pointer-events-none opacity-10" style="width:${Math.min(pct, 100)}%; background:${barAccent};"></div>

            <div class="relative flex items-center gap-3 min-w-0 flex-1">
              <span style="color:${isFolder ? 'var(--app-warn)' : 'var(--app-muted)'};">${icon(isFolder ? 'folder' : 'file', 'w-4 h-4')}</span>
              <div class="min-w-0">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="text-xs font-semibold truncate" style="color:var(--app-text);">${escapeHtml(cname)}</span>
                  ${isFolder ? `<span class="text-[10px] font-mono tabular-nums" style="color:var(--app-muted);">${fCount.toLocaleString('fr-FR')} fichiers</span>` : ''}
                  ${lastMod ? `<span class="text-[10px]" style="color:var(--app-muted);">${escapeHtml(lastMod)}</span>` : ''}
                  ${isSanct ? `<span title="Dossier système ou personnel protégé par défaut" class="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded" style="background:var(--app-warn-soft); color:var(--app-warn);">${icon('lock', 'w-2.5 h-2.5')} Protégé</span>` : ''}
                  ${!isSanct && isPersonalSub ? `<span title="Situé dans Documents/Vidéos" class="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded" style="background:var(--app-surface-2); color:var(--app-muted); border:1px solid var(--app-border);">Perso</span>` : ''}
                  ${isProt ? `<span class="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded" style="background:var(--app-safe-soft); color:var(--app-safe);">${icon('shield-check', 'w-2.5 h-2.5')} Sanctuarisé</span>` : ''}
                </div>
              </div>
            </div>

            <div class="relative flex items-center gap-3 shrink-0">
              <div class="text-right font-mono tabular-nums">
                <div class="text-xs font-bold" style="color:var(--app-text);">${formatSizeNode(c)}</div>
                <div class="text-[10px]" style="color:var(--app-muted);">${pct}%</div>
              </div>

              <div class="flex items-center gap-1" onclick="event.stopPropagation()">
                <button onclick="askResearchPath('${safeEnc(cname)}', '${safeEnc(cpath)}')" title="Analyser avec l'inspecteur & Gemini IA" class="btn-secondary p-1.5 rounded-lg inline-flex items-center justify-center">
                  ${icon('info', 'w-3.5 h-3.5')}
                </button>
                <button onclick="openFolder('${safeEnc(cpath)}')" title="Ouvrir dans l'Explorateur Windows" class="btn-secondary p-1.5 rounded-lg inline-flex items-center justify-center">
                  ${icon('folder', 'w-3.5 h-3.5')}
                </button>
                <button onclick="promptDeleteTreeNode(${idx})" ${isSanct || isProt ? 'disabled' : ''} title="Supprimer cet élément" class="btn-secondary p-1.5 rounded-lg inline-flex items-center justify-center disabled:opacity-30 disabled:pointer-events-none" style="color:var(--app-danger);">
                  ${icon('trash', 'w-3.5 h-3.5')}
                </button>
              </div>
            </div>
          </div>
        `;
      }).join('');
    }

    function promptDeleteTreeNode(idx) {
      const children = currentTreeNode?.children || currentTreeNode?.Children || [];
      const c = children[idx];
      if (!c) return;
      pendingDirectDeleteItems = [{
        id: 'tree-' + Date.now(),
        name: c.name || c.Name,
        path: c.path || c.Path,
        category: 'system',
        drive: (c.path || c.Path || '').slice(0, 2),
        sizeGB: c.sizeGB ?? c.SizeGB ?? 0,
        sizeMB: c.sizeMB ?? c.SizeMB ?? 0,
        isFolder: c.isFolder ?? c.IsFolder ?? true
      }];
      openCleanModalWithList(pendingDirectDeleteItems);
    }

    // ================= PROTECTED LIST VIEW =================
    function protectItem(encId) {
      const id = decodeURIComponent(encId);
      const item = allItems.find(x => (x.id || x.Id) === id);
      if (!item) return;
      selectedDeleteIds.delete(id);
      postToBridge({
        action: 'addProtected',
        item: {
          name: item.name || item.Name,
          path: item.path || item.Path,
          category: item.category || item.Category,
          drive: item.drive || item.Drive,
          sizeGB: item.sizeGB ?? item.SizeGB ?? 0,
          sizeMB: item.sizeMB ?? item.SizeMB ?? 0,
          description: item.description || item.Description || ''
        }
      });
      updateSelectionCalc();
      showToast(`« ${item.name || item.Name} » ajouté à la Liste Protégée.`, 'success');
    }

    function unprotectItem(encId) {
      postToBridge({ action: 'removeProtected', id: decodeURIComponent(encId) });
      showToast('Élément retiré de la Liste Protégée.', 'info');
    }

    function renderProtectedView() {
      const container = document.getElementById('protected-container');
      if (!protectedItems || protectedItems.length === 0) {
        container.innerHTML = `
          <div class="surface-card rounded-2xl p-10 text-center space-y-2">
            <div class="flex justify-center opacity-45" style="color:var(--app-muted);">${icon('lock', 'w-7 h-7')}</div>
            <p class="text-xs" style="color:var(--app-muted);">Votre Liste Protégée est vide. Cliquez sur <strong style="color:var(--app-text);">Protéger</strong> dans l'audit pour sanctuariser un jeu ou logiciel.</p>
          </div>
        `;
        return;
      }
      container.innerHTML = protectedItems.map(p => {
        const id = p.id || p.Id;
        const name = p.name || p.Name;
        const path = p.path || p.Path;
        const date = p.protectedDate || p.ProtectedDate || '';
        const szGB = p.sizeGB ?? p.SizeGB ?? 0;
        const szMB = p.sizeMB ?? p.SizeMB ?? 0;
        const szText = szGB >= 0.1 ? `${szGB.toFixed(2)} Go` : `${szMB.toFixed(1)} Mo`;
        return `
          <div class="item-row rounded-xl px-4 py-3 flex items-center justify-between gap-3">
            <div class="min-w-0 space-y-1">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="text-xs font-bold truncate" style="color:var(--app-text);">${escapeHtml(name)}</span>
                <span class="text-[10px] font-mono px-2 py-0.5 rounded-md tabular-nums font-semibold" style="background:var(--app-safe-soft); color:var(--app-safe);">${szText}</span>
                <span class="text-[10px]" style="color:var(--app-muted);">Protégé le ${escapeHtml(date)}</span>
              </div>
              <p class="text-[10px] font-mono truncate" style="color:var(--app-muted);">${escapeHtml(path)}</p>
            </div>
            <button onclick="unprotectItem('${safeEnc(id)}')" class="btn-secondary px-3 py-1.5 rounded-lg text-xs font-medium shrink-0">
              Retirer protection
            </button>
          </div>
        `;
      }).join('');
    }

    // ================= RESEARCH & AI =================
    function askResearch(encId) {
      const id = decodeURIComponent(encId);
      const item = allItems.find(x => (x.id || x.Id) === id);
      if (!item) return;
      triggerResearch(item.name || item.Name || '', item.path || item.Path || '', item.riskPct ?? item.RiskPct ?? 0);
    }

    function askResearchPath(encName, encPath) {
      triggerResearch(decodeURIComponent(encName), decodeURIComponent(encPath), -1);
    }

    function triggerResearch(name, path, riskPct) {
      activeResearchItem = { name, path, riskPct };
      activeResearchRequestId++;
      const reqId = activeResearchRequestId;
      const key = (path || '').toLowerCase();
      const cachedAi = aiReportCache[key];
      if (cachedAi) {
        showResearchModal(cachedAi);
        return;
      }
      const cachedLocal = localReportCache[key];
      if (cachedLocal) {
        showResearchModal(cachedLocal);
        return;
      }
      const isLoading = aiStatusByPath[key] === 'loading';
      resetAiDeepButton(false, isLoading);
      postToBridge({ action: 'researchItem', name, path, riskPct, requestId: reqId });
    }

    function showResearchModal(rep) {
      if (!rep) return;
      const title = rep.itemName || rep.ItemName || 'Élément';
      const path = rep.itemPath || rep.ItemPath || '';
      const date = rep.lastAccessDate || rep.LastAccessDate || 'Inconnu';
      const risk = rep.riskPct ?? rep.RiskPct ?? 0;
      const isAi = rep.isAiEnriched || rep.IsAiEnriched || false;

      document.getElementById('res-title').innerText = title;
      document.getElementById('res-path').innerText = path;
      document.getElementById('res-date').innerText = date;
      const riskEl = document.getElementById('res-risk');
      riskEl.innerText = `${risk}%`;
      const rm = riskMeta(risk);
      riskEl.style.color = rm.color;
      document.getElementById('res-def').innerText = rep.definition || rep.Definition || '';
      document.getElementById('res-origin').innerText = rep.origin || rep.Origin || '';
      document.getElementById('res-activity').innerText = rep.usageActivity || rep.UsageActivity || '';
      document.getElementById('res-safety').innerText = rep.safetyImpact || rep.SafetyImpact || '';

      document.getElementById('res-ai-badge').classList.toggle('hidden', !isAi);

      const pathKey = path.toLowerCase();
      const hasAi = isAi || !!aiReportCache[pathKey];
      const toggleWrap = document.getElementById('res-mode-toggle');
      const btnLocal = document.getElementById('btn-mode-local');
      const btnAi = document.getElementById('btn-mode-ai');
      if (toggleWrap) {
        toggleWrap.classList.toggle('hidden', !hasAi);
        if (hasAi) {
          btnLocal.style.background = !isAi ? 'var(--app-surface)' : 'transparent';
          btnLocal.style.border = !isAi ? '1px solid var(--app-border-strong)' : '1px solid transparent';
          btnLocal.style.color = !isAi ? 'var(--app-text)' : 'var(--app-muted)';
          btnAi.style.background = isAi ? 'var(--app-safe-soft)' : 'transparent';
          btnAi.style.border = isAi ? '1px solid var(--app-safe)' : '1px solid transparent';
          btnAi.style.color = isAi ? 'var(--app-safe)' : 'var(--app-muted)';
        }
      }

      const isLoading = activeResearchItem && aiStatusByPath[(activeResearchItem.path || '').toLowerCase()] === 'loading';
      resetAiDeepButton(hasAi, isLoading);
      document.getElementById('modal-research').classList.remove('hidden');
    }

    function switchResearchMode(mode) {
      if (!activeResearchItem) return;
      const key = activeResearchItem.path.toLowerCase();
      if (mode === 'ai') {
        if (aiReportCache[key]) {
          showResearchModal(aiReportCache[key]);
        } else {
          askAiDeepResearch();
        }
      } else {
        if (localReportCache[key]) {
          showResearchModal(localReportCache[key]);
        } else {
          postToBridge({
            action: 'researchItem',
            name: activeResearchItem.name,
            path: activeResearchItem.path,
            riskPct: activeResearchItem.riskPct,
            bypassAiCache: true
          });
        }
      }
    }

    function closeResearchModal() {
      activeResearchItem = null;
      document.getElementById('modal-research').classList.add('hidden');
    }

    function resetAiDeepButton(hasAi = false, isLoading = false) {
      if (isLoading) {
        document.getElementById('ai-spinner').classList.remove('hidden');
        document.getElementById('ai-deep-label').innerText = 'Analyse Gemini en cours...';
        document.getElementById('btn-ai-deep-research').disabled = true;
      } else {
        document.getElementById('ai-spinner').classList.add('hidden');
        document.getElementById('ai-deep-label').innerText = hasAi ? "Réanalyser avec l'IA" : "Approfondir avec l'IA";
        document.getElementById('btn-ai-deep-research').disabled = false;
      }
    }

    function renderAiCardButton(name, path, riskPct) {
      const pKey = (path || '').toLowerCase();
      const state = aiStatusByPath[pKey] || (aiReportCache[pKey] ? 'done' : '');
      if (state === 'loading') {
        return `<button disabled title="Analyse IA Gemini en cours en arrière-plan..." class="px-2.5 py-1.5 rounded-lg text-[11px] font-medium inline-flex items-center gap-1 cursor-wait" style="background:var(--app-surface-2); border:1px solid var(--app-border-strong); color:var(--app-text);">${icon('spinner', 'w-3 h-3')} <span>IA...</span></button>`;
      }
      if (state === 'done') {
        return `<button onclick="openAiReportForCard('${safeEnc(name)}', '${safeEnc(path)}', ${riskPct})" title="Analyse IA terminée — cliquer pour consulter" class="px-2.5 py-1.5 rounded-lg text-[11px] font-semibold inline-flex items-center gap-1 spring hover:opacity-90" style="background:var(--app-safe-soft); border:1px solid var(--app-safe); color:var(--app-safe);">${icon('sparkles', 'w-3 h-3')} <span>IA</span> ${icon('check', 'w-3 h-3')}</button>`;
      }
      if (state === 'error') {
        return `<button onclick="launchAiForCard('${safeEnc(name)}', '${safeEnc(path)}', ${riskPct})" title="${escapeHtml(aiErrorByPath[pKey] || 'Échec de l’analyse IA — cliquer pour réessayer')}" class="px-2.5 py-1.5 rounded-lg text-[11px] font-semibold inline-flex items-center gap-1 spring" style="background:var(--app-danger-soft); border:1px solid var(--app-danger); color:var(--app-danger);">${icon('alert-triangle', 'w-3 h-3')} <span>IA</span></button>`;
      }
      return `<button onclick="launchAiForCard('${safeEnc(name)}', '${safeEnc(path)}', ${riskPct})" title="Lancer l'analyse IA Gemini en arrière-plan" class="btn-secondary px-2.5 py-1.5 rounded-lg text-[11px] font-medium inline-flex items-center gap-1">${icon('sparkles', 'w-3 h-3')} <span>IA</span></button>`;
    }

    function launchAiForCard(encName, encPath, riskPct) {
      startBackgroundAiAnalysis(decodeURIComponent(encName), decodeURIComponent(encPath), riskPct, false);
    }

    function openAiReportForCard(encName, encPath, riskPct) {
      triggerResearch(decodeURIComponent(encName), decodeURIComponent(encPath), riskPct);
    }

    function startBackgroundAiAnalysis(name, path, riskPct, updateModalSpinner = true) {
      if (!currentSettings.geminiApiKey) {
        showToast('Configurez votre clé API Gemini dans « Mode IA Gemini ».', 'warn');
        openAiSettingsModal();
        return;
      }
      const key = (path || '').toLowerCase();
      if (aiStatusByPath[key] === 'loading') return;

      aiStatusByPath[key] = 'loading';
      delete aiErrorByPath[key];
      if (currentView === 'audit' && allItems.length > 0) renderItems();

      activeResearchRequestId++;
      const reqId = activeResearchRequestId;
      if (updateModalSpinner) {
        document.getElementById('ai-spinner').classList.remove('hidden');
        document.getElementById('ai-deep-label').innerText = 'Analyse Gemini en cours...';
        document.getElementById('btn-ai-deep-research').disabled = true;
      } else {
        showToast(`Analyse IA lancée en arrière-plan pour « ${name} »...`, 'info');
      }

      postToBridge({
        action: 'researchItemAI',
        name,
        path,
        riskPct,
        apiKey: currentSettings.geminiApiKey || '',
        requestId: reqId
      });
    }

    function askAiDeepResearch() {
      if (!activeResearchItem) return;
      startBackgroundAiAnalysis(activeResearchItem.name, activeResearchItem.path, activeResearchItem.riskPct, true);
    }

    function openFolder(encPath) {
      postToBridge({ action: 'openFolder', path: decodeURIComponent(encPath) });
    }

    function openExternalUrl(url) {
      postToBridge({ action: 'openUrl', url });
    }

    // ================= AI SETTINGS =================
    function openAiSettingsModal() {
      document.getElementById('input-api-key').value = currentSettings.geminiApiKey || '';
      document.getElementById('api-test-status').innerText = '';
      document.getElementById('modal-ai-settings').classList.remove('hidden');
    }

    function closeAiSettingsModal() {
      document.getElementById('modal-ai-settings').classList.add('hidden');
    }

    function toggleApiKeyVisibility() {
      const input = document.getElementById('input-api-key');
      input.type = input.type === 'password' ? 'text' : 'password';
    }

    function testApiKey() {
      const key = document.getElementById('input-api-key').value.trim();
      if (!key) {
        document.getElementById('api-test-status').innerHTML = `<span style="color:var(--app-warn);">Veuillez saisir une clé.</span>`;
        return;
      }
      document.getElementById('test-spinner').classList.remove('hidden');
      document.getElementById('api-test-status').innerHTML = `<span style="color:var(--app-muted);">Test en cours...</span>`;
      postToBridge({ action: 'testGeminiApiKey', apiKey: key });
    }

    function handleApiKeyTestResult(success, message) {
      document.getElementById('test-spinner').classList.add('hidden');
      const statusEl = document.getElementById('api-test-status');
      const msg = escapeHtml(message || (success ? 'Clé valide et connectée !' : 'Clé invalide ou erreur réseau'));
      statusEl.innerHTML = success
        ? `<span class="font-semibold" style="color:var(--app-safe);">${msg}</span>`
        : `<span class="font-semibold break-all" style="color:var(--app-danger);">${msg}</span>`;
    }

    function saveAiSettings() {
      currentSettings.geminiApiKey = document.getElementById('input-api-key').value.trim();
      currentSettings.enableAI = true;
      postToBridge({ action: 'saveSettings', settings: currentSettings });
      updateAiHeaderUI();
      closeAiSettingsModal();
      showToast('Paramètres Mode IA enregistrés.', 'success');
    }

    function updateAiHeaderUI() {
      const indicator = document.getElementById('ai-header-indicator');
      const label = document.getElementById('ai-header-label');
      if (!indicator || !label) return;
      if (currentSettings.geminiApiKey && currentSettings.geminiApiKey.length > 5) {
        indicator.style.background = 'var(--app-safe)';
        indicator.classList.add('animate-pulse');
        label.innerText = 'Mode IA Actif';
      } else {
        indicator.style.background = 'var(--app-muted)';
        indicator.classList.remove('animate-pulse');
        label.innerText = 'Mode IA Gemini';
      }
    }

    // ================= CLEAN EXECUTION & HISTORY =================
    function openCleanModal() {
      if (currentView === 'duplicates') {
        openDuplicatesCleanModal();
        return;
      }
      pendingDirectDeleteItems = null;
      const selected = allItems.filter(x => selectedDeleteIds.has(x.id || x.Id));
      if (selected.length === 0) return;
      openCleanModalWithList(selected);
    }

    function onCleanDeleteModeChange() {
      const isPerm = document.getElementById('clean-mode-perm')?.checked ?? false;
      const warn = document.getElementById('clean-perm-warning');
      const btn = document.getElementById('btn-confirm-delete');
      if (warn) warn.classList.toggle('hidden', !isPerm);
      if (btn) {
        btn.className = `${isPerm ? 'btn-danger' : 'btn-primary'} px-4 py-2 rounded-lg font-semibold text-xs flex items-center gap-1.5`;
        btn.innerHTML = isPerm
          ? `${icon('alert-triangle', 'w-3.5 h-3.5')} <span>Supprimer définitivement</span>`
          : `${icon('rotate-ccw', 'w-3.5 h-3.5')} <span>Envoyer à la Corbeille</span>`;
      }
    }

    function openCleanModalWithList(selected) {
      let total = 0;
      const byDrive = {};
      const runningNames = [];

      document.getElementById('clean-summary-list').innerHTML = selected.map(raw => {
        const name = raw.name || raw.Name;
        const szGB = getItemSizeGB(raw);
        const drv = (raw.drive || raw.Drive || (raw.path || raw.Path || '').slice(0, 2) || '?').toUpperCase();
        const desc = (raw.description || raw.Description || '').toLowerCase();
        if (desc.includes('en cours')) runningNames.push(name);
        byDrive[drv] = (byDrive[drv] || 0) + szGB;
        total += szGB;
        const szText = szGB >= 0.1 ? `${szGB.toFixed(2)} Go` : `${(raw.sizeMB ?? raw.SizeMB ?? 0).toFixed(1)} Mo`;
        return `<div class="flex items-center justify-between gap-2 py-0.5" style="color:var(--app-text);"><span class="truncate">• ${escapeHtml(name)}</span><span class="font-bold font-mono shrink-0">${szText}</span></div>`;
      }).join('');

      const drivePalette = ['var(--app-accent)', 'var(--app-warn)', 'var(--app-safe)', 'var(--app-muted)'];
      const driveEntries = Object.entries(byDrive);

      const barEl = document.getElementById('clean-drive-bar');
      if (barEl) {
        barEl.innerHTML = driveEntries.map(([, gb], idx) => {
          const w = total > 0 ? Math.max(4, (gb / total) * 100) : 0;
          return `<div style="width:${w}%; background:${drivePalette[idx % drivePalette.length]};" class="h-full rounded-full"></div>`;
        }).join('');
      }

      const breakdownEl = document.getElementById('clean-drive-breakdown');
      if (breakdownEl) {
        breakdownEl.innerHTML = driveEntries.map(([d, gb], idx) =>
          `<span class="px-2 py-0.5 rounded font-mono inline-flex items-center gap-1.5" style="background:var(--app-surface-2); border:1px solid var(--app-border); color:var(--app-text);"><span class="w-2 h-2 rounded-full" style="background:${drivePalette[idx % drivePalette.length]};"></span>${escapeHtml(d)} : <strong>${gb.toFixed(2)} Go</strong></span>`
        ).join('');
      }

      const warnEl = document.getElementById('clean-running-warning');
      if (warnEl) {
        if (runningNames.length > 0) {
          warnEl.classList.remove('hidden');
          warnEl.innerHTML = `<strong>Processus actifs détectés :</strong> ${escapeHtml(runningNames.join(', '))} — les processus liés seront fermés avant suppression.`;
        } else {
          warnEl.classList.add('hidden');
        }
      }

      const recycleRadio = document.getElementById('clean-mode-recycle');
      if (recycleRadio) recycleRadio.checked = true;
      onCleanDeleteModeChange();

      document.getElementById('clean-modal-total').innerText = `${total.toFixed(2)} Go`;
      document.getElementById('modal-clean').classList.remove('hidden');
    }

    function closeCleanModal() {
      pendingDirectDeleteItems = null;
      document.getElementById('modal-clean').classList.add('hidden');
    }

    function executeSelectedClean() {
      const selected = pendingDirectDeleteItems || allItems.filter(x => selectedDeleteIds.has(x.id || x.Id));
      const allowManualPersonalChild = pendingDirectDeleteItems !== null;
      const useRecycleBin = document.getElementById('clean-mode-recycle')?.checked ?? false;
      const btn = document.getElementById('btn-confirm-delete');
      if (btn) {
        btn.disabled = true;
        btn.innerHTML = `${icon('spinner', 'w-3.5 h-3.5')} <span>Nettoyage en cours...</span>`;
      }
      postToBridge({ action: 'executeClean', items: selected, useRecycleBin, allowManualPersonalChild });
    }

    function onCleanCompleted(res) {
      const wasDirectTree = pendingDirectDeleteItems !== null;
      closeCleanModal();
      const btn = document.getElementById('btn-confirm-delete');
      if (btn) btn.disabled = false;
      onCleanDeleteModeChange();

      const deletedIds = res.deletedIds || res.DeletedIds || [];
      const deletedPaths = res.deletedPaths || res.DeletedPaths || [];
      const errors = res.errors || res.Errors || [];
      if (deletedIds.length > 0 && !wasDirectTree) {
        allItems = allItems.filter(x => !deletedIds.includes(x.id || x.Id));
        deletedIds.forEach(id => selectedDeleteIds.delete(id));
      }
      if (deletedPaths.length > 0) {
        treeCache = {};
        deletedPaths.forEach(dp => {
          if (dp && dp.length >= 2 && dp[1] === ':') {
            indexedDriveRoots.delete(normalizeTreeKey(dp.slice(0, 2) + '\\'));
          }
        });
        renderExplorerDrivePills();
        if (duplicateGroups.length > 0) {
          const delSet = new Set(deletedPaths.map(p => p.toLowerCase()));
          duplicateGroups.forEach(g => {
            g.files = (g.files || g.Files || []).filter(f => !delSet.has((f.path || f.Path || '').toLowerCase()));
          });
          duplicateGroups = duplicateGroups.filter(g => (g.files || []).length >= 2);
          if (currentView === 'duplicates') renderDuplicatesView();
        }
      }
      if (allItems.length > 0) refreshAuditUI();
      if (currentView === 'explorer' && currentTreePath) explorerRefresh();
      postToBridge({ action: 'getDrives' });
      postToBridge({ action: 'getHistory' });

      const freed = res.freedGB ?? res.FreedGB ?? 0;
      if (errors.length > 0) {
        showToast(errors[0], 'warn');
      } else {
        showToast(`Nettoyage terminé ! ${freed.toFixed(2)} Go libérés.`, 'success');
      }
    }

    function clearHistory() {
      postToBridge({ action: 'clearHistory' });
      showToast('Historique effacé.', 'info');
    }

    function renderHistoryView(history) {
      const total = history.reduce((acc, h) => acc + (h.freedGB ?? h.FreedGB ?? 0), 0);
      document.getElementById('history-total-gb').innerText = `${total.toFixed(2)} Go`;
      const list = document.getElementById('history-list');
      if (!history || history.length === 0) {
        list.innerHTML = `<div class="surface-card rounded-xl p-10 text-center text-xs" style="color:var(--app-muted);">Aucun nettoyage enregistré pour le moment.</div>`;
        return;
      }
      list.innerHTML = history.map(h => `
        <div class="item-row rounded-xl p-3.5 space-y-1">
          <div class="flex items-center justify-between text-xs">
            <span class="font-semibold" style="color:var(--app-text);">${escapeHtml(h.date || h.Date)}</span>
            <span class="font-mono font-bold tabular-nums" style="color:var(--app-safe);">+${(h.freedGB ?? h.FreedGB ?? 0).toFixed(2)} Go (${h.itemsCount ?? h.ItemsCount} éléments)</span>
          </div>
          <p class="text-[11px] truncate" style="color:var(--app-muted);">${escapeHtml((h.itemNames || h.ItemNames || []).join(', '))}</p>
        </div>
      `).join('');
    }

    // ================= AUDIT EVOLUTION & WINSXS =================
    function renderHomeLastSnapshot(snapshots) {
      const el = document.getElementById('home-last-snapshot');
      if (!el) return;
      if (!snapshots || snapshots.length === 0) {
        el.innerHTML = `<span style="color:var(--app-muted);">Aucun audit précédent enregistré</span>`;
        return;
      }
      const last = snapshots[0];
      const dt = last.date || last.Date || '';
      const recGB = (last.recommendedGB ?? last.RecommendedGB ?? 0).toFixed(2);
      const recCnt = last.recommendedCount ?? last.RecommendedCount ?? 0;
      el.innerHTML = `Dernier audit (${escapeHtml(dt)}) : <span class="font-bold" style="color:var(--app-text);">${recCnt} éléments</span> • <span class="font-bold" style="color:var(--app-safe);">${recGB} Go récupérables</span>`;
    }

    function renderAuditEvolutionBanner(prev) {
      const el = document.getElementById('audit-evolution-banner');
      if (!el) return;
      if (!prev || !(prev.drives || prev.Drives)) {
        el.classList.add('hidden');
        return;
      }
      const prevDrives = prev.drives || prev.Drives || [];
      const diffs = drivesList.map(curr => {
        const cname = (curr.name || curr.Name || '').toUpperCase();
        const prevD = prevDrives.find(p => (p.drive || p.Drive || '').toUpperCase() === cname);
        if (!prevD) return null;
        const deltaFree = (curr.freeGB ?? curr.FreeGB ?? 0) - (prevD.freeGB ?? prevD.FreeGB ?? 0);
        const sign = deltaFree >= 0 ? '+' : '';
        const col = deltaFree >= 0 ? 'var(--app-safe)' : 'var(--app-warn)';
        return `<span class="font-mono font-semibold tabular-nums" style="color:${col};">${escapeHtml(cname)} ${sign}${deltaFree.toFixed(2)} Go libres</span>`;
      }).filter(Boolean);

      if (diffs.length === 0) {
        el.classList.add('hidden');
        return;
      }
      el.classList.remove('hidden');
      el.innerHTML = `
        <span style="color:var(--app-text);"><strong>Évolution depuis le dernier audit</strong> (${escapeHtml(prev.date || prev.Date || '')}) :</span>
        <div class="flex flex-wrap items-center gap-3">${diffs.join(' • ')}</div>
      `;
    }

    function analyzeWinSxS() {
      const btn = document.getElementById('btn-analyze-winsxs');
      const txt = document.getElementById('winsxs-summary-text');
      if (btn) {
        btn.disabled = true;
        btn.innerHTML = `${icon('spinner', 'w-3.5 h-3.5')} <span>Analyse DISM...</span>`;
      }
      if (txt) txt.innerText = 'Analyse lecture seule de WinSxS en cours (DISM)...';
      postToBridge({ action: 'analyzeWinSxS' });
    }

    function renderWinSxSResult(report) {
      const btn = document.getElementById('btn-analyze-winsxs');
      const txt = document.getElementById('winsxs-summary-text');
      if (btn) {
        btn.disabled = false;
        btn.innerHTML = `${icon('rotate-ccw', 'w-3.5 h-3.5')} <span>Réanalyser WinSxS</span>`;
      }
      if (!txt || !report) return;

      if (report.requiresAdmin || report.RequiresAdmin) {
        txt.innerHTML = `<span class="inline-flex items-center gap-1.5" style="color:var(--app-warn);">${icon('lock', 'w-3.5 h-3.5')} ${escapeHtml(report.message || report.Message)}</span>`;
        return;
      }
      const actual = report.actualSize || report.ActualSize || '?';
      const reclaim = report.reclaimablePackages ?? report.ReclaimablePackages ?? '?';
      const msg = report.message || report.Message || '';
      txt.innerHTML = `<strong style="color:var(--app-text);">WinSxS :</strong> Taille réelle <span class="font-mono font-semibold" style="color:var(--app-text);">${escapeHtml(actual)}</span> • Packages récupérables : <span class="font-mono font-semibold" style="color:var(--app-safe);">${escapeHtml(String(reclaim))}</span> — <span style="color:var(--app-muted);">${escapeHtml(msg)}</span>`;
    }

    // ================= BENTO DISK GAUGES & SCAN TARGETS =================
    function ringSvg(pct, active = true) {
      const r = 17;
      const c = 2 * Math.PI * r;
      const offset = c - (Math.min(100, Math.max(0, pct)) / 100) * c;
      const stroke = !active
        ? 'var(--app-muted)'
        : (pct > 85 ? 'var(--app-danger)' : (pct > 70 ? 'var(--app-warn)' : 'var(--app-safe)'));
      const arcOpacity = active ? '1' : '0.45';
      return `
        <svg width="44" height="44" viewBox="0 0 44 44" class="shrink-0 -rotate-90">
          <circle cx="22" cy="22" r="${r}" fill="none" stroke="var(--app-ring-track)" stroke-width="3.5" />
          <circle cx="22" cy="22" r="${r}" fill="none" stroke="${stroke}" stroke-opacity="${arcOpacity}" stroke-width="3.5" stroke-linecap="round" stroke-dasharray="${c}" stroke-dashoffset="${offset}" class="spring" />
        </svg>
      `;
    }

    function pickFolders(target) {
      postToBridge({ action: 'pickFolders', target });
    }

    function removeCustomFolder(target, idx) {
      const arr = target === 'audit' ? auditCustomFolders : duplicateCustomFolders;
      arr.splice(idx, 1);
      renderCustomFolders(target);
    }

    function clearCustomFolders(target) {
      if (target === 'audit') auditCustomFolders = [];
      else duplicateCustomFolders = [];
      renderCustomFolders(target);
    }

    function toggleScanDrive(target, encName) {
      const locked = target === 'audit' ? auditInProgress : dupInProgress;
      if (locked) return;
      const set = target === 'audit' ? auditScanDrives : dupScanDrives;
      const name = decodeURIComponent(encName).toUpperCase();
      if (set.has(name)) {
        if (set.size === 1) {
          showToast('Au moins un disque doit rester sélectionné pour le scan.', 'warn');
          return;
        }
        set.delete(name);
      } else {
        set.add(name);
      }
      renderScanDrivePills(target);
    }

    function renderScanDrivePills(target) {
      const el = document.getElementById(target === 'audit' ? 'audit-scan-drives' : 'dup-scan-drives');
      const set = target === 'audit' ? auditScanDrives : dupScanDrives;
      const locked = target === 'audit' ? auditInProgress : dupInProgress;
      const customArr = target === 'audit' ? auditCustomFolders : duplicateCustomFolders;
      const drivesWrap = document.getElementById(target === 'audit' ? 'audit-scan-drives-wrap' : 'dup-scan-drives-wrap');

      if (drivesWrap) {
        drivesWrap.classList.toggle('opacity-50', customArr.length > 0);
      }
      if (el && drivesList) {
        if (target === 'audit') {
          el.innerHTML = drivesList.map(d => {
            const name = (d.name || d.Name || 'C:').toUpperCase();
            const freeGB = d.freeGB ?? d.FreeGB ?? 0;
            const totalGB = d.totalGB ?? d.TotalGB ?? 0;
            const pctUsed = d.pctUsed ?? d.PctUsed ?? 0;
            const active = set.has(name);
            const badgeHtml = active
              ? `<span class="text-[10px] font-mono font-bold px-2 py-0.5 rounded-full inline-flex items-center gap-1" style="background:var(--app-accent); color:var(--app-accent-fg);">${icon('check', 'w-2.5 h-2.5')}<span>Ciblé</span></span>`
              : `<span class="text-[10px] font-mono font-medium px-2 py-0.5 rounded-full inline-flex items-center" style="background:var(--app-surface-2); color:var(--app-muted);">Exclu</span>`;
            return `
              <button onclick="toggleScanDrive('audit', '${safeEnc(name)}')" ${locked ? 'disabled' : ''} class="scan-drive-card ${active ? 'active' : ''} rounded-xl p-3 flex items-center gap-3 text-left disabled:pointer-events-none">
                ${ringSvg(pctUsed, active)}
                <div class="min-w-0 flex-1">
                  <div class="flex items-center justify-between gap-1.5">
                    <span class="font-mono text-xs font-bold inline-flex items-center gap-1" style="color:${active ? 'var(--app-text)' : 'var(--app-muted)'};">${icon('hard-drive', 'w-3.5 h-3.5')} ${escapeHtml(name)}</span>
                    ${badgeHtml}
                  </div>
                  <div class="text-[11px] tabular-nums mt-0.5 truncate" style="color:var(--app-muted);">${freeGB.toFixed(0)} Go libres / ${totalGB.toFixed(0)}</div>
                </div>
              </button>
            `;
          }).join('');
        } else {
          el.innerHTML = drivesList.map(d => {
            const name = (d.name || d.Name || 'C:').toUpperCase();
            const active = set.has(name);
            const chkHtml = active
              ? `<span class="w-3.5 h-3.5 rounded flex items-center justify-center shrink-0" style="background:var(--app-accent); color:var(--app-accent-fg);">${icon('check', 'w-2.5 h-2.5')}</span>`
              : `<span class="w-3.5 h-3.5 rounded shrink-0" style="background:var(--app-surface); border:1px solid var(--app-border-strong);"></span>`;
            return `
              <button onclick="toggleScanDrive('duplicates', '${safeEnc(name)}')" ${locked ? 'disabled' : ''} class="scan-drive-pill ${active ? 'active' : ''} px-3 py-1.5 rounded-lg font-mono text-[11px] font-semibold inline-flex items-center gap-2 disabled:opacity-50 disabled:pointer-events-none">
                ${chkHtml}
                <span>${escapeHtml(name)}</span>
                <span class="opacity-75 font-normal tabular-nums">(${(d.freeGB ?? d.FreeGB ?? 0).toFixed(0)} Go)</span>
              </button>
            `;
          }).join('');
        }
      }
      updateScanButtonLabel(target);
    }

    function updateScanButtonLabel(target) {
      const btn = document.getElementById(target === 'audit' ? 'btn-audit-launch' : 'btn-scan-duplicates');
      const set = target === 'audit' ? auditScanDrives : dupScanDrives;
      const customArr = target === 'audit' ? auditCustomFolders : duplicateCustomFolders;
      const locked = target === 'audit' ? auditInProgress : dupInProgress;
      if (!btn || locked) return;
      btn.disabled = false;

      if (customArr.length > 0) {
        btn.innerHTML = target === 'audit'
          ? `${icon('play', 'w-3.5 h-3.5')} <span>Lancer l'Audit (${customArr.length} dossier${customArr.length > 1 ? 's' : ''})</span>`
          : `${icon('search', 'w-3.5 h-3.5')} <span>Scanner (${customArr.length} dossier${customArr.length > 1 ? 's' : ''})</span>`;
      } else {
        const allSelected = drivesList.length > 0 && set.size === drivesList.length;
        const listStr = Array.from(set).sort().join(', ');
        btn.innerHTML = target === 'audit'
          ? `${icon('play', 'w-3.5 h-3.5')} <span>${allSelected ? "Lancer l'Audit (tous les disques)" : `Lancer l'Audit (${escapeHtml(listStr)})`}</span>`
          : `${icon('search', 'w-3.5 h-3.5')} <span>${allSelected ? 'Scanner tous les disques' : `Scanner sur ${escapeHtml(listStr)}`}</span>`;
      }
    }

    function renderCustomFolders(target) {
      const wrap = document.getElementById(target === 'audit' ? 'audit-custom-folders-wrap' : 'dup-custom-folders-wrap');
      const list = document.getElementById(target === 'audit' ? 'audit-custom-folders' : 'dup-custom-folders');
      const arr = target === 'audit' ? auditCustomFolders : duplicateCustomFolders;
      if (!wrap || !list) return;

      if (arr.length === 0) {
        wrap.classList.add('hidden');
        wrap.classList.remove('flex');
      } else {
        wrap.classList.remove('hidden');
        wrap.classList.add('flex');
        list.innerHTML = arr.map((p, idx) => `
          <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md font-mono text-[10px]" style="background:var(--app-surface-2); border:1px solid var(--app-border-strong); color:var(--app-text);">
            <span>${escapeHtml(p)}</span>
            <button onclick="removeCustomFolder('${target}', ${idx})" class="font-bold hover:opacity-70" style="color:var(--app-danger);">✕</button>
          </span>
        `).join('');
      }
      renderScanDrivePills(target);
    }

    function startDuplicatesScan() {
      if (dupInProgress) return;
      if (duplicateCustomFolders.length === 0 && dupScanDrives.size === 0) {
        showToast('Sélectionnez au moins un disque ou un dossier.', 'warn');
        return;
      }
      const minMB = parseInt(document.getElementById('dup-min-mb')?.value || '50', 10);
      const drives = Array.from(dupScanDrives);
      if (duplicateCustomFolders.length === 0) {
        dupScanDrives.forEach(d => activeDrives.add(d));
        renderSidebarDrives();
        updateHeaderSubtitle();
      }
      selectedDuplicateIds.clear();
      updateSelectionCalc();
      dupInProgress = true;
      unseenResults.duplicates = false;
      updateNavUnseenBadges();
      renderScanDrivePills('duplicates');
      const btn = document.getElementById('btn-scan-duplicates');
      const pickBtn = document.getElementById('btn-dup-pick-folders');
      if (btn) {
        btn.disabled = true;
        btn.innerHTML = `${icon('spinner', 'w-3.5 h-3.5')} <span>Scan en cours...</span>`;
      }
      if (pickBtn) pickBtn.disabled = true;
      showDupProgress(duplicateCustomFolders.length > 0 ? 'Étape 1/3 : Parcours des dossiers ciblés...' : `Étape 1/3 : Parcours sur ${drives.join(', ')}...`, 5);
      postToBridge({ action: 'scanDuplicates', drives, minMB, paths: duplicateCustomFolders });
    }

    function showDupProgress(text, pct) {
      const topBanner = document.getElementById('progress-banner-dup');
      if (topBanner) {
        topBanner.classList.remove('hidden');
        document.getElementById('progress-text-dup').innerHTML = `
          <span class="progress-tag progress-tag-dup">Doublons</span>
          <span style="color:var(--app-warn); display:flex; align-items:center;">${icon('spinner', 'w-3.5 h-3.5')}</span>
          <span class="truncate">${escapeHtml(text)}</span>`;
        document.getElementById('progress-pct-dup').innerText = `${Math.round(pct)}%`;
        document.getElementById('progress-bar-dup').style.width = `${Math.min(100, Math.max(0, pct))}%`;
      }
      const wrap = document.getElementById('dup-progress-wrap');
      if (wrap) wrap.classList.add('hidden');
    }

    function hideDupProgress() {
      dupInProgress = false;
      const topBanner = document.getElementById('progress-banner-dup');
      if (topBanner) topBanner.classList.add('hidden');
      const wrap = document.getElementById('dup-progress-wrap');
      if (wrap) wrap.classList.add('hidden');
      const pickBtn = document.getElementById('btn-dup-pick-folders');
      if (pickBtn) pickBtn.disabled = false;
      renderScanDrivePills('duplicates');
    }

    function getDuplicateFileDrive(f) {
      const drv = (f.drive || f.Drive || '').trim().toUpperCase();
      if (drv.length >= 2 && drv[1] === ':') return drv.slice(0, 2);
      const p = (f.path || f.Path || '').trim();
      if (p.length >= 2 && p[1] === ':') return p.slice(0, 2).toUpperCase();
      return '';
    }

    function getDriveFilteredDuplicateGroups() {
      return (duplicateGroups || []).filter(g => {
        const files = g.files || g.Files || [];
        return files.some(f => {
          const drv = getDuplicateFileDrive(f);
          return !drv || activeDrives.has(drv);
        });
      });
    }

    function renderDuplicatesView() {
      const container = document.getElementById('duplicates-container');
      const toolbar = document.getElementById('dup-toolbar');
      const badge = document.getElementById('nav-count-duplicates');
      const filteredGroups = getDriveFilteredDuplicateGroups();
      if (badge) badge.innerText = filteredGroups.length;

      if (!duplicateGroups || duplicateGroups.length === 0) {
        if (toolbar) toolbar.classList.add('hidden');
        container.innerHTML = `
          <div class="surface-card rounded-2xl p-10 text-center space-y-2">
            <div class="flex justify-center opacity-45" style="color:var(--app-muted);">${icon('layers', 'w-7 h-7')}</div>
            <p class="text-xs" style="color:var(--app-muted);">Aucun doublon volumineux trouvé sur les disques ou dossiers sélectionnés.</p>
          </div>
        `;
        updateSelectionCalc();
        return;
      }

      if (filteredGroups.length === 0) {
        if (toolbar) toolbar.classList.add('hidden');
        container.innerHTML = `
          <div class="surface-card rounded-2xl p-10 text-center space-y-2">
            <div class="flex justify-center opacity-45" style="color:var(--app-muted);">${icon('layers', 'w-7 h-7')}</div>
            <p class="text-xs" style="color:var(--app-muted);">Aucun doublon trouvé pour les disques affichés.</p>
          </div>
        `;
        updateSelectionCalc();
        return;
      }

      if (toolbar) toolbar.classList.remove('hidden');
      document.getElementById('dup-groups-count').innerText = filteredGroups.length;
      const totalReclaim = filteredGroups.reduce((acc, g) => acc + (g.reclaimableGB ?? g.ReclaimableGB ?? 0), 0);
      document.getElementById('dup-reclaim-total').innerText = `${totalReclaim.toFixed(2)} Go`;

      container.innerHTML = filteredGroups.map(g => {
        const files = g.files || g.Files || [];
        const szText = (g.sizeGB ?? g.SizeGB ?? 0) >= 0.1 ? `${(g.sizeGB ?? g.SizeGB ?? 0).toFixed(2)} Go` : `${(g.sizeMB ?? g.SizeMB ?? 0).toFixed(1)} Mo`;
        const recText = (g.reclaimableGB ?? g.ReclaimableGB ?? 0) >= 0.1 ? `${(g.reclaimableGB ?? g.ReclaimableGB ?? 0).toFixed(2)} Go` : `${(g.reclaimableMB ?? g.ReclaimableMB ?? 0).toFixed(1)} Mo`;
        const firstName = files[0]?.name || files[0]?.Name || 'Fichier';
        const distinctNames = new Set(files.map(f => (f.name || f.Name || '').trim().toLowerCase()).filter(Boolean));
        const hasDifferentNames = distinctNames.size > 1;

        const rowsHtml = files.map((f, idx) => {
          const fid = f.id || f.Id;
          const fpath = f.path || f.Path;
          const fDrive = getDuplicateFileDrive(f);
          const isOtherDrive = !!(fDrive && !activeDrives.has(fDrive));
          const isSanct = f.isSanctuarized ?? f.IsSanctuarized ?? false;
          const isProt = f.isProtected ?? f.IsProtected ?? false;
          const locked = isSanct || isProt;
          const isDelete = selectedDuplicateIds.has(fid);
          return `
            <div class="item-row rounded-xl px-3.5 py-2.5 flex flex-wrap items-center justify-between gap-3 text-xs ${isDelete ? 'selected-delete' : ''}">
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="font-semibold truncate" style="color:var(--app-text);">${escapeHtml(f.name || f.Name)}</span>
                  ${fDrive ? `<span class="text-[10px] font-mono px-1.5 py-0.5 rounded" style="background:var(--app-surface-2); color:var(--app-muted);">${escapeHtml(fDrive)}</span>` : ''}
                  ${isOtherDrive ? `<span class="text-[10px] px-1.5 py-0.5 rounded font-medium" style="background:var(--app-surface-2); color:var(--app-muted); border:1px solid var(--app-border);">Autre disque</span>` : ''}
                  ${idx === 0 ? `<span class="text-[10px] px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1" style="background:var(--app-safe-soft); color:var(--app-safe);">${icon('shield-check', 'w-3 h-3')} Original conservé</span>` : ''}
                  ${locked ? `<span class="text-[10px] px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1" style="background:var(--app-warn-soft); color:var(--app-warn);">${icon('lock', 'w-3 h-3')} Protégé</span>` : ''}
                  <span class="text-[10px] font-mono tabular-nums" style="color:var(--app-muted);">${escapeHtml(f.lastModified || f.LastModified || '')}</span>
                </div>
                <div class="text-[11px] font-mono truncate opacity-75" style="color:var(--app-muted);">${escapeHtml(fpath)}</div>
              </div>
              <div class="flex items-center gap-2 shrink-0">
                <button onclick="openFolder('${safeEnc(fpath)}')" title="Ouvrir l'emplacement" class="btn-secondary p-1.5 rounded-lg inline-flex items-center justify-center">
                  ${icon('folder', 'w-3.5 h-3.5')}
                </button>
                ${locked ? '' : `
                  <div class="seg-group">
                    <button onclick="setDupAction('${safeEnc(fid)}', 'keep')" class="seg-btn ${!isDelete ? 'active-keep' : ''}">Garder</button>
                    <button onclick="setDupAction('${safeEnc(fid)}', 'delete')" class="seg-btn ${isDelete ? 'active-delete' : ''}">
                      ${isDelete ? icon('check', 'w-3 h-3') : ''}
                      <span>Supprimer</span>
                    </button>
                  </div>
                `}
              </div>
            </div>
          `;
        }).join('');

        return `
          <div class="surface-card rounded-2xl p-4 space-y-2.5">
            <div class="flex flex-wrap items-center justify-between gap-2 pb-2" style="border-bottom:1px solid var(--app-border);">
              <div class="flex items-center gap-2 flex-wrap">
                <span style="color:var(--app-muted);">${icon('file', 'w-4 h-4')}</span>
                <span class="text-xs font-bold" style="color:var(--app-text);">${escapeHtml(firstName)}</span>
                <span class="text-[10px] font-mono tabular-nums px-2 py-0.5 rounded" style="background:var(--app-surface-2); color:var(--app-muted);">${files.length} copies × ${szText}</span>
                <span class="text-[10px] font-mono opacity-75" style="color:var(--app-muted);">SHA256: ${escapeHtml(g.hash || g.Hash || '')}</span>
                ${hasDifferentNames ? `
                  <span title="Ces fichiers portent des noms différents mais leur contenu binaire est 100 % identique (même empreinte SHA-256)." class="dup-diff-names-badge inline-flex items-center gap-1 text-[10px] font-medium px-2 py-0.5 rounded-full">
                    ${icon('info', 'w-3 h-3')}
                    <span>Noms différents • Contenu identique</span>
                  </span>
                ` : ''}
              </div>
              <span class="text-xs font-mono font-bold tabular-nums" style="color:var(--app-safe);">+${recText} récupérables</span>
            </div>
            <div class="space-y-2">${rowsHtml}</div>
          </div>
        `;
      }).join('');
      updateSelectionCalc();
    }

    function setDupAction(encFid, action) {
      const fid = decodeURIComponent(encFid);
      if (action === 'delete') selectedDuplicateIds.add(fid);
      else selectedDuplicateIds.delete(fid);
      renderDuplicatesView();
    }

    function clearDuplicateSelection() {
      selectedDuplicateIds.clear();
      renderDuplicatesView();
    }

    function selectSuggestedDuplicates() {
      selectedDuplicateIds.clear();
      getDriveFilteredDuplicateGroups().forEach(g => {
        (g.files || g.Files || []).forEach(f => {
          if ((f.suggestedDelete ?? f.SuggestedDelete) && !(f.isSanctuarized ?? f.IsSanctuarized) && !(f.isProtected ?? f.IsProtected)) {
            selectedDuplicateIds.add(f.id || f.Id);
          }
        });
      });
      renderDuplicatesView();
    }

    function openDuplicatesCleanModal() {
      const selected = [];
      duplicateGroups.forEach(g => {
        (g.files || g.Files || []).forEach(f => {
          const fid = f.id || f.Id;
          if (selectedDuplicateIds.has(fid)) {
            selected.push({
              id: fid,
              name: `${f.name || f.Name} (Doublon)`,
              path: f.path || f.Path,
              category: 'downloads',
              drive: f.drive || f.Drive || '',
              sizeGB: f.sizeGB ?? f.SizeGB ?? 0,
              sizeMB: f.sizeMB ?? f.SizeMB ?? 0,
              isFolder: false
            });
          }
        });
      });
      if (selected.length === 0) {
        showToast('Cochez au moins un doublon à supprimer.', 'warn');
        return;
      }
      pendingDirectDeleteItems = selected;
      openCleanModalWithList(selected);
    }
