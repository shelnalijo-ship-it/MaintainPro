# Maintenance Management Application

## UI Specification, Workflow, Database Schema and API Requirements

**Version:** 3.0
**Application:** MaintainPro
**Architecture:** Mobile App + Manager Web Portal + Shared Backend
**Theme:** Kerala Onam Inspired Corporate UI

---

# 1. Purpose

This document converts the approved Functional Requirements Document and UI concept into a development-ready functional design.

It defines:

* Mobile application screens
* Manager web portal screens
* Role-based navigation
* User interactions
* Validation rules
* Approval workflow
* Escalation workflow
* Database entities
* Database relationships
* API requirements
* Notification events
* Reporting requirements
* Recommended implementation order

The purpose is to reduce interpretation by developers and provide a single reference for application development.

---

# 2. System Architecture

The system shall contain four primary technical layers.

```text
Mobile Application
Technicians + Supervisors
        |
        |
        v
Shared Backend API
        |
        +-------------------+
        |                   |
        v                   v
PostgreSQL Database    File Storage
                            |
                            v
                     Photos / PDF /
                     Certificates
        |
        +-------------------+
        |
        v
Notification Service
        |
        v
Push / In-App / Email

Manager Web Portal
        |
        +---- Uses Same Backend API
```

The mobile and web applications shall never maintain separate copies of core business logic.

All business rules shall be enforced by the backend.

---

# 3. UI Design System

## 3.1 Primary Colors

The approved Kerala Onam-inspired design shall use the following conceptual palette.

### Primary Green

Used for:

* Main buttons
* Active navigation
* Approved status
* Normal machine status
* Primary actions

Recommended visual reference:

**Deep Banana Leaf Green**

Approximate digital color:

`#165B3A`

---

### Secondary Green

Used for:

* Background highlights
* Success cards
* Secondary information

Approximate color:

`#DDEADF`

---

### Kasavu Gold

Used for:

* Highlights
* Section borders
* Icons
* Calendar indicators
* Decorative separators

Approximate color:

`#C99A3D`

---

### Deep Maroon

Used for:

* Branding accent
* Important action emphasis
* Critical visual highlights

Approximate color:

`#851B2B`

---

### Ivory

Primary application background.

Approximate color:

`#FAF7EF`

---

### Soft Terracotta

Used for secondary cultural decorative elements.

Approximate color:

`#C96F4A`

---

### Critical Red

Used only for:

* Overdue
* Breakdown
* Rejected
* Expired certificate
* Critical alerts

---

# 4. UI Principles

The Onam theme shall remain subtle.

The application must not look like an event or festival application.

Cultural elements may be used in:

* Login screen
* Header accents
* Empty states
* Decorative card backgrounds
* Footer sections
* Splash screen

Operational screens should remain primarily functional.

Pookalam graphics, banana leaves, Kerala lamps and kasavu-inspired borders should not reduce readability.

---

# 5. Mobile Application Roles

The Mobile Application shall support:

### Technician

Primary operational user.

### Supervisor

Operational + approval user.

The application shall dynamically change functions based on the user's role.

---

# 6. Mobile Navigation

Recommended persistent bottom navigation:

```text
Home
My Jobs
Machines
Notifications
More
```

Supervisor accounts may additionally show:

```text
Home
Jobs
Approvals
Machines
More
```

The interface should avoid more than five primary navigation items.

---

# 7. Mobile Screen M01

## Splash Screen

### Purpose

Displayed when the application launches.

### Elements

* MaintainPro logo
* Kerala-inspired subtle visual motif
* Application name
* Loading indicator

### Behavior

Application checks:

* Existing login token
* User status
* Role
* Network availability

### Routing

If valid session:

→ Home Dashboard

If no valid session:

→ Login

---

# 8. Mobile Screen M02

## Login

### Fields

* Username / email
* Password

### Actions

* Sign In
* Forgot Password
* Show / hide password

Optional future:

* Microsoft login
* Google login

