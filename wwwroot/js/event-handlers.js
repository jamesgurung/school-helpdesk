// Event Handlers and Listeners
function setupEventListeners() {    
  elements.tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      elements.tabs.forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      state.activeTab = tab.dataset.tab;
      renderTickets(state.activeTab);
      resetDetailsView();
    });
  });
  
  elements.mobileBack.addEventListener('click', resetDetailsView);
  elements.ticketTitleInput.addEventListener('blur', saveTicketChanges);
  elements.sendMessageBtn.addEventListener('click', sendMessage);
  elements.closeTicketBtn.addEventListener('click', closeTicket);
  elements.newMessageInput.addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  });
  
  elements.newTicketButton.addEventListener('click', openNewTicketModal);
  elements.closeModalBtn.addEventListener('click', closeNewTicketModal);
  elements.cancelNewTicketBtn.addEventListener('click', closeNewTicketModal);
  elements.createNewTicketBtn.addEventListener('click', createNewTicket);
  
  elements.parentSearchInput.addEventListener('input', e => {
    const results = filterParents(e.target.value);
    displayParentAutocompleteResults(results, state.activeParent);
  });
  
  elements.parentSearchInput.addEventListener('focus', e => {
    const query = e.target.value;
    if (query.trim()) {
      const results = filterParents(query);
      displayParentAutocompleteResults(results, state.activeParent);
    }
  });
  
  elements.parentInfo.addEventListener('click', toggleParentSearchMode);
  document.getElementById('parent-edit-icon').addEventListener('click', toggleParentSearchMode);
  
  setupParentSearchKeyboardNavigation();
  setupAssigneeSearchListeners();
  setupAssigneeEditListeners();
  setupDocumentClickHandlers();
}

function setupParentSearchKeyboardNavigation() {
  elements.parentSearchInput.addEventListener('keydown', e => {
    const autocompleteItems = elements.parentAutocompleteResults.querySelectorAll('.autocomplete-item');
    const selectedItem = elements.parentAutocompleteResults.querySelector('.autocomplete-item.selected');
    
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      if (autocompleteItems.length === 0) return;
      
      let nextSelectedIndex = 0;
      
      if (selectedItem) {
        const currentIndex = Array.from(autocompleteItems).indexOf(selectedItem);
        selectedItem.classList.remove('selected');
        
        if (e.key === 'ArrowDown') {
          nextSelectedIndex = (currentIndex + 1) % autocompleteItems.length;
        } else {
          nextSelectedIndex = (currentIndex - 1 + autocompleteItems.length) % autocompleteItems.length;
        }
      }
      
      autocompleteItems[nextSelectedIndex].classList.add('selected');
      autocompleteItems[nextSelectedIndex].scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'Enter') {
      const selectedItem = elements.parentAutocompleteResults.querySelector('.autocomplete-item.selected');
      
      if (selectedItem && elements.parentAutocompleteResults.style.display !== 'none') {
        // Extract parent data and select
        const nameEl = selectedItem.querySelector('.autocomplete-name');
        const emailEl = selectedItem.querySelector('.autocomplete-email');
        if (nameEl && emailEl) {
          const parent = parents.find(p => p.name === nameEl.textContent && p.email === emailEl.textContent);
          if (parent) selectParent(parent);
        }
        e.preventDefault();
      }
    } else if (e.key === 'Escape') {
      elements.parentAutocompleteResults.style.display = 'none';
    }
  });
}

function setupAssigneeSearchListeners() {
  elements.assigneeSearchInput.addEventListener('input', e => {
    const query = e.target.value;
    const results = staff.filter(s => s.name.toLowerCase().includes(query.toLowerCase()) || s.email.toLowerCase().includes(query.toLowerCase()));
    elements.assigneeAutocompleteResults.innerHTML = '';
    if (!results.length) { 
      elements.assigneeAutocompleteResults.style.display = 'none'; 
      return; 
    }
    results.forEach(s => {
      const item = document.createElement('div'); 
      item.className = 'autocomplete-item';
      item.innerHTML = `<div class="autocomplete-name">${s.name}</div><div class="autocomplete-email">${s.email}</div>`;
      item.addEventListener('click', () => selectNewTicketAssignee(s));
      elements.assigneeAutocompleteResults.appendChild(item);
    });
    elements.assigneeAutocompleteResults.style.display = 'block';
  });
  
  elements.assigneeSearchInput.addEventListener('focus', e => {
    setTimeout(() => {
      const val = e.target.value.trim();
      if (val) {
        e.target.dispatchEvent(new Event('input'));
      }
    }, 50);
  });
  
  elements.assigneeInfoDisplay.addEventListener('click', toggleAssigneeSearchMode);
  elements.assigneeEditIcon.addEventListener('click', toggleAssigneeSearchMode);
}

