﻿namespace AdventureWorksApi.Data.Models;

public class OrderLineItem
{
    public int OrderLineItemId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public Order Order { get; set; } = null!;
}