### Validation

* Username required
* Password required
* Account must be active

### Error Messages

Examples:

* Invalid username or password
* Account disabled
* Unable to connect
* Session expired

---

# 9. Mobile Screen M03

## Technician Dashboard

### Header

Display:

* Technician name
* Technician profile photo, optional
* Role
* Notification icon

### KPI Cards

Display:

* Due Today
* Upcoming
* Overdue
* Rejected
* Notifications

### Quick Actions

Recommended:

* My Jobs
* Report Breakdown
* My Machines
* Scan Machine QR, future
* Upload Service Report

### Recent Activity

Show last five:

* Work orders
* Approvals
* Rejections
* Breakdown assignments

---

# 10. Mobile Screen M04

## Supervisor Dashboard

Display:

* Awaiting Approval
* Overdue Jobs
* Escalated Jobs
* Open Breakdowns
* Calibration Alerts
* Technician Workload

### Quick Actions

* Pending Approvals
* Assign Job
* Reassign Job
* Breakdown Review
* View Team

---

# 11. Mobile Screen M05

## My Jobs

### Tabs

* All
* Due Today
* Upcoming
* In Progress
* Overdue
* Rejected

### Job Card

Each card shall show:

* Work order number
* Machine
* Maintenance type
* Priority
* Status
* Due date
* Machine location

### Sorting

Default:

1. Overdue
2. Due Today
3. Upcoming

### Filtering

Filter by:

* Machine
* Priority
* Status
* Date

---

# 12. Mobile Screen M06

## Work Order Details

### Information

* Work Order ID
* Machine
* Machine ID
* Location
* Maintenance type
* Priority
* Due date
* Assigned technician
* Assigned supervisor
* Maintenance description

### Actions

Depending on status:

**Assigned**

→ Start Job

**In Progress**

→ Continue

**Rejected**

→ View Rejection

**Awaiting Approval**

→ View Submitted Work

---

# 13. Mobile Screen M07

## Maintenance Checklist

Checklist item types:

* Checkbox
* Yes / No
* Pass / Fail
* Numerical reading
* Text
* Photo required

Example:

```text
☑ Check lubrication

☑ Clean filter

☐ Inspect belt

Pressure:
[ 6.2 ] bar

Vibration:
[ Normal ▼ ]
```

### Mandatory Items

Mandatory items shall display a clear indicator.

The job cannot be submitted until all mandatory tasks are complete.

---

# 14. Mobile Screen M08

## Meter / Measurement Entry

Supported inputs:

* Numeric
* Decimal
* Temperature
* Pressure
* Voltage
* Current
* Running hours
* Free text

Each measurement may optionally define:

* Minimum allowed value
* Maximum allowed value
* Unit

Example:

```text
Discharge Pressure

Expected:
5.5 - 7.5 bar

Measured:
[ 6.2 ]

Status:
Within Range
```

If outside range:

System should visually alert the technician.

---

# 15. Mobile Screen M09

## Maintenance Photos

Technician may:

* Take photo
* Upload from gallery
* Preview photo
* Delete before submission
* Add description

Photo categories may include:

* Before Maintenance
* During Maintenance
* After Maintenance
* Defect
* Replaced Part
* Service Report

---

# 16. Mobile Screen M10

## Review and Submit

Before submission display:

* Checklist completion
* Measurements
* Photos
* Parts used
* Comments
* Defects found
* Duration

### Actions

* Save Draft
* Submit for Approval

### Validation

Submission blocked when:

* Mandatory checklist incomplete
* Required photo missing
* Mandatory reading missing

---

# 17. Mobile Screen M11

## Rejected Job

Display:

* Supervisor name
* Rejection date
* Rejection reason
* Original submission
* Missing/incorrect elements

Actions:

* Edit Work
* Add Evidence
* Re-submit

The original rejection shall remain in history.

---

# 18. Mobile Screen M12

## Supervisor Pending Approvals

List shall show:

* Work order
* Machine
* Technician
* Submission date/time
* Priority
* Current delay

Sorting priority:

1. Critical
2. Overdue
3. Oldest submission

---

# 19. Mobile Screen M13

## Supervisor Approval Review

Supervisor shall see:

* Technician
* Machine
* Checklist
* Measurements
* Photos
* Parts used
* Comments
* Defects
* Completion duration

### Actions

**Approve**

or

**Reject**

### Reject Requirement

Reason mandatory.

Optional predefined rejection categories:

* Missing photo
* Checklist incomplete
* Reading incorrect
* Additional work required
* Documentation incomplete
* Other

---

# 20. Mobile Screen M14

## My Machines

List machines assigned to the technician.

Display:

* Machine ID
* Machine name
* Location
* Machine status
* Next maintenance date
* Calibration status

---

# 21. Mobile Screen M15

## Machine Detail

Display:

* Machine photograph
* Asset ID
* Manufacturer
* Model
* Serial number
* Location
* Status
* Machine owner
* Supervisor
* Next PM date
* Calibration status

Tabs:

* Overview
* Maintenance
* Calibration
* Documents
* Breakdowns

---

# 22. Mobile Screen M16

## Breakdown Reporting

Fields:

* Machine
* Date/time
* Severity
* Machine stopped Yes/No
* Description
* Photos
* Initial observation

Severity:

* Low
* Medium
* High
* Critical

Critical breakdown shall immediately trigger escalation.

---

# 23. Mobile Screen M17

## Corrective Maintenance

Fields:

* Breakdown
* Root cause
* Action taken
* Parts used
* Start time
* Completion time
* Downtime
* Photos
* Comments

Submit to supervisor for closure.

---

# 24. Mobile Screen M18

## External Service Report

Fields:

* Machine
* Service company
* Service technician
* Service date
* Description
* Findings
* Recommendation
* Follow-up date

Attachments:

* Camera photo
* PDF
* JPG
* PNG

---

# 25. Mobile Screen M19

## Notifications

Notification categories:

* Maintenance
* Approval
* Rejection
* Breakdown
* Calibration
* Escalation
* System

Actions:

* Mark as read
* Open related record

---

# 26. Web Portal Navigation

Manager portal sidebar:

```text
Dashboard
Machines
Maintenance Plans
Work Orders
Calendar
Breakdowns
Calibration
External Services
Documents
Reports
Employees
Audit Logs
Settings
```

---

# 27. Web Screen W01

## Manager Dashboard

### KPI Cards

* Total Machines
* Due Today
* Upcoming PM
* Overdue
* Awaiting Approval
* Open Breakdowns
* Expired Calibration
* Expiring Calibration

### Charts

* Maintenance compliance
* PM completed vs overdue
* Breakdown by machine
* Technician workload
* Calibration compliance

### Action Panels

* Escalation Alerts
* Upcoming Calibration
* Recent Breakdowns
* Pending Supervisor Approvals

---

# 28. Web Screen W02

## Machine Master

### Main Table

Columns:

* Machine ID
* Machine Name
* Category
* Department
* Location
* Status
* Calibration
* Owner
* Supervisor
* Next PM
* Actions

### Filters

* Location
* Category
* Machine status
* Calibration status
* Technician

### Actions

* Add Machine
* Edit
* View
* Decommission
* Assign Owner

---

# 29. Web Screen W03

## Add / Edit Machine

Fields:

### Identification

* Machine ID
* Asset number
* Name
* Category
* Manufacturer
* Model
* Serial number

### Location

* Department
* Location

### Dates

* Installation date
* Commissioning date
* Warranty expiry

### Responsibility

* Machine owner
* Supervisor

### Compliance

* Calibration required
* PM required

### Attachments

* Machine photo
* Manual
* Datasheet
* Other document

---

# 30. Web Screen W04

## Maintenance Plans

Table:

* Plan ID
* Machine
* Maintenance name
* Frequency
* Technician
* Supervisor
* Next due
* Status