function setupAssigneeEditListeners() {
  elements.assigneeEditInput.addEventListener('input', e => {
    const query = e.target.value;
    const results = staff.filter(s => s.name.toLowerCase().includes(query.toLowerCase()) || s.email.toLowerCase().includes(query.toLowerCase()));
    elements.assigneeEditAutocompleteResults.innerHTML = '';
    if (!results.length) { 
      elements.assigneeEditAutocompleteResults.style.display = 'none'; 
      return; 
    }
    results.forEach(s => {
      const item = document.createElement('div'); 
      item.className = 'autocomplete-item';
      item.innerHTML = `<div class="autocomplete-name">${s.name}</div><div class="autocomplete-email">${s.email}</div>`;
      item.addEventListener('click', () => selectAssignee(s));
      elements.assigneeEditAutocompleteResults.appendChild(item);
    });
    elements.assigneeEditAutocompleteResults.style.display = 'block';
  });
  
  elements.assigneeEditInput.addEventListener('focus', e => {
    setTimeout(() => {
      const val = e.target.value.trim();
      if (val) {
        e.target.dispatchEvent(new Event('input'));
      }
    }, 50);
  });
    elements.assigneeEditInput.addEventListener('keydown', e => {
    const autocompleteItems = elements.assigneeEditAutocompleteResults.querySelectorAll('.autocomplete-item');
    const selectedItem = elements.assigneeEditAutocompleteResults.querySelector('.autocomplete-item.selected');
    
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      if (autocompleteItems.length === 0) return;
      
      let nextSelectedIndex = 0;
      
      if (selectedItem) {
        const currentIndex = Array.from(autocompleteItems).indexOf(selectedItem);
        selectedItem.classList.remove('selected');
        
        if (e.key === 'ArrowDown') {
          nextSelectedIndex = (currentIndex + 1) % autocompleteItems.length;
        } else {
          nextSelectedIndex = (currentIndex - 1 + autocompleteItems.length) % autocompleteItems.length;
        }
      }
      
      autocompleteItems[nextSelectedIndex].classList.add('selected');
      autocompleteItems[nextSelectedIndex].scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'Enter') {
      const selectedItem = elements.assigneeEditAutocompleteResults.querySelector('.autocomplete-item.selected');
      
      if (selectedItem && elements.assigneeEditAutocompleteResults.style.display !== 'none') {
        e.preventDefault();
        selectedItem.click();
      }
    } else if (e.key === 'Escape') {
      elements.assigneeEditAutocompleteResults.style.display = 'none';
    }
  });
}

function setupDocumentClickHandlers() {
  document.addEventListener('click', e => {
    if (!elements.parentSearchInput.contains(e.target) && !elements.parentAutocompleteResults.contains(e.target)) {
      elements.parentAutocompleteResults.style.display = 'none';
    }
    
    if (!elements.assigneeSearchInput.contains(e.target) && !elements.assigneeAutocompleteResults.contains(e.target)) {
      elements.assigneeAutocompleteResults.style.display = 'none';
    }
    
    if (!elements.assigneeEditInput.contains(e.target) && 
        !elements.assigneeEditAutocompleteResults.contains(e.target) &&
        !elements.assigneeInfoSection.querySelector('.edit-icon')?.contains(e.target)) {
      elements.assigneeEditAutocompleteResults.style.display = 'none';
      elements.assigneeEditContainer.style.display = 'none';
      const infoContainer = elements.assigneeInfoSection.querySelector('.info-container');
      if (infoContainer) infoContainer.style.display = 'flex';
    }
  });
}
