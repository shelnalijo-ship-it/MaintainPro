# Functional Requirements Document

## Maintenance Management Application

**Document Version:** 2.0
**Date:** 14 September 2026
**Purpose:** Internal Company Maintenance Management System

---

# 1. Document Purpose

This Functional Requirements Document defines the functional, workflow, access-control, reporting, notification, approval, mobile, web, and data-management requirements for the company Maintenance Management Application.

The solution shall consist of:

* A **Mobile Application** for technicians and supervisors
* A **Web Application** for the manager and administrator
* A **Shared Backend System**
* A **Central Database**
* A **Central File and Photo Storage System**
* A **Notification and Escalation Service**
* A **Reporting Service**

The application is intended for a company operating approximately:

* Up to 40 machines/assets
* Approximately 20 employees/technicians
* 2 supervisors
* 1 manager

The system shall support preventive maintenance planning and execution, technician assignment, maintenance approval, escalation, calibration certificate management, breakdown and corrective maintenance, external service documentation, reporting, and future spare-parts inventory management.

---

# 2. Project Objectives

The primary objectives of the system are to:

1. Maintain a centralized database of all company machines and assets.
2. Plan preventive maintenance systematically.
3. Assign machine ownership to technicians.
4. Automatically generate preventive maintenance work orders.
5. Notify technicians of assigned and upcoming maintenance.
6. Allow technicians to perform maintenance using a mobile application.
7. Allow technicians to upload photos and service documents directly from mobile devices.
8. Allow supervisors to review, approve, reject, and reassign work using the mobile application.
9. Escalate overdue maintenance to supervisors and management.
10. Maintain a complete maintenance history for every machine.
11. Monitor calibration certificate validity.
12. Generate calibration renewal reminders.
13. Store external service reports and supporting documents.
14. Record machine breakdowns and corrective maintenance.
15. Provide the manager with a centralized web dashboard.
16. Allow the manager to plan maintenance and monitor maintenance performance.
17. Generate downloadable PDF and Excel reports.
18. Maintain a reliable audit trail.
19. Introduce full spare-parts inventory management in a later phase.

---

# 3. Solution Architecture

The system shall use separate user interfaces for operational and management functions.

## 3.1 Mobile Application

The Mobile Application shall primarily be used by:

* Technicians
* Supervisors

The application shall be optimized for Android and iOS mobile devices.

The mobile application shall focus on operational maintenance activities.

---

## 3.2 Manager Web Application

The Web Application shall primarily be used by:

* Manager
* Administrator
* Supervisors, where desktop access is required

The web interface shall be optimized for desktop and laptop screens.

The web application shall focus on:

* Administration
* Maintenance planning
* Monitoring
* Reporting
* Escalations
* Machine management
* Configuration

---

## 3.3 Shared Backend

Both applications shall connect to the same backend application programming interface.

The backend shall control:

* Authentication
* Authorization
* Business rules
* Maintenance scheduling
* Work order generation
* Approval workflow
* Escalation logic
* Notifications
* Reporting
* Audit logging
* Data storage

Business rules shall not be duplicated independently between the mobile and web applications.

---

# 4. High-Level Architecture

The recommended logical structure is:

Mobile Application
Technicians / Supervisors

↓

Shared Backend API

↓

Central Database

↓

Document and Photo Storage

↓

Notification Service

↓

Reporting Service

↑

Manager Web Application

---

# 5. User Roles

The application shall support role-based access control.

The primary roles shall be:

1. Technician
2. Supervisor
3. Manager
4. Administrator

---

# 6. Technician Access

Technicians shall primarily use the Mobile Application.

Technicians shall be able to:

* Log in securely
* View assigned machines
* View assigned maintenance work orders
* View jobs due today
* View upcoming maintenance
* View overdue work
* Open maintenance checklists
* Complete checklist items
* Enter machine readings
* Add comments
* Take photos using the mobile camera
* Upload files and service reports
* Record spare parts used
* Record additional defects
* Submit maintenance for supervisor approval
* Receive rejection comments
* Re-submit rejected maintenance
* Report machine breakdowns
* Perform assigned corrective maintenance
* View notifications
* View selected machine maintenance history
* View calibration status where relevant

Technicians shall not be permitted to:

