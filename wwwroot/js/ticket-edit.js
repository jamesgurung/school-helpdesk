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

  const newTitle = elements.ticketTitleInput.innerText.trim();
  if (newTitle === ticket.title) return;

  try {
    await apiUpdateTicketTitle(ticket.id, ticket.assigneeEmail, newTitle);
    ticket.title = newTitle;
    renderTicketInList(ticket);
  } catch (error) {
    console.error('Failed to update title:', error);
    elements.ticketTitleInput.innerText = ticket.title;
  }
}

async function updateTicketStudent() {
  const ticket = getCurrentTicket();
  if (!ticket) return;

  const [firstName, lastName] = elements.studentSelect.value.split('|');
  const children = getTicketChildren();
  const student = children.find(child =>
    child.firstName === firstName && child.lastName === lastName
  );

  if (!student) return;

  try {
    await apiUpdateTicketStudent(ticket.id, ticket.assigneeEmail, firstName, lastName, student.tutorGroup);
    ticket.studentFirstName = firstName;
    ticket.studentLastName = lastName;
    ticket.tutorGroup = student.tutorGroup;
    ticket.parentRelationship = student.parentRelationship;
    renderTicketInList(ticket);
    renderStudentInfo(ticket, children);
    renderParentInfo(ticket);
    renderAssigneeInfo(ticket);
    updateMessageControlsState(ticket);
  } catch (error) {
    console.error('Failed to update student:', error);
    elements.studentSelect.value = `${ticket.studentFirstName}|${ticket.studentLastName}`;
  }
}

async function updateTicketParent() {
  const ticket = getCurrentTicket();
  if (!ticket) return;

  const [parentName, parentEmail] = elements.parentSelect.value.split('|');
  const ticketParents = getTicketParents();
  const selectedParent = ticketParents.find(parent => parent.name === parentName && parent.email === parentEmail);

  if (!selectedParent) return;

  try {
    await apiUpdateTicketParent(ticket.id, ticket.assigneeEmail, parentName);
    ticket.parentName = parentName;
    ticket.parentEmail = parentEmail;

    const newChildren = selectedParent.children;
    const currentStudent = newChildren.find(child =>
      child.firstName === ticket.studentFirstName && child.lastName === ticket.studentLastName
    );

    if (currentStudent) {
      ticket.parentRelationship = currentStudent.parentRelationship;
    } else {
      ticket.parentRelationship = 'Parent/Carer';
    }
    renderTicketInList(ticket);
    populateStudentSelect(ticket, newChildren);
    renderStudentInfo(ticket, newChildren);
    renderParentInfo(ticket);
    updateMessageControlsState(ticket);
  } catch (error) {
    console.error('Failed to update parent:', error);
    const currentParents = getTicketParents();
    const currentParent = currentParents.find(p => p.name === ticket.parentName);
    if (currentParent) {
      elements.parentSelect.value = `${currentParent.name}|${currentParent.email}`;
    }
  }
}

async function updateTicketAssignee() {
  const ticket = getCurrentTicket();
  if (!ticket || !state.activeEditAssignee) return;

  const oldAssigneeEmail = ticket.assigneeEmail;
  const newAssigneeEmail = state.activeEditAssignee.email;

  if (oldAssigneeEmail === newAssigneeEmail) {
    state.activeEditAssignee = null;
    renderAssigneeInfo(ticket);
    return;
  }

  try {
    await apiUpdateTicketAssignee(ticket.id, oldAssigneeEmail, newAssigneeEmail);
    ticket.assigneeEmail = state.activeEditAssignee.email;
    ticket.assigneeName = state.activeEditAssignee.name;
    renderTicketInList(ticket);
    renderAssigneeInfo(ticket);
    state.activeEditAssignee = null;
    updateMessageControlsState(ticket);
  } catch (error) {
    console.error('Failed to update assignee:', error);
  }
}

async function toggleTicketStatus() {
  const ticket = getCurrentTicket();
  if (!ticket) return;

  const newStatus = !ticket.isClosed;

  try {
    await apiUpdateTicketStatus(ticket.id, ticket.assigneeEmail, newStatus);
    ticket.isClosed = newStatus;
    renderTickets(state.activeTab);
    resetDetailsView();
  } catch (error) {
    console.error('Failed to update ticket status:', error);
  }
}

function toggleSelectEdit(selectEl, infoSection, getOptions, updateFn) {
  const options = getOptions();
  if (options.length <= 1) return;
  const infoContainer = infoSection.querySelector('.info-container');
  if (selectEl.parentElement === infoSection) {
    selectEl.removeEventListener('change', selectEl._changeHandler);
    selectEl.removeEventListener('blur', selectEl._blurHandler);
    selectEl._blurPrevented = true;
    updateFn();
    selectEl.classList.add('hidden-select');
    document.querySelector('.hidden-selects').appendChild(selectEl);
    infoContainer.style.display = 'flex';
  } else {
    infoContainer.style.display = 'none';
    selectEl.classList.remove('hidden-select');
    infoSection.appendChild(selectEl);
    selectEl.style.display = 'block';
    selectEl.focus();
    selectEl._changeHandler = () => {
      selectEl._blurPrevented = true;
      toggleSelectEdit(selectEl, infoSection, getOptions, updateFn);
    };
    selectEl._blurHandler = () => {
      setTimeout(() => {
        if (!selectEl._blurPrevented && document.activeElement !== selectEl) {
          toggleSelectEdit(selectEl, infoSection, getOptions, updateFn);
        }
        selectEl._blurPrevented = false;
      }, 100);
    };
    selectEl.addEventListener('change', selectEl._changeHandler, { once: true });
    selectEl.addEventListener('blur', selectEl._blurHandler, { once: true });
  }
}

function toggleStudentEdit() {
  const ticket = getCurrentTicket();
  if (!canEditField(ticket, 'student')) return;
  toggleSelectEdit(elements.studentSelect, elements.studentInfoSection, getTicketChildren, updateTicketStudent);
}

function toggleParentEdit() {
  const ticket = getCurrentTicket();
  if (!canEditField(ticket, 'parent')) return;
  toggleSelectEdit(elements.parentSelect, elements.parentInfoSection, getTicketParents, updateTicketParent);
}

function toggleAssigneeEdit() {
  const ticket = getCurrentTicket();
  if (!canEditField(ticket, 'assignee')) return;
  
  const editContainer = elements.assigneeEditContainer;
  const infoContainer = elements.assigneeInfoSection.querySelector('.info-container');

  if (editContainer.style.display === 'none') {
    const ticket = getCurrentTicket();
    if (!ticket) return;

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
