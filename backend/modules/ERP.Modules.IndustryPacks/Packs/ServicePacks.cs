using ERP.Modules.IndustryPacks.Domain;
using static ERP.Modules.IndustryPacks.Domain.Def;

namespace ERP.Modules.IndustryPacks.Packs;

/// <summary>
/// Patient administration and billing. Administrative only: there is no clinical record here. Patient details are restricted fields and
/// opening a patient leaves a trace, but values are not encrypted at rest, so real patient data needs the controls listed in the audit report first.
/// </summary>
public sealed class HealthcareModule : PackModule
{
    public override string Key => "healthcare";
    public override string Name => "Healthcare administration";
    public override string Description => "Patients, appointments, medical billing, dispensing and insurance claims. Administrative records only, not clinical notes.";
    public override IReadOnlyList<string> Requires => ["inventory"];
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("patients", "Patients", "Patient", "user", "MRN",
            [
                Date("dateOfBirth", "Date of birth", sensitive: true), Choice("gender", "Gender", "Female|Male|Other|Not stated", sensitive: true),
                Text("phone", "Phone", sensitive: true), Choice("bloodGroup", "Blood group", "A+|A-|B+|B-|AB+|AB-|O+|O-|Unknown", sensitive: true),
                LongText("allergies", "Known allergies", sensitive: true), Text("insurer", "Insurer", inList: true), Text("policyNumber", "Policy number", sensitive: true),
            ],
            MasterStatuses, MasterTransitions, PartyRole: "Customer", PartyLabel: "Billing account", Sensitive: true, MenuOrder: 10,
            Kpis: [new("active-patients", "Active patients", ["active"])]),
        new("appointments", "Appointments", "Appointment", "calendar", "APT",
            [Text("department", "Department", required: true, inList: true), Text("clinician", "Clinician", required: true, inList: true), Text("time", "Time", required: true), Choice("visitType", "Visit type", "New|Follow-up|Emergency|Procedure", inList: true), LongText("reason", "Reason for visit", sensitive: true)],
            [S("scheduled", "Scheduled", "processing"), S("checkedin", "Checked in", "warning"), S("completed", "Completed", "success"), S("noshow", "No show", "error"), S("cancelled", "Cancelled", "error")],
            [T("scheduled", "checkedin", "Check in"), T("scheduled", "noshow", "Mark no show"), T("scheduled", "cancelled", "Cancel", reason: true), T("checkedin", "completed", "Complete")],
            ParentType: "patients", ParentLabel: "Patient", MenuOrder: 20, TitleLabel: "Purpose",
            Kpis: [new("open-appointments", "Appointments to attend", ["scheduled", "checkedin"])]),
        new("medicalbills", "Medical bills", "Medical bill", "file-text", "MBL",
            [Text("department", "Department", inList: true), Choice("payer", "Paid by", "Self|Insurer|Employer", inList: true)],
            [S("draft", "Draft"), S("finalized", "Finalized", "processing"), S("billed", "Invoiced", "success"), S("cancelled", "Cancelled", "error")],
            [T("draft", "finalized", "Finalize"), T("draft", "cancelled", "Cancel", reason: true), T("finalized", "draft", "Reopen"), T("finalized", "cancelled", "Cancel", reason: true)],
            PartyRole: "Customer", PartyLabel: "Billing account", ParentType: "patients", ParentLabel: "Patient", HasLines: true, LinesLabel: "Charges", HasDueDate: true,
            Billing: new(Invoice: true, WhenStatus: ["finalized"], AfterBillingStatus: "billed"), MenuOrder: 30, TitleLabel: "Bill description",
            Kpis: [new("bills-to-invoice", "Bills to invoice", ["finalized"], SumAmount: true)]),
        new("dispensings", "Pharmacy dispensing", "Dispensing", "medicine-box", "DSP",
            [Text("prescriber", "Prescriber", inList: true), Text("prescriptionRef", "Prescription reference", sensitive: true)],
            [S("draft", "Draft"), S("dispensed", "Dispensed", "success"), S("cancelled", "Cancelled", "error")],
            [T("draft", "dispensed", "Dispense"), T("draft", "cancelled", "Cancel", reason: true), T("dispensed", "cancelled", "Reverse", reason: true)],
            PartyRole: "Customer", PartyLabel: "Billing account", ParentType: "patients", ParentLabel: "Patient", HasLines: true, LinesLabel: "Medicines",
            Billing: new(Invoice: true, WhenStatus: ["dispensed"]), ConsumesStock: true, ConsumeAccountRole: "cogs", MenuOrder: 40),
        new("labtests", "Diagnostic orders", "Diagnostic order", "experiment", "LAB",
            [Text("orderedBy", "Ordered by", inList: true), Text("sampleType", "Sample type")],
            [S("ordered", "Ordered", "processing"), S("collected", "Sample collected", "warning"), S("reported", "Reported", "success"), S("cancelled", "Cancelled", "error")],
            [T("ordered", "collected", "Collect sample"), T("ordered", "cancelled", "Cancel", reason: true), T("collected", "reported", "Mark reported")],
            PartyRole: "Customer", PartyLabel: "Billing account", ParentType: "patients", ParentLabel: "Patient", HasLines: true, LinesLabel: "Tests",
            Billing: new(Invoice: true, WhenStatus: ["collected", "reported"]), MenuOrder: 50),
        new("claims", "Insurance claims", "Insurance claim", "safety-certificate", "CLM",
            [Text("policyNumber", "Policy number", sensitive: true), Text("authorizationCode", "Authorization code"), Text("rejectionReason", "Rejection reason")],
            [S("draft", "Draft"), S("submitted", "Submitted", "processing"), S("approved", "Approved", "success"), S("rejected", "Rejected", "error"), S("settled", "Invoiced to insurer", "success")],
            [T("draft", "submitted", "Submit"), T("submitted", "approved", "Approve", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true), T("rejected", "draft", "Rework")],
            PartyRole: "Customer", PartyLabel: "Insurer", ParentType: "patients", ParentLabel: "Patient", HasAmount: true, HasDueDate: true,
            Billing: new(Invoice: true, WhenStatus: ["approved"], AfterBillingStatus: "settled"), MenuOrder: 60, TitleLabel: "Claim description",
            Kpis: [new("claims-open", "Claims awaiting decision", ["submitted"], SumAmount: true)]),
    ];
}