* Approve their own work
* Modify maintenance schedules
* Modify other users
* Change escalation rules
* Delete approved maintenance records

---

# 7. Supervisor Access

Supervisors shall primarily use the Mobile Application.

Supervisors may additionally access selected functions through the Web Application if permitted.

Supervisors shall be able to:

* View assigned technicians
* View machines under their supervision
* View technician workloads
* View due maintenance
* View overdue maintenance
* View escalated maintenance
* Assign maintenance work
* Reassign maintenance work
* Review submitted maintenance
* Review checklist completion
* Review photos
* Review recorded readings
* Approve completed maintenance
* Reject maintenance
* Enter mandatory rejection reasons
* Review re-submitted work
* Review breakdown reports
* Review corrective actions
* View calibration alerts
* Receive escalation notifications
* View machine maintenance history

Supervisors shall not normally be permitted to:

* Change system security settings
* Delete approved maintenance history
* Change manager permissions

---

# 8. Manager Access

The Manager shall primarily use the Web Application.

The Manager shall be able to:

* View all machines
* View all users
* View all maintenance schedules
* Create preventive maintenance plans
* Modify maintenance plans
* View maintenance calendar
* View all work orders
* View overdue maintenance
* View escalated maintenance
* Review supervisor approvals
* Assign machines to technicians
* Reassign machine ownership
* View technician workload
* View maintenance compliance
* View breakdown history
* View downtime
* View corrective maintenance
* View calibration certificates
* View expiring calibration certificates
* View expired certificates
* Upload or manage compliance documents
* View external service reports
* Download service reports
* Generate management reports
* Export PDF reports
* Export Excel reports
* Configure notification rules
* Configure escalation rules
* Review audit logs
* Configure master data

---

# 9. Administrator Access

The Administrator shall use the Web Application.

The Administrator shall be able to:

* Create users
* Edit users
* Disable users
* Assign roles
* Reset user access
* Configure departments
* Configure machine categories
* Configure maintenance types
* Configure notification rules
* Configure escalation rules
* Maintain system master data
* Configure application settings

The Manager and Administrator may initially be the same user.

---

# 10. Authentication

The system shall require individual authentication.

The system should support:

* Unique username or email
* Password
* Secure password storage
* Password reset
* User activation and deactivation
* Session control
* Role-based authorization

Future functionality may include:

* Multi-factor authentication
* Microsoft login
* Google Workspace login

---

# 11. Mobile Application Requirements

The Mobile Application shall be designed for fast daily operational usage.

The interface shall minimize typing where possible.

The primary technician workflow shall be:

My Jobs
→ Select Work Order
→ Complete Checklist
→ Enter Readings
→ Take Photos
→ Add Comments
→ Submit

The primary supervisor workflow shall be:

Pending Approvals
→ Review Work
→ Review Evidence
→ Approve or Reject

---

# 12. Mobile Application Main Screens

The Mobile Application should include:

1. Login
2. Home Dashboard
3. My Jobs
4. My Machines
5. Work Order Details
6. Maintenance Checklist
7. Add Reading
8. Camera / Photo Upload
9. Document Upload
10. Submit Maintenance
11. Rejected Jobs
12. Breakdown Reporting
13. Corrective Maintenance
14. Pending Approvals
15. Approval Review
16. Notifications
17. Machine Details
18. Machine History
19. Calibration Status
20. User Profile

Screen visibility shall depend on user role.

---

# 13. Mobile Home Dashboard

## Technician Dashboard

The technician home screen should display:

* Jobs due today
* Upcoming jobs
* Overdue jobs
* Rejected jobs
* Breakdown jobs
* Notifications

## Supervisor Dashboard

The supervisor home screen should display:

* Jobs awaiting approval
* Overdue maintenance
* Escalated maintenance
* Technician workload
* Open breakdowns
* Calibration alerts
* Notifications

---

# 14. Mobile Notifications

The Mobile Application shall support push notifications.

Push notifications should be generated for:

* New work assignment
* Reassigned work
* Maintenance due soon
* Maintenance due today
* Overdue maintenance
* Supervisor approval request
* Work approval
* Work rejection
* Escalation
* Critical breakdown
* Calibration expiry
* Calibration expiration

---

# 15. Camera and Photo Capture

