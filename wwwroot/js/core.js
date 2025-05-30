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
  assigneeEditIcon: document.getElementById('assignee-edit-icon')
};

const state = {
  currentTicketId: null,
  activeTab: 'open',
  timeUpdateInterval: null,
  conversation: [],
  activeParent: null,
  activeAssignee: null,
  activeEditAssignee: null
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
    renderTickets(state.activeTab);
    setupEventListeners();
    populateNewTicketForm();
    state.timeUpdateInterval = setInterval(updateAllElapsedTimes, 1000);
  } catch (error) {
    console.error('Failed to initialize the app:', error);
    await fetchUsers();
  }
}

document.addEventListener('DOMContentLoaded', init);