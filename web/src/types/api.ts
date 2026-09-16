export type Role = "TECHNICIAN" | "SUPERVISOR" | "MANAGER" | "ADMIN";
export type MachineStatus = "Operational" | "UnderMaintenance" | "Breakdown" | "OutOfService" | "Standby" | "Decommissioned";
export type MachineCriticality = "Low" | "Medium" | "High" | "Critical";
export type MaintenancePriority = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export type WorkOrderLifecycleStatus = "PLANNED" | "ASSIGNED" | "IN_PROGRESS" | "AWAITING_APPROVAL" | "APPROVED" | "REJECTED" | "CANCELLED";
export type BreakdownSeverity = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export type BreakdownStatus = "REPORTED" | "ASSIGNED" | "IN_PROGRESS" | "AWAITING_APPROVAL" | "REJECTED" | "CLOSED" | "CANCELLED";
export type CalibrationValidityStatus = "NOT_REQUIRED" | "VALID" | "EXPIRING_SOON" | "EXPIRED";
export type CalibrationRenewalStatus = "NOT_STARTED" | "IN_PROGRESS" | "COMPLETED" | "CANCELLED";
export type CalibrationResult = "PASS" | "FAIL" | "CONDITIONAL";
export type ChecklistResponseType = "BOOLEAN" | "PASS_FAIL" | "NUMBER" | "TEXT" | "PHOTO" | "CONFIRMATION";

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface User {
  id: string;
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
  mobile?: string | null;
  departmentId?: string | null;
  isActive: boolean;
  roles: Role[];
  createdAt: string;
  updatedAt: string;
  lastLoginAt?: string | null;
}

export interface UserLookup {
  id: string;
  employeeId: string;
  firstName: string;
  lastName: string;
}

