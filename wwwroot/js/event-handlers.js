function setupEventListeners() {
  setupTabNavigation();
  setupTicketDetails();
  setupTicketCreation();
  setupUserActions();
  setupParentSearch();
  setupAssigneeFeatures();
  setupTicketEditFeatures();
  setupDocumentClickHandlers();
  setupIFrameStyles();
}

function setupTabNavigation() {
  elements.tabs.forEach(tab => tab.addEventListener('click', () => {
    confirmNavigationWithUnsentText('switch tabs', () => {
      elements.tabs.forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      state.activeTab = tab.dataset.tab;
      renderTickets(state.activeTab);
      resetDetailsView();
    });
  }));
}

function setupTicketDetails() {
  elements.backBtn.addEventListener('click', () => confirmNavigationWithUnsentText('go back', resetDetailsView));
    if (isManager) {
    elements.ticketTitleInput.addEventListener('blur', updateTicketTitle);
    elements.ticketTitleInput.addEventListener('input', e => {
      const text = e.target.innerText;
      if (text.length > 40) {
        e.target.innerText = text.substring(0, 40);
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(e.target);
        range.collapse(false);
        selection.removeAllRanges();
        selection.addRange(range);
      }
    });
    elements.ticketTitleInput.addEventListener('keydown', e => { 
      if (e.key === 'Enter') { 
        e.preventDefault(); 
        elements.ticketTitleInput.blur(); 
      } 
    });
  }
    elements.sendMessageBtn.addEventListener('click', sendMessage);
  elements.closeTicketBtn.addEventListener('click', closeTicket);
  elements.newMessageInput.addEventListener('input', e => {
    autoExpandTextarea(e.target);
    updateCloseTicketButtonText();
  });
  elements.messageAttachments.addEventListener('change', handleAttachmentChange);
  
  elements.internalNoteCheckbox.addEventListener('change', e => {
    const adding = e.target.checked;
    elements.newMessageInput.classList.toggle('internal-note', adding);
    if (!elements.sendMessageBtn.disabled) {
      elements.sendMessageBtn.textContent = adding ? 'Add Note' : 'Send Message';
    }
    elements.salutation.style.opacity = adding ? '0.2' : '1';
    elements.valediction.style.opacity = adding ? '0.2' : '1';
    updateCloseTicketButtonText();
  });
}

function setupTicketCreation() {
  elements.newTicketButton?.addEventListener('click', openNewTicketModal);
  
  [elements.closeModalBtn, elements.cancelNewTicketBtn].forEach(btn => 
    btn.addEventListener('click', () => 
      confirmModalCloseWithUnsentText(
        btn === elements.closeModalBtn ? 'close the new ticket form' : 'cancel creating this ticket', 
        closeNewTicketModal
      )
    )
  );
  
  elements.createNewTicketBtn.addEventListener('click', createNewTicket);
  
  elements.studentSelectInput.addEventListener('change', () => {
    const val = elements.studentSelectInput.value;
    if (state.activeParent && val) {
      const [firstName, lastName] = val.split('|');
      const child = state.activeParent.children.find(c => c.firstName === firstName && c.lastName === lastName);
      updateParentRelationshipDisplay(child?.parentRelationship || '');
    }
  });
}

function setupUserActions() { 
  elements.logoutBtn.addEventListener('click', () => window.location.href = '/auth/logout'); 
}

function setupParentSearch() {
  setupSearchInputListeners(elements.parentSearchInput, filterParents, results => displayParentAutocompleteResults(results, state.activeParent));
  elements.parentInfo.addEventListener('click', toggleParentSearchMode);
  document.getElementById('parent-edit-icon').addEventListener('click', toggleParentSearchMode);
  setupParentSearchKeyboardNavigation();
}

function setupAssigneeFeatures() { 
  setupAssigneeSearchListeners(); 
  setupAssigneeEditListeners(); 
}

const filterStaff = query => {
  const queryLC = query.toLowerCase().trim();
  if (!queryLC) return [];
  return staff.filter(s => matchesWordBeginning(s.name, queryLC) || matchesWordBeginning(s.email, queryLC));
};

function setupSearchInputListeners(input, filter, display) {
  input.addEventListener('input', e => display(filter(e.target.value)));
  input.addEventListener('focus', e => setTimeout(() => { 
    const v = e.target.value.trim(); 
    if (v) display(filter(v)); 
  }, 50));
}

function populateAutocompleteResults(container, items, nameField, emailField, onClick) {
  container.innerHTML = ''; 
  if (!items.length) { 
    container.style.display = 'none'; 
    return; 
  }
  items.forEach(item => {
    const div = document.createElement('div');
    div.className = 'autocomplete-item';
    div.innerHTML = `<div class="autocomplete-name">${item[nameField]}</div><div class="autocomplete-email">${item[emailField]}</div>`;
    div.addEventListener('click', () => onClick(item));
    container.appendChild(div);
  });
  container.style.display = 'block';
}

