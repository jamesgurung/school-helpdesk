const elements = {
  ticketsContainer: document.getElementById('tickets-container'),
  ticketDetails: document.getElementById('ticket-details'),
  detailsEmpty: document.querySelector('.details-empty'),
  detailsContent: document.querySelector('.details-content'),
  backBtn: document.getElementById('back-button'),
  tabs: document.querySelectorAll('.tab'),
  ticketTitleInput: document.getElementById('ticket-title'),
  studentSelect: document.getElementById('student-select'),
  parentSelect: document.getElementById('parent-select'),
  parentInfoSection: document.getElementById('parent-info-section'),
  studentInfoSection: document.getElementById('student-info-section'),
  assigneeInfoSection: document.getElementById('assignee-info-section'),
  assigneeEditContainer: document.getElementById('assignee-edit-container'),
  assigneeEditInput: document.getElementById('assignee-edit-input'),
  assigneeEditAutocompleteResults: document.getElementById('assignee-edit-autocomplete-results'),
  conversationContainer: document.getElementById('conversation'),
  newMessageInput: document.getElementById('new-message'),
  messageAttachments: document.getElementById('message-attachments'),
  attachmentList: document.getElementById('attachment-list'),
  internalNoteCheckbox: document.getElementById('internal-note'),
  uploadFilesBtn: document.querySelector('.file-upload-label'),
  sendMessageBtn: document.getElementById('send-message'),
  closeTicketBtn: document.getElementById('close-ticket'),
  logoutBtn: document.getElementById('logout-button'),
  newTicketButton: document.getElementById('new-ticket-button'),
  newTicketModal: document.getElementById('new-ticket-modal'),
  closeModalBtn: document.querySelector('.close-modal'),
  cancelNewTicketBtn: document.getElementById('cancel-new-ticket'),
  createNewTicketBtn: document.getElementById('create-new-ticket'),
  newTicketForm: document.getElementById('new-ticket-form'),
  studentSelectInput: document.getElementById('student-select-input'),
  ticketTitleFormInput: document.getElementById('ticket-title-input'),
  messageInput: document.getElementById('message-input'),
  parentNameDisplay: document.getElementById('parent-name-display'),
  parentRelationshipDisplay: document.getElementById('parent-relationship-display'),
  parentSearchInput: document.getElementById('parent-search-input'),
  parentSearchContainer: document.getElementById('parent-search-container'),
  parentAutocompleteResults: document.getElementById('parent-autocomplete-results'),
  assigneeSearchContainer: document.getElementById('assignee-search-container'),
  assigneeSearchInput: document.getElementById('assignee-search-input'),
  assigneeAutocompleteResults: document.getElementById('assignee-autocomplete-results'),
  parentInfo: document.getElementById('parent-info'),
  assigneeInfoDisplay: document.getElementById('assignee-info'),
  assigneeNameDisplay: document.getElementById('assignee-name-display'),
  assigneeEditIcon: document.getElementById('assignee-edit-icon'),
  originalEmailModal: document.getElementById('original-email-modal'),
  originalEmailIframe: document.getElementById('original-email-frame'),
  originalEmailMetadata: document.getElementById('original-email-metadata'),
  originalEmailTo: document.getElementById('original-email-to'),
  originalEmailCc: document.getElementById('original-email-cc'),
  originalEmailSubject: document.getElementById('original-email-subject'),
  salutation: document.getElementById('salutation'),
  valediction: document.getElementById('valediction'),
  suggestStart: document.getElementById('suggest-start'),
  suggestModal: document.getElementById('suggest-modal'),
  guidanceInput: document.getElementById('guidance'),
  generateSuggestBtn: document.getElementById('generate-suggest'),
  generatedResponse: document.getElementById('generated-response'),
  suggestResponseSection: document.getElementById('suggest-response-section'),
  cancelSuggestBtn: document.getElementById('cancel-suggest'),
  insertSuggestBtn: document.getElementById('insert-suggest'),
  openBadge: document.getElementById('open-badge')
};

const state = {
  currentTicketId: null,
  activeTab: 'open',
  timeUpdateInterval: null,
  pollingInterval: null,
  conversation: [],
  activeParent: null,
  activeAssignee: null,
  activeEditAssignee: null,
  updating: false
};

function getCurrentTicket() {
  return tickets.find(ticket => ticket.id === state.currentTicketId);
}

let parents;
let staff;

function openDatabase() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open('helpdesk', 1);

    request.onerror = event => reject(event.target.error);

    request.onupgradeneeded = event => {
      const db = event.target.result;
      if (!db.objectStoreNames.contains('users')) {
        db.createObjectStore('users');
      }
    };

    request.onsuccess = event => resolve(event.target.result);
  });
}

async function getUsersData() {
  try {
    const db = await openDatabase();
    const transaction = db.transaction('users', 'readonly');
    const store = transaction.objectStore('users');

    const parentsRequest = store.get('parents');
    const staffRequest = store.get('staff');
    const hashRequest = store.get('hash');

    return new Promise((resolve, reject) => {
      transaction.oncomplete = () => {
        resolve({
          parents: parentsRequest.result,
          staff: staffRequest.result,
          usersHash: hashRequest.result
        });
      };
      transaction.onerror = event => reject(event.target.error);
    });
  } catch (error) {
    console.error('Failed to get users data:', error);
    return { parents: null, staff: null, usersHash: null };
  }
}

async function fetchUsers() {
  const response = await fetch('/api/users');
  const users = await response.json();

  parents = users.parents;
  staff = users.staff;

  try {
    const db = await openDatabase();
    const transaction = db.transaction('users', 'readwrite');
    const store = transaction.objectStore('users');

    store.put(parents, 'parents');
    store.put(staff, 'staff');
    store.put(usersHash, 'hash');

    return new Promise((resolve, reject) => {
      transaction.oncomplete = () => resolve(true);
      transaction.onerror = event => reject(event.target.error);
    });
  } catch (error) {
    console.error('Failed to store users data:', error);
    return false;
  }
}

async function init() {
  try {
    if (isManager) {
      const storedData = await getUsersData();
      if (storedData?.usersHash === usersHash) {
        parents = storedData.parents;
        staff = storedData.staff;
      } else {
        await fetchUsers();
      }
    }
    initHolidays();
    renderTickets(state.activeTab);
    updateOpenTicketsBadge();
    setupEventListeners();
    populateNewTicketForm();
    state.timeUpdateInterval = setInterval(updateAllElapsedTimes, 1000);
    elements.valediction.querySelector('span').textContent = getSalutation(currentUser);
    fromHash();
  } catch (error) {
    console.error('Failed to initialize the app:', error);
    await fetchUsers();
  }
}

document.addEventListener('DOMContentLoaded', init);
window.addEventListener('hashchange', fromHash);

async function fromHash() {
  const { hash } = window.location;
  if (hash.length >= 10 && hash.startsWith('#tickets/') && /^\d+$/.test(hash.slice(9))) {
    const ticketId = hash.slice(9).padStart(6, '0');
    const ticket = tickets.find(t => t.id === ticketId);
    if (ticket) {
      openTicketDetails(ticketId, true);
      return;
    } else if (isManager && !window.location.search.startsWith('?archive')) {
      window.location = '/?archive/#tickets/' + hash.slice(9);
      return;
    }
  }
  history.replaceState(null, '', '/' + window.location.search);
}