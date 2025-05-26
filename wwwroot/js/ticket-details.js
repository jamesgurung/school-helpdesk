// Ticket Details Management
function openTicketDetails(ticketId) {
  const ticket = tickets.find(t => t.id === ticketId);
  if (!ticket) return;
  
  state.currentTicketId = ticketId;
  
  document.querySelectorAll('.ticket-item').forEach(item => item.classList.remove('selected'));
  const selectedTicket = document.querySelector(`.ticket-item[data-id="${ticketId}"]`);
  if (selectedTicket) selectedTicket.classList.add('selected');
  
  elements.ticketTitleInput.innerText = ticket.title;
  
  fetch(`/api/tickets/${ticketId}`)
    .then(response => response.json())
    .then(conversation => {
      state.conversation = conversation;
      
      const children = parents.find(p => p.email === ticket.parentEmail)?.children || [];
      
      populateStudentSelect(ticket, children);
      renderStudentInfo(ticket, children);
      renderParentInfo(ticket);
      renderAssigneeInfo(ticket);
      renderConversation();
      
      elements.detailsEmpty.style.display = 'none';
      elements.detailsContent.style.display = 'block';
      elements.ticketDetails.classList.add('open');
      updateBackButtonIcon();
      
      elements.closeTicketBtn.textContent = ticket.closed ? 'Reopen Ticket' : 'Close Ticket';
    })
    .catch(error => {
      console.error('Error fetching conversation:', error);
      showToast('Error fetching ticket conversation: ' + error, 'error');
    });
}

function populateStudentSelect(ticket, children) {
  elements.studentSelect.innerHTML = '';
  
  children.forEach(child => {
    const option = document.createElement('option');
    option.value = `${child.firstName}-${child.lastName}`;
    option.textContent = getFullName(child.firstName, child.lastName) + ` (${child.tutorGroup})`;
    elements.studentSelect.appendChild(option);
    
    if (child.firstName === ticket.studentFirstName && child.lastName === ticket.studentLastName) {
      option.selected = true;
    }
  });
}

function renderStudentInfo(ticket, children) {
  renderInfoSection('student', {
    heading: 'Student',
    icon: 'child_care',
    name: getFullName(ticket.studentFirstName, ticket.studentLastName),
    detail: ticket.tutorGroup,
    editable: children.length > 1,
    editHandler: toggleStudentEdit
  });
}

function renderParentInfo(ticket) {
  renderInfoSection('parent', {
    heading: 'Parent/Carer',
    icon: 'supervisor_account',
    name: ticket.parentName,
    detail: ticket.parentRelationship,
    editable: false
  });
}

function renderAssigneeInfo(ticket) {
  renderInfoSection('assignee', {
    heading: 'Assigned To',
    icon: 'school',
    name: ticket.assigneeName,
    editable: true,
    editHandler: toggleAssigneeEdit
  });
}

function renderInfoSection(type, config) {
  const container = elements[`${type}InfoSection`];
  container.innerHTML = '';
  
  const infoClone = document.getElementById('info-section-template').content.cloneNode(true);
  
  infoClone.querySelector('.heading-text').textContent = config.heading;
  
  if (config.editable) {
    infoClone.querySelector('.edit-icon').textContent = 'edit';
    infoClone.querySelector('.edit-icon').addEventListener('click', config.editHandler);
  } else {
    infoClone.querySelector('.edit-icon').remove();
  }
  
  const infoContainer = infoClone.querySelector('.info-container');
  infoContainer.classList.add(`${type}-info`);
  
  infoClone.querySelector('.info-icon').textContent = config.icon;
  infoClone.querySelector('.info-name').textContent = config.name;
  infoClone.querySelector('.info-detail').textContent = config.detail || '';
  
  container.appendChild(infoClone);
}

function renderTicketInList(ticket) {
  const ticketElement = document.querySelector(`.ticket-item[data-id="${ticket.id}"]`);
  if (!ticketElement) return;
  
  ticketElement.querySelector('.ticket-title').textContent = ticket.title;
  ticketElement.querySelector('.student-value span:not(.material-symbols-rounded)').textContent = 
    getFullName(ticket.studentFirstName, ticket.studentLastName);
  ticketElement.querySelector('.assignee-value span:not(.material-symbols-rounded)').textContent = 
    ticket.assigneeName;
}

function closeTicket() {
  toggleTicketStatus();
}
