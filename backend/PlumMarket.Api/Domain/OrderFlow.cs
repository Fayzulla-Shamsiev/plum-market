namespace PlumMarket.Api.Domain;

/// <summary>
/// The one place that knows how an order moves. The admin advances an order one step at a time (the spec's
/// "последовательно"); the storefront shows the same steps to the customer.
/// </summary>
public static class OrderFlow
{
    static readonly OrderStatus[] DeliverySteps =
    [
        OrderStatus.New, OrderStatus.Assembling, OrderStatus.Ready, OrderStatus.HandedToCourier,
        OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Completed,
    ];
    static readonly OrderStatus[] PickupSteps = [OrderStatus.New, OrderStatus.Assembling, OrderStatus.Ready, OrderStatus.Completed];

    public static IReadOnlyList<OrderStatus> Steps(DeliveryType d) => d == DeliveryType.Pickup ? PickupSteps : DeliverySteps;

    /// <summary>Not yet finished (still in the store's queue or on the way).</summary>
    public static readonly OrderStatus[] Active =
    [
        OrderStatus.New, OrderStatus.Assembling, OrderStatus.Ready, OrderStatus.HandedToCourier,
        OrderStatus.OnTheWay, OrderStatus.Delivered,
    ];

    public static bool IsFinal(OrderStatus s) => s is OrderStatus.Completed or OrderStatus.Cancelled;

    /// <summary>The single next step, or null when the order is finished.</summary>
    public static OrderStatus? Next(Order o)
    {
        var steps = Steps(o.DeliveryType);
        var i = Array.IndexOf(steps.ToArray(), o.Status);
        return i >= 0 && i < steps.Count - 1 ? steps[i + 1] : null;
    }

    /// <summary>The store can cancel until the order has been delivered / handed over.</summary>
    public static bool StoreCanCancel(OrderStatus s) => s is OrderStatus.New or OrderStatus.Assembling or OrderStatus.Ready
        or OrderStatus.HandedToCourier or OrderStatus.OnTheWay;

    /// <summary>The customer can cancel only until the store confirms the order (spec: "в статусе ожидания").</summary>
    public static bool CustomerCanCancel(OrderStatus s) => s == OrderStatus.New;

    /// <summary>Waiting too long before it's ready: still New or being assembled after the store's limit.</summary>
    public static bool IsOverdue(Order o, int overdueMinutes, DateTime now) =>
        o.Status is OrderStatus.New or OrderStatus.Assembling && (now - o.CreatedAt).TotalMinutes > overdueMinutes;

    public static string Label(OrderStatus s, DeliveryType d) => s switch
    {
        OrderStatus.New => "Новый",
        OrderStatus.Assembling => "В сборке",
        OrderStatus.Ready => d == DeliveryType.Pickup ? "Готов к выдаче" : "Готов к отправке",
        OrderStatus.HandedToCourier => "Передан в доставку",
        OrderStatus.OnTheWay => "В пути",
        OrderStatus.Delivered => "Доставлен",
        OrderStatus.Completed => "Завершён",
        OrderStatus.Cancelled => "Отменён",
        _ => s.ToString(),
    };
}