The Mobile Application shall allow users to use the mobile device camera directly.

Users shall be able to:

* Capture new photographs
* Select existing photographs
* Upload multiple images
* Preview images
* Delete images before submission
* Add descriptions where required

Photos may include:

* Equipment condition
* Damaged components
* Completed maintenance
* External service reports
* Certificates
* Replaced parts

---

# 16. Offline Consideration

The application should be designed with unstable connectivity in mind.

At minimum, the Mobile Application should:

* Handle interrupted uploads safely
* Avoid losing entered checklist data unexpectedly
* Display clear connectivity status
* Retry failed uploads where technically feasible

Full offline operation is not mandatory for the initial release unless required by operational conditions.

---

# 17. Manager Web Application Requirements

The Web Application shall provide a broader management interface than the Mobile Application.

The manager shall be able to monitor the complete maintenance operation from one interface.

---

# 18. Web Application Main Navigation

Recommended navigation:

1. Dashboard
2. Machines
3. Maintenance Plans
4. Work Orders
5. Maintenance Calendar
6. Breakdowns
7. Calibration
8. External Service
9. Documents
10. Employees
11. Reports
12. Notifications
13. Audit Logs
14. Settings

---

# 19. Manager Dashboard

The Manager Dashboard shall display operational exceptions and key indicators.

It should show:

* Total machines
* Operational machines
* Machines under maintenance
* Machines in breakdown
* Maintenance due today
* Maintenance due this week
* Overdue maintenance
* Maintenance awaiting approval
* Escalated maintenance
* Open breakdowns
* Calibration certificates expiring soon
* Expired calibration certificates
* Technician workload
* Maintenance compliance rate

The dashboard should emphasize items requiring management attention.

---

# 20. Machine Master Module

The manager shall maintain machine records using the Web Application.

Each machine shall have a unique Machine ID.

Machine information should include:

* Machine ID
* Asset number
* Machine name
* Machine category
* Manufacturer
* Model
* Serial number
* Department
* Location
* Installation date
* Commissioning date
* Supplier
* Service provider
* Warranty expiry
* Machine status
* Criticality
* Assigned machine owner
* Assigned supervisor
* Calibration requirement
* Preventive maintenance requirement
* Notes
* Machine photograph
* Supporting documents

---

# 21. Machine Status

Machine statuses should include:

* Operational
* Under Maintenance
* Breakdown
* Out of Service
* Standby
* Decommissioned

All important status changes shall be recorded in the audit log.

---

# 22. Machine Ownership

Each machine may be assigned to a primary technician.

The system shall distinguish between:

* Machine Owner
* Assigned Work Order Technician

Machine ownership represents ongoing responsibility.

Individual work orders may be temporarily assigned to another technician.

The manager and authorized supervisor shall be able to change assignments.

---

# 23. Preventive Maintenance Planning

Preventive Maintenance Plans shall be created and managed from the Web Application.

Each plan shall include:

* Machine
* Plan name
* Maintenance type
* Frequency
* Start date
* Next due date
* Assigned machine owner
* Assigned supervisor
* Maintenance checklist
* Mandatory readings
* Mandatory photos
* Instructions
* Estimated duration
* Priority
* Active/inactive status

---

# 24. Maintenance Frequency

The system should support:

* Daily
* Weekly
* Monthly
* Every two months
* Quarterly
* Half-yearly
* Yearly
* Custom days
* Custom weeks
* Custom months

Future versions may support:

* Machine operating hours
* Production cycles
* Usage counters

---

# 25. Maintenance Checklist Templates

Managers shall be able to create reusable checklist templates.

Checklist item types may include:

* Yes/No
* Pass/Fail
* Numerical reading
* Text input
* Photo required
* Confirmation
* Observation

Each checklist item may be configured as mandatory or optional.

---

# 26. Work Order Generation

The system shall automatically generate preventive maintenance work orders according to approved maintenance plans.

Each work order shall contain:

* Work order number
* Machine
* Maintenance plan
* Technician
* Supervisor
* Planned date
* Due date
* Priority
* Checklist
* Instructions
* Status

---

# 27. Work Order Status

Supported statuses should include:

