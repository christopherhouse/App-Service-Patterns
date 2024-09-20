namespace AdventureWorksApi.Data.Dto;

public class OrderLineItemDto
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}