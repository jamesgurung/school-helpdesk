// Event Handlers and Listeners
function setupEventListeners() {    
  setupTabNavigation();
  setupTicketDetails();
  setupTicketCreation();
  setupUserActions();
  setupParentSearch();
  setupAssigneeFeatures();
  setupDocumentClickHandlers();
}

function setupTabNavigation() {
  elements.tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      elements.tabs.forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      state.activeTab = tab.dataset.tab;
      renderTickets(state.activeTab);
      resetDetailsView();
    });
  });
}

function setupTicketDetails() {
  elements.mobileBack.addEventListener('click', resetDetailsView);
  
  if (isManager) {
    elements.ticketTitleInput.addEventListener('blur', updateTicketTitle);
    elements.ticketTitleInput.addEventListener('keydown', e => {
      if (e.key === 'Enter') {
        e.preventDefault();
        elements.ticketTitleInput.blur();
      }
    });
  }
  elements.sendMessageBtn.addEventListener('click', sendMessage);
  elements.closeTicketBtn.addEventListener('click', closeTicket);
  elements.internalNoteCheckbox.addEventListener('change', e => {
    if (e.target.checked) {
      elements.newMessageInput.classList.add('internal-note');
      if (!elements.sendMessageBtn.disabled) {
        elements.sendMessageBtn.textContent = 'Add Note';
      }
    } else {
      elements.newMessageInput.classList.remove('internal-note');
      if (!elements.sendMessageBtn.disabled) {
        elements.sendMessageBtn.textContent = 'Send Message';
      }
    }
  });
}

function setupTicketCreation() {
  elements.newTicketButton?.addEventListener('click', openNewTicketModal);
  elements.closeModalBtn.addEventListener('click', closeNewTicketModal);
  elements.cancelNewTicketBtn.addEventListener('click', closeNewTicketModal);
  elements.createNewTicketBtn.addEventListener('click', createNewTicket);
}

function setupUserActions() {
  elements.logoutBtn.addEventListener('click', () => {
    window.location.href = '/auth/logout';
  });
}

function setupParentSearch() {
  setupSearchInputListeners(
    elements.parentSearchInput,
    filterParents,
    (results) => displayParentAutocompleteResults(results, state.activeParent)
  );
  
  elements.parentInfo.addEventListener('click', toggleParentSearchMode);
  document.getElementById('parent-edit-icon').addEventListener('click', toggleParentSearchMode);
  
  setupParentSearchKeyboardNavigation();
}

function setupAssigneeFeatures() {
  setupAssigneeSearchListeners();
  setupAssigneeEditListeners();
}

function setupSearchInputListeners(inputElement, filterCallback, displayResultsCallback) {
  inputElement.addEventListener('input', e => {
    displayResultsCallback(filterCallback(e.target.value));
  });

  inputElement.addEventListener('focus', e => {
    setTimeout(() => {
      const query = e.target.value.trim();
      if (query) {
        displayResultsCallback(filterCallback(query));
      }
    }, 50);
  });
}

function populateAutocompleteResults(resultsElement, items, nameField, emailField, clickCallback) {
  resultsElement.innerHTML = '';
  
  if (!items.length) {
    resultsElement.style.display = 'none';
    return;
  }
  
  items.forEach(item => {
    const itemDiv = document.createElement('div');
    itemDiv.className = 'autocomplete-item';
    itemDiv.innerHTML = `
      <div class="autocomplete-name">${item[nameField]}</div>
      <div class="autocomplete-email">${item[emailField]}</div>
    `;
    itemDiv.addEventListener('click', () => clickCallback(item));
    resultsElement.appendChild(itemDiv);
  });
  
  resultsElement.style.display = 'block';
}

function handleAutocompleteKeyboardNavigation(inputElement, resultsElement, onSelectCallback, getItemTextCallback) {
  inputElement.addEventListener('keydown', e => {
    const items = resultsElement.querySelectorAll('.autocomplete-item');
    const selectedItem = resultsElement.querySelector('.autocomplete-item.selected');
    
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      if (items.length === 0) return;

      let nextIndex = 0;
      if (selectedItem) {
        const currentIndex = Array.from(items).indexOf(selectedItem);
        selectedItem.classList.remove('selected');
        nextIndex = e.key === 'ArrowDown' 
          ? (currentIndex + 1) % items.length 
          : (currentIndex - 1 + items.length) % items.length;
      }
      
      items[nextIndex].classList.add('selected');
      items[nextIndex].scrollIntoView({ block: 'nearest' });
    } 
    else if ((e.key === 'Enter' || e.key === 'Tab') && 
             resultsElement.style.display !== 'none') {
      const currentSelected = resultsElement.querySelector('.autocomplete-item.selected');
      if (currentSelected) {
        e.preventDefault();
        if (getItemTextCallback) {
          const { name, email } = getItemTextCallback(currentSelected);
          onSelectCallback({ name, email });
        } else {
          currentSelected.click();
        }
      }
    } 
    else if (e.key === 'Escape') {
      resultsElement.style.display = 'none';
    }
  });
}

