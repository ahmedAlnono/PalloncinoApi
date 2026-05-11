namespace Palloncino.Models.Enums;

public enum ChecklistPhase
{
    LoadingFromBranch = 1,   // Loading items from branch to vehicle
    DeliveryToCustomer = 2,   // Delivering to customer location
    ReturnRental = 3,         // Returning rental items from customer
    Installation = 4,         // On-site installation (if applicable)
    Pickup = 5                // Customer picking up from branch
}