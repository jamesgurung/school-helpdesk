// Conversation and Messaging
function renderConversation() {
  const currentTicket = getCurrentTicket();
  if (!currentTicket || !state.conversation) return;

  elements.conversationContainer.innerHTML = '';

  state.conversation.forEach((message, index) => {
    const messageClone = document.getElementById('message-template').content.cloneNode(true);
    const messageElement = messageClone.querySelector('.message');
    const messageIconElement = messageClone.querySelector('.message-icon');
    const authorNameElement = messageClone.querySelector('.author-name');

    const isOnBehalfOfParent = message.isEmployee && index === 0;
    const isEmployee = message.isEmployee && index > 0;
    const config = {
      class: isEmployee ? 'employee' : 'parent',
      icon: isOnBehalfOfParent ? 'support_agent' : (isEmployee ? 'school' : 'person'),
      iconColorVar: isEmployee ? '--primary-dark' : '--secondary-dark',
      colorVar: isEmployee ? '--primary-dark' : '--secondary-dark'
    };
    messageElement.classList.add(config.class);
    messageIconElement.textContent = config.icon;
    messageIconElement.style.color = `var(${config.iconColorVar})`;
    authorNameElement.style.color = `var(${config.colorVar})`;

    if (message.isPrivate) {
      messageElement.style.backgroundColor = 'var(--internal-note-bg)';
    }

    const content = isOnBehalfOfParent ? `${message.content}<p class="reply-note">Note that you are replying directly to the parent/carer.</p>` : message.content;

    authorNameElement.innerHTML = isOnBehalfOfParent ? `${message.authorName} <span>on behalf of</span> ${currentTicket.parentName}` : message.authorName;
    messageClone.querySelector('.message-date').textContent = formatDateTime(message.timestamp);
    messageClone.querySelector('.message-content').innerHTML = content;

    if (message.attachments?.length) {
      renderMessageAttachments(messageElement, message.attachments);
    }

    elements.conversationContainer.appendChild(messageClone);
  });
}

function renderMessageAttachments(messageElement, attachments) {
  const container = document.createElement('div');
  container.className = 'message-attachments';

  attachments.forEach(attachment => {
    const link = document.createElement('a');
    link.href = attachment.url;
    link.className = 'attachment-link';
    link.target = '_blank';

    const attachmentEl = document.createElement('div');
    attachmentEl.className = 'attachment';

    const icon = document.createElement('span');
    icon.className = 'material-symbols-rounded';
    icon.textContent = 'attachment';

    const fileName = document.createElement('span');
    fileName.textContent = attachment.fileName;

    attachmentEl.append(icon, fileName);
    link.appendChild(attachmentEl);
    container.appendChild(link);
  });

  messageElement.appendChild(container);
}

async function sendMessage() {
  const currentTicket = getCurrentTicket();
  if (!currentTicket || !elements.newMessageInput.value.trim()) return;

  const assigneeStaff = staff.find(s => s.email === currentTicket.assigneeEmail);

  if (!assigneeStaff) {
    showToast('Please assign this ticket to a staff member before sending a message.', 'error');
    return;
  }

  const isPrivate = elements.internalNoteCheckbox.checked;
  const messageContent = elements.newMessageInput.value.trim();

  elements.sendMessageBtn.disabled = true;
  elements.sendMessageBtn.textContent = 'Sending...';

  try {
    await apiSendMessage(currentTicket.id, currentTicket.assigneeEmail, messageContent, isPrivate);

    const newMessage = {
      timestamp: new Date().toISOString(),
      authorName: currentUser,
      isEmployee: true,
      content: messageContent,
      isPrivate: isPrivate,
      attachments: []
    };
    state.conversation.push(newMessage);
    elements.newMessageInput.value = '';
    elements.internalNoteCheckbox.checked = false;
    elements.newMessageInput.classList.remove('internal-note');
    renderConversation();
    elements.ticketDetails.scrollTop = elements.ticketDetails.scrollHeight;
    updateCloseTicketButtonText();

    showToast(isPrivate ? 'Internal note added successfully' : 'Message sent successfully', 'success');
  } finally {
    elements.sendMessageBtn.disabled = false;
    elements.sendMessageBtn.textContent = 'Send Message';
  }
}
