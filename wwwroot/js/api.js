async function apiPutRequest(endpoint, data, actionText) {
  try {
    const response = await fetch(endpoint, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'X-XSRF-TOKEN': antiforgeryToken
      },
      body: JSON.stringify(data)
    });
    
    if (!response.ok) {
      throw new Error(`${actionText}: ${response.status}`);
    }
  } catch (error) {
    showToast(`Failed to ${actionText.toLowerCase()}. Please try again.`, 'error');
  }
}

async function apiUpdateTicketAssignee(ticketId, assigneeEmail, newAssigneeEmail) {
  await apiPutRequest(
    `/api/tickets/${ticketId}/assignee`,
    { assigneeEmail, newAssigneeEmail },
    'update assignee'
  );
}

async function apiUpdateTicketStudent(ticketId, assigneeEmail, studentFirst, studentLast, studentTutorGroup) {
  await apiPutRequest(
    `/api/tickets/${ticketId}/student`,
    { assigneeEmail, studentFirst, studentLast, studentTutorGroup },
    'update student'
  );
}

async function apiUpdateTicketStatus(ticketId, assigneeEmail, isClosed) {
  await apiPutRequest(
    `/api/tickets/${ticketId}/status`,
    { assigneeEmail, isClosed },
    'update ticket status'
  );
}

async function apiUpdateTicketTitle(ticketId, assigneeEmail, newTitle) {
  await apiPutRequest(
    `/api/tickets/${ticketId}/title`,
    { assigneeEmail, newTitle },
    'update ticket title'
  );
}