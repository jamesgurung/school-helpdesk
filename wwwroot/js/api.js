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

async function apiSendMessage(ticketId, assigneeEmail, content, isPrivate) {
  await apiRequest(
    'POST',
    `/api/tickets/${ticketId}/message`,
    { assigneeEmail, content, isPrivate },
    'send message'
  );
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