* Planned
* Assigned
* Due
* In Progress
* Completed
* Awaiting Approval
* Approved
* Rejected
* Overdue
* Escalated
* Cancelled

Status changes shall be recorded in the audit trail.

---

# 28. Technician Maintenance Execution

The technician shall execute maintenance using the Mobile Application.

The technician shall be able to:

1. Open assigned work.
2. Start work.
3. Complete checklist items.
4. Enter readings.
5. Add comments.
6. Take photographs.
7. Upload supporting documents.
8. Record parts used.
9. Record defects found.
10. Complete the job.
11. Submit for supervisor approval.

The system shall prevent submission where mandatory requirements are incomplete.

---

# 29. Supervisor Approval Workflow

After technician submission, the work order shall change to:

**Awaiting Approval**

The supervisor shall receive a push notification.

The supervisor shall review:

* Checklist completion
* Technician observations
* Recorded readings
* Photographs
* Documents
* Spare parts used
* Additional defects
* Maintenance duration

The supervisor shall then select:

* Approve
* Reject

---

# 30. Maintenance Approval

When maintenance is approved:

* Work order status shall become Approved.
* Approval date shall be recorded.
* Supervisor identity shall be recorded.
* Maintenance history shall be updated.
* The next planned maintenance shall remain scheduled according to the maintenance plan.

---

# 31. Maintenance Rejection

When maintenance is rejected:

* A rejection reason shall be mandatory.
* Work order status shall become Rejected.
* Technician shall receive a notification.
* Technician shall be able to correct the work.
* Technician shall re-submit for approval.

Previous submissions and rejection comments should remain traceable.

---

# 32. Escalation Rules

The system shall automatically monitor overdue work.

Initial logic may be:

* Before due date: Technician reminder
* Due date: Technician reminder
* 1 day overdue: Technician and Supervisor
* 3 days overdue: Supervisor warning
* 5 days overdue: Manager escalation

Escalation intervals shall be configurable from the Web Application.

---

# 33. Manager Escalation Screen

The Manager Web Application shall provide a dedicated escalation view.

The manager shall be able to see:

* Work order
* Machine
* Technician
* Supervisor
* Due date
* Days overdue
* Current escalation level
* Last notification date
* Current status

---

# 34. Breakdown Reporting

Technicians and supervisors shall be able to report breakdowns through the Mobile Application.

Breakdown records should include:

* Breakdown number
* Machine
* Date
* Time
* Reported by
* Problem description
* Severity
* Machine stopped Yes/No
* Photos
* Initial observations
* Assigned technician
* Assigned supervisor

---

# 35. Breakdown Severity

Severity should support:

* Low
* Medium
* High
* Critical

Critical breakdowns shall immediately notify:

* Assigned supervisor
* Manager

---

# 36. Corrective Maintenance

The system shall support corrective maintenance after a breakdown.

Records may include:

* Fault description
* Root cause
* Corrective action
* Technician
* Start time
* Completion time
* Machine downtime
* Parts used
* External service required
* Photos
* Final comments
* Supervisor approval

---

# 37. External Service Management

External service details may be entered from either the Mobile Application or Web Application depending on user permissions.

External service records should include:

* Service record number
* Machine
* Service company
* Service technician
* Service date
* Service type
* Service description
* Findings
* Work completed
* Parts replaced
* Cost, optional
* Purchase order number, optional
* Invoice number, optional
* Follow-up date
* Next service recommendation
* Comments

---

# 38. External Service Report Capture

Users shall be able to photograph external service reports directly from the Mobile Application.

The application shall also support file upload.

Supported formats should include:

* PDF
* JPG
* JPEG
* PNG

Multiple attachments shall be permitted.

---

# 39. Calibration Management

Calibration management shall primarily be administered through the Manager Web Application.

Each calibration record shall include:

* Machine or instrument
* Calibration required Yes/No
* Certificate number
* Calibration provider
* Calibration date
* Expiry date
* Next calibration date
* Calibration result
* Remarks
* Certificate attachment
* Status

---

# 40. Calibration Status

Calibration status shall be automatically determined as:

* Valid
* Expiring Soon
* Expired
* Renewal in Progress
* Not Required

---

# 41. Calibration Reminder Rules

The default reminder schedule should be:

* 60 days before expiry
* 30 days before expiry
* 7 days before expiry
* On expiry

