    let currentView = 'audit';
    let allItems = [];
    let protectedItems = [];
    let drivesList = [];
    let activeDrives = new Set();     // Multi-sélection active (1, 2, 3+ disques)
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
    let sortField = 'recommended';
    let sortAsc = true;
    let mainExpanded = true;
    let extraExpanded = false;
    let personalExpanded = false;
    let currentSettings = { geminiApiKey: '', enableAI: true };
    let activeResearchItem = null;
    let aiReportCache = {};
    let localReportCache = {};
    let selectedDeleteIds = new Set();
    let pendingDirectDeleteItems = null; // Utilisé si suppression depuis l'Explorateur d'Espace

    // Space Explorer state & cache
    let currentTreeNode = null;
    let currentTreePath = '';
    let explorerLoading = false;
    let treeCache = {};
    let pendingTreeScans = {};
    let explorerHistory = [];
    let explorerRequestCounter = 0;
    let activeExplorerRequestId = 0;

    const TREEMAP_COLORS = [
      { bg: 'bg-blue-500', dot: 'bg-blue-400', text: 'text-blue-300' },
      { bg: 'bg-cyan-500', dot: 'bg-cyan-400', text: 'text-cyan-300' },
      { bg: 'bg-indigo-500', dot: 'bg-indigo-400', text: 'text-indigo-300' },
      { bg: 'bg-purple-500', dot: 'bg-purple-400', text: 'text-purple-300' },
      { bg: 'bg-emerald-500', dot: 'bg-emerald-400', text: 'text-emerald-300' },
      { bg: 'bg-amber-500', dot: 'bg-amber-400', text: 'text-amber-300' },
      { bg: 'bg-rose-500', dot: 'bg-rose-400', text: 'text-rose-300' },
      { bg: 'bg-teal-500', dot: 'bg-teal-400', text: 'text-teal-300' }
    ];

    window.addEventListener('DOMContentLoaded', () => {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener('message', onBridgeMessage);
        postToBridge({ action: 'getDrives' });
        postToBridge({ action: 'getProtected' });
        postToBridge({ action: 'getSettings' });
        postToBridge({ action: 'getHistory' });
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
        showToast(`Audit terminé : ${allItems.length} éléments analysés.`, 'success');
      } else if (type === 'duplicatesProgress') {
        showDupProgress(data.text || data.Text || 'Analyse des doublons...', data.pct ?? data.Pct ?? 0);
      } else if (type === 'duplicatesResult') {
        hideDupProgress();
        duplicateGroups = data.groups || data.Groups || [];
        renderDuplicatesView();
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
      } else if (type === 'protectedList') {
        protectedItems = data.items || data.Items || [];
        syncProtectedStatus();
        document.getElementById('nav-count-protected').innerText = protectedItems.length;
        renderProtectedView();
        if (allItems.length > 0) refreshAuditUI();
      } else if (type === 'researchResult') {
        const rep = data.report || data.Report;
        if (rep && (rep.itemPath || rep.ItemPath)) {
          const rKey = (rep.itemPath || rep.ItemPath).toLowerCase();
          if (rep.isAiEnriched || rep.IsAiEnriched) {
            aiReportCache[rKey] = rep;
          } else {
            localReportCache[rKey] = rep;
          }
          if (data.aiError && aiReportCache[rKey]) {
            resetAiDeepButton(true);
            showToast(data.aiError, 'warn');
            return;
          }
        }
        showResearchModal(rep);
        if (data.aiError) {
          showToast(data.aiError, 'warn');
        }
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
        if (node) {
          const nKey = normalizeTreeKey(node.path || node.Path || '');
          delete pendingTreeScans[nKey];
          treeCache[nKey] = node;
          renderExplorerDrivePills();
          if (data.requestId === activeExplorerRequestId || nKey === normalizeTreeKey(currentTreePath)) {
            explorerLoading = false;
            currentTreeNode = node;
            currentTreePath = node.path || node.Path || currentTreePath;
            renderExplorerTree();
          }
        }
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
      const colors = kind === 'success'
        ? 'bg-emerald-950/95 border-emerald-500/45 text-emerald-200'
        : (kind === 'warn' ? 'bg-amber-950/95 border-amber-500/45 text-amber-200' : 'bg-slate-900/95 border-blue-500/45 text-slate-200');
      const el = document.createElement('div');
      el.className = `toast-anim pointer-events-auto px-4 py-2.5 rounded-xl border shadow-2xl text-xs font-medium flex items-center gap-2 ${colors}`;
      el.innerHTML = `<span>${kind === 'success' ? '✓' : (kind === 'warn' ? '⚠️' : 'ℹ️')}</span><span>${escapeHtml(message)}</span>`;
      container.appendChild(el);
      setTimeout(() => {
        el.style.opacity = '0';
        el.style.transition = 'opacity 0.25s';
        setTimeout(() => el.remove(), 260);
      }, 3600);
    }

    // ================= VIEW SWITCHING =================
    function switchView(view) {
      currentView = view;
      ['audit', 'explorer', 'duplicates', 'protected', 'history'].forEach(v => {
        document.getElementById(`view-${v}`).classList.toggle('hidden', v !== view);
        document.getElementById(`nav-${v}`).classList.toggle('active', v === view);
      });

      const titles = {
        audit: 'Audit Intelligent Multi-Disques',
        explorer: "Explorateur d'Espace & Arborescence",
        duplicates: 'Doublons Volumineux Multi-Disques',
        protected: 'Liste Protégée Permanente',
        history: 'Historique des Nettoyages'
      };
      document.getElementById('view-title').innerText = titles[view] || 'Cleaner Pro';
      document.getElementById('topbar-search-wrap').classList.toggle('hidden', view !== 'audit');
      updateHeaderSubtitle();

      if (view === 'explorer' && !currentTreeNode && drivesList.length > 0) {
        const firstActive = Array.from(activeDrives)[0] || (drivesList[0].name || drivesList[0].Name || 'C:');
        navigateExplorer(firstActive.endsWith('\\') ? firstActive : `${firstActive}\\`);
      } else if (view === 'history') {
        postToBridge({ action: 'getHistory' });
      }
      updateSelectionCalc();
    }

    function updateHeaderSubtitle() {
      const el = document.getElementById('view-subtitle');
      if (currentView === 'audit') {
        const list = Array.from(activeDrives).sort().join(', ');
        el.innerText = list ? `Disques actifs : ${list}` : 'Aucun disque actif';
      } else if (currentView === 'explorer') {
        el.innerText = currentTreePath ? `Dossier analysé : ${currentTreePath}` : 'Visualisez quels dossiers consomment le plus d\'espace';
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

      const barColors = ['bg-emerald-500', 'bg-blue-500', 'bg-purple-500', 'bg-amber-500'];
      container.innerHTML = drivesList.map((d, idx) => {
        const name = (d.name || d.Name || 'C:').toUpperCase();
        const label = d.label || d.Label || 'Disque Local';
        const freeGB = d.freeGB ?? d.FreeGB ?? 0;
        const totalGB = d.totalGB ?? d.TotalGB ?? 0;
        const pctUsed = d.pctUsed ?? d.PctUsed ?? 0;
        const isOn = activeDrives.has(name);
        const notYetScanned = allItems.length > 0 && isOn && scannedDrives.size > 0 && !scannedDrives.has(name);
        const barColor = barColors[idx % barColors.length];

        return `
          <div onclick="toggleDrive('${safeEnc(name)}')" class="drive-pill ${isOn ? 'active' : 'opacity-55'} cursor-pointer rounded-xl p-2.5 space-y-1.5">
            <div class="flex items-center justify-between gap-1.5">
              <div class="flex items-center gap-2 min-w-0">
                <div class="w-4 h-4 rounded flex items-center justify-center border ${isOn ? 'bg-blue-600 border-blue-400 text-white' : 'bg-slate-950 border-white/20 text-transparent'}">
                  <svg class="w-2.5 h-2.5" fill="none" stroke="currentColor" stroke-width="3" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5"/></svg>
                </div>
                <span class="text-xs font-bold text-white">${escapeHtml(name)}</span>
                <span class="text-[10px] text-slate-400 truncate">${escapeHtml(label)}</span>
              </div>
              <button onclick="event.stopPropagation(); isolateDrive('${safeEnc(name)}')" title="Afficher uniquement ${escapeHtml(name)}" class="text-[9px] px-1.5 py-0.5 rounded bg-white/[0.06] hover:bg-blue-500/25 text-slate-300 hover:text-blue-200">Seul</button>
            </div>
            <div class="w-full h-1.5 rounded-full bg-slate-950 overflow-hidden">
              <div class="${barColor} h-full rounded-full transition-all duration-300" style="width: ${pctUsed}%;"></div>
            </div>
            <div class="flex items-center justify-between text-[10px] font-mono tabular-nums">
              <span class="text-slate-400">${freeGB.toFixed(0)} / ${totalGB.toFixed(0)} Go libres</span>
              ${notYetScanned ? '<span class="text-amber-400">Non scanné</span>' : `<span class="text-slate-500">${pctUsed}%</span>`}
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
    }

    // ================= AUDIT =================
    function startAudit() {
      if (auditCustomFolders.length === 0 && auditScanDrives.size === 0) {
        showToast('Sélectionnez au moins un disque ou un dossier à analyser.', 'warn');
        return;
      }
      auditInProgress = true;
      if (auditCustomFolders.length === 0) {
        scannedDrives = new Set(auditScanDrives);
        // Aligner le filtre d'affichage initial sur les disques scannés
        activeDrives = new Set(auditScanDrives);
      } else {
        scannedDrives = new Set(drivesList.map(d => (d.name || d.Name || '').toUpperCase()));
        activeDrives = new Set(scannedDrives);
      }
      renderSidebarDrives();
      renderScanDrivePills('audit');
      const btn = document.getElementById('btn-audit-launch');
      const pickBtn = document.getElementById('btn-audit-pick-folders');
      if (btn) { btn.disabled = true; btn.innerText = 'Audit en cours...'; }
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
      document.getElementById('progress-banner').classList.remove('hidden');
      document.getElementById('progress-text').innerHTML = `
        <svg class="animate-spin h-4 w-4 text-blue-400" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path></svg>
        ${escapeHtml(text)}`;
      document.getElementById('progress-pct').innerText = `${pct}%`;
      document.getElementById('progress-bar').style.width = `${pct}%`;
    }

    function hideProgress() {
      auditInProgress = false;
      document.getElementById('progress-banner').classList.add('hidden');
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

    function renderItems() {
      const container = document.getElementById('items-container');
      const filtered = filterItemsList();
      document.getElementById('kpi-visible-count').innerText = filtered.length;

      if (allItems.length === 0) return;

      if (filtered.length === 0) {
        container.innerHTML = `<div class="surface-card rounded-xl p-10 text-center text-xs text-slate-400">Aucun élément trouvé pour les disques et filtres sélectionnés.</div>`;
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
            <button onclick="toggleMainSection()" class="w-full flex items-center justify-between px-4 py-2.5 rounded-xl surface-card hover:bg-slate-800/70 transition-all text-left border-blue-500/20">
              <div class="flex items-center gap-2.5">
                <span class="text-xs font-mono text-blue-300">${mainExpanded ? '▼' : '▶'}</span>
                <div>
                  <span class="text-xs font-bold text-white">Audit Intelligent Recommandé</span>
                  <span class="text-[11px] text-slate-400 ml-2">(${mainItems.length} éléments • ${mainTotalGB.toFixed(1)} Go)</span>
                </div>
              </div>
              <span class="text-[11px] text-blue-400 font-semibold">${mainExpanded ? 'Replier' : 'Déplier'}</span>
            </button>
            <div class="${mainExpanded ? 'space-y-2' : 'hidden'}">
              ${mainItems.map(renderCardHtml).join('')}
            </div>
          </div>
        `;
      } else {
        html += `<div class="surface-card rounded-xl p-4 text-center text-xs text-slate-400">Aucun reliquat ou élément inactif recommandé pour ces disques.</div>`;
      }

      if (extraItems.length > 0) {
        const extraTotalGB = extraItems.reduce((acc, x) => acc + getItemSizeGB(x), 0);
        html += `
          <div class="mt-4 pt-4 border-t border-white/[0.06]">
            <button onclick="toggleExtraSection()" class="surface-card hover:bg-slate-800/70 border w-full flex items-center justify-between px-4 py-2.5 rounded-xl transition-all text-left">
              <div class="flex items-center gap-2.5">
                <span class="text-xs font-mono text-slate-400">${extraExpanded ? '▼' : '▶'}</span>
                <div>
                  <span class="text-xs font-bold text-slate-200">Logiciels & jeux récents / actifs</span>
                  <span class="text-[11px] text-slate-400 ml-2">(${extraItems.length} éléments • ${extraTotalGB.toFixed(1)} Go)</span>
                </div>
              </div>
              <span class="text-[11px] text-blue-400 font-semibold">${extraExpanded ? 'Replier' : 'Déplier'}</span>
            </button>
            <div class="${extraExpanded ? 'mt-2.5 space-y-2' : 'hidden'}">
              ${extraItems.map(renderCardHtml).join('')}
            </div>
          </div>
        `;
      }

      if (personalItems.length > 0) {
        const personalTotalGB = personalItems.reduce((acc, x) => acc + getItemSizeGB(x), 0);
        html += `
          <div class="mt-4 pt-4 border-t border-white/[0.06]">
            <button onclick="togglePersonalSection()" class="surface-card hover:bg-slate-800/70 border border-amber-500/20 w-full flex items-center justify-between px-4 py-2.5 rounded-xl transition-all text-left">
              <div class="flex items-center gap-2.5">
                <span class="text-xs font-mono text-amber-300">${personalExpanded ? '▼' : '▶'}</span>
                <div>
                  <span class="text-xs font-bold text-amber-200">📁 Fichiers Personnels (Documents, Vidéos, Images, Musique, Bureau)</span>
                  <span class="text-[11px] text-slate-400 ml-2">(${personalItems.length} éléments • ${personalTotalGB.toFixed(1)} Go)</span>
                </div>
              </div>
              <span class="text-[11px] text-amber-400 font-semibold">${personalExpanded ? 'Replier' : 'Déplier'}</span>
            </button>
            <div class="${personalExpanded ? 'mt-2.5 space-y-2' : 'hidden'}">
              ${personalItems.map(renderCardHtml).join('')}
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
      if (mArr) mArr.innerText = mainExpanded ? '▼' : '▶';
      if (eArr) eArr.innerText = extraExpanded ? '▼' : '▶';
      if (pArr) pArr.innerText = personalExpanded ? '▼' : '▶';
      if (pBtn) pBtn.className = `w-full flex items-center justify-between px-2.5 py-2 rounded-xl border text-xs transition-all ${personalExpanded ? 'border-amber-500/35 bg-amber-950/20 text-amber-100' : 'border-white/[0.07] bg-slate-900/50 text-slate-400'}`;
      if (mBtn) mBtn.className = `w-full flex items-center justify-between px-2.5 py-2 rounded-xl border text-xs transition-all ${mainExpanded ? 'border-blue-500/35 bg-blue-950/25 text-slate-100' : 'border-white/[0.07] bg-slate-900/50 text-slate-400'}`;
      if (eBtn) eBtn.className = `w-full flex items-center justify-between px-2.5 py-2 rounded-xl border text-xs transition-all ${extraExpanded ? 'border-blue-500/35 bg-blue-950/25 text-slate-100' : 'border-white/[0.07] bg-slate-900/50 text-slate-400'}`;
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

    function formatAgeBadge(ageDays, lastMod) {
      if (ageDays >= 365) {
        const yrs = Math.floor(ageDays / 365);
        return `<span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-amber-500/15 text-amber-300 border border-amber-500/30">Inactif ${yrs} an${yrs > 1 ? 's' : ''}</span>`;
      }
      if (ageDays >= 120) {
        const mos = Math.floor(ageDays / 30);
        return `<span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800 text-slate-300 border border-white/[0.08]">${mos} mois</span>`;
      }
      return lastMod ? `<span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800/70 text-slate-400">${escapeHtml(lastMod)}</span>` : '';
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
      const riskBadge = riskPct <= 10
        ? 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30'
        : (riskPct <= 35 ? 'bg-blue-500/15 text-blue-300 border-blue-500/30' : (riskPct <= 65 ? 'bg-amber-500/15 text-amber-300 border-amber-500/30' : 'bg-rose-500/15 text-rose-300 border-rose-500/30'));

      return `
        <div class="item-row rounded-xl px-4 py-3 flex flex-col lg:flex-row lg:items-center justify-between gap-3 ${isDelChecked ? 'border-rose-500/50 bg-rose-950/20' : ''}">
          <div class="flex-1 min-w-0 space-y-1">
            <div class="flex items-center gap-2 flex-wrap">
              <span class="text-xs font-bold text-white truncate max-w-md">${escapeHtml(name)}</span>
              <span class="text-[10px] font-mono px-2 py-0.5 rounded-md bg-blue-500/15 text-blue-300 border border-blue-500/25 font-bold tabular-nums">${szText}</span>
              <span class="text-[10px] font-mono px-2 py-0.5 rounded-md border ${riskBadge} font-semibold tabular-nums">Risque ${riskPct}%</span>
              ${drive ? `<span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800 text-slate-300 border border-white/[0.08]">${escapeHtml(drive)}</span>` : ''}
              ${formatAgeBadge(ageDays, lastMod)}
              ${isSanct ? `<span title="Dossier système ou personnel protégé par défaut" class="text-[10px] px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">🔒 Protégé (Système / Perso)</span>` : ''}
              ${isProt ? `<span class="text-[10px] px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">🛡️ Protégé</span>` : ''}
            </div>
            <p class="text-[11px] text-slate-400 truncate">${escapeHtml(desc)}</p>
            <p class="text-[10px] font-mono text-slate-500 truncate">${escapeHtml(path)}</p>
          </div>

          <div class="flex items-center gap-1.5 shrink-0 flex-wrap">
            <button onclick="setItemAction('${safeEnc(id)}', 'delete')" ${isSanct || isProt ? 'disabled' : ''} class="px-2.5 py-1.5 rounded-lg text-[11px] font-semibold border transition-all ${isDelChecked ? 'bg-rose-600 text-white border-rose-500 shadow-md shadow-rose-600/30' : 'bg-slate-950/70 text-slate-300 border-white/[0.08] hover:border-rose-500/40 hover:text-rose-300'} disabled:opacity-35 disabled:pointer-events-none">
              ${isDelChecked ? '✓ À supprimer' : 'Supprimer'}
            </button>
            <button onclick="setItemAction('${safeEnc(id)}', 'keep')" class="px-2.5 py-1.5 rounded-lg text-[11px] font-medium border transition-all ${!isDelChecked && !isProt ? 'bg-emerald-600/20 text-emerald-300 border-emerald-500/35' : 'bg-slate-950/70 text-slate-400 border-white/[0.08] hover:text-white'}">
              Garder
            </button>
            <button onclick="protectItem('${safeEnc(id)}')" ${isProt ? 'disabled' : ''} title="Protéger définitivement" class="px-2.5 py-1.5 rounded-lg text-[11px] font-medium border border-indigo-500/30 bg-indigo-950/30 hover:bg-indigo-600/30 text-indigo-300 disabled:opacity-35">
              🛡️ Protéger
            </button>
            <button onclick="askResearch('${safeEnc(id)}')" class="px-2.5 py-1.5 rounded-lg text-[11px] font-medium border border-white/[0.09] bg-slate-800/75 hover:bg-slate-700 text-amber-300">
              ❓ Infos
            </button>
            <button onclick="openFolder('${safeEnc(path)}')" title="Ouvrir dans l'Explorateur" class="p-1.5 rounded-lg border border-white/[0.09] bg-slate-800/75 hover:bg-slate-700 text-slate-300">
              📂
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
      document.getElementById('live-selected-gb').innerText = `${totalGB.toFixed(2)} Go`;
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
        const isCached = !!treeCache[rootKey];
        const isScanning = !!pendingTreeScans[rootKey];
        return `
          <button onclick="navigateExplorerEnc('${safeEnc(name + '\\')}')" class="px-3 py-1.5 rounded-lg text-xs font-semibold border transition-all flex items-center gap-1.5 ${isCurrent ? 'bg-cyan-600/25 border-cyan-400/50 text-cyan-200' : 'bg-slate-900/80 border-white/[0.08] text-slate-300 hover:text-white'}">
            ${isScanning ? '<span class="w-1.5 h-1.5 rounded-full bg-amber-400 animate-ping"></span>' : (isCached ? '<span class="w-1.5 h-1.5 rounded-full bg-emerald-400" title="En mémoire (accès instantané)"></span>' : '')}
            <span>${escapeHtml(name)}</span>
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
      postToBridge({ action: 'scanFolderTree', path: targetPath, requestId: activeExplorerRequestId });
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
        delete treeCache[normalizeTreeKey(currentTreePath)];
        navigateExplorer(currentTreePath, true);
      }
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
      document.getElementById('explorer-breadcrumb').innerHTML = `<span class="text-cyan-300">${escapeHtml(currentTreePath)}</span>`;
      document.getElementById('explorer-tree-container').innerHTML = `
        <div class="surface-card rounded-xl p-10 text-center space-y-3 max-w-xl mx-auto">
          <svg class="animate-spin h-6 w-6 text-cyan-400 mx-auto" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path></svg>
          <div id="explorer-progress-text" class="text-xs font-medium text-slate-200">${escapeHtml(statusText || `Calcul de l'arborescence pour ${currentTreePath}...`)}</div>
          <div class="w-full bg-slate-950 rounded-full h-2 overflow-hidden border border-white/[0.06]">
            <div id="explorer-progress-bar" class="bg-gradient-to-r from-cyan-500 to-blue-500 h-full rounded-full transition-all duration-200" style="width: ${pct}%;"></div>
          </div>
          <div id="explorer-progress-pct" class="text-[11px] font-mono text-cyan-300 tabular-nums">${pct}%</div>
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

      // Breadcrumb segments
      const cleanPath = (node.path || node.Path || 'C:\\').replace(/[\\\/]+$/, '');
      const parts = cleanPath.split('\\').filter(Boolean);
      let acc = '';
      const bcHtml = parts.map((part, idx) => {
        acc = idx === 0 ? `${part}\\` : `${acc}${acc.endsWith('\\') ? '' : '\\'}${part}`;
        const target = acc;
        return `
          ${idx > 0 ? '<span class="text-slate-600">›</span>' : ''}
          <button onclick="navigateExplorerEnc('${safeEnc(target)}')" class="px-1.5 py-0.5 rounded hover:bg-white/[0.08] ${idx === parts.length - 1 ? 'text-cyan-300 font-bold' : 'text-slate-300'}">${escapeHtml(idx === 0 ? part + '\\' : part)}</button>
        `;
      }).join('');
      document.getElementById('explorer-breadcrumb').innerHTML = bcHtml;

      const children = node.children || node.Children || [];
      const totalFiles = node.fileCount ?? node.FileCount ?? 0;
      document.getElementById('explorer-total-meta').innerText = `${formatSizeNode(node)} • ${totalFiles.toLocaleString('fr-FR')} fichiers`;

      // Visual Treemap Bar (top 8)
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
          return `<div onclick="${isFolder ? `navigateExplorerEnc('${safeEnc(cpath)}')` : `askResearchPath('${safeEnc(cname)}','${safeEnc(cpath)}')`}" title="${escapeHtml(cname)} (${formatSizeNode(c)} — ${p}%)" class="${col.bg} h-full rounded-md cursor-pointer hover:brightness-110 transition-all" style="width: ${Math.max(p, 1.5)}%"></div>`;
        }).join('') + (restPct > 1 ? `<div title="Autres éléments (${restPct.toFixed(1)}%)" class="bg-slate-700/70 h-full rounded-md" style="width: ${restPct}%"></div>` : '');

        legEl.innerHTML = topForBar.map((c, idx) => {
          const col = TREEMAP_COLORS[idx % TREEMAP_COLORS.length];
          const p = c.pctOfParent ?? c.PctOfParent ?? 0;
          const cname = c.name || c.Name;
          const cpath = c.path || c.Path;
          const isFolder = c.isFolder ?? c.IsFolder ?? true;
          return `
            <button onclick="${isFolder ? `navigateExplorerEnc('${safeEnc(cpath)}')` : `askResearchPath('${safeEnc(cname)}','${safeEnc(cpath)}')`}" class="flex items-center gap-1.5 text-[11px] bg-slate-900/80 hover:bg-slate-800 px-2 py-1 rounded-lg border border-white/[0.06]">
              <span class="w-2 h-2 rounded-full ${col.dot}"></span>
              <span class="text-slate-200 max-w-[140px] truncate">${escapeHtml(cname)}</span>
              <span class="font-mono ${col.text}">${p}%</span>
            </button>
          `;
        }).join('');
      } else {
        visualWrap.classList.add('hidden');
      }

      // Rows
      const container = document.getElementById('explorer-tree-container');
      if (children.length === 0) {
        container.innerHTML = `<div class="surface-card rounded-xl p-8 text-center text-xs text-slate-400">Ce dossier est vide ou inaccessible.</div>`;
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
        const barAccent = idx < 8 ? TREEMAP_COLORS[idx % TREEMAP_COLORS.length].bg : 'bg-slate-500';

        const iconSvg = isFolder
          ? `<svg class="w-4 h-4 text-amber-400 shrink-0" fill="currentColor" viewBox="0 0 24 24"><path d="M19.5 21a3 3 0 003-3v-4.5a3 3 0 00-3-3h-15a3 3 0 00-3 3V18a3 3 0 003 3h15zM1.5 10.146V6a3 3 0 013-3h5.379a2.25 2.25 0 011.59.659l2.122 2.121c.14.141.331.22.53.22H19.5a3 3 0 013 3v1.146A4.483 4.483 0 0019.5 9h-15a4.483 4.483 0 00-3 1.146z"/></svg>`
          : `<svg class="w-4 h-4 text-slate-400 shrink-0" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z"/></svg>`;

        return `
          <div ${isFolder ? `onclick="navigateExplorerEnc('${safeEnc(cpath)}')"` : ''} class="item-row relative overflow-hidden rounded-xl px-4 py-2.5 flex items-center justify-between gap-3 ${isFolder ? 'cursor-pointer' : ''}">
            <!-- Background weight bar -->
            <div class="absolute inset-y-0 left-0 ${barAccent} opacity-[0.07] pointer-events-none" style="width: ${Math.min(pct, 100)}%"></div>

            <div class="relative flex items-center gap-3 min-w-0 flex-1">
              ${iconSvg}
              <div class="min-w-0">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="text-xs font-semibold text-white truncate">${escapeHtml(cname)}</span>
                  ${isFolder ? `<span class="text-[10px] text-slate-500 font-mono">${fCount.toLocaleString('fr-FR')} fichiers</span>` : ''}
                  ${lastMod ? `<span class="text-[10px] text-slate-500">${escapeHtml(lastMod)}</span>` : ''}
                  ${isSanct ? `<span title="Dossier système ou personnel protégé par défaut" class="text-[10px] px-1.5 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">🔒 Protégé (Système / Perso)</span>` : ''}
                  ${!isSanct && isPersonalSub ? `<span title="Situé dans Documents/Vidéos (protégé de l'audit auto, suppression manuelle autorisée)" class="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-300 border border-white/[0.1]">📁 Dossier Perso</span>` : ''}
                  ${isProt ? `<span class="text-[10px] px-1.5 py-0.5 rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">🛡️ Protégé</span>` : ''}
                </div>
              </div>
            </div>

            <div class="relative flex items-center gap-3 shrink-0">
              <div class="text-right">
                <div class="text-xs font-bold font-mono text-white tabular-nums">${formatSizeNode(c)}</div>
                <div class="text-[10px] font-mono text-slate-400 tabular-nums">${pct}%</div>
              </div>

              <div class="flex items-center gap-1" onclick="event.stopPropagation()">
                <button onclick="askResearchPath('${safeEnc(cname)}', '${safeEnc(cpath)}')" title="Analyser avec l'inspecteur & Gemini IA" class="px-2 py-1 rounded-lg bg-slate-800/80 hover:bg-slate-700 border border-white/[0.08] text-[11px] text-amber-300">❓</button>
                <button onclick="openFolder('${safeEnc(cpath)}')" title="Ouvrir dans l'Explorateur Windows" class="px-2 py-1 rounded-lg bg-slate-800/80 hover:bg-slate-700 border border-white/[0.08] text-[11px] text-slate-300">📂</button>
                <button onclick="promptDeleteTreeNode(${idx})" ${isSanct || isProt ? 'disabled' : ''} title="Supprimer cet élément" class="px-2 py-1 rounded-lg bg-slate-800/80 hover:bg-rose-900/70 border border-white/[0.08] hover:border-rose-500/40 text-[11px] text-rose-300 disabled:opacity-30 disabled:pointer-events-none">🗑️</button>
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
        container.innerHTML = `<div class="surface-card rounded-2xl p-10 text-center text-xs text-slate-400">Votre Liste Protégée est vide. Cliquez sur <strong>🛡️ Protéger</strong> dans l'audit pour sanctuariser un jeu ou logiciel.</div>`;
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
          <div class="item-row rounded-xl px-4 py-3 flex items-center justify-between gap-3 border-indigo-500/30">
            <div class="min-w-0 space-y-1">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="text-xs font-bold text-white truncate">${escapeHtml(name)}</span>
                <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300">${szText}</span>
                <span class="text-[10px] text-slate-400">Protégé le ${escapeHtml(date)}</span>
              </div>
              <p class="text-[10px] font-mono text-slate-500 truncate">${escapeHtml(path)}</p>
            </div>
            <button onclick="unprotectItem('${safeEnc(id)}')" class="px-3 py-1.5 rounded-lg bg-slate-900 hover:bg-rose-950/60 text-slate-300 hover:text-rose-300 text-xs font-medium border border-white/[0.08] shrink-0">
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
      triggerResearch(decodeURIComponent(encName), decodeURIComponent(encPath), 15);
    }

    function triggerResearch(name, path, riskPct) {
      activeResearchItem = { name, path, riskPct };
      const cached = aiReportCache[path.toLowerCase()];
      if (cached) {
        showResearchModal(cached);
        return;
      }
      resetAiDeepButton();
      postToBridge({ action: 'researchItem', name, path, riskPct });
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
      riskEl.className = `font-bold font-mono text-sm ${risk <= 10 ? 'text-emerald-400' : (risk <= 35 ? 'text-blue-400' : (risk <= 65 ? 'text-amber-400' : 'text-rose-400'))}`;
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
          btnLocal.className = `px-2.5 py-1 rounded-md font-medium transition-all ${!isAi ? 'bg-blue-600 text-white' : 'text-slate-300 hover:text-white'}`;
          btnAi.className = `px-2.5 py-1 rounded-md font-medium transition-all ${isAi ? 'bg-purple-600 text-white' : 'text-slate-300 hover:text-white'}`;
        }
      }

      resetAiDeepButton(hasAi);
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
      document.getElementById('modal-research').classList.add('hidden');
    }

    function resetAiDeepButton(hasAi = false) {
      document.getElementById('ai-spinner').classList.add('hidden');
      document.getElementById('ai-deep-label').innerText = hasAi ? "Réanalyser avec l'IA" : "Approfondir avec l'IA";
      document.getElementById('btn-ai-deep-research').disabled = false;
    }

    function askAiDeepResearch() {
      if (!activeResearchItem) return;
      if (!currentSettings.geminiApiKey) {
        showToast('Configurez votre clé API Gemini dans « ✨ Mode IA ».', 'warn');
        openAiSettingsModal();
        return;
      }
      document.getElementById('ai-spinner').classList.remove('hidden');
      document.getElementById('ai-deep-label').innerText = "Analyse Gemini en cours...";
      document.getElementById('btn-ai-deep-research').disabled = true;
      postToBridge({
        action: 'researchItemAI',
        name: activeResearchItem.name,
        path: activeResearchItem.path,
        riskPct: activeResearchItem.riskPct,
        apiKey: currentSettings.geminiApiKey || ''
      });
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
        document.getElementById('api-test-status').innerHTML = '<span class="text-amber-400">Veuillez saisir une clé.</span>';
        return;
      }
      document.getElementById('test-spinner').classList.remove('hidden');
      document.getElementById('api-test-status').innerHTML = '<span class="text-slate-400">Test en cours...</span>';
      postToBridge({ action: 'testGeminiApiKey', apiKey: key });
    }

    function handleApiKeyTestResult(success, message) {
      document.getElementById('test-spinner').classList.add('hidden');
      const statusEl = document.getElementById('api-test-status');
      const msg = escapeHtml(message || (success ? '✓ Clé valide & connectée !' : '✗ Clé invalide ou erreur réseau'));
      statusEl.innerHTML = success
        ? `<span class="text-emerald-400 font-semibold">${msg}</span>`
        : `<span class="text-rose-400 font-semibold break-all">${msg}</span>`;
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
      if (currentSettings.geminiApiKey && currentSettings.geminiApiKey.length > 5) {
        indicator.className = "w-2 h-2 rounded-full bg-emerald-400 shadow-sm shadow-emerald-400/80 animate-pulse";
        label.innerText = "Mode IA Actif";
      } else {
        indicator.className = "w-2 h-2 rounded-full bg-slate-500";
        label.innerText = "Mode IA Gemini";
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
        return `<div class="flex items-center justify-between text-slate-300 py-0.5"><span>• ${escapeHtml(name)}</span><span class="text-blue-400 font-bold font-mono">${szText}</span></div>`;
      }).join('');

      const breakdownEl = document.getElementById('clean-drive-breakdown');
      if (breakdownEl) {
        breakdownEl.innerHTML = Object.entries(byDrive).map(([d, gb]) =>
          `<span class="px-2 py-0.5 rounded bg-slate-800 border border-white/[0.08] text-slate-300 font-mono">${escapeHtml(d)} : <strong class="text-emerald-400">${gb.toFixed(2)} Go</strong></span>`
        ).join('');
      }

      const warnEl = document.getElementById('clean-running-warning');
      if (warnEl) {
        if (runningNames.length > 0) {
          warnEl.classList.remove('hidden');
          warnEl.innerHTML = `⚠️ <strong>Processus actifs détectés :</strong> ${escapeHtml(runningNames.join(', '))} — les processus liés seront fermés avant suppression.`;
        } else {
          warnEl.classList.add('hidden');
        }
      }

      const recycleRadio = document.getElementById('clean-mode-recycle');
      if (recycleRadio) recycleRadio.checked = true;

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
      btn.disabled = true;
      btn.innerText = "Nettoyage en cours...";
      postToBridge({ action: 'executeClean', items: selected, useRecycleBin, allowManualPersonalChild });
    }

    function onCleanCompleted(res) {
      const wasDirectTree = pendingDirectDeleteItems !== null;
      closeCleanModal();
      const btn = document.getElementById('btn-confirm-delete');
      btn.disabled = false;
      btn.innerHTML = `<span>🚀</span> Lancer le Nettoyage Silencieux`;

      const deletedIds = res.deletedIds || res.DeletedIds || [];
      const deletedPaths = res.deletedPaths || res.DeletedPaths || [];
      const errors = res.errors || res.Errors || [];
      if (deletedIds.length > 0 && !wasDirectTree) {
        allItems = allItems.filter(x => !deletedIds.includes(x.id || x.Id));
        deletedIds.forEach(id => selectedDeleteIds.delete(id));
      }
      if (deletedPaths.length > 0) {
        // Invalider le cache Space Explorer pour les dossiers touchés
        treeCache = {};
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
        list.innerHTML = `<div class="surface-card rounded-xl p-10 text-center text-xs text-slate-500">Aucun nettoyage enregistré pour le moment.</div>`;
        return;
      }
      list.innerHTML = history.map(h => `
        <div class="item-row rounded-xl p-3.5 space-y-1">
          <div class="flex items-center justify-between text-xs">
            <span class="font-semibold text-white">${escapeHtml(h.date || h.Date)}</span>
            <span class="font-mono font-bold text-emerald-400 tabular-nums">+${(h.freedGB ?? h.FreedGB ?? 0).toFixed(2)} Go (${h.itemsCount ?? h.ItemsCount} éléments)</span>
          </div>
          <p class="text-[11px] text-slate-400 truncate">${escapeHtml((h.itemNames || h.ItemNames || []).join(', '))}</p>
        </div>
      `).join('');
    }

    // ================= AUDIT EVOLUTION & WINSXS =================
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
        const cls = deltaFree >= 0 ? 'text-emerald-400' : 'text-amber-300';
        return `<span class="font-mono ${cls}">${escapeHtml(cname)} ${sign}${deltaFree.toFixed(2)} Go libres</span>`;
      }).filter(Boolean);

      if (diffs.length === 0) {
        el.classList.add('hidden');
        return;
      }
      el.classList.remove('hidden');
      el.innerHTML = `
        <span class="text-cyan-200">📊 <strong>Évolution depuis le dernier audit</strong> (${escapeHtml(prev.date || prev.Date || '')}) :</span>
        <div class="flex flex-wrap items-center gap-3">${diffs.join(' • ')}</div>
      `;
    }

    function analyzeWinSxS() {
      const btn = document.getElementById('btn-analyze-winsxs');
      const txt = document.getElementById('winsxs-summary-text');
      if (btn) { btn.disabled = true; btn.innerText = 'Analyse DISM...'; }
      if (txt) txt.innerText = 'Analyse lecture seule de WinSxS en cours (DISM)...';
      postToBridge({ action: 'analyzeWinSxS' });
    }

    function renderWinSxSResult(report) {
      const btn = document.getElementById('btn-analyze-winsxs');
      const txt = document.getElementById('winsxs-summary-text');
      if (btn) { btn.disabled = false; btn.innerText = '↻ Réanalyser WinSxS'; }
      if (!txt || !report) return;

      if (report.requiresAdmin || report.RequiresAdmin) {
        txt.innerHTML = `<span class="text-amber-300">🔒 ${escapeHtml(report.message || report.Message)}</span>`;
        return;
      }
      const actual = report.actualSize || report.ActualSize || '?';
      const reclaim = report.reclaimablePackages ?? report.ReclaimablePackages ?? '?';
      const msg = report.message || report.Message || '';
      txt.innerHTML = `<strong>WinSxS :</strong> Taille réelle <span class="font-mono text-cyan-300">${escapeHtml(actual)}</span> • Packages récupérables : <span class="font-mono text-emerald-400">${escapeHtml(String(reclaim))}</span> — <span class="text-slate-400">${escapeHtml(msg)}</span>`;
    }

    // ================= SCAN TARGETS (DRIVES & MULTI-FOLDERS FOR AUDIT & DUPLICATES) =================
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
        el.innerHTML = drivesList.map(d => {
          const name = (d.name || d.Name || 'C:').toUpperCase();
          const active = set.has(name);
          return `<button onclick="toggleScanDrive('${target}', '${safeEnc(name)}')" ${locked ? 'disabled' : ''} class="px-2.5 py-1 rounded-lg font-mono text-[11px] font-semibold border transition-all disabled:opacity-50 disabled:pointer-events-none ${active ? 'bg-blue-600/25 border-blue-500/60 text-blue-200' : 'bg-slate-900 border-white/[0.08] text-slate-400 hover:text-white'}">${active ? '✓ ' : ''}${escapeHtml(name)} <span class="opacity-70 font-normal">(${(d.freeGB ?? d.FreeGB ?? 0).toFixed(0)} Go libres)</span></button>`;
        }).join('');
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
        btn.innerText = target === 'audit'
          ? `🚀 Lancer l'Audit (${customArr.length} dossier${customArr.length > 1 ? 's' : ''})`
          : `🔍 Scanner (${customArr.length} dossier${customArr.length > 1 ? 's' : ''})`;
      } else {
        const allSelected = drivesList.length > 0 && set.size === drivesList.length;
        const listStr = Array.from(set).sort().join(', ');
        btn.innerText = target === 'audit'
          ? (allSelected ? '🚀 Lancer l’Audit (tous les disques)' : `🚀 Lancer l’Audit (${listStr})`)
          : (allSelected ? '🔍 Scanner tous les disques' : `🔍 Scanner sur ${listStr}`);
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
          <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-slate-800 border border-white/[0.1] text-slate-200 font-mono text-[10px]">
            <span>${escapeHtml(p)}</span>
            <button onclick="removeCustomFolder('${target}', ${idx})" class="text-rose-400 hover:text-rose-300 font-bold">✕</button>
          </span>
        `).join('');
      }
      renderScanDrivePills(target);
    }

    function startDuplicatesScan() {
      if (duplicateCustomFolders.length === 0 && dupScanDrives.size === 0) {
        showToast('Sélectionnez au moins un disque ou un dossier.', 'warn');
        return;
      }
      const minMB = parseInt(document.getElementById('dup-min-mb')?.value || '50', 10);
      const drives = Array.from(dupScanDrives);
      selectedDuplicateIds.clear();
      updateSelectionCalc();
      dupInProgress = true;
      renderScanDrivePills('duplicates');
      const btn = document.getElementById('btn-scan-duplicates');
      const pickBtn = document.getElementById('btn-dup-pick-folders');
      if (btn) { btn.disabled = true; btn.innerText = 'Scan en cours...'; }
      if (pickBtn) pickBtn.disabled = true;
      showDupProgress(duplicateCustomFolders.length > 0 ? 'Étape 1/3 : Parcours des dossiers ciblés...' : `Étape 1/3 : Parcours sur ${drives.join(', ')}...`, 5);
      postToBridge({ action: 'scanDuplicates', drives, minMB, paths: duplicateCustomFolders });
    }

    function showDupProgress(text, pct) {
      const wrap = document.getElementById('dup-progress-wrap');
      if (!wrap) return;
      wrap.classList.remove('hidden');
      document.getElementById('dup-progress-text').innerText = text;
      document.getElementById('dup-progress-pct').innerText = `${Math.round(pct)}%`;
      document.getElementById('dup-progress-bar').style.width = `${Math.min(100, Math.max(0, pct))}%`;
    }

    function hideDupProgress() {
      dupInProgress = false;
      const wrap = document.getElementById('dup-progress-wrap');
      if (wrap) wrap.classList.add('hidden');
      const pickBtn = document.getElementById('btn-dup-pick-folders');
      if (pickBtn) pickBtn.disabled = false;
      renderScanDrivePills('duplicates');
    }

    function renderDuplicatesView() {
      const container = document.getElementById('duplicates-container');
      const toolbar = document.getElementById('dup-toolbar');
      const badge = document.getElementById('nav-count-duplicates');
      if (badge) badge.innerText = duplicateGroups.length;

      if (!duplicateGroups || duplicateGroups.length === 0) {
        if (toolbar) toolbar.classList.add('hidden');
        container.innerHTML = `<div class="surface-card rounded-2xl p-10 text-center text-xs text-slate-400">Aucun doublon volumineux trouvé sur les disques ou dossiers sélectionnés.</div>`;
        updateSelectionCalc();
        return;
      }

      if (toolbar) toolbar.classList.remove('hidden');
      document.getElementById('dup-groups-count').innerText = duplicateGroups.length;
      const totalReclaim = duplicateGroups.reduce((acc, g) => acc + (g.reclaimableGB ?? g.ReclaimableGB ?? 0), 0);
      document.getElementById('dup-reclaim-total').innerText = `${totalReclaim.toFixed(2)} Go`;

      container.innerHTML = duplicateGroups.map(g => {
        const files = g.files || g.Files || [];
        const szText = (g.sizeGB ?? g.SizeGB ?? 0) >= 0.1 ? `${(g.sizeGB ?? g.SizeGB ?? 0).toFixed(2)} Go` : `${(g.sizeMB ?? g.SizeMB ?? 0).toFixed(1)} Mo`;
        const recText = (g.reclaimableGB ?? g.ReclaimableGB ?? 0) >= 0.1 ? `${(g.reclaimableGB ?? g.ReclaimableGB ?? 0).toFixed(2)} Go` : `${(g.reclaimableMB ?? g.ReclaimableMB ?? 0).toFixed(1)} Mo`;
        const firstName = files[0]?.name || files[0]?.Name || 'Fichier';

        const rowsHtml = files.map((f, idx) => {
          const fid = f.id || f.Id;
          const fpath = f.path || f.Path;
          const isSanct = f.isSanctuarized ?? f.IsSanctuarized ?? false;
          const isProt = f.isProtected ?? f.IsProtected ?? false;
          const locked = isSanct || isProt;
          const isDelete = selectedDuplicateIds.has(fid);
          return `
            <div class="item-row rounded-xl px-3 py-2.5 flex items-center justify-between gap-3 text-xs ${isDelete ? 'selected-delete' : ''}">
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="font-semibold text-white truncate">${escapeHtml(f.name || f.Name)}</span>
                  ${idx === 0 ? `<span class="text-[10px] px-1.5 py-0.5 rounded bg-emerald-500/15 text-emerald-300 border border-emerald-500/30">Original suggéré à garder</span>` : ''}
                  ${locked ? `<span class="text-[10px] px-1.5 py-0.5 rounded bg-amber-500/15 text-amber-300 border border-amber-500/30">🛡️ Protégé</span>` : ''}
                  <span class="text-[10px] text-slate-500 font-mono">${escapeHtml(f.lastModified || f.LastModified || '')}</span>
                </div>
                <div class="text-[11px] text-slate-400 font-mono truncate">${escapeHtml(fpath)}</div>
              </div>
              <div class="flex items-center gap-1.5 shrink-0">
                <button onclick="openFolder('${safeEnc(fpath)}')" title="Ouvrir l'emplacement" class="px-2 py-1 rounded-lg bg-slate-800 hover:bg-slate-700 border border-white/[0.08] text-[11px] text-slate-300">📂</button>
                ${locked ? '' : `
                  <button onclick="setDupAction('${escapeHtml(fid)}', 'keep')" class="px-2.5 py-1 rounded-lg text-[11px] font-medium border transition-all ${!isDelete ? 'bg-slate-700 text-white border-white/20' : 'bg-slate-900 text-slate-400 border-white/[0.08] hover:text-white'}">Garder</button>
                  <button onclick="setDupAction('${escapeHtml(fid)}', 'delete')" class="px-2.5 py-1 rounded-lg text-[11px] font-medium border transition-all ${isDelete ? 'bg-rose-600 text-white border-rose-500' : 'bg-slate-900 text-rose-300 border-rose-500/30 hover:bg-rose-950/60'}">Supprimer</button>
                `}
              </div>
            </div>
          `;
        }).join('');

        return `
          <div class="surface-card rounded-2xl p-4 space-y-2.5">
            <div class="flex flex-wrap items-center justify-between gap-2 border-b border-white/[0.07] pb-2">
              <div class="flex items-center gap-2">
                <span class="text-xs font-bold text-white">${escapeHtml(firstName)}</span>
                <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-300">${files.length} copies × ${szText}</span>
                <span class="text-[10px] font-mono text-slate-500">SHA256: ${escapeHtml(g.hash || g.Hash || '')}</span>
              </div>
              <span class="text-xs font-mono font-bold text-emerald-400">+${recText} récupérables</span>
            </div>
            <div class="space-y-1.5">${rowsHtml}</div>
          </div>
        `;
      }).join('');
      updateSelectionCalc();
    }

    function setDupAction(fid, action) {
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
      duplicateGroups.forEach(g => {
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