Actions:

* Add Plan
* Duplicate Plan
* Edit
* Disable

---

# 31. Web Screen W05

## Maintenance Plan Editor

Sections:

### General

* Machine
* Plan name
* Maintenance type
* Priority

### Schedule

* Frequency type
* Frequency interval
* Start date
* Next due date

### Assignment

* Default technician
* Supervisor

### Checklist

Manager can:

* Add item
* Reorder item
* Make mandatory
* Define response type
* Define expected limits

### Evidence

Configure:

* Photo required
* Number of photos
* Comment mandatory

---

# 32. Web Screen W06

## Work Orders

Table:

* WO number
* Machine
* Maintenance type
* Technician
* Supervisor
* Planned date
* Due date
* Status
* Priority

Filters:

* Technician
* Supervisor
* Machine
* Date
* Status
* Priority

---

# 33. Web Screen W07

## Maintenance Calendar

Views:

* Month
* Week
* Day

Event colors shall reflect status.

Example:

Green = Approved

Gold = Upcoming

Red = Overdue

Blue = Scheduled

Maroon = Breakdown

Clicking an event shall open the work order side panel.

---

# 34. Web Screen W08

## Work Order Detail

Display:

* Full job information
* Checklist
* Technician submission
* Attachments
* Approval history
* Escalation history

Manager may:

* Reassign technician
* Change due date, with audit log
* Cancel work
* Add manager comment

---

# 35. Web Screen W09

## Breakdown Management

Table:

* Breakdown ID
* Machine
* Severity
* Reported date
* Reported by
* Technician
* Downtime
* Status

Statuses:

* Reported
* Assigned
* In Progress
* Awaiting Approval
* Closed

---

# 36. Web Screen W10

## Calibration Dashboard

KPI:

* Total certificates
* Valid
* Expiring in 60 days
* Expiring in 30 days
* Expiring in 7 days
* Expired

Table:

* Machine
* Certificate
* Provider
* Calibration date
* Expiry date
* Status
* Attachment

---

# 37. Web Screen W11

## Add Calibration Certificate

Fields:

* Machine
* Certificate number
* Provider
* Calibration date
* Expiry date
* Result
* Comments
* Certificate upload

When saved:

Previous certificate remains unchanged.

---

# 38. Web Screen W12

## External Services

Table:

* Record number
* Machine
* Service provider
* Service date
* Service type
* Follow-up
* Documents

Actions:

* Add
* View
* Download attachments

---

# 39. Web Screen W13

## Employees

Columns:

* Name
* Employee ID
* Role
* Department
* Active status
* Assigned machines
* Open jobs

Actions:

* Create
* Edit
* Disable
* Reset access
* Assign role

---

# 40. Web Screen W14

## Reports

Report categories:

* Preventive Maintenance
* Overdue Maintenance
* Machine History
* Breakdown
* Calibration
* Technician Workload
* Monthly Summary

Filters should remain consistent.

Exports:

* PDF
* Excel

---

# 41. Web Screen W15

## Audit Logs

Columns:

* Timestamp
* User
* Action
* Entity
* Record
* Previous value
* New value

Audit logs shall be read-only.

---

# 42. Web Screen W16

## Settings

Configuration categories:

### Notifications

* Reminder intervals
* Email enabled
* Push enabled

### Escalations

* Technician delay threshold
* Supervisor escalation
* Manager escalation

### Master Data

* Departments
* Locations
* Machine categories
* Maintenance types
* Priorities

---

# 43. Core Clickable Workflow

## Technician Preventive Maintenance

```text
Login
  ↓
Dashboard
  ↓
My Jobs
  ↓
Work Order
  ↓
Start Job
  ↓
Checklist
  ↓
Readings
  ↓
Photos
  ↓
Review
  ↓
Submit
  ↓
Awaiting Supervisor Approval
```

---

# 44. Approval Workflow