Reminder intervals shall be configurable.

Notifications may be sent to:

* Machine owner
* Supervisor
* Manager

depending on configured rules.

---

# 42. Calibration Renewal

When a new calibration is completed:

* New certificate shall be uploaded.
* New calibration date shall be recorded.
* New expiry date shall be entered.
* New certificate number shall be entered.
* Old certificates shall remain stored.

Historical calibration records shall never be overwritten.

---

# 43. Maintenance History

Each machine shall have a complete lifecycle history.

The history shall include:

* Preventive maintenance
* Corrective maintenance
* Breakdowns
* External service
* Calibration
* Spare parts usage
* Photos
* Documents
* Approval history
* Rejection history
* Comments

---

# 44. Document Management

The system shall maintain documents centrally.

Document records should include:

* Document title
* Document category
* Machine
* Related work order
* Related service
* Uploaded by
* Upload date
* Expiry date where relevant
* File attachment

---

# 45. Spare Parts Usage

Initial release functionality shall support basic spare-parts usage recording.

Technicians shall be able to record:

* Part name
* Part number
* Quantity
* Remarks

This shall be linked to the related maintenance work order.

---

# 46. Future Spare Parts Inventory

The future inventory module may include:

* Spare-part code
* Description
* Category
* Current stock
* Minimum stock
* Maximum stock
* Reorder quantity
* Store location
* Supplier
* Unit cost
* Compatible machines

Future alerts may include:

* Below minimum stock
* Out of stock
* Reorder required

---

# 47. Maintenance Calendar

The Manager Web Application shall include a maintenance calendar.

The calendar should support:

* Daily view
* Weekly view
* Monthly view

The manager shall be able to view maintenance by:

* Machine
* Technician
* Supervisor
* Status
* Due date

Selecting an activity shall open the related work order.

---

# 48. Search and Filtering

The Web Application shall provide advanced search and filtering.

Filters should include:

* Machine
* Machine ID
* Technician
* Supervisor
* Department
* Maintenance type
* Work order status
* Date range
* Calibration status
* Breakdown status
* Escalation level

---

# 49. Reporting

The Manager Web Application shall provide reporting functionality.

Reports shall support filtering, viewing, downloading, and export.

---

# 50. Preventive Maintenance Report

The Preventive Maintenance Report should include:

* Machine
* Work order
* Planned date
* Due date
* Completion date
* Technician
* Supervisor
* Status
* Approval date
* Days overdue

---

# 51. Machine History Report

The Machine History Report should include:

* Preventive maintenance
* Breakdown history
* Corrective maintenance
* External service
* Calibration
* Spare parts
* Maintenance dates
* Technician
* Supervisor
* Comments

---

# 52. Calibration Report

The Calibration Report should include:

* Machine
* Certificate number
* Calibration provider
* Calibration date
* Expiry date
* Remaining validity
* Status

---

# 53. Overdue Maintenance Report

The report shall include:

* Machine
* Work order
* Technician
* Supervisor
* Due date
* Days overdue
* Current status
* Escalation level

---

# 54. Technician Workload Report

The report may include:

* Jobs assigned
* Jobs completed
* Jobs awaiting approval
* Jobs rejected
* Jobs overdue

---

# 55. Breakdown Report

The report should include:

* Machine
* Breakdown date
* Severity
* Fault description
* Downtime
* Root cause
* Corrective action
* Technician
* Status

---

# 56. Monthly Management Summary

The system should provide:

* Total planned maintenance
* Completed maintenance
* Overdue maintenance
* Preventive maintenance compliance percentage
* Total breakdowns
* Total downtime
* External services performed
* Calibration certificates expiring
* Calibration certificates expired

---

# 57. Report Export

Reports shall be downloadable as:

* PDF
* Excel

Downloaded reports should include:

* Company name
* Report title
* Report period
* Generated date
* Generated by

---

# 58. Notifications

The system shall support:

* Mobile push notifications
* In-app notifications
* Email notifications where configured

Notification types shall include:

* Maintenance assignment
* Maintenance reassignment
* Upcoming maintenance
* Overdue maintenance
* Approval required
* Maintenance approved
* Maintenance rejected
* Escalation
* Critical breakdown
* Calibration expiry
* Calibration expired

