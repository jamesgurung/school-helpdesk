function hideSearchElements(itemName, inputEl, autocompleteEl, containerEl) {
  if (inputEl) inputEl.value = itemName;
  if (autocompleteEl) autocompleteEl.style.display = 'none';
  if (containerEl) containerEl.style.display = 'none';
}

function confirmSelectionAndShowDetails(item, config) {
  config.activeStateSetter(item);

  if (config.searchInputWithValue) config.searchInputWithValue.value = item[config.nameProperty];
  if (config.nameDisplay) {
    config.nameDisplay.textContent = item[config.nameProperty];
    if (config.nameDisplayClassToRemove) config.nameDisplay.classList.remove(config.nameDisplayClassToRemove);
  }

  if (config.autocompleteResults) config.autocompleteResults.style.display = 'none';
  if (config.searchContainer) config.searchContainer.style.display = 'none';
  if (config.infoDisplay) config.infoDisplay.style.display = config.infoDisplayType || 'flex';
  if (config.editIcon) config.editIcon.style.display = 'inline-block';
}

function toggleSearchDisplayMode(e, config) {
  e?.preventDefault();

  const {
    searchContainer,
    infoDisplay,
    editIcon,
    searchInput,
    activeItem,
    activeItemNameProperty,
    onActivateSearch,
    infoDisplayType
  } = config;

  const isInSearchMode = searchContainer.style.display !== 'none';

  if (isInSearchMode) {
    if (activeItem) {
      searchContainer.style.display = 'none';
      infoDisplay.style.display = infoDisplayType || 'flex';
      editIcon.style.display = 'inline-block';
    }
  } else {
    searchContainer.style.display = 'block';
    infoDisplay.style.display = 'none';
    if (activeItem) {
      searchInput.value = activeItem[activeItemNameProperty];
    }
    searchInput.focus();
    editIcon.style.display = 'none';
    if (onActivateSearch) {
      onActivateSearch(searchInput, activeItem);
    }
  }
}

function parentMatchesQuery(parent, queryLC, matchesWordBeginningFn) {
  const nameMatch = matchesWordBeginningFn(parent.name, queryLC);
  const emailMatch = matchesWordBeginningFn(parent.email, queryLC);
  const childrenMatch = parent.children && parent.children.some(child => {
    const fullName = `${child.firstName} ${child.lastName}`;
    return matchesWordBeginningFn(fullName, queryLC) ||
      matchesWordBeginningFn(child.firstName, queryLC) ||
      matchesWordBeginningFn(child.lastName, queryLC);
  });
  return nameMatch || emailMatch || childrenMatch;
}

function selectAssignee(assignee) {
  state.activeEditAssignee = assignee;
  hideSearchElements(assignee.name, elements.assigneeEditInput, elements.assigneeEditAutocompleteResults, elements.assigneeEditContainer);
  updateTicketAssignee();
}

function selectNewTicketAssignee(assignee) {
  confirmSelectionAndShowDetails(assignee, {
    activeStateSetter: (a) => { state.activeAssignee = a; },
    searchInputWithValue: elements.assigneeSearchInput,
    nameDisplay: elements.assigneeNameDisplay,
    nameDisplayClassToRemove: 'no-parent',
    autocompleteResults: elements.assigneeAutocompleteResults,
    searchContainer: elements.assigneeSearchContainer,
    infoDisplay: elements.assigneeInfoDisplay,
    editIcon: elements.assigneeEditIcon,
    nameProperty: 'name',
    infoDisplayType: 'flex'
  });
}

function filterParents(query) {
  const queryLC = query.toLowerCase().trim();
  if (!queryLC) return [];

  const allMatchedParents = parents.filter(parent => parentMatchesQuery(parent, queryLC, matchesWordBeginning));

  const exactMatchIndex = allMatchedParents.findIndex(parent =>
    parent.name.toLowerCase() === queryLC
  );

  if (exactMatchIndex > -1) {
    const exactMatch = allMatchedParents.splice(exactMatchIndex, 1)[0];
    return [exactMatch, ...allMatchedParents];
  }
  return allMatchedParents;
}

