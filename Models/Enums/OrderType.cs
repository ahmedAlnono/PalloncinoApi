namespace Palloncino.Models.Enums;

public enum OrderType
{
    Regular = 1,     // Standard order from catalog/template
    Custom = 2,      // Custom request without design
    Design = 3       // Custom design request
}