---

# 59. Notification History

Notifications shall maintain:

* Recipient
* Message
* Notification type
* Date/time
* Related machine
* Related work order
* Read/unread status

---

# 60. Audit Trail

The backend shall maintain an audit trail.

The audit log shall record:

* Date
* Time
* User
* Action
* Record affected
* Machine
* Work order
* Previous value
* New value

Examples include:

* Work reassigned
* Machine owner changed
* Maintenance approved
* Maintenance rejected
* Machine status changed
* Calibration updated
* User permission changed

---

# 61. Data Retention

Maintenance and calibration records shall remain available throughout the machine lifecycle.

Approved maintenance records shall not be permanently deleted by ordinary users.

Decommissioned machines shall remain available for historical reports.

---

# 62. File and Photo Storage

Photos and documents shall be stored in secure file storage.

The database shall store references linking files to:

* Machine
* Work order
* Breakdown
* External service
* Calibration certificate

Large files should not be stored directly inside the primary relational database.

---

# 63. Security Requirements

The application shall implement:

* Secure authentication
* Role-based authorization
* HTTPS communication
* Secure password storage
* Restricted file access
* User activity logging
* Session management
* Database backup
* File backup

---

# 64. Backup Requirements

The production environment should provide:

* Automated daily database backup
* File storage redundancy
* Backup monitoring
* Data restoration procedure

---

# 65. Performance Requirements

The system shall be sized for approximately:

* 40 machines
* 20 employees/technicians
* 2 supervisors
* 1 manager

The system should be responsive under normal use.

Typical application screens should load within a few seconds under reasonable network conditions.

---

# 66. Usability Requirements

The Mobile Application shall prioritize speed and simplicity.

Technicians should not need to navigate management functions.

The Manager Web Application shall prioritize visibility, planning, exception management, and reporting.

Role-based interfaces shall display only relevant functions.

---

# 67. Technical Architecture Recommendation

A recommended architecture is:

## Mobile

Flutter or React Native

## Web

React or Next.js

## Backend

.NET, Node.js, or Django

## Database

PostgreSQL

## File Storage

Azure Blob Storage, Amazon S3, or equivalent

## Notifications

Firebase Cloud Messaging or equivalent

## Reports

Server-generated PDF and Excel files

The final stack should be selected based on development capability, infrastructure, security requirements, and long-term support.

---

# 68. Core Database Entities

The database is expected to include entities such as:

* Users
* Roles
* UserRoles
* Machines
* MachineAssignments
* MaintenancePlans
* ChecklistTemplates
* ChecklistItems
* WorkOrders
* WorkOrderTasks
* WorkOrderReadings
* WorkOrderPhotos
* Approvals
* Rejections
* Breakdowns
* CorrectiveActions
* ExternalServices
* CalibrationCertificates
* Documents
* Notifications
* Escalations
* SpareParts
* SparePartUsage
* AuditLogs

---

# 69. Core Data Relationship

The Machine shall act as the central asset record.

All major records shall link back to a machine, including:

* Preventive maintenance
* Corrective maintenance
* Breakdown
* Calibration
* External service
* Documents
* Spare parts
* Ownership

---

# 70. Core Workflow: Preventive Maintenance

Maintenance Plan Created in Web Application
→ System Generates Work Order
→ Technician Receives Mobile Notification
→ Technician Performs Maintenance
→ Technician Completes Checklist
→ Technician Uploads Evidence
→ Technician Submits
→ Supervisor Receives Mobile Notification
→ Supervisor Reviews
→ Supervisor Approves
→ Maintenance History Updated

---

# 71. Core Workflow: Rejected Maintenance

Technician Submits
→ Supervisor Reviews
→ Supervisor Rejects
→ Rejection Reason Recorded
→ Technician Notified
→ Technician Corrects Work
→ Technician Re-submits
→ Supervisor Reviews Again

---

# 72. Core Workflow: Overdue Maintenance

Work Order Due
→ Technician Reminder
→ Maintenance Not Completed
→ Work Order Becomes Overdue
→ Supervisor Notification
→ Continued Delay
→ Manager Escalation

---

# 73. Core Workflow: Calibration Renewal

