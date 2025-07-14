function renderConversation() {
  const ticket = getCurrentTicket();
  if (!ticket || !state.conversation) return;
  elements.conversationContainer.innerHTML = '';
  const template = document.getElementById('message-template').content;

  state.conversation.forEach((msg, i) => {
    const { isEmployee, isPrivate, authorName, timestamp, content, attachments, originalEmail } = msg;
    
    if (content === '#close' || content === '#reopen' || content.startsWith('#assign ')) {
      const eventEl = document.createElement('div');
      eventEl.className = 'message-header ticket-event';
      
      let eventText, icon;
      if (content === '#close') {
        eventText = `${authorName} <span>closed this ticket</span>`;
        icon = 'check_circle';
      } else if (content === '#reopen') {
        eventText = `${authorName} <span>reopened this ticket</span>`;
        icon = 'arrow_circle_down';
      } else if (content.startsWith('#assign ')) {
        const assigneeName = content.substring(8);
        eventText = `<span>Assigned to</span> ${assigneeName}`;
        icon = 'assignment_ind';
      }
      
      eventEl.innerHTML = `<div class="message-author"><div class="message-icon material-symbols-rounded">${icon}</div><div class="author-name">${eventText}</div></div><div class="message-date">${formatDateTime(timestamp)}</div>`;
      elements.conversationContainer.appendChild(eventEl);
      return;
    }

    const isOnBehalf = isEmployee && i === 0;
    const isEmp = isEmployee && i > 0;
    const cls = isEmp ? 'employee' : 'parent';
    const iconName = isOnBehalf ? 'support_agent' : isEmp ? 'account_circle' : 'supervisor_account';
    const colorVar = isEmp ? '--primary-dark' : '--secondary-dark';

    const clone = template.cloneNode(true);
    const el = clone.querySelector('.message');
    el.classList.add(cls);
    if (isPrivate) el.style.backgroundColor = 'var(--internal-note-bg)';

    const icon = clone.querySelector('.message-icon');
    icon.textContent = iconName;
    icon.style.color = `var(${colorVar})`;

    const authorEl = clone.querySelector('.author-name');
    authorEl.style.color = `var(${colorVar})`;
    authorEl.innerHTML = isOnBehalf
      ? `${authorName} <span>on behalf of</span> ${ticket.parentName ?? 'the parent/carer'}`
      : authorName;

    clone.querySelector('.message-date').textContent = formatDateTime(timestamp);
    
    if (originalEmail) {
      const dateContainer = clone.querySelector('.message-date');
      const emailIcon = document.createElement('span');
      emailIcon.className = 'material-symbols-rounded original-email-icon';
      emailIcon.textContent = 'email';
      emailIcon.title = 'View original email';
      emailIcon.onclick = () => showOriginalEmailModal(originalEmail);
      dateContainer.insertAdjacentElement('afterbegin', emailIcon);
    }
    clone.querySelector('.message-content').textContent = content;
    if (isOnBehalf) {
      const replyInstructions = '<p class="reply-note">Replies will be sent directly to the parent/carer.</p>';
      clone.querySelector('.message-content').insertAdjacentHTML('beforeend', replyInstructions);
    }

    if (attachments?.length) renderMessageAttachments(el, attachments);
    elements.conversationContainer.appendChild(clone);
  });
}

const isImage = name => !['pdf', 'docx'].includes(name.toLowerCase().split('.').pop());
const getFileIcon = name => isImage(name) ? 'image' : 'description';

function showImageModal(src, name) {
  const modal = document.getElementById('image-modal');
  modal.style.display = 'block';
  document.getElementById('modal-image').src = src;
  document.getElementById('image-caption').textContent = name;
}
const closeImageModal = () => document.getElementById('image-modal').style.display = 'none';

function showOriginalEmailModal(html) {
  const modal = document.getElementById('original-email-modal');
  modal.style.display = 'block';
  elements.iframe.srcdoc = html;
}
const closeOriginalEmailModal = () => document.getElementById('original-email-modal').style.display = 'none';

function renderMessageAttachments(container, attachments) {
  const wrap = document.createElement('div');
  wrap.className = 'message-attachments';
  attachments.forEach(({ fileName, url }) => {
    const item = document.createElement('div');
    item.className = 'attachment';
    const icon = document.createElement('span');
    icon.className = 'material-symbols-rounded';
    icon.textContent = getFileIcon(fileName);
    const nameEl = document.createElement('span');
    nameEl.textContent = fileName;
    item.append(icon, nameEl);
    if (isImage(fileName)) {
      item.style.cursor = 'pointer';
      item.onclick = () => showImageModal(url, fileName);
      wrap.append(item);
    } else {
      const link = document.createElement('a');
      link.href = url;
      link.className = 'attachment-link';
      link.append(item);
      wrap.append(link);
    }
  });
  container.append(wrap);
}

async function sendMessage() {
  const ticket = getCurrentTicket();
  const content = elements.newMessageInput.value.trim();
  if (!ticket || !content) return false;
  if (!canSendMessages(ticket)) return showToast('Please complete all ticket details before sending messages.', 'error');
  if (!ticket.assigneeName) return showToast('Please assign this ticket to a staff member before sending a message.', 'error');

  if (containsSalutationOrValediction(content, getSalutation(currentUser))) {
    elements.newMessageInput.focus();
    return showToast('You must not include a salutation or sign-off. These are added automatically.', 'error');
  }

  const files = Array.from(elements.messageAttachments.files);
  const isPrivate = elements.internalNoteCheckbox.checked;

  elements.sendMessageBtn.disabled = true;
  elements.closeTicketBtn.disabled = true;
  elements.sendMessageBtn.textContent = 'Sending...';
  try {
    const msg = await apiSendMessage(ticket.id, ticket.assigneeEmail, content, isPrivate, files);
    state.conversation.push(msg);
    elements.newMessageInput.value = '';
    autoExpandTextarea(elements.newMessageInput);
    elements.internalNoteCheckbox.checked = false;
    elements.newMessageInput.classList.remove('internal-note');
    elements.salutation.style.opacity = '1';
    elements.valediction.style.opacity = '1';
    elements.messageAttachments.value = '';
    elements.attachmentList.innerHTML = '';
    renderConversation();
    elements.ticketDetails.scrollTop = elements.ticketDetails.scrollHeight;
    updateCloseTicketButtonText();
    return true;
  } finally {
    elements.sendMessageBtn.disabled = false;
    elements.closeTicketBtn.disabled = false;
    elements.sendMessageBtn.textContent = 'Send Message';
  }
}

