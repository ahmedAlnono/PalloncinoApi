namespace Palloncino.Models.Enums;

public enum ExecutionType
{
    PickupFromBranch = 1,        // Customer picks up from branch
    DeliveryOnly = 2,            // Deliver only (no installation)
    DeliveryWithInstallation = 3 // Deliver and install on site
}