public sealed class EducationModule : PackModule
{
    public override string Key => "education";
    public override string Name => "Education";
    public override string Description => "Admissions, students, academic sessions, classes, fee billing and examination administration.";
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("sessions", "Academic sessions", "Academic session", "calendar", "SES",
            [Date("startsOn", "Starts on", required: true, inList: true), Date("endsOn", "Ends on", required: true, inList: true)],
            [S("planned", "Planned", "processing"), S("open", "Open", "success"), S("closed", "Closed")], [T("planned", "open", "Open"), T("open", "closed", "Close")], MenuOrder: 10),
        new("classes", "Classes", "Class", "cluster", "CLS",
            [Text("grade", "Grade or programme", required: true, inList: true), Text("section", "Section", inList: true), Text("classTeacher", "Class teacher", inList: true), Number("capacity", "Capacity")],
            MasterStatuses, MasterTransitions, ParentType: "sessions", ParentLabel: "Academic session", MenuOrder: 20),
        new("admissions", "Admissions", "Application", "form", "ADM",
            [Text("programme", "Programme", required: true, inList: true), Date("dateOfBirth", "Date of birth", sensitive: true), Text("guardianName", "Guardian", inList: true), Text("guardianPhone", "Guardian phone", sensitive: true), Text("previousSchool", "Previous school")],
            [S("applied", "Applied", "processing"), S("shortlisted", "Shortlisted", "warning"), S("approved", "Approved", "success"), S("rejected", "Rejected", "error"), S("enrolled", "Enrolled", "success"), S("withdrawn", "Withdrawn")],
            [T("applied", "shortlisted", "Shortlist"), T("applied", "rejected", "Reject", reason: true, approval: true), T("shortlisted", "approved", "Approve", approval: true), T("shortlisted", "rejected", "Reject", reason: true, approval: true), T("approved", "enrolled", "Enrol"), T("approved", "withdrawn", "Withdraw", reason: true), T("applied", "withdrawn", "Withdraw", reason: true)],
            ParentType: "sessions", ParentLabel: "Academic session", MenuOrder: 30, TitleLabel: "Applicant name",
            Kpis: [new("open-applications", "Applications in review", ["applied", "shortlisted"])]),
        new("students", "Students", "Student", "team", "STD",
            [Text("grade", "Grade or programme", inList: true), Date("dateOfBirth", "Date of birth", sensitive: true), Text("guardianName", "Guardian", inList: true), Text("guardianPhone", "Guardian phone", sensitive: true)],
            [S("active", "Active", "success"), S("graduated", "Graduated"), S("withdrawn", "Withdrawn", "error")], [T("active", "graduated", "Graduate"), T("active", "withdrawn", "Withdraw", reason: true)],
            PartyRole: "Customer", PartyLabel: "Fee payer", ParentType: "classes", ParentLabel: "Class", MenuOrder: 40,
            Kpis: [new("active-students", "Enrolled students", ["active"])]),
        new("fees", "Fee plans", "Fee plan", "account-book", "FEE",
            [Text("term", "Term", inList: true), Text("scholarship", "Scholarship")],
            [S("draft", "Draft"), S("active", "Active", "processing"), S("billed", "Fully invoiced", "success"), S("cancelled", "Cancelled", "error")],
            [T("draft", "active", "Activate"), T("draft", "cancelled", "Cancel", reason: true), T("active", "cancelled", "Cancel", reason: true)],
            PartyRole: "Customer", PartyLabel: "Fee payer", ParentType: "students", ParentLabel: "Student", HasLines: true, LinesLabel: "Instalments",
            Billing: new(Invoice: true, PerLine: true, WhenStatus: ["active"], AfterBillingStatus: "billed"), MenuOrder: 50, TitleLabel: "Plan name",
            Kpis: [new("fee-plans-open", "Fees still to invoice", ["active"], SumAmount: true)]),
        new("exams", "Examinations", "Examination", "solution", "EXM",
            [Text("subject", "Subject", required: true, inList: true), Number("maxMarks", "Maximum marks", required: true), Date("examDate", "Exam date", required: true, inList: true)],
            [S("planned", "Planned", "processing"), S("held", "Held", "warning"), S("published", "Results published", "success"), S("cancelled", "Cancelled", "error")],
            [T("planned", "held", "Mark held"), T("planned", "cancelled", "Cancel", reason: true), T("held", "published", "Publish", approval: true)],
            ParentType: "classes", ParentLabel: "Class", MenuOrder: 60),
        new("results", "Exam results", "Exam result", "trophy", "RES",
            [Text("subject", "Subject", required: true, inList: true), Number("marks", "Marks", required: true, inList: true), Text("grade", "Grade", inList: true)],
            [S("draft", "Draft"), S("final", "Final", "success")], [T("draft", "final", "Finalize")], ParentType: "students", ParentLabel: "Student", MenuOrder: 70, TitleLabel: "Examination"),
    ];
}

