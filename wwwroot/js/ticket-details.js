// Ticket Details Management
function openTicketDetails(ticketId) {
  const ticket = tickets.find(t => t.id === ticketId);
  if (!ticket) return;

  state.currentTicketId = ticketId;

  document.querySelectorAll('.ticket-item').forEach(item => item.classList.remove('selected'));
  const selectedTicket = document.querySelector(`.ticket-item[data-id="${ticketId}"]`);
  if (selectedTicket) selectedTicket.classList.add('selected');

  elements.ticketTitleInput.innerText = ticket.title;
  elements.ticketTitleInput.contentEditable = isManager;

  const children = parents?.find(p => p.email === ticket.parentEmail && p.name === ticket.parentName)?.children || [];
  const ticketParents = parents?.filter(p => p.email === ticket.parentEmail) || [];

  populateStudentSelect(ticket, children);
  populateParentSelect(ticket, ticketParents);
  renderStudentInfo(ticket, children);
  renderParentInfo(ticket);
  renderAssigneeInfo(ticket);
  elements.internalNoteCheckbox.checked = false;
  elements.newMessageInput.classList.remove('internal-note');
  elements.salutation.style.opacity = '1';
  elements.valediction.style.opacity = '1';
  elements.salutation.querySelector('span').textContent = getSalutation(ticket.parentName);
  elements.sendMessageBtn.textContent = 'Send Message';
  elements.detailsEmpty.style.display = 'none';
  elements.detailsContent.style.display = 'block';
  elements.ticketDetails.classList.add('open');

  elements.closeTicketBtn.textContent = ticket.isClosed ? 'Reopen Ticket' : 'Close Ticket';
  updateCloseTicketButtonText();
  elements.ticketDetails.scrollTop = 0;
  elements.conversationContainer.innerHTML = '<div style="text-align: center; padding: 12px">Loading conversation...</div>';
  elements.sendMessageBtn.disabled = true;
  elements.newMessageInput.disabled = true;
  elements.internalNoteCheckbox.disabled = true;
  elements.closeTicketBtn.disabled = true;
  elements.sendMessageBtn.style.opacity = '0.5';
  elements.newMessageInput.style.opacity = '0.5';
  elements.closeTicketBtn.style.opacity = '0.5';
  elements.uploadFilesBtn.style.opacity = '0.5';
  elements.uploadFilesBtn.style.pointerEvents = 'none';
  elements.guidanceInput.value = '';

  fetch(`/api/tickets/${ticketId}`).then(response => response.json())
    .then(conversation => {
      state.conversation = conversation;      
      renderConversation();
      updateMessageControlsState(ticket);
    })
    .catch(error => {
      console.error('Error fetching conversation:', error);
      showToast('Error fetching ticket conversation: ' + error, 'error');
    });
}

function populateStudentSelect(ticket, children) {
  elements.studentSelect.innerHTML = '';
  
  const placeholderOption = document.createElement('option');
  placeholderOption.value = '';
  placeholderOption.textContent = 'Select a student';
  placeholderOption.disabled = true;
  placeholderOption.selected = true;
  elements.studentSelect.appendChild(placeholderOption);
  
  children.forEach(child => {
    const option = document.createElement('option');
    option.value = `${child.firstName}|${child.lastName}`;
    option.textContent = getFullName(child.firstName, child.lastName) + ` (${child.tutorGroup})`;
    elements.studentSelect.appendChild(option);
    if (child.firstName === ticket.studentFirstName && child.lastName === ticket.studentLastName) {
      option.selected = true;
      placeholderOption.selected = false;
    }
  });
}

function populateParentSelect(ticket, ticketParents) {
  elements.parentSelect.innerHTML = '';
  
  const placeholderOption = document.createElement('option');
  placeholderOption.value = '';
  placeholderOption.textContent = 'Select a parent/carer';
  placeholderOption.disabled = true;
  placeholderOption.selected = true;
  elements.parentSelect.appendChild(placeholderOption);
  
  ticketParents.forEach(parent => {
    const option = document.createElement('option');
    option.value = `${parent.name}|${parent.email}`;
    option.textContent = parent.name;
    elements.parentSelect.appendChild(option);
    if (parent.name === ticket.parentName && parent.email === ticket.parentEmail) {
      option.selected = true;
      placeholderOption.selected = false;
    }
  });
}

function renderStudentInfo(ticket, children) {
  const status = getTicketValidationStatus(ticket);
  const hasStudent = status.hasStudent;
  
  renderInfoSection('student', {
    heading: 'Student',
    icon: hasStudent ? 'school' : 'warning',
    name: hasStudent ? getFullName(ticket.studentFirstName, ticket.studentLastName) : 'Not Set',
    detail: hasStudent ? ticket.tutorGroup : '',
    editable: canEditField(ticket, 'student') && children.length > 1,
    editHandler: toggleStudentEdit,
    isWarning: !hasStudent
  });
}

