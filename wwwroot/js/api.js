async function apiRequest(httpMethod, endpoint, data, actionText) {
  try {
    const isFormData = data instanceof FormData;
    const response = await fetch(endpoint, {
      method: httpMethod,
      headers: {
        ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
        'X-XSRF-TOKEN': antiforgeryToken
      },
      body: isFormData ? data : JSON.stringify(data)
    });

    if (!response.ok) throw new Error(`${actionText}: ${response.status}`);
    return response;
  } catch (error) {
    showToast(`Failed to ${actionText.toLowerCase()}. Please try again.`, 'error');
    throw error;
  }
}

async function apiJsonRequest(httpMethod, endpoint, data, actionText, reportParseError = false) {
  const response = await apiRequest(httpMethod, endpoint, data, actionText);
  try {
    return await response.json();
  } catch (error) {
    if (reportParseError) showToast(`Failed to ${actionText.toLowerCase()}. Please try again.`, 'error');
    throw error;
  }
}

async function apiUpdateTicketAssignee(ticketId, assigneeEmail, newAssigneeEmail) {
  return apiJsonRequest(
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
  const response = await apiRequest(
    'PUT',
    `/api/tickets/${ticketId}/status`,
    { assigneeEmail, isClosed },
    'update ticket status'
  );
  if (response.status === 204) return null;
  return response.json();
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
  return apiJsonRequest('POST', `/api/tickets/${ticketId}/message`, formData, 'send message', true);
}

async function apiCreateTicket(ticketData) {
  return apiJsonRequest(
    'POST',
    '/api/tickets',
    ticketData,
    'create ticket'
  );
}

async function apiGetAllTickets() {
  const response = await fetch('/api/tickets');
  if (!response.ok) {
    showToast('Failed to load tickets. Please try again.', 'error');
    throw new Error(`load tickets: ${response.status}`);
  }
  return response.json();
}

async function apiSuggestResponse(ticketId, assigneeEmail, guidance) {
  return apiJsonRequest(
    'POST',
    `/api/tickets/${ticketId}/suggest`,
    { assigneeEmail, guidance },
    'generate suggestion'
  );
}

async function apiGetLastUpdated(ticketId) {
  const response = await fetch(`/api/tickets/${ticketId}/lastupdated`);
  if (!response.ok) return null;
  return response.json();
}
