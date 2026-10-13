let internCalendarState = {
    selectedDates: [],
    fp: null,
    config: null
};

function initializeInternCalendar(config) {
    const { revealedDates, upcomingDates, revealedMap, minDate, maxDate } = config;
    internCalendarState.config = config;

    // Build antiforgery token input if it is not already on the page
    if (!document.querySelector('input[name="__RequestVerificationToken"]')) {
        const formWrapper = document.createElement('form');
        formWrapper.innerHTML = config.antiforgeryTokenHtml;
        document.body.appendChild(formWrapper);
    }

    config.tokenValue = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    internCalendarState.fp = flatpickr("#calendar-input", {
        mode: "multiple",
        inline: true,
        dateFormat: "Y-m-d",
        minDate: minDate,
        maxDate: maxDate,
        disableMobile: true,
        onChange: function (dates, dateStr, fp) {
            const today = formatDate(new Date());
            const selected = dates.map(d => formatDate(d));

            // Past days cannot be changed by interns; clicking a covered day only previews its topic
            const pastClicked = selected.filter(d => d < today);
            pastClicked.forEach(d => {
                if (revealedDates.includes(d)) previewDay(d, revealedMap);
            });

            if (pastClicked.length) {
                fp.setDate(dates.filter(d => formatDate(d) >= today), false);
            }

            internCalendarState.selectedDates = selected.filter(d => d >= today);
            updateSummary();
            updateButtons();

            const { selectedDates } = internCalendarState;
            if (selectedDates.length === 1 && upcomingDates.includes(selectedDates[0])) {
                previewUpcomingDay(selectedDates[0]);
            }
        },
        onDayCreate: function (dObj, dStr, fp, dayElem) {
            const dateStr = formatDate(dayElem.dateObj);
            if (revealedDates.includes(dateStr)) {
                dayElem.style.background = '#d1e7dd';
                dayElem.style.color = '#0a3622';
                dayElem.style.borderRadius = '4px';
                dayElem.title = 'Covered — click to view topic';
            } else if (upcomingDates.includes(dateStr)) {
                dayElem.style.background = '#cfe2ff';
                dayElem.style.color = '#0a58ca';
                dayElem.style.borderRadius = '4px';
                dayElem.title = 'Upcoming work day';
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

function formatLongDate(dateStr) {
    return new Date(dateStr + 'T00:00:00')
        .toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
}

function hoursBadge(data) {
    const startTime = data?.StartTime || data?.startTime;
    const endTime = data?.EndTime || data?.endTime;
    return startTime && endTime
        ? `<p class="mb-2"><span class="badge bg-light text-dark border"><i class="fas fa-clock me-1"></i>${startTime} – ${endTime}</span></p>`
        : '';
}

function updateSummary() {
    const el = document.getElementById('summary-text');
    const { selectedDates, config } = internCalendarState;

    if (!selectedDates.length) {
        el.innerHTML = 'No dates selected';
        return;
    }

    const newDates = selectedDates.filter(d => !config.upcomingDates.includes(d));
    const existing = selectedDates.filter(d => config.upcomingDates.includes(d));

    let parts = [];
    if (newDates.length) parts.push(`<strong class="text-primary">${newDates.length} new</strong>`);
    if (existing.length) parts.push(`<strong class="text-danger">${existing.length} existing</strong>`);
    el.innerHTML = `<span class="badge bg-primary me-1">${selectedDates.length}</span> ${parts.join(' · ')} selected`;
}

function updateButtons() {
    const { selectedDates, config } = internCalendarState;
    const hasExisting = selectedDates.some(d => config.upcomingDates.includes(d));
    document.getElementById('btn-add').disabled = !selectedDates.some(d => !config.upcomingDates.includes(d));
    document.getElementById('btn-remove').disabled = !hasExisting;
    document.getElementById('btn-hours').disabled = !hasExisting;
}

function clearSelection() {
    internCalendarState.fp?.clear();
    internCalendarState.selectedDates = [];
    updateSummary();
    updateButtons();
}

function getWorkingHours() {
    const startTime = document.getElementById('start-time').value;
    const endTime = document.getElementById('end-time').value;

    if (!startTime && !endTime) return { startTime: null, endTime: null };

    if (!startTime || !endTime) {
        toast('warning', 'Please fill in both start and end time, or leave both empty.');
        return null;
    }

    if (endTime <= startTime) {
        toast('warning', 'End time must be after start time.');
        return null;
    }

    return { startTime, endTime };
}

async function addDays() {
    const { selectedDates, config } = internCalendarState;
    const newDates = selectedDates.filter(d => !config.upcomingDates.includes(d));
    if (!newDates.length) return;

    const hours = getWorkingHours();
    if (!hours) return;

    await sendRequest(config.addUrl, { dates: newDates, ...hours }, 'btn-add', 'Failed to add work days.');
}

async function updateHours() {
    const { selectedDates, config } = internCalendarState;
    const toUpdate = selectedDates.filter(d => config.upcomingDates.includes(d));
    if (!toUpdate.length) return;

    const hours = getWorkingHours();
    if (!hours) return;

    await sendRequest(config.updateHoursUrl, { dates: toUpdate, ...hours }, 'btn-hours', 'Failed to update work hours.');
}

async function removeDays() {
    const { selectedDates, config } = internCalendarState;
    const toRemove = selectedDates.filter(d => config.upcomingDates.includes(d));
    if (!toRemove.length) return;

    if (!confirm(`Remove ${toRemove.length} work day(s) from your schedule?`)) return;

    await sendRequest(config.removeUrl, { dates: toRemove }, 'btn-remove', 'Failed to remove work days.');
}

async function sendRequest(url, body, btnId, fallbackError) {
    setLoading(btnId, true);
    try {
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': internCalendarState.config.tokenValue
            },
            body: JSON.stringify(body)
        });
        const json = await res.json().catch(() => ({}));
        res.ok ? toast('success', json.message ?? 'Saved.') : toast('danger', json.message ?? fallbackError);
        if (res.ok) setTimeout(() => location.reload(), 1000);
    } catch {
        toast('danger', 'Network error. Please try again.');
    } finally {
        setLoading(btnId, false);
    }
}

function setLoading(btnId, loading) {
    const btn = document.getElementById(btnId);
    if (!btn) return;
    if (loading) {
        btn.dataset.original = btn.innerHTML;
        btn.innerHTML = 'Saving...';
        btn.disabled = true;
    } else {
        btn.innerHTML = btn.dataset.original ?? btn.innerHTML;
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

function previewUpcomingDay(dateStr) {
    const preview = document.getElementById('topic-preview');
    const data = internCalendarState.config?.upcomingMap?.[dateStr];

    preview.className = 'card-body';
    preview.innerHTML = `
        <p class="text-muted small text-uppercase fw-semibold mb-1 mt-2">${formatLongDate(dateStr)}</p>
        ${hoursBadge(data)}
        <p class="text-muted"><i class="fas fa-lock me-1"></i>Topic revealed on the day</p>`;
}

// Preview a specific selected date's curriculum item details
function previewDay(dateStr, dataMap) {
    // Fallback if dataMap isn't passed directly (e.g. called directly from HTML inline click handler)
    const map = dataMap || window.internCalendarDataMap;

    document.querySelectorAll('.day-row').forEach(r => r.classList.remove('active'));
    document.querySelector(`.day-row[data-date="${dateStr}"]`)?.classList.add('active');

    const preview = document.getElementById('topic-preview');
    const data = map ? map[dateStr] : null;

    if (!data) {
        preview.className = 'card-body text-center text-muted';
        preview.innerHTML = `<p class="mt-3">Topic not yet revealed</p>`;
        return;
    }

    preview.className = 'card-body';

    preview.innerHTML = `
        <p class="text-muted small text-uppercase fw-semibold mb-1 mt-2">${formatLongDate(dateStr)}</p>
        ${hoursBadge(data)}
        <h5 class="fw-bold">${data.TopicName || data.topicName || ''}</h5>
        <p class="text-muted">${data.TopicDescription || data.topicDescription || ''}</p>`;
}
