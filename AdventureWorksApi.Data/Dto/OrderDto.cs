using AdventureWorksApi.Data.Models;

namespace AdventureWorksApi.Data.Dto;

public class OrderDto
{
    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    public string OrderNumber { get; set; } = null!;

    public string OrderStatus { get; set; } = null!;

    public IEnumerable<OrderLineItemDto> LineItems { get; set; } = null!;

    public Order ToOrder()
    {
        var order = new Order
        {
            CustomerId = CustomerId,
            OrderDate = OrderDate,
            OrderNumber = OrderNumber,
            OrderStatus = OrderStatus
        };

        var lineItems = LineItems.Select(li => new OrderLineItem
        {
            ProductId = li.ProductId,
            Quantity = li.Quantity,
            Price = li.Price,
            Order = order
        });

        order.LineItems = lineItems.ToList();

        return order;
    }
}