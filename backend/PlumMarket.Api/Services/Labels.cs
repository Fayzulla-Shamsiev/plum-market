using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>Russian display names used in exported files.</summary>
public static class Labels
{
    public static string Status(OrderStatus s, DeliveryType d = DeliveryType.Delivery) => OrderFlow.Label(s, d);

    public static string Payment(PaymentMethod p) => p switch
    {
        PaymentMethod.Cash => "Наличные",
        PaymentMethod.CardToCard => "Перевод с карты",
        PaymentMethod.Click => "Click",
        PaymentMethod.Payme => "Payme",
        _ => p.ToString(),
    };

    public static string Delivery(DeliveryType d) => d == DeliveryType.Delivery ? "Доставка" : "Самовывоз";

    public static string Platform(Platform p) => p switch
    {
        Domain.Platform.Telegram => "Telegram",
        Domain.Platform.Website => "Веб-сайт",
        Domain.Platform.Instagram => "Instagram",
        _ => p.ToString(),
    };
}
