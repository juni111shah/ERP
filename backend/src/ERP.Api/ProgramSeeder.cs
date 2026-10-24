using ERP.Application.Abstractions;
using ERP.Domain.Common;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class ProgramSeeder
{
    public static async Task SeedModulesAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var section = config.GetSection("Seed");
        if (!section.GetValue<bool>("Enabled")) return;

        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ProgramSeeder");

        var demo = await db.Organizations.FirstOrDefaultAsync(o => o.Code == "demo", ct);
        if (demo is null) return;

        var legalEntity = await db.LegalEntities.FirstOrDefaultAsync(e => e.OrgId == demo.Id, ct);
        if (legalEntity is null) return;

        var orgCtx = sp.GetRequiredService<IOrgContext>();
        using (orgCtx.Use(demo.Id))
        {
            await EnableModulesAsync(db, demo.Id, ct);
            await SeedNumberSequencesAsync(db, demo.Id, legalEntity.Id, ct);

            // 1. Sales
            await SeedSalesAsync(db, demo.Id, legalEntity.Id, ct);

            // 2. Purchasing
            await SeedPurchasingAsync(db, demo.Id, legalEntity.Id, ct);

            // 3. POS
            await SeedPosAsync(db, demo.Id, legalEntity.Id, ct);

            // 4. Retail
            await SeedRetailAsync(db, demo.Id, legalEntity.Id, ct);

            // 5. Distribution
            await SeedDistributionAsync(db, demo.Id, legalEntity.Id, ct);

            // 6. Manufacturing
            await SeedManufacturingAsync(db, demo.Id, legalEntity.Id, ct);

            // 7. CRM
            await SeedCrmAsync(db, demo.Id, legalEntity.Id, ct);

            // 8. Professional Services
            await SeedServicesAsync(db, demo.Id, legalEntity.Id, ct);

            // 9. Projects
            await SeedProjectsAsync(db, demo.Id, legalEntity.Id, ct);

            await db.SaveChangesAsync(ct);
            log.LogInformation("Successfully verified and seeded test/demo data for all modular expansions in workspace 'demo'.");
        }
    }

    private static async Task EnableModulesAsync(AppDbContext db, Guid orgId, CancellationToken ct)
    {
        var modulesToEnable = new[]
        {
            "sales", "purchasing", "pos", "retail", "distribution",
            "manufacturing", "crm", "services", "projects", "inventory", "hr", "realestate"
        };

        foreach (var mod in modulesToEnable)
        {
            if (!await db.OrganizationModules.AnyAsync(m => m.OrgId == orgId && m.ModuleKey == mod, ct))
            {
                db.OrganizationModules.Add(new OrganizationModule
                {
                    OrgId = orgId,
                    ModuleKey = mod,
                    IsEnabled = true,
                    EnabledAt = DateTime.UtcNow
                });
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
            ("project", "PRJ")
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

    private static async Task SeedSalesAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        var quotesSet = db.Set<SalesQuote>();
        if (!await quotesSet.AnyAsync(ct))
        {
            var quote = new SalesQuote
            {
                LegalEntityId = legalEntityId,
                Number = "QT-2026-0001",
                CustomerName = "Apex Global Trading LLC",
                CustomerEmail = "procurement@apexglobal.example.com",
                QuoteDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
                Currency = "AED",
                Status = SalesQuoteStatus.Sent,
                SubTotal = 145000m,
                TaxTotal = 7250m,
                TotalAmount = 152250m,
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
                        TaxRate = 5m
                    },
                    new()
                    {
                        ItemCode = "SRV-IMPL",
                        Description = "Professional Implementation, Customization & Go-Live Support",
                        Quantity = 1,
                        UnitPrice = 50000m,
                        LineTotal = 50000m,
                        TaxRate = 5m
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
                Currency = "AED",
                Status = SalesOrderStatus.Confirmed,
                SubTotal = 68500m,
                TaxTotal = 3425m,
                TotalAmount = 71925m,
                PaymentTerms = "Net 30",
                ShippingAddress = "Al Quoz Industrial Area 3, Dubai, UAE",
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
                        TaxRate = 5m
                    },
                    new()
                    {
                        ItemCode = "POS-SCAN-2D",
                        Description = "Wireless Omni-directional 2D Barcode Scanner",
                        Quantity = 10,
                        UnitPrice = 850m,
                        LineTotal = 8500m,
                        TaxRate = 5m
                    },
                    new()
                    {
                        ItemCode = "POS-PRN-80",
                        Description = "Thermal High-Speed Receipt Printer 80mm USB/LAN",
                        Quantity = 10,
                        UnitPrice = 1500m,
                        LineTotal = 15000m,
                        TaxRate = 5m
                    }
                }
            };
            ordersSet.Add(order);
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
                Currency = "AED",
                Status = PurchaseOrderStatus.Approved,
                SubTotal = 42500m,
                TaxTotal = 2125m,
                TotalAmount = 44625m,
                PaymentTerms = "Net 60",
                DeliveryAddress = "Jebel Ali Free Zone, Warehouse Unit 14B, Dubai",
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
                        TaxRate = 5m
                    },
                    new()
                    {
                        ItemCode = "HW-DOCK-4B",
                        Description = "Multi-slot 4-Bay Battery Charging Cradle",
                        Quantity = 5,
                        UnitPrice = 1000m,
                        LineTotal = 5000m,
                        TaxRate = 5m
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
                ReceiptHeader = "=== DEMO TRADING LLC - DUBAI MALL ===\nTax Invoice / TRN: 100294827100003",
                ReceiptFooter = "Thank you for shopping with us!\nExchange within 14 days with original receipt."
            };
            var reg2 = new PosRegister
            {
                LegalEntityId = legalEntityId,
                Code = "REG-002",
                Name = "Express Self-Checkout A",
                IsActive = true,
                ReceiptHeader = "=== DEMO TRADING LLC - EXPRESS ===\nTax Invoice",
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
                        TaxRate = 5m
                    },
                    new()
                    {
                        ItemCode = "RET-ACC-02",
                        Description = "USB-C 65W Fast GaN Wall Charger",
                        Quantity = 1,
                        UnitPrice = 130m,
                        LineTotal = 130m,
                        TaxRate = 5m
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
                Name = "Downtown Flagship Boutique",
                StoreManagerName = "Noura Al-Zaabi",
                Phone = "+971 4 398 1234",
                Email = "downtown@demotrading.local",
                Address = "The Dubai Mall, Ground Floor Unit G-142",
                IsActive = true
            });
            storeSet.Add(new RetailStore
            {
                LegalEntityId = legalEntityId,
                Code = "STR-002",
                Name = "Marina Promenade Branch",
                StoreManagerName = "Karim Mansour",
                Phone = "+971 4 456 7890",
                Email = "marina@demotrading.local",
                Address = "Dubai Marina Walk, Retail Unit R-08",
                IsActive = true
            });

            var priceListSet = db.Set<RetailPriceList>();
            priceListSet.Add(new RetailPriceList
            {
                LegalEntityId = legalEntityId,
                Code = "PL-RETAIL-2026",
                Name = "Standard Consumer Retail Price List 2026",
                Type = PriceListType.Standard,
                Currency = "AED",
                ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
                IsActive = true,
                Description = "All UAE brick-and-mortar retail locations standard tier"
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
                Name = "Deira & Bur Dubai Commercial Route",
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
                        Address = "Al Baraha St, Deira, Dubai",
                        ContactPhone = "+971 4 271 1122",
                        PreferredTimeWindow = "08:00 - 10:00"
                    },
                    new()
                    {
                        CustomerName = "Al Fahidi Electronics Market",
                        SequenceOrder = 2,
                        Address = "Al Fahidi St, Bur Dubai",
                        ContactPhone = "+971 4 353 3344",
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

            var woSet = db.Set<WorkOrder>();
            woSet.Add(new WorkOrder
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
                Status = WorkOrderStatus.InProgress,
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
                Phone = "+971 50 812 3456",
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
                CompanyName = "Emirates Petro-Chemicals FZE",
                JobTitle = "VP Supply Chain & Operations",
                Email = "fatima@epcfze.example.com",
                Phone = "+971 55 921 7890",
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
                ClientName = "Apex Global Trading LLC",
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
                Status = ERP.Modules.Services.Domain.MilestoneStatus.ReadyToInvoice,
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
                Status = ERP.Modules.Services.Domain.MilestoneStatus.Planned,
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
        }
    }
}
