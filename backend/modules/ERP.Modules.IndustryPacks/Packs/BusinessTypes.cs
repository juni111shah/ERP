using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;

namespace ERP.Modules.IndustryPacks.Packs;

/// <summary>
/// The industries and business types offered at onboarding. Choosing one decides the modules, roles, units, categories, optional core areas,
/// dashboard focus and wording of a workspace. It is data, kept out of the core, so a new business type never needs code in the core.
/// </summary>
public sealed class BusinessTypesModule : ModuleBase
{
    public const string Key = "businesstypes";

    public override ModuleManifest Manifest { get; } = new(Key, "Business types", "Industry templates offered when a workspace is set up.", "System", IsCore: true, Hidden: true);

    public override IEnumerable<IndustryTemplate> IndustryTemplates => All;

    private const string Trading = "Trading, wholesale and distribution";
    private const string Retail = "Retail and commerce";
    private const string Manufacturing = "Manufacturing";
    private const string Property = "Real estate";
    private const string Building = "Construction and engineering";
    private const string Health = "Healthcare";
    private const string Transport = "Logistics and transport";
    private const string Services = "Professional services";
    private const string Schools = "Education";
    private const string Food = "Hospitality and food";
    private const string Farming = "Agriculture and livestock";
    private const string Corporate = "Financial and corporate";

    private static readonly string[] Supply = ["inventory", "purchasing", "sales"];
    private static readonly string[] Back = ["hr", "contracts", "expenses", "assets"];
    private static readonly string[] Weights = ["ea", "box", "carton", "kg", "g", "tonne", "litre", "ml", "metre", "pack"];
    private static readonly string[] Hours = ["hour", "day", "month", "project", "ea"];

    private static IndustryTemplate T(
        string key, string group, string label, string description, string[] modules, string[]? units = null, string[]? categories = null, string[]? focus = null,
        string[]? off = null, Dictionary<string, string>? words = null, int order = 100, IReadOnlyList<AccountTemplate>? accounts = null)
        => new(key, label, description, IndustryAvailability.Available, [.. modules, .. Back], Accounts: accounts, SortOrder: order, Group: group,
            Units: units ?? Weights, ItemCategories: categories, CapabilitiesOff: off, DashboardFocus: focus, Terminology: words);

    private static readonly string[] ChainFocus = ["sales", "gross-profit", "inventory", "branch", "receivables"];
    private static readonly string[] ProductionFocus = ["production", "gross-profit", "inventory", "shortages", "purchasing"];
    private static readonly string[] DistributionFocus = ["sales", "receivables", "inventory", "slow-stock", "purchasing"];
    private static readonly string[] ProjectFocus = ["project-profit", "receivables", "payables", "budget", "cash"];
    private static readonly string[] ServiceFocus = ["revenue", "project-profit", "receivables", "employees", "cash"];
    private static readonly string[] PropertyFocus = ["revenue", "receivables", "cash", "budget", "expenses"];

