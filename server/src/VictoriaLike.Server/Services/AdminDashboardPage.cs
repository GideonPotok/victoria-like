namespace VictoriaLike.Server.Services;

public static class AdminDashboardPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Victoria II Admin</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f2efe7;
      --panel: #fffdf8;
      --ink: #1f2a2c;
      --muted: #66757b;
      --accent: #8b5e34;
      --accent-soft: #dbc7ae;
      --border: #d8cfc1;
      --good: #266150;
      --warn: #b5651d;
      --bad: #8f2d2d;
    }

    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: Georgia, "Iowan Old Style", serif;
      background:
        radial-gradient(circle at top right, rgba(139, 94, 52, 0.12), transparent 32%),
        linear-gradient(180deg, #f8f4ed 0%, var(--bg) 100%);
      color: var(--ink);
    }
    main {
      max-width: 1200px;
      margin: 0 auto;
      padding: 24px;
    }
    h1, h2 { margin: 0 0 12px; }
    p { color: var(--muted); }
    .topbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 16px;
      margin-bottom: 20px;
    }
    .topbar button {
      border: 1px solid var(--accent);
      background: var(--accent);
      color: white;
      padding: 10px 14px;
      cursor: pointer;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 12px;
      margin-bottom: 20px;
    }
    .panel {
      background: var(--panel);
      border: 1px solid var(--border);
      padding: 16px;
      box-shadow: 0 12px 30px rgba(31, 42, 44, 0.05);
    }
    .metric {
      font-size: 28px;
      color: var(--accent);
      font-weight: bold;
    }
    .label {
      color: var(--muted);
      font-size: 13px;
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }
    table {
      width: 100%;
      border-collapse: collapse;
    }
    th, td {
      text-align: left;
      padding: 10px 8px;
      border-bottom: 1px solid var(--border);
      font-size: 14px;
    }
    .status-Healthy { color: var(--good); }
    .status-Degraded { color: var(--warn); }
    .status-Unhealthy { color: var(--bad); }
    code {
      font-family: "SFMono-Regular", ui-monospace, monospace;
      font-size: 12px;
      background: #f4ede1;
      padding: 2px 4px;
    }
    .stack {
      display: grid;
      gap: 12px;
    }
    .filters {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 8px;
      margin-bottom: 12px;
    }
    input, select {
      width: 100%;
      border: 1px solid var(--border);
      background: #fffaf1;
      color: var(--ink);
      padding: 8px;
      font: inherit;
    }
    .filters button {
      border: 1px solid var(--accent);
      background: var(--accent);
      color: white;
      cursor: pointer;
      padding: 8px;
    }
    .split {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 12px;
    }
    .pill {
      display: inline-block;
      padding: 2px 7px;
      border-radius: 999px;
      background: #efe3d1;
      color: var(--accent);
      font-size: 12px;
      margin-right: 4px;
    }
    .rejected {
      color: var(--bad);
      font-weight: bold;
    }
    @media (max-width: 720px) {
      .topbar { flex-direction: column; align-items: flex-start; }
      table { display: block; overflow-x: auto; }
    }
  </style>
