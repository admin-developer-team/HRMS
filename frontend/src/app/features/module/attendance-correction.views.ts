import { FormFieldDefinition, WorkspaceViewDefinition } from '../../core/models';

const version: FormFieldDefinition = { key: 'version', label: 'Version', type: 'number', sourceKey: 'version', hidden: true };
export const correctionFields: FormFieldDefinition[] = [
  { key: 'workDate', label: 'Workday', type: 'date', required: true, sourceKey: 'workDate', help: 'The date of the shift in your company time zone.' },
  { key: 'requestedClockIn', label: 'Correct check-in', type: 'datetime-local', required: true, sourceKey: 'clockedInAt', help: 'Enter the time in this device’s local time zone.' },
  { key: 'requestedClockOut', label: 'Correct check-out', type: 'datetime-local', required: true, sourceKey: 'clockedOutAt' },
  { key: 'reason', label: 'Reason for correction', type: 'textarea', required: true, help: 'Explain what happened in 10 to 500 characters. A separate reviewer must approve the change.' },
];

export function correctionsView(scope: 'mine' | 'team' | 'all'): WorkspaceViewDefinition {
  return {
    label: scope === 'mine' ? 'My corrections' : 'Attendance corrections', icon: 'edit_calendar',
    endpoint: `/attendance-corrections?scope=${scope}`, listShape: 'paged',
    ...(scope === 'mine' ? { createLabel: 'Report a missing day', createEndpoint: '/attendance-corrections', fields: correctionFields } : {}),
    columns: [
      { key: 'employeeName', label: 'Employee' }, { key: 'workDate', label: 'Workday', type: 'date-only' },
      { key: 'requestedClockIn', label: 'Requested check-in', type: 'datetime' },
      { key: 'requestedClockOut', label: 'Requested check-out', type: 'datetime' },
      { key: 'reason', label: 'Reason' }, { key: 'status', label: 'Status', type: 'status' },
      { key: 'reviewComment', label: 'Reviewer comment' },
    ],
    filters: [{ key: 'status', label: 'Status', type: 'select', options: ['Pending', 'Approved', 'Rejected', 'Cancelled'].map(value => ({ label: value, value })) }],
    rowActions: scope === 'mine' ? [{ label: 'Cancel request', icon: 'cancel', method: 'put', path: '/attendance-corrections/{id}/cancel?version={version}', visibleStatuses: ['Pending'], confirm: 'Cancel this correction request?' }] : [
      { label: 'Review correction', icon: 'fact_check', method: 'put', path: '/attendance-corrections/{id}/review', visibleStatuses: ['Pending'], fields: [
        { key: 'approve', label: 'Decision', type: 'select', required: true, options: [{ label: 'Approve', value: true }, { label: 'Reject', value: false }] },
        { key: 'comment', label: 'Review comment', type: 'textarea', help: 'Required when rejecting a correction.' }, version,
      ] },
    ],
    emptyMessage: 'No attendance corrections match this view.',
  };
}

export const myAttendanceSessionsView: WorkspaceViewDefinition = {
  label: 'Clock sessions', icon: 'history', endpoint: '/me/attendance', listShape: 'paged',
  columns: [
    { key: 'workDate', label: 'Workday', type: 'date-only' }, { key: 'clockedInAt', label: 'Check-in', type: 'datetime' },
    { key: 'clockedOutAt', label: 'Check-out', type: 'datetime' }, { key: 'workHours', label: 'Hours', type: 'duration' },
    { key: 'sessionState', label: 'Session', type: 'status' }, { key: 'source', label: 'Source' },
  ],
  filters: [{ key: 'from', label: 'From', type: 'date' }, { key: 'to', label: 'To', type: 'date' }],
  rowActions: [{ label: 'Request correction', icon: 'edit_calendar', method: 'post', path: '/attendance-corrections', fields: [
    { key: 'attendanceRecordId', label: 'Session', type: 'text', sourceKey: 'id', hidden: true }, ...correctionFields,
  ] }],
};