public sealed class HospitalityModule : PackModule
{
    public override string Key => "hospitality";
    public override string Name => "Hospitality";
    public override string Description => "Rooms, reservations, guest folios, catering events and kitchen waste. Restaurants sell through the point of sale module.";
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("rooms", "Rooms and tables", "Room or table", "home", "RM",
            [Choice("kind", "Kind", "Room|Suite|Table|Hall", required: true, inList: true), Text("floor", "Floor or area", inList: true), Money("rate", "Standard rate", inList: true), Number("capacity", "Capacity")],
            MasterStatuses, MasterTransitions, MenuOrder: 10, Kpis: [new("active-rooms", "Rooms and tables", ["active"])]),
        new("reservations", "Reservations", "Reservation", "calendar", "RSV",
            [Date("arrival", "Arrival", required: true, inList: true), Date("departure", "Departure", required: true, inList: true), Number("guests", "Guests"), Choice("source", "Booked through", "Direct|Phone|Agency|Website|Walk-in", inList: true)],
            [S("confirmed", "Confirmed", "processing"), S("checkedin", "Checked in", "warning"), S("checkedout", "Checked out", "success"), S("noshow", "No show", "error"), S("cancelled", "Cancelled", "error")],
            [T("confirmed", "checkedin", "Check in"), T("confirmed", "noshow", "Mark no show"), T("confirmed", "cancelled", "Cancel", reason: true), T("checkedin", "checkedout", "Check out")],
            PartyRole: "Customer", PartyLabel: "Guest account", ParentType: "rooms", ParentLabel: "Room or table", HasLines: true, LinesLabel: "Folio charges",
            Billing: new(Invoice: true, WhenStatus: ["checkedout"]), MenuOrder: 20, TitleLabel: "Guest name",
            Kpis: [new("in-house", "Guests in house", ["checkedin"]), new("upcoming", "Upcoming reservations", ["confirmed"])]),
        new("events", "Catering and events", "Event", "gift", "EVT",
            [Date("eventDate", "Event date", required: true, inList: true), Number("covers", "Covers", inList: true), Text("venue", "Venue")],
            [S("draft", "Enquiry"), S("confirmed", "Confirmed", "processing"), S("delivered", "Delivered", "success"), S("cancelled", "Cancelled", "error")],
            [T("draft", "confirmed", "Confirm"), T("draft", "cancelled", "Cancel", reason: true), T("confirmed", "delivered", "Mark delivered"), T("confirmed", "cancelled", "Cancel", reason: true)],
            PartyRole: "Customer", PartyLabel: "Client", HasLines: true, LinesLabel: "Packages and charges", Billing: new(Invoice: true, WhenStatus: ["confirmed", "delivered"]), MenuOrder: 30, TitleLabel: "Event name"),
        new("waste", "Kitchen waste", "Waste log", "delete", "WST",
            [Choice("reason", "Reason", "Expired|Spoiled|Overproduction|Preparation|Returned", required: true, inList: true), Text("recordedBy", "Recorded by")],
            [S("draft", "Draft"), S("posted", "Posted", "success"), S("cancelled", "Cancelled", "error")], [T("draft", "posted", "Post"), T("draft", "cancelled", "Cancel", reason: true), T("posted", "cancelled", "Reverse", reason: true)],
            HasLines: true, LinesLabel: "Items wasted", ConsumesStock: true, ConsumeAccountRole: "cogs", MenuOrder: 40),
    ];
}