</head>
<body>
  <main>
    <div class="topbar">
      <div>
        <h1>World Admin Inspector</h1>
        <p>Tick, health, connections, commands, and snapshots in one place.</p>
      </div>
      <button id="snapshot-button" type="button">Capture Savepoint</button>
    </div>

    <section class="grid" id="metrics"></section>

    <section class="stack">
      <div class="panel">
        <h2>Health</h2>
        <table>
          <thead>
            <tr><th>Check</th><th>Status</th><th>Description</th></tr>
          </thead>
          <tbody id="health-body"></tbody>
        </table>
      </div>

      <div class="panel">
        <h2>Invariant Checks</h2>
        <table>
          <thead>
            <tr><th>Code</th><th>Message</th></tr>
          </thead>
          <tbody id="invariants-body"></tbody>
        </table>
      </div>

      <div class="panel">
        <h2>Connections</h2>
        <table>
          <thead>
            <tr><th>Actor</th><th>Connected At (UTC)</th></tr>
          </thead>
          <tbody id="connections-body"></tbody>
        </table>
      </div>

      <div class="panel">
        <h2>Command Budgets</h2>
        <table>
          <thead>
            <tr><th>Actor</th><th>Country</th><th>Window</th><th>Remaining</th><th>Cooldowns</th></tr>
          </thead>
          <tbody id="budgets-body"></tbody>
        </table>
      </div>

      <div class="panel">
        <h2>Recent Commands</h2>
        <table>
          <thead>
            <tr><th>Command</th><th>Actor</th><th>Type</th><th>Status</th><th>Reason</th><th>Applied Tick</th></tr>
          </thead>
          <tbody id="commands-body"></tbody>
        </table>
      </div>

      <div class="panel">
        <h2>Command Log Viewer</h2>
        <div class="filters">
          <input id="filter-actor" placeholder="actor/account id" />
          <input id="filter-country" placeholder="country id" />
          <select id="filter-type">
            <option value="">any command type</option>
            <option value="ChangeTaxRate">ChangeTaxRate</option>
            <option value="QueueBuilding">QueueBuilding</option>
          </select>
          <select id="filter-result">
            <option value="">any result</option>
            <option value="accepted">accepted/queued</option>
            <option value="applied">applied</option>
            <option value="rejected">rejected</option>
            <option value="failed">failed</option>
          </select>
          <input id="filter-from" type="number" min="0" placeholder="from tick" />
          <input id="filter-to" type="number" min="0" placeholder="to tick" />
          <button id="command-filter-button" type="button">Search</button>
        </div>
        <table>
          <thead>
            <tr><th>Tick</th><th>Command</th><th>Actor</th><th>Country</th><th>Type</th><th>Result</th><th>Reason</th></tr>
          </thead>
          <tbody id="audit-body"></tbody>
        </table>
      </div>

      <div class="panel">
        <h2>Market Explanation</h2>
        <table>
          <thead>
            <tr><th>Good</th><th>Price</th><th>Pressure</th><th>Supply</th><th>Demand</th><th>Unmet</th><th>Clamp</th><th>Largest Producer</th><th>Largest Consumer</th></tr>
          </thead>
          <tbody id="market-explain-body"></tbody>
        </table>
      </div>

      <div class="split">
        <div class="panel">
          <h2>Province Inspector</h2>
          <select id="province-select"></select>
          <div id="province-detail"></div>
        </div>
        <div class="panel">
          <h2>Country Inspector</h2>
          <select id="country-select"></select>
          <div id="country-detail"></div>
        </div>
      </div>

      <div class="panel">
        <h2>Snapshots</h2>
        <table>
          <thead>
            <tr><th>File</th><th>Name</th><th>Tick</th><th>World Date</th><th>Captured At (UTC)</th></tr>
          </thead>
          <tbody id="snapshots-body"></tbody>
        </table>
      </div>
    </section>
  </main>

  <script>
    const metricsEl = document.getElementById('metrics');
    const healthBody = document.getElementById('health-body');
    const invariantsBody = document.getElementById('invariants-body');
    const connectionsBody = document.getElementById('connections-body');
    const budgetsBody = document.getElementById('budgets-body');
    const commandsBody = document.getElementById('commands-body');
    const auditBody = document.getElementById('audit-body');
    const marketExplainBody = document.getElementById('market-explain-body');
    const provinceSelect = document.getElementById('province-select');
    const countrySelect = document.getElementById('country-select');
    const provinceDetail = document.getElementById('province-detail');
    const countryDetail = document.getElementById('country-detail');
    const snapshotsBody = document.getElementById('snapshots-body');
    const snapshotButton = document.getElementById('snapshot-button');
    const commandFilterButton = document.getElementById('command-filter-button');

    function metricCard(label, value) {
      return `<div class="panel"><div class="label">${label}</div><div class="metric">${value}</div></div>`;
    }

    function fmt(value) {
      if (value === null || value === undefined || value === '') return '';
      if (typeof value === 'number') return Number.isInteger(value) ? value : value.toFixed(2);
      return value;
    }

    function dictRows(dict) {
      return Object.entries(dict ?? {})
        .map(([key, value]) => `<span class="pill">${key}: ${fmt(value)}</span>`)
        .join(' ');
    }

    function pct(value) {
      if (value === null || value === undefined) return '';
      return `${(Number(value) * 100).toFixed(1)}%`;
    }

    async function refresh() {
      const response = await fetch('/api/admin/summary', { cache: 'no-store' });
      const data = await response.json();

      metricsEl.innerHTML = [
        metricCard('Tick', data.tick),
        metricCard('World Date', data.world_date),
        metricCard('Last Tick (ms)', data.last_tick_duration_ms),
        metricCard('Avg Tick (ms)', data.average_tick_duration_ms.toFixed(2)),
        metricCard('Connected Clients', data.connected_clients),
        metricCard('Active Sessions', data.active_sessions),
        metricCard('Active Subscriptions', data.active_subscriptions),
        metricCard('Pending Commands', data.pending_commands),
        metricCard('DB Writes / Tick', data.last_tick_db_writes),
        metricCard('Total DB Writes', data.total_db_writes),
        metricCard('Health', `<span class="status-${data.server_health}">${data.server_health}</span>`)
      ].join('');

      healthBody.innerHTML = data.health_checks.map(check => `
        <tr>
          <td>${check.name}</td>
          <td class="status-${check.status}">${check.status}</td>
          <td>${check.description ?? ''}</td>
        </tr>`).join('') || '<tr><td colspan="3">No health data</td></tr>';

      invariantsBody.innerHTML = data.invariant_violations.map(violation => `
        <tr>
          <td><code>${violation.code}</code></td>
          <td>${violation.message}</td>
        </tr>`).join('') || '<tr><td colspan="2">No invariant violations</td></tr>';

      connectionsBody.innerHTML = data.connections.map(connection => `
        <tr>
          <td>${connection.actor_id ?? '<em>anonymous</em>'}</td>
          <td>${connection.connected_at_utc}<br><code>${(connection.subscriptions ?? []).join(', ')}</code></td>
        </tr>`).join('') || '<tr><td colspan="2">No active WebSocket clients</td></tr>';

      budgetsBody.innerHTML = data.command_budgets.map(budget => {
        const cooldowns = Object.entries(budget.cooldowns_remaining_ticks ?? {})
          .map(([type, ticks]) => `${type}: ${ticks}t`)
          .join(', ');
        return `
          <tr>
            <td><code>${budget.actor_id}</code></td>
            <td><code>${budget.country_id ?? ''}</code></td>
            <td>${budget.used_in_window}/${budget.hard_limit} in ${budget.window_seconds}s</td>
            <td>${budget.remaining_in_window}</td>
            <td>${cooldowns}</td>
          </tr>`;
      }).join('') || '<tr><td colspan="5">No tracked command budgets</td></tr>';

      commandsBody.innerHTML = data.recent_commands.map(command => `
        <tr>
          <td><code>${command.command_id}</code></td>
          <td><code>${command.actor_id}</code></td>
          <td>${command.command_type}</td>
          <td>${command.outcome_status ?? command.status}</td>
          <td>${command.outcome_reason ?? ''}</td>
          <td>${command.applied_tick ?? ''}</td>
        </tr>`).join('') || '<tr><td colspan="6">No commands yet</td></tr>';

      snapshotsBody.innerHTML = data.recent_snapshots.map(snapshot => `
        <tr>
          <td><code>${snapshot.file_name}</code></td>
          <td>${snapshot.savepoint_name ?? ''}</td>
          <td>${snapshot.tick}</td>
          <td>${snapshot.world_date}</td>
          <td>${snapshot.captured_at_utc}</td>
        </tr>`).join('') || '<tr><td colspan="5">No snapshots yet</td></tr>';
    }

    async function loadCommandLog() {
      const params = new URLSearchParams();
      const actor = document.getElementById('filter-actor').value.trim();
      const country = document.getElementById('filter-country').value.trim();
      const type = document.getElementById('filter-type').value;
      const result = document.getElementById('filter-result').value;
      const fromTick = document.getElementById('filter-from').value;
      const toTick = document.getElementById('filter-to').value;
      if (actor) params.set('actorId', actor);
      if (country) params.set('countryId', country);
      if (type) params.set('commandType', type);
      if (result) params.set('outcome', result);
      if (fromTick) params.set('fromTick', fromTick);
      if (toTick) params.set('toTick', toTick);
      params.set('limit', '100');

      const response = await fetch(`/api/admin/commands?${params.toString()}`, { cache: 'no-store' });
      const data = await response.json();
      auditBody.innerHTML = data.records.map(record => {
        const resultClass = record.outcome === 'rejected' || record.outcome === 'failed' ? 'rejected' : '';
        return `
          <tr>
            <td>${record.submitted_tick}</td>
            <td><code>${record.command_id}</code></td>
            <td><code>${record.actor_id}</code></td>
            <td><code>${record.country_id ?? ''}</code></td>
            <td>${record.command_type}</td>
            <td class="${resultClass}">${record.outcome}</td>
            <td>${record.rejection_reason_code ?? record.outcome_reason ?? ''}</td>
          </tr>`;
      }).join('') || '<tr><td colspan="7">No matching commands</td></tr>';
    }

    async function loadMarketExplanation() {
      const response = await fetch('/api/admin/market', { cache: 'no-store' });
      const data = await response.json();
      marketExplainBody.innerHTML = data.goods.map(good => `
        <tr>
          <td>${good.name}<br><code>${good.id}</code></td>
          <td>${fmt(good.previous_price)} -> ${fmt(good.price)}<br>delta ${fmt(good.price_delta)}</td>
          <td>${fmt(good.target_pressure)}</td>
          <td>${fmt(good.supply)}</td>
          <td>${fmt(good.demand)}</td>
          <td>${fmt(good.unmet_demand)}</td>
          <td>${good.clamp_applied ? 'yes' : 'no'}</td>
          <td>${good.largest_producer ?? ''}</td>
          <td>${good.largest_consumer ?? ''}</td>
        </tr>`).join('') || '<tr><td colspan="9">No market data yet</td></tr>';
    }

    async function loadInspectorOptions() {
      const [countries, provinces] = await Promise.all([
        fetch('/api/world/countries', { cache: 'no-store' }).then(r => r.json()),
        fetch('/api/world/provinces', { cache: 'no-store' }).then(r => r.json())
      ]);

      countrySelect.innerHTML = countries.map(country =>
        `<option value="${country.id}">${country.tag} - ${country.name}</option>`).join('');
      provinceSelect.innerHTML = provinces.map(province =>
        `<option value="${province.id}">${province.name} (${province.owner_name})</option>`).join('');

      if (countrySelect.value) await loadCountryInspector(countrySelect.value);
      if (provinceSelect.value) await loadProvinceInspector(provinceSelect.value);
    }

    async function loadProvinceInspector(provinceId) {
      const detail = await fetch(`/api/admin/provinces/${provinceId}`, { cache: 'no-store' }).then(r => r.json());
      const popRows = (detail.pop_groups ?? []).map(pop => `
        <tr>
          <td>${pop.pop_type}<br><code>${pop.strata}</code></td>
          <td>${fmt(pop.size)}<br>${pct(pop.population_share)}</td>
          <td>${pop.culture}<br>${pop.religion}</td>
          <td>${pct(pop.literacy)}</td>
          <td>${fmt(pop.militancy)} / ${fmt(pop.consciousness)}</td>
          <td>${fmt(pop.employed_count)} / ${fmt(pop.unemployed_count)}</td>
          <td>${pct(pop.life_needs_fulfillment)} / ${pct(pop.everyday_needs_fulfillment)} / ${pct(pop.luxury_needs_fulfillment)}</td>
        </tr>`).join('') || '<tr><td colspan="7">No POP groups</td></tr>';
      provinceDetail.innerHTML = `
        <p><strong>${detail.name}</strong> owned by ${detail.owner_name}</p>
        <p>Population/workforce: ${detail.population}/${detail.workforce}</p>
        <p>RGO: ${detail.rgo_type || 'unknown'}</p>
        <p>Needs fulfillment: ${fmt(detail.needs_fulfillment)}</p>
        <p>Production: ${dictRows(detail.outputs_per_tick)}</p>
        <p>Local demand: ${dictRows(detail.local_demand)}</p>
        <p>Construction: ${(detail.construction ?? []).map(item => `${item.building_type} (${item.ticks_remaining}t)`).join(', ') || 'none'}</p>
        <table>
          <thead>
            <tr><th>POP</th><th>Size</th><th>Culture</th><th>Literacy</th><th>Mil/Con</th><th>Emp/Unemp</th><th>Needs L/E/X</th></tr>
          </thead>
          <tbody>${popRows}</tbody>
        </table>`;
    }

    async function loadCountryInspector(countryId) {
      const detail = await fetch(`/api/admin/countries/${countryId}`, { cache: 'no-store' }).then(r => r.json());
      countryDetail.innerHTML = `
        <p><strong>${detail.tag} - ${detail.name}</strong></p>
        <p>Treasury: ${fmt(detail.treasury)} | Tax: ${detail.tax_rate}%</p>
        <p>Controller: ${detail.controlled_username ?? 'none'} <code>${detail.controlled_account_id ?? ''}</code></p>
        <p>Provinces/population: ${detail.province_count}/${detail.population}</p>
        <p>Active commands: ${(detail.active_commands ?? []).map(command => `<code>${command.command_type}@${command.submitted_tick}</code>`).join(' ') || 'none'}</p>
        <p>Market summary: ${(detail.market_summary ?? []).slice(0, 5).map(good => `${good.id} ${fmt(good.price)}`).join(', ')}</p>`;
    }

    snapshotButton.addEventListener('click', async () => {
      snapshotButton.disabled = true;
      try {
        const name = prompt('Savepoint name', `manual-${Date.now()}`);
        await fetch('/api/admin/snapshots', {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ name })
        });
        await refresh();
      } finally {
        snapshotButton.disabled = false;
      }
    });

    commandFilterButton.addEventListener('click', loadCommandLog);
    provinceSelect.addEventListener('change', () => loadProvinceInspector(provinceSelect.value));
    countrySelect.addEventListener('change', () => {
      document.getElementById('filter-country').value = countrySelect.value;
      loadCountryInspector(countrySelect.value);
      loadCommandLog();
    });

    refresh();
    loadCommandLog();
    loadMarketExplanation();
    loadInspectorOptions();
    setInterval(refresh, 2000);
    setInterval(loadMarketExplanation, 5000);
  </script>
</body>
</html>
""";
}
