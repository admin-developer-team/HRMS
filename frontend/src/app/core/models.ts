export interface UserSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: {
    id: string;
    tenantId: string;
    employeeId?: string;
    email: string;
    displayName: string;
    roles: string[];
    permissions: string[];
  };
}

export interface AttendanceRecord {
  id: string;
  employeeId: string;
  workDate: string;
  clockedInAt?: string;
  clockedOutAt?: string;
  status: string;
  sessionState: string;
  workHours: number;
  overtimeHours: number;
  source?: string;
  notes?: string;
  clockInLatitude?: number;
  clockInLongitude?: number;
  clockInAccuracyMeters?: number;
  clockInAddress?: string;
  clockInIpAddress?: string;
  clockInUserAgent?: string;
  clockOutLatitude?: number;
  clockOutLongitude?: number;
  clockOutAccuracyMeters?: number;
  clockOutAddress?: string;
  clockOutIpAddress?: string;
  clockOutUserAgent?: string;
  version: number;
}

export interface SelfDashboard {
  profile: {
    employeeId: string;
    employeeNumber: string;
    fullName: string;
    workEmail: string;
    phone?: string;
    hireDate: string;
    status: string;
    employmentType: string;
    departmentId?: string;
    designationId?: string;
    locationId?: string;
    managerId?: string;
    salaryCurrency: string;
    baseSalary: number;
  };
  todayAttendance?: AttendanceRecord;
  todayTotalHours: number;
  todaySessionCount: number;
  requireLocationCapture: boolean;
  pendingLeaveRequests: number;
  availableLeaveDays: number;
  pendingTimesheets: number;
  openExpenses: number;
  trainingDue: number;
  announcements: {
    id: string;
    title: string;
    body: string;
    publishedAt: string;
    expiresAt?: string;
    audience: string;
  }[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface Dashboard {
  activeEmployees: number;
  pendingLeaveRequests: number;
  openJobs: number;
  availableAssets: number;
  currentPayrollTotal: number;
  employeesByStatus: Record<string, number>;
}

export interface Employee {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  workEmail: string;
  phone?: string;
  hireDate: string;
  status: string;
  employmentType: string;
  departmentId?: string;
  designationId?: string;
  locationId?: string;
  managerId?: string;
  baseSalary: number;
  salaryCurrency: string;
  userId?: string;
  version: number;
}

export interface ColumnDefinition {
  key: string;
  label: string;
  type?: 'text' | 'date' | 'date-only' | 'datetime' | 'time' | 'duration' | 'minutes' | 'currency' | 'status' | 'number' | 'attendance-map' | 'documents';
  documentOwnerType?: DocumentOwnerType;
  documentCategory?: string;
  documentLabel?: string;
  documentReadonly?: boolean;
  documentReadonlyStatuses?: string[];
  documentAllowedExtensions?: string[];
}

export interface FormFieldDefinition {
  key: string;
  label: string;
  type:
    | 'text'
    | 'email'
    | 'password'
    | 'number'
    | 'date'
    | 'datetime-local'
    | 'time'
    | 'select'
    | 'multiselect'
    | 'textarea'
    | 'checkbox';
  required?: boolean;
  options?: { label: string; value: string | number | boolean }[];
  optionsEndpoint?: string;
  optionLabel?: string;
  optionValue?: string;
  optionsShape?: 'array' | 'paged';
  placeholder?: string;
  defaultValue?: string | number | boolean | string[];
  min?: number;
  help?: string;
  checkboxLabel?: string;
  sourceKey?: string;
  hidden?: boolean;
}

export interface FilterDefinition {
  key: string;
  label: string;
  type?: 'text' | 'select' | 'date' | 'number';
  options?: { label: string; value: string }[];
  optionsEndpoint?: string;
  optionLabel?: string;
  optionValue?: string;
  optionsShape?: 'array' | 'paged';
}

export interface RowActionDefinition {
  label: string;
  icon: string;
  method: 'get' | 'post' | 'put' | 'delete' | 'documents';
  path?: string;
  fields?: FormFieldDefinition[];
  confirm?: string;
  tone?: 'default' | 'danger';
  detailColumns?: ColumnDefinition[];
  detailTitle?: string;
  visibleStatuses?: string[];
  visibleField?: string;
  visibleValues?: string[];
  documentOwnerType?: DocumentOwnerType;
  documentOwnerIdField?: string;
  documentCategory?: string;
  documentLabel?: string;
  documentReadonly?: boolean;
  documentReadonlyStatuses?: string[];
  documentMaxFiles?: number;
  documentReplaceMode?: boolean;
  documentAllowedExtensions?: string[];
}

export interface WorkspaceViewDefinition {
  label: string;
  icon?: string;
  endpoint: string;
  listShape?: 'paged' | 'array';
  columns: ColumnDefinition[];
  createEndpoint?: string;
  createLabel?: string;
  fields?: FormFieldDefinition[];
  filters?: FilterDefinition[];
  rowActions?: RowActionDefinition[];
  toolbarActions?: RowActionDefinition[];
  emptyMessage?: string;
  createDocumentOwnerType?: DocumentOwnerType;
  createDocumentCategory?: string;
  createDocumentLabel?: string;
  createDocumentAllowedExtensions?: string[];
}

export interface ModuleDefinition {
  key: string;
  title: string;
  eyebrow: string;
  description: string;
  icon: string;
  endpoint?: string;
  listShape?: 'paged' | 'array';
  columns?: ColumnDefinition[];
  createEndpoint?: string;
  createLabel?: string;
  fields?: FormFieldDefinition[];
  views: WorkspaceViewDefinition[];
  highlights: {
    label: string;
    value: string;
    trend: string;
    tone: 'blue' | 'green' | 'amber' | 'violet';
  }[];
}

export interface WorkProject {
  id: string;
  key: string;
  name: string;
  description?: string;
  leadEmployeeId?: string;
  leadName?: string;
  isActive: boolean;
  memberCount: number;
  version: number;
}

export interface WorkProjectMember {
  id: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  canCreateItems: boolean;
  canAssignItems: boolean;
  canTransitionItems: boolean;
  canLogWork: boolean;
  canViewAllWorklogs: boolean;
}

export interface WorkItem {
  id: string;
  projectId: string;
  projectKey: string;
  key: string;
  parentId?: string;
  sprintId?: string;
  type: string;
  summary: string;
  status: string;
  priority: string;
  reporterEmployeeId?: string;
  reporterName?: string;
  assigneeEmployeeId?: string;
  assigneeName?: string;
  assigneeEmployeeIds: string[];
  assigneeNames: string[];
  dueDate?: string;
  originalEstimateMinutes?: number;
  remainingEstimateMinutes?: number;
  loggedMinutes: number;
  storyPoints?: number;
  labels: string[];
  resolution?: string;
  resolvedAt?: string;
  createdAt: string;
  version: number;
}

export interface WorkComment {
  id: string;
  authorEmployeeId?: string;
  authorName: string;
  body: string;
  createdAt: string;
  updatedAt?: string;
  canEdit: boolean;
  version: number;
}

export interface WorkLog {
  id: string;
  employeeId: string;
  employeeName: string;
  workDate: string;
  minutes: number;
  description?: string;
  createdAt: string;
  canEdit: boolean;
  version: number;
}

export interface WorkHistory {
  id: string;
  actorEmployeeId?: string;
  actorName: string;
  eventType: string;
  fieldName?: string;
  beforeValue?: string;
  afterValue?: string;
  createdAt: string;
}

export interface WorkItemDetail {
  item: WorkItem;
  description?: string;
  comments: WorkComment[];
  worklogs: WorkLog[];
  history: WorkHistory[];
  access?: WorkProjectMember;
}

export interface WorkOverview {
  openItems: number;
  dueSoon: number;
  overdue: number;
  completedThisMonth: number;
  loggedMinutesThisMonth: number;
}

export interface WorkTimeReportRow {
  workItemId: string;
  key: string;
  summary: string;
  assigneeName?: string;
  minutesByDate: Record<string, number>;
  totalMinutes: number;
}

export interface WorkTimeReport {
  from: string;
  to: string;
  dates: string[];
  rows: WorkTimeReportRow[];
  totalMinutes: number;
}

export type DocumentOwnerType =
  | 'Tenant'
  | 'User'
  | 'Employee'
  | 'WorkItem'
  | 'LeaveRequest'
  | 'ExpenseClaim'
  | 'Candidate';

export interface StoredDocument {
  id: string;
  ownerType: DocumentOwnerType;
  ownerId: string;
  category: string;
  fileName: string;
  extension: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
  uploadedByUserId?: string;
}

export interface CompanyProfile {
  id: string;
  name: string;
  legalName?: string;
  slug: string;
  defaultCurrency: string;
  timeZone: string;
  locale: string;
  logoDocumentId?: string;
  version: number;
}

export interface UserNotification {
  id: string;
  title: string;
  message: string;
  kind: string;
  link?: string;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationFeed {
  items: UserNotification[];
  unreadCount: number;
}
