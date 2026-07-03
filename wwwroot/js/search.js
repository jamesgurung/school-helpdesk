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

function parentMatchesQuery(parent, query) {
  if (!query) return false;
  
  if (matchesWordBeginning(parent.name, query) || matchesWordBeginning(parent.email, query)) {
    return true;
  }

  if (parent.phone?.replace(/\s/g, '').startsWith(query.replace(/\s/g, ''))) {
    return true;
  }
  
  return parent.children && parent.children.some(child => {
    const fullName = `${child.firstName} ${child.lastName}`;
    return matchesWordBeginning(fullName, query) ||
      matchesWordBeginning(child.firstName, query) ||
      matchesWordBeginning(child.lastName, query);
    });
}

function normalisePhone(phone) {
  return (phone || '').replace(/\s/g, '');
}

function mergeTickets(loadedTickets) {
  loadedTickets.forEach(ticket => {
    const existing = tickets.find(t => t.id === ticket.id);
    if (existing) {
      Object.assign(existing, ticket);
    } else {
      tickets.push(ticket);
    }
  });
  tickets.sort((a, b) => Date.parse(b.lastUpdated) - Date.parse(a.lastUpdated));
}

async function loadAllTickets() {
  if (state.allTicketsLoaded) return;
  if (state.allTicketsLoading) return await state.allTicketsLoading;

  state.allTicketsLoading = (async () => {
    const loadedTickets = await apiGetAllTickets();
    mergeTickets(loadedTickets);
    state.allTicketsLoaded = true;
    updateOpenTicketsBadge();
  })();
  updateTicketSearchPanel();

  try {
    await state.allTicketsLoading;
  } finally {
    state.allTicketsLoading = null;
    updateTicketSearchPanel();
  }
}

function ticketMatchesActiveSearch(ticket) {
  const search = state.activeTicketSearch;
  if (!search) return false;

  if (search.type === 'id') return ticket.id === search.value;
  if (search.type === 'parent') return getParentSearchValue(ticket) === search.value;
  if (search.type === 'student') return getStudentSearchValue(ticket) === search.value;
  if (search.type === 'assignee') return getAssigneeSearchValue(ticket) === search.value;
  if (search.type === 'title') return normalise(ticket.title || '').includes(search.value);

  return false;
}

function addTicketSearchOption(options, seen, type, value, name, detail, inputValue = name) {
  if (!value) return;
  const key = `${type}:${value}`;
  if (seen.has(key)) return;
  seen.add(key);
  options.push({ type, value, name, detail, inputValue });
}

function getStudentName(ticket) {
  return getFullName(ticket.studentFirstName || '', ticket.studentLastName || '').trim();
}

function getStudentSearchValue(ticket) {
  return [ticket.studentFirstName, ticket.studentLastName, ticket.tutorGroup].map(o => normalise(o || '')).join('|');
}

function getStudentDetail(ticket) {
  const name = getStudentName(ticket);
  return `${name || 'Not Set'}${ticket.tutorGroup ? ` (${ticket.tutorGroup})` : ''}`;
}

function getParentSearchValue(ticket) {
  return [ticket.parentName, ticket.parentEmail].map(o => normalise(o || '')).join('|');
}

function getAssigneeSearchValue(ticket) {
  return [ticket.assigneeName, ticket.assigneeEmail].map(o => normalise(o || '')).join('|');
}

function getParentStudents(parentValue) {
  return tickets
    .filter(ticket => getParentSearchValue(ticket) === parentValue)
    .map(getStudentName)
    .filter(Boolean)
    .filter((student, index, students) => students.indexOf(student) === index)
    .join(', ');
}

