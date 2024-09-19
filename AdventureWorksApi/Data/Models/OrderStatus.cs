﻿namespace AdventureWorksApi.Data.Models;

public class OrderStatus
{
    public string OrderNumber { get; set; } = null!;

    public int CustomerId { get; set; }

    public string Status { get; set; } = null!;
}
