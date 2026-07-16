// Modal Management
function openNewTicketModal() {
  elements.newTicketModal.style.display = 'block';
  elements.parentSearchInput.focus();
  elements.parentEditIcon.style.display = 'none';
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

  elements.parentEditIcon.style.display = 'none';
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
  if (!title || !studentValue || !message || !assignee) {
    showToast('Please fill in all required fields', 'error');
    return;
  }

  const [firstName, lastName, tutorGroup] = studentValue.split('|');

  const selectedChild = state.activeParent.children.find(child =>
    child.firstName === firstName && child.lastName === lastName
  );

  const newTicketData = {
    title,
    studentFirstName: firstName,
    studentLastName: lastName,
    tutorGroup,
    assigneeName: assignee.name,
    assigneeEmail: assignee.email,
    parentName: state.activeParent.name,
    parentEmail: state.activeParent.email,
    parentPhone: state.activeParent.phone,
    parentRelationship: selectedChild?.parentRelationship || '',
    message
  };
  elements.createNewTicketBtn.disabled = true;
  let newTicket;
  try {
    newTicket = await apiCreateTicket(newTicketData);
  } catch {
    elements.createNewTicketBtn.disabled = false;
    return;
  }

  closeNewTicketModal();
  elements.tabs[0].click();
  if (isManager || newTicket.assigneeEmail === currentUserEmail) {
    tickets.unshift(newTicket);
    ticketsById.set(newTicket.id, newTicket);
    updateOpenTicketsBadge();
    renderTickets(state.activeTab);
    openTicketDetails(newTicket.id);
  } else {
    showToast('Ticket created successfully', 'success');
  }
}
