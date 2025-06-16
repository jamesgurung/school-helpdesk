function matchesWordBeginning(text, query) {
  if (!text || !query) return false;
  const normalisedText = normalise(text);
  const normalisedQuery = normalise(query);
  return new RegExp(`\\b${normalisedQuery.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`).test(normalisedText);
}

function normalise(text) {
  return text.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').trim().toLowerCase();
}

function populateNewTicketForm() {
  Object.assign(elements.assigneeSearchContainer.style, { display: 'block' });
  elements.assigneeSearchInput.value = '';
  elements.assigneeAutocompleteResults.style.display = 'none';
  state.activeAssignee = null;
  elements.assigneeNameDisplay.textContent = 'No assignee selected';
  elements.assigneeNameDisplay.classList.add('no-parent');
  elements.assigneeInfoDisplay.style.display = 'none';
  elements.assigneeEditIcon.style.display = 'none';
  
  elements.parentSearchContainer.style.display = 'block';
  elements.parentSearchInput.value = '';
  state.activeParent = null;
  elements.parentInfo.style.display = 'none';
  document.getElementById('parent-edit-icon').style.display = 'none';
  
  elements.parentNameDisplay.textContent = 'No parent selected';
  elements.parentNameDisplay.classList.add('no-parent');
  elements.parentRelationshipDisplay.textContent = '';
  elements.studentSelectInput.innerHTML = '<option value="" disabled selected>Select a student</option>';
  elements.studentSelectInput.disabled = true;
}

function getFullName(firstName, lastName) {
  return `${firstName} ${lastName}`;
}

function updateParentRelationshipDisplay(relationship) {
  elements.parentRelationshipDisplay.textContent = relationship ? ` (${relationship})` : '';
}

function showToast(message, type = 'info', duration = 3000) {
  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  toast.textContent = message;
  document.body.appendChild(toast);
  setTimeout(() => toast.classList.add('show'), 10);
  setTimeout(() => { toast.classList.remove('show'); setTimeout(() => document.body.removeChild(toast), 300); }, duration);
}

function hasUnsentText() {
  return elements.newMessageInput?.value.trim().length > 0;
}

function hasUnsentNewTicketText() {
  return elements.messageInput?.value.trim().length > 0 ||
         elements.ticketTitleFormInput?.value.trim().length > 0 ||
         elements.parentSearchInput?.value.trim().length > 0 ||
         elements.assigneeSearchInput?.value.trim().length > 0 ||
         state.activeParent ||
         state.activeAssignee ||
         (elements.studentSelectInput?.value && elements.studentSelectInput.value !== '');
}

function confirmNavigationWithUnsentText(actionDescription, callback) {
  if (!hasUnsentText()) { callback(); return; }
  if (confirm(`You have unsent text in the message box. Are you sure you want to ${actionDescription}? Your unsent text will be lost.`)) {
    elements.newMessageInput.value = '';
    autoExpandTextarea(elements.newMessageInput);
    callback();
  }
}

function confirmModalCloseWithUnsentText(actionDescription, callback) {
  if (!hasUnsentNewTicketText()) { callback(); return; }
  if (confirm(`You have unsent text in the message box. Are you sure you want to ${actionDescription}? Your unsent text will be lost.`)) {
    elements.messageInput.value = '';
    callback();
  }
}

function getTicketValidationStatus(ticket) {
  if (!ticket) return { hasParent: false, hasStudent: false, hasAssignee: false };
  const hasParent = !!ticket.parentName?.trim();
  const hasStudent = !!(ticket.studentFirstName?.trim() && ticket.studentLastName?.trim());
  const hasAssignee = !!ticket.assigneeName?.trim();
  return { hasParent, hasStudent, hasAssignee };
}

function canEditField(ticket, fieldType) {
  if (!isManager) return false;
  const { hasParent, hasStudent } = getTicketValidationStatus(ticket);
  return fieldType === 'parent'
    || (fieldType === 'student' ? hasParent
    : fieldType === 'assignee' ? hasParent && hasStudent
    : false);
}

function canSendMessages(ticket) {
  const { hasParent, hasStudent, hasAssignee } = getTicketValidationStatus(ticket);
  return hasParent && hasStudent && hasAssignee;
}

function autoExpandTextarea(textarea) {
  textarea.style.height = 'auto';
  textarea.style.height = Math.max(150, textarea.scrollHeight) + 'px';
  elements.ticketDetails.scrollTop = elements.ticketDetails.scrollHeight;
}

function getSalutation(addressee) {
  if (!addressee) return 'Parent/Carer';
  const [a, b, ...c] = addressee.split(' ');
  const parts = c.length ? [a, b, c.join(' ')] : b ? [a, b] : [a];
  if (parts.length === 3 && parts[1].length === 1) return `${parts[0]} ${parts[2]}`;
  if (parts.length >= 2) return addressee;
  return 'Parent/Carer';
}