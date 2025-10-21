// Date and Time Utilities
const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const oneDay = 86400000;

function formatDateTime(dateString) {
  const date = new Date(dateString);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const dateOnly = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const diffMs = today - dateOnly;
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
  const time = date.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });

  if (diffDays === 0) return `Today, ${time}`;
  if (diffDays === 1) return `Yesterday, ${time}`;
  if (diffDays > 1 && diffDays <= 6) {
    const dayName = date.toLocaleDateString('en-GB', { weekday: 'long' });
    return `${dayName}, ${time}`;
  }
  if (date.getFullYear() === now.getFullYear()) {
    const dayName = date.toLocaleDateString('en-GB', { weekday: 'short' });
    const day = date.getDate();
    const month = months[date.getMonth()];
    return `${dayName} ${day} ${month}, ${time}`;
  }
  const day = date.getDate();
  const month = months[date.getMonth()];
  const year = date.getFullYear();
  return `${day} ${month} ${year}, ${time}`;
}

function calculateTimeElapsed(dateString) {
  const diffMs = new Date() - new Date(dateString);
  const minutes = Math.floor(diffMs / (1000 * 60)) % 60;
  const hours = Math.floor(diffMs / (1000 * 60 * 60)) % 24;
  const days = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (days === 0 && hours === 0 && minutes === 0) return '<1m';

  const timeUnits = [
    { value: days, label: 'd' },
    { value: hours, label: 'h' },
    { value: minutes, label: 'm' }
  ].filter(unit => unit.value > 0);

  return timeUnits.length ? timeUnits.slice(0, 2).map(unit => `${unit.value}${unit.label}`).join(' ') : '<1m';
}

function updateAllElapsedTimes() {
  document.querySelectorAll('.elapsed-time').forEach(element => {
    const ticketId = element.closest('.ticket-item')?.dataset?.id;
    if (ticketId) {
      const ticket = tickets.find(t => t.id === ticketId);
      if (ticket) {
        element.textContent = calculateTimeElapsed(ticket.waitingSince);
        element.parentElement.classList.toggle('overdue', workingDaysSince(ticket.waitingSince) >= 2);
      }
    }
  });
}

function initHolidays() {
  for (let i = 0; i < holidays.length; i++) {
    if (Array.isArray(holidays[i])) continue;
    const [sy, sm, sd] = holidays[i].start.split("-").map(Number);
    const [ey, em, ed] = holidays[i].end.split("-").map(Number);
    const s = new Date(sy, sm - 1, sd);
    const e = new Date(new Date(ey, em - 1, ed).getTime() + oneDay);
    holidays[i] = [s, e];
  }
}

function workingDaysSince(startString) {
  const start = new Date(startString);
  const now = new Date();
  if (now <= start) return 0;

  const firstDay = new Date(start.getFullYear(), start.getMonth(), start.getDate());
  const isWeekend = d => { const wd = d.getDay(); return wd === 0 || wd === 6; };
  const isHoliday = d => holidays.some(([s, e]) => d >= s && d < e);

  let total = 0;
  for (let day = firstDay; day < now; day = new Date(day.getTime() + oneDay)) {
    if (isWeekend(day) || isHoliday(day)) continue;
    const dayStart = start > day ? start : day;
    const dayEnd = new Date(Math.min(now, day.getTime() + oneDay));
    if (dayEnd > dayStart) total += (dayEnd - dayStart) / oneDay;
  }
  return Math.floor(total);
}