function filterTicketSearchOptions(query) {
  const rawQuery = query.trim();
  const normalisedQuery = normalise(rawQuery);
  const idQuery = rawQuery.replace(/^#/, '');
  const phoneQuery = normalisePhone(rawQuery);
  const isExplicitTicketNumberQuery = rawQuery.startsWith('#');
  const isTicketNumberQuery = /^\d+$/.test(idQuery);
  if (!normalisedQuery && !idQuery) return [];

  const maxOptions = 10;
  const options = [];
  const seen = new Set();

  if (isTicketNumberQuery) {
    for (const ticket of tickets) {
      const id = parseInt(ticket.id);
      if (String(id).startsWith(idQuery)) {
        addTicketSearchOption(options, seen, 'id', ticket.id, `#${id} ${ticket.title || ''}`.trim(), `Ticket for ${getStudentDetail(ticket)}`, `#${id}`);
        if (options.length >= maxOptions) break;
      }
    }
    if (isExplicitTicketNumberQuery || options.length >= maxOptions || normalisedQuery.length < 3) return options;
  }

  if (normalisedQuery.length < 3) return [];

  for (const ticket of tickets) {
    const studentName = getStudentName(ticket);
    if (matchesWordBeginning(studentName, normalisedQuery)) {
      addTicketSearchOption(options, seen, 'student', getStudentSearchValue(ticket), studentName, `Student in ${ticket.tutorGroup || 'unknown tutor group'}`);
      if (options.length >= maxOptions) break;
    }

    if (matchesWordBeginning(ticket.parentName, normalisedQuery) ||
      matchesWordBeginning(ticket.parentEmail, normalisedQuery) ||
      (phoneQuery && normalisePhone(ticket.parentPhone).startsWith(phoneQuery))) {
      const parentValue = getParentSearchValue(ticket);
      addTicketSearchOption(options, seen, 'parent', parentValue, ticket.parentName, `Parent of ${getParentStudents(parentValue) || 'no students'}`);
      if (options.length >= maxOptions) break;
    }

    if (ticket.assigneeName && (matchesWordBeginning(ticket.assigneeName, normalisedQuery) ||
      matchesWordBeginning(ticket.assigneeEmail, normalisedQuery))) {
      addTicketSearchOption(options, seen, 'assignee', getAssigneeSearchValue(ticket), ticket.assigneeName, 'Tickets assigned to staff member');
      if (options.length >= maxOptions) break;
    }
  }

  if (options.length < maxOptions && tickets.some(ticket => normalise(ticket.title || '').includes(normalisedQuery))) {
    addTicketSearchOption(options, seen, 'title', normalisedQuery, `Title containing "${rawQuery}"`, 'Ticket search', rawQuery);
  } else if (options.length === 0) {
    addTicketSearchOption(options, seen, 'title', normalisedQuery, `Title containing "${rawQuery}"`, 'No matches', rawQuery);
    options[0].detailClass = 'autocomplete-error';
  }

  return options;
}

function displayTicketSearchAutocompleteResults(results) {
  elements.ticketSearchAutocompleteResults.innerHTML = '';
  if (results.length === 0) {
    elements.ticketSearchAutocompleteResults.style.display = 'none';
    return;
  }

  results.forEach(result => {
    const item = document.createElement('div');
    item.className = 'autocomplete-item';

    const name = document.createElement('div');
    name.className = 'autocomplete-name';
    name.textContent = result.name;

    const detail = document.createElement('div');
    detail.className = 'autocomplete-email';
    if (result.detailClass) detail.classList.add(result.detailClass);
    detail.textContent = result.detail;

    item.append(name, detail);
    item.addEventListener('click', () => selectTicketSearch(result));
    elements.ticketSearchAutocompleteResults.appendChild(item);
  });

  elements.ticketSearchAutocompleteResults.style.display = 'block';
}

function selectTicketSearch(search, openIdTicket = true) {
  state.activeTicketSearch = search;
  elements.ticketSearchInput.value = search.inputValue;
  elements.ticketSearchAutocompleteResults.style.display = 'none';
  renderTickets('search');

  if (openIdTicket && search.type === 'id') {
    confirmNavigationWithUnsentText('switch to another ticket', () => {
      openTicketDetails(search.value);
    });
  }
}

async function openTicketFromSearch(ticketId) {
  const preloadedTicket = tickets.find(t => t.id === ticketId);
  const preloadedTab = preloadedTicket && (!preloadedTicket.isClosed ? 'open' : isRecentClosedTicket(preloadedTicket) ? 'closed' : null);
  if (preloadedTab) {
    await activateTicketsTab(preloadedTab, false, false);
    document.querySelector(`.ticket-item[data-id="${ticketId}"]`)?.scrollIntoView({ block: 'nearest' });
    openTicketDetails(ticketId, true);
    history.replaceState(null, '', `/tickets/${parseInt(ticketId)}`);
    return;
  }

  await activateTicketsTab('search', false, false);
  const ticket = tickets.find(t => t.id === ticketId);
  const search = {
    type: 'id',
    value: ticketId,
    name: ticket ? `#${parseInt(ticketId)} ${ticket.title || ''}`.trim() : `#${parseInt(ticketId)}`,
    inputValue: `#${parseInt(ticketId)}`,
    detail: ticket ? `Ticket for ${getStudentDetail(ticket)}` : 'Ticket for Not Set'
  };
  selectTicketSearch(search, false);

  if (!ticket) {
    history.replaceState(null, '', '/tickets/');
    showToast('This ticket is no longer accessible.', 'error');
    return;
  }

  openTicketDetails(ticketId, true);
  history.replaceState(null, '', `/tickets/${parseInt(ticketId)}`);
}

function setupTicketSearchListeners() {
  elements.ticketSearchInput.addEventListener('input', e => {
    state.activeTicketSearch = null;
    renderTickets('search');
    displayTicketSearchAutocompleteResults(filterTicketSearchOptions(e.target.value));
  });
  elements.ticketSearchInput.addEventListener('focus', e => setTimeout(() => {
    const value = e.target.value.trim();
    if (value) displayTicketSearchAutocompleteResults(filterTicketSearchOptions(value));
  }, 50));
  elements.ticketSearchInput.addEventListener('keydown', e => {
    if (e.key !== 'Enter' || elements.ticketSearchAutocompleteResults.style.display === 'none') return;
    const items = elements.ticketSearchAutocompleteResults.querySelectorAll('.autocomplete-item');
    if (items.length !== 1) return;
    e.preventDefault();
    items[0].click();
  });
  handleAutocompleteKeyboardNavigation(elements.ticketSearchInput, elements.ticketSearchAutocompleteResults, () => { }, null);
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
  query = normalise(query);
  if (!query) return [];

  const allMatchedParents = parents.filter(parent => parentMatchesQuery(parent, query));

  const exactMatchIndex = allMatchedParents.findIndex(parent =>
    parent.name.toLowerCase() === query
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
        return `${fullName} (${child.tutorGroup})`;
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