```text
Technician Submits
        ↓
Supervisor Notification
        ↓
Supervisor Opens Approval
        ↓
Review Checklist + Evidence
        ↓
   +------------+
   |            |
Approve       Reject
   |            |
   ↓            ↓
Closed      Technician
            Notified
                ↓
             Correct
                ↓
            Re-submit
```

---

# 45. Escalation Workflow

```text
Work Order Created
       ↓
Reminder Before Due Date
       ↓
Due Date
       ↓
Not Completed
       ↓
1 Day Overdue
Technician + Supervisor
       ↓
3 Days Overdue
Supervisor Escalation
       ↓
5 Days Overdue
Manager Escalation
```

The exact intervals must be configurable.

---

# 46. Calibration Workflow

```text
Certificate Added
      ↓
System Monitors Expiry
      ↓
60-Day Reminder
      ↓
30-Day Reminder
      ↓
7-Day Reminder
      ↓
Expiry
      ↓
Escalation
      ↓
New Calibration
      ↓
Upload New Certificate
      ↓
Archive Previous Certificate
```

---

# 47. Core Database Design

The database shall use a relational model.

Recommended database:

**PostgreSQL**

---

# 48. USERS Table

Fields:

```text
id
employee_id
first_name
last_name
email
mobile
password_hash
role_id
department_id
is_active
created_at
updated_at
last_login_at
```

---

# 49. ROLES Table

```text
id
name
description
```

Values:

* TECHNICIAN
* SUPERVISOR
* MANAGER
* ADMIN

---

# 50. DEPARTMENTS Table

```text
id
name
description
is_active
```

---

# 51. LOCATIONS Table

```text
id
name
department_id
description
is_active
```

---

# 52. MACHINES Table

```text
id
machine_code
asset_number
name
category_id
manufacturer
model
serial_number
department_id
location_id
installation_date
commissioning_date
warranty_expiry_date
status
criticality
calibration_required
pm_required
machine_owner_user_id
supervisor_user_id
photo_file_id
notes
is_active
created_at
updated_at
```

---

# 53. MACHINE_CATEGORIES Table

```text
id
name
description
```

---

# 54. MACHINE_ASSIGNMENT_HISTORY

```text
id
machine_id
technician_id
supervisor_id
effective_from
effective_to
assigned_by
reason
```

This prevents losing historical ownership.

---

# 55. MAINTENANCE_PLANS Table

```text
id
machine_id
plan_name
maintenance_type_id
priority
frequency_type
frequency_value
start_date
next_due_date
estimated_duration_minutes
default_technician_id
supervisor_id
instructions
photo_required
minimum_photo_count
is_active
created_by
created_at
updated_at
```

---

# 56. CHECKLIST_TEMPLATES Table

```text
id
maintenance_plan_id
name
version
is_active
created_at
```

---

# 57. CHECKLIST_ITEMS Table

```text
id
checklist_template_id
sequence_number
title
description
response_type
is_mandatory
unit
minimum_value
maximum_value
photo_required
```

Possible response_type values:

* BOOLEAN
* PASS_FAIL
* NUMBER
* TEXT
* PHOTO
* CONFIRMATION

---

# 58. WORK_ORDERS Table

```text
id
work_order_number
machine_id
maintenance_plan_id
assigned_technician_id
supervisor_id
planned_date
due_date
priority
status
started_at
completed_at
submitted_at
approved_at
approved_by
rejection_reason
escalation_level
created_at
updated_at
```

---

# 59. WORK_ORDER_CHECKLIST_RESULTS

```text
id
work_order_id
checklist_item_id
boolean_value
numeric_value
text_value
pass_fail_value
comment
completed_at
completed_by
```

---

# 60. WORK_ORDER_PHOTOS

```text
id
work_order_id
file_id
photo_type
description
uploaded_by
uploaded_at
```

---

# 61. WORK_ORDER_APPROVALS

```text
id
work_order_id
supervisor_id
decision
remarks
decision_at
submission_version
```

