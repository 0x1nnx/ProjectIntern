function initializeInternCalendar(config) {
    const { revealedDates, upcomingDates, revealedMap, minDate, maxDate } = config;

    function formatDate(d) {
        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    flatpickr("#calendar-input", {
        mode: "single",
        inline: true,
        dateFormat: "Y-m-d",
        locale: { firstDayOfWeek: 1 },
        minDate: minDate,
        maxDate: maxDate,
        clickOpens: false,
        disableMobile: true,
        onChange: function (dates) {
            if (!dates.length) return;
            const dateStr = formatDate(dates[0]);
            if (revealedDates.includes(dateStr)) {
                previewDay(dateStr, revealedMap);
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

    const formatted = new Date(dateStr + 'T00:00:00')
        .toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });

    preview.innerHTML = `
        <p class="text-muted small text-uppercase fw-semibold mb-1 mt-2">${formatted}</p>
        <h5 class="fw-bold">${data.TopicName || data.topicName || ''}</h5>
        <p class="text-muted">${data.TopicDescription || data.topicDescription || ''}</p>`;
}