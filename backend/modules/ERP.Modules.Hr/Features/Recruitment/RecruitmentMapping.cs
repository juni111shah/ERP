using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Copies vacancy and candidate inputs onto their records (create and update).</summary>
internal static class RecruitmentMapping
{
    public static void Fill(JobOpening o, JobOpeningInput i)
    {
        o.Title = i.Title.Trim(); o.DepartmentId = i.DepartmentId; o.PositionId = i.PositionId; o.Headcount = i.Headcount; o.Status = i.Status; o.Description = i.Description?.Trim(); o.ClosingDate = i.ClosingDate;
    }

    public static void Fill(Candidate c, CandidateInput i)
    {
        c.FullName = i.FullName.Trim(); c.Email = string.IsNullOrWhiteSpace(i.Email) ? null : i.Email.Trim(); c.Phone = i.Phone?.Trim(); c.Source = i.Source?.Trim(); c.ExpectedSalary = i.ExpectedSalary; c.Notes = i.Notes?.Trim();
    }
}
