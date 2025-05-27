// Date and Time Utilities
function formatDateTime(dateString) {
  return new Date(dateString).toLocaleDateString('en-GB', { 
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit'
  });
}

function calculateTimeElapsed(dateString) {
  const diffMs = new Date() - new Date(dateString);
  const seconds = Math.floor(diffMs / 1000) % 60;
  const minutes = Math.floor(diffMs / (1000 * 60)) % 60;
  const hours = Math.floor(diffMs / (1000 * 60 * 60)) % 24;
  const days = Math.floor(diffMs / (1000 * 60 * 60 * 24));
  
  if (days === 0 && hours === 0 && minutes === 0) return `${seconds}s`;
  
  const timeUnits = [
    { value: days, label: 'd' },
    { value: hours, label: 'h' },
    { value: minutes, label: 'm' },
    { value: seconds, label: 's' }
  ].filter(unit => unit.value > 0);
  
  return timeUnits.length ? timeUnits.slice(0, 2).map(unit => `${unit.value}${unit.label}`).join(' ') : '0s';
}

function updateAllElapsedTimes() {
  document.querySelectorAll('.elapsed-time').forEach(element => {
    const ticketId = element.closest('.ticket-item')?.dataset?.id;
    if (ticketId) {
      const ticket = tickets.find(t => t.id === ticketId);
      if (ticket) element.textContent = calculateTimeElapsed(ticket.waitingSince);
    }
  });
}