function displayParentAutocompleteResults(results, selectedParent = null) {
  elements.parentAutocompleteResults.innerHTML = '';
  if (results.length === 0) {
    elements.parentAutocompleteResults.style.display = 'none';
    return;
  }

  let selectedItem = null;
  const query = elements.parentSearchInput.value.toLowerCase().trim();

  results.forEach(parent => {
    const item = document.createElement('div');
    item.className = 'autocomplete-item';
    if (selectedParent && parent.email === selectedParent.email) {
      item.classList.add('selected');
      selectedItem = item;
    }

    let childrenInfo = 'No children';
    if (parent.children && parent.children.length > 0) {
      childrenInfo = parent.children.map(child => {
        const fullName = `${child.firstName} ${child.lastName}`;
        const childNameMatchesQuery = query && (
          matchesWordBeginning(fullName, query) ||
          matchesWordBeginning(child.firstName, query) ||
          matchesWordBeginning(child.lastName, query)
        );
        return `${childNameMatchesQuery ? '<strong>' : ''}${fullName}${childNameMatchesQuery ? '</strong>' : ''} (${child.tutorGroup})`;
      }).join(', ');
    }

    item.innerHTML = `
      <div class="autocomplete-name">${parent.name}</div>
      <div class="autocomplete-email">${parent.email} - ${childrenInfo}</div>
    `;
    item.addEventListener('click', () => selectParent(parent));
    elements.parentAutocompleteResults.appendChild(item);
  });

  elements.parentAutocompleteResults.style.display = 'block';
  if (selectedItem) {
    setTimeout(() => selectedItem.scrollIntoView({ block: 'nearest' }), 0);
  }
}

function focusNextInputAfterParentSelection(parent) {
  if (parent.children && parent.children.length === 1) {
    setTimeout(() => elements.ticketTitleFormInput.focus(), 50);
  } else if (parent.children && parent.children.length > 1) {
    setTimeout(() => elements.studentSelectInput.focus(), 50);
  }
}

function selectParent(parent) {
  confirmSelectionAndShowDetails(parent, {
    activeStateSetter: (p) => { state.activeParent = p; },
    searchInputWithValue: elements.parentSearchInput,
    nameDisplay: elements.parentNameDisplay,
    nameDisplayClassToRemove: 'no-parent',
    autocompleteResults: elements.parentAutocompleteResults,
    searchContainer: elements.parentSearchContainer,
    infoDisplay: elements.parentInfo,
    editIcon: document.getElementById('parent-edit-icon'),
    nameProperty: 'name',
    infoDisplayType: 'flex'
  });

  updateStudentOptions(parent.children);
  if (parent.children && parent.children.length === 1) {
    const child = parent.children[0];
    const studentValue = `${child.firstName}|${child.lastName}|${child.tutorGroup}`;
    elements.studentSelectInput.value = studentValue;
    updateParentRelationshipDisplay(child.parentRelationship);
    setTimeout(() => {
      elements.studentSelectInput.classList.add('auto-selected');
      setTimeout(() => elements.studentSelectInput.classList.remove('auto-selected'), 1000);
    }, 0);
  }
  focusNextInputAfterParentSelection(parent);
}

function updateStudentOptions(children) {
  elements.studentSelectInput.innerHTML = '<option value="" disabled selected>Select a student</option>';
  updateParentRelationshipDisplay('');

  if (!children || children.length === 0) {
    elements.studentSelectInput.disabled = true;
    return;
  } children.forEach(child => {
    const option = document.createElement('option');
    option.value = `${child.firstName}|${child.lastName}|${child.tutorGroup}`;
    option.textContent = `${child.firstName} ${child.lastName} (${child.tutorGroup})`;
    elements.studentSelectInput.appendChild(option);
  });
  elements.studentSelectInput.disabled = false;
}

function toggleParentSearchMode(e) {
  toggleSearchDisplayMode(e, {
    searchContainer: elements.parentSearchContainer,
    infoDisplay: elements.parentInfo,
    editIcon: document.getElementById('parent-edit-icon'),
    searchInput: elements.parentSearchInput,
    activeItem: state.activeParent,
    activeItemNameProperty: 'name',
    infoDisplayType: 'flex',
    onActivateSearch: (input, activeItem) => {
      setTimeout(() => {
        if (input.value.trim()) {
          const results = filterParents(input.value);
          displayParentAutocompleteResults(results, activeItem);
        }
      }, 50);
    }
  });
}

function toggleAssigneeSearchMode(e) {
  toggleSearchDisplayMode(e, {
    searchContainer: elements.assigneeSearchContainer,
    infoDisplay: elements.assigneeInfoDisplay,
    editIcon: elements.assigneeEditIcon,
    searchInput: elements.assigneeSearchInput,
    activeItem: state.activeAssignee,
    activeItemNameProperty: 'name',
    infoDisplayType: 'flex',
    onActivateSearch: (input) => {
      setTimeout(() => {
        if (input.value.trim()) {
          input.dispatchEvent(new Event('input'));
        }
      }, 50);
    }
  });
}
