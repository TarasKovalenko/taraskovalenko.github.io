namespace SpecificationExample.Domain;

public sealed class Order
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public required Customer Customer { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal Total { get; set; }

    public OrderStatus Status { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    public List<Payment> Payments { get; set; } = [];
}

public sealed class Customer
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public List<Order> Orders { get; set; } = [];
}

public sealed class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public required string ProductName { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

public sealed class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    Paid,
    Cancelled
}
