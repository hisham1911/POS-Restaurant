namespace KasserPro.Application.DTOs.Customers;

public class GetOrCreateCustomerResult
{
    public CustomerDto Customer { get; set; } = new();
    public bool WasCreated { get; set; }
}
