const elements = {
  ticketsContainer: document.getElementById('tickets-container'),
  ticketDetails: document.getElementById('ticket-details'),
  detailsEmpty: document.querySelector('.details-empty'),
  detailsContent: document.querySelector('.details-content'),
  mobileBack: document.getElementById('back-button'),
  tabs: document.querySelectorAll('.tab'),
  ticketTitleInput: document.getElementById('ticket-title'),
  studentSelect: document.getElementById('student-select'),
  assigneeSelect: document.getElementById('assignee-select'),
  parentInfoSection: document.getElementById('parent-info-section'),
  studentInfoSection: document.getElementById('student-info-section'),
  assigneeInfoSection: document.getElementById('assignee-info-section'),
  assigneeEditContainer: document.getElementById('assignee-edit-container'),
  assigneeEditInput: document.getElementById('assignee-edit-input'),
  assigneeEditAutocompleteResults: document.getElementById('assignee-edit-autocomplete-results'),
  conversationContainer: document.getElementById('conversation'),
  newMessageInput: document.getElementById('new-message'),
  sendMessageBtn: document.getElementById('send-message'),
  closeTicketBtn: document.getElementById('close-ticket'),
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
  assigneeRoleDisplay: document.getElementById('assignee-role-display'),
  assigneeEditIcon: document.getElementById('assignee-edit-icon')
};

const state = {
  activeTicket: null,
  activeTab: 'open',
  activeTicketMessages: [],
  activeTicketChildren: [],
  timeUpdateInterval: null,
  parentInfo: null,
  activeParent: null,
  activeAssignee: null,
  activeEditAssignee: null
};

const customConversations = {};

function init() {
  populateSelectOptions();
  renderTickets(state.activeTab);
  setupEventListeners();
  updateBackButtonIcon();
  populateNewTicketForm();
  window.addEventListener('resize', updateBackButtonIcon);
  state.timeUpdateInterval = setInterval(updateAllElapsedTimes, 1000);
}

document.addEventListener('DOMContentLoaded', init);