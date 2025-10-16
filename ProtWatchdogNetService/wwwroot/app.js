async function refresh() {
    try {
        const res = await fetch('/api/processes');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        
        const arr = await res.json();
        const tbody = document.querySelector('#table tbody');
        tbody.innerHTML = '';
        
        document.getElementById('processCount').textContent = arr.length;

        if (arr.length === 0) {
            tbody.innerHTML = '<tr><td colspan="16" style="text-align:center; color:#999;">No processes configured yet</td></tr>';
            return;
        }

        for (const p of arr) {
            const tr = document.createElement('tr');
            
            const statusText = p.isRunning ? '✅ Running' : '❌ Stopped';
            const statusClass = p.isRunning ? 'status-running' : 'status-stopped';
            const pid = p.currentPid ? p.currentPid : '-';
            const lastStart = p.lastStart ? new Date(p.lastStart).toLocaleString() : '-';
            const exitCode = p.lastExitCode !== null ? p.lastExitCode : '-';
            const params = p.parameters || '-';
            const autoRestart = p.autoRestart ? '✅ Yes' : '❌ No';
            
            // Circuit breaker status
            const circuitStatus = p.circuitBreakerTripped 
                ? '🔴 TRIPPED' 
                : (p.recentRestartCount >= p.maxRestartAttempts * 0.7 ? '🟡 Warning' : '🟢 OK');
            const circuitClass = p.circuitBreakerTripped ? 'status-stopped' : '';
            
            // Recent restarts with limit
            const recentRestarts = `${p.recentRestartCount}/${p.maxRestartAttempts} (${p.restartTimeWindowMinutes}m)`;
            
            // Health status
            const healthIcon = p.enableHealthCheck 
                ? (p.isHealthy ? '💚' : '❤️')
                : '⚪';
            const healthText = p.enableHealthCheck 
                ? (p.healthStatus || '-') 
                : 'Disabled';
            const cpuText = p.enableHealthCheck ? p.lastCpuPercent.toFixed(1) : '-';
            const memText = p.enableHealthCheck ? p.lastMemoryMB.toFixed(1) : '-';
            
            // Action buttons based on state
            let actionButtons = '';
            if (p.isRunning) {
                actionButtons = `
                    <button data-id='${p.id}' class='action-btn stop-btn'>⏹️ Stop</button>
                `;
            } else {
                actionButtons = `
                    <button data-id='${p.id}' class='action-btn start-btn'>▶️ Start</button>
                `;
            }
            actionButtons += `<button data-id='${p.id}' class='action-btn remove-btn'>🗑️ Remove</button>`;
            
            tr.innerHTML = `
                <td class="${statusClass}">${statusText}</td>
                <td><strong>${escapeHtml(p.name)}</strong></td>
                <td class="mono">${pid}</td>
                <td class="mono" style="font-size:0.8em;">${escapeHtml(p.executablePath)}</td>
                <td class="mono" style="font-size:0.8em;">${escapeHtml(params)}</td>
                <td>${p.restartDelaySeconds}s</td>
                <td>${lastStart}</td>
                <td>${p.restartCount}</td>
                <td><strong>${recentRestarts}</strong></td>
                <td class="mono">${exitCode}</td>
                <td>${autoRestart}</td>
                <td class="${circuitClass}">${circuitStatus}</td>
                <td>${healthIcon} ${healthText}</td>
                <td>${cpuText}</td>
                <td>${memText}</td>
                <td class="action-buttons">${actionButtons}</td>
            `;
            tbody.appendChild(tr);
        }

        // Attach event handlers
        attachActionHandlers();
    } catch (err) {
        console.error('Refresh failed:', err);
        document.querySelector('#table tbody').innerHTML = 
            `<tr><td colspan="16" style="text-align:center; color:red;">Error loading processes: ${err.message}</td></tr>`;
    }
}

function attachActionHandlers() {
    // Start button
    document.querySelectorAll('.start-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const id = e.target.getAttribute('data-id');
            try {
                const res = await fetch('/api/processes/start', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: id })
                });
                
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                await refresh();
            } catch (err) {
                alert('Failed to start process: ' + err.message);
            }
        });
    });

    // Stop button
    document.querySelectorAll('.stop-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const id = e.target.getAttribute('data-id');
            try {
                const res = await fetch('/api/processes/stop', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: id })
                });
                
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                await refresh();
            } catch (err) {
                alert('Failed to stop process: ' + err.message);
            }
        });
    });

    // Remove button
    document.querySelectorAll('.remove-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const id = e.target.getAttribute('data-id');
            const name = e.target.closest('tr').querySelector('strong').textContent;
            
            if (!confirm(`Remove process "${name}"? This will kill the process if running.`)) {
                return;
            }

            try {
                const res = await fetch('/api/processes/remove', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: id })
                });
                
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                await refresh();
            } catch (err) {
                alert('Failed to remove process: ' + err.message);
            }
        });
    });
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

document.getElementById('addForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    
    const dto = {
        name: document.getElementById('name').value.trim(),
        executablePath: document.getElementById('exe').value.trim(),
        parameters: document.getElementById('params').value.trim(),
        restartDelaySeconds: parseInt(document.getElementById('delay').value || '5'),
        maxRestartAttempts: parseInt(document.getElementById('maxRestarts').value || '10'),
        restartTimeWindowMinutes: parseInt(document.getElementById('timeWindow').value || '5'),
        enableHealthCheck: document.getElementById('enableHealth').checked,
        healthCheckIntervalSeconds: parseInt(document.getElementById('healthInterval').value || '30'),
        maxMemoryMB: parseFloat(document.getElementById('maxMemory').value || '0'),
        maxCpuPercent: parseFloat(document.getElementById('maxCpu').value || '0'),
        minCpuPercent: parseFloat(document.getElementById('minCpu').value || '0'),
        unhealthyThresholdSeconds: parseInt(document.getElementById('unhealthyThreshold').value || '60')
    };

    if (!dto.name || !dto.executablePath) {
        alert('Name and Executable Path are required');
        return;
    }

    try {
        const res = await fetch('/api/processes/add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        
        if (!res.ok) {
            const text = await res.text();
            throw new Error(`HTTP ${res.status}: ${text}`);
        }

        document.getElementById('addForm').reset();
        await refresh();
    } catch (err) {
        alert('Failed to add process: ' + err.message);
    }
});

// Auto-refresh every 3 seconds
setInterval(refresh, 3000);

// Initial load
refresh();