function renderParentInfo(ticket) {
  const status = getTicketValidationStatus(ticket);
  const hasParent = status.hasParent;
  
  let parentRelationship = '';
  if (hasParent) {
    parentRelationship = ticket.parentRelationship;
    if (!parentRelationship) {
      const parent = parents?.find(p => p.email === ticket.parentEmail && p.name === ticket.parentName);
      const children = parent?.children || [];
      const currentChild = children.find(child =>
        child.firstName === ticket.studentFirstName && child.lastName === ticket.studentLastName
      );
      parentRelationship = currentChild?.parentRelationship || '';
    }
  }

  const ticketParents = parents?.filter(p => p.email === ticket.parentEmail) || [];

  renderInfoSection('parent', {
    heading: 'Parent/Carer',
    icon: hasParent ? 'supervisor_account' : 'warning',
    name: hasParent ? ticket.parentName : 'Not Set',
    detail: parentRelationship,
    email: ticket.parentEmail,
    phone: ticket.parentPhone,
    editable: canEditField(ticket, 'parent') && ticketParents.length > 1,
    editHandler: toggleParentEdit,
    isWarning: !hasParent
  });
}

function renderAssigneeInfo(ticket) {
  const status = getTicketValidationStatus(ticket);
  const hasAssignee = status.hasAssignee;
  
  renderInfoSection('assignee', {
    heading: 'Assigned To',
    icon: hasAssignee ? 'account_circle' : 'warning',
    name: hasAssignee ? ticket.assigneeName : 'Unassigned',
    editable: canEditField(ticket, 'assignee'),
    editHandler: toggleAssigneeEdit,
    isWarning: !hasAssignee
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
  
  const nameElement = infoClone.querySelector('.info-name');
  nameElement.textContent = config.name;
  if (config.isWarning) {
    nameElement.style.color = 'var(--warning)';
    infoClone.querySelector('.info-icon').style.color = 'var(--warning)';
  }
  
  infoClone.querySelector('.info-detail').textContent = config.detail || '';

  if (config.email) {
    const emailElement = infoClone.querySelector('.email-address');
    emailElement.textContent = config.email;
    emailElement.href = `mailto:${config.email}?subject=${encodeURIComponent(getCurrentTicket().title)}`;
    if (config.phone) {
      infoClone.querySelector('.phone-number').textContent = config.phone;
    } else {
      const phoneElement = infoClone.querySelector('.phone-number');
      phoneElement.style.display = 'none';
      phoneElement.previousElementSibling.style.display = 'none';
    }
  } else {
    infoClone.querySelector('.info-contact').style.display = 'none';
  }

  container.appendChild(infoClone);
}

function renderTicketInList(ticket) {
  const ticketElement = document.querySelector(`.ticket-item[data-id="${ticket.id}"]`);
  if (!ticketElement) return;

  ticketElement.querySelector('.ticket-title').textContent = ticket.title;
  
  const studentElement = ticketElement.querySelector('.student-value span:not(.material-symbols-rounded)');
  const assigneeElement = ticketElement.querySelector('.assignee-value span:not(.material-symbols-rounded)');
  
  const status = getTicketValidationStatus(ticket);
  
  if (status.hasStudent) {
    studentElement.textContent = getFullName(ticket.studentFirstName, ticket.studentLastName);
    studentElement.style.color = '';
    studentElement.style.fontStyle = '';
  } else {
    studentElement.textContent = 'Not Set';
    studentElement.style.color = 'var(--warning)';
  }
  
  if (status.hasAssignee) {
    assigneeElement.textContent = ticket.assigneeName;
    assigneeElement.style.color = '';
    assigneeElement.style.fontStyle = '';
  } else {
    assigneeElement.textContent = 'Unassigned';
    assigneeElement.style.color = 'var(--warning)';
  }
}

async function closeTicket() {
  const ticket = getCurrentTicket();
  if (!ticket) return;
  if (!ticket.isClosed && !canSendMessages(ticket)) return;
  if (elements.newMessageInput.value.trim().length > 0) {
    const sentSuccessfully = await sendMessage();
    if (!sentSuccessfully) return;
  } else {
    if (!ticket.isClosed && !state.conversation.slice(1).some(msg => msg.isEmployee && !msg.content.startsWith('#'))) {
      showToast('Please send a message or leave an internal note before closing.', 'error');
      return;
    }
  }
  toggleTicketStatus();
}

function updateMessageControlsState(ticket) {
  const canSend = canSendMessages(ticket);
  
  elements.sendMessageBtn.disabled = !canSend;
  elements.newMessageInput.disabled = !canSend;
  elements.internalNoteCheckbox.disabled = !canSend;
  elements.suggestStart.disabled = !canSend;
  elements.uploadFilesBtn.style.pointerEvents = canSend ? 'auto' : 'none';
  
  const canClose = ticket.isClosed || canSend;
  elements.closeTicketBtn.disabled = !canClose;
  
  if (!canSend) {
    elements.newMessageInput.placeholder = 'Complete ticket details first.';
    elements.sendMessageBtn.style.opacity = '0.5';
    elements.newMessageInput.style.opacity = '0.5';
    elements.uploadFilesBtn.style.opacity = '0.5';
    elements.suggestStart.style.opacity = '0.5';
  } else {
    elements.newMessageInput.placeholder = 'Type your message here...';
    elements.sendMessageBtn.style.opacity = '1';
    elements.newMessageInput.style.opacity = '1';
    elements.uploadFilesBtn.style.opacity = '1';
    elements.suggestStart.style.opacity = '1';
  }
  
  if (!canClose) {
    elements.closeTicketBtn.style.opacity = '0.5';  } else {
    elements.closeTicketBtn.style.opacity = '1';
  }
}
