// Modal Management
function openNewTicketModal() {
  elements.newTicketModal.style.display = 'block';
  elements.parentSearchInput.focus();
  document.getElementById('parent-edit-icon').style.display = 'none';
}

function closeNewTicketModal() {
  elements.createNewTicketBtn.disabled = false;
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

  state.activeParent = null;
  elements.parentNameDisplay.textContent = 'No parent selected';
  elements.parentNameDisplay.classList.add('no-parent');
  elements.parentRelationshipDisplay.textContent = '';
  elements.parentInfo.style.display = 'flex';
  elements.parentSearchContainer.style.display = 'none';
  document.getElementById('parent-edit-icon').style.display = 'none';

  state.activeAssignee = null;
  elements.assigneeNameDisplay.textContent = 'No assignee selected';
  elements.assigneeNameDisplay.classList.add('no-parent');
  elements.assigneeInfoDisplay.style.display = 'flex';
  elements.assigneeSearchContainer.style.display = 'none';
  elements.assigneeEditIcon.style.display = 'none';
}

async function createNewTicket() {
  const title = elements.ticketTitleFormInput.value.trim().substring(0, 40);
  const studentValue = elements.studentSelectInput.value;
  const assignee = state.activeAssignee;
  const message = elements.messageInput.value.trim();

  if (!state.activeParent) {
    showToast('Please select a parent/carer', 'error');
    return;
  }
  if (!title || !studentValue || !message || !state.activeAssignee) {
    showToast('Please fill in all required fields', 'error');
    return;
  }

  const [firstName, lastName, tutorGroup] = studentValue.split('|');

  const selectedChild = state.activeParent.children.find(child =>
    child.firstName === firstName && child.lastName === lastName
  );

  const now = new Date().toISOString();
  const newTicketData = {
    title: title,
    isClosed: false,
    created: now,
    waitingSince: now,
    studentFirstName: firstName,
    studentLastName: lastName,
    tutorGroup: tutorGroup,
    assigneeName: assignee.name,
    assigneeEmail: assignee.email,
    parentName: state.activeParent.name,
    parentEmail: state.activeParent.email,
    parentPhone: state.activeParent.phone,
    parentRelationship: selectedChild?.parentRelationship || '',
    message: message
  };
  elements.createNewTicketBtn.disabled = true;
  const newTicketId = await apiCreateTicket(newTicketData);

  const newTicket = {
    ...newTicketData,
    id: newTicketId
  };

  delete newTicket.message;
  tickets.unshift(newTicket);
  updateOpenTicketsBadge();

  state.conversation = [{
    timestamp: now,
    authorName: state.activeParent.name,
    isEmployee: false,
    content: message
  }];

  closeNewTicketModal();
  elements.tabs[0].click();
  openTicketDetails(newTicketId);
}