function setupParentSearchKeyboardNavigation() {
  const extractItemData = (selectedItem) => {
    const nameEl = selectedItem.querySelector('.autocomplete-name');
    const emailEl = selectedItem.querySelector('.autocomplete-email');
    return nameEl && emailEl 
      ? { name: nameEl.textContent, email: emailEl.textContent.split(' - ')[0] }
      : null;
  };

  const handleSelection = ({ name, email }) => {
    if (name && email) {
      const parent = parents.find(p => p.name === name && p.email === email);
      if (parent) selectParent(parent);
    }
  };
  
  handleAutocompleteKeyboardNavigation(
    elements.parentSearchInput,
    elements.parentAutocompleteResults,
    handleSelection,
    extractItemData
  );
}

function setupAssigneeSearchListeners() {
  const filterStaff = (query) => {
    const lowercaseQuery = query.toLowerCase();
    return staff.filter(s => 
      matchesWordBeginning(s.name, query) || 
      matchesWordBeginning(s.email, query)
    );
  };

  setupSearchInputListeners(
    elements.assigneeSearchInput,
    filterStaff,
    (results) => populateAutocompleteResults(
      elements.assigneeAutocompleteResults, 
      results, 
      'name', 
      'email', 
      selectNewTicketAssignee
    )
  );

  const extractItemData = (selectedItem) => {
    const nameEl = selectedItem.querySelector('.autocomplete-name');
    const emailEl = selectedItem.querySelector('.autocomplete-email');
    return nameEl && emailEl 
      ? { name: nameEl.textContent, email: emailEl.textContent }
      : null;
  };

  const handleSelection = ({ name, email }) => {
    if (name && email) {
      const assignee = staff.find(s => s.name === name && s.email === email);
      if (assignee) selectNewTicketAssignee(assignee);
    }
  };

  handleAutocompleteKeyboardNavigation(
    elements.assigneeSearchInput,
    elements.assigneeAutocompleteResults,
    handleSelection,
    extractItemData
  );

  elements.assigneeInfoDisplay.addEventListener('click', toggleAssigneeSearchMode);
  elements.assigneeEditIcon.addEventListener('click', toggleAssigneeSearchMode);
}

function setupAssigneeEditListeners() {
  const filterStaff = (query) => {
    const lowercaseQuery = query.toLowerCase();
    return staff.filter(s => 
      matchesWordBeginning(s.name, query) || 
      matchesWordBeginning(s.email, query)
    );
  };

  setupSearchInputListeners(
    elements.assigneeEditInput,
    filterStaff,
    (results) => populateAutocompleteResults(
      elements.assigneeEditAutocompleteResults, 
      results, 
      'name', 
      'email', 
      selectAssignee
    )
  );

  handleAutocompleteKeyboardNavigation(
    elements.assigneeEditInput,
    elements.assigneeEditAutocompleteResults,
    () => {},
    null
  );
}

function setupDocumentClickHandlers() {
  document.addEventListener('click', e => {
    if (!elements.parentSearchInput.contains(e.target) && 
        !elements.parentAutocompleteResults.contains(e.target)) {
      elements.parentAutocompleteResults.style.display = 'none';
    }
    
    if (!elements.assigneeSearchInput.contains(e.target) && 
        !elements.assigneeAutocompleteResults.contains(e.target)) {
      elements.assigneeAutocompleteResults.style.display = 'none';
    }
    
    const assigneeEditIcon = elements.assigneeInfoSection.querySelector('.edit-icon');
    if (!elements.assigneeEditInput.contains(e.target) && 
        !elements.assigneeEditAutocompleteResults.contains(e.target) &&
        !(assigneeEditIcon && assigneeEditIcon.contains(e.target))) {
      
      elements.assigneeEditAutocompleteResults.style.display = 'none';
      elements.assigneeEditContainer.style.display = 'none';
      
      const infoContainer = elements.assigneeInfoSection.querySelector('.info-container');
      if (infoContainer) infoContainer.style.display = 'flex';
    }
  });
}
