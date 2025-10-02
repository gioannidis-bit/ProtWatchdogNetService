async function refresh() {
    const res = await fetch('/api/processes');
    const arr = await res.json();
    const tbody = document.querySelector('#table tbody');
    tbody.innerHTML = '';
    for (const p of arr) {
        const tr = document.createElement('tr');
        tr.innerHTML = `<td>${p.name}</td><td>${p.executablePath}</td><td>${p.parameters}</td><td>${p.restartDelaySeconds}</td><td>${p.lastStart ?? ''}</td><td>${p.restartCount}</td><td><button data-id='${p.id}' class='rm'>Remove</button></td>`;
        tbody.appendChild(tr);
    }
    document.querySelectorAll('.rm').forEach(b => b.addEventListener('click', async (e) => {
        const id = e.target.getAttribute('data-id');
        await fetch('/api/processes/remove', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: id })
        });
        refresh();
    }));
}

document.getElementById('addForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const dto = {
        name: document.getElementById('name').value,
        executablePath: document.getElementById('exe').value,
        parameters: document.getElementById('params').value,
        restartDelaySeconds: parseInt(document.getElementById('delay').value || '5')
    };
    await fetch('/api/processes/add', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(dto) });
    document.getElementById('addForm').reset();
    refresh();
});

refresh();
