// Modal Management
function openNewTicketModal() {
  elements.newTicketModal.style.display = 'block';
  elements.parentSearchInput.focus();
  document.getElementById('parent-edit-icon').style.display = 'none';
}

function closeNewTicketModal() {
  elements.newTicketModal.style.display = 'none';
  elements.newTicketForm.reset();
  elements.parentAutocompleteResults.style.display = 'none';
  elements.assigneeAutocompleteResults.style.display = 'none';
  
  state.activeParent = null;
  state.activeAssignee = null;
  
  elements.parentNameDisplay.textContent = 'No parent selected';
  elements.parentNameDisplay.classList.add('no-parent');
  elements.parentRelationshipDisplay.textContent = '';
  elements.assigneeNameDisplay.textContent = 'No assignee selected';
  elements.assigneeNameDisplay.classList.add('no-parent');
  
  elements.studentSelectInput.disabled = true;
  elements.studentSelectInput.innerHTML = '<option value="" disabled selected>Select a student</option>';
  
  elements.parentSearchContainer.style.display = 'block';
  elements.parentInfo.style.display = 'none';
  elements.assigneeSearchContainer.style.display = 'block';
  elements.assigneeInfoDisplay.style.display = 'none';
  
  document.getElementById('parent-edit-icon').style.display = 'none';
  document.getElementById('assignee-edit-icon').style.display = 'none';
}

function resetNewTicketForm() {
  elements.newTicketForm.reset();
  elements.ticketTitleFormInput.value = '';
  elements.messageInput.value = '';
  elements.studentSelectInput.innerHTML = '<option value="">Select a student...</option>';
  
  // Reset parent info
  state.activeParent = null;
  elements.parentNameDisplay.textContent = 'No parent selected';
  elements.parentNameDisplay.classList.add('no-parent');
  elements.parentRelationshipDisplay.textContent = '';
  elements.parentInfo.style.display = 'flex';
  elements.parentSearchContainer.style.display = 'none';
  document.getElementById('parent-edit-icon').style.display = 'none';
  
  // Reset assignee info
  state.activeAssignee = null;
  elements.assigneeNameDisplay.textContent = 'No assignee selected';
  elements.assigneeNameDisplay.classList.add('no-parent');
  elements.assigneeInfoDisplay.style.display = 'flex';
  elements.assigneeSearchContainer.style.display = 'none';
  elements.assigneeEditIcon.style.display = 'none';
}

function createNewTicket() {
  const title = elements.ticketTitleFormInput.value.trim();
  const studentValue = elements.studentSelectInput.value;
  const assignee = state.activeAssignee;
  const assigneeEmail = assignee?.email;
  const message = elements.messageInput.value.trim();
  
  if (!state.activeParent) {
    alert('Please select a parent/carer');
    return;
  }
  
  if (!title || !studentValue || !assigneeEmail || !message) {
    alert('Please fill in all required fields');
    return;
  }
  
  const [firstName, lastName, tutorGroup] = studentValue.split('-');
  const assigneeStaff = assignee;
  
  if (!assigneeStaff) {
    alert('Please select a valid staff member');
    return;
  }
  
  const now = new Date().toISOString();
  
  const newTicketData = {
    title: title,
    closed: false,
    created: now,
    updated: now,
    studentFirstName: firstName,
    studentLastName: lastName,
    tutorGroup: tutorGroup,
    assigneeName: assigneeStaff.name,
    assigneeEmail: assigneeStaff.email,
    parentName: state.activeParent.name,
    parentEmail: state.activeParent.email,
    parentRelationship: state.activeParent.relationship,
    message: message
  };
  
  fetch('/api/tickets', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-XSRF-TOKEN': antiforgeryToken
    },
    body: JSON.stringify(newTicketData)
  })
  .then(response => response.json())
  .then(data => {
    const newTicketId = data.id;
    
    const newTicket = {
      ...newTicketData,
      id: newTicketId
    };
    
    delete newTicket.message;
    tickets.unshift(newTicket);
    
    state.conversation = [{
      timestamp: now,
      authorEmail: state.activeParent.email,
      authorName: state.activeParent.name,
      isEmployee: false,
      content: message,
      attachments: []
    }];
    
    closeNewTicketModal();
    renderTickets('open');
    openTicketDetails(newTicketId);
  })
  .catch(error => {
    console.error('Error creating new ticket:', error);
    alert('Failed to create ticket. Please try again.');
  });
}
