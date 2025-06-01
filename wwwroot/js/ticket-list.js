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
    
    const studentElement = ticketClone.querySelector('.student-value span:not(.material-symbols-rounded)');
    const assigneeElement = ticketClone.querySelector('.assignee-value span:not(.material-symbols-rounded)');
    
    const status = getTicketValidationStatus(ticket);
    
    if (status.hasStudent) {
      studentElement.textContent = getFullName(ticket.studentFirstName, ticket.studentLastName);
    } else {
      studentElement.textContent = 'Not Set';
      studentElement.style.color = 'var(--warning)';
    }
    
    if (status.hasAssignee) {
      assigneeElement.textContent = ticket.assigneeName;
    } else {
      assigneeElement.textContent = 'Unassigned';
      assigneeElement.style.color = 'var(--warning)';
    }
    ticketClone.querySelector('.created-date').textContent = formatDateTime(ticket.created);

    const waitTimeIcon = ticketClone.querySelector('.wait-time-icon');
    const waitTimeText = ticketClone.querySelector('.wait-time');

    if (ticket.isClosed) {
      waitTimeIcon.textContent = "check_circle";
      waitTimeText.textContent = "Resolved";
    } else if (ticket.waitingSince === null) {
      waitTimeIcon.textContent = "mark_email_read";
      waitTimeText.textContent = "Awaiting parent";
    } else {
      waitTimeIcon.textContent = "timer";
      waitTimeText.innerHTML = `Open for <span class="elapsed-time">${calculateTimeElapsed(ticket.waitingSince)}</span>`;
    }
    elements.ticketsContainer.appendChild(ticketClone);

    document.querySelector(`.ticket-item[data-id="${ticket.id}"]`).addEventListener('click', () => {
      confirmNavigationWithUnsentText('switch to another ticket', () => {
        openTicketDetails(ticket.id);
      });
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
  elements.salutation.style.opacity = '1';
  elements.valediction.style.opacity = '1';
  elements.salutation.querySelector('span').textContent = 'Parent/Carer';
  elements.newMessageInput.value = '';
  autoExpandTextarea(elements.newMessageInput);
  elements.messageAttachments.value = '';
  elements.attachmentList.innerHTML = '';
  elements.sendMessageBtn.textContent = 'Send Message';

  document.querySelectorAll('.ticket-item').forEach(item => {
    item.classList.remove('selected');
  });
}
