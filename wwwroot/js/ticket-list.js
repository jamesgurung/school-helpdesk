// Ticket List Rendering
function renderTickets(status) {
  elements.ticketsContainer.innerHTML = '';
  const filteredTickets = tickets.filter(ticket => ticket.closed === (status === 'closed'));
  
  if (filteredTickets.length === 0) {
    const emptyClone = document.getElementById('empty-tickets-template').content.cloneNode(true);
    emptyClone.querySelector('.status-text').textContent = status;
    elements.ticketsContainer.appendChild(emptyClone);
    return;
  }
  
  filteredTickets.forEach(ticket => {
    const ticketClone = document.getElementById('ticket-item-template').content.cloneNode(true);
    const ticketElement = ticketClone.querySelector('.ticket-item');
    ticketElement.dataset.id = ticket.id;
    
    ticketClone.querySelector('.ticket-title').textContent = ticket.title;
    ticketClone.querySelector('.student-value span:not(.material-symbols-rounded)').textContent = getFullName(ticket.studentFirstName, ticket.studentLastName);
    ticketClone.querySelector('.parent-value span:not(.material-symbols-rounded)').textContent = ticket.parentName;
    ticketClone.querySelector('.assignee-value span:not(.material-symbols-rounded)').textContent = ticket.assigneeName;
    ticketClone.querySelector('.created-date').textContent = formatDate(ticket.created);
    ticketClone.querySelector('.elapsed-time').textContent = calculateTimeElapsed(ticket.updated);
    
    elements.ticketsContainer.appendChild(ticketClone);
    
    document.querySelector(`.ticket-item[data-id="${ticket.id}"]`).addEventListener('click', () => {
      openTicketDetails(ticket.id);
    });
  });
}

function resetDetailsView() {
  state.activeTicket = null;
  state.conversation = [];
  elements.detailsEmpty.style.display = 'flex';
  elements.detailsContent.style.display = 'none';
  elements.ticketDetails.classList.remove('open');
  
  document.querySelectorAll('.ticket-item').forEach(item => {
    item.classList.remove('selected');
  });
}
