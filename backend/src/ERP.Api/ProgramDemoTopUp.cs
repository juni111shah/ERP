using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Infrastructure.Persistence;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;



/// <summary>
/// Fills the demo tables the main seeder leaves empty so every screen has something to show. Each block is guarded by an
/// "any rows yet" check, so running it again (every start-up) adds nothing.
/// </summary>
public static class ProgramDemoTopUp
{
    public static async Task RunAsync(AppDbContext db, Guid orgId, Guid legalEntityId, CancellationToken ct)
    {
        await SeedHrAsync(db, legalEntityId, ct);
        await db.SaveChangesAsync(ct);
        await SeedOthersAsync(db, legalEntityId, ct);
        await db.SaveChangesAsync(ct);
        await SeedProjectsExtrasAsync(db, legalEntityId, ct);
        await db.SaveChangesAsync(ct);
        await EnsureProjectCostAccountAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Workspaces created before project cost posting existed have no "Project cost recovery" account; add it so the posting works.</summary>
    private static async Task EnsureProjectCostAccountAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Set<Account>().AnyAsync(a => a.SystemRole == "projects.costrecovery", ct)) return;
        var code = "5080";
        if (await db.Set<Account>().AnyAsync(a => a.Code == code, ct)) code = "5081";
        db.Set<Account>().Add(new Account { Code = code, Name = "Project cost recovery", Type = AccountType.Expense, SystemRole = "projects.costrecovery", ReportingGroup = "Cost of sales", IsPostable = true, IsActive = true });
    }

    private static async Task SeedHrAsync(AppDbContext db, Guid legalEntityId, CancellationToken ct)
    {
        var staff = await db.Set<Employee>().Where(e => e.Status != EmployeeStatus.Terminated).OrderBy(e => e.Number).ToListAsync(ct);
        if (staff.Count == 0) return;
        var today = Today;

        // ---- structure: departments, positions, shifts -------------------------------------------------------------------
        var departments = await db.Set<Department>().ToListAsync(ct);
        if (departments.Count == 0)
        {
            departments =
            [
                new() { Code = "EXEC", Name = "Executive Management" },
                new() { Code = "FIN", Name = "Finance & Accounting" },
                new() { Code = "OPS", Name = "Real Estate Operations" },
                new() { Code = "HR", Name = "Human Resources" },
                new() { Code = "SALES", Name = "Sales & Leasing" },
                new() { Code = "IT", Name = "Information Technology" },
            ];
            db.Set<Department>().AddRange(departments);
            await db.SaveChangesAsync(ct);
        }
        var positions = await db.Set<Position>().ToListAsync(ct);
        if (positions.Count == 0)
        {
            positions =
            [
                new() { Code = "MD", Title = "Managing Director", Grade = "G1" },
                new() { Code = "FC", Title = "Financial Controller", Grade = "G2" },
                new() { Code = "PM", Title = "Property Manager", Grade = "G3" },
                new() { Code = "HRO", Title = "HR Officer", Grade = "G4" },
                new() { Code = "LA", Title = "Leasing Agent", Grade = "G5" },
                new() { Code = "ACC", Title = "Accountant", Grade = "G5" },
            ];
            db.Set<Position>().AddRange(positions);
            await db.SaveChangesAsync(ct);
        }
        var shifts = await db.Set<Shift>().ToListAsync(ct);
        if (shifts.Count == 0)
        {
            shifts =
            [
                new() { Code = "DAY", Name = "Day shift", StartTime = new TimeOnly(8, 0), GraceMinutes = 10, HoursPerDay = 8 },
                new() { Code = "EVE", Name = "Evening shift", StartTime = new TimeOnly(15, 0), GraceMinutes = 10, HoursPerDay = 8 },
                new() { Code = "NIGHT", Name = "Night shift", StartTime = new TimeOnly(22, 0), GraceMinutes = 5, HoursPerDay = 9 },
            ];
            db.Set<Shift>().AddRange(shifts);
            await db.SaveChangesAsync(ct);
        }

        // link employees to a department by their department text, give them a position, manager and contract
        var managerId = staff[0].Id;
        for (var i = 0; i < staff.Count; i++)
        {
            var e = staff[i];
            e.DepartmentId ??= departments.FirstOrDefault(d => string.Equals(d.Name, e.Department, StringComparison.OrdinalIgnoreCase))?.Id ?? departments[i % departments.Count].Id;
            e.PositionId ??= positions[i % positions.Count].Id;
            if (i > 0) e.ManagerEmployeeId ??= managerId;
            e.ContractType ??= i == staff.Count - 1 ? ContractType.Probation : ContractType.Permanent;
            if (e.ContractType == ContractType.Probation) e.ProbationEndDate ??= today.AddDays(20);
            e.NoticePeriodDays ??= 30;
            e.ShiftId ??= shifts[0].Id;
        }
        var head = departments.FirstOrDefault(d => d.Code == "EXEC");
        if (head is not null && head.HeadEmployeeId is null) head.HeadEmployeeId = managerId;
        await db.SaveChangesAsync(ct);

        // ---- leave types, balances, requests, holidays --------------------------------------------------------------------
        var types = await db.Set<LeaveType>().ToListAsync(ct);
        if (types.Count == 0)
        {
            var annual = new LeaveType { Code = "ANNUAL", Name = "Annual leave", IsPaid = true, AnnualDays = 21, CarryForwardCap = 5, ProRataFirstYear = true, Encashable = true, AccrualMode = LeaveAccrualMode.Monthly };
            annual.Tiers.Add(new LeaveEntitlementTier { FromServiceYears = 0, Days = 21 });
            annual.Tiers.Add(new LeaveEntitlementTier { FromServiceYears = 5, Days = 30 });
            var sick = new LeaveType { Code = "SICK", Name = "Sick leave", IsPaid = true, AnnualDays = 14, RequiresAttachmentAfterDays = 2 };
            sick.PayBands.Add(new LeavePayBand { FromDay = 1, PayPercent = 100 });
            sick.PayBands.Add(new LeavePayBand { FromDay = 8, PayPercent = 50 });
            var unpaid = new LeaveType { Code = "UNPAID", Name = "Unpaid leave", IsPaid = false, AnnualDays = 30 };
            var maternity = new LeaveType { Code = "MATERNITY", Name = "Maternity leave", IsPaid = true, AnnualDays = 50 };
            types = [annual, sick, unpaid, maternity];
            db.Set<LeaveType>().AddRange(types);
            await db.SaveChangesAsync(ct);
        }
        if (!await db.Set<PublicHoliday>().AnyAsync(ct))
        {
            var y = today.Year;
            db.Set<PublicHoliday>().AddRange(
                new PublicHoliday { Date = new DateOnly(y, 2, 11), Name = "National Day (Sports Day)" },
                new PublicHoliday { Date = new DateOnly(y, 12, 18), Name = "National Day" },
                new PublicHoliday { Date = new DateOnly(y + 1, 1, 1), Name = "New Year's Day" });
        }
        var annualType = types.First(t => t.Code == "ANNUAL");
        var sickType = types.First(t => t.Code == "SICK");
        if (!await db.Set<LeaveBalance>().AnyAsync(ct))
        {
            foreach (var e in staff)
            {
                db.Set<LeaveBalance>().Add(new LeaveBalance { EmployeeId = e.Id, LeaveTypeId = annualType.Id, Year = today.Year, Entitled = 21, Carried = 3, Taken = 0 });
                db.Set<LeaveBalance>().Add(new LeaveBalance { EmployeeId = e.Id, LeaveTypeId = sickType.Id, Year = today.Year, Entitled = 14, Carried = 0, Taken = 0 });
            }
        }
        if (!await db.Set<LeaveRequest>().AnyAsync(ct))
        {
            await db.SaveChangesAsync(ct);
            var balances = await db.Set<LeaveBalance>().ToListAsync(ct);
            void Request(Employee e, LeaveType t, int fromOffset, int days, LeaveStatus status, string reason)
            {
                var from = today.AddDays(fromOffset);
                db.Set<LeaveRequest>().Add(new LeaveRequest
                {
                    LegalEntityId = legalEntityId, EmployeeId = e.Id, EmployeeName = e.FullName, LeaveTypeId = t.Id, FromDate = from, ToDate = from.AddDays(days - 1), Days = days,
                    Status = status, Reason = reason, SubmittedAt = DateTime.UtcNow.AddDays(fromOffset - 5 < 0 ? fromOffset - 5 : -2),
                    DecidedAt = status is LeaveStatus.Approved or LeaveStatus.Rejected ? DateTime.UtcNow.AddDays(-1) : null,
                    DecisionComment = status == LeaveStatus.Rejected ? "Busy period, please pick other dates." : null,
                });
                if (status == LeaveStatus.Approved)
                {
                    var b = balances.FirstOrDefault(x => x.EmployeeId == e.Id && x.LeaveTypeId == t.Id && x.Year == today.Year);
                    if (b is not null) b.Taken += days;
                }
            }
            Request(staff[Math.Min(1, staff.Count - 1)], annualType, -40, 5, LeaveStatus.Approved, "Family trip");
            Request(staff[Math.Min(2, staff.Count - 1)], sickType, -12, 2, LeaveStatus.Approved, "Flu");
            Request(staff[Math.Min(3, staff.Count - 1)], annualType, 10, 4, LeaveStatus.Submitted, "Wedding");
            Request(staff[Math.Min(1, staff.Count - 1)], annualType, 30, 3, LeaveStatus.Submitted, "Long weekend");
            Request(staff[Math.Min(2, staff.Count - 1)], annualType, 3, 2, LeaveStatus.Rejected, "Personal");
            Request(staff[Math.Min(3, staff.Count - 1)], annualType, 60, 10, LeaveStatus.Draft, "Summer break");
        }

        // ---- documents with upcoming expiries ------------------------------------------------------------------------------
        if (!await db.Set<EmployeeDocument>().AnyAsync(ct))
        {
            for (var i = 0; i < staff.Count; i++)
            {
                var e = staff[i];
                db.Set<EmployeeDocument>().Add(new EmployeeDocument { LegalEntityId = legalEntityId, EmployeeId = e.Id, Type = EmployeeDocumentType.Passport, Number = $"P{7000000 + i * 137}", IssueDate = today.AddYears(-4), ExpiryDate = today.AddYears(6).AddDays(i * 11), IssuedBy = e.Nationality });
                db.Set<EmployeeDocument>().Add(new EmployeeDocument { LegalEntityId = legalEntityId, EmployeeId = e.Id, Type = EmployeeDocumentType.ResidenceId, Number = e.LabourCardNumber, IssueDate = today.AddYears(-1), ExpiryDate = today.AddDays(i == 0 ? 12 : i == 1 ? 40 : 200 + i * 30), IssuedBy = "Ministry of Interior" });
                if (i % 2 == 0) db.Set<EmployeeDocument>().Add(new EmployeeDocument { LegalEntityId = legalEntityId, EmployeeId = e.Id, Type = EmployeeDocumentType.Medical, IssueDate = today.AddMonths(-6), ExpiryDate = today.AddDays(i == 2 ? -5 : 150), Notes = "Annual medical fitness" });
            }
        }

        // ---- loans ----------------------------------------------------------------------------------------------------------
        if (!await db.Set<EmployeeLoan>().AnyAsync(ct) && staff.Count > 2)
        {
            db.Set<EmployeeLoan>().Add(new EmployeeLoan { LegalEntityId = legalEntityId, EmployeeId = staff[2].Id, Kind = LoanKind.Loan, Principal = 12000m, Instalments = 12, InstalmentAmount = 1000m, StartYear = today.Year, StartMonth = Math.Max(1, today.Month - 2), Remaining = 10000m, Reason = "Car repair", DisbursedOn = today.AddMonths(-3) });
            db.Set<EmployeeLoan>().Add(new EmployeeLoan { LegalEntityId = legalEntityId, EmployeeId = staff[3].Id, Kind = LoanKind.Advance, Principal = 3000m, Instalments = 3, InstalmentAmount = 1000m, StartYear = today.Year, StartMonth = today.Month, Remaining = 3000m, Reason = "Salary advance", DisbursedOn = today.AddDays(-10) });
        }

        // ---- statutory rules (examples a company can edit) -------------------------------------------------------------------
        if (!await db.Set<StatutoryRule>().AnyAsync(ct))
        {
            db.Set<StatutoryRule>().Add(new StatutoryRule { LegalEntityId = legalEntityId, Code = "PENSION-EE", Name = "Pension - employee share", Kind = StatutoryKind.EmployeeDeduction, Method = StatutoryMethod.Flat, Base = StatutoryBase.Basic, Rate = 5m, Nationalities = "Qatari", EffectiveFrom = new DateOnly(today.Year, 1, 1) });
            db.Set<StatutoryRule>().Add(new StatutoryRule { LegalEntityId = legalEntityId, Code = "PENSION-ER", Name = "Pension - employer share", Kind = StatutoryKind.EmployerCost, Method = StatutoryMethod.Flat, Base = StatutoryBase.Basic, Rate = 10m, Nationalities = "Qatari", EffectiveFrom = new DateOnly(today.Year, 1, 1) });
            var slab = new StatutoryRule { LegalEntityId = legalEntityId, Code = "WELFARE", Name = "Welfare contribution (slabs)", Kind = StatutoryKind.EmployeeDeduction, Method = StatutoryMethod.Slab, Base = StatutoryBase.Gross, Nationalities = "!Qatari" };
            slab.Slabs.Add(new StatutorySlab { FromAmount = 0, Rate = 0 });
            slab.Slabs.Add(new StatutorySlab { FromAmount = 10000, Rate = 1 });
            slab.Slabs.Add(new StatutorySlab { FromAmount = 30000, Rate = 2 });
            db.Set<StatutoryRule>().Add(slab);
        }

        // ---- daily attendance for the last 3 weeks, rostered cover, history -------------------------------------------------------
        if (!await db.Set<AttendanceLog>().AnyAsync(ct))
        {
            var rnd = new Random(42);
            for (var d = 21; d >= 1; d--)
            {
                var date = today.AddDays(-d);
                if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) continue; // weekend
                foreach (var e in staff.Where(x => x.JoinDate <= date))
                {
                    var roll = rnd.Next(100);
                    var inMin = 8 * 60 + (roll < 15 ? rnd.Next(12, 45) : rnd.Next(-10, 8));
                    var outMin = 17 * 60 + (roll > 80 ? rnd.Next(30, 120) : rnd.Next(-5, 15));
                    if (roll == 99) { db.Set<AttendanceLog>().Add(new AttendanceLog { LegalEntityId = legalEntityId, EmployeeId = e.Id, Date = date, Status = DailyAttendanceStatus.Absent, Source = "manual", Notes = "No show" }); continue; }
                    if (roll == 98) { db.Set<AttendanceLog>().Add(new AttendanceLog { LegalEntityId = legalEntityId, EmployeeId = e.Id, Date = date, Status = DailyAttendanceStatus.Leave, Source = "manual" }); continue; }
                    var ci = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(inMin)); var co = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(outMin));
                    var worked = (int)(co - ci).TotalMinutes;
                    var late = Math.Max(0, inMin - 8 * 60 - 10); if (late > 0) late += 10;
                    db.Set<AttendanceLog>().Add(new AttendanceLog
                    {
                        LegalEntityId = legalEntityId, EmployeeId = e.Id, Date = date, CheckIn = ci, CheckOut = co, Status = roll == 97 ? DailyAttendanceStatus.HalfDay : DailyAttendanceStatus.Present,
                        WorkedMinutes = worked, LateMinutes = late, OvertimeMinutes = Math.Max(0, worked - 480), Source = "gate-1",
                    });
                }
            }
        }
        if (!await db.Set<ShiftRoster>().AnyAsync(ct) && staff.Count > 3)
            db.Set<ShiftRoster>().Add(new ShiftRoster { LegalEntityId = legalEntityId, EmployeeId = staff[3].Id, ShiftId = shifts[1].Id, FromDate = today.AddDays(1), ToDate = today.AddDays(7) });
        if (!await db.Set<EmployeeHistoryEntry>().AnyAsync(ct))
        {
            foreach (var e in staff)
            {
                db.Set<EmployeeHistoryEntry>().Add(new EmployeeHistoryEntry { LegalEntityId = legalEntityId, EmployeeId = e.Id, EffectiveDate = e.JoinDate, Kind = HistoryKind.Joined, Summary = $"Joined as {e.Designation}" });
                if (e.JoinDate < today.AddYears(-1))
                    db.Set<EmployeeHistoryEntry>().Add(new EmployeeHistoryEntry { LegalEntityId = legalEntityId, EmployeeId = e.Id, EffectiveDate = today.AddMonths(-8), Kind = HistoryKind.Salary, Summary = "Annual increase", OldValue = (e.BasicSalary * 0.93m).ToString("0"), NewValue = e.BasicSalary.ToString("0"), IsPay = true });
            }
        }

        // ---- onboarding / offboarding checklists and tasks -----------------------------------------------------------------------
        if (!await db.Set<ChecklistTemplate>().AnyAsync(ct))
        {
            var on = new ChecklistTemplate { Name = "New joiner", Kind = ChecklistKind.Onboarding };
            string[] onSteps = ["Sign the contract", "Issue a laptop", "Create the email account", "Bank account for salary", "Orientation with the manager"];
            for (var i = 0; i < onSteps.Length; i++) on.Items.Add(new ChecklistTemplateItem { Position = i, Title = onSteps[i], DueOffsetDays = new[] { 0, 0, 1, 3, 7 }[i] });
            var off = new ChecklistTemplate { Name = "Leaver", Kind = ChecklistKind.Offboarding };
            string[] offSteps = ["Exit interview", "Return the laptop and badge", "Disable accounts", "Final settlement"];
            for (var i = 0; i < offSteps.Length; i++) off.Items.Add(new ChecklistTemplateItem { Position = i, Title = offSteps[i], DueOffsetDays = new[] { -5, -1, 0, 3 }[i] });
            db.Set<ChecklistTemplate>().AddRange(on, off);
        }
        if (!await db.Set<EmployeeTask>().AnyAsync(ct))
        {
            var newest = staff[^1];
            db.Set<EmployeeTask>().Add(new EmployeeTask { LegalEntityId = legalEntityId, EmployeeId = newest.Id, Kind = ChecklistKind.Onboarding, Title = "Sign the contract", DueDate = today.AddDays(-3), IsDone = true, DoneAt = DateTime.UtcNow.AddDays(-3) });
            db.Set<EmployeeTask>().Add(new EmployeeTask { LegalEntityId = legalEntityId, EmployeeId = newest.Id, Kind = ChecklistKind.Onboarding, Title = "Issue a laptop", DueDate = today.AddDays(-1) });
            db.Set<EmployeeTask>().Add(new EmployeeTask { LegalEntityId = legalEntityId, EmployeeId = newest.Id, Kind = ChecklistKind.Onboarding, Title = "Bank account for salary", DueDate = today.AddDays(4) });
        }

        // ---- recruitment ---------------------------------------------------------------------------------------------------------
        if (!await db.Set<JobOpening>().AnyAsync(ct))
        {
            var o1 = new JobOpening { Title = "Leasing Agent", DepartmentId = departments.First(d => d.Code == "SALES").Id, PositionId = positions.First(p => p.Code == "LA").Id, Headcount = 2, Status = OpeningStatus.Open, Description = "Show properties and close leases.", ClosingDate = today.AddDays(30) };
            var o2 = new JobOpening { Title = "Accountant", DepartmentId = departments.First(d => d.Code == "FIN").Id, PositionId = positions.First(p => p.Code == "ACC").Id, Headcount = 1, Status = OpeningStatus.OnHold, Description = "Accounts payable and reconciliations." };
            db.Set<JobOpening>().AddRange(o1, o2);
            await db.SaveChangesAsync(ct);
            var now = DateTime.UtcNow;
            db.Set<Candidate>().AddRange(
                new Candidate { OpeningId = o1.Id, FullName = "Noor Hassan", Email = "noor.h@example.com", Phone = "+974 5500 1001", Stage = CandidateStage.Interview, Source = "LinkedIn", ExpectedSalary = 9000m, StageChangedAt = now.AddDays(-2) },
                new Candidate { OpeningId = o1.Id, FullName = "Yusuf Karim", Email = "yusuf.k@example.com", Stage = CandidateStage.Screening, Source = "Referral", ExpectedSalary = 8500m, StageChangedAt = now.AddDays(-4) },
                new Candidate { OpeningId = o1.Id, FullName = "Mariam Saleh", Email = "mariam.s@example.com", Stage = CandidateStage.Offer, Source = "Website", ExpectedSalary = 10000m, StageChangedAt = now.AddDays(-1), Notes = "Strong references" },
                new Candidate { OpeningId = o1.Id, FullName = "Daniel Ortiz", Stage = CandidateStage.Rejected, Source = "Job board", StageChangedAt = now.AddDays(-6), Notes = "Salary expectation too high" },
                new Candidate { OpeningId = o2.Id, FullName = "Hana Aziz", Email = "hana.a@example.com", Stage = CandidateStage.Applied, Source = "Website", ExpectedSalary = 14000m, StageChangedAt = now });
        }
    }

    private static async Task SeedOthersAsync(AppDbContext db, Guid legalEntityId, CancellationToken ct)
    {
        var today = Today;

        // currency rates against the base currency of the workspace
        if (!await db.Set<ExchangeRate>().AnyAsync(ct))
        {
            var baseCurrency = await db.LegalEntities.IgnoreQueryFilters().Where(l => l.Id == legalEntityId).Select(l => l.BaseCurrency).FirstOrDefaultAsync(ct) ?? "QAR";
            (string Code, decimal PerBase)[] rates = [("USD", 0.2747m), ("EUR", 0.2530m), ("GBP", 0.2160m), ("AED", 1.0089m), ("SAR", 1.0302m), ("INR", 22.95m)];
            foreach (var (code, perBase) in rates.Where(r => r.Code != baseCurrency))
                for (var m = 0; m < 3; m++)
                    db.Set<ExchangeRate>().Add(new ExchangeRate { FromCurrency = baseCurrency, ToCurrency = code, RateDate = today.AddMonths(-m), Rate = Math.Round(perBase * (1m + (m * 0.004m)), 6) });
        }

        // customer / supplier groups
        if (!await db.Set<PartyGroup>().AnyAsync(ct))
            db.Set<PartyGroup>().AddRange(
                new PartyGroup { Code = "VIP", Name = "VIP customers" }, new PartyGroup { Code = "WHOLESALE", Name = "Wholesale" },
                new PartyGroup { Code = "RETAIL", Name = "Retail" }, new PartyGroup { Code = "LOCAL-SUP", Name = "Local suppliers" });

        // stock counts: one posted and one in progress, with lines from real items
        var items = await db.Set<Item>().OrderBy(i => i.Sku).Take(6).ToListAsync(ct);
        var warehouse = await db.Set<ERP.Modules.Inventory.Warehouse>().FirstOrDefaultAsync(ct);
        if (warehouse is not null && items.Count > 0 && !await db.Set<StockCount>().AnyAsync(ct))
        {
            var done = new StockCount { LegalEntityId = legalEntityId, Number = "SC-0001", WarehouseId = warehouse.Id, CountDate = today.AddDays(-30), Status = StockCountStatus.Posted, Memo = "Month-end count", PostedAt = DateTime.UtcNow.AddDays(-29) };
            var open = new StockCount { LegalEntityId = legalEntityId, Number = "SC-0002", WarehouseId = warehouse.Id, CountDate = today, Status = StockCountStatus.Counting, Memo = "Cycle count, aisle 3" };
            var n = 0;
            foreach (var it in items)
            {
                var sys = 20 + n * 7;
                done.Lines.Add(new StockCountLine { ItemId = it.Id, SystemQuantity = sys, CountedQuantity = n % 3 == 0 ? sys - 1 : sys });
                open.Lines.Add(new StockCountLine { ItemId = it.Id, SystemQuantity = sys, CountedQuantity = n < 3 ? sys : null });
                n++;
            }
            db.Set<StockCount>().AddRange(done, open);
        }

        // projects: milestones and timesheets
        var projects = await db.Set<Project>().OrderBy(p => p.Number).Take(3).ToListAsync(ct);
        if (projects.Count > 0 && !await db.Set<ProjectMilestone>().AnyAsync(ct))
            foreach (var (p, i) in projects.Select((p, i) => (p, i)))
            {
                db.Set<ProjectMilestone>().Add(new ProjectMilestone { ProjectId = p.Id, Title = "Kick-off and plan", DueDate = today.AddDays(-45 + i * 5), Amount = Math.Round(p.BudgetAmount * 0.2m, 2), Status = MilestoneStatus.Invoiced, AchievedDate = today.AddDays(-44 + i * 5) });
                db.Set<ProjectMilestone>().Add(new ProjectMilestone { ProjectId = p.Id, Title = "Phase 1 delivery", DueDate = today.AddDays(-5), Amount = Math.Round(p.BudgetAmount * 0.4m, 2), Status = MilestoneStatus.Achieved, AchievedDate = today.AddDays(-6) });
                db.Set<ProjectMilestone>().Add(new ProjectMilestone { ProjectId = p.Id, Title = "Final acceptance", DueDate = today.AddDays(40 + i * 10), Amount = Math.Round(p.BudgetAmount * 0.4m, 2), Status = MilestoneStatus.Pending });
            }
        if (projects.Count > 0 && !await db.Set<ProjectTimesheet>().AnyAsync(ct))
        {
            string[] workers = ["Sarah Jenkins", "Omar Farooq", "Layla Mansoor"];
            foreach (var p in projects)
                for (var d = 1; d <= 10; d++)
                {
                    var date = today.AddDays(-d);
                    if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) continue;
                    var hours = 4 + d % 5; var rate = 150m + (d % 3) * 25m;
                    db.Set<ProjectTimesheet>().Add(new ProjectTimesheet { ProjectId = p.Id, Date = date, WorkerName = workers[d % workers.Length], Hours = hours, HourlyRate = rate, TotalAmount = hours * rate, Description = "Project work", IsBillable = d % 4 != 0 });
                }
        }

        // POS cash drawer movements on the latest session
        var session = await db.Set<PosSession>().OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(ct);
        if (session is not null && !await db.Set<CashDrawerTransaction>().AnyAsync(ct))
        {
            var now = DateTimeOffset.UtcNow;
            db.Set<CashDrawerTransaction>().AddRange(
                new CashDrawerTransaction { SessionId = session.Id, Type = CashMovementType.FloatIn, Amount = 500m, Reason = "Opening float", PerformedByUserId = Guid.Empty, Timestamp = now.AddHours(-8) },
                new CashDrawerTransaction { SessionId = session.Id, Type = CashMovementType.PayOut, Amount = 45m, Reason = "Cleaning supplies", PerformedByUserId = Guid.Empty, Timestamp = now.AddHours(-5) },
                new CashDrawerTransaction { SessionId = session.Id, Type = CashMovementType.DropOut, Amount = 1200m, Reason = "Safe drop", PerformedByUserId = Guid.Empty, Timestamp = now.AddHours(-2) });
        }

        // retail price list lines
        var priceList = await db.Set<RetailPriceList>().OrderBy(p => p.Code).FirstOrDefaultAsync(ct);
        if (priceList is not null && items.Count > 0 && !await db.Set<RetailPriceListItem>().AnyAsync(ct))
            foreach (var it in items)
            {
                var basePrice = it.SalesPrice ?? 100m;
                db.Set<RetailPriceListItem>().Add(new RetailPriceListItem { PriceListId = priceList.Id, ItemId = it.Id, ItemCode = it.Sku, ItemName = it.Name, BasePrice = basePrice, Price = Math.Round(basePrice * 0.9m, 2) });
            }

        // van sales: what each trip loaded and sold
        var trips = await db.Set<VanSalesTrip>().OrderBy(t => t.TripNumber).Take(4).ToListAsync(ct);
        if (trips.Count > 0 && items.Count > 0 && !await db.Set<TripInventoryItem>().AnyAsync(ct))
            foreach (var t in trips)
                foreach (var it in items.Take(3))
                {
                    var scheduled = t.Status == TripStatus.Scheduled;
                    var loaded = 40m; var sold = scheduled ? 0m : 28m;
                    db.Set<TripInventoryItem>().Add(new TripInventoryItem { LegalEntityId = legalEntityId, TripId = t.Id, ItemId = it.Id, ItemCode = it.Sku, ItemName = it.Name, LoadedQuantity = loaded, SoldQuantity = sold, ReturnedQuantity = scheduled ? 0m : loaded - sold - 2m, DamagedQuantity = scheduled ? 0m : 2m, UnitPrice = it.SalesPrice ?? 100m });
                }

        // goods receipt lines from the purchase order lines each receipt belongs to
        if (!await db.Set<GoodsReceiptLine>().AnyAsync(ct))
        {
            var receipts = await db.Set<GoodsReceipt>().Take(10).ToListAsync(ct);
            foreach (var r in receipts)
            {
                var lines = await db.Set<PurchaseOrderLine>().Where(l => l.OrderId == r.PurchaseOrderId).Take(3).ToListAsync(ct);
                foreach (var l in lines)
                    db.Set<GoodsReceiptLine>().Add(new GoodsReceiptLine { GoodsReceiptId = r.Id, PurchaseOrderLineId = l.Id, ItemId = l.ItemId, ItemCode = l.ItemCode, Description = l.Description, QuantityReceived = l.ReceivedQuantity > 0 ? l.ReceivedQuantity : l.Quantity, UnitOfMeasure = l.UnitOfMeasure });
            }
        }
    }

    private static async Task SeedProjectsExtrasAsync(AppDbContext db, Guid legalEntityId, CancellationToken ct)
    {
        var today = Today;
        var projects = await db.Set<Project>().OrderBy(p => p.Number).Take(3).ToListAsync(ct);
        if (projects.Count == 0) return;
        var member = await db.Set<ERP.Domain.Identity.OrganizationMember>().OrderBy(m => m.JoinedAt).FirstOrDefaultAsync(ct);
        var user = member is null ? null : await db.Users.FirstOrDefaultAsync(u => u.Id == member.UserId, ct);

        // rate cards: a workspace default, a role rate and a project rate
        if (!await db.Set<ProjectRate>().AnyAsync(ct))
        {
            db.Set<ProjectRate>().AddRange(
                new ProjectRate { BillRate = 150m, CostRate = 80m },
                new ProjectRate { Role = "Senior consultant", BillRate = 220m, CostRate = 120m },
                new ProjectRate { Role = "Analyst", BillRate = 110m, CostRate = 60m },
                new ProjectRate { ProjectId = projects[0].Id, BillRate = 175m, CostRate = 85m });
            if (user is not null) db.Set<ProjectRate>().Add(new ProjectRate { UserId = user.Id, BillRate = 240m, CostRate = 130m });
        }

        // the time that was logged before cost rates existed gets a cost at 55 percent of the billing rate
        foreach (var t in await db.Set<ProjectTimesheet>().Where(t => t.CostAmount == 0m && t.TotalAmount > 0m).ToListAsync(ct))
        {
            t.CostRate = Math.Round(t.HourlyRate * 0.55m, 2); t.CostAmount = Math.Round(t.Hours * t.CostRate, 2);
        }

        // task dates, a sub-task and dependencies so the board, the Gantt chart and the critical path have something to show
        var tasks = await db.Set<ProjectTask>().Where(t => t.ProjectId == projects[0].Id).OrderBy(t => t.Number).ToListAsync(ct);
        if (tasks.Count >= 3 && !await db.Set<ProjectTaskDependency>().AnyAsync(ct))
        {
            var start = projects[0].StartDate ?? today.AddDays(-30);
            for (var i = 0; i < tasks.Count; i++) { tasks[i].StartDate ??= start.AddDays(i * 6); tasks[i].DueDate ??= start.AddDays(i * 6 + 8); if (tasks[i].EstimatedHours == 0) tasks[i].EstimatedHours = 16; }
            db.Set<ProjectTaskDependency>().AddRange(new ProjectTaskDependency { TaskId = tasks[1].Id, DependsOnTaskId = tasks[0].Id }, new ProjectTaskDependency { TaskId = tasks[2].Id, DependsOnTaskId = tasks[1].Id });
            tasks[0].Labels ??= "planning,client"; tasks[1].Labels ??= "build";
            if (user is not null) { tasks[0].AssignedUserId ??= user.Id; tasks[0].AssignedTo ??= user.FullName; }
        }
        if (tasks.Count >= 4 && !tasks.Any(t => t.ParentTaskId != null) && !await db.Set<ProjectTask>().AnyAsync(t => t.ParentTaskId != null, ct))
            tasks[3].ParentTaskId = tasks[0].Id;
        foreach (var t in await db.Set<ProjectTask>().ToListAsync(ct))
            t.ActualHours = await db.Set<ProjectTimesheet>().Where(x => x.TaskId == t.Id).Select(x => x.Hours).ToListAsync(ct) is { Count: > 0 } hours ? hours.Sum() : t.ActualHours;

        // expenses
        if (!await db.Set<ProjectExpense>().AnyAsync(ct))
            foreach (var (p, i) in projects.Select((p, i) => (p, i)))
            {
                db.Set<ProjectExpense>().Add(new ProjectExpense { ProjectId = p.Id, Date = today.AddDays(-12 - i), Category = "Travel", Description = "Site visit transport", Amount = 320m + i * 40, IsBillable = p.BillingType != ProjectBillingType.NonBillable, MarkupPercent = 10m, WorkerName = "Project team", Status = ExpenseStatus.Approved });
                db.Set<ProjectExpense>().Add(new ProjectExpense { ProjectId = p.Id, Date = today.AddDays(-5), Category = "Materials", Description = "Printing and survey kits", Amount = 780m, IsBillable = false, Status = ExpenseStatus.Approved, WorkerName = "Project team" });
                db.Set<ProjectExpense>().Add(new ProjectExpense { ProjectId = p.Id, Date = today.AddDays(-2), Category = "Subcontractor", Description = "Specialist day rate", Amount = 1500m, IsBillable = p.BillingType != ProjectBillingType.NonBillable, MarkupPercent = 15m, Status = i == 0 ? ExpenseStatus.Submitted : ExpenseStatus.Approved, WorkerName = "External" });
            }

        // comments, change order, risks, team and allocation
        if (!await db.Set<ProjectComment>().AnyAsync(ct))
        {
            db.Set<ProjectComment>().Add(new ProjectComment { OwnerType = "project", OwnerId = projects[0].Id, ProjectId = projects[0].Id, UserId = user?.Id, AuthorName = user?.FullName ?? "Project manager", Text = "Kick-off meeting held. Scope and milestones agreed with the client." });
            if (tasks.Count > 0) db.Set<ProjectComment>().Add(new ProjectComment { OwnerType = "task", OwnerId = tasks[0].Id, ProjectId = projects[0].Id, UserId = user?.Id, AuthorName = user?.FullName ?? "Project manager", Text = "Waiting for the client's floor plans before we can finish this." });
        }
        if (!await db.Set<ProjectChangeOrder>().AnyAsync(ct))
        {
            db.Set<ProjectChangeOrder>().Add(new ProjectChangeOrder { ProjectId = projects[0].Id, Number = $"CO-{projects[0].Number}-01", Title = "Extra reporting module", Description = "Client asked for a monthly management report.", AmountChange = 4500m, HoursChange = 30m, Status = ChangeOrderStatus.Approved, DecidedAt = DateTime.UtcNow.AddDays(-9), DecisionNote = "Approved by the client by email" });
            db.Set<ProjectChangeOrder>().Add(new ProjectChangeOrder { ProjectId = projects[0].Id, Number = $"CO-{projects[0].Number}-02", Title = "Second site", Description = "Roll out to the second branch.", AmountChange = 12000m, HoursChange = 80m, Status = ChangeOrderStatus.Draft });
        }
        if (!await db.Set<ProjectRisk>().AnyAsync(ct))
            db.Set<ProjectRisk>().AddRange(
                new ProjectRisk { ProjectId = projects[0].Id, Kind = RiskKind.Risk, Title = "Client sign-off may be late", Notes = "Approver is travelling in the second half of the month.", Severity = RiskSeverity.High, Status = RiskStatus.Open, OwnerName = "Project manager", DueDate = today.AddDays(10) },
                new ProjectRisk { ProjectId = projects[0].Id, Kind = RiskKind.Issue, Title = "Test environment unavailable", Severity = RiskSeverity.Medium, Status = RiskStatus.Mitigated, OwnerName = "IT" },
                new ProjectRisk { ProjectId = projects[Math.Min(1, projects.Count - 1)].Id, Kind = RiskKind.Risk, Title = "Key person leaves", Severity = RiskSeverity.Low, Status = RiskStatus.Open, DueDate = today.AddDays(-3) });
        if (user is not null && !await db.Set<ProjectMember>().AnyAsync(ct))
        {
            foreach (var p in projects) db.Set<ProjectMember>().Add(new ProjectMember { ProjectId = p.Id, UserId = user.Id, Name = user.FullName, Role = "Project manager" });
            db.Set<ProjectAllocation>().Add(new ProjectAllocation { ProjectId = projects[0].Id, UserId = user.Id, UserName = user.FullName, FromDate = today.AddDays(-30), ToDate = today.AddDays(60), HoursPerWeek = 24m });
            db.Set<ProjectAllocation>().Add(new ProjectAllocation { ProjectId = projects[Math.Min(1, projects.Count - 1)].Id, UserId = user.Id, UserName = user.FullName, FromDate = today.AddDays(-10), ToDate = today.AddDays(40), HoursPerWeek = 12m });
        }

        // one template to clone from
        if (!await db.Set<Project>().AnyAsync(p => p.IsTemplate, ct))
        {
            var template = new Project { LegalEntityId = legalEntityId, Number = "TPL-001", Name = "Standard implementation (template)", Description = "Discovery, build, test and hand-over.", Status = ProjectStatus.Planning, BillingType = ProjectBillingType.TimeAndMaterials, BudgetAmount = 60000m, BudgetHours = 400m, IsTemplate = true };
            db.Set<Project>().Add(template);
            await db.SaveChangesAsync(ct);
            string[] steps = ["Discovery workshop", "Solution design", "Build", "Testing", "Training and hand-over"];
            decimal[] hours = [24, 40, 120, 60, 24];
            for (var i = 0; i < steps.Length; i++)
                db.Set<ProjectTask>().Add(new ProjectTask { ProjectId = template.Id, Number = $"TPL-T{i + 1:00}", Title = steps[i], Status = ProjectTaskStatus.Todo, Priority = TaskPriority.Medium, EstimatedHours = hours[i] });
        }
    }
}
