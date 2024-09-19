namespace AdventureWorksApi.Data.Models;

public class Order
{
    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    public string OrderNumber { get; set; }

    public string OrderStatus { get; set; }

    public IEnumerable<OrderLineItem> LineItems { get; set; }
}

public class OrderLineItem
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}
