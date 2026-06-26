namespace HIAS_NET_CORE.Common;

// Trimmed down from the original HIAS ConfigHelpers — only the constants the
// Fleet controllers actually reference (role-code check in FleetDevicesController.SeedDevice).
public static class ConfigHelpers
{
    public static int RoleCodeSuperAdmin = 1;
    public static int RoleCodeAdmin = 10;
}
