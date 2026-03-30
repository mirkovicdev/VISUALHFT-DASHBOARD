using VisualHFT.Enums;

public class LicenseManager
{
    private static LicenseManager _instance = new LicenseManager();
    public static LicenseManager Instance => _instance;

    public eLicenseLevel CurrentLicenseLevel { get; private set; } = eLicenseLevel.COMMUNITY;

    public bool HasAccess(eLicenseLevel required)
    {
        return CurrentLicenseLevel >= required;
    }

    // Later: this will be populated via API
    public void LoadFromKeygen()
    {
        CurrentLicenseLevel = eLicenseLevel.COMMUNITY;
    }
}

