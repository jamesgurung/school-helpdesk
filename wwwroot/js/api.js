async function apiRequest(httpMethod, endpoint, data, actionText) {
  try {
    const response = await fetch(endpoint, {
      method: httpMethod,
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': antiforgeryToken
      },
      body: JSON.stringify(data)
    });

    if (!response.ok) {
      throw new Error(`${actionText}: ${response.status}`);
    }

    return response;
  } catch (error) {
    showToast(`Failed to ${actionText.toLowerCase()}. Please try again.`, 'error');
    throw error;
  }
}

async function apiUpdateTicketAssignee(ticketId, assigneeEmail, newAssigneeEmail) {
  await apiRequest(
    'PUT',
    `/api/tickets/${ticketId}/assignee`,
    { assigneeEmail, newAssigneeEmail },
    'update assignee'
  );
}

async function apiUpdateTicketStudent(ticketId, assigneeEmail, studentFirst, studentLast, studentTutorGroup) {
  await apiRequest(
    'PUT',
    `/api/tickets/${ticketId}/student`,
    { assigneeEmail, studentFirst, studentLast, studentTutorGroup },
    'update student'
  );
}

async function apiUpdateTicketParent(ticketId, assigneeEmail, newParentName) {
  await apiRequest(
    'PUT',
    `/api/tickets/${ticketId}/parent`,
    { assigneeEmail, newParentName },
    'update parent'
  );
}

async function apiUpdateTicketStatus(ticketId, assigneeEmail, isClosed) {
  await apiRequest(
    'PUT',
    `/api/tickets/${ticketId}/status`,
    { assigneeEmail, isClosed },
    'update ticket status'
  );
}

async function apiUpdateTicketTitle(ticketId, assigneeEmail, newTitle) {
  await apiRequest(
    'PUT',
    `/api/tickets/${ticketId}/title`,
    { assigneeEmail, newTitle },
    'update ticket title'
  );
}

async function apiSendMessage(ticketId, assigneeEmail, content, isPrivate, files) {
  const formData = new FormData();
  formData.append('assigneeEmail', assigneeEmail);
  formData.append('content', content);
  formData.append('isPrivate', isPrivate.toString());
  files.forEach(file => { formData.append('attachments', file); });

  try {
    const response = await fetch(`/api/tickets/${ticketId}/message`, {
      method: 'POST',
      headers: {
        'X-XSRF-TOKEN': antiforgeryToken
      },
      body: formData
    });

    if (!response.ok) {
      throw new Error(`send message: ${response.status}`);
    }

    return await response.json();
  } catch (error) {
    showToast('Failed to send message. Please try again.', 'error');
    throw error;
  }
}

async function apiCreateTicket(ticketData) {
  const response = await apiRequest(
    'POST',
    '/api/tickets',
    ticketData,
    'create ticket'
  );
  return await response.json();
}

async function apiSuggestResponse(ticketId, assigneeEmail, guidance) {
  const response = await apiRequest(
    'POST',
    `/api/tickets/${ticketId}/suggest`,
    { assigneeEmail, guidance },
    'generate suggestion'
  );
  return await response.json();
}