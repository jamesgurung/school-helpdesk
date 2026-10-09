let ticketEditPending = false;

function setTicketEditPending(pending) {
  ticketEditPending = pending;
  elements.studentSelect.disabled = pending;
  elements.parentSelect.disabled = pending;
  elements.assigneeEditInput.disabled = pending;
  if (pending) elements.assigneeEditAutocompleteResults.style.display = 'none';
}

function getTicketChildren() {
  const ticket = getCurrentTicket();
  if (!ticket) return [];
  const parent = parents.find(p => p.email === ticket.parentEmail && p.name === ticket.parentName);
  return parent?.children || [];
}

function getTicketParents() {
  const ticket = getCurrentTicket();
  if (!ticket) return [];
  return parents.filter(p => p.email === ticket.parentEmail);
}

async function updateTicketTitle() {
  const ticket = getCurrentTicket();
  if (!ticket) return;

  const newTitle = elements.ticketTitleInput.innerText.trim().substring(0, 40);
  if (newTitle === ticket.title) return;
  const originalTitle = ticket.title;

  try {
    ticket.title = newTitle;
    document.title = `#${+ticket.id} ${newTitle} - Parent Helpdesk`;
    renderTicketInList(ticket);
    await apiUpdateTicketTitle(ticket.id, ticket.assigneeEmail, newTitle);
  } catch (error) {
    console.error('Failed to update title:', error);
    ticket.title = originalTitle;
    elements.ticketTitleInput.innerText = originalTitle;
    document.title = `#${+ticket.id} ${originalTitle} - Parent Helpdesk`;
  }
}

async function updateTicketStudent() {
  const ticket = getCurrentTicket();
  if (!ticket || ticketEditPending) return;

  const children = getTicketChildren();
  const student = children.find(child => getStudentSelectionValue(child) === elements.studentSelect.value);

  if (!student || studentMatchesTicket(student, ticket)) return;

  try {
    setTicketEditPending(true);
    await apiUpdateTicketStudent(ticket.id, ticket.assigneeEmail, student.firstName, student.lastName, student.tutorGroup, student.id);
    ticket.studentId = student.id;
    ticket.studentFirstName = student.firstName;
    ticket.studentLastName = student.lastName;
    ticket.tutorGroup = student.tutorGroup;
    ticket.parentRelationship = student.parentRelationship;
    renderTicketInList(ticket);
    if (state.currentTicketId !== ticket.id) return;
    renderStudentInfo(ticket, children);
    renderParentInfo(ticket);
    renderAssigneeInfo(ticket);
    updateMessageControlsState(ticket);
  } catch (error) {
    console.error('Failed to update student:', error);
    if (state.currentTicketId !== ticket.id) return;
    populateStudentSelect(ticket, children);
  } finally {
    setTicketEditPending(false);
  }
}

async function updateTicketParent() {
  const ticket = getCurrentTicket();
  if (!ticket || ticketEditPending) return;

  const [parentName, parentEmail] = elements.parentSelect.value.split('|');
  const ticketParents = getTicketParents();
  const selectedParent = ticketParents.find(parent => parent.name === parentName && parent.email === parentEmail);

  if (!selectedParent || ticket.parentName === parentName) return;

  try {
    setTicketEditPending(true);
    await apiUpdateTicketParent(ticket.id, ticket.assigneeEmail, parentName);
    ticket.parentName = parentName;
    ticket.parentPhone = selectedParent.phone;

    const newChildren = selectedParent.children;
    const currentStudent = newChildren.find(child => studentMatchesTicket(child, ticket));

    if (currentStudent) {
      ticket.parentRelationship = currentStudent.parentRelationship;
    } else {
      ticket.studentId = null;
      ticket.studentFirstName = null;
      ticket.studentLastName = null;
      ticket.tutorGroup = null;
      ticket.parentRelationship = null;
    }
    renderTicketInList(ticket);
    if (state.currentTicketId !== ticket.id) return;
    populateStudentSelect(ticket, newChildren);
    renderStudentInfo(ticket, newChildren);
    renderParentInfo(ticket);
    renderAssigneeInfo(ticket);
    updateMessageControlsState(ticket);
    elements.salutation.querySelector('span').textContent = getSalutation(parentName);
  } catch (error) {
    console.error('Failed to update parent:', error);
    if (state.currentTicketId !== ticket.id) return;
    const currentParents = getTicketParents();
    const currentParent = currentParents.find(p => p.name === ticket.parentName);
    if (currentParent) {
      elements.parentSelect.value = `${currentParent.name}|${currentParent.email}`;
    }
  } finally {
    setTicketEditPending(false);
  }
}

