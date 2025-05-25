// Conversation and Messaging
function renderConversation() {
  if (!state.activeTicket || !state.conversation) return;
  
  elements.conversationContainer.innerHTML = '';
  
  state.conversation.forEach((message, index) => {
    const messageClone = document.getElementById('message-template').content.cloneNode(true);
    const messageElement = messageClone.querySelector('.message');
    const messageIconElement = messageClone.querySelector('.message-icon');
    const authorNameElement = messageClone.querySelector('.author-name');
    
    const isEmployee = message.isEmployee;
    const config = {
      class: isEmployee ? 'employee' : 'parent',
      icon: isEmployee ? (index === 0 ? 'support_agent' : 'school') : 'person',
      colorVar: isEmployee ? '--primary' : '--secondary',
      colorDarkVar: isEmployee ? '--primary-dark' : '--secondary-dark'
    };
    
    messageElement.classList.add(config.class);
    messageIconElement.textContent = config.icon;
    messageIconElement.style.color = `var(${config.colorVar})`;
    authorNameElement.style.color = `var(${config.colorDarkVar})`;
    
    authorNameElement.textContent = message.authorName;
    messageClone.querySelector('.message-date').textContent = formatDateTime(message.timestamp);
    messageClone.querySelector('.message-content').textContent = message.content;
    
    if (message.attachments?.length) {
      renderMessageAttachments(messageElement, message.attachments);
    }
    
    elements.conversationContainer.appendChild(messageClone);
  });
  
  elements.conversationContainer.scrollTop = elements.conversationContainer.scrollHeight;
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

function sendMessage() {
  if (!state.activeTicket || !elements.newMessageInput.value.trim()) return;
  
  const assigneeStaff = staff.find(s => s.email === state.activeTicket.assigneeEmail);
  
  // If no assignee is selected, alert the user and don't send the message
  if (!assigneeStaff) {
    alert('Please assign this ticket to a staff member before sending a message.');
    return;
  }
  
  const newMessage = {
    timestamp: new Date().toISOString(),
    authorEmail: assigneeStaff.email,
    authorName: assigneeStaff.name,
    isEmployee: true,
    content: elements.newMessageInput.value.trim(),
    attachments: []
  };
  
  state.conversation.push(newMessage);
  updateTicket();
  
  elements.newMessageInput.value = '';
  renderConversation();
}