decision:

* APPROVED
* REJECTED

---

# 62. BREAKDOWNS

```text
id
breakdown_number
machine_id
reported_by
reported_at
severity
machine_stopped
description
initial_observation
assigned_technician_id
supervisor_id
status
closed_at
```

---

# 63. CORRECTIVE_ACTIONS

```text
id
breakdown_id
root_cause
corrective_action
started_at
completed_at
downtime_minutes
technician_id
supervisor_id
approval_status
comments
```

---

# 64. CALIBRATION_CERTIFICATES

```text
id
machine_id
certificate_number
provider
calibration_date
expiry_date
result
status
file_id
comments
uploaded_by
created_at
```

No old certificate shall be overwritten.

---

# 65. EXTERNAL_SERVICES

```text
id
service_number
machine_id
service_company
service_technician
service_date
service_type
description
findings
parts_replaced
cost
purchase_order_number
invoice_number
follow_up_date
next_service_date
comments
created_by
created_at
```

---

# 66. FILES

```text
id
storage_key
original_filename
mime_type
file_size
uploaded_by
uploaded_at
```

Physical files shall be stored externally.

---

# 67. SPARE_PART_USAGE

For Phase 1:

```text
id
work_order_id
part_name
part_number
quantity
remarks
created_by
```

---

# 68. NOTIFICATIONS

```text
id
user_id
notification_type
title
message
entity_type
entity_id
is_read
sent_at
read_at
```

---

# 69. ESCALATIONS

```text
id
work_order_id
level
triggered_at
recipient_user_id
notification_id
resolved_at
```

---

# 70. AUDIT_LOGS

```text
id
user_id
action
entity_type
entity_id
old_value_json
new_value_json
ip_address
device_info
created_at
```

Audit records shall not be editable through the application.

---

# 71. Key Database Relationships

```text
USER
 |
 +------ MACHINE OWNER

MACHINE
 |
 +------ MAINTENANCE PLAN
 |            |
 |            +------ CHECKLIST
 |
 +------ WORK ORDER
 |            |
 |            +------ CHECKLIST RESULTS
 |            +------ PHOTOS
 |            +------ APPROVALS
 |
 +------ BREAKDOWNS
 |
 +------ CALIBRATION CERTIFICATES
 |
 +------ EXTERNAL SERVICES
 |
 +------ DOCUMENTS
```

The machine is the central entity.

---

# 72. API Design Principles

Recommended API style:

**REST API**

Base structure:

```text
/api/v1/
```

All endpoints shall require authentication except login and password-reset endpoints.

---

# 73. Authentication APIs

```text
POST /api/v1/auth/login
POST /api/v1/auth/logout
POST /api/v1/auth/refresh
POST /api/v1/auth/forgot-password
POST /api/v1/auth/reset-password
GET  /api/v1/auth/me
```

---

# 74. User APIs

```text
GET    /api/v1/users
POST   /api/v1/users
GET    /api/v1/users/{id}
PUT    /api/v1/users/{id}
PATCH  /api/v1/users/{id}/status
GET    /api/v1/users/{id}/machines
GET    /api/v1/users/{id}/work-orders
```

---

# 75. Machine APIs

```text
GET    /api/v1/machines
POST   /api/v1/machines
GET    /api/v1/machines/{id}
PUT    /api/v1/machines/{id}
PATCH  /api/v1/machines/{id}/status
POST   /api/v1/machines/{id}/assign-owner
GET    /api/v1/machines/{id}/history
GET    /api/v1/machines/{id}/documents
```

---

# 76. Maintenance Plan APIs

```text
GET    /api/v1/maintenance-plans
POST   /api/v1/maintenance-plans
GET    /api/v1/maintenance-plans/{id}
PUT    /api/v1/maintenance-plans/{id}
PATCH  /api/v1/maintenance-plans/{id}/status
```

---

# 77. Checklist APIs

