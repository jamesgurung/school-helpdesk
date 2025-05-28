// Utilities and Helper Functions

function matchesWordBeginning(text, query) {
  if (!text || !query) return false;

  const textLC = text.toLowerCase();
  const queryLC = query.toLowerCase().trim();

  if (queryLC === '') return false;

  for (let i = 0; i < textLC.length; i++) {
    if (i === 0 || /\s/.test(textLC[i - 1])) {
      if (textLC.substring(i).startsWith(queryLC)) {
        return true;
      }
    }
  }
  return false;
}

function updateBackButtonIcon() {
  const isMobile = window.innerWidth <= 768;
  const backIcon = elements.mobileBack.querySelector('.material-symbols-rounded');
  if (isMobile) {
    backIcon.textContent = 'arrow_back';
  } else {
    backIcon.textContent = 'close';
  }
}

function populateNewTicketForm() {
  elements.assigneeSearchInput.value = '';
  elements.assigneeAutocompleteResults.style.display = 'none';
  state.activeAssignee = null;
  elements.assigneeNameDisplay.textContent = 'No assignee selected';
  elements.assigneeNameDisplay.classList.add('no-parent');
  elements.assigneeSearchContainer.style.display = 'block';
  elements.assigneeInfoDisplay.style.display = 'none';
  elements.assigneeEditIcon.style.display = 'none';

  elements.parentNameDisplay.textContent = 'No parent selected';
  elements.parentNameDisplay.classList.add('no-parent');
  elements.parentRelationshipDisplay.textContent = '';
  elements.studentSelectInput.innerHTML = '<option value="" disabled selected>Select a student</option>';
  elements.studentSelectInput.disabled = true;

  elements.parentSearchInput.value = '';
  state.activeParent = null;

  elements.parentSearchContainer.style.display = 'block';
  elements.parentInfo.style.display = 'none';

  document.getElementById('parent-edit-icon').style.display = 'none';
}

function getFullName(firstName, lastName) {
  return `${firstName} ${lastName}`;
}

function updateParentRelationshipDisplay(relationship) {
  if (relationship) {
    elements.parentRelationshipDisplay.textContent = ` (${relationship})`;
  } else {
    elements.parentRelationshipDisplay.textContent = '';
  }
}

function showToast(message, type = 'info', duration = 3000) {
  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  toast.textContent = message;

  document.body.appendChild(toast);

  setTimeout(() => {
    toast.classList.add('show');
  }, 10);

  setTimeout(() => {
    toast.classList.remove('show');
    setTimeout(() => {
      document.body.removeChild(toast);
    }, 300);
  }, duration);
}

// Navigation warning utilities
function hasUnsentText() {
  return elements.newMessageInput && elements.newMessageInput.value.trim().length > 0;
}

function hasUnsentNewTicketText() {
  return elements.messageInput && elements.messageInput.value.trim().length > 0;
}

function confirmNavigationWithUnsentText(actionDescription, callback) {
  if (!hasUnsentText()) {
    callback();
    return;
  }

  const message = `You have unsent text in the message box. Are you sure you want to ${actionDescription}? Your unsent text will be lost.`;

  if (confirm(message)) {
    elements.newMessageInput.value = '';
    callback();
  }
}

function confirmModalCloseWithUnsentText(actionDescription, callback) {
  if (!hasUnsentNewTicketText()) {
    callback();
    return;
  }

  const message = `You have unsent text in the message box. Are you sure you want to ${actionDescription}? Your unsent text will be lost.`;

  if (confirm(message)) {
    elements.messageInput.value = '';
    callback();
  }
}