Certificate Uploaded
→ Expiry Monitored
→ Reminder Generated
→ Manager / Supervisor Alerted
→ Calibration Renewed
→ New Certificate Uploaded
→ Previous Certificate Archived
→ New Expiry Monitored

---

# 74. Core Workflow: Breakdown

Technician Reports Breakdown on Mobile
→ Supervisor Notified
→ Technician Assigned
→ Repair Conducted
→ Corrective Action Recorded
→ Downtime Recorded
→ Photos Uploaded
→ Supervisor Reviews
→ Machine Returned to Service

---

# 75. Core Workflow: External Service

External Service Required
→ Service Provider Attends
→ Service Activity Recorded
→ Technician/Supervisor Photographs or Uploads Report
→ Report Stored Against Machine
→ Findings Recorded
→ Follow-Up Date Recorded

---

# 76. Phase 1 Development

Phase 1 shall include the essential maintenance workflow.

### Mobile Application

* Login
* Technician dashboard
* Supervisor dashboard
* My Machines
* My Jobs
* Work order details
* Maintenance checklist
* Readings
* Photos
* Document upload
* Maintenance submission
* Supervisor approval
* Rejection workflow
* Notifications
* Basic breakdown reporting

### Web Application

* Manager login
* Dashboard
* Machine master
* User management
* Machine ownership
* Maintenance plans
* Maintenance checklist templates
* Maintenance calendar
* Work orders
* Escalation monitoring
* Basic reports
* Audit trail

---

# 77. Phase 2 Development

Phase 2 should add:

* Calibration management
* Calibration reminders
* External service management
* Advanced document management
* Breakdown and corrective maintenance
* Enhanced reporting

---

# 78. Phase 3 Development

Phase 3 should add:

* Downtime analytics
* Root-cause analysis
* Maintenance KPIs
* Technician workload analytics
* Machine reliability analysis
* Advanced dashboards

---

# 79. Phase 4 Development

Phase 4 should introduce:

* Spare-part master
* Stock balance
* Minimum stock
* Maximum stock
* Reorder level
* Stock receipts
* Stock issues
* Supplier management
* Inventory alerts

---

# 80. Out-of-Scope for Initial Release

The initial release should not include:

* ERP integration
* Full purchase-order integration
* IoT machine integration
* PLC integration
* Predictive AI maintenance
* GPS employee tracking
* Payroll integration
* Complex costing
* WhatsApp integration

These items should only be considered after the core system is operational and stable.

---

# 81. Acceptance Criteria

The initial system shall be considered functionally acceptable when:

1. Technicians can log into the Mobile Application.
2. Supervisors can log into the Mobile Application.
3. Managers can log into the Web Application.
4. Machines can be created and maintained from the Web Application.
5. Machine owners can be assigned.
6. Preventive maintenance plans can be created.
7. Work orders can be automatically generated.
8. Technicians receive work notifications.
9. Technicians can complete maintenance using mobile devices.
10. Technicians can take and upload photos.
11. Technicians can submit completed maintenance.
12. Supervisors can approve or reject maintenance.
13. Rejected work can be corrected and re-submitted.
14. Overdue work is automatically escalated.
15. Managers can view overdue and escalated work.
16. Machine maintenance history can be retrieved.
17. Calibration certificates can be stored.
18. Calibration expiry reminders can be generated.
19. External service reports can be stored.
20. Breakdown records can be created.
21. Reports can be downloaded.
22. Major actions are recorded in the audit trail.

---

# 82. Success Criteria

The system should result in:

* Reduced missed maintenance
* Improved technician accountability
* Faster supervisor approval
* Better maintenance traceability
* Improved calibration compliance
* Centralized machine documentation
* Better control of overdue work
* Faster access to service reports
* Improved maintenance reporting
* Reliable machine lifecycle history

---

# 83. Final Functional Principle

The system shall maintain a clear separation between operational and management activities.

For Technicians:

**Receive Task → Perform Maintenance → Record Evidence → Submit**

For Supervisors:

**Review → Approve or Reject → Escalate Where Required**

For Managers:

**Plan → Monitor → Control → Escalate → Analyze → Report**

The Mobile Application shall remain simple and task-oriented.

The Manager Web Application shall provide centralized visibility, planning, reporting, administration, and control.
