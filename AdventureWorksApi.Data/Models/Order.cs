namespace AdventureWorksApi.Data.Models;

public class Order
{
    public int OrderHeaderId { get; set; }

    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    public string OrderNumber { get; set; } = null!;

    public string OrderStatus { get; set; } = null!;

    public ICollection<OrderLineItem> LineItems { get; set; } = null!;
}