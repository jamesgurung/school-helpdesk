// Ticket Details Management
function openTicketDetails(ticketId) {
  state.activeTicket = tickets.find(ticket => ticket.id === ticketId);
  if (!state.activeTicket) return;
  
  document.querySelectorAll('.ticket-item').forEach(item => item.classList.remove('selected'));
  const selectedTicket = document.querySelector(`.ticket-item[data-id="${ticketId}"]`);
  if (selectedTicket) selectedTicket.classList.add('selected');
  
  elements.ticketTitleInput.innerText = state.activeTicket.title;
  state.activeTicketChildren = parents.find(p => p.email === state.activeTicket.parentEmail).children;
  
  fetch(`/api/tickets/${ticketId}`)
    .then(response => response.json())
    .then(data => {
      state.conversation = data;
      
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
    })
    .catch(error => {
      alert('Error fetching ticket conversation:', error);
    });
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
    icon: 'child_care',
    name: getFullName(state.activeTicket.studentFirstName, state.activeTicket.studentLastName),
    detail: getStudentDetail(),
    editable: state.activeTicketChildren.length > 1,
    editHandler: toggleStudentEdit
  });
  
  displayInfoSection('parent', {
    heading: 'Parent/Carer',
    icon: 'supervisor_account',
    name: state.activeTicket.parentName,
    detail: state.activeTicket.parentRelationship,
    editable: false
  });
  
  displayInfoSection('assignee', {
    heading: 'Assigned To',
    icon: 'school',
    name: state.activeTicket.assigneeName,
    editable: true,
    editHandler: toggleAssigneeEdit
  });
}

function getStudentDetail() {
  const student = state.activeTicketChildren.find(
    child => child.firstName === state.activeTicket.studentFirstName && 
    child.lastName === state.activeTicket.studentLastName
  );
  return student ? student.tutorGroup : '';
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
  
  Object.assign(state.activeTicket, {...updates});
  
  renderTickets(state.activeTab);
  
  if (updates.assigneeEmail || updates.assigneeName) {
    displayInfoSection('assignee', {
      heading: 'Assigned To',
      icon: 'school',
      name: state.activeTicket.assigneeName,
      editable: true,
      editHandler: toggleAssigneeEdit
    });
  }
  
  if (updates.studentFirstName || updates.studentLastName) {
    displayInfoSection('student', {
      heading: 'Student',
      icon: 'child_care',
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
