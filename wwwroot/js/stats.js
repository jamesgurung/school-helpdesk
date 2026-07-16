const elements = {
  latestWeekRange: document.getElementById('latest-week-range'),
  latestWeekCount: document.getElementById('latest-week-count'),
  latestWeekResponse: document.getElementById('latest-week-response'),
  weeklyChart: document.getElementById('weekly-stats-chart'),
  parentsTableBody: document.getElementById('parents-table-body'),
  assigneesTableBody: document.getElementById('assignees-table-body'),
  sortButtons: document.querySelectorAll('.stats-sort-btn'),
  logoutBtn: document.getElementById('logout-button')
};

function parseDateOnly(value) {
  if (!value) return null;
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function formatWeek(value) {
  const date = parseDateOnly(value);
  if (!date || Number.isNaN(date.valueOf())) return '-';
  return date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

function joinDurationParts(parts) {
  if (parts.length <= 1) return parts[0] || '';
  if (parts.length === 2) return `${parts[0]} and ${parts[1]}`;
  return `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
}

function formatWorkingDuration(seconds) {
  if (seconds == null || Number.isNaN(seconds)) return 'Manager responded';
  const wholeSeconds = Math.max(0, Math.round(seconds));
  const totalMinutes = Math.floor(wholeSeconds / 60);
  if (totalMinutes <= 0) return '<1 min';

  const minutesPerDay = 8 * 60;
  const days = Math.floor(totalMinutes / minutesPerDay);
  const hours = Math.floor((totalMinutes % minutesPerDay) / 60);
  const minutes = totalMinutes % 60;
  const parts = [];

  if (days > 0) parts.push(`${days} ${days === 1 ? 'day' : 'days'}`);
  if (hours > 0) parts.push(`${hours} ${hours === 1 ? 'hour' : 'hours'}`);
  if (minutes > 0) parts.push(`${minutes} ${minutes === 1 ? 'min' : 'mins'}`);

  return joinDurationParts(parts);
}

function formatNumber(value) {
  if (value == null || Number.isNaN(value)) return '-';
  return new Intl.NumberFormat('en-GB').format(value);
}

function updateLatestWeekHeadline() {
  const latestWeek = [...weeks].reverse().find(week => !week.isCurrentWeek && !week.isHolidayWeek);
  if (!latestWeek) {
    elements.latestWeekRange.textContent = 'No data';
    elements.latestWeekCount.textContent = '0';
    elements.latestWeekResponse.textContent = 'No data';
    return;
  }

  elements.latestWeekRange.textContent = formatWeek(latestWeek.weekStart);
  elements.latestWeekCount.textContent = formatNumber(latestWeek.ticketsClosed);
  elements.latestWeekResponse.textContent = formatWorkingDuration(latestWeek.averageTimeToFirstResponse);
}

function renderWeeklyChart() {
  if (!elements.weeklyChart || !Array.isArray(weeks) || weeks.length === 0) return;

  const monthsBack = window.innerWidth < 600 ? 3 : 6;
  const cutoffDate = new Date();
  cutoffDate.setMonth(cutoffDate.getMonth() - monthsBack);
  const chartWeeks = weeks.filter(row => {
    const week = parseDateOnly(row.weekStart);
    return week && week >= cutoffDate && !row.isHolidayWeek;
  });
  if (chartWeeks.length === 0) return;

  const labels = chartWeeks.map(row => formatWeek(row.weekStart));
  const ticketCounts = chartWeeks.map(row => row.ticketsClosed ?? 0);
  const responseMinutes = chartWeeks.map(row => row.averageTimeToFirstResponse == null ? null : +(row.averageTimeToFirstResponse / 60).toFixed(1));

  const barFill = getComputedStyle(document.documentElement).getPropertyValue('--primary').trim() || '#5e5ce6';
  const barFillRegular = `${barFill}CC`;
  const barFillCurrentWeek = `${barFill}66`;
  const lineColor = getComputedStyle(document.documentElement).getPropertyValue('--secondary-dark').trim() || '#08807a';

  new Chart(elements.weeklyChart, {
    type: 'bar',
    data: {
      labels,
      datasets: [
        {
          label: 'Tickets closed',
          type: 'bar',
          data: ticketCounts,
          backgroundColor: chartWeeks.map(row => row.isCurrentWeek ? barFillCurrentWeek : barFillRegular),
          borderColor: chartWeeks.map(row => row.isCurrentWeek ? barFillCurrentWeek : barFill),
          borderWidth: 1,
          borderRadius: 6,
          yAxisID: 'yTickets',
          order: 2
        },
        {
          label: 'Median time to first response (minutes)',
          type: 'line',
          data: responseMinutes,
          borderColor: lineColor,
          backgroundColor: lineColor,
          pointRadius: 4,
          pointHoverRadius: 5,
          pointBorderWidth: 2,
          pointBackgroundColor: '#fff',
          pointBorderColor: lineColor,
          tension: 0.3,
          yAxisID: 'yResponse',
          order: 1,
          spanGaps: true
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        mode: 'index',
        intersect: false
      },
      plugins: {
        legend: {
          position: 'bottom',
          labels: {
            usePointStyle: true,
            boxHeight: 8,
            boxWidth: 8
          }
        },
        tooltip: {
          callbacks: {
            label(context) {
              if (context.datasetIndex === 1) {
                const seconds = chartWeeks[context.dataIndex]?.averageTimeToFirstResponse;
                return ` Median time to first response: ${formatWorkingDuration(seconds)}`;
              }
              return ` Tickets closed: ${formatNumber(context.parsed.y)}`;
            }
          }
        }
      },
      scales: {
        x: {
          grid: {
            color: 'rgba(127, 127, 127, 0.12)'
          },
          ticks: {
            maxRotation: 0,
            autoSkip: true,
            maxTicksLimit: 12
          }
        },
        yTickets: {
          type: 'linear',
          position: 'left',
          beginAtZero: true,
          title: {
            display: true,
            text: 'Tickets closed'
          },
          ticks: {
            precision: 0
          },
          grid: {
            color: 'rgba(127, 127, 127, 0.12)'
          }
        },
        yResponse: {
          type: 'linear',
          position: 'right',
          beginAtZero: true,
          title: {
            display: true,
            text: 'Time to first response'
          },
          ticks: {
            callback(value) {
              return `${formatNumber(Number(value))}m`;
            }
          },
          grid: {
            drawOnChartArea: false
          }
        }
      }
    }
  });
}

const sortState = {
  parents: { key: 'count', direction: 'desc', type: 'number' },
  assignees: { key: 'count', direction: 'desc', type: 'number' }
};

const tableConfig = {
  parents: {
    body: elements.parentsTableBody,
    rows: Array.isArray(parents) ? parents : [],
    cells: row => [row.parentName || '-', row.studentName || '-', formatNumber(row.count)],
    pillColumn: 2
  },
  assignees: {
    body: elements.assigneesTableBody,
    rows: Array.isArray(assignees) ? assignees : [],
    cells: row => [
      row.assigneeName || '-',
      row.averageResponseTime != null && row.averageResponseTime < 1800 ? '< 30 mins' : formatWorkingDuration(row.averageResponseTime),
      formatNumber(row.count)
    ],
    pillColumn: 2
  }
};

function compareValues(a, b, type) {
  if (type === 'number') {
    const left = a == null ? Number.NEGATIVE_INFINITY : Number(a);
    const right = b == null ? Number.NEGATIVE_INFINITY : Number(b);
    return left - right;
  }
  return String(a || '').localeCompare(String(b || ''), undefined, { sensitivity: 'base' });
}

function getSortedRows(tableName) {
  const config = tableConfig[tableName];
  const state = sortState[tableName];
  const direction = state.direction === 'asc' ? 1 : -1;

  return [...config.rows].sort((left, right) => {
    const leftValue = left[state.key];
    const rightValue = right[state.key];
    const leftMissing = leftValue == null;
    const rightMissing = rightValue == null;
    if (leftMissing && !rightMissing) return 1;
    if (!leftMissing && rightMissing) return -1;
    const result = compareValues(leftValue, rightValue, state.type);
    return result * direction;
  });
}

function renderTable(tableName) {
  const config = tableConfig[tableName];
  const tbody = config.body;
  if (!tbody) return;

  const sortedRows = getSortedRows(tableName);
  tbody.replaceChildren();
  if (sortedRows.length === 0) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 3;
    cell.className = 'stats-empty-row';
    cell.textContent = 'No data available.';
    row.appendChild(cell);
    tbody.appendChild(row);
    return;
  }

  const fragment = document.createDocumentFragment();
  sortedRows.forEach(item => {
    const row = document.createElement('tr');
    config.cells(item).forEach((value, index) => {
      const cell = document.createElement('td');
      if (index === config.pillColumn) {
        const pill = document.createElement('span');
        pill.className = 'stats-pill';
        pill.textContent = value;
        cell.appendChild(pill);
      } else {
        cell.textContent = value;
      }
      row.appendChild(cell);
    });
    fragment.appendChild(row);
  });
  tbody.appendChild(fragment);
}

function updateSortButtonLabels() {
  elements.sortButtons.forEach(button => {
    const tableName = button.dataset.table;
    const key = button.dataset.key;
    const state = sortState[tableName];
    if (!state) return;

    const isActive = state.key === key;
    const marker = isActive ? (state.direction === 'asc' ? '\u25B2' : '\u25BC') : '';
    button.classList.toggle('active', isActive);

    const baseText = button.dataset.baseText || button.textContent.trim();
    button.dataset.baseText = baseText;
    button.textContent = marker ? `${baseText} ${marker}` : baseText;
  });
}

function setupTableSorting() {
  elements.sortButtons.forEach(button => {
    button.addEventListener('click', () => {
      const tableName = button.dataset.table;
      const key = button.dataset.key;
      const type = button.dataset.type || 'text';
      const defaultDirection = button.dataset.defaultDir || 'asc';
      const state = sortState[tableName];
      if (!state) return;

      if (state.key === key) {
        state.direction = state.direction === 'asc' ? 'desc' : 'asc';
      } else {
        state.key = key;
        state.type = type;
        state.direction = defaultDirection;
      }

      renderTable(tableName);
      updateSortButtonLabels();
    });
  });
}

function initStatsPage() {
  elements.logoutBtn.addEventListener('click', () => window.location.href = '/logout');
  updateLatestWeekHeadline();
  renderWeeklyChart();
  renderTable('parents');
  renderTable('assignees');
  updateSortButtonLabels();
  setupTableSorting();
}

document.addEventListener('DOMContentLoaded', initStatsPage);