function handleAutocompleteKeyboardNavigation(input, results, onSelect, getText) {
  input.addEventListener('keydown', e => {
    const items = results.querySelectorAll('.autocomplete-item');
    let selected = results.querySelector('.autocomplete-item.selected');
    if (['ArrowDown', 'ArrowUp'].includes(e.key)) {
      e.preventDefault(); 
      if (!items.length) return;
      let i = selected ? Array.from(items).indexOf(selected) : -1;
      selected && selected.classList.remove('selected');
      i = (e.key === 'ArrowDown' ? i + 1 : i - 1 + items.length) % items.length;
      items[i].classList.add('selected'); 
      items[i].scrollIntoView({ block: 'nearest' });
    } else if ((e.key === 'Enter' || e.key === 'Tab') && results.style.display !== 'none') {
      const curr = results.querySelector('.autocomplete-item.selected');
      if (curr) { 
        e.preventDefault(); 
        if (getText) { 
          const { name, email } = getText(curr); 
          onSelect({ name, email }); 
        } else curr.click(); 
      }
    } else if (e.key === 'Escape') results.style.display = 'none';
  });
}

function setupParentSearchKeyboardNavigation() {
  const getData = item => ({
    name: item.querySelector('.autocomplete-name').textContent,
    email: item.querySelector('.autocomplete-email').textContent.split(' - ')[0]
  });
  handleAutocompleteKeyboardNavigation(
    elements.parentSearchInput,
    elements.parentAutocompleteResults,
    ({ name, email }) => { const p = parents.find(p => p.name === name && p.email === email); p && selectParent(p); },
    getData
  );
}

function setupAssigneeSearchListeners() {
  setupSearchInputListeners(
    elements.assigneeSearchInput,
    filterStaff,
    results => populateAutocompleteResults(elements.assigneeAutocompleteResults, results, 'name', 'email', selectNewTicketAssignee)
  );
  handleAutocompleteKeyboardNavigation(
    elements.assigneeSearchInput,
    elements.assigneeAutocompleteResults,
    ({ name, email }) => { 
      const a = staff.find(s => s.name === name && s.email === email); 
      a && selectNewTicketAssignee(a); 
    },
    item => ({ name: item.querySelector('.autocomplete-name').textContent, email: item.querySelector('.autocomplete-email').textContent })
  );
  [elements.assigneeInfoDisplay, elements.assigneeEditIcon].forEach(el => el.addEventListener('click', toggleAssigneeSearchMode));
}

function setupAssigneeEditListeners() {
  setupSearchInputListeners(elements.assigneeEditInput, filterStaff, results => populateAutocompleteResults(elements.assigneeEditAutocompleteResults, results, 'name', 'email', selectAssignee));
  handleAutocompleteKeyboardNavigation(elements.assigneeEditInput, elements.assigneeEditAutocompleteResults, () => { }, null);
}

function setupTicketEditFeatures() { 
  if (isManager) { 
    elements.studentSelect.addEventListener('change', function() { this.blur(); }); 
    elements.studentSelect.addEventListener('blur', updateTicketStudent); 
    elements.parentSelect.addEventListener('change', function() { this.blur(); }); 
    elements.parentSelect.addEventListener('blur', updateTicketParent); 
  } 
}

function setupDocumentClickHandlers() {
  document.addEventListener('click', e => {
    if (!elements.parentSearchInput.contains(e.target) && !elements.parentAutocompleteResults.contains(e.target)) {
      elements.parentAutocompleteResults.style.display = 'none';
    }
    if (!elements.assigneeSearchInput.contains(e.target) && !elements.assigneeAutocompleteResults.contains(e.target)) {
      elements.assigneeAutocompleteResults.style.display = 'none';
    }
    const editIcon = elements.assigneeInfoSection.querySelector('.edit-icon');
    if (!elements.assigneeEditInput.contains(e.target) && !elements.assigneeEditAutocompleteResults.contains(e.target) && !(editIcon && editIcon.contains(e.target))) {
      elements.assigneeEditAutocompleteResults.style.display = 'none'; 
      elements.assigneeEditContainer.style.display = 'none';
      const infoC = elements.assigneeInfoSection.querySelector('.info-container'); 
      infoC && (infoC.style.display = 'flex');
    }
    if (e.target === elements.newTicketModal) {
      if (!hasUnsentNewTicketText()) {
        closeNewTicketModal();
      }
    }
    if (e.target.id === 'image-modal') {
      closeImageModal();
    }
    if (e.target.id === 'original-email-modal') {
      closeOriginalEmailModal();
    }
  });
  document.addEventListener('keydown', e => { 
    if (e.key === 'Escape') { 
      if (elements.newTicketModal.style.display === 'block') {
        confirmModalCloseWithUnsentText('close the new ticket form', closeNewTicketModal); 
      }
      if (document.getElementById('image-modal').style.display === 'block') {
        closeImageModal(); 
      }
      if (document.getElementById('original-email-modal').style.display === 'block') {
        closeOriginalEmailModal(); 
      }
    } 
  });
}

function setupIFrameStyles() {
  elements.iframe.onload = () => {
    const style = elements.iframe.contentDocument.createElement('style');
    style.textContent = '* { margin: 0; } html { padding: 10px; }';
    elements.iframe.contentDocument.head.appendChild(style);
  };
}

function updateCloseTicketButtonText() {
  const t = getCurrentTicket(); 
  if (!t) return;
  const hasMsg = elements.newMessageInput.value.trim();
  if (t.isClosed) elements.closeTicketBtn.textContent = hasMsg ? 'Send & Reopen' : 'Reopen Ticket';
  else elements.closeTicketBtn.textContent = hasMsg ? 'Send & Close' : 'Close Ticket';
}
