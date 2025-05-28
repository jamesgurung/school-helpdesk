// Ticket List Rendering
function renderTickets(status) {
  elements.ticketsContainer.innerHTML = '';
  const filteredTickets = tickets.filter(ticket => ticket.isClosed === (status === 'closed'));
  
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
    ticketClone.querySelector('.assignee-value span:not(.material-symbols-rounded)').textContent = ticket.assigneeName;
    ticketClone.querySelector('.created-date').textContent = formatDateTime(ticket.created);
    
    const waitTimeIcon = ticketClone.querySelector('.wait-time-icon');
    const waitTimeText = ticketClone.querySelector('.wait-time');

    if (ticket.isClosed) {
      waitTimeIcon.textContent = "check_circle";
      waitTimeText.textContent = "Resolved";
    } else if (ticket.waitingSince === null) {
      waitTimeIcon.textContent = "pending";
      waitTimeText.textContent = "Reply sent";
    } else {
      waitTimeIcon.textContent = "timer";
      waitTimeText.innerHTML = `Needs reply &ndash; <span class="elapsed-time">${calculateTimeElapsed(ticket.waitingSince)}</span>`;
    }
    
    elements.ticketsContainer.appendChild(ticketClone);
    
    document.querySelector(`.ticket-item[data-id="${ticket.id}"]`).addEventListener('click', () => {
      openTicketDetails(ticket.id);
    });
  });
}

function resetDetailsView() {
  state.currentTicketId = null;
  state.conversation = [];
  elements.detailsEmpty.style.display = 'flex';
  elements.detailsContent.style.display = 'none';
  elements.ticketDetails.classList.remove('open');
  
  elements.internalNoteCheckbox.checked = false;
  elements.newMessageInput.classList.remove('internal-note');
  elements.sendMessageBtn.textContent = 'Send Message';
  
  document.querySelectorAll('.ticket-item').forEach(item => {
    item.classList.remove('selected');
  });
}
