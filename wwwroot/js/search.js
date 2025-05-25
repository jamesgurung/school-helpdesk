// Search and Autocomplete Functionality
function selectAssignee(assignee) {
  state.activeEditAssignee = assignee;
  elements.assigneeEditInput.value = assignee.name;
  elements.assigneeEditAutocompleteResults.style.display = 'none';
  elements.assigneeEditContainer.style.display = 'none';
  updateTicket({ assigneeEmail: assignee.email, assigneeName: assignee.name, assigneeRole: assignee.role });
}

function selectNewTicketAssignee(assignee) {
  state.activeAssignee = assignee;
  elements.assigneeSearchInput.value = assignee.name;
  elements.assigneeNameDisplay.textContent = assignee.name;
  elements.assigneeNameDisplay.classList.remove('no-parent');
  elements.assigneeRoleDisplay.textContent = ` (${assignee.role})`;
  elements.assigneeAutocompleteResults.style.display = 'none';
  
  elements.assigneeSearchContainer.style.display = 'none';
  elements.assigneeInfoDisplay.style.display = 'flex';
  elements.assigneeEditIcon.style.display = 'inline-block';
}

function filterParents(query) {
  query = query.toLowerCase().trim();
  
  if (!query) return [];
  
  const exactMatch = parents.find(parent => 
    parent.name.toLowerCase() === query.toLowerCase()
  );
  
  if (exactMatch) {
    const otherMatches = parents.filter(parent => {
      if (parent.email === exactMatch.email) return false;
      const nameMatch = parent.name.toLowerCase().includes(query);
      const emailMatch = parent.email.toLowerCase().includes(query);
      return nameMatch || emailMatch;
    });
    
    return [exactMatch, ...otherMatches];
  }
  
  return parents.filter(parent => {
    const nameMatch = parent.name.toLowerCase().includes(query);
    const emailMatch = parent.email.toLowerCase().includes(query);
    return nameMatch || emailMatch;
  });
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
    
    item.innerHTML = `
      <div class="autocomplete-name">${parent.name}</div>
      <div class="autocomplete-email">${parent.email}</div>
    `;
    
    item.addEventListener('click', () => {
      selectParent(parent);
    });
    
    elements.parentAutocompleteResults.appendChild(item);
  });
  
  elements.parentAutocompleteResults.style.display = 'block';
  
  if (selectedItem) {
    setTimeout(() => {
      selectedItem.scrollIntoView({ block: 'nearest' });
    }, 0);
  }
}

function selectParent(parent) {
  state.activeParent = parent;
  elements.parentSearchInput.value = parent.name;
  elements.parentNameDisplay.textContent = parent.name;
  elements.parentNameDisplay.classList.remove('no-parent');
  elements.parentRelationshipDisplay.textContent = ` (${parent.relationship})`;
  elements.parentAutocompleteResults.style.display = 'none';
  
  elements.parentSearchContainer.style.display = 'none';
  elements.parentInfo.style.display = 'flex';
  
  document.getElementById('parent-edit-icon').style.display = 'inline-block';
  
  updateStudentOptions(parent.children);
}

function updateStudentOptions(children) {
  elements.studentSelectInput.innerHTML = '<option value="" disabled selected>Select a student</option>';
  
  if (!children || children.length === 0) {
    elements.studentSelectInput.disabled = true;
    return;
  }
  
  children.forEach(child => {
    const option = document.createElement('option');
    option.value = `${child.firstName}-${child.lastName}-${child.tutorGroup}`;
    option.textContent = `${child.firstName} ${child.lastName} (${child.tutorGroup})`;
    elements.studentSelectInput.appendChild(option);
  });
  
  elements.studentSelectInput.disabled = false;
}

function toggleParentSearchMode(e) {
  e?.preventDefault();
  
  const isInSearchMode = elements.parentSearchContainer.style.display !== 'none';
  const parentEditIcon = document.getElementById('parent-edit-icon');
  
  if (isInSearchMode) {
    if (state.activeParent) {
      elements.parentSearchContainer.style.display = 'none';
      elements.parentInfo.style.display = 'flex';
      parentEditIcon.style.display = 'inline-block';
    }
  } else {
    elements.parentSearchContainer.style.display = 'block';
    elements.parentInfo.style.display = 'none';
    
    if (state.activeParent) {
      elements.parentSearchInput.value = state.activeParent.name;
    }
    
    elements.parentSearchInput.focus();
    parentEditIcon.style.display = 'none';
    
    setTimeout(() => {
      if (elements.parentSearchInput.value.trim()) {
        const results = filterParents(elements.parentSearchInput.value);
        displayParentAutocompleteResults(results, state.activeParent);
      }
    }, 50);
  }
}

function toggleAssigneeSearchMode(e) {
  e?.preventDefault();
  
  const isInSearchMode = elements.assigneeSearchContainer.style.display !== 'none';
  
  if (isInSearchMode) {
    if (state.activeAssignee) {
      elements.assigneeSearchContainer.style.display = 'none';
      elements.assigneeInfoDisplay.style.display = 'flex';
      elements.assigneeEditIcon.style.display = 'inline-block';
    }
  } else {
    elements.assigneeSearchContainer.style.display = 'block';
    elements.assigneeInfoDisplay.style.display = 'none';
    
    if (state.activeAssignee) {
      elements.assigneeSearchInput.value = state.activeAssignee.name;
    }
    
    elements.assigneeSearchInput.focus();
    elements.assigneeEditIcon.style.display = 'none';
    
    setTimeout(() => {
      if (elements.assigneeSearchInput.value.trim()) {
        elements.assigneeSearchInput.dispatchEvent(new Event('input'));
      }
    }, 50);
  }
}