    private static readonly IndustryTemplate[] All =
    [
        // Trading, wholesale and distribution
        T("wholesale-general", Trading, "General wholesale", "Buy in bulk and sell to trade customers on credit.", [.. Supply, "distribution"], focus: DistributionFocus, categories: ["Fast movers", "Seasonal", "Bulk"], order: 100),
        T("wholesale-fmcg", Trading, "FMCG distribution", "Route sales, van stock and retailer credit.", [.. Supply, "distribution"], focus: DistributionFocus, categories: ["Beverages", "Snacks", "Household", "Personal care"], order: 101),
        T("wholesale-pharma", Trading, "Pharmaceutical distribution", "Licensed drug wholesale with quality checks.", [.. Supply, "distribution", "quality"], focus: DistributionFocus, categories: ["Prescription", "OTC", "Medical devices", "Cold chain"], order: 102),
        T("wholesale-electronics", Trading, "Electronics distribution", "Serial-sensitive goods sold to resellers.", [.. Supply, "distribution"], units: ["ea", "box", "carton"], focus: DistributionFocus, categories: ["Mobiles", "Computing", "Accessories", "Appliances"], order: 103),
        T("trade-import-export", Trading, "Import and export", "Letters of credit, consignments and landed costs.", [.. Supply, "trade"], focus: DistributionFocus, categories: ["Imported goods", "Export goods", "Transit"], order: 104),
        T("wholesale-agri", Trading, "Agricultural trading", "Commodities bought from growers and sold in bulk.", [.. Supply, "distribution", "quality"], units: ["kg", "tonne", "bag", "litre"], focus: DistributionFocus, categories: ["Grain", "Pulses", "Oilseed", "Fertilizer"], order: 105),
        T("wholesale-building", Trading, "Building materials", "Cement, steel and fittings sold to contractors.", [.. Supply, "distribution"], units: ["bag", "tonne", "metre", "sq m", "ea"], focus: DistributionFocus, categories: ["Cement", "Steel", "Tiles", "Plumbing", "Electrical"], order: 106),
        T("wholesale-food", Trading, "Food distribution", "Short shelf life goods for shops and kitchens.", [.. Supply, "distribution", "quality"], focus: DistributionFocus, categories: ["Dry", "Chilled", "Frozen", "Fresh"], order: 107),

        // Retail and commerce
        T("retail-supermarket", Retail, "Supermarket or grocery", "Tills, barcodes, promotions and store transfers.", [.. Supply, "pos", "retail"], focus: ChainFocus, categories: ["Grocery", "Dairy", "Produce", "Household", "Beverages"], order: 200),
        T("retail-clothing", Retail, "Clothing and fashion", "Seasonal collections and returns.", [.. Supply, "pos", "retail"], units: ["ea", "pair", "set"], focus: ChainFocus, categories: ["Menswear", "Womenswear", "Children", "Footwear"], order: 201),
        T("retail-electronics", Retail, "Electronics shop", "Consumer electronics with warranties.", [.. Supply, "pos", "retail"], units: ["ea", "box"], focus: ChainFocus, categories: ["Phones", "Computers", "Audio", "Accessories"], order: 202),
        T("retail-furniture", Retail, "Furniture showroom", "Showroom sales with delivery.", [.. Supply, "pos", "retail"], units: ["ea", "set"], focus: ChainFocus, categories: ["Living", "Bedroom", "Office", "Outdoor"], order: 203),
        T("retail-pharmacy", Retail, "Pharmacy chain", "Dispensing counter sales across branches.", [.. Supply, "pos", "retail", "quality"], focus: ChainFocus, categories: ["Prescription", "OTC", "Personal care", "Baby"], order: 204),
        T("retail-online", Retail, "Online store", "E-commerce orders and fulfilment.", [.. Supply, "retail", "crm"], focus: ChainFocus, categories: ["Featured", "Clearance"], order: 205),
        T("retail-chain", Retail, "Multi-branch retail", "Several stores under one owner.", [.. Supply, "pos", "retail"], focus: ChainFocus, order: 206),

        // Manufacturing
        T("mfg-textile", Manufacturing, "Textile", "Yarn to fabric with piece-rate labour.", [.. Supply, "manufacturing", "quality"], units: ["kg", "metre", "roll", "ea"], focus: ProductionFocus, categories: ["Raw material", "Yarn", "Greige", "Finished fabric"], order: 300),
        T("mfg-food", Manufacturing, "Food production", "Recipes, batches and quality checks.", [.. Supply, "manufacturing", "quality"], focus: ProductionFocus, categories: ["Ingredients", "Packaging", "Work in progress", "Finished goods"], order: 301),
        T("mfg-pharma", Manufacturing, "Pharmaceutical manufacturing", "Formulation under inspection.", [.. Supply, "manufacturing", "quality"], focus: ProductionFocus, categories: ["API", "Excipients", "Packaging", "Finished dosage"], order: 302),
        T("mfg-furniture", Manufacturing, "Furniture", "Boards and hardware into assemblies.", [.. Supply, "manufacturing", "quality"], units: ["ea", "sheet", "metre", "set"], focus: ProductionFocus, categories: ["Boards", "Hardware", "Finishing", "Finished goods"], order: 303),
        T("mfg-automotive", Manufacturing, "Automotive parts", "Components to customer schedules.", [.. Supply, "manufacturing", "quality"], units: ["ea", "kg", "set"], focus: ProductionFocus, categories: ["Raw material", "Bought-out", "Sub-assemblies", "Finished parts"], order: 304),
        T("mfg-electronics", Manufacturing, "Electronics assembly", "Boards and components into products.", [.. Supply, "manufacturing", "quality"], units: ["ea", "reel", "set"], focus: ProductionFocus, categories: ["Components", "PCBs", "Sub-assemblies", "Finished goods"], order: 305),
        T("mfg-chemical", Manufacturing, "Chemicals", "Formulations and process batches.", [.. Supply, "manufacturing", "quality"], units: ["kg", "litre", "drum", "tonne"], focus: ProductionFocus, categories: ["Raw chemicals", "Intermediates", "Finished products"], order: 306),
        T("mfg-packaging", Manufacturing, "Packaging", "Converted paper and plastics.", [.. Supply, "manufacturing", "quality"], units: ["ea", "kg", "roll", "bundle"], focus: ProductionFocus, categories: ["Board", "Film", "Ink", "Finished packs"], order: 307),
        T("mfg-industrial", Manufacturing, "Industrial goods", "Made to order engineered goods.", [.. Supply, "manufacturing", "quality", "projects"], focus: ProductionFocus, order: 308),

        // Real estate
        T("re-agency", Property, "Real estate agency", "Listings, leads and commissions.", ["realestate", "crm"], Hours, focus: PropertyFocus, order: 400),
        T("re-developer", Property, "Property developer", "Sale contracts with instalment plans.", ["realestate", "propertysales", "projects", "crm"], focus: PropertyFocus, order: 401),
        T("re-management", Property, "Property management", "Leases, rent, maintenance and owner statements.", ["realestate"], focus: PropertyFocus, words: new() { ["sales.customers"] = "Tenants", ["purchases.vendors"] = "Vendors and contractors" }, order: 402),
        T("re-society", Property, "Housing society", "Member allotments, dues and instalments.", ["realestate", "propertysales"], focus: PropertyFocus, words: new() { ["sales.customers"] = "Members" }, order: 403),
        T("re-commercial", Property, "Commercial property", "Offices and retail space on commercial leases.", ["realestate"], focus: PropertyFocus, words: new() { ["sales.customers"] = "Tenants" }, order: 404),
        T("re-investment", Property, "Property investment", "A portfolio with valuations and yields.", ["realestate", "propertysales"], focus: PropertyFocus, order: 405),

        // Construction and engineering
        T("con-contractor", Building, "General contractor", "BOQ, valuations, subcontractors and materials.", ["construction", "inventory", "purchasing", "projects"], units: ["cu m", "sq m", "metre", "tonne", "bag", "ea", "lump sum"], focus: ProjectFocus, categories: ["Cement", "Steel", "Aggregates", "Finishes", "MEP"], words: new() { ["purchases.vendors"] = "Suppliers and subcontractors" }, order: 500),
        T("con-civil", Building, "Civil and infrastructure", "Roads, utilities and large works.", ["construction", "inventory", "purchasing", "projects"], units: ["cu m", "sq m", "metre", "tonne", "lump sum"], focus: ProjectFocus, order: 501),
        T("con-mep", Building, "Mechanical and electrical", "Installation contracts and maintenance.", ["construction", "inventory", "purchasing", "projects"], units: ["ea", "metre", "lump sum", "hour"], focus: ProjectFocus, order: 502),
        T("con-architecture", Building, "Architecture and interior design", "Fee-based design and fit-out work.", ["projects", "crm"], Hours, focus: ServiceFocus, order: 503),

        // Healthcare
        T("health-hospital", Health, "Hospital", "Departments, patients, billing, pharmacy and claims.", ["healthcare", "inventory", "purchasing"], units: ["ea", "strip", "bottle", "box", "visit", "test"], focus: ["revenue", "receivables", "inventory", "expenses", "branch"], categories: ["Medicines", "Consumables", "Surgical", "Laboratory"], words: new() { ["sales.customers"] = "Billing accounts" }, order: 600),
        T("health-clinic", Health, "Clinic or medical centre", "Appointments and billing for a practice.", ["healthcare", "inventory"], units: ["ea", "visit", "box"], focus: ["revenue", "receivables", "cash", "expenses"], categories: ["Medicines", "Consumables"], words: new() { ["sales.customers"] = "Billing accounts" }, order: 601),
        T("health-lab", Health, "Laboratory", "Diagnostic orders and billing.", ["healthcare", "inventory"], units: ["test", "ea", "kit"], focus: ["revenue", "receivables", "inventory"], categories: ["Reagents", "Consumables"], words: new() { ["sales.customers"] = "Billing accounts" }, order: 602),

        // Logistics and transport
        T("log-freight", Transport, "Freight forwarding", "Consignments by road, air and sea.", ["logistics", "trade"], units: ["kg", "cbm", "trip", "container"], focus: ["revenue", "receivables", "expenses", "cash"], off: ["cheques"], order: 700),
        T("log-cargo", Transport, "Cargo and transport", "Own fleet carrying customer goods.", ["logistics"], units: ["kg", "trip", "tonne", "km"], focus: ["revenue", "receivables", "expenses", "cash"], order: 701),
        T("log-courier", Transport, "Courier and delivery", "Parcels and last-mile delivery.", ["logistics"], units: ["parcel", "kg", "trip"], focus: ["revenue", "receivables", "expenses"], order: 702),
        T("log-warehousing", Transport, "Warehousing", "Storage and handling for clients.", ["logistics", "inventory"], units: ["pallet", "cbm", "day", "ea"], focus: ["revenue", "receivables", "inventory"], order: 703),

        // Professional services
        T("svc-software", Services, "Software and IT services", "Projects, retainers and support.", ["projects", "services", "crm"], Hours, focus: ServiceFocus, off: ["cheques"], order: 800),
        T("svc-consulting", Services, "Consulting", "Engagements billed by time or milestone.", ["projects", "services", "crm"], Hours, focus: ServiceFocus, off: ["cheques"], order: 801),
        T("svc-marketing", Services, "Marketing agency", "Campaigns and monthly retainers.", ["projects", "services", "crm"], Hours, focus: ServiceFocus, order: 802),
        T("svc-legal", Services, "Legal practice", "Matters billed by time.", ["services", "crm"], Hours, focus: ServiceFocus, words: new() { ["sales.customers"] = "Clients" }, order: 803),
        T("svc-accounting", Services, "Accounting practice", "Recurring client work.", ["services", "crm"], Hours, focus: ServiceFocus, words: new() { ["sales.customers"] = "Clients" }, order: 804),
        T("svc-maintenance", Services, "Maintenance, cleaning or security", "Contracts with staff on sites.", ["services", "inventory"], ["hour", "visit", "month", "ea"], focus: ServiceFocus, order: 805),

        // Education
        T("edu-school", Schools, "School", "Admissions, classes, fees and exams.", ["education"], ["term", "month", "ea"], focus: ["revenue", "receivables", "cash", "employees"], off: ["cheques"], words: new() { ["sales.customers"] = "Fee payers" }, order: 900),
        T("edu-university", Schools, "College or university", "Programmes, sessions and fees.", ["education"], ["credit", "term", "ea"], focus: ["revenue", "receivables", "budget", "employees"], words: new() { ["sales.customers"] = "Fee payers" }, order: 901),
        T("edu-training", Schools, "Training institute or coaching centre", "Short courses and batches.", ["education", "crm"], ["course", "hour", "ea"], focus: ["revenue", "receivables", "cash"], words: new() { ["sales.customers"] = "Fee payers" }, order: 902),

        // Hospitality and food
        T("food-restaurant", Food, "Restaurant or cafe", "Tills, recipes, purchases and waste.", ["pos", "inventory", "purchasing", "hospitality", "manufacturing"], ["ea", "kg", "litre", "portion"], focus: ["sales", "gross-profit", "inventory", "cash"], categories: ["Food", "Beverage", "Packaging", "Ingredients"], order: 1000),
        T("food-hotel", Food, "Hotel or resort", "Rooms, folios and outlets.", ["hospitality", "pos", "inventory", "purchasing"], ["night", "ea", "kg", "litre"], focus: ["revenue", "receivables", "expenses", "cash"], words: new() { ["sales.customers"] = "Guests and companies" }, order: 1001),
        T("food-catering", Food, "Catering or cafeteria", "Events and contract meals.", ["hospitality", "inventory", "purchasing", "manufacturing"], ["portion", "kg", "litre", "ea"], focus: ["revenue", "gross-profit", "inventory"], order: 1002),

        // Agriculture and livestock
        T("agri-dairy", Farming, "Dairy farm", "Herd, milk, feed and vet costs.", ["agriculture", "inventory", "purchasing", "sales"], ["litre", "kg", "bag", "ea"], focus: ["revenue", "gross-profit", "inventory", "expenses"], categories: ["Feed", "Veterinary", "Milk products"], order: 1100),
        T("agri-poultry", Farming, "Poultry", "Flocks, eggs, feed and mortality.", ["agriculture", "inventory", "purchasing", "sales"], ["tray", "kg", "bag", "ea"], focus: ["revenue", "gross-profit", "inventory", "expenses"], categories: ["Feed", "Veterinary", "Eggs", "Birds"], order: 1101),
        T("agri-livestock", Farming, "Livestock", "Herds raised for sale.", ["agriculture", "inventory", "purchasing", "sales"], ["head", "kg", "bag"], focus: ["revenue", "gross-profit", "inventory"], order: 1102),
        T("agri-crops", Farming, "Crop farming", "Fields, inputs and harvests.", ["agriculture", "inventory", "purchasing", "sales"], ["kg", "tonne", "bag", "hectare"], focus: ["revenue", "inventory", "expenses"], categories: ["Seed", "Fertilizer", "Harvested goods"], order: 1103),
        T("agri-supplier", Farming, "Agricultural supplier", "Inputs sold to farmers.", [.. Supply, "pos"], ["bag", "kg", "litre", "ea"], focus: DistributionFocus, categories: ["Seed", "Fertilizer", "Pesticide", "Tools"], order: 1104),
        T("agri-processing", Farming, "Food processing", "Farm output into packed goods.", [.. Supply, "manufacturing", "quality", "agriculture"], focus: ProductionFocus, order: 1105),

        // Financial and corporate
        T("corp-holding", Corporate, "Holding company", "Several companies under one owner.", [], focus: ["revenue", "branch", "cash", "budget"], order: 1200),
        T("corp-investment", Corporate, "Investment company", "Portfolios, contracts and assets.", [], focus: ["revenue", "cash", "budget"], order: 1201),
        T("corp-group", Corporate, "Multi-company group", "Shared services across companies.", ["purchasing"], focus: ["revenue", "branch", "payables", "budget"], order: 1202),
        T("corp-financial", Corporate, "Financial services", "Fee income and client accounts.", ["crm", "services"], Hours, focus: ["revenue", "receivables", "cash"], order: 1203),
    ];
}