async function updateTicketAssignee() {
  const ticket = getCurrentTicket();
  if (!ticket || ticketEditPending || !state.activeEditAssignee) return;

  const assignee = state.activeEditAssignee;
  const oldAssigneeEmail = ticket.assigneeEmail;
  const newAssigneeEmail = assignee.email;

  if (oldAssigneeEmail === newAssigneeEmail) {
    state.activeEditAssignee = null;
    renderAssigneeInfo(ticket);
    return;
  }

  state.activeEditAssignee = null;
  try {
    setTicketEditPending(true);
    state.updating = true;
    const lastUpdated = await apiUpdateTicketAssignee(ticket.id, oldAssigneeEmail, newAssigneeEmail);
    ticket.assigneeId = assignee.id;
    ticket.assigneeEmail = assignee.email;
    ticket.assigneeName = assignee.name;
    ticket.lastUpdated = lastUpdated;
    renderTickets(state.activeTab);
    if (state.currentTicketId !== ticket.id) return;
    addConversationEntry(`#assign ${assignee.name}`);
    renderTicketInList(ticket);
    renderAssigneeInfo(ticket);
    updateMessageControlsState(ticket);
  } catch (error) {
    console.error('Failed to update assignee:', error);
  } finally {
    state.updating = false;
    setTicketEditPending(false);
  }
}

async function toggleTicketStatus() {
  const ticket = getCurrentTicket();
  if (!ticket) return;

  try {
    ticket.isClosed = !ticket.isClosed;
    if (ticket.isClosed) {
      resetDetailsView();
    } else {
      addConversationEntry('#reopen');
      elements.closeTicketBtn.textContent = 'Close Ticket';
      updateCloseTicketButtonText();
      updateMessageControlsState(ticket);
    }
    renderTickets(state.activeTab);
    updateOpenTicketsBadge();
    state.updating = true;
    const updated = await apiUpdateTicketStatus(ticket.id, ticket.assigneeEmail, ticket.isClosed);
    if (updated) ticket.lastUpdated = updated;
    renderTickets(state.activeTab);
  } catch (error) {
    console.error('Failed to update ticket status:', error);
  } finally {
    state.updating = false;
  }
}

function toggleSelectEdit(selectEl, infoSection, getOptions, updateFn) {
  const options = getOptions();
  const hasSelection = !!(selectEl && selectEl.value && selectEl.value.trim() !== '');
  if (options.length <= 1 && hasSelection) return;
  const infoContainer = infoSection.querySelector('.info-container');
  if (selectEl.parentElement === infoSection) {
    updateFn();
    selectEl.classList.add('hidden-select');
    elements.hiddenSelects.appendChild(selectEl);
    infoContainer.style.display = 'flex';
  } else {
    infoContainer.style.display = 'none';
    selectEl.classList.remove('hidden-select');
    infoSection.appendChild(selectEl);
    selectEl.style.display = 'block';
    selectEl.focus();
  }
}

function toggleStudentEdit() {
  const ticket = getCurrentTicket();
  if (ticketEditPending || !canEditField(ticket, 'student')) return;
  toggleSelectEdit(elements.studentSelect, elements.studentInfoSection, getTicketChildren, updateTicketStudent);
}

function toggleParentEdit() {
  const ticket = getCurrentTicket();
  if (ticketEditPending || !canEditField(ticket, 'parent')) return;
  toggleSelectEdit(elements.parentSelect, elements.parentInfoSection, getTicketParents, updateTicketParent);
}

function toggleAssigneeEdit() {
  const ticket = getCurrentTicket();
  if (ticketEditPending || !canEditField(ticket, 'assignee')) return;
  
  const editContainer = elements.assigneeEditContainer;
  const infoContainer = elements.assigneeInfoSection.querySelector('.info-container');

  if (editContainer.style.display === 'none') {
    infoContainer.style.display = 'none';
    editContainer.style.display = 'block';
    elements.assigneeEditInput.value = ticket.assigneeName;
    elements.assigneeEditInput.focus();
    elements.assigneeEditInput.select();
    setTimeout(() => {
      if (elements.assigneeEditInput.value) {
        elements.assigneeEditInput.dispatchEvent(new Event('input'));
      }
    }, 50);
  } else {
    editContainer.style.display = 'none';
    infoContainer.style.display = 'flex';
    updateTicketAssignee();
  }
}
