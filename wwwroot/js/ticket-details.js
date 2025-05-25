// Ticket Details Management
function openTicketDetails(ticketId) {
  state.activeTicket = tickets.find(ticket => ticket.id === ticketId);
  if (!state.activeTicket) return;
  
  document.querySelectorAll('.ticket-item').forEach(item => item.classList.remove('selected'));
  const selectedTicket = document.querySelector(`.ticket-item[data-id="${ticketId}"]`);
  if (selectedTicket) selectedTicket.classList.add('selected');
  
  elements.ticketTitleInput.innerText = state.activeTicket.title;
  
  const ticketData = customConversations[ticketId] || getTicketMessages(ticketId);
  state.activeTicketMessages = ticketData.messages;
  state.activeTicketChildren = ticketData.children;
  
  populateStudentSelect();
  displayInfoSections();
  renderConversation();
  
  elements.detailsEmpty.style.display = 'none';
  elements.detailsContent.style.display = 'block';
  elements.ticketDetails.classList.add('open');
  updateBackButtonIcon();
  if (state.activeTicket.closed) {
    elements.closeTicketBtn.textContent = 'Reopen Ticket';
  } else {
    elements.closeTicketBtn.textContent = 'Close Ticket';
  }
}

function populateStudentSelect() {
  elements.studentSelect.innerHTML = '';
  
  if (!state.activeTicketChildren?.length) return;
  
  state.activeTicketChildren.forEach(child => {
    const option = document.createElement('option');
    option.value = `${child.firstName}-${child.lastName}`;
    option.textContent = getFullName(child.firstName, child.lastName) + ` (${child.tutorGroup})`;
    elements.studentSelect.appendChild(option);
    
    if (child.firstName === state.activeTicket.studentFirstName && 
        child.lastName === state.activeTicket.studentLastName) {
      option.selected = true;
    }
  });
}

function displayInfoSections() {
  displayInfoSection('student', {
    heading: 'Student',
    icon: 'school',
    name: getFullName(state.activeTicket.studentFirstName, state.activeTicket.studentLastName),
    detail: getStudentDetail(),
    editable: state.activeTicketChildren.length > 1,
    editHandler: toggleStudentEdit
  });
  
  displayInfoSection('parent', {
    heading: 'Parent/Carer',
    icon: 'person',
    name: state.activeTicket.parentName,
    detail: ` (${state.activeTicket.parentRelationship})`,
    editable: false
  });
  
  displayInfoSection('assignee', {
    heading: 'Assigned To',
    icon: 'support_agent',
    name: state.activeTicket.assigneeName,
    detail: getAssigneeDetail(),
    editable: true,
    editHandler: toggleAssigneeEdit
  });
}

function getStudentDetail() {
  const student = state.activeTicketChildren.find(
    child => child.firstName === state.activeTicket.studentFirstName && 
    child.lastName === state.activeTicket.studentLastName
  );
  return student ? ` (${student.tutorGroup})` : '';
}

function getAssigneeDetail() {
  const assigneeStaff = staff.find(s => s.email === state.activeTicket.assigneeEmail);
  return assigneeStaff ? ` (${assigneeStaff.role})` : '';
}

function displayInfoSection(type, config) {
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
  infoClone.querySelector('.info-detail').textContent = config.detail;
  
  container.appendChild(infoClone);
}

function updateTicket(updates = {}) {
  if (!state.activeTicket) return;
  
  Object.assign(state.activeTicket, {
    ...updates,
    updated: new Date().toISOString()
  });
  
  renderTickets(state.activeTab);
  
  if (updates.assigneeEmail || updates.assigneeName) {
    const assigneeStaff = staff.find(s => s.email === state.activeTicket.assigneeEmail);
    displayInfoSection('assignee', {
      heading: 'Assigned To',
      icon: 'support_agent',
      name: state.activeTicket.assigneeName,
      detail: getAssigneeDetail(),
      editable: true,
      editHandler: toggleAssigneeEdit
    });
  }
  
  if (updates.studentFirstName || updates.studentLastName) {
    displayInfoSection('student', {
      heading: 'Student',
      icon: 'school',
      name: getFullName(state.activeTicket.studentFirstName, state.activeTicket.studentLastName),
      detail: getStudentDetail(),
      editable: true,
      editHandler: toggleStudentEdit
    });
  }

  const ticketElement = document.querySelector(`.ticket-item[data-id="${state.activeTicket.id}"]`);
  if (ticketElement) ticketElement.classList.add('selected');
}

function closeTicket() {
  if (!state.activeTicket) return;
  const ticketId = state.activeTicket.id;
  if (!state.activeTicket.closed) {
    updateTicket({ closed: true });
    resetDetailsView();
  } else {
    updateTicket({ closed: false });
    state.activeTab = 'open';
    elements.tabs.forEach(t => t.classList.toggle('active', t.dataset.tab === 'open'));
    renderTickets(state.activeTab);
    resetDetailsView();
  }
}
