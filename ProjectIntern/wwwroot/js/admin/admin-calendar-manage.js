let adminCalendarState = {
    selectedDates: [],
    fp: null,
    config: null
};

function initializeAdminCalendar(config) {
    adminCalendarState.config = config;

    // Build automated antifraud tokens from document if missing
    if (!document.querySelector('input[name="__RequestVerificationToken"]')) {
        const formWrapper = document.createElement('form');
        formWrapper.innerHTML = config.antiforgeryTokenHtml;
        document.body.appendChild(formWrapper);
    }

    // Cache current antiforgery parameter value
    adminCalendarState.config.tokenValue = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    adminCalendarState.fp = flatpickr("#calendar-input", {
        mode: "multiple",
        inline: true,
        dateFormat: "Y-m-d",
        locale: { firstDayOfWeek: 1 },
        minDate: config.minDate,
        maxDate: config.maxDate,
        disableMobile: true,
        onChange: function (dates) {
            adminCalendarState.selectedDates = dates.map(d => formatDate(d));
            updateSummary();
            updateButtons();

            if (adminCalendarState.selectedDates.length === 1 && config.existingDates.includes(adminCalendarState.selectedDates[0])) {
                previewDay(adminCalendarState.selectedDates[0]);
            }
        },
        onDayCreate: function (dObj, dStr, fp, dayElem) {
            const dateStr = formatDate(dayElem.dateObj);
            if (config.existingDates.includes(dateStr)) {
                dayElem.style.background = '#cfe2ff';
                dayElem.style.color = '#0a58ca';
                dayElem.style.borderRadius = '4px';
            }
        }
    });
}

function formatDate(d) {
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

function updateSummary() {
    const el = document.getElementById('summary-text');
    const { selectedDates, config } = adminCalendarState;

    if (!selectedDates.length) {
        el.innerHTML = 'No dates selected';
        return;
    }

    const newDates = selectedDates.filter(d => !config.existingDates.includes(d));
    const existing = selectedDates.filter(d => config.existingDates.includes(d));

    let parts = [];
    if (newDates.length) parts.push(`<strong class="text-primary">${newDates.length} new</strong>`);
    if (existing.length) parts.push(`<strong class="text-danger">${existing.length} to remove</strong>`);
    el.innerHTML = `<span class="badge bg-primary me-1">${selectedDates.length}</span> ${parts.join(' · ')} selected`;
}

function updateButtons() {
    const { selectedDates, config } = adminCalendarState;
    document.getElementById('btn-add').disabled = !selectedDates.some(d => !config.existingDates.includes(d));
    document.getElementById('btn-remove').disabled = !selectedDates.some(d => config.existingDates.includes(d));
}

function clearSelection() {
    if (adminCalendarState.fp) {
        adminCalendarState.fp.clear();
    }
    adminCalendarState.selectedDates = [];
    updateSummary();
    updateButtons();
}

function previewDay(dateStr) {
    document.querySelectorAll('.day-row').forEach(r => r.classList.remove('active'));
    document.querySelector(`.day-row[data-date="${dateStr}"]`)?.classList.add('active');

    const preview = document.getElementById('topic-preview');
    const data = adminCalendarState.config.workDayMap[dateStr];

    if (!data) {
        preview.innerHTML = `<p class="text-muted mt-3">No data for this day</p>`;
        return;
    }

    const formatted = new Date(dateStr + 'T00:00:00').toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
    preview.innerHTML = `
        <p class="text-muted small text-uppercase fw-semibold mb-1 mt-2">${formatted}</p>
        <h5 class="fw-bold">${data.TopicName || data.topicName || ''}</h5>
        <p class="text-muted">${data.TopicDescription || data.topicDescription || ''}</p>`;
}

async function addDays() {
    const { selectedDates, config } = adminCalendarState;
    const newDates = selectedDates.filter(d => !config.existingDates.includes(d));
    if (!newDates.length) return;

    setLoading('btn-add', true);
    try {
        const res = await fetch(config.addUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': config.tokenValue
            },
            body: JSON.stringify({ internId: config.internId, dates: newDates })
        });
        const json = await res.json();
        res.ok ? toast('success', json.message ?? 'Work days added.') : toast('danger', json.message ?? 'Failed to add work days.');
        if (res.ok) setTimeout(() => location.reload(), 1000);
    } catch {
        toast('danger', 'Network error. Please try again.');
    } finally {
        setLoading('btn-add', false);
    }
}

async function removeDays() {
    const { selectedDates, config } = adminCalendarState;
    const toRemove = selectedDates.filter(d => config.existingDates.includes(d));
    if (!toRemove.length) return;

    const past = toRemove.filter(d => new Date(d) < new Date(new Date().toDateString()));
    if (past.length && !confirm(`${past.length} day(s) have already passed. Removing them will not affect curriculum progress. Continue?`)) return;

    setLoading('btn-remove', true);
    try {
        const res = await fetch(config.removeUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': config.tokenValue
            },
            body: JSON.stringify({ internId: config.internId, dates: toRemove })
        });
        const json = await res.json();
        res.ok ? toast('success', json.message ?? 'Work days removed.') : toast('danger', json.message ?? 'Failed to remove work days.');
        if (res.ok) setTimeout(() => location.reload(), 1000);
    } catch {
        toast('danger', 'Network error. Please try again.');
    } finally {
        setLoading('btn-remove', false);
    }
}

function setLoading(btnId, loading) {
    const btn = document.getElementById(btnId);
    if (!btn) return;
    btn.disabled = loading;
    btn.innerHTML = loading ? 'Saving...' : (btn.dataset.original ?? btn.innerHTML);
    if (!loading) {
        btn.dataset.original = btn.innerHTML;
        updateButtons();
    }
}

function toast(type, message) {
    const el = document.createElement('div');
    el.className = `alert alert-${type} alert-dismissible fade show shadow-sm`;
    el.innerHTML = `${message}<button type="button" class="btn-close" data-bs-dismiss="alert"></button>`;
    document.getElementById('toast-container').appendChild(el);
    setTimeout(() => el.remove(), 4000);
}