export interface AuditLogUserSummary {
  id: string;
  employeeId: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface AuditLog {
  id: string;
  createdAt: string;
  userId?: string | null;
  user?: AuditLogUserSummary | null;
  action: string;
  entityType: string;
  entityId?: string | null;
  oldValuesJson?: string | null;
  newValuesJson?: string | null;
  ipAddress?: string | null;
  deviceInfo?: string | null;
}

export interface AuthResponse {
  accessTokenExpiresAt: string;
  user: User;
}

export interface MasterDataItem {
  id: string;
  name: string;
  description?: string | null;
  departmentId?: string | null;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface Machine {
  id: string;
  machineCode: string;
  assetNumber?: string | null;
  name: string;
  categoryId?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  departmentId?: string | null;
  locationId?: string | null;
  installationDate?: string | null;
  commissioningDate?: string | null;
  warrantyExpiryDate?: string | null;
  status: MachineStatus;
  criticality: MachineCriticality;
  calibrationRequired: boolean;
  preventiveMaintenanceRequired: boolean;
  machineOwnerUserId?: string | null;
  supervisorUserId?: string | null;
  notes?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  currentCalibrationStatus: CalibrationValidityStatus;
  currentCertificateNumber?: string | null;
  calibrationExpiryDate?: string | null;
  daysUntilCalibrationExpiry?: number | null;
  renewalInProgress: boolean;
}

export interface AssignmentHistory {
  id: string;
  machineId: string;
  technicianId: string;
  supervisorId?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  assignedByUserId: string;
  reason?: string | null;
}

export interface MaintenancePlan {
  id: string;
  machineId: string;
  planName: string;
  maintenanceTypeId: string;
  priority: MaintenancePriority;
  frequencyType: "DAILY" | "WEEKLY" | "MONTHLY" | "DAYS" | "WEEKS" | "MONTHS" | "QUARTERLY" | "HALF_YEARLY" | "YEARLY";
  frequencyValue: number;
  startDate: string;
  nextDueDate: string;
  defaultTechnicianId?: string | null;
  supervisorId: string;
  instructions?: string | null;
  estimatedDurationMinutes?: number | null;
  photoRequired: boolean;
  minimumPhotoCount: number;
  commentRequired: boolean;
  isActive: boolean;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

export interface ChecklistItem {
  id?: string;
  checklistTemplateId?: string;
  sequenceNumber: number;
  title: string;
  description?: string | null;
  responseType: ChecklistResponseType;
  isMandatory: boolean;
  unit?: string | null;
  minimumValue?: number | null;
  maximumValue?: number | null;
  photoRequired: boolean;
}

export interface ChecklistTemplate {
  id: string;
  maintenancePlanId: string;
  version: number;
  name: string;
  isActive: boolean;
  createdByUserId: string;
  createdAt: string;
  items: ChecklistItem[];
}

export interface WorkOrderSummary {
  id: string;
  workOrderNumber: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  maintenancePlanId?: string | null;
  planName: string;
  assignedTechnicianId?: string | null;
  assignedTechnicianName?: string | null;
  supervisorId: string;
  supervisorName: string;
  plannedDate: string;
  dueDate: string;
  priority: MaintenancePriority;
  lifecycleStatus: WorkOrderLifecycleStatus;
  overdue: boolean;
  escalationLevel: number;
  createdAt?: string;
  updatedAt?: string;
  isDueSoon: boolean;
  daysOverdue: number;
  lastEscalatedAt?: string | null;
  isDueToday: boolean;
}

export interface WorkOrderDefinition {
  id: string;
  maintenanceTypeId: string;
  maintenanceTypeName: string;
  checklistTemplateId: string;
  checklistVersion: number;
  checklistName: string;
  planName: string;
  instructions?: string | null;
  estimatedDurationMinutes?: number | null;
  photoRequired: boolean;
  minimumPhotoCount: number;
  commentRequired: boolean;
  priority: MaintenancePriority;
  machineCode: string;
  machineName: string;
  assignedTechnicianEmployeeId?: string | null;
  assignedTechnicianName?: string | null;
  supervisorEmployeeId: string;
  supervisorName: string;
  items: ChecklistItem[];
}

export interface WorkOrderDetail {
  workOrder: WorkOrderSummary;
  startedAt?: string | null;
  completedAt?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  cancelledAt?: string | null;
  definition: WorkOrderDefinition;
}

export interface WorkOrderExecution {
  workOrderId: string;
  lifecycleStatus: WorkOrderLifecycleStatus;
  overallComments?: string | null;
  observations?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  durationMinutes: number;
  isComplete: boolean;
  technicianId?: string | null;
  technicianName?: string | null;
  checklistResults: Record<string, unknown>[];
  parts: Record<string, unknown>[];
  defects: Record<string, unknown>[];
  attachments: FileAttachment[];
}

export interface WorkOrderReview { id: string; workOrderId: string; workOrderSubmissionId: string; supervisorId: string; supervisorEmployeeId: string; supervisorName: string; decision: "APPROVED" | "REJECTED"; remarks?: string | null; decisionAt: string }
export interface WorkOrderSubmissionSummary { id: string; workOrderId: string; versionNumber: number; submittedByUserId: string; technicianEmployeeId: string; technicianName: string; submittedAt: string; completedAt: string; durationMinutes: number; review?: WorkOrderReview | null }

export interface TimelineEvent {
  id: string;
  sequenceNumber: number;
  action: string;
  actorUserId?: string | null;
  actorName?: string | null;
  occurredAt: string;
  submissionVersion?: number | null;
  details?: string | null;
}

export interface Breakdown {
  id: string;
  breakdownNumber: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  reportedByUserId: string;
  reporterName: string;
  reportedAt: string;
  severity: BreakdownSeverity;
  machineStopped: boolean;
  description: string;
  initialObservation?: string | null;
  assignedTechnicianId?: string | null;
  technicianName?: string | null;
  supervisorId: string;
  supervisorName: string;
  status: BreakdownStatus;
  startedAt?: string | null;
  completedAt?: string | null;
  submittedAt?: string | null;
  closedAt?: string | null;
  returnedToServiceAt?: string | null;
  downtimeMinutes: number;
  downtimeOngoing: boolean;
  submissionVersion: number;
}

export interface CorrectiveExecution {
  breakdownId: string;
  status: BreakdownStatus;
  technicianId?: string | null;
  technicianName?: string | null;
  rootCause?: string | null;
  correctiveAction?: string | null;
  comments?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  durationMinutes: number;
  downtimeMinutes: number;
  isComplete: boolean;
  parts: Record<string, unknown>[];
  attachments: FileAttachment[];
}

export interface CalibrationCertificate {
  id: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  certificateNumber: string;
  calibrationProvider: string;
  calibrationDate: string;
  expiryDate: string;
  result: CalibrationResult;
  remarks?: string | null;
  certificateFileId?: string | null;
  daysRemaining?: number | null;
  validityStatus: CalibrationValidityStatus;
  renewalStatus: CalibrationRenewalStatus;
}

export interface CalibrationSummary {
  totalCalibrationRequiredMachines: number;
  valid: number;
  expiringWithin60Days: number;
  expiringWithin30Days: number;
  expiringWithin7Days: number;
  expired: number;
  renewalInProgress: number;
}

export interface ExternalService {
  id: string;
  serviceNumber: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  serviceCompany: string;
  serviceTechnician?: string | null;
  serviceDate: string;
  serviceType: string;
  description: string;
  findings?: string | null;
  workCompleted?: string | null;
  partsReplaced?: string | null;
  cost?: number | null;
  purchaseOrderNumber?: string | null;
  invoiceNumber?: string | null;
  followUpDate?: string | null;
  nextServiceDate?: string | null;
  recommendation?: string | null;
  comments?: string | null;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
  activeAttachmentCount: number;
}

export interface FileAttachment {
  id: string;
  fileId: string;
  originalFilename: string;
  mimeType: string;
  fileSize: number;
  uploadedAt: string;
  description?: string | null;
}

export interface MachineDocument extends FileAttachment {
  machineId: string;
  documentType: string;
  title: string;
  documentDate?: string | null;
  expiryDate?: string | null;
  isExpired: boolean;
  daysUntilExpiry?: number | null;
  expiryStatus: string;
  uploadedByUserId: string;
  updatedAt: string;
  isActive: boolean;
}

export interface Notification {
  id: string;
  userId: string;
  notificationType: string;
  title: string;
  message: string;
  entityType?: string | null;
  entityId?: string | null;
  createdAt: string;
  readAt?: string | null;
  isRead: boolean;
  priority: string;
  expiresAt?: string | null;
}

export interface ReportingPeriod { from: string; to: string; asOf: string; dateConvention: string }
export interface TechnicianWorkload { technicianId: string; employeeId: string; technicianName: string; assigned: number; started: number; approved: number; rejected: number; awaitingApproval: number; overdue: number; averageExecutionMinutes?: number | null; breakdownAssignments: number; correctiveClosures: number }
export interface ManagerDashboard {
  scope: string;
  period: ReportingPeriod;
  machines: { totalMachines: number; operationalMachines: number; machinesUnderMaintenance: number; breakdownMachines: number; outOfServiceMachines: number };
  maintenance: { maintenanceDueToday: number; maintenanceDueThisWeek: number; maintenanceOverdue: number; maintenanceAwaitingApproval: number; maintenanceEscalated: number };
  breakdowns: { openBreakdowns: number; criticalBreakdowns: number };
  calibration: { calibrationValid: number; calibrationExpiringWithin60Days: number; calibrationExpiringWithin30Days: number; calibrationExpiringWithin7Days: number; calibrationExpired: number; calibrationRenewalInProgress: number };
  externalServiceFollowUpsDue: number;
  maintenanceCompliance: { percentage?: number | null; completedCount: number; overdueCount: number; pendingCount: number; totalDueCount: number };
  technicianWorkload: TechnicianWorkload[];
  escalationAlerts: Array<{ workOrderId: string; workOrderNumber: string; machineCode: string; machineName: string; dueDate: string; escalationLevel: number; lastEscalatedAt?: string | null }>;
  upcomingCalibrations: Array<{ machineId: string; machineCode: string; machineName: string; certificateNumber?: string | null; expiryDate?: string | null; daysRemaining?: number | null; validityStatus: CalibrationValidityStatus }>;
  externalServiceFollowUps: Array<{ externalServiceId: string; serviceNumber: string; machineCode: string; machineName: string; serviceCompany: string; dueDate: string; overdue: boolean; recommendation?: string | null }>;
}

export interface ReportingTrends {
  period: ReportingPeriod;
  maintenance: Array<{ year: number; month: number; compliancePercentage?: number | null; approved: number; overdue: number }>;
  breakdowns: Array<{ year: number; month: number; breakdownCount: number; downtimeMinutes: number }>;
  technicianWorkload: Array<{ year: number; month: number; assigned: number; approved: number; overdue: number }>;
  calibrationStatus: Array<{ status: CalibrationValidityStatus; count: number }>;
}

export interface EscalationSettings { dueSoonDays: number; technicianOverdueDays: number; supervisorEscalationDays: number; managerEscalationDays: number; updatedAt?: string | null; updatedByUserId?: string | null }

export interface ReportDefinition {
  slug: string;
  label: string;
  endpoint: string;
  exportable: boolean;
}