/// <summary>Contracts as a business record for any industry. Billing stays with the module that performs the work.</summary>
public sealed class ContractsModule : PackModule
{
    public override string Key => "contracts";
    public override string Name => "Contracts";
    public override string Description => "Customer and supplier contracts with approval, value, term and expiry.";
    public override string Category => "Horizontal";
    public override int GroupOrder => 380;
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("agreements", "Contracts", "Contract", "file-protect", "CTR",
            [Choice("direction", "Contract with", "Customer|Supplier|Other", required: true, inList: true), Choice("kind", "Type", "Service|Supply|Lease|Maintenance|Framework|Employment|Other", inList: true), Date("startsOn", "Starts on", required: true, inList: true), Number("noticeDays", "Notice period in days"), Flag("autoRenew", "Renews automatically")],
            [S("draft", "Draft"), S("submitted", "In review", "processing"), S("active", "Active", "success"), S("rejected", "Rejected", "error"), S("expired", "Expired"), S("terminated", "Terminated", "error")],
            [T("draft", "submitted", "Submit"), T("submitted", "active", "Approve", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true), T("rejected", "draft", "Rework"), T("active", "expired", "Mark expired"), T("active", "terminated", "Terminate", reason: true)],
            PartyRole: "Any", PartyLabel: "Counterparty", HasAmount: true, HasDueDate: true, TitleLabel: "Contract title",
            Kpis: [new("active-contracts", "Active contracts", ["active"]), new("contract-value", "Active contract value", ["active"], SumAmount: true), new("contracts-overdue", "Contracts past end date", ["active"], OverdueOnly: true)]),
    ];
}

/// <summary>Staff expense claims. An approved claim becomes a vendor bill to the person, so it reaches the ledger through the normal payables path.</summary>
public sealed class ExpensesModule : PackModule
{
    public override string Key => "expenses";
    public override string Name => "Expense claims";
    public override string Description => "Staff expense claims with approval, raised as a payable when approved.";
    public override string Category => "Horizontal";
    public override int GroupOrder => 385;
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("claims", "Expense claims", "Expense claim", "wallet", "EXP",
            [Text("department", "Department", inList: true), Choice("category", "Category", "Travel|Meals|Accommodation|Fuel|Office|Client entertainment|Other", required: true, inList: true)],
            [.. DocumentStatuses, S("billed", "Sent to payables", "success")], DocumentTransitions,
            PartyRole: "Vendor", PartyLabel: "Payee", HasLines: true, LinesLabel: "Expenses",
            Billing: new(Bill: true, WhenStatus: ["approved"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), TitleLabel: "Purpose",
            Kpis: [new("claims-to-approve", "Claims to approve", ["submitted"], SumAmount: true)]),
    ];
}
