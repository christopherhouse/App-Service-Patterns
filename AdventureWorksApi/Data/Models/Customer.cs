namespace AdventureWorksApi.Data.Models;

public class Customer
{
    public int CustomerID { get; set; }

    public bool NameStyle { get; set; }

    public string? Title { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string? MiddleName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? Suffix { get; set; } = null!;

    public string? CompanyName { get; set; } = null!;

    public string? SalesPerson { get; set; } = null!;

    public string? EmailAddress { get; set; } = null!;

    public string? Phone { get; set; } = null!;

    public DateTime ModifiedDate { get; set; }
}