function handleAttachmentChange() {
  const files = Array.from(elements.messageAttachments.files);
  const allowed = ['.pdf', '.docx', '.png', '.jpg', '.jpeg', '.webp', '.heic'];
  const invalid = /[<>:"/\\|?*\x00-\x1f]/;
  const max = 10 * 1024 * 1024;
  const valid = [];
  files.forEach(f => {
    let msg;
    const ext = f.name.slice(f.name.lastIndexOf('.')).toLowerCase();
    if (!f.size) msg = `File "${f.name}" is empty and cannot be attached.`;
    else if (f.size > max) msg = `File "${f.name}" exceeds the 10 MB size limit.`;
    else if (!allowed.includes(ext)) msg = `File "${f.name}" has an invalid file type. Only PDF, DOCX, PNG, JPG, JPEG, WEBP, and HEIC files are allowed.`;
    else if (f.name.length > 100) msg = `File "${f.name}" has a name that is too long (maximum 100 characters).`;
    else if (invalid.test(f.name) || f.name.includes('..') || f.name.includes('/')) msg = `File "${f.name}" contains invalid characters in its name.`;
    if (msg) showToast(msg, 'error'); else valid.push(f);
  });
  if (valid.length !== files.length) {
    const dt = new DataTransfer();
    valid.forEach(f => dt.items.add(f));
    elements.messageAttachments.files = dt.files;
  }
  renderAttachmentList(valid);
}

function renderAttachmentList(files) {
  elements.attachmentList.innerHTML = '';
  files.forEach((file, i) => {
    const item = document.createElement('div');
    item.className = 'attachment-item';
    const icon = document.createElement('span');
    icon.className = 'material-symbols-rounded';
    icon.textContent = getFileIcon(file.name);
    const nameEl = document.createElement('span');
    nameEl.textContent = file.name;
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'remove-attachment';
    btn.innerHTML = '<span class="material-symbols-rounded">close</span>';
    btn.onclick = () => removeAttachment(i);
    item.append(icon, nameEl, btn);
    elements.attachmentList.appendChild(item);
  });
}

function removeAttachment(idx) {
  const dt = new DataTransfer();
  Array.from(elements.messageAttachments.files).forEach((f, i) => i !== idx && dt.items.add(f));
  elements.messageAttachments.files = dt.files;
  renderAttachmentList(Array.from(dt.files));
}

function addConversationEntry(content) {
  const entry = {
    timestamp: new Date().toISOString(),
    authorName: currentUser,
    isEmployee: true,
    isPrivate: true,
    content
  };
  state.conversation.push(entry);
  renderConversation();
}

function openSuggestModal() {
  elements.suggestModal.style.display = 'block';
  elements.generatedResponse.textContent = '';
  elements.suggestResponseSection.style.display = 'none';
  elements.insertSuggestBtn.style.display = 'none';
  elements.guidanceInput.focus();
}

function closeSuggestModal() {
  elements.suggestModal.style.display = 'none';
}

async function generateSuggestion() {
  const ticket = getCurrentTicket();
  const guidance = elements.guidanceInput.value.trim();
  if (!ticket || !guidance) return;
  elements.generateSuggestBtn.disabled = true;
  elements.generateSuggestBtn.textContent = 'Generating...';
  
  try {
    const result = await apiSuggestResponse(ticket.id, ticket.assigneeEmail, guidance);
    elements.generatedResponse.textContent = result;
    elements.suggestResponseSection.style.display = 'block';
    elements.insertSuggestBtn.style.display = 'inline-flex';
  } catch (error) {
    showToast('AI generation failed.', 'error');
  } finally {
    elements.generateSuggestBtn.disabled = false;
    elements.generateSuggestBtn.textContent = 'Generate';
  }
}

function insertSuggestion() {
  const suggestion = elements.generatedResponse.textContent;
  if (suggestion) {
    elements.newMessageInput.value = suggestion;
    autoExpandTextarea(elements.newMessageInput);
    closeSuggestModal();
    updateCloseTicketButtonText();
    elements.newMessageInput.focus();
  }
}

function containsSalutationOrValediction(text, name) {
  if (typeof text !== 'string' || typeof name !== 'string') return false;
  const trimmed = text.trim();
  const greetingRegex = /^(?:Dear|Hi|Hello|Hey|Greetings|To whom it may concern|Good morning|Good afternoon|Good evening)\b/i;
  if (greetingRegex.test(trimmed)) return true;
  const valedictionRegex = /^\s*(?:Best regards|Kind regards|Warm regards|Warm wishes|Best wishes|Warmest regards|Warmest wishes|Sincerely|Yours sincerely|Yours faithfully|Yours truly|All the best|Regards|Cheers|Thank you|Thanks)\b[\s,]*$/im;
  if (valedictionRegex.test(text)) return true;
  const esc = name.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&');
  const nameEndRegex = new RegExp(`\\b${esc}[\\.,]?\\s*$`, 'i');
  return nameEndRegex.test(text);
}