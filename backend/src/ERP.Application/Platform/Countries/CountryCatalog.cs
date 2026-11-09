namespace ERP.Application.Platform;

/// <summary>Neutral ISO data for countries without a pack (name, usual currency, time zone). Country rules never live here.</summary>
public static class CountryCatalog
{
    public static IReadOnlyList<(string Code, string Name, string Currency, string TimeZone)> Generic { get; } =
    [
        ("AE", "United Arab Emirates", "AED", "Asia/Dubai"), ("SA", "Saudi Arabia", "SAR", "Asia/Riyadh"), ("QA", "Qatar", "QAR", "Asia/Qatar"),
        ("KW", "Kuwait", "KWD", "Asia/Kuwait"), ("BH", "Bahrain", "BHD", "Asia/Bahrain"), ("OM", "Oman", "OMR", "Asia/Muscat"),
        ("PK", "Pakistan", "PKR", "Asia/Karachi"), ("IN", "India", "INR", "Asia/Kolkata"), ("BD", "Bangladesh", "BDT", "Asia/Dhaka"),
        ("LK", "Sri Lanka", "LKR", "Asia/Colombo"), ("NP", "Nepal", "NPR", "Asia/Kathmandu"), ("EG", "Egypt", "EGP", "Africa/Cairo"),
        ("JO", "Jordan", "JOD", "Asia/Amman"), ("TR", "Turkey", "TRY", "Europe/Istanbul"), ("GB", "United Kingdom", "GBP", "Europe/London"),
        ("IE", "Ireland", "EUR", "Europe/Dublin"), ("FR", "France", "EUR", "Europe/Paris"), ("DE", "Germany", "EUR", "Europe/Berlin"),
        ("ES", "Spain", "EUR", "Europe/Madrid"), ("IT", "Italy", "EUR", "Europe/Rome"), ("NL", "Netherlands", "EUR", "Europe/Amsterdam"),
        ("US", "United States", "USD", "America/New_York"), ("CA", "Canada", "CAD", "America/Toronto"), ("AU", "Australia", "AUD", "Australia/Sydney"),
        ("SG", "Singapore", "SGD", "Asia/Singapore"), ("MY", "Malaysia", "MYR", "Asia/Kuala_Lumpur"), ("ZA", "South Africa", "ZAR", "Africa/Johannesburg"),
        ("NG", "Nigeria", "NGN", "Africa/Lagos"), ("KE", "Kenya", "KES", "Africa/Nairobi"),
    ];
}