```text
GET    /api/v1/maintenance-plans/{id}/checklist
POST   /api/v1/maintenance-plans/{id}/checklist-items
PUT    /api/v1/checklist-items/{id}
DELETE /api/v1/checklist-items/{id}
```

Checklist deletion shall only be allowed before associated work history exists.

---

# 78. Work Order APIs

```text
GET    /api/v1/work-orders
POST   /api/v1/work-orders
GET    /api/v1/work-orders/{id}
PATCH  /api/v1/work-orders/{id}/start
PATCH  /api/v1/work-orders/{id}/assign
PATCH  /api/v1/work-orders/{id}/reassign
POST   /api/v1/work-orders/{id}/checklist-results
POST   /api/v1/work-orders/{id}/photos
POST   /api/v1/work-orders/{id}/parts
POST   /api/v1/work-orders/{id}/submit
POST   /api/v1/work-orders/{id}/approve
POST   /api/v1/work-orders/{id}/reject
```

---

# 79. Breakdown APIs

```text
GET    /api/v1/breakdowns
POST   /api/v1/breakdowns
GET    /api/v1/breakdowns/{id}
PATCH  /api/v1/breakdowns/{id}/assign
POST   /api/v1/breakdowns/{id}/corrective-action
POST   /api/v1/breakdowns/{id}/close
```

---

# 80. Calibration APIs

```text
GET    /api/v1/calibrations
POST   /api/v1/calibrations
GET    /api/v1/calibrations/{id}
GET    /api/v1/machines/{id}/calibrations
POST   /api/v1/calibrations/{id}/certificate
```

---

# 81. External Service APIs

```text
GET    /api/v1/external-services
POST   /api/v1/external-services
GET    /api/v1/external-services/{id}
PUT    /api/v1/external-services/{id}
POST   /api/v1/external-services/{id}/attachments
```

---

# 82. Notification APIs

```text
GET    /api/v1/notifications
GET    /api/v1/notifications/unread-count
PATCH  /api/v1/notifications/{id}/read
POST   /api/v1/notifications/read-all
```

---

# 83. Dashboard APIs

Technician:

```text
GET /api/v1/dashboard/technician
```

Supervisor:

```text
GET /api/v1/dashboard/supervisor
```

Manager:

```text
GET /api/v1/dashboard/manager
```

Do not make the frontend calculate management KPIs independently.

The backend shall calculate and return KPI values.

---

# 84. Reporting APIs

```text
GET /api/v1/reports/preventive-maintenance
GET /api/v1/reports/overdue-maintenance
GET /api/v1/reports/machine-history
GET /api/v1/reports/calibration
GET /api/v1/reports/breakdowns
GET /api/v1/reports/technician-workload
GET /api/v1/reports/monthly-summary
```

Exports:

```text
GET /api/v1/reports/{report}/export/pdf
GET /api/v1/reports/{report}/export/excel
```

---

# 85. File Upload API

Recommended method:

```text
POST /api/v1/files
```

Response:

```text
file_id
filename
mime_type
size
url
```

The application should upload the file once and associate its `file_id` with the required record.

---

# 86. API Security

The backend shall enforce permissions independently from frontend visibility.

Example:

Even if a technician manually calls:

```text
POST /work-orders/{id}/approve
```

the API must return:

```text
403 Forbidden
```

Role security shall never rely only on hiding buttons.

---

# 87. Notification Events

The system shall automatically create notifications for:

```text
WORK_ORDER_ASSIGNED
WORK_ORDER_REASSIGNED
WORK_ORDER_DUE_SOON
WORK_ORDER_DUE
WORK_ORDER_OVERDUE
WORK_ORDER_SUBMITTED
WORK_ORDER_APPROVED
WORK_ORDER_REJECTED

BREAKDOWN_REPORTED
BREAKDOWN_CRITICAL

CALIBRATION_60_DAY
CALIBRATION_30_DAY
CALIBRATION_7_DAY
CALIBRATION_EXPIRED

ESCALATION_SUPERVISOR
ESCALATION_MANAGER
```

