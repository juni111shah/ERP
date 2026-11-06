using ERP.Modules.IndustryPacks.Domain;
using static ERP.Modules.IndustryPacks.Domain.Def;

namespace ERP.Modules.IndustryPacks.Packs;

public sealed class LogisticsModule : PackModule
{
    public override string Key => "logistics";
    public override string Name => "Logistics and fleet";
    public override string Description => "Shipments, vehicles, drivers, fuel, vehicle maintenance, delivery expenses and freight invoicing.";
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("shipments", "Shipments", "Shipment", "send", "SHP",
            [
                Text("origin", "Origin", required: true, inList: true), Text("destination", "Destination", required: true, inList: true), Choice("mode", "Mode", "Road|Air|Sea|Rail|Courier", required: true, inList: true),
                Number("weightKg", "Weight (kg)", inList: true), Number("pieces", "Pieces"), Text("vehiclePlate", "Vehicle"), Text("driver", "Driver"), Date("deliveredOn", "Delivered on"), Text("proofOfDelivery", "Proof of delivery reference"),
            ],
            [S("booked", "Booked", "processing"), S("picked", "Picked up", "warning"), S("intransit", "In transit", "warning"), S("delivered", "Delivered", "success"), S("invoiced", "Invoiced", "success"), S("returned", "Returned", "error"), S("cancelled", "Cancelled", "error")],
            [T("booked", "picked", "Pick up"), T("booked", "cancelled", "Cancel", reason: true), T("picked", "intransit", "Dispatch"), T("intransit", "delivered", "Mark delivered"), T("intransit", "returned", "Return to sender", reason: true)],
            PartyRole: "Customer", PartyLabel: "Customer", HasLines: true, LinesLabel: "Freight charges", HasDueDate: true,
            Billing: new(Invoice: true, WhenStatus: ["delivered"], AfterBillingStatus: "invoiced"), MenuOrder: 10, TitleLabel: "Consignment",
            Kpis: [new("in-transit", "Shipments in transit", ["picked", "intransit"]), new("to-invoice", "Delivered, not invoiced", ["delivered"], SumAmount: true), new("late", "Late shipments", ["booked", "picked", "intransit"], OverdueOnly: true)]),
        new("vehicles", "Vehicles", "Vehicle", "car", "VEH",
            [Text("plate", "Plate number", required: true, inList: true), Choice("kind", "Type", "Van|Light truck|Heavy truck|Trailer|Motorbike|Bus", required: true, inList: true), Number("capacityKg", "Capacity (kg)"), Number("odometerKm", "Odometer (km)", inList: true), Date("registrationExpiry", "Registration expires", inList: true)],
            MasterStatuses, MasterTransitions, MenuOrder: 20, Kpis: [new("active-vehicles", "Vehicles in service", ["active"])]),
        new("drivers", "Drivers", "Driver", "idcard", "DRV",
            [Text("licenseNumber", "Licence number", required: true, sensitive: true), Date("licenseExpiry", "Licence expires", inList: true), Text("phone", "Phone", inList: true)],
            MasterStatuses, MasterTransitions, MenuOrder: 30),
        new("fuel", "Fuel logs", "Fuel log", "dashboard", "FUL",
            [Number("litres", "Litres", required: true, inList: true), Number("odometerKm", "Odometer (km)", inList: true), Text("station", "Station")],
            [S("draft", "Draft"), S("approved", "Approved", "processing"), S("billed", "Billed", "success"), S("rejected", "Rejected", "error")],
            [T("draft", "approved", "Approve"), T("draft", "rejected", "Reject", reason: true)],
            PartyRole: "Vendor", PartyLabel: "Fuel supplier", ParentType: "vehicles", ParentLabel: "Vehicle", HasAmount: true,
            Billing: new(Bill: true, WhenStatus: ["approved"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), MenuOrder: 40, TitleLabel: "Description",
            Kpis: [new("fuel-spend", "Fuel billed", ["billed"], SumAmount: true)]),
        new("maintenance", "Vehicle maintenance", "Maintenance job", "tool", "VMT",
            [Choice("kind", "Type", "Scheduled service|Repair|Tyres|Accident|Inspection", required: true, inList: true), Number("odometerKm", "Odometer (km)"), Text("workshop", "Workshop", inList: true)],
            [S("open", "Open", "processing"), S("completed", "Completed", "success"), S("billed", "Billed", "success"), S("cancelled", "Cancelled", "error")],
            [T("open", "completed", "Complete"), T("open", "cancelled", "Cancel", reason: true)],
            PartyRole: "Vendor", PartyLabel: "Workshop", ParentType: "vehicles", ParentLabel: "Vehicle", HasLines: true, LinesLabel: "Work and parts",
            Billing: new(Bill: true, WhenStatus: ["completed"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), MenuOrder: 50, TitleLabel: "Job",
            Kpis: [new("open-maintenance", "Open maintenance jobs", ["open"])]),
        new("deliveryexpenses", "Delivery expenses", "Delivery expense", "wallet", "DEX",
            [Choice("kind", "Type", "Tolls|Port charges|Loading|Customs handling|Driver allowance|Other", required: true, inList: true), Text("shipmentRef", "Shipment reference", inList: true)],
            [S("draft", "Draft"), S("approved", "Approved", "processing"), S("billed", "Billed", "success"), S("rejected", "Rejected", "error")],
            [T("draft", "approved", "Approve"), T("draft", "rejected", "Reject", reason: true)],
            PartyRole: "Vendor", PartyLabel: "Payee", HasAmount: true,
            Billing: new(Bill: true, WhenStatus: ["approved"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), MenuOrder: 60, TitleLabel: "Description"),
    ];
}

public sealed class TradeModule : PackModule
{
    public override string Key => "trade";
    public override string Name => "Import and export";
    public override string Description => "Letters of credit, import consignments with landed costs, and export orders.";
    public override IReadOnlyList<string> Requires => ["purchasing", "sales"];
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("lettersofcredit", "Letters of credit", "Letter of credit", "bank", "LC",
            [Text("issuingBank", "Issuing bank", required: true, inList: true), Text("lcReference", "Bank reference", inList: true), Choice("kind", "Type", "Sight|Usance|Standby", inList: true), Text("currencyNote", "Currency note")],
            [S("draft", "Draft"), S("submitted", "Submitted", "processing"), S("opened", "Opened", "success"), S("utilized", "Utilized", "success"), S("expired", "Expired"), S("rejected", "Rejected", "error")],
            [T("draft", "submitted", "Submit"), T("submitted", "opened", "Open", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true), T("opened", "utilized", "Mark utilized"), T("opened", "expired", "Mark expired")],
            PartyRole: "Vendor", PartyLabel: "Beneficiary", HasAmount: true, HasDueDate: true, MenuOrder: 10, TitleLabel: "Description",
            Kpis: [new("open-lc", "Open letters of credit", ["opened"], SumAmount: true)]),
        new("imports", "Import consignments", "Import consignment", "import", "IMP",
            [Text("billOfLading", "Bill of lading", inList: true), Text("vessel", "Vessel or flight"), Text("portOfEntry", "Port of entry", inList: true), Date("eta", "Expected arrival", inList: true), Text("customsEntry", "Customs entry"), Text("purchaseOrderRef", "Purchase order", inList: true)],
            [S("booked", "Booked", "processing"), S("onwater", "On the way", "warning"), S("atport", "At port", "warning"), S("cleared", "Cleared", "success"), S("billed", "Costs billed", "success"), S("cancelled", "Cancelled", "error")],
            [T("booked", "onwater", "Mark shipped"), T("booked", "cancelled", "Cancel", reason: true), T("onwater", "atport", "Mark arrived"), T("atport", "cleared", "Mark cleared")],
            PartyRole: "Vendor", PartyLabel: "Forwarder or broker", HasLines: true, LinesLabel: "Freight, duty and clearing costs",
            Billing: new(Bill: true, WhenStatus: ["cleared"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), MenuOrder: 20, TitleLabel: "Consignment",
            Kpis: [new("imports-open", "Consignments on the way", ["onwater", "atport"])]),
        new("exports", "Export orders", "Export order", "export", "EXO",
            [Text("destinationCountry", "Destination", required: true, inList: true), Choice("incoterm", "Delivery terms", "EXW|FOB|CFR|CIF|DAP|DDP", inList: true), Text("portOfLoading", "Port of loading"), Text("billOfLading", "Bill of lading"), Text("salesOrderRef", "Sales order", inList: true)],
            [S("draft", "Draft"), S("confirmed", "Confirmed", "processing"), S("shipped", "Shipped", "warning"), S("invoiced", "Invoiced", "success"), S("cancelled", "Cancelled", "error")],
            [T("draft", "confirmed", "Confirm"), T("draft", "cancelled", "Cancel", reason: true), T("confirmed", "shipped", "Mark shipped"), T("confirmed", "cancelled", "Cancel", reason: true)],
            PartyRole: "Customer", PartyLabel: "Buyer", HasLines: true, LinesLabel: "Goods and charges",
            Billing: new(Invoice: true, WhenStatus: ["shipped"], AfterBillingStatus: "invoiced"), MenuOrder: 30, TitleLabel: "Order",
            Kpis: [new("exports-open", "Exports to ship", ["confirmed"], SumAmount: true)]),
    ];
}

public sealed class AgricultureModule : PackModule
{
    public override string Key => "agriculture";
    public override string Name => "Agriculture and livestock";
    public override string Description => "Herds and flocks, daily production, feed, veterinary care, crop cycles and harvests.";
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("herds", "Herds and flocks", "Herd or flock", "bug", "HRD",
            [Choice("species", "Species", "Dairy cattle|Beef cattle|Buffalo|Goat|Sheep|Layer poultry|Broiler poultry|Other", required: true, inList: true), Number("headCount", "Head count", required: true, inList: true), Text("location", "Shed or pen", inList: true)],
            MasterStatuses, MasterTransitions, MenuOrder: 10, Kpis: [new("active-herds", "Active herds and flocks", ["active"])]),
        new("production", "Daily production", "Production log", "line-chart", "PRD",
            [Choice("product", "Product", "Milk (litres)|Eggs (trays)|Meat (kg)|Wool (kg)|Other", required: true, inList: true), Number("quantity", "Quantity", required: true, inList: true), Number("fatPercent", "Fat %"), Number("mortality", "Mortality")],
            [S("recorded", "Recorded", "success"), S("voided", "Voided", "error")], [T("recorded", "voided", "Void", reason: true)],
            ParentType: "herds", ParentLabel: "Herd or flock", MenuOrder: 20, TitleLabel: "Shift or batch"),
        new("feed", "Feed issues", "Feed issue", "shopping", "FED",
            [Text("ration", "Ration", inList: true), Text("issuedBy", "Issued by")],
            [S("draft", "Draft"), S("issued", "Issued", "success"), S("cancelled", "Cancelled", "error")], [T("draft", "issued", "Issue"), T("draft", "cancelled", "Cancel", reason: true), T("issued", "cancelled", "Reverse", reason: true)],
            ParentType: "herds", ParentLabel: "Herd or flock", HasLines: true, LinesLabel: "Feed and supplements", ConsumesStock: true, ConsumeAccountRole: "cogs", MenuOrder: 30),
        new("vetcare", "Veterinary care", "Veterinary visit", "medicine-box", "VET",
            [Choice("kind", "Type", "Vaccination|Treatment|Deworming|Pregnancy check|Inspection", required: true, inList: true), Number("animalsTreated", "Animals treated", inList: true), Text("veterinarian", "Veterinarian")],
            [S("scheduled", "Scheduled", "processing"), S("done", "Done", "success"), S("billed", "Billed", "success"), S("cancelled", "Cancelled", "error")],
            [T("scheduled", "done", "Mark done"), T("scheduled", "cancelled", "Cancel", reason: true)],
            PartyRole: "Vendor", PartyLabel: "Veterinary supplier", ParentType: "herds", ParentLabel: "Herd or flock", HasAmount: true,
            Billing: new(Bill: true, WhenStatus: ["done"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), MenuOrder: 40, TitleLabel: "Description"),
        new("crops", "Crop cycles", "Crop cycle", "environment", "CRP",
            [Text("crop", "Crop", required: true, inList: true), Number("areaHectares", "Area (hectares)", inList: true), Text("field", "Field", inList: true), Date("plantedOn", "Planted on"), Date("expectedHarvest", "Expected harvest", inList: true)],
            [S("planned", "Planned", "processing"), S("planted", "Planted", "warning"), S("harvested", "Harvested", "success"), S("failed", "Failed", "error")],
            [T("planned", "planted", "Mark planted"), T("planted", "harvested", "Mark harvested"), T("planted", "failed", "Mark failed", reason: true)], MenuOrder: 50,
            Kpis: [new("growing", "Crops in the ground", ["planted"])]),
        new("harvests", "Harvests", "Harvest", "gold", "HRV",
            [Number("quantityKg", "Quantity (kg)", required: true, inList: true), Choice("grade", "Grade", "A|B|C|Rejected", inList: true)],
            [S("recorded", "Recorded", "success"), S("voided", "Voided", "error")], [T("recorded", "voided", "Void", reason: true)],
            ParentType: "crops", ParentLabel: "Crop cycle", MenuOrder: 60, TitleLabel: "Lot"),
    ];
}

/// <summary>
/// The construction layer on top of general projects: measured quantities, valuations, subcontractors, materials and plant.
/// Retention is recorded on a valuation but is not yet held in its own ledger account.
/// </summary>
public sealed class ConstructionModule : PackModule
{
    public override string Key => "construction";
    public override string Name => "Construction";
    public override string Description => "Sites, bills of quantities, progress valuations, subcontracts, material issues and equipment.";
    public override IReadOnlyList<string> Requires => ["inventory", "projects"];
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("sites", "Construction sites", "Site", "environment", "SIT",
            [Text("location", "Location", required: true, inList: true), Money("contractValue", "Contract value", inList: true), Money("costBudget", "Cost budget", inList: true), Text("projectManager", "Project manager", inList: true), Number("progressPercent", "Progress %", inList: true)],
            [S("planned", "Planned", "processing"), S("active", "Active", "success"), S("onhold", "On hold", "warning"), S("handedover", "Handed over", "success"), S("cancelled", "Cancelled", "error")],
            [T("planned", "active", "Start"), T("planned", "cancelled", "Cancel", reason: true), T("active", "onhold", "Hold", reason: true), T("onhold", "active", "Resume"), T("active", "handedover", "Hand over")],
            PartyRole: "Customer", PartyLabel: "Client", MenuOrder: 10, TitleLabel: "Project name", HasDueDate: true, Kpis: [new("active-sites", "Active sites", ["active"])]),
        new("boqs", "Bills of quantities", "Bill of quantities", "profile", "BOQ",
            [Text("revision", "Revision", inList: true), Choice("discipline", "Discipline", "Civil|Structural|Mechanical|Electrical|Plumbing|Finishes|External works", inList: true)],
            [.. DocumentStatuses], DocumentTransitions,
            ParentType: "sites", ParentLabel: "Site", HasLines: true, LinesLabel: "Measured items", TracksProgress: true, MenuOrder: 20, TitleLabel: "Title",
            Kpis: [new("boq-approved", "Approved BOQ value", ["approved", "completed"], SumAmount: true)]),
        new("valuations", "Progress valuations", "Valuation", "file-done", "VAL",
            [Number("periodNumber", "Valuation number", required: true, inList: true), Number("retentionPercent", "Retention %", inList: true), Number("certifiedPercent", "Certified progress %", inList: true), Text("certifiedBy", "Certified by")],
            [S("draft", "Draft"), S("submitted", "Submitted", "processing"), S("certified", "Certified", "success"), S("rejected", "Rejected", "error"), S("invoiced", "Invoiced", "success")],
            [T("draft", "submitted", "Submit"), T("submitted", "certified", "Certify", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true), T("rejected", "draft", "Rework")],
            PartyRole: "Customer", PartyLabel: "Client", ParentType: "sites", ParentLabel: "Site", HasLines: true, LinesLabel: "Work done this period", HasDueDate: true,
            Billing: new(Invoice: true, WhenStatus: ["certified"], AfterBillingStatus: "invoiced"), MenuOrder: 30, TitleLabel: "Title",
            Kpis: [new("valuations-to-invoice", "Certified, not invoiced", ["certified"], SumAmount: true)]),
        new("subcontracts", "Subcontracts", "Subcontract", "solution", "SUB",
            [Text("scope", "Scope", required: true, inList: true), Number("retentionPercent", "Retention %")],
            [S("draft", "Draft"), S("submitted", "Submitted", "processing"), S("active", "Active", "success"), S("rejected", "Rejected", "error"), S("closed", "Closed"), S("terminated", "Terminated", "error")],
            [T("draft", "submitted", "Submit"), T("submitted", "active", "Approve", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true), T("rejected", "draft", "Rework"), T("active", "closed", "Close"), T("active", "terminated", "Terminate", reason: true)],
            PartyRole: "Vendor", PartyLabel: "Subcontractor", ParentType: "sites", ParentLabel: "Site", HasAmount: true, MenuOrder: 40, TitleLabel: "Title",
            Kpis: [new("subcontract-value", "Active subcontract value", ["active"], SumAmount: true)]),
        new("subcontractorpayments", "Subcontractor payment certificates", "Payment certificate", "audit", "SPC",
            [Text("subcontractRef", "Subcontract", required: true, inList: true), Number("periodNumber", "Certificate number", inList: true)],
            [S("draft", "Draft"), S("submitted", "Submitted", "processing"), S("approved", "Approved", "success"), S("rejected", "Rejected", "error"), S("billed", "Billed", "success")],
            [T("draft", "submitted", "Submit"), T("submitted", "approved", "Approve", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true), T("rejected", "draft", "Rework")],
            PartyRole: "Vendor", PartyLabel: "Subcontractor", ParentType: "sites", ParentLabel: "Site", HasLines: true, LinesLabel: "Certified work",
            Billing: new(Bill: true, WhenStatus: ["approved"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), MenuOrder: 50, TitleLabel: "Title"),
        new("materialissues", "Material issues", "Material issue", "shopping-cart", "MIS",
            [Text("issuedTo", "Issued to", inList: true), Text("workArea", "Work area", inList: true)],
            [S("draft", "Draft"), S("issued", "Issued", "success"), S("cancelled", "Cancelled", "error")], [T("draft", "issued", "Mark issued"), T("draft", "cancelled", "Cancel", reason: true), T("issued", "cancelled", "Reverse", reason: true)],
            ParentType: "sites", ParentLabel: "Site", HasLines: true, LinesLabel: "Materials", ConsumesStock: true, ConsumeAccountRole: "cogs", MenuOrder: 60, TitleLabel: "Reference"),
        new("equipment", "Equipment use", "Equipment log", "tool", "EQP",
            [Text("equipmentName", "Equipment", required: true, inList: true), Number("hours", "Hours", required: true, inList: true), Choice("ownership", "Ownership", "Owned|Hired", inList: true)],
            [S("draft", "Draft"), S("approved", "Approved", "processing"), S("billed", "Billed", "success"), S("rejected", "Rejected", "error")], [T("draft", "approved", "Approve"), T("draft", "rejected", "Reject", reason: true)],
            PartyRole: "Vendor", PartyLabel: "Hire company", ParentType: "sites", ParentLabel: "Site", HasAmount: true,
            Billing: new(Bill: true, WhenStatus: ["approved"], ExpenseAccountRole: "purchases.expense", AfterBillingStatus: "billed"), MenuOrder: 70, TitleLabel: "Description"),
        new("sitereports", "Daily site reports", "Site report", "read", "DSR",
            [Number("workers", "Workers on site", inList: true), Choice("weather", "Weather", "Clear|Hot|Windy|Rain|Dust storm", inList: true), LongText("workDone", "Work done", required: true), LongText("issues", "Issues")],
            [S("submitted", "Submitted", "success")], [], ParentType: "sites", ParentLabel: "Site", MenuOrder: 80, TitleLabel: "Report title"),
    ];
}

/// <summary>Quality inspection and production planning for any business that makes or checks goods, alongside the manufacturing module.</summary>
public sealed class QualityModule : PackModule
{
    public override string Key => "quality";
    public override string Name => "Quality and planning";
    public override string Description => "Production plans, quality inspections and non-conformance reports.";
    public override string Category => "Horizontal";
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("plans", "Production plans", "Production plan", "schedule", "PLN",
            [Text("period", "Period", required: true, inList: true), Text("plant", "Plant or line", inList: true)],
            [S("draft", "Draft"), S("released", "Released", "processing"), S("completed", "Completed", "success"), S("cancelled", "Cancelled", "error")],
            [T("draft", "released", "Release"), T("draft", "cancelled", "Cancel", reason: true), T("released", "completed", "Complete"), T("released", "cancelled", "Cancel", reason: true)],
            HasLines: true, LinesLabel: "Products to make", TracksProgress: true, MenuOrder: 10, TitleLabel: "Plan name",
            Kpis: [new("plans-open", "Plans in progress", ["released"])]),
        new("inspections", "Quality inspections", "Inspection", "safety-certificate", "QIN",
            [Choice("stage", "Stage", "Incoming|In process|Final|Customer return", required: true, inList: true), Text("sourceRef", "Source document", required: true, inList: true), Text("product", "Product", inList: true), Number("sampleSize", "Sample size"), Number("defects", "Defects", inList: true), Text("inspector", "Inspector", inList: true)],
            [S("planned", "Planned", "processing"), S("passed", "Passed", "success"), S("failed", "Failed", "error"), S("onhold", "On hold", "warning")],
            [T("planned", "passed", "Pass"), T("planned", "failed", "Fail", reason: true), T("planned", "onhold", "Hold", reason: true), T("onhold", "passed", "Release"), T("onhold", "failed", "Fail", reason: true)], MenuOrder: 20, TitleLabel: "Inspection",
            Kpis: [new("inspections-failed", "Failed inspections", ["failed"]), new("inspections-open", "Inspections to do", ["planned", "onhold"])]),
        new("nonconformances", "Non-conformance reports", "Non-conformance", "warning", "NCR",
            [Choice("severity", "Severity", "Minor|Major|Critical", required: true, inList: true), Text("sourceRef", "Source document", inList: true), LongText("rootCause", "Root cause"), LongText("correctiveAction", "Corrective action")],
            [S("open", "Open", "error"), S("investigating", "Investigating", "warning"), S("closed", "Closed", "success")], [T("open", "investigating", "Investigate"), T("investigating", "closed", "Close", approval: true)], MenuOrder: 30, TitleLabel: "Issue",
            Kpis: [new("ncr-open", "Open non-conformances", ["open", "investigating"])]),
    ];
}

/// <summary>Seller-side property business for developers, housing societies and investors. Leasing, brokerage and maintenance stay in the real estate module.</summary>
public sealed class PropertySalesModule : PackModule
{
    public override string Key => "propertysales";
    public override string Name => "Property sales";
    public override string Description => "Sale contracts and allotments with instalment plans, and property valuations.";
    public override IReadOnlyList<string> Requires => ["realestate"];
    public override IReadOnlyList<RecordTypeDef> Types { get; } =
    [
        new("salecontracts", "Sale contracts", "Sale contract", "file-protect", "SAL",
            [Text("unitRef", "Unit or plot", required: true, inList: true), Text("projectName", "Project", inList: true), Money("listPrice", "List price"), Text("agent", "Sales agent", inList: true), Date("handoverDate", "Handover date", inList: true)],
            [S("draft", "Draft"), S("submitted", "In review", "processing"), S("active", "Active", "success"), S("rejected", "Rejected", "error"), S("completed", "Fully invoiced", "success"), S("handedover", "Handed over", "success"), S("cancelled", "Cancelled", "error")],
            [T("draft", "submitted", "Submit"), T("submitted", "active", "Approve", approval: true), T("submitted", "rejected", "Reject", reason: true, approval: true), T("rejected", "draft", "Rework"), T("active", "cancelled", "Cancel", reason: true), T("completed", "handedover", "Hand over")],
            PartyRole: "Customer", PartyLabel: "Buyer", HasLines: true, LinesLabel: "Instalment plan",
            Billing: new(Invoice: true, PerLine: true, WhenStatus: ["active"], AfterBillingStatus: "completed"), MenuOrder: 10, TitleLabel: "Contract title",
            Kpis: [new("sale-contracts", "Active sale contracts", ["active"], SumAmount: true)]),
        new("allotments", "Society allotments", "Allotment", "apartment", "ALT",
            [Text("plotNumber", "Plot or file number", required: true, inList: true), Text("block", "Block or phase", inList: true), Text("membershipNumber", "Membership number", inList: true), Number("areaSqm", "Area (sq m)")],
            [S("draft", "Draft"), S("allotted", "Allotted", "success"), S("transferred", "Transferred"), S("cancelled", "Cancelled", "error")],
            [T("draft", "allotted", "Allot", approval: true), T("draft", "cancelled", "Cancel", reason: true), T("allotted", "transferred", "Transfer", reason: true), T("allotted", "cancelled", "Cancel", reason: true)],
            PartyRole: "Customer", PartyLabel: "Member", HasLines: true, LinesLabel: "Dues and instalments",
            Billing: new(Invoice: true, PerLine: true, WhenStatus: ["allotted"]), MenuOrder: 20, TitleLabel: "Allotment"),
        new("valuations", "Property valuations", "Valuation", "stock", "VLN",
            [Text("propertyRef", "Property", required: true, inList: true), Text("valuer", "Valuer", required: true, inList: true), Choice("basis", "Basis", "Market value|Rental yield|Replacement cost|Forced sale", inList: true), Money("marketValue", "Valuation", required: true, inList: true)],
            [S("draft", "Draft"), S("final", "Final", "success")], [T("draft", "final", "Finalize", approval: true)], MenuOrder: 30, TitleLabel: "Title"),
    ];
}
