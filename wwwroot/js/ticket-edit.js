// Edit Functionality for Tickets
function saveTicketChanges() {
  if (!state.activeTicket) return;
  
  updateTicket({
    title: elements.ticketTitleInput.innerText
    // Removed assigneeEmail and assigneeName handling as it's now handled by autocomplete
  });
}

function saveStudentChanges() {
  if (!state.activeTicket) return;
  
  const [firstName, lastName] = elements.studentSelect.value.split('-');
  
  updateTicket({
    studentFirstName: firstName,
    studentLastName: lastName
  });
}

function toggleEdit(type, saveFunction) {
  // We only handle student selection now as assignee uses autocomplete
  if (type !== 'student') return;
  
  const infoSection = elements[`${type}InfoSection`];
  const infoContainer = infoSection.querySelector('.info-container');
  const selectEl = elements[`${type}Select`];
  
  if (selectEl.parentElement === infoSection) {
    selectEl.removeEventListener('change', selectEl._changeHandler);
    selectEl.removeEventListener('blur', selectEl._blurHandler);
    
    saveFunction();
    
    selectEl.classList.add('hidden-select');
    document.querySelector('.hidden-selects').appendChild(selectEl);
    infoContainer.style.display = 'flex';
  } 
  else {
    infoContainer.style.display = 'none';
    selectEl.classList.remove('hidden-select');
    infoSection.appendChild(selectEl);
    selectEl.style.display = 'block';
    selectEl.focus();
    
    selectEl._changeHandler = () => {
      selectEl._blurPrevented = true;
      toggleEdit(type, saveFunction);
    };
    
    selectEl._blurHandler = () => {
      setTimeout(() => {
        if (!selectEl._blurPrevented && document.activeElement !== selectEl) {
          toggleEdit(type, saveFunction);
        }
        selectEl._blurPrevented = false;
      }, 100);
    };
    
    selectEl.addEventListener('change', selectEl._changeHandler, { once: true });
    selectEl.addEventListener('blur', selectEl._blurHandler, { once: true });
  }
}

function toggleStudentEdit() {
  toggleEdit('student', saveStudentChanges);
}

function toggleAssigneeEdit() {
  const editContainer = elements.assigneeEditContainer;
  const infoContainer = elements.assigneeInfoSection.querySelector('.info-container');
  if (editContainer.style.display === 'none') {
    infoContainer.style.display = 'none';
    editContainer.style.display = 'block';
    elements.assigneeEditInput.value = state.activeTicket.assigneeName;
    elements.assigneeEditInput.focus();
    setTimeout(() => {
      if (elements.assigneeEditInput.value) {
        elements.assigneeEditInput.dispatchEvent(new Event('input'));
      }
    }, 50);
  } else {
    editContainer.style.display = 'none';
    infoContainer.style.display = 'flex';
    if (state.activeEditAssignee) {
      updateTicket({
        assigneeEmail: state.activeEditAssignee.email,
        assigneeName: state.activeEditAssignee.name
      });
      state.activeEditAssignee = null;
    }
  }
}
