namespace AdventureWorksApi.Data.Models;

public class OrderLineItem
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}