using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Finance;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Inventory;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Aliases to resolve conflicts across modules
using MfgWorkOrder = ERP.Modules.Manufacturing.Domain.WorkOrder;
using ReWorkOrder = ERP.Modules.RealEstate.Domain.WorkOrder;
using MfgWorkOrderStatus = ERP.Modules.Manufacturing.Domain.WorkOrderStatus;
using ReWorkOrderStatus = ERP.Modules.RealEstate.Domain.WorkOrderStatus;
using ReLead = ERP.Modules.RealEstate.Domain.Lead;
using CrmLead = ERP.Modules.Crm.Domain.CrmLead;
using RePriority = ERP.Modules.RealEstate.Domain.Priority;
using SvcMilestoneStatus = ERP.Modules.Services.Domain.MilestoneStatus;

public static class ProgramSeeder
{
    public static async Task SeedModulesAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        var env = sp.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        if (env is not null && (env.IsEnvironment("Testing") || env.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))) return;

        var config = sp.GetRequiredService<IConfiguration>();
        var section = config.GetSection("Seed");
        if (!section.GetValue<bool>("Enabled")) return;

        var orgs = await sp.GetRequiredService<AppDbContext>().Organizations.ToListAsync(ct);
        if (orgs.Count == 0) return;

        foreach (var org in orgs)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var orgCtx = scope.ServiceProvider.GetRequiredService<IOrgContext>();
            var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ProgramSeeder");

            using (orgCtx.Use(org.Id))
            {
                var legalEntity = await db.LegalEntities.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.OrgId == org.Id, ct);
                if (legalEntity is null)
                {
                    log.LogWarning("Skipping org '{OrgCode}' - no LegalEntity found.", org.Code);
                    continue;
                }

                orgCtx.SetLegalEntity(legalEntity.Id);

                await EnableModulesAsync(db, sp, org.Id, ct);
                await EnsureAdminAccessAsync(db, org.Id, ct);
                await SeedNumberSequencesAsync(db, org.Id, legalEntity.Id, ct);

                // Core master data & finance
                await SeedMasterDataAsync(db, org.Id, legalEntity.Id, ct);
                await SeedFinanceAsync(db, org.Id, legalEntity.Id, ct);

                // 1. Real Estate
                await SeedRealEstateAsync(db, org.Id, legalEntity.Id, ct);

                // 2. HR & Payroll
                await SeedHrAsync(db, org.Id, legalEntity.Id, ct);

                // 3. Inventory
                await SeedInventoryAsync(db, org.Id, legalEntity.Id, ct);

                // 4. Sales
                await SeedSalesAsync(db, org.Id, legalEntity.Id, ct);

                // 5. Purchasing
                await SeedPurchasingAsync(db, org.Id, legalEntity.Id, ct);

                // 6. POS
                await SeedPosAsync(db, org.Id, legalEntity.Id, ct);

                // 7. Retail
                await SeedRetailAsync(db, org.Id, legalEntity.Id, ct);

                // 8. Distribution
                await SeedDistributionAsync(db, org.Id, legalEntity.Id, ct);

                // 9. Manufacturing
                await SeedManufacturingAsync(db, org.Id, legalEntity.Id, ct);

                // 10. CRM
                await SeedCrmAsync(db, org.Id, legalEntity.Id, ct);

                // 11. Professional Services
                await SeedServicesAsync(db, org.Id, legalEntity.Id, ct);

                // 12. Projects
                await SeedProjectsAsync(db, org.Id, legalEntity.Id, ct);

                // 13. Comprehensive 2-Year Historical Data (24 months of activity)
                await SeedTwoYearHistoryAsync(db, org.Id, legalEntity.Id, ct);

                await db.SaveChangesAsync(ct);
                log.LogInformation("Successfully verified and seeded demo data for workspace '{OrgName}' ({OrgCode}).", org.Name, org.Code);
            }
        }
    }

    private static async Task EnableModulesAsync(AppDbContext db, IServiceProvider sp, Guid orgId, CancellationToken ct)
    {
        var modulesToEnable = new[]
        {
            "sales", "purchasing", "pos", "retail", "distribution",
            "manufacturing", "crm", "services", "projects", "inventory", "hr", "realestate"
        };

        foreach (var mod in modulesToEnable)
        {
            var existing = await db.OrganizationModules.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.OrgId == orgId && m.ModuleKey == mod, ct);
            if (existing is null)
            {
                db.OrganizationModules.Add(new OrganizationModule
                {
                    OrgId = orgId,
                    ModuleKey = mod,
                    IsEnabled = true,
                    EnabledAt = DateTime.UtcNow
                });
            }
            else if (!existing.IsEnabled)
            {
                existing.IsEnabled = true;
                existing.EnabledAt = DateTime.UtcNow;
                existing.DisabledAt = null;
            }
        }
        await db.SaveChangesAsync(ct);

        var cache = sp.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        cache?.Remove(ERP.Infrastructure.Services.ModuleAccess.CacheKey(orgId));
    }

    private static async Task EnsureAdminAccessAsync(AppDbContext db, Guid orgId, CancellationToken ct)
    {
        var adminRole = await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.OrgId == orgId && r.Name == "Org admin", ct);
        if (adminRole is null) return;

        var users = await db.Users.Where(u => u.Email == "owner@demo.local" || u.Email == "admin@demo.local").ToListAsync(ct);
        foreach (var user in users)
        {
            if (!await db.OrganizationMembers.IgnoreQueryFilters().AnyAsync(m => m.OrgId == orgId && m.UserId == user.Id, ct))
            {
                db.OrganizationMembers.Add(new OrganizationMember { OrgId = orgId, UserId = user.Id, JoinedAt = DateTime.UtcNow, IsActive = true });
            }

            if (!await db.UserRoles.IgnoreQueryFilters().AnyAsync(ur => ur.OrgId == orgId && ur.UserId == user.Id && ur.RoleId == adminRole.Id, ct))
            {
                db.UserRoles.Add(new UserRole { OrgId = orgId, UserId = user.Id, RoleId = adminRole.Id });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedNumberSequencesAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var docTypes = new[]
        {
            ("sales_quote", "QT"), ("sales_order", "SO"),
            ("purchase_requisition", "PR"), ("purchase_order", "PO"), ("goods_receipt", "GR"),
            ("pos_order", "POS"), ("pos_register", "REG"),
            ("retail_store", "STR"), ("retail_pricelist", "PL"), ("retail_promo", "PRM"),
            ("dist_route", "RTE"), ("dist_trip", "TRIP"),
            ("mfg_bom", "BOM"), ("mfg_workorder", "WO"), ("mfg_costing", "CR"),
            ("crm_lead", "LEAD"), ("crm_opportunity", "OPP"),
            ("svc_engagement", "ENG"), ("svc_retainer", "RET"), ("svc_milestone", "MLS"),
            ("project", "PRJ"),
            ("property", "PROP"), ("unit", "UNT"), ("lease", "LSE"), ("re_workorder", "WO"),
            ("acquisition", "ACQ"), ("deal", "DEAL"), ("listing", "LST"),
            ("employee", "EMP"), ("payroll", "PR"),
            ("warehouse", "WH"),
            ("party", "PTY"), ("item", "ITM"), ("invoice", "INV"), ("bill", "BIL"),
            ("settlement", "SET"), ("journal", "JRN"), ("cheque", "CHQ")
        };

        foreach (var (docType, prefix) in docTypes)
        {
            if (!await db.NumberSequences.AnyAsync(s => s.DocumentType == docType, ct))
            {
                db.NumberSequences.Add(new NumberSequence
                {
                    DocumentType = docType,
                    Prefix = prefix,
                    Pattern = "{PREFIX}-{YYYY}-{SEQ:4}",
                    ResetYearly = true,
                    Year = DateTime.UtcNow.Year,
                    NextValue = 10
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedMasterDataAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        // 1. Business Units
        if (!await db.BusinessUnits.AnyAsync(ct))
        {
            db.BusinessUnits.AddRange(
                new BusinessUnit { LegalEntityId = legalEntityId, Code = "BU-COMM", Name = "Commercial & Enterprise Division", IsActive = true },
                new BusinessUnit { LegalEntityId = legalEntityId, Code = "BU-RES", Name = "Residential & Property Portfolio", IsActive = true },
                new BusinessUnit { LegalEntityId = legalEntityId, Code = "BU-RET", Name = "Retail & Consumer Operations", IsActive = true }
            );
            await db.SaveChangesAsync(ct);
        }

        // 2. Parties
        if (!await db.Parties.AnyAsync(ct))
        {
            db.Parties.AddRange(
                new Party
                {
                    Code = "CUST-001",
                    Name = "Apex Global Trading W.L.L.",
                    Kind = PartyKind.Company,
                    Roles = PartyRoles.Customer,
                    Email = "procurement@apexglobal.example.com",
                    Phone = "+974 4398 1234",
                    TaxNumber = "TC-482917",
                    Address = "Sheikh Zayed Road, Latifa Tower Suite 1802, Doha, Qatar",
                    CurrencyCode = "QAR",
                    PaymentTermsDays = 30,
                    IsActive = true
                },
                new Party
                {
                    Code = "CUST-002",
                    Name = "Oasis Retailers Corp",
                    Kind = PartyKind.Company,
                    Roles = PartyRoles.Customer,
                    Email = "supplychain@oasisretail.example.com",
                    Phone = "+974 4456 7890",
                    TaxNumber = "TC-389172",
                    Address = "Al Quoz Industrial Area 3, Doha, Qatar",
                    CurrencyCode = "QAR",
                    PaymentTermsDays = 30,
                    IsActive = true
                },
                new Party
                {
                    Code = "CUST-003",
                    Name = "Qatari Diar Investment Q.P.S.C.",
                    Kind = PartyKind.Company,
                    Roles = PartyRoles.Customer,
                    Email = "investments@emaar.example.com",
                    Phone = "+974 4367 3333",
                    TaxNumber = "TC-112233",
                    Address = "Msheireb Downtown Bldg 3, Doha, Qatar",
                    CurrencyCode = "QAR",
                    PaymentTermsDays = 45,
                    IsActive = true
                },
                new Party
                {
                    Code = "VEND-001",
                    Name = "TechSupply Electronics International",
                    Kind = PartyKind.Company,
                    Roles = PartyRoles.Vendor,
                    Email = "sales@techsupply.example.com",
                    Phone = "+974 4881 5566",
                    TaxNumber = "TC-492817",
                    Address = "Jebel Ali Free Zone, South Zone, Warehouse 14B, Doha",
                    CurrencyCode = "QAR",
                    PaymentTermsDays = 60,
                    IsActive = true
                },
                new Party
                {
                    Code = "VEND-002",
                    Name = "Al Futtaim Facilities Management W.L.L.",
                    Kind = PartyKind.Company,
                    Roles = PartyRoles.Vendor,
                    Email = "service@alfuttaimfm.example.com",
                    Phone = "+974 4213 1122",
                    TaxNumber = "TC-581920",
                    Address = "Lusail Marina, Tower 2, Floor 8, Doha",
                    CurrencyCode = "QAR",
                    PaymentTermsDays = 30,
                    IsActive = true
                },
                new Party
                {
                    Code = "VEND-003",
                    Name = "Qatar General Electricity & Water Corporation (Kahramaa)",
                    Kind = PartyKind.Company,
                    Roles = PartyRoles.Vendor,
                    Email = "customercare@kahramaa.com.qa",
                    Phone = "+974 4601 9999",
                    TaxNumber = "TC-000000",
                    Address = "Near Wafi Mall, Zabeel East, Doha, Qatar",
                    CurrencyCode = "QAR",
                    PaymentTermsDays = 15,
                    IsActive = true
                },
                new Party
                {
                    Code = "PART-001",
                    Name = "Michael Schumacher",
                    Kind = PartyKind.Individual,
                    Roles = PartyRoles.Customer,
                    Email = "m.schumacher@example.com",
                    Phone = "+974 5012 3456",
                    Address = "Pearl Bay Heights Unit 101, The Pearl-Qatar, Doha, Qatar",
                    CurrencyCode = "QAR",
                    IsActive = true
                },
                new Party
                {
                    Code = "PART-002",
                    Name = "Amina Al-Mansouri",
                    Kind = PartyKind.Individual,
                    Roles = PartyRoles.Customer,
                    Email = "amina.mansouri@example.com",
                    Phone = "+974 5598 7654",
                    Address = "West Bay Executive Tower Unit 201, Doha, Qatar",
                    CurrencyCode = "QAR",
                    IsActive = true
                },
                new Party
                {
                    Code = "EMP-001",
                    Name = "Tariq Al-Hashimi",
                    Kind = PartyKind.Individual,
                    Roles = PartyRoles.Employee,
                    Email = "tariq@demo.local",
                    Phone = "+974 5233 4455",
                    Address = "Al Barsha 2, Villa 18, Doha, Qatar",
                    CurrencyCode = "QAR",
                    IsActive = true
                },
                new Party
                {
                    Code = "EMP-002",
                    Name = "Sarah Jenkins",
                    Kind = PartyKind.Individual,
                    Roles = PartyRoles.Employee,
                    Email = "sarah.j@demo.local",
                    Phone = "+974 5077 6554",
                    Address = "West Bay Lagoon Residence, Sadaf 4, Doha",
                    CurrencyCode = "QAR",
                    IsActive = true
                }
            );
            await db.SaveChangesAsync(ct);
        }

        // 2b. Qatar levies no VAT: one default 0% tax code so documents and reports have a code to carry
        if (!await db.TaxCodes.AnyAsync(ct))
        {
            db.TaxCodes.Add(new TaxCode { Code = "QA0", Name = "No VAT (Qatar)", Kind = TaxKind.Vat, Rate = 0m, IsDefault = true, IsRecoverable = false });
            await db.SaveChangesAsync(ct);
        }

        // 3. Items (Catalog)
        if (!await db.Items.AnyAsync(ct))
        {
            var taxCode = await db.TaxCodes.FirstOrDefaultAsync(t => t.IsDefault, ct);
            var incomeAcc = await db.Accounts.FirstOrDefaultAsync(a => a.Type == AccountType.Income, ct);
            var expenseAcc = await db.Accounts.FirstOrDefaultAsync(a => a.Type == AccountType.Expense, ct);

            db.Items.AddRange(
                new Item
                {
                    Sku = "ERP-CORE",
                    Name = "Enterprise ERP Core License & Database",
                    Type = ItemType.Service,
                    Uom = "ea",
                    SalesPrice = 95000m,
                    PurchasePrice = 45000m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                },
                new Item
                {
                    Sku = "SRV-IMPL",
                    Name = "Professional Implementation, Customization & Go-Live Support",
                    Type = ItemType.Service,
                    Uom = "day",
                    SalesPrice = 3500m,
                    PurchasePrice = 2000m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                },
                new Item
                {
                    Sku = "POS-TERM-01",
                    Name = "Dual-Screen Touch POS Terminal i5/8GB/SSD",
                    Type = ItemType.Product,
                    Uom = "pcs",
                    SalesPrice = 4500m,
                    PurchasePrice = 2800m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                },
                new Item
                {
                    Sku = "POS-SCAN-2D",
                    Name = "Wireless Omni-directional 2D Barcode Scanner",
                    Type = ItemType.Product,
                    Uom = "pcs",
                    SalesPrice = 850m,
                    PurchasePrice = 480m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                },
                new Item
                {
                    Sku = "POS-PRN-80",
                    Name = "Thermal High-Speed Receipt Printer 80mm USB/LAN",
                    Type = ItemType.Product,
                    Uom = "pcs",
                    SalesPrice = 1500m,
                    PurchasePrice = 950m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                },
                new Item
                {
                    Sku = "HW-IOT-GW02",
                    Name = "Industrial IoT Edge Gateway Hub",
                    Type = ItemType.Product,
                    Uom = "pcs",
                    SalesPrice = 2200m,
                    PurchasePrice = 1350m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                },
                new Item
                {
                    Sku = "SRV-FM-MAINT",
                    Name = "HVAC & Mechanical Preventive Maintenance",
                    Type = ItemType.Service,
                    Uom = "mth",
                    SalesPrice = 3500m,
                    PurchasePrice = 2200m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                },
                new Item
                {
                    Sku = "SRV-PROP-MGMT",
                    Name = "Monthly Asset & Property Management Fee",
                    Type = ItemType.Service,
                    Uom = "mth",
                    SalesPrice = 5000m,
                    PurchasePrice = 0m,
                    TaxCodeId = taxCode?.Id,
                    IncomeAccountId = incomeAcc?.Id,
                    ExpenseAccountId = expenseAcc?.Id,
                    IsActive = true
                }
            );
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedFinanceAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var cust1 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "CUST-001", ct);
        var cust2 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "CUST-002", ct);
        var cust3 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "CUST-003", ct);
        var vend1 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "VEND-001", ct);
        var vend2 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "VEND-002", ct);

        var itemCore = await db.Items.FirstOrDefaultAsync(i => i.Sku == "ERP-CORE", ct);
        var itemImpl = await db.Items.FirstOrDefaultAsync(i => i.Sku == "SRV-IMPL", ct);
        var itemPos = await db.Items.FirstOrDefaultAsync(i => i.Sku == "POS-TERM-01", ct);
        var taxCode = await db.TaxCodes.FirstOrDefaultAsync(t => t.IsDefault, ct);
        var bankAcc = await db.Accounts.FirstOrDefaultAsync(a => a.IsBank, ct);
        var arAcc = await db.Accounts.FirstOrDefaultAsync(a => a.SystemRole == "ar.control" || a.Type == AccountType.Asset, ct);
        var apAcc = await db.Accounts.FirstOrDefaultAsync(a => a.SystemRole == "ap.control" || a.Type == AccountType.Liability, ct);
        var incomeAcc = await db.Accounts.FirstOrDefaultAsync(a => a.Type == AccountType.Income, ct);
        var expenseAcc = await db.Accounts.FirstOrDefaultAsync(a => a.Type == AccountType.Expense, ct);

        // 1. Invoices
        if (!await db.Invoices.AnyAsync(ct))
        {
            var inv1 = new Invoice
            {
                LegalEntityId = legalEntityId,
                Number = "INV-2026-0001",
                IsCreditNote = false,
                PartyId = cust1?.Id ?? Guid.NewGuid(),
                PartyName = cust1?.Name ?? "Apex Global Trading W.L.L.",
                InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
                Net = 95000m,
                Tax = 0m,
                Total = 95000m,
                Paid = 95000m,
                Status = DocumentStatus.Paid,
                Notes = "Enterprise ERP Cloud Platform Deployment",
                Lines = new List<InvoiceLine>
                {
                    new()
                    {
                        Seq = 1,
                        ItemId = itemCore?.Id,
                        Description = "Enterprise ERP Core License & Multi-tenant Database",
                        Quantity = 1,
                        Price = 95000m,
                        TaxCodeId = taxCode?.Id,
                        TaxAmount = 0m
                    }
                }
            };

            var inv2 = new Invoice
            {
                LegalEntityId = legalEntityId,
                Number = "INV-2026-0002",
                IsCreditNote = false,
                PartyId = cust2?.Id ?? Guid.NewGuid(),
                PartyName = cust2?.Name ?? "Oasis Retailers Corp",
                InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(22)),
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
                Net = 68500m,
                Tax = 0m,
                Total = 68500m,
                Paid = 30000m,
                Status = DocumentStatus.PartlyPaid,
                Notes = "POS Hardware Fleet Deployment for Villaggio Mall",
                Lines = new List<InvoiceLine>
                {
                    new()
                    {
                        Seq = 1,
                        ItemId = itemPos?.Id,
                        Description = "Dual-Screen Touch POS Terminal i5/8GB/SSD (10 units)",
                        Quantity = 10,
                        Price = 4500m,
                        TaxCodeId = taxCode?.Id,
                        TaxAmount = 0m
                    },
                    new()
                    {
                        Seq = 2,
                        ItemId = itemImpl?.Id,
                        Description = "On-site Installation, Staff Training and Staging",
                        Quantity = 1,
                        Price = 23500m,
                        TaxCodeId = taxCode?.Id,
                        TaxAmount = 0m
                    }
                }
            };

            var inv3 = new Invoice
            {
                LegalEntityId = legalEntityId,
                Number = "INV-2026-0003",
                IsCreditNote = false,
                PartyId = cust3?.Id ?? Guid.NewGuid(),
                PartyName = cust3?.Name ?? "Qatari Diar Investment Q.P.S.C.",
                InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(28)),
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
                Net = 35000m,
                Tax = 0m,
                Total = 35000m,
                Paid = 0m,
                Status = DocumentStatus.Issued,
                Notes = "Portfolio Valuation & Real Estate Advisory Fee",
                Lines = new List<InvoiceLine>
                {
                    new()
                    {
                        Seq = 1,
                        ItemId = itemImpl?.Id,
                        Description = "Quarterly Strategic Portfolio & Financial Audit",
                        Quantity = 10,
                        Price = 3500m,
                        TaxCodeId = taxCode?.Id,
                        TaxAmount = 0m
                    }
                }
            };

            db.Invoices.AddRange(inv1, inv2, inv3);
            await db.SaveChangesAsync(ct);
        }

        // 2. Bills
        if (!await db.Bills.AnyAsync(ct))
        {
            var bill1 = new Bill
            {
                LegalEntityId = legalEntityId,
                Number = "BIL-2026-0001",
                PartyId = vend1?.Id ?? Guid.NewGuid(),
                PartyName = vend1?.Name ?? "TechSupply Electronics International",
                VendorReference = "TSE-INV-8841",
                BillDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
                Net = 42500m,
                Tax = 0m,
                Total = 42500m,
                Paid = 42500m,
                Status = DocumentStatus.Paid,
                Notes = "Rugged Handheld Barcode Scanners and Multi-bay Docks",
                Lines = new List<BillLine>
                {
                    new()
                    {
                        Seq = 1,
                        ItemId = itemPos?.Id,
                        Description = "Industrial Mobile Scanners & Multi-Dock Kits",
                        Quantity = 15,
                        Price = 2500m,
                        TaxCodeId = taxCode?.Id,
                        TaxAmount = 0m,
                        Net = 37500m,
                        AccountId = expenseAcc?.Id
                    },
                    new()
                    {
                        Seq = 2,
                        Description = "Charging Cradles",
                        Quantity = 5,
                        Price = 1000m,
                        TaxCodeId = taxCode?.Id,
                        TaxAmount = 0m,
                        Net = 5000m,
                        AccountId = expenseAcc?.Id
                    }
                }
            };

            var bill2 = new Bill
            {
                LegalEntityId = legalEntityId,
                Number = "BIL-2026-0002",
                PartyId = vend2?.Id ?? Guid.NewGuid(),
                PartyName = vend2?.Name ?? "Al Futtaim Facilities Management W.L.L.",
                VendorReference = "AF-FM-9021",
                BillDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)),
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
                Net = 25000m,
                Tax = 0m,
                Total = 25000m,
                Paid = 0m,
                Status = DocumentStatus.Issued,
                Notes = "Monthly Chiller and Building Maintenance Retainer",
                Lines = new List<BillLine>
                {
                    new()
                    {
                        Seq = 1,
                        Description = "Commercial Chiller & HVAC System Preventative Servicing",
                        Quantity = 1,
                        Price = 25000m,
                        TaxCodeId = taxCode?.Id,
                        TaxAmount = 0m,
                        Net = 25000m,
                        AccountId = expenseAcc?.Id
                    }
                }
            };

            db.Bills.AddRange(bill1, bill2);
            await db.SaveChangesAsync(ct);
        }

        // 3. Journal Entries
        if (!await db.JournalEntries.AnyAsync(ct))
        {
            var jrn1 = new JournalEntry
            {
                LegalEntityId = legalEntityId,
                Number = "JRN-2026-0001",
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
                Memo = "Opening Equity Capital Contribution & Cash Injection",
                Reference = "CAP-INJ-01",
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
                Status = JournalStatus.Posted,
                Amount = 500000m,
                PostedAt = DateTime.UtcNow.AddDays(-30),
                Lines = new List<JournalLine>
                {
                    new()
                    {
                        Seq = 1,
                        AccountId = bankAcc?.Id ?? Guid.NewGuid(),
                        Debit = 500000m,
                        Credit = 0m,
                        Description = "Cash at Bank - QNB Operating Account"
                    },
                    new()
                    {
                        Seq = 2,
                        AccountId = arAcc?.Id ?? Guid.NewGuid(),
                        Debit = 0m,
                        Credit = 500000m,
                        Description = "Shareholder Capital / Owner's Equity"
                    }
                }
            };

            var jrn2 = new JournalEntry
            {
                LegalEntityId = legalEntityId,
                Number = "JRN-2026-0002",
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                Memo = "Monthly Technology Depreciation & Amortization Provision",
                Reference = "DEP-M09-2026",
                CurrencyCode = "QAR",
                ExchangeRate = 1m,
                Status = JournalStatus.Posted,
                Amount = 12500m,
                PostedAt = DateTime.UtcNow.AddDays(-10),
                Lines = new List<JournalLine>
                {
                    new()
                    {
                        Seq = 1,
                        AccountId = expenseAcc?.Id ?? Guid.NewGuid(),
                        Debit = 12500m,
                        Credit = 0m,
                        Description = "Depreciation Expense - POS & Server Hardware"
                    },
                    new()
                    {
                        Seq = 2,
                        AccountId = apAcc?.Id ?? Guid.NewGuid(),
                        Debit = 0m,
                        Credit = 12500m,
                        Description = "Accumulated Depreciation - Plant & Equipment"
                    }
                }
            };

            db.JournalEntries.AddRange(jrn1, jrn2);
            await db.SaveChangesAsync(ct);
        }

        // 4. Payment Instruments (Cheques / PDCs)
        if (!await db.PaymentInstruments.AnyAsync(ct))
        {
            db.PaymentInstruments.AddRange(
                new PaymentInstrument
                {
                    LegalEntityId = legalEntityId,
                    Number = "CHQ-2026-0001",
                    Kind = InstrumentKind.PostDatedCheque,
                    Direction = InstrumentDirection.Received,
                    InstrumentNo = "408192",
                    BankName = "QNB",
                    Amount = 30000m,
                    CurrencyCode = "QAR",
                    InstrumentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
                    ReceivedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                    PartyId = cust2?.Id,
                    PartyName = cust2?.Name,
                    Status = InstrumentStatus.InCustody,
                    BankAccountId = bankAcc?.Id,
                    Notes = "Q4 Instalment PDC held in office vault"
                },
                new PaymentInstrument
                {
                    LegalEntityId = legalEntityId,
                    Number = "CHQ-2026-0002",
                    Kind = InstrumentKind.PostDatedCheque,
                    Direction = InstrumentDirection.Received,
                    InstrumentNo = "889102",
                    BankName = "First Al Khor Bank",
                    Amount = 25000m,
                    CurrencyCode = "QAR",
                    InstrumentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)),
                    ReceivedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                    PartyId = cust1?.Id,
                    PartyName = cust1?.Name,
                    Status = InstrumentStatus.Deposited,
                    BankAccountId = bankAcc?.Id,
                    DepositedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    Notes = "Deposited at branch drop box for clearance"
                },
                new PaymentInstrument
                {
                    LegalEntityId = legalEntityId,
                    Number = "CHQ-2026-0003",
                    Kind = InstrumentKind.Cheque,
                    Direction = InstrumentDirection.Issued,
                    InstrumentNo = "100412",
                    BankName = "Masraf Al Rayan",
                    Amount = 42500m,
                    CurrencyCode = "QAR",
                    InstrumentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-12)),
                    ReceivedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15)),
                    PartyId = vend1?.Id,
                    PartyName = vend1?.Name,
                    Status = InstrumentStatus.Cleared,
                    BankAccountId = bankAcc?.Id,
                    ClearedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8)),
                    Notes = "Settlement for PO-2026-0001 delivered hardware"
                }
            );
            await db.SaveChangesAsync(ct);
        }

        // 5. Settlements (Receipts & Payments)
        if (!await db.Settlements.AnyAsync(ct))
        {
            var inv1 = await db.Invoices.FirstOrDefaultAsync(i => i.Number == "INV-2026-0001", ct);
            var bill1 = await db.Bills.FirstOrDefaultAsync(b => b.Number == "BIL-2026-0001", ct);

            db.Settlements.AddRange(
                new Settlement
                {
                    LegalEntityId = legalEntityId,
                    Number = "REC-2026-0001",
                    Direction = InstrumentDirection.Received,
                    PartyId = cust1?.Id ?? Guid.NewGuid(),
                    PartyName = cust1?.Name ?? "Apex Global Trading W.L.L.",
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15)),
                    Method = SettlementMethod.BankTransfer,
                    BankAccountId = bankAcc?.Id,
                    CurrencyCode = "QAR",
                    Amount = 95000m,
                    Unallocated = 0m,
                    Reference = "FT-ENBD-88491",
                    Status = DocumentStatus.Paid,
                    Allocations = inv1 != null ? new List<SettlementAllocation>
                    {
                        new() { InvoiceId = inv1.Id, Amount = 95000m }
                    } : new()
                },
                new Settlement
                {
                    LegalEntityId = legalEntityId,
                    Number = "PAY-2026-0001",
                    Direction = InstrumentDirection.Issued,
                    PartyId = vend1?.Id ?? Guid.NewGuid(),
                    PartyName = vend1?.Name ?? "TechSupply Electronics International",
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                    Method = SettlementMethod.BankTransfer,
                    BankAccountId = bankAcc?.Id,
                    CurrencyCode = "QAR",
                    Amount = 42500m,
                    Unallocated = 0m,
                    Reference = "SWIFT-OUT-49120",
                    Status = DocumentStatus.Paid,
                    Allocations = bill1 != null ? new List<SettlementAllocation>
                    {
                        new() { BillId = bill1.Id, Amount = 42500m }
                    } : new()
                }
            );
            await db.SaveChangesAsync(ct);
        }

        // 6. Budgets
        if (!await db.Budgets.AnyAsync(ct))
        {
            var budget = new Budget
            {
                LegalEntityId = legalEntityId,
                Name = "Corporate Operating Budget 2026",
                FiscalYear = 2026,
                IsActive = true,
                Notes = "Board approved annual operating budget across commercial units"
            };

            if (incomeAcc != null)
            {
                for (int m = 1; m <= 12; m++)
                {
                    budget.Lines.Add(new BudgetLine
                    {
                        AccountId = incomeAcc.Id,
                        Month = m,
                        Amount = 250000m
                    });
                }
            }
            if (expenseAcc != null)
            {
                for (int m = 1; m <= 12; m++)
                {
                    budget.Lines.Add(new BudgetLine
                    {
                        AccountId = expenseAcc.Id,
                        Month = m,
                        Amount = 145000m
                    });
                }
            }
            db.Budgets.Add(budget);
            await db.SaveChangesAsync(ct);
        }

        // 7. Bank Statement Lines (Reconciliation)
        if (!await db.BankStatementLines.AnyAsync(ct))
        {
            if (bankAcc != null)
            {
                db.BankStatementLines.AddRange(
                    new BankStatementLine
                    {
                        LegalEntityId = legalEntityId,
                        AccountId = bankAcc.Id,
                        Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15)),
                        Description = "DIRECT DEPOSIT APEX GLOBAL TRADING REF 88491",
                        Reference = "CR-88491",
                        Amount = 95000m,
                        Status = StatementLineStatus.Matched
                    },
                    new BankStatementLine
                    {
                        LegalEntityId = legalEntityId,
                        AccountId = bankAcc.Id,
                        Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                        Description = "TELEGRAPHIC TRANSFER TECHSUPPLY ELEC",
                        Reference = "DR-49120",
                        Amount = -42500m,
                        Status = StatementLineStatus.Matched
                    },
                    new BankStatementLine
                    {
                        LegalEntityId = legalEntityId,
                        AccountId = bankAcc.Id,
                        Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                        Description = "POS SETTLEMENT DUBAI MALL TERMINAL BATCH 104",
                        Reference = "CR-POS-104",
                        Amount = 4650m,
                        Status = StatementLineStatus.Unmatched
                    }
                );
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static async Task SeedRealEstateAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var tenant1 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "PART-001", ct);
        var tenant2 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "PART-002", ct);
        var tenant3 = await db.Parties.FirstOrDefaultAsync(p => p.Code == "CUST-001", ct);
        var vendorFm = await db.Parties.FirstOrDefaultAsync(p => p.Code == "VEND-002", ct);
        var agentUser = await db.Parties.FirstOrDefaultAsync(p => p.Code == "EMP-001", ct);

        // 1. Unit Types
        var utSet = db.Set<UnitType>();
        if (!await utSet.AnyAsync(ct))
        {
            utSet.AddRange(
                new UnitType { Code = "UT-STU", Name = "Studio Apartment", DefaultUsage = UnitUsage.Residential, DefaultBedrooms = 0, IsActive = true },
                new UnitType { Code = "UT-1BR", Name = "1 Bedroom Modern Suite", DefaultUsage = UnitUsage.Residential, DefaultBedrooms = 1, IsActive = true },
                new UnitType { Code = "UT-2BR", Name = "2 Bedroom Luxury Residence", DefaultUsage = UnitUsage.Residential, DefaultBedrooms = 2, IsActive = true },
                new UnitType { Code = "UT-3BR", Name = "3 Bedroom Penthouse Suite", DefaultUsage = UnitUsage.Residential, DefaultBedrooms = 3, IsActive = true },
                new UnitType { Code = "UT-OFF", Name = "Commercial Fitted Office", DefaultUsage = UnitUsage.Commercial, IsActive = true },
                new UnitType { Code = "UT-RET", Name = "Ground Retail & Showroom", DefaultUsage = UnitUsage.Commercial, IsActive = true }
            );
            await db.SaveChangesAsync(ct);
        }

        var ut1Br = await utSet.FirstOrDefaultAsync(u => u.Code == "UT-1BR", ct);
        var ut2Br = await utSet.FirstOrDefaultAsync(u => u.Code == "UT-2BR", ct);
        var utOff = await utSet.FirstOrDefaultAsync(u => u.Code == "UT-OFF", ct);
        var utRet = await utSet.FirstOrDefaultAsync(u => u.Code == "UT-RET", ct);

        // 2. Properties
        var propSet = db.Set<Property>();
        if (!await propSet.AnyAsync(ct))
        {
            var p1 = new Property
            {
                LegalEntityId = legalEntityId,
                Code = "PROP-MARINA",
                Name = "Pearl Bay Heights",
                PropertyType = PropertyType.Residential,
                OwnershipType = OwnershipType.Owned,
                Region = "The Pearl-Qatar",
                Community = "The Pearl-Qatar Waterfront",
                PlotNumber = "DM-142-990",
                YearBuilt = 2021,
                TotalUnits = 48,
                IndicativeValue = 85000000m,
                IsActive = true,
                Notes = "Luxury waterfront twin residential towers with private marina berths"
            };
            var p2 = new Property
            {
                LegalEntityId = legalEntityId,
                Code = "PROP-DOWNTOWN",
                Name = "West Bay Executive Tower",
                PropertyType = PropertyType.Commercial,
                OwnershipType = OwnershipType.Owned,
                Region = "West Bay",
                Community = "West Bay Central District",
                PlotNumber = "DT-882-104",
                YearBuilt = 2023,
                TotalUnits = 24,
                IndicativeValue = 120000000m,
                IsActive = true,
                Notes = "Grade A prime commercial building overlooking the Doha Corniche"
            };
            var p3 = new Property
            {
                LegalEntityId = legalEntityId,
                Code = "PROP-PALM",
                Name = "Pearl Azure Residences",
                PropertyType = PropertyType.Villa,
                OwnershipType = OwnershipType.ManagedForOwner,
                ManagementFeePercent = 5.0m,
                Region = "The Pearl-Qatar",
                Community = "Frond N",
                PlotNumber = "PJ-FN-012",
                YearBuilt = 2022,
                TotalUnits = 12,
                IndicativeValue = 65000000m,
                IsActive = true,
                Notes = "Boutique beachfront luxury villas managed under exclusive contract"
            };

            propSet.AddRange(p1, p2, p3);
            await db.SaveChangesAsync(ct);
        }

        var prop1 = await propSet.FirstOrDefaultAsync(p => p.Code == "PROP-MARINA", ct);
        var prop2 = await propSet.FirstOrDefaultAsync(p => p.Code == "PROP-DOWNTOWN", ct);

        // 3. Units
        var unitSet = db.Set<Unit>();
        if (!await unitSet.AnyAsync(ct) && prop1 != null && prop2 != null)
        {
            var u101 = Unit.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-120)));
            u101.LegalEntityId = legalEntityId;
            u101.PropertyId = prop1.Id;
            u101.UnitNumber = "101";
            u101.UnitTypeId = ut1Br?.Id ?? Guid.NewGuid();
            u101.Usage = UnitUsage.Residential;
            u101.Floor = "1";
            u101.Bedrooms = 1;
            u101.AreaSqft = 920m;
            u101.AskingRent = 85000m;
            u101.Occupy();

            var u201 = Unit.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90)));
            u201.LegalEntityId = legalEntityId;
            u201.PropertyId = prop1.Id;
            u201.UnitNumber = "201";
            u201.UnitTypeId = ut2Br?.Id ?? Guid.NewGuid();
            u201.Usage = UnitUsage.Residential;
            u201.Floor = "2";
            u201.Bedrooms = 2;
            u201.AreaSqft = 1380m;
            u201.AskingRent = 120000m;
            u201.Occupy();

            var u301 = Unit.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15)));
            u301.LegalEntityId = legalEntityId;
            u301.PropertyId = prop1.Id;
            u301.UnitNumber = "301";
            u301.UnitTypeId = ut2Br?.Id ?? Guid.NewGuid();
            u301.Usage = UnitUsage.Residential;
            u301.Floor = "3";
            u301.Bedrooms = 2;
            u301.AreaSqft = 1420m;
            u301.AskingRent = 125000m;

            var uOff401 = Unit.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-200)));
            uOff401.LegalEntityId = legalEntityId;
            uOff401.PropertyId = prop2.Id;
            uOff401.UnitNumber = "OFF-401";
            uOff401.UnitTypeId = utOff?.Id ?? Guid.NewGuid();
            uOff401.Usage = UnitUsage.Commercial;
            uOff401.Floor = "4";
            uOff401.AreaSqft = 2250m;
            uOff401.AskingRent = 160000m;
            uOff401.Occupy();

            var uRetG01 = Unit.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
            uRetG01.LegalEntityId = legalEntityId;
            uRetG01.PropertyId = prop2.Id;
            uRetG01.UnitNumber = "RET-G01";
            uRetG01.UnitTypeId = utRet?.Id ?? Guid.NewGuid();
            uRetG01.Usage = UnitUsage.Commercial;
            uRetG01.Floor = "G";
            uRetG01.AreaSqft = 1850m;
            uRetG01.AskingRent = 280000m;

            unitSet.AddRange(u101, u201, u301, uOff401, uRetG01);
            await db.SaveChangesAsync(ct);

            // Unit Rent Rates
            var rateSet = db.Set<UnitRentRate>();
            rateSet.AddRange(
                new UnitRentRate { LegalEntityId = legalEntityId, UnitId = u101.Id, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)), AnnualRent = 80000m, Reason = "Initial market launch" },
                new UnitRentRate { LegalEntityId = legalEntityId, UnitId = u101.Id, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-4)), AnnualRent = 85000m, Reason = "Annual market index revision" },
                new UnitRentRate { LegalEntityId = legalEntityId, UnitId = uOff401.Id, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-8)), AnnualRent = 160000m, Reason = "Commercial lease agreement" }
            );
            await db.SaveChangesAsync(ct);
        }

        var unit101 = await unitSet.FirstOrDefaultAsync(u => u.UnitNumber == "101", ct);
        var unit201 = await unitSet.FirstOrDefaultAsync(u => u.UnitNumber == "201", ct);
        var unitOff401 = await unitSet.FirstOrDefaultAsync(u => u.UnitNumber == "OFF-401", ct);

        // 4. Leases
        var leaseSet = db.Set<Lease>();
        if (!await leaseSet.AnyAsync(ct) && unit101 != null && unitOff401 != null && prop1 != null && prop2 != null)
        {
            var l1 = new Lease
            {
                LegalEntityId = legalEntityId,
                Number = "LSE-2026-0001",
                UnitId = unit101.Id,
                PropertyId = prop1.Id,
                LesseePartyId = tenant1?.Id ?? Guid.NewGuid(),
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(9)),
                AnnualRent = 85000m,
                CurrencyCode = "QAR",
                PaymentFrequency = 4,
                SecurityDepositAmount = 5000m,
                Notes = "Standard residential tenancy contract with 4 cheques"
            };
            l1.Activate(DateTime.UtcNow.AddMonths(-3), null);

            var l2 = new Lease
            {
                LegalEntityId = legalEntityId,
                Number = "LSE-2026-0002",
                UnitId = unitOff401.Id,
                PropertyId = prop2.Id,
                LesseePartyId = tenant3?.Id ?? Guid.NewGuid(),
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
                AnnualRent = 160000m,
                CurrencyCode = "QAR",
                PaymentFrequency = 2,
                SecurityDepositAmount = 10000m,
                Notes = "Commercial office lease agreement with semi-annual instalments"
            };
            l2.Activate(DateTime.UtcNow.AddMonths(-6), null);

            leaseSet.AddRange(l1, l2);
            await db.SaveChangesAsync(ct);

            // Rent Instalments
            var instSet = db.Set<RentInstalment>();
            instSet.AddRange(
                new RentInstalment { LeaseId = l1.Id, Sequence = 1, PeriodFrom = l1.StartDate, PeriodTo = l1.StartDate.AddMonths(3).AddDays(-1), DueDate = l1.StartDate, Amount = 21250m, Status = InstalmentStatus.Invoiced },
                new RentInstalment { LeaseId = l1.Id, Sequence = 2, PeriodFrom = l1.StartDate.AddMonths(3), PeriodTo = l1.StartDate.AddMonths(6).AddDays(-1), DueDate = l1.StartDate.AddMonths(3), Amount = 21250m, Status = InstalmentStatus.Scheduled },
                new RentInstalment { LeaseId = l1.Id, Sequence = 3, PeriodFrom = l1.StartDate.AddMonths(6), PeriodTo = l1.StartDate.AddMonths(9).AddDays(-1), DueDate = l1.StartDate.AddMonths(6), Amount = 21250m, Status = InstalmentStatus.Scheduled },
                new RentInstalment { LeaseId = l1.Id, Sequence = 4, PeriodFrom = l1.StartDate.AddMonths(9), PeriodTo = l1.EndDate, DueDate = l1.StartDate.AddMonths(9), Amount = 21250m, Status = InstalmentStatus.Scheduled },
                new RentInstalment { LeaseId = l2.Id, Sequence = 1, PeriodFrom = l2.StartDate, PeriodTo = l2.StartDate.AddMonths(6).AddDays(-1), DueDate = l2.StartDate, Amount = 80000m, Status = InstalmentStatus.Invoiced },
                new RentInstalment { LeaseId = l2.Id, Sequence = 2, PeriodFrom = l2.StartDate.AddMonths(6), PeriodTo = l2.EndDate, DueDate = l2.StartDate.AddMonths(6), Amount = 80000m, Status = InstalmentStatus.Scheduled }
            );

            // Deposit Transactions
            var depSet = db.Set<DepositTransaction>();
            depSet.AddRange(
                new DepositTransaction { LegalEntityId = legalEntityId, LeaseId = l1.Id, Date = l1.StartDate, Kind = DepositKind.Received, Amount = 5000m, Description = "Security deposit received via bank transfer" },
                new DepositTransaction { LegalEntityId = legalEntityId, LeaseId = l2.Id, Date = l2.StartDate, Kind = DepositKind.Received, Amount = 10000m, Description = "Security deposit received via cheque" }
            );
            await db.SaveChangesAsync(ct);
        }

        var lease1 = await leaseSet.FirstOrDefaultAsync(l => l.Number == "LSE-2026-0001", ct);

        // 5. Work Orders (Facilities)
        var woSet = db.Set<ReWorkOrder>();
        if (!await woSet.AnyAsync(ct) && prop1 != null)
        {
            var w1 = new ReWorkOrder
            {
                LegalEntityId = legalEntityId,
                Number = "WO-2026-0001",
                PropertyId = prop1.Id,
                UnitId = unit101?.Id,
                LeaseId = lease1?.Id,
                Issue = "Central HVAC Chiller Unit Compressor Servicing",
                Category = "HVAC",
                Priority = RePriority.High,
                ReportedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)),
                ReportedBy = "Tenant - Michael Schumacher",
                VendorPartyId = vendorFm?.Id,
                Technician = "Ravi Kumar (Al Futtaim FM)",
                EstimatedCost = 3500m,
                ActualCost = 3450m,
                Details = "Chiller compressor low cooling gas alarm and vibration check",
                ResolutionNotes = "Topped up refrigerant R410A and replaced anti-vibration rubber mounts"
            };
            w1.MoveTo(ReWorkOrderStatus.Assigned, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)));
            w1.MoveTo(ReWorkOrderStatus.InProgress, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)));
            w1.MoveTo(ReWorkOrderStatus.Completed, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "Resolved and tested");

            var w2 = new ReWorkOrder
            {
                LegalEntityId = legalEntityId,
                Number = "WO-2026-0002",
                PropertyId = prop1.Id,
                UnitId = unit201?.Id,
                Issue = "Master Bathroom Water Pressure Regulator Replacement",
                Category = "Plumbing",
                Priority = RePriority.Normal,
                ReportedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                ReportedBy = "Tenant - Amina Al-Mansouri",
                VendorPartyId = vendorFm?.Id,
                Technician = "Kareem Nassar",
                EstimatedCost = 850m,
                Details = "Fluctuating water pressure in ensuite mixer shower valve"
            };
            w2.MoveTo(ReWorkOrderStatus.Assigned, DateOnly.FromDateTime(DateTime.UtcNow));

            woSet.AddRange(w1, w2);
            await db.SaveChangesAsync(ct);
        }

        // 6. Preventive Maintenance (PM Schedules)
        var pmSet = db.Set<PmSchedule>();
        if (!await pmSet.AnyAsync(ct) && prop1 != null)
        {
            pmSet.AddRange(
                new PmSchedule
                {
                    LegalEntityId = legalEntityId,
                    PropertyId = prop1.Id,
                    Title = "Quarterly HVAC Chiller & Fan Coil Units Deep Servicing",
                    Category = "HVAC",
                    Priority = RePriority.High,
                    FrequencyMonths = 3,
                    NextDue = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)),
                    LeadDays = 7,
                    EstimatedCost = 8500m,
                    IsActive = true,
                    Notes = "Comprehensive seasonal coil washing, belt adjustments and refrigerant tests"
                },
                new PmSchedule
                {
                    LegalEntityId = legalEntityId,
                    PropertyId = prop1.Id,
                    Title = "Annual Fire Safety, Smoke Dampers & Emergency Light Certification",
                    Category = "Fire Safety",
                    Priority = RePriority.Critical,
                    FrequencyMonths = 12,
                    NextDue = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
                    LeadDays = 14,
                    EstimatedCost = 15000m,
                    IsActive = true,
                    Notes = "Civil Defence statutory compliance inspection and alarm panel testing"
                }
            );
            await db.SaveChangesAsync(ct);
        }

        // 7. Inspections
        var inspSet = db.Set<Inspection>();
        if (!await inspSet.AnyAsync(ct) && prop1 != null && unit101 != null)
        {
            var insp = new Inspection
            {
                LegalEntityId = legalEntityId,
                PropertyId = prop1.Id,
                UnitId = unit101.Id,
                LeaseId = lease1?.Id,
                Kind = InspectionKind.MoveIn,
                InspectionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
                Inspector = "Tariq Al-Hashimi",
                Status = InspectionStatus.Completed,
                Notes = "Unit handed over in immaculate condition with keys and access cards",
                DamageCost = 0m,
                Items = new List<InspectionItem>
                {
                    new() { Area = "Living & Dining Room", Condition = ItemCondition.Good, Notes = "Walls and marble flooring spotless" },
                    new() { Area = "Kitchen Appliances", Condition = ItemCondition.Good, Notes = "Cooker, oven and exhaust tested and sanitized" },
                    new() { Area = "Master Bedroom", Condition = ItemCondition.Good, Notes = "Built-in wardrobes operational" },
                    new() { Area = "Balcony & Glass Railings", Condition = ItemCondition.Good, Notes = "Clean and secure" }
                }
            };
            inspSet.Add(insp);
            await db.SaveChangesAsync(ct);
        }

        // 8. Listings & Leads
        var listSet = db.Set<Listing>();
        if (!await listSet.AnyAsync(ct) && prop1 != null)
        {
            var u301 = await unitSet.FirstOrDefaultAsync(u => u.UnitNumber == "301", ct);
            var listing = new Listing
            {
                LegalEntityId = legalEntityId,
                PropertyId = prop1.Id,
                UnitId = u301?.Id,
                Kind = ListingKind.Rent,
                Title = "Stunning 2BR Pearl Sea View with Balcony",
                Price = 125000m,
                Status = ListingStatus.Published,
                Channels = "Bayut, PropertyFinder, Dubizzle, Website",
                Description = "High-floor fully fitted luxury apartment with panoramic sea and yacht views",
                ListedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15))
            };
            listSet.Add(listing);
            await db.SaveChangesAsync(ct);

            var leadSet = db.Set<ReLead>();
            leadSet.AddRange(
                new ReLead
                {
                    LegalEntityId = legalEntityId,
                    Name = "Sultan Al-Qasimi",
                    Phone = "+974 5088 2119",
                    Email = "sultan.q@example.com",
                    Source = "PropertyFinder",
                    Interest = "Looking for 2BR luxury apartment with sea view",
                    ListingId = listing.Id,
                    UnitId = u301?.Id,
                    Stage = LeadStage.Viewing,
                    FollowUpOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                    Notes = "Viewing scheduled this Saturday morning at 11:00 AM"
                },
                new ReLead
                {
                    LegalEntityId = legalEntityId,
                    Name = "Nathalie Dupont",
                    Phone = "+974 5544 1223",
                    Email = "n.dupont@example.com",
                    Source = "Website Direct",
                    Interest = "Furnished 1BR or 2BR with quick move-in",
                    Stage = LeadStage.Contacted,
                    FollowUpOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                    Notes = "Sent brochure and video walk-through"
                }
            );
            await db.SaveChangesAsync(ct);
        }

        // 9. Acquisitions & Funding
        var acqSet = db.Set<Acquisition>();
        if (!await acqSet.AnyAsync(ct) && prop1 != null)
        {
            var acq = new Acquisition
            {
                LegalEntityId = legalEntityId,
                Number = "ACQ-2026-0001",
                PropertyId = prop1.Id,
                SellerName = "Qatari Diar Real Estate Q.P.S.C.",
                PurchaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
                ContractReference = "EMAAR-SPA-2024-819",
                PurchasePrice = 72000000m,
                DownPayment = 18000000m,
                TransferFees = 2880000m,
                OtherCosts = 120000m,
                CurrencyCode = "QAR",
                Notes = "Acquisition of Pearl Bay Heights Tower directly from master developer"
            };
            acq.Complete(Guid.NewGuid(), DateTime.UtcNow.AddYears(-2));
            acqSet.Add(acq);
            await db.SaveChangesAsync(ct);

            var fundSet = db.Set<FundingSource>();
            var fund = new FundingSource
            {
                LegalEntityId = legalEntityId,
                AcquisitionId = acq.Id,
                Type = FundingType.BankMortgage,
                Lender = "First Al Khor Bank (FAB Commercial Banking)",
                Principal = 54000000m,
                AnnualRatePercent = 4.5m,
                TermMonths = 240,
                FirstDueDate = acq.PurchaseDate.AddMonths(1),
                MonthlyPayment = 341500m
            };
            fundSet.Add(fund);
            await db.SaveChangesAsync(ct);

            var fundInstSet = db.Set<FundingInstalment>();
            fundInstSet.AddRange(
                new FundingInstalment
                {
                    LegalEntityId = legalEntityId,
                    FundingSourceId = fund.Id,
                    Sequence = 1,
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                    Principal = 139000m,
                    Interest = 202500m,
                    Total = 341500m,
                    BalanceAfter = 53861000m,
                    Status = InstalmentPayStatus.Paid,
                    PaidOn = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1))
                },
                new FundingInstalment
                {
                    LegalEntityId = legalEntityId,
                    FundingSourceId = fund.Id,
                    Sequence = 2,
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    Principal = 139520m,
                    Interest = 201980m,
                    Total = 341500m,
                    BalanceAfter = 53721480m,
                    Status = InstalmentPayStatus.Scheduled
                }
            );
            await db.SaveChangesAsync(ct);
        }

        // 10. Brokerage Deals
        var dealSet = db.Set<BrokerageDeal>();
        if (!await dealSet.AnyAsync(ct) && prop1 != null)
        {
            var deal1 = new BrokerageDeal
            {
                LegalEntityId = legalEntityId,
                Number = "DEAL-2026-0001",
                DealType = DealType.Sale,
                PropertyId = prop1.Id,
                UnitReference = "Penthouse Suite 4801",
                ClientName = "Qatari Diar Investment Q.P.S.C.",
                ClientPartyId = tenant3?.Id,
                AgentPartyId = agentUser?.Id,
                DealDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-18)),
                DealValue = 12500000m,
                CurrencyCode = "QAR",
                CommissionRatePercent = 2.0m,
                AgentSharePercent = 50.0m,
                TeamLeadSharePercent = 10.0m,
                Notes = "Direct investor transaction with agreed 2% commercial commission"
            };
            deal1.ComputeCommission();
            deal1.MoveTo(DealStage.Agreed);

            var deal2 = new BrokerageDeal
            {
                LegalEntityId = legalEntityId,
                Number = "DEAL-2026-0002",
                DealType = DealType.Lease,
                PropertyId = prop2?.Id,
                UnitReference = "Executive Suite OFF-401",
                ClientName = "Apex Global Trading W.L.L.",
                ClientPartyId = tenant3?.Id,
                AgentPartyId = agentUser?.Id,
                DealDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
                DealValue = 160000m,
                CurrencyCode = "QAR",
                CommissionRatePercent = 5.0m,
                AgentSharePercent = 60.0m,
                TeamLeadSharePercent = 10.0m,
                Notes = "Full year commercial lease placement"
            };
            deal2.ComputeCommission();
            deal2.MoveTo(DealStage.Agreed);

            dealSet.AddRange(deal1, deal2);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedHrAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var empSet = db.Set<Employee>();
        if (!await empSet.AnyAsync(ct))
        {
            empSet.AddRange(
                new Employee
                {
                    LegalEntityId = legalEntityId,
                    Number = "EMP-001",
                    FullName = "Tariq Al-Hashimi",
                    Designation = "Managing Director",
                    Department = "Executive Management",
                    JoinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
                    Email = "tariq@demo.local",
                    Phone = "+974 5233 4455",
                    Nationality = "Qatar",
                    Status = EmployeeStatus.Active,
                    BasicSalary = 45000m,
                    HousingAllowance = 20000m,
                    TransportAllowance = 5000m,
                    Iban = "QA90QNBA000000000000012345671",
                    LabourCardNumber = "784198501928371",
                    BankRoutingCode = "033"
                },
                new Employee
                {
                    LegalEntityId = legalEntityId,
                    Number = "EMP-002",
                    FullName = "Sarah Jenkins",
                    Designation = "Senior Financial Controller",
                    Department = "Finance & Accounting",
                    JoinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
                    Email = "sarah.j@demo.local",
                    Phone = "+974 5077 6554",
                    Nationality = "United Kingdom",
                    Status = EmployeeStatus.Active,
                    BasicSalary = 32000m,
                    HousingAllowance = 12000m,
                    TransportAllowance = 3000m,
                    Iban = "QA17DOHB000000000000098765432",
                    LabourCardNumber = "784199018273645",
                    BankRoutingCode = "026"
                },
                new Employee
                {
                    LegalEntityId = legalEntityId,
                    Number = "EMP-003",
                    FullName = "Omar Farooq",
                    Designation = "Senior Property & Facilities Manager",
                    Department = "Real Estate Operations",
                    JoinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-18)),
                    Email = "omar.f@demo.local",
                    Phone = "+974 5599 8112",
                    Nationality = "Jordan",
                    Status = EmployeeStatus.Active,
                    BasicSalary = 24000m,
                    HousingAllowance = 10000m,
                    TransportAllowance = 2500m,
                    Iban = "QA20MAFR000000000000034567890",
                    LabourCardNumber = "784199298374615",
                    BankRoutingCode = "044"
                },
                new Employee
                {
                    LegalEntityId = legalEntityId,
                    Number = "EMP-004",
                    FullName = "Layla Mansoor",
                    Designation = "Human Resources & Talent Officer",
                    Department = "Human Resources",
                    JoinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-14)),
                    Email = "layla.m@demo.local",
                    Phone = "+974 5033 2887",
                    Nationality = "Lebanon",
                    Status = EmployeeStatus.Active,
                    BasicSalary = 20000m,
                    HousingAllowance = 8000m,
                    TransportAllowance = 2000m,
                    Iban = "QA42CBQA000000000000056789012",
                    LabourCardNumber = "784199482716354",
                    BankRoutingCode = "033"
                }
            );
            await db.SaveChangesAsync(ct);
        }

        var emp1 = await empSet.FirstOrDefaultAsync(e => e.Number == "EMP-001", ct);
        var emp2 = await empSet.FirstOrDefaultAsync(e => e.Number == "EMP-002", ct);
        var emp3 = await empSet.FirstOrDefaultAsync(e => e.Number == "EMP-003", ct);
        var emp4 = await empSet.FirstOrDefaultAsync(e => e.Number == "EMP-004", ct);

        // 2. Attendance Records
        var attSet = db.Set<AttendanceRecord>();
        if (!await attSet.AnyAsync(ct) && emp1 != null && emp2 != null)
        {
            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;

            attSet.AddRange(
                new AttendanceRecord { LegalEntityId = legalEntityId, EmployeeId = emp1.Id, Year = currentYear, Month = currentMonth, WorkingDays = 26, DaysPresent = 26, OvertimeHours = 0m },
                new AttendanceRecord { LegalEntityId = legalEntityId, EmployeeId = emp2.Id, Year = currentYear, Month = currentMonth, WorkingDays = 26, DaysPresent = 25, PaidLeaveDays = 1, OvertimeHours = 4m },
                new AttendanceRecord { LegalEntityId = legalEntityId, EmployeeId = emp3!.Id, Year = currentYear, Month = currentMonth, WorkingDays = 26, DaysPresent = 26, OvertimeHours = 8.5m },
                new AttendanceRecord { LegalEntityId = legalEntityId, EmployeeId = emp4!.Id, Year = currentYear, Month = currentMonth, WorkingDays = 26, DaysPresent = 26, OvertimeHours = 0m }
            );
            await db.SaveChangesAsync(ct);
        }

        // 3. Payroll Runs
        var prSet = db.Set<PayrollRun>();
        if (!await prSet.AnyAsync(ct) && emp1 != null && emp2 != null)
        {
            var prevMonth = DateTime.UtcNow.AddMonths(-1);
            var prPrev = new PayrollRun
            {
                LegalEntityId = legalEntityId,
                Number = $"PR-{prevMonth.Year}-{prevMonth.Month:D2}",
                Year = prevMonth.Year,
                Month = prevMonth.Month,
                PlannedPayDate = DateOnly.FromDateTime(new DateTime(prevMonth.Year, prevMonth.Month, 28)),
                PaidOn = DateOnly.FromDateTime(new DateTime(prevMonth.Year, prevMonth.Month, 28)),
                Status = PayrollStatus.Paid,
                Employees = 4,
                GrossEarnings = 181500m,
                Deductions = 0m,
                NetPay = 181500m,
                Notes = "Monthly automated payroll execution with bank WPS (Qatar Wage Protection System) file uploaded and cleared",
                Lines = new List<PayrollLine>
                {
                    new() { EmployeeId = emp1.Id, EmployeeName = emp1.FullName, EmployeeNumber = emp1.Number, Basic = emp1.BasicSalary, Allowances = emp1.Allowances, GrossEarnings = emp1.GrossMonthly, NetPay = emp1.GrossMonthly },
                    new() { EmployeeId = emp2.Id, EmployeeName = emp2.FullName, EmployeeNumber = emp2.Number, Basic = emp2.BasicSalary, Allowances = emp2.Allowances, GrossEarnings = emp2.GrossMonthly, NetPay = emp2.GrossMonthly },
                    new() { EmployeeId = emp3!.Id, EmployeeName = emp3.FullName, EmployeeNumber = emp3.Number, Basic = emp3.BasicSalary, Allowances = emp3.Allowances, GrossEarnings = emp3.GrossMonthly, NetPay = emp3.GrossMonthly },
                    new() { EmployeeId = emp4!.Id, EmployeeName = emp4.FullName, EmployeeNumber = emp4.Number, Basic = emp4.BasicSalary, Allowances = emp4.Allowances, GrossEarnings = emp4.GrossMonthly, NetPay = emp4.GrossMonthly }
                }
            };

            var currMonth = DateTime.UtcNow;
            var prCurr = new PayrollRun
            {
                LegalEntityId = legalEntityId,
                Number = $"PR-{currMonth.Year}-{currMonth.Month:D2}",
                Year = currMonth.Year,
                Month = currMonth.Month,
                PlannedPayDate = DateOnly.FromDateTime(new DateTime(currMonth.Year, currMonth.Month, 28)),
                Status = PayrollStatus.Draft,
                Employees = 4,
                GrossEarnings = 181500m,
                Deductions = 0m,
                NetPay = 181500m,
                Notes = "Current pay period salary draft generation",
                Lines = new List<PayrollLine>
                {
                    new() { EmployeeId = emp1.Id, EmployeeName = emp1.FullName, EmployeeNumber = emp1.Number, Basic = emp1.BasicSalary, Allowances = emp1.Allowances, GrossEarnings = emp1.GrossMonthly, NetPay = emp1.GrossMonthly },
                    new() { EmployeeId = emp2.Id, EmployeeName = emp2.FullName, EmployeeNumber = emp2.Number, Basic = emp2.BasicSalary, Allowances = emp2.Allowances, GrossEarnings = emp2.GrossMonthly, NetPay = emp2.GrossMonthly },
                    new() { EmployeeId = emp3.Id, EmployeeName = emp3.FullName, EmployeeNumber = emp3.Number, Basic = emp3.BasicSalary, Allowances = emp3.Allowances, GrossEarnings = emp3.GrossMonthly, NetPay = emp3.GrossMonthly },
                    new() { EmployeeId = emp4.Id, EmployeeName = emp4.FullName, EmployeeNumber = emp4.Number, Basic = emp4.BasicSalary, Allowances = emp4.Allowances, GrossEarnings = emp4.GrossMonthly, NetPay = emp4.GrossMonthly }
                }
            };

            prSet.AddRange(prPrev, prCurr);
            await db.SaveChangesAsync(ct);
        }

        // 4. Review Cycles & Performance Reviews
        var cycleSet = db.Set<ReviewCycle>();
        if (!await cycleSet.AnyAsync(ct))
        {
            var cycle = new ReviewCycle
            {
                Name = "Annual Corporate Performance Review 2026",
                PeriodStart = DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, 1, 1)),
                PeriodEnd = DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, 12, 31)),
                Status = ReviewCycleStatus.Open
            };
            cycleSet.Add(cycle);
            await db.SaveChangesAsync(ct);

            if (emp2 != null && emp3 != null)
            {
                var reviewSet = db.Set<PerformanceReview>();
                var r1 = new PerformanceReview
                {
                    LegalEntityId = legalEntityId,
                    EmployeeId = emp2.Id,
                    CycleId = cycle.Id,
                    ReviewerName = emp1?.FullName ?? "Tariq Al-Hashimi",
                    OverallRating = 5,
                    Goals = "Lead clean financial audits and automated monthly closing",
                    Comments = "Exceptional performance in financial controls, tax compliance, and budget tracking.",
                    Status = ReviewStatus.Submitted,
                    SubmittedAt = DateTime.UtcNow.AddDays(-5),
                    Competencies = new List<ReviewCompetency>
                    {
                        new() { Name = "Financial Reporting & Accuracy", Score = 5, Notes = "Flawless audit outcomes" },
                        new() { Name = "Team Leadership & Compliance", Score = 5, Notes = "Strong team oversight" }
                    }
                };

                var r2 = new PerformanceReview
                {
                    LegalEntityId = legalEntityId,
                    EmployeeId = emp3.Id,
                    CycleId = cycle.Id,
                    ReviewerName = emp1?.FullName ?? "Tariq Al-Hashimi",
                    OverallRating = 4,
                    Goals = "Improve facilities maintenance response times below 4 hours",
                    Comments = "Very solid operational execution across properties and high tenant satisfaction.",
                    Status = ReviewStatus.Draft,
                    Competencies = new List<ReviewCompetency>
                    {
                        new() { Name = "Facilities SLA & Preventative Maintenance", Score = 4, Notes = "Consistently timely" },
                        new() { Name = "Tenant & Vendor Relationship Management", Score = 5, Notes = "Excellent feedback" }
                    }
                };

                reviewSet.AddRange(r1, r2);
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static async Task SeedInventoryAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var whSet = db.Set<Warehouse>();
        if (!await whSet.AnyAsync(ct))
        {
            whSet.AddRange(
                new Warehouse
                {
                    LegalEntityId = legalEntityId,
                    Code = "WH-JAFZA",
                    Name = "Central Freezone Distribution Center",
                    Address = "Jebel Ali Free Zone, South Distribution Park, Unit 14B, Doha",
                    IsActive = true
                },
                new Warehouse
                {
                    LegalEntityId = legalEntityId,
                    Code = "WH-ALQUOZ",
                    Name = "Al Quoz Operations & Spare Parts Depot",
                    Address = "Al Quoz Industrial 3, Street 19, Warehouse 4, Doha",
                    IsActive = true
                },
                new Warehouse
                {
                    LegalEntityId = legalEntityId,
                    Code = "WH-DOWNTOWN",
                    Name = "West Bay Staging & Retail Stockroom",
                    Address = "The Villaggio Mall, Retail Staging Bay G-142, Doha",
                    IsActive = true
                }
            );
            await db.SaveChangesAsync(ct);
        }

        var wh1 = await whSet.FirstOrDefaultAsync(w => w.Code == "WH-JAFZA", ct);
        var wh2 = await whSet.FirstOrDefaultAsync(w => w.Code == "WH-ALQUOZ", ct);
        var itemPos = await db.Items.FirstOrDefaultAsync(i => i.Sku == "POS-TERM-01", ct);
        var itemScan = await db.Items.FirstOrDefaultAsync(i => i.Sku == "POS-SCAN-2D", ct);
        var itemIot = await db.Items.FirstOrDefaultAsync(i => i.Sku == "HW-IOT-GW02", ct);

        // Stock Movements
        var mvtSet = db.Set<StockMovement>();
        if (!await mvtSet.AnyAsync(ct) && wh1 != null && wh2 != null && itemPos != null && itemScan != null)
        {
            mvtSet.AddRange(
                new StockMovement
                {
                    LegalEntityId = legalEntityId,
                    WarehouseId = wh1.Id,
                    ItemId = itemPos.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-25)),
                    Kind = MovementKind.Receipt,
                    Quantity = 50,
                    UnitCost = 2800m,
                    Reference = "GR-2026-0001",
                    Memo = "Initial stock arrival from vendor air shipment"
                },
                new StockMovement
                {
                    LegalEntityId = legalEntityId,
                    WarehouseId = wh1.Id,
                    ItemId = itemScan.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-25)),
                    Kind = MovementKind.Receipt,
                    Quantity = 80,
                    UnitCost = 480m,
                    Reference = "GR-2026-0001",
                    Memo = "Barcode scanners incoming receipt"
                },
                new StockMovement
                {
                    LegalEntityId = legalEntityId,
                    WarehouseId = wh1.Id,
                    ItemId = itemPos.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                    Kind = MovementKind.Issue,
                    Quantity = -10,
                    UnitCost = 2800m,
                    Reference = "SO-2026-0001",
                    Memo = "Delivered to Oasis Retailers for Villaggio Mall deployment"
                },
                new StockMovement
                {
                    LegalEntityId = legalEntityId,
                    WarehouseId = wh2.Id,
                    ItemId = itemScan.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                    Kind = MovementKind.TransferIn,
                    Quantity = 20,
                    UnitCost = 480m,
                    Reference = "TRF-WH-001",
                    Memo = "Restock transferred to Al Quoz depot"
                }
            );

            // Stock Policies
            var polSet = db.Set<StockPolicy>();
            polSet.AddRange(
                new StockPolicy { ItemId = itemPos.Id, ReorderLevel = 10, ReorderQuantity = 30 },
                new StockPolicy { ItemId = itemScan.Id, ReorderLevel = 15, ReorderQuantity = 50 }
            );
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedSalesAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var quotesSet = db.Set<SalesQuote>();
        if (!await quotesSet.AnyAsync(ct))
        {
            var quote = new SalesQuote
            {
                LegalEntityId = legalEntityId,
                Number = "QT-2026-0001",
                CustomerName = "Apex Global Trading W.L.L.",
                CustomerEmail = "procurement@apexglobal.example.com",
                QuoteDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
                Currency = "QAR",
                Status = SalesQuoteStatus.Sent,
                SubTotal = 145000m,
                TaxTotal = 0m,
                TotalAmount = 145000m,
                Notes = "Enterprise ERP Core Deployment Pack with Cloud Hosting",
                Lines = new List<SalesQuoteLine>
                {
                    new()
                    {
                        ItemCode = "ERP-CORE",
                        Description = "Enterprise ERP Core License & Multi-tenant Database",
                        Quantity = 1,
                        UnitPrice = 95000m,
                        LineTotal = 95000m,
                        TaxRate = 0m
                    },
                    new()
                    {
                        ItemCode = "SRV-IMPL",
                        Description = "Professional Implementation, Customization & Go-Live Support",
                        Quantity = 1,
                        UnitPrice = 50000m,
                        LineTotal = 50000m,
                        TaxRate = 0m
                    }
                }
            };
            quotesSet.Add(quote);

            var ordersSet = db.Set<SalesOrder>();
            var order = new SalesOrder
            {
                LegalEntityId = legalEntityId,
                Number = "SO-2026-0001",
                QuoteId = quote.Id,
                QuoteNumber = quote.Number,
                CustomerName = "Oasis Retailers Corp",
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
                ExpectedDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                Currency = "QAR",
                Status = SalesOrderStatus.Confirmed,
                SubTotal = 68500m,
                TaxTotal = 0m,
                TotalAmount = 68500m,
                PaymentTerms = "Net 30",
                ShippingAddress = "Al Quoz Industrial Area 3, Doha, Qatar",
                Notes = "Deliver to central staging warehouse with calibration reports",
                Lines = new List<SalesOrderLine>
                {
                    new()
                    {
                        ItemCode = "POS-TERM-01",
                        Description = "Dual-Screen Touch POS Terminal i5/8GB/SSD",
                        Quantity = 10,
                        UnitPrice = 4500m,
                        LineTotal = 45000m,
                        TaxRate = 0m
                    },
                    new()
                    {
                        ItemCode = "POS-SCAN-2D",
                        Description = "Wireless Omni-directional 2D Barcode Scanner",
                        Quantity = 10,
                        UnitPrice = 850m,
                        LineTotal = 8500m,
                        TaxRate = 0m
                    },
                    new()
                    {
                        ItemCode = "POS-PRN-80",
                        Description = "Thermal High-Speed Receipt Printer 80mm USB/LAN",
                        Quantity = 10,
                        UnitPrice = 1500m,
                        LineTotal = 15000m,
                        TaxRate = 0m
                    }
                }
            };
            ordersSet.Add(order);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedPurchasingAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var reqsSet = db.Set<PurchaseRequisition>();
        if (!await reqsSet.AnyAsync(ct))
        {
            var req = new PurchaseRequisition
            {
                LegalEntityId = legalEntityId,
                Number = "PR-2026-0001",
                RequesterName = "Khalid Al-Marri",
                Department = "Warehouse Operations",
                RequisitionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-12)),
                RequiredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
                Status = PurchaseRequisitionStatus.Approved,
                EstimatedTotal = 42500m,
                Reason = "Warehouse scanner inventory expansion for peak season",
                Lines = new List<PurchaseRequisitionLine>
                {
                    new()
                    {
                        ItemCode = "HW-SCAN-WH",
                        Description = "Rugged Industrial Handheld Barcode Mobile Computer",
                        Quantity = 15,
                        EstimatedUnitPrice = 2500m,
                        LineTotal = 37500m
                    },
                    new()
                    {
                        ItemCode = "HW-DOCK-4B",
                        Description = "Multi-slot 4-Bay Battery Charging Cradle",
                        Quantity = 5,
                        EstimatedUnitPrice = 1000m,
                        LineTotal = 5000m
                    }
                }
            };
            reqsSet.Add(req);

            var ordersSet = db.Set<PurchaseOrder>();
            var po = new PurchaseOrder
            {
                LegalEntityId = legalEntityId,
                Number = "PO-2026-0001",
                RequisitionId = req.Id,
                VendorName = "TechSupply Electronics International",
                VendorContact = "David Miller (+1 555 982 1200)",
                VendorReference = "TSE-INV-8841",
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6)),
                ExpectedDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)),
                Currency = "QAR",
                Status = PurchaseOrderStatus.Approved,
                SubTotal = 42500m,
                TaxTotal = 0m,
                TotalAmount = 42500m,
                PaymentTerms = "Net 60",
                DeliveryAddress = "Jebel Ali Free Zone, Warehouse Unit 14B, Doha",
                Notes = "Air freight shipment, customs clearance handled by vendor",
                Lines = new List<PurchaseOrderLine>
                {
                    new()
                    {
                        ItemCode = "HW-SCAN-WH",
                        Description = "Rugged Industrial Handheld Barcode Mobile Computer",
                        Quantity = 15,
                        UnitPrice = 2500m,
                        LineTotal = 37500m,
                        TaxRate = 0m
                    },
                    new()
                    {
                        ItemCode = "HW-DOCK-4B",
                        Description = "Multi-slot 4-Bay Battery Charging Cradle",
                        Quantity = 5,
                        UnitPrice = 1000m,
                        LineTotal = 5000m,
                        TaxRate = 0m
                    }
                }
            };
            ordersSet.Add(po);

            var receiptsSet = db.Set<GoodsReceipt>();
            receiptsSet.Add(new GoodsReceipt
            {
                LegalEntityId = legalEntityId,
                Number = "GR-2026-0001",
                PurchaseOrderId = po.Id,
                PurchaseOrderNumber = po.Number,
                VendorName = po.VendorName,
                ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                ReceivedBy = "Khalid Al-Marri",
                Notes = "All 20 packages verified in pristine condition"
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedPosAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var regSet = db.Set<PosRegister>();
        if (!await regSet.AnyAsync(ct))
        {
            var reg1 = new PosRegister
            {
                LegalEntityId = legalEntityId,
                Code = "REG-001",
                Name = "Flagship Counter 01",
                IsActive = true,
                ReceiptHeader = "=== DEMO TRADING W.L.L. - VILLAGGIO MALL ===\nInvoice / Tax Card: TC-482917",
                ReceiptFooter = "Thank you for shopping with us!\nExchange within 14 days with original receipt."
            };
            var reg2 = new PosRegister
            {
                LegalEntityId = legalEntityId,
                Code = "REG-002",
                Name = "Express Self-Checkout A",
                IsActive = true,
                ReceiptHeader = "=== DEMO TRADING W.L.L. - EXPRESS ===\nInvoice",
                ReceiptFooter = "Visit us online at demotrading.local"
            };
            regSet.AddRange(reg1, reg2);

            var sessionSet = db.Set<PosSession>();
            var session = new PosSession
            {
                LegalEntityId = legalEntityId,
                RegisterId = reg1.Id,
                RegisterName = reg1.Name,
                OpenedByUserId = Guid.NewGuid(),
                CashierName = "Rashid Al-Falasi",
                OpenedAt = DateTimeOffset.UtcNow.AddHours(-4),
                OpeningCash = 1000m,
                TotalSales = 4650m,
                TotalReturns = 0,
                TotalPayIn = 200m,
                TotalPayOut = 0,
                ExpectedCash = 5850m,
                Status = PosSessionStatus.Open,
                Notes = "Morning peak retail shift"
            };
            sessionSet.Add(session);

            var orderSet = db.Set<PosOrder>();
            orderSet.Add(new PosOrder
            {
                LegalEntityId = legalEntityId,
                Number = "POS-2026-0001",
                RegisterId = reg1.Id,
                SessionId = session.Id,
                CustomerName = "Walk-in Retail Customer",
                OrderDate = DateTimeOffset.UtcNow.AddHours(-2),
                SubTotal = 380m,
                TaxTotal = 19m,
                TotalAmount = 399m,
                Status = PosOrderStatus.Completed,
                PaymentStatus = PosPaymentStatus.Paid,
                Lines = new List<PosOrderLine>
                {
                    new()
                    {
                        ItemCode = "RET-ACC-01",
                        Description = "Wireless Bluetooth Stereo Headset",
                        Quantity = 1,
                        UnitPrice = 250m,
                        LineTotal = 250m,
                        TaxRate = 0m
                    },
                    new()
                    {
                        ItemCode = "RET-ACC-02",
                        Description = "USB-C 65W Fast GaN Wall Charger",
                        Quantity = 1,
                        UnitPrice = 130m,
                        LineTotal = 130m,
                        TaxRate = 0m
                    }
                },
                Payments = new List<PosPayment>
                {
                    new()
                    {
                        Method = PosPaymentMethod.Card,
                        Amount = 399m,
                        TenderedAmount = 399m,
                        PaymentDate = DateTimeOffset.UtcNow.AddHours(-2),
                        Reference = "VISA-AUTH-948172"
                    }
                }
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedRetailAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var storeSet = db.Set<RetailStore>();
        if (!await storeSet.AnyAsync(ct))
        {
            storeSet.Add(new RetailStore
            {
                LegalEntityId = legalEntityId,
                Code = "STR-001",
                Name = "Villaggio Flagship Boutique",
                StoreManagerName = "Noura Al-Zaabi",
                Phone = "+974 4398 1234",
                Email = "downtown@demotrading.local",
                Address = "The Villaggio Mall, Ground Floor Unit G-142",
                IsActive = true
            });
            storeSet.Add(new RetailStore
            {
                LegalEntityId = legalEntityId,
                Code = "STR-002",
                Name = "Porto Arabia Promenade Branch",
                StoreManagerName = "Karim Mansour",
                Phone = "+974 4456 7890",
                Email = "marina@demotrading.local",
                Address = "Porto Arabia Walk, Retail Unit R-08",
                IsActive = true
            });

            var priceListSet = db.Set<RetailPriceList>();
            priceListSet.Add(new RetailPriceList
            {
                LegalEntityId = legalEntityId,
                Code = "PL-RETAIL-2026",
                Name = "Standard Consumer Retail Price List 2026",
                Type = PriceListType.Standard,
                Currency = "QAR",
                ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
                IsActive = true,
                Description = "All Qatar brick-and-mortar retail locations standard tier"
            });

            var promoSet = db.Set<RetailPromotion>();
            promoSet.Add(new RetailPromotion
            {
                LegalEntityId = legalEntityId,
                Code = "PROMO-SUMMER15",
                Name = "Summer Electronics Flash Sale 15%",
                Type = PromotionType.PercentageDiscount,
                DiscountValue = 15m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
                IsActive = true,
                TimesUsed = 42
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedDistributionAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var routeSet = db.Set<DistributionRoute>();
        if (!await routeSet.AnyAsync(ct))
        {
            var route1 = new DistributionRoute
            {
                LegalEntityId = legalEntityId,
                Code = "RTE-NORTH-01",
                Name = "Msheireb & Old Doha Commercial Route",
                DriverName = "Farhan Tariq",
                VehicleNumber = "DXB-VAN-7819",
                VehicleCapacityKg = 1500m,
                IsActive = true,
                Description = "Daily high-density distribution and consignment delivery route",
                Stops = new List<DeliveryStop>
                {
                    new()
                    {
                        CustomerName = "Al Baraha Supermarket",
                        SequenceOrder = 1,
                        Address = "Al Baraha St, Msheireb, Doha",
                        ContactPhone = "+974 4271 1122",
                        PreferredTimeWindow = "08:00 - 10:00"
                    },
                    new()
                    {
                        CustomerName = "Al Fahidi Electronics Market",
                        SequenceOrder = 2,
                        Address = "Al Fahidi St, Bur Doha",
                        ContactPhone = "+974 4353 3344",
                        PreferredTimeWindow = "10:30 - 12:30"
                    }
                }
            };
            routeSet.Add(route1);

            var tripSet = db.Set<VanSalesTrip>();
            tripSet.Add(new VanSalesTrip
            {
                LegalEntityId = legalEntityId,
                TripNumber = "TRIP-2026-0001",
                RouteId = route1.Id,
                RouteName = route1.Name,
                DriverName = route1.DriverName ?? "Driver",
                VehicleNumber = route1.VehicleNumber ?? "Van",
                DepartureDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = TripStatus.InProgress,
                TotalSalesAmount = 8450m,
                TotalCollectionsAmount = 8450m,
                Notes = "On-schedule, morning delivery batch completed"
            });

            var tierSet = db.Set<WholesalePriceMatrix>();
            tierSet.Add(new WholesalePriceMatrix
            {
                LegalEntityId = legalEntityId,
                ItemCode = "HW-SCAN-WH",
                ItemName = "Industrial Barcode Scanner",
                Tier = WholesaleTier.Gold,
                MinQuantity = 50,
                DiscountPercentage = 20m,
                WholesaleUnitPrice = 2000m,
                IsActive = true
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedManufacturingAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var bomSet = db.Set<BillOfMaterials>();
        if (!await bomSet.AnyAsync(ct))
        {
            var bom = new BillOfMaterials
            {
                LegalEntityId = legalEntityId,
                Code = "BOM-IOT-2026",
                Name = "Industrial IoT Edge Gateway Hub v2.0",
                ProducedItemCode = "HW-IOT-GW02",
                ProducedItemName = "Industrial IoT Edge Gateway Hub",
                OutputQuantity = 1,
                Uom = "EA",
                Version = "2.0",
                LaborCostPerUnit = 28.50m,
                OverheadCostPerUnit = 14.00m,
                IsActive = true,
                Description = "DIN-rail industrial edge gateway with 4G/LTE fallback and Modbus RTU",
                Components = new List<BomComponent>
                {
                    new()
                    {
                        ComponentItemCode = "RAW-ALUM-CASE",
                        ComponentItemName = "Extruded Anodized Aluminum DIN Enclosure",
                        QuantityRequired = 1,
                        Uom = "PCS",
                        UnitCost = 22.00m
                    },
                    new()
                    {
                        ComponentItemCode = "PCB-MAIN-SMT",
                        ComponentItemName = "Quad-Core ARM Motherboard PCB Assembly",
                        QuantityRequired = 1,
                        Uom = "PCS",
                        UnitCost = 54.00m
                    },
                    new()
                    {
                        ComponentItemCode = "PSU-DIN-24V",
                        ComponentItemName = "Industrial 24V DC Isolated Power Converter",
                        QuantityRequired = 1,
                        Uom = "PCS",
                        UnitCost = 16.50m
                    }
                }
            };
            bomSet.Add(bom);

            var woSet = db.Set<MfgWorkOrder>();
            woSet.Add(new MfgWorkOrder
            {
                LegalEntityId = legalEntityId,
                WorkOrderNumber = "WO-2026-0001",
                BomId = bom.Id,
                BomCode = bom.Code,
                ProducedItemCode = bom.ProducedItemCode,
                ProducedItemName = bom.ProducedItemName,
                PlannedQuantity = 200,
                CompletedQuantity = 65,
                ScrappedQuantity = 1,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                Status = MfgWorkOrderStatus.InProgress,
                EstimatedTotalCost = 27000m,
                ActualTotalCost = 9120m,
                Operations = new List<WorkOrderOperation>
                {
                    new()
                    {
                        SequenceOrder = 1,
                        OperationName = "SMT Surface-Mount Electronics Assembly",
                        WorkCenterName = "Line 1 - SMT Pick & Place",
                        PlannedHours = 20,
                        ActualHours = 20,
                        HourlyRate = 45m,
                        Status = OperationStatus.Completed
                    },
                    new()
                    {
                        SequenceOrder = 2,
                        OperationName = "Mechanical Enclosure & Thermal Pad Integration",
                        WorkCenterName = "Line 2 - Final Box Assembly",
                        PlannedHours = 25,
                        ActualHours = 12,
                        HourlyRate = 45m,
                        Status = OperationStatus.InProgress
                    }
                }
            });

            var costSet = db.Set<ProductCostingRecord>();
            costSet.Add(new ProductCostingRecord
            {
                LegalEntityId = legalEntityId,
                ItemId = bom.ProducedItemId,
                ItemCode = bom.ProducedItemCode,
                ItemName = bom.ProducedItemName,
                CalculationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                MaterialCost = 92.50m,
                LaborCost = 28.50m,
                OverheadCost = 14.00m,
                StandardTotalUnitCost = 135.00m,
                LatestActualUnitCost = 136.20m,
                CostVariance = 1.20m
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedCrmAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var leadSet = db.Set<CrmLead>();
        if (!await leadSet.AnyAsync(ct))
        {
            var lead1 = new CrmLead
            {
                LegalEntityId = legalEntityId,
                LeadNumber = "LEAD-2026-0001",
                FullName = "Dr. Jonathan Vance",
                CompanyName = "Vance Healthcare Group",
                JobTitle = "Chief Technology Officer",
                Email = "j.vance@vancehealth.example.com",
                Phone = "+974 5081 2345",
                Source = LeadSource.Website,
                Status = LeadStatus.Qualified,
                EstimatedValue = 185000m,
                AssignedUserName = "Demo Administrator",
                Notes = "Inquiring about telematics and clinical ERP inventory modules"
            };
            var lead2 = new CrmLead
            {
                LegalEntityId = legalEntityId,
                LeadNumber = "LEAD-2026-0002",
                FullName = "Fatima Al-Nuaimi",
                CompanyName = "Qatar Petro-Chemicals Q.S.C.",
                JobTitle = "VP Supply Chain & Operations",
                Email = "fatima@epcfze.example.com",
                Phone = "+974 5592 1789",
                Source = LeadSource.Referral,
                Status = LeadStatus.Contacted,
                EstimatedValue = 340000m,
                AssignedUserName = "Demo Administrator",
                Notes = "Referred by Gulf Partner Network for multi-plant manufacturing rollout"
            };
            leadSet.AddRange(lead1, lead2);

            var oppSet = db.Set<CrmOpportunity>();
            oppSet.Add(new CrmOpportunity
            {
                LegalEntityId = legalEntityId,
                OpportunityNumber = "OPP-2026-0001",
                Title = "Enterprise ERP & Distribution Suite Rollout",
                CustomerName = "Vance Healthcare Group",
                Stage = OpportunityStage.Proposal,
                ExpectedRevenue = 185000m,
                ProbabilityPercent = 70,
                ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25)),
                AssignedUserName = "Demo Administrator",
                Notes = "Formal RFP proposal submitted; board review scheduled next week"
            });

            var actSet = db.Set<CrmInteraction>();
            actSet.Add(new CrmInteraction
            {
                LegalEntityId = legalEntityId,
                RelatedEntityKind = "Lead",
                RelatedEntityId = lead1.Id,
                Type = InteractionType.Meeting,
                Subject = "Architecture Scope & Solution Deep-Dive",
                Details = "Met with Dr. Vance and chief architect to review security, multi-tenancy, and compliance.",
                InteractionDate = DateTimeOffset.UtcNow.AddDays(-2),
                PerformedByName = "Demo Administrator",
                IsResolved = true
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedServicesAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var engSet = db.Set<ServiceEngagement>();
        if (!await engSet.AnyAsync(ct))
        {
            var eng = new ServiceEngagement
            {
                LegalEntityId = legalEntityId,
                EngagementCode = "ENG-2026-0001",
                Title = "Cloud Infrastructure & Microservices Transformation",
                ClientId = Guid.NewGuid(),
                ClientName = "Apex Global Trading W.L.L.",
                Type = EngagementType.FixedPrice,
                Status = EngagementStatus.Active,
                ContractValue = 145000m,
                BudgetedHours = 450m,
                ConsumedHours = 175m,
                ConsumedCost = 42000m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-40)),
                TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50)),
                LeadConsultantName = "Alex Carter (Principal Architect)",
                Description = "Complete migration to Kubernetes and automated CI/CD microservice workflows"
            };
            engSet.Add(eng);

            var retSet = db.Set<RetainerContract>();
            retSet.Add(new RetainerContract
            {
                LegalEntityId = legalEntityId,
                RetainerNumber = "RET-2026-0001",
                EngagementId = eng.Id,
                ClientId = eng.ClientId,
                ClientName = eng.ClientName,
                Title = "Executive DevOps & SLA Support Retainer",
                Cycle = RetainerCycle.Monthly,
                Status = RetainerStatus.Active,
                RecurringFee = 8500m,
                IncludedHours = 30m,
                OverageHourlyRate = 275m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
                Terms = "Includes 24/7 incident escalation and weekly sprint reviews"
            });

            var mlsSet = db.Set<BillingMilestone>();
            mlsSet.Add(new BillingMilestone
            {
                LegalEntityId = legalEntityId,
                MilestoneCode = "MLS-2026-0001",
                EngagementId = eng.Id,
                Title = "Phase 1 Architecture Blueprint Sign-Off",
                Amount = 45000m,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                CompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
                Status = SvcMilestoneStatus.ReadyToInvoice,
                Notes = "Client signed acceptance form on Oct 21"
            });
            mlsSet.Add(new BillingMilestone
            {
                LegalEntityId = legalEntityId,
                MilestoneCode = "MLS-2026-0002",
                EngagementId = eng.Id,
                Title = "Phase 2 Production Cluster Cutover",
                Amount = 60000m,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                Status = SvcMilestoneStatus.Planned,
                Notes = "Planned production maintenance window"
            });

            var timeSet = db.Set<TimeEntryRecord>();
            timeSet.Add(new TimeEntryRecord
            {
                LegalEntityId = legalEntityId,
                EngagementId = eng.Id,
                ConsultantUserId = Guid.NewGuid(),
                ConsultantName = "Alex Carter",
                ActivityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                Hours = 7.5m,
                HourlyRate = 220m,
                TotalAmount = 1650m,
                TaskDescription = "Hardened Istio service mesh mTLS policies and conducted vulnerability scans",
                IsBillable = true,
                IsApproved = true,
                ApprovedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedProjectsAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var projSet = db.Set<Project>();
        if (!await projSet.AnyAsync(ct))
        {
            var proj = new Project
            {
                LegalEntityId = legalEntityId,
                Number = "PRJ-2026-0001",
                Name = "E-Commerce Omnichannel Re-platforming",
                CustomerName = "Oasis Retailers Corp",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
                BudgetAmount = 95000m,
                Status = ProjectStatus.Active,
                BillingType = ProjectBillingType.FixedFee,
                ManagerName = "Elena Rostova",
                Description = "Modernize storefront with headless Angular frontends and instant checkout API"
            };
            projSet.Add(proj);

            var taskSet = db.Set<ProjectTask>();
            taskSet.Add(new ProjectTask
            {
                ProjectId = proj.Id,
                Number = "TSK-001",
                Title = "Implement Payment Gateway ApplePay & GooglePay",
                AssignedTo = "Elena Rostova",
                Status = ProjectTaskStatus.InProgress,
                Priority = TaskPriority.High,
                EstimatedHours = 35m,
                ActualHours = 18m,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
            });
            taskSet.Add(new ProjectTask
            {
                ProjectId = proj.Id,
                Number = "TSK-002",
                Title = "Design High-Fidelity Figma Prototype for Mobile Web",
                AssignedTo = "Samir Qazi",
                Status = ProjectTaskStatus.Done,
                Priority = TaskPriority.Medium,
                EstimatedHours = 40m,
                ActualHours = 38m,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
            });
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Months of demo history, counted back from the current month (3.5 years). Demo data only.</summary>
    private const int HistoryMonths = 42;
    /// <summary>The first version of the seeder covered only this many months; a database seeded then is backfilled with the older months.</summary>
    private const int LegacyHistoryMonths = 24;

    private static async Task SeedTwoYearHistoryAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var clock = DateTime.UtcNow;
        var oldest = clock.AddMonths(-(HistoryMonths - 1));
        var legacyOldest = clock.AddMonths(-(LegacyHistoryMonths - 1));
        async Task<bool> HasPayrollAsync(DateTime month) { var number = $"PR-{month.Year}-{month.Month:D2}"; return await db.Set<PayrollRun>().IgnoreQueryFilters().AnyAsync(r => r.OrgId == orgId && r.Number == number, ct); }
        if (await HasPayrollAsync(oldest)) return; // already seeded with the full history
        var backfillOnly = await HasPayrollAsync(legacyOldest); // seeded by the 24-month version: add only the older months

        var customers = await db.Parties.IgnoreQueryFilters().Where(p => p.OrgId == orgId && (p.Roles & PartyRoles.Customer) != 0).ToListAsync(ct);
        var vendors = await db.Parties.IgnoreQueryFilters().Where(p => p.OrgId == orgId && (p.Roles & PartyRoles.Vendor) != 0).ToListAsync(ct);
        var employees = await db.Set<Employee>().IgnoreQueryFilters().Where(e => e.OrgId == orgId).ToListAsync(ct);
        var items = await db.Items.IgnoreQueryFilters().Where(i => i.OrgId == orgId).ToListAsync(ct);
        var reProperties = await db.Set<Property>().IgnoreQueryFilters().Where(p => p.OrgId == orgId).ToListAsync(ct);
        var reUnits = await db.Set<Unit>().IgnoreQueryFilters().Where(u => u.OrgId == orgId).ToListAsync(ct);
        var warehouses = await db.Set<Warehouse>().IgnoreQueryFilters().Where(w => w.OrgId == orgId).ToListAsync(ct);
        var posRegisters = await db.Set<PosRegister>().IgnoreQueryFilters().Where(r => r.OrgId == orgId).ToListAsync(ct);
        var routes = await db.Set<DistributionRoute>().IgnoreQueryFilters().Where(r => r.OrgId == orgId).ToListAsync(ct);
        var boms = await db.Set<BillOfMaterials>().IgnoreQueryFilters().Where(b => b.OrgId == orgId).ToListAsync(ct);
        var engagements = await db.Set<ServiceEngagement>().IgnoreQueryFilters().Where(e => e.OrgId == orgId).ToListAsync(ct);

        var taxCode = await db.TaxCodes.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.OrgId == orgId && t.IsDefault, ct);
        var bankAcc = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.OrgId == orgId && a.IsBank, ct);
        var arAcc = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.OrgId == orgId && (a.SystemRole == "ar.control" || a.Type == AccountType.Asset), ct);
        var apAcc = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.OrgId == orgId && (a.SystemRole == "ap.control" || a.Type == AccountType.Liability), ct);
        var incomeAcc = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.OrgId == orgId && a.Type == AccountType.Income, ct);
        var expenseAcc = await db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.OrgId == orgId && a.Type == AccountType.Expense, ct);

        // 1. One annual budget per fiscal year in the history window (budgets grow about 15% a year)
        {
            var budgetYears = (await db.Budgets.IgnoreQueryFilters().Where(b => b.OrgId == orgId).Select(b => b.FiscalYear).ToListAsync(ct)).ToHashSet();
            foreach (var yr in Enumerable.Range(oldest.Year, clock.Year - oldest.Year + 1).Where(y => !budgetYears.Contains(y)))
            {
                var scale = (decimal)Math.Pow(1.15, yr - oldest.Year);
                var b = new Budget
                {
                    LegalEntityId = legalEntityId,
                    FiscalYear = yr,
                    Name = $"FY{yr} Annual Operating & Capital Budget",
                    IsActive = true,
                    Notes = $"Approved by board of directors for corporate operations in fiscal year {yr}"
                };
                for (int mth = 1; mth <= 12; mth++)
                {
                    if (incomeAcc != null)
                        b.Lines.Add(new BudgetLine { AccountId = incomeAcc.Id, Month = mth, Amount = Math.Round(150000m * scale, 0) });
                    if (expenseAcc != null)
                        b.Lines.Add(new BudgetLine { AccountId = expenseAcc.Id, Month = mth, Amount = Math.Round(80000m * scale, 0) });
                }
                db.Budgets.Add(b);
            }
            await db.SaveChangesAsync(ct);
        }

        // 2. Loop across the history window, oldest month first
        var now = clock;
        var newestMonthOffset = backfillOnly ? LegacyHistoryMonths : 0;
        var leadNames = new[] { "Sheikh Mansoor Al-Qasimi", "Farhan & Brothers Trading", "Gulf Prime Logistics W.L.L.", "Elena Petrova Real Estate", "Al-Majid Technical Solutions", "Horizon Media Agency FZ" };

        var existingPayrollYearsMonths = (await db.Set<PayrollRun>().IgnoreQueryFilters()
            .Where(r => r.OrgId == orgId)
            .Select(r => new { r.Year, r.Month })
            .ToListAsync(ct))
            .Select(r => (r.Year, r.Month))
            .ToHashSet();

        var existingAttendanceKeys = (await db.Set<AttendanceRecord>().IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId)
            .Select(a => new { a.EmployeeId, a.Year, a.Month })
            .ToListAsync(ct))
            .Select(a => (a.EmployeeId, a.Year, a.Month))
            .ToHashSet();

        for (int m = HistoryMonths - 1; m >= newestMonthOffset; m--)
        {
            var targetDate = now.AddMonths(-m);
            var year = targetDate.Year;
            var month = targetDate.Month;
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var isPastMonth = (m > 0);

            // A. Invoices & Customer Settlements
            if (customers.Count > 0 && items.Count > 0)
            {
                var cust = customers[m % customers.Count];
                var item = items[m % items.Count];
                var net = Math.Round((25000m + (m * 850m) % 35000m) * (1m + (HistoryMonths - 1 - m) * 0.01m), 2); // sales trend upward over the years
                var tax = Math.Round(net * 0m, 2);
                var total = net + tax;

                var inv = new Invoice
                {
                    LegalEntityId = legalEntityId,
                    Number = $"INV-{year}-{month:D2}-{m + 1:D3}",
                    IsCreditNote = false,
                    PartyId = cust.Id,
                    PartyName = cust.Name,
                    InvoiceDate = new DateOnly(year, month, Math.Min(5, daysInMonth)),
                    DueDate = new DateOnly(year, month, Math.Min(25, daysInMonth)),
                    CurrencyCode = "QAR",
                    ExchangeRate = 1m,
                    Net = net,
                    Tax = tax,
                    Total = total,
                    Paid = isPastMonth ? total : Math.Round(total * 0.4m, 2),
                    Status = isPastMonth ? DocumentStatus.Paid : DocumentStatus.PartlyPaid,
                    Notes = $"Professional commercial billing cycle for {year}-{month:D2}",
                    Lines = new List<InvoiceLine>
                    {
                        new()
                        {
                            Seq = 1,
                            ItemId = item.Id,
                            Description = $"{item.Name} - Scheduled period fulfillment {year}-{month:D2}",
                            Quantity = 1,
                            Price = net,
                            TaxCodeId = taxCode?.Id,
                            TaxAmount = tax
                        }
                    }
                };
                db.Invoices.Add(inv);

                if (isPastMonth)
                {
                    var settlement = new Settlement
                    {
                        LegalEntityId = legalEntityId,
                        Number = $"RCT-{year}-{month:D2}-{m + 1:D3}",
                        Direction = InstrumentDirection.Received,
                        PartyId = cust.Id,
                        PartyName = cust.Name,
                        Date = new DateOnly(year, month, Math.Min(20, daysInMonth)),
                        Method = SettlementMethod.BankTransfer,
                        BankAccountId = bankAcc?.Id,
                        CurrencyCode = "QAR",
                        Amount = total,
                        Unallocated = 0m,
                        Reference = $"WIRE-IN-{year}{month:D2}{m + 1:D2}",
                        Status = DocumentStatus.Paid,
                        Allocations = new List<SettlementAllocation>
                        {
                            new() { InvoiceId = inv.Id, Amount = total }
                        }
                    };
                    db.Settlements.Add(settlement);
                }
            }

            // B. Vendor Bills
            if (vendors.Count > 0)
            {
                var vend = vendors[m % vendors.Count];
                var billNet = 3800m + (m * 120m) % 2400m;
                var billTax = Math.Round(billNet * 0m, 2);
                var billTotal = billNet + billTax;

                var bill = new Bill
                {
                    LegalEntityId = legalEntityId,
                    Number = $"BIL-{year}-{month:D2}-{m + 1:D3}",
                    PartyId = vend.Id,
                    PartyName = vend.Name,
                    BillDate = new DateOnly(year, month, Math.Min(10, daysInMonth)),
                    DueDate = new DateOnly(year, month, Math.Min(28, daysInMonth)),
                    CurrencyCode = "QAR",
                    ExchangeRate = 1m,
                    Net = billNet,
                    Tax = billTax,
                    Total = billTotal,
                    Paid = isPastMonth ? billTotal : 0m,
                    Status = isPastMonth ? DocumentStatus.Paid : DocumentStatus.Issued,
                    VendorReference = $"VR-{year}{month:D2}-{m + 1:D2}",
                    Notes = $"Consolidated utility and facility maintenance charges {year}-{month:D2}",
                    Lines = new List<BillLine>
                    {
                        new()
                        {
                            Seq = 1,
                            Description = $"Commercial utilities and maintenance service {year}-{month:D2}",
                            Quantity = 1,
                            Price = billNet,
                            TaxAmount = billTax,
                            Net = billNet,
                            AccountId = expenseAcc?.Id
                        }
                    }
                };
                db.Bills.Add(bill);
            }

            // C. General Ledger Journal Entries
            if (expenseAcc != null && arAcc != null)
            {
                var depAmount = 5500m + (m * 50m);
                var jn = new JournalEntry
                {
                    LegalEntityId = legalEntityId,
                    Number = $"JV-{year}-{month:D2}-DEP",
                    Date = new DateOnly(year, month, Math.Min(28, daysInMonth)),
                    CurrencyCode = "QAR",
                    ExchangeRate = 1m,
                    Amount = depAmount,
                    Memo = $"Monthly asset depreciation and hardware amortization allocation for {year}-{month:D2}",
                    Reference = $"DEP-{year}{month:D2}",
                    Status = JournalStatus.Posted,
                    PostedAt = DateTime.UtcNow,
                    Lines = new List<JournalLine>
                    {
                        new() { Seq = 1, AccountId = expenseAcc.Id, Debit = depAmount, Credit = 0m, Description = "Depreciation & Operating Amortization Expense" },
                        new() { Seq = 2, AccountId = arAcc.Id, Debit = 0m, Credit = depAmount, Description = "Accumulated Depreciation & Reserves" }
                    }
                };
                jn.Recalculate();
                db.JournalEntries.Add(jn);
            }

            // D. Bank Statement Lines
            if (bankAcc != null)
            {
                db.BankStatementLines.AddRange(
                    new BankStatementLine
                    {
                        LegalEntityId = legalEntityId,
                        AccountId = bankAcc.Id,
                        Date = new DateOnly(year, month, Math.Min(8, daysInMonth)),
                        Description = $"Client wire transfer invoice settlement - {year}-{month:D2}",
                        Reference = $"STMT-{year}{month:D2}-01",
                        Amount = 28500m + (m * 400m) % 15000m,
                        Status = StatementLineStatus.Matched
                    },
                    new BankStatementLine
                    {
                        LegalEntityId = legalEntityId,
                        AccountId = bankAcc.Id,
                        Date = new DateOnly(year, month, Math.Min(18, daysInMonth)),
                        Description = $"Direct debit statutory utility & operational payment - {year}-{month:D2}",
                        Reference = $"STMT-{year}{month:D2}-02",
                        Amount = -(3800m + (m * 120m) % 2400m),
                        Status = StatementLineStatus.Matched
                    }
                );
            }

            // E. HR Payroll & Attendance
            if (employees.Count > 0)
            {
                if (!existingPayrollYearsMonths.Contains((year, month)))
                {
                    var grossSum = employees.Sum(e => e.GrossMonthly);
                    var pr = new PayrollRun
                    {
                        LegalEntityId = legalEntityId,
                        Number = $"PR-{year}-{month:D2}",
                        Year = year,
                        Month = month,
                        PlannedPayDate = new DateOnly(year, month, Math.Min(28, daysInMonth)),
                        PaidOn = isPastMonth ? new DateOnly(year, month, Math.Min(28, daysInMonth)) : null,
                        Status = isPastMonth ? PayrollStatus.Paid : PayrollStatus.Draft,
                        Employees = employees.Count,
                        GrossEarnings = grossSum,
                        Deductions = 0m,
                        NetPay = grossSum,
                        Notes = $"Automated payroll cycle with bank WPS (Qatar Wage Protection System) file cleared for {year}-{month:D2}"
                    };
                    foreach (var emp in employees)
                    {
                        pr.Lines.Add(new PayrollLine
                        {
                            EmployeeId = emp.Id,
                            EmployeeName = emp.FullName,
                            EmployeeNumber = emp.Number,
                            Basic = emp.BasicSalary,
                            Allowances = emp.Allowances,
                            GrossEarnings = emp.GrossMonthly,
                            NetPay = emp.GrossMonthly
                        });
                    }
                    db.Set<PayrollRun>().Add(pr);
                    existingPayrollYearsMonths.Add((year, month));
                }

                foreach (var emp in employees)
                {
                    if (!existingAttendanceKeys.Contains((emp.Id, year, month)))
                    {
                        db.Set<AttendanceRecord>().Add(new AttendanceRecord
                        {
                            LegalEntityId = legalEntityId,
                            EmployeeId = emp.Id,
                            Year = year,
                            Month = month,
                            WorkingDays = 26,
                            DaysPresent = 25 + (m % 2),
                            PaidLeaveDays = (m % 2),
                            OvertimeHours = (m * 1.5m) % 8m
                        });
                        existingAttendanceKeys.Add((emp.Id, year, month));
                    }
                }
            }

            // F. Inventory Stock Movements
            if (warehouses.Count > 0 && items.Count > 0)
            {
                var wh = warehouses[m % warehouses.Count];
                var itm = items[m % items.Count];
                db.Set<StockMovement>().AddRange(
                    new StockMovement
                    {
                        LegalEntityId = legalEntityId,
                        WarehouseId = wh.Id,
                        ItemId = itm.Id,
                        Kind = MovementKind.Receipt,
                        Quantity = 60 + (m % 20),
                        UnitCost = 450m,
                        Reference = $"GRN-{year}-{month:D2}-01",
                        Date = new DateOnly(year, month, Math.Min(4, daysInMonth)),
                        Memo = $"Supplier consignment replenishment batch {year}-{month:D2}"
                    },
                    new StockMovement
                    {
                        LegalEntityId = legalEntityId,
                        WarehouseId = wh.Id,
                        ItemId = itm.Id,
                        Kind = MovementKind.Issue,
                        Quantity = -(40 + (m % 15)),
                        UnitCost = 450m,
                        Reference = $"DISP-{year}-{month:D2}-01",
                        Date = new DateOnly(year, month, Math.Min(22, daysInMonth)),
                        Memo = $"Outbound customer order fulfillment batch {year}-{month:D2}"
                    }
                );
            }

            // G. Sales Quotes & Orders
            if (customers.Count > 0 && items.Count > 0)
            {
                var cust = customers[m % customers.Count];
                var itm = items[m % items.Count];
                var qty = 5 + (m % 10);
                var unitPrice = (decimal)(itm.SalesPrice ?? 2500m);
                var total = qty * unitPrice;

                var sq = new SalesQuote
                {
                    LegalEntityId = legalEntityId,
                    Number = $"QT-{year}-{month:D2}-{m + 1:D2}",
                    CustomerName = cust.Name,
                    CustomerEmail = cust.Email ?? "client@example.com",
                    QuoteDate = new DateOnly(year, month, Math.Min(3, daysInMonth)),
                    ValidUntil = new DateOnly(year, month, Math.Min(25, daysInMonth)),
                    Currency = "QAR",
                    SubTotal = total,
                    TaxTotal = Math.Round(total * 0m, 2),
                    TotalAmount = total + Math.Round(total * 0m, 2),
                    Status = SalesQuoteStatus.Accepted,
                    Notes = $"Quarterly procurement proposal for {year}-{month:D2}",
                    Lines = new List<SalesQuoteLine>
                    {
                        new() { ItemCode = itm.Sku, Description = itm.Name, Quantity = qty, UnitPrice = unitPrice, LineTotal = total, TaxRate = 0m }
                    }
                };
                db.Set<SalesQuote>().Add(sq);

                var so = new SalesOrder
                {
                    LegalEntityId = legalEntityId,
                    Number = $"SO-{year}-{month:D2}-{m + 1:D2}",
                    QuoteId = sq.Id,
                    QuoteNumber = sq.Number,
                    CustomerName = cust.Name,
                    OrderDate = new DateOnly(year, month, Math.Min(8, daysInMonth)),
                    ExpectedDeliveryDate = new DateOnly(year, month, Math.Min(24, daysInMonth)),
                    Currency = "QAR",
                    SubTotal = total,
                    TaxTotal = Math.Round(total * 0m, 2),
                    TotalAmount = total + Math.Round(total * 0m, 2),
                    Status = isPastMonth ? SalesOrderStatus.Delivered : SalesOrderStatus.Confirmed,
                    ShippingAddress = cust.Address ?? "Commercial District, Doha, Qatar",
                    Notes = $"Confirmed commercial purchase contract for {year}-{month:D2}",
                    Lines = new List<SalesOrderLine>
                    {
                        new() { ItemCode = itm.Sku, Description = itm.Name, Quantity = qty, UnitPrice = unitPrice, LineTotal = total, TaxRate = 0m }
                    }
                };
                db.Set<SalesOrder>().Add(so);
            }

            // H. Purchasing Orders & Goods Receipts
            if (vendors.Count > 0 && items.Count > 0)
            {
                var vend = vendors[m % vendors.Count];
                var itm = items[m % items.Count];
                var qty = 10 + (m % 15);
                var cost = (decimal)(itm.PurchasePrice ?? 1500m);
                var poTotal = qty * cost;

                var po = new PurchaseOrder
                {
                    LegalEntityId = legalEntityId,
                    Number = $"PO-{year}-{month:D2}-{m + 1:D2}",
                    VendorName = vend.Name,
                    OrderDate = new DateOnly(year, month, Math.Min(6, daysInMonth)),
                    ExpectedDeliveryDate = new DateOnly(year, month, Math.Min(22, daysInMonth)),
                    Currency = "QAR",
                    SubTotal = poTotal,
                    TaxTotal = Math.Round(poTotal * 0m, 2),
                    TotalAmount = poTotal + Math.Round(poTotal * 0m, 2),
                    Status = PurchaseOrderStatus.Approved,
                    PaymentTerms = "Net 30 Days",
                    Notes = $"Standard stock replenishment PO for {year}-{month:D2}",
                    Lines = new List<PurchaseOrderLine>
                    {
                        new() { ItemCode = itm.Sku, Description = itm.Name, Quantity = qty, UnitPrice = cost, LineTotal = poTotal, TaxRate = 0m }
                    }
                };
                db.Set<PurchaseOrder>().Add(po);

                if (isPastMonth)
                {
                    db.Set<GoodsReceipt>().Add(new GoodsReceipt
                    {
                        LegalEntityId = legalEntityId,
                        Number = $"GRN-{year}-{month:D2}-{m + 1:D2}",
                        PurchaseOrderId = po.Id,
                        PurchaseOrderNumber = po.Number,
                        VendorName = po.VendorName,
                        ReceiptDate = new DateOnly(year, month, Math.Min(20, daysInMonth)),
                        ReceivedBy = "Khalid Al-Marri",
                        Notes = $"Packages verified in good order {year}-{month:D2}"
                    });
                }
            }

            // I. POS Sessions & Orders
            if (posRegisters.Count > 0 && items.Count > 0)
            {
                var reg = posRegisters[m % posRegisters.Count];
                var itm = items[m % items.Count];
                var posSales = 14500m + (m * 450m) % 9000m;
                var ordCount = 20 + (m % 10);
                var sessDate = new DateTime(year, month, Math.Min(15, daysInMonth), 8, 30, 0, DateTimeKind.Utc);

                var sess = new PosSession
                {
                    LegalEntityId = legalEntityId,
                    RegisterId = reg.Id,
                    RegisterName = reg.Name,
                    CashierName = "Fatima Al-Zahra",
                    OpenedByUserId = Guid.NewGuid(),
                    OpenedAt = sessDate,
                    ClosedAt = sessDate.AddHours(12),
                    OpeningCash = 2000m,
                    TotalSales = posSales,
                    ExpectedCash = 2000m + (posSales * 0.45m),
                    Status = PosSessionStatus.Closed,
                    Notes = $"Daily retail shift for {year}-{month:D2}"
                };
                db.Set<PosSession>().Add(sess);

                var ord = new PosOrder
                {
                    LegalEntityId = legalEntityId,
                    RegisterId = reg.Id,
                    SessionId = sess.Id,
                    Number = $"POS-{year}{month:D2}-{m + 1:D3}",
                    CustomerName = "Walk-in Retail Customer",
                    CashierName = "Fatima Al-Zahra",
                    OrderDate = sessDate.AddHours(4),
                    SubTotal = 450m,
                    TaxTotal = 22.5m,
                    TotalAmount = 472.5m,
                    Status = PosOrderStatus.Completed,
                    PaymentStatus = PosPaymentStatus.Paid,
                    Lines = new List<PosOrderLine>
                    {
                        new() { ItemCode = itm.Sku, Description = itm.Name, Quantity = 1, UnitPrice = 450m, TaxRate = 0m, LineTotal = 450m }
                    },
                    Payments = new List<PosPayment>
                    {
                        new() { Method = PosPaymentMethod.Card, Amount = 472.5m, PaymentDate = sessDate.AddHours(4) }
                    }
                };
                db.Set<PosOrder>().Add(ord);
            }

            // J. Distribution Trips
            if (routes.Count > 0)
            {
                var rte = routes[m % routes.Count];
                var tripSales = 8500m + (m * 350m) % 4000m;
                db.Set<VanSalesTrip>().Add(new VanSalesTrip
                {
                    LegalEntityId = legalEntityId,
                    TripNumber = $"TRIP-{year}-{month:D2}-{m + 1:D2}",
                    RouteId = rte.Id,
                    RouteName = rte.Name,
                    DriverName = rte.DriverName ?? "Driver",
                    VehicleNumber = rte.VehicleNumber ?? "Van",
                    DepartureDate = new DateOnly(year, month, Math.Min(12, daysInMonth)),
                    ReturnDate = new DateOnly(year, month, Math.Min(12, daysInMonth)),
                    Status = TripStatus.Completed,
                    TotalSalesAmount = tripSales,
                    TotalCollectionsAmount = tripSales,
                    Notes = $"Scheduled wholesale route distribution trip for {year}-{month:D2}"
                });
            }

            // K. Manufacturing Work Orders
            if (boms.Count > 0)
            {
                var bom = boms[0];
                var mfgWo = new MfgWorkOrder
                {
                    LegalEntityId = legalEntityId,
                    WorkOrderNumber = $"MFG-{year}-{month:D2}-01",
                    BomId = bom.Id,
                    BomCode = bom.Code,
                    ProducedItemCode = bom.ProducedItemCode,
                    ProducedItemName = bom.ProducedItemName,
                    PlannedQuantity = 80 + (m % 20),
                    CompletedQuantity = isPastMonth ? (80 + (m % 20)) : 40,
                    ScrappedQuantity = 1,
                    StartDate = new DateOnly(year, month, Math.Min(5, daysInMonth)),
                    DueDate = new DateOnly(year, month, Math.Min(20, daysInMonth)),
                    Status = isPastMonth ? MfgWorkOrderStatus.Completed : MfgWorkOrderStatus.InProgress,
                    Notes = $"Production batch run for {bom.ProducedItemName} ({year}-{month:D2})"
                };
                db.Set<MfgWorkOrder>().Add(mfgWo);
            }

            // L. Real Estate Operations
            if (reProperties.Count > 0)
            {
                var prop = reProperties[m % reProperties.Count];
                var unit = reUnits.FirstOrDefault(u => u.PropertyId == prop.Id);
                var issues = new[] { "Quarterly Chiller Compressor Check", "Plumbing Pressure Balancing", "Elevator Brake Testing", "Emergency Light Inverter Test", "Fire Alarm Sensor Calibrations" };
                var categories = new[] { "HVAC", "Plumbing", "Elevator", "Electrical", "Fire Safety" };
                var cost = 1450m + (m * 85m) % 2200m;

                var reWo = new ReWorkOrder
                {
                    LegalEntityId = legalEntityId,
                    Number = $"WO-{year}-{month:D2}-{m + 1:D2}",
                    PropertyId = prop.Id,
                    UnitId = unit?.Id,
                    Issue = issues[m % issues.Length],
                    Category = categories[m % categories.Length],
                    Priority = RePriority.Normal,
                    ReportedOn = new DateOnly(year, month, Math.Min(8, daysInMonth)),
                    ReportedBy = "Facilities Monitoring System",
                    Technician = "Ravi Kumar (Al Futtaim FM)",
                    EstimatedCost = cost,
                    ActualCost = isPastMonth ? cost : 0m,
                    Details = $"Routine scheduled facilities servicing for {prop.Name}",
                    ResolutionNotes = isPastMonth ? "Servicing completed and passed inspection test" : null
                };
                if (isPastMonth)
                {
                    reWo.MoveTo(ReWorkOrderStatus.Assigned, new DateOnly(year, month, Math.Min(9, daysInMonth)));
                    reWo.MoveTo(ReWorkOrderStatus.InProgress, new DateOnly(year, month, Math.Min(10, daysInMonth)));
                    reWo.MoveTo(ReWorkOrderStatus.Completed, new DateOnly(year, month, Math.Min(12, daysInMonth)), "Completed");
                }
                else
                {
                    reWo.MoveTo(ReWorkOrderStatus.Assigned, new DateOnly(year, month, Math.Min(9, daysInMonth)));
                    reWo.MoveTo(ReWorkOrderStatus.InProgress, new DateOnly(year, month, Math.Min(10, daysInMonth)));
                }
                db.Set<ReWorkOrder>().Add(reWo);
            }

            // M. CRM Leads, Opportunities & Interactions
            var crmLead = new CrmLead
            {
                LegalEntityId = legalEntityId,
                LeadNumber = $"LEAD-{year}-{month:D2}-01",
                FullName = leadNames[m % leadNames.Length],
                CompanyName = $"{leadNames[m % leadNames.Length]} Holding",
                JobTitle = "Managing Director",
                Email = $"lead.{year}.{month}@example.com",
                Phone = $"+974 5{(m + 100) % 1000:D3} {m + 2000:D4}",
                Source = LeadSource.Referral,
                Status = m > 2 ? LeadStatus.Qualified : LeadStatus.Contacted,
                EstimatedValue = 150000m + (m * 10000m) % 200000m,
                AssignedUserName = "Demo Administrator",
                Notes = $"Inbound enterprise consultation inquiry from {year}-{month:D2}"
            };
            db.Set<CrmLead>().Add(crmLead);

            var crmOpp = new CrmOpportunity
            {
                LegalEntityId = legalEntityId,
                OpportunityNumber = $"OPP-{year}-{month:D2}-01",
                Title = $"ERP Deployment & Integration - {leadNames[m % leadNames.Length]}",
                CustomerName = crmLead.CompanyName,
                Stage = m > 3 ? OpportunityStage.ClosedWon : (m > 1 ? OpportunityStage.Proposal : OpportunityStage.Qualification),
                ExpectedRevenue = crmLead.EstimatedValue,
                ProbabilityPercent = m > 3 ? 100 : 65,
                ExpectedCloseDate = new DateOnly(year, month, Math.Min(26, daysInMonth)),
                AssignedUserName = "Demo Administrator",
                Notes = $"High-priority pipeline opportunity for {year}-{month:D2}"
            };
            db.Set<CrmOpportunity>().Add(crmOpp);

            db.Set<CrmInteraction>().Add(new CrmInteraction
            {
                LegalEntityId = legalEntityId,
                RelatedEntityKind = "Lead",
                RelatedEntityId = crmLead.Id,
                Type = InteractionType.Meeting,
                Subject = $"Strategy Consultation & Demo Workshop {year}-{month:D2}",
                Details = "Demonstrated core accounting, multi-currency reporting, and warehouse dispatch.",
                InteractionDate = new DateTimeOffset(new DateTime(year, month, Math.Min(14, daysInMonth), 11, 0, 0, DateTimeKind.Utc)),
                PerformedByName = "Demo Administrator",
                IsResolved = true
            });

            // N. Professional Services & Time Entries
            if (engagements.Count > 0)
            {
                var eng = engagements[0];
                db.Set<TimeEntryRecord>().Add(new TimeEntryRecord
                {
                    LegalEntityId = legalEntityId,
                    EngagementId = eng.Id,
                    ConsultantUserId = Guid.NewGuid(),
                    ConsultantName = "Alex Carter",
                    ActivityDate = new DateOnly(year, month, Math.Min(16, daysInMonth)),
                    Hours = 35m + (m % 10),
                    HourlyRate = 220m,
                    TotalAmount = (35m + (m % 10)) * 220m,
                    TaskDescription = $"Monthly architectural consulting, code reviews and system tuning ({year}-{month:D2})",
                    IsBillable = true,
                    IsApproved = true,
                    ApprovedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