---

# 88. Push Notification Content Example

```text
Preventive Maintenance Due

WO-1045
Air Compressor AC-01

Due Today
```

Clicking the notification shall open the relevant work order.

---

# 89. Work Order Number Format

Recommended format:

```text
WO-2026-0001
WO-2026-0002
```

Breakdown:

```text
BD-2026-0001
```

External Service:

```text
ES-2026-0001
```

---

# 90. Status Model

Work Order:

```text
PLANNED
ASSIGNED
IN_PROGRESS
AWAITING_APPROVAL
APPROVED
REJECTED
OVERDUE
ESCALATED
CANCELLED
```

Machine:

```text
OPERATIONAL
UNDER_MAINTENANCE
BREAKDOWN
OUT_OF_SERVICE
STANDBY
DECOMMISSIONED
```

---

# 91. Data Deletion Rules

The system should not physically delete operational history.

Use soft deletion / inactive status for:

* Users
* Machines
* Maintenance plans

Do not delete:

* Approved maintenance work
* Calibration certificates
* Breakdown records
* Audit logs

---

# 92. Recommended Development Sequence

## Sprint Group 1

### Foundation

Build:

* Authentication
* User roles
* Database
* File storage
* Mobile navigation
* Web navigation

---

## Sprint Group 2

### Machine Management

Build:

* Machine master
* Machine assignment
* Mobile machine view
* Web machine administration

---

## Sprint Group 3

### Preventive Maintenance

Build:

* PM plans
* Checklists
* Work order generation
* Technician job list
* Work execution

---

## Sprint Group 4

### Approval

Build:

* Technician submission
* Supervisor approval
* Rejection
* Re-submission
* History

---

## Sprint Group 5

### Notifications and Escalations

Build:

* Push notifications
* Reminder engine
* Overdue engine
* Manager escalation

---

## Sprint Group 6

### Breakdown Management

Build:

* Breakdown reporting
* Corrective maintenance
* Closure

---

## Sprint Group 7

### Calibration

Build:

* Certificates
* Expiry monitoring
* Renewal reminders
* Certificate history

---

## Sprint Group 8

### Reporting

Build:

* Manager reports
* PDF
* Excel
* Dashboard KPIs

---

# 93. MVP Boundary

The MVP should contain:

### Mobile

* Authentication
* Technician dashboard
* Supervisor dashboard
* My Jobs
* Work Order
* Checklist
* Readings
* Photos
* Submit
* Approval
* Rejection
* Notifications
* Breakdown reporting

### Web

* Manager dashboard
* Machines
* Users
* Maintenance plans
* Work orders
* Calendar
* Escalations
* Calibration
* Reports
* Settings

### Backend

* Database
* Authentication
* Role management
* Scheduler
* Notifications
* File storage
* Reporting
* Audit trail

---

# 94. Features That Should Not Delay MVP

Do not delay the initial operational release for:

* Spare inventory
* Purchasing
* ERP integration
* QR scanning
* WhatsApp
* IoT integration
* Predictive maintenance
* Cost accounting

These should remain later-phase features.

---

# 95. Critical Development Rule

The system must be designed around one central principle:

**Maintenance history must never depend on the current employee, machine owner, checklist version, or certificate version.**

Historical records must preserve the facts that existed when the maintenance was performed.

For example:

If a machine is reassigned from Technician A to Technician B, previously completed work must still show Technician A.

If a checklist is changed next year, old work orders must retain the checklist version used at the time.

This must be built correctly from the beginning.

---

# 96. Development Readiness

After approval of this specification, development can proceed with:

1. Database migrations
2. Backend authentication
3. API implementation
4. Mobile component library
5. Web component library
6. Work order workflow
7. Notification engine
8. Report generation

The approved Onam-inspired design system should be converted into reusable UI components rather than individually styling every screen.

This will maintain visual consistency across the complete MaintainPro platform.
