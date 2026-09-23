using ClosedXML.Excel;
using PlumMarket.Api.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PlumMarket.Api.Services;

/// <summary>Builds the orders export (Excel) and the assembly/picking sheet (PDF or Excel).</summary>
public static class OrderDocuments
{
    const string DateFmt = "dd.MM.yyyy HH:mm";

    public static byte[] ExportExcel(IReadOnlyList<Order> orders)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Заказы");
        string[] headers = ["ID", "Дата", "Клиент", "Телефон", "Статус", "Получатель", "Промокод", "Тип доставки",
            "Филиал", "Комментарий", "Адрес", "Товары", "Сумма товаров", "Доставка", "Итого", "Себестоимость"];
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        foreach (var o in orders)
        {
            ws.Cell(row, 1).Value = o.Id;
            ws.Cell(row, 2).Value = o.CreatedAt;
            ws.Cell(row, 2).Style.DateFormat.Format = DateFmt;
            ws.Cell(row, 3).Value = o.Customer.FullName;
            ws.Cell(row, 4).Value = o.Customer.Phone;
            ws.Cell(row, 5).Value = Labels.Status(o.Status, o.DeliveryType);
            ws.Cell(row, 6).Value = o.RecipientName is null ? "" : $"{o.RecipientName}, {o.RecipientPhone}";
            ws.Cell(row, 7).Value = o.PromoCode ?? "";
            ws.Cell(row, 8).Value = Labels.Delivery(o.DeliveryType);
            ws.Cell(row, 9).Value = o.Branch.Name;
            ws.Cell(row, 10).Value = o.Comment ?? "";
            ws.Cell(row, 11).Value = o.Address ?? "";
            ws.Cell(row, 12).Value = string.Join("; ", o.Items.Select(i => $"{i.ProductName} × {i.Quantity}"));
            ws.Cell(row, 13).Value = o.Subtotal;
            ws.Cell(row, 14).Value = o.DeliveryCost;
            ws.Cell(row, 15).Value = o.Total;
            ws.Cell(row, 16).Value = o.CostTotal;
            row++;
        }
        ws.Range(2, 13, Math.Max(2, row - 1), 16).Style.NumberFormat.Format = "#,##0";
        StyleTable(ws, headers.Length, row - 1);
        return Save(wb);
    }

    public static byte[] AssemblyExcel(IReadOnlyList<Order> orders, string mode)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Лист сборки");
        if (mode == "products")
        {
            string[] headers = ["Товар", "Количество", "Заказов", "Номера заказов"];
            for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            var row = 2;
            foreach (var p in ByProduct(orders))
            {
                ws.Cell(row, 1).Value = p.Name;
                ws.Cell(row, 2).Value = p.Quantity;
                ws.Cell(row, 3).Value = p.OrderIds.Count;
                ws.Cell(row, 4).Value = string.Join(", ", p.OrderIds.Select(id => "#" + id));
                row++;
            }
            StyleTable(ws, headers.Length, row - 1);
        }
        else
        {
            string[] headers = ["Заказ", "Время", "Клиент", "Телефон", "Тип", "Адрес / филиал", "Товар", "Кол-во", "Комментарий", "✓"];
            for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            var row = 2;
            foreach (var o in orders)
            {
                var first = row;
                foreach (var item in o.Items)
                {
                    ws.Cell(row, 7).Value = item.ProductName;
                    ws.Cell(row, 8).Value = item.Quantity;
                    row++;
                }
                ws.Cell(first, 1).Value = "#" + o.Id;
                ws.Cell(first, 2).Value = o.CreatedAt.ToString(DateFmt);
                ws.Cell(first, 3).Value = o.Customer.FullName;
                ws.Cell(first, 4).Value = o.Customer.Phone;
                ws.Cell(first, 5).Value = Labels.Delivery(o.DeliveryType);
                ws.Cell(first, 6).Value = o.DeliveryType == DeliveryType.Delivery ? o.Address : o.Branch.Name;
                ws.Cell(first, 9).Value = o.Comment ?? "";
                ws.Range(first, 1, row - 1, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            StyleTable(ws, headers.Length, row - 1);
        }
        return Save(wb);
    }

    public static byte[] AssemblyPdf(IReadOnlyList<Order> orders, string mode, string storeName, string period)
    {
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(t => t.FontSize(9));
            page.Header().Column(c =>
            {
                c.Item().Text($"Лист сборки — {storeName}").FontSize(16).Bold();
                c.Item().Text($"{period} · {(mode == "products" ? "по товарам" : "по заказам")} · заказов: {orders.Count}")
                    .FontColor(Colors.Grey.Darken1);
                c.Item().PaddingBottom(8);
            });
            page.Content().Element(content =>
            {
                if (mode == "products") ProductTable(content, orders);
                else OrderBlocks(content, orders);
            });
            page.Footer().AlignRight().Text(t =>
            {
                t.Span("Стр. ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        })).GeneratePdf();
    }

    static void ProductTable(IContainer container, IReadOnlyList<Order> orders)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(24);
                c.RelativeColumn(4);
                c.ConstantColumn(60);
                c.RelativeColumn(4);
            });
            table.Header(h =>
            {
                foreach (var title in new[] { "", "Товар", "Кол-во", "Заказы" })
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(title).Bold();
            });
            foreach (var p in ByProduct(orders))
            {
                table.Cell().BorderBottom(0.5f).Padding(4).Element(CheckBox);
                table.Cell().BorderBottom(0.5f).Padding(4).Text(p.Name);
                table.Cell().BorderBottom(0.5f).Padding(4).Text(p.Quantity.ToString()).Bold();
                table.Cell().BorderBottom(0.5f).Padding(4).Text(string.Join(", ", p.OrderIds.Select(id => "#" + id)))
                    .FontColor(Colors.Grey.Darken2);
            }
        });
    }

    static void OrderBlocks(IContainer container, IReadOnlyList<Order> orders)
    {
        container.Column(col =>
        {
            col.Spacing(8);
            foreach (var o in orders)
            {
                col.Item().Border(0.75f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"#{o.Id} · {o.CreatedAt.ToString(DateFmt)}").Bold().FontSize(11);
                        r.AutoItem().Text($"{Labels.Delivery(o.DeliveryType)} · {Labels.Status(o.Status, o.DeliveryType)}");
                    });
                    c.Item().Text(o.RecipientName is null ? $"{o.Customer.FullName}, {o.Customer.Phone}" : $"{o.RecipientName}, {o.RecipientPhone}");
                    c.Item().Text(o.DeliveryType == DeliveryType.Delivery ? $"Адрес: {o.Address}" : $"Самовывоз: филиал {o.Branch.Name}")
                        .FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrEmpty(o.Comment)) c.Item().Text($"Комментарий: {o.Comment}").Italic();
                    c.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(18);
                            cd.RelativeColumn();
                            cd.ConstantColumn(40);
                        });
                        foreach (var i in o.Items)
                        {
                            t.Cell().PaddingTop(1).Element(CheckBox);
                            t.Cell().Text(i.ProductName);
                            t.Cell().AlignRight().Text($"× {i.Quantity}").Bold();
                        }
                    });
                    c.Item().AlignRight().Text($"Итого: {OrderWorkflow.Money(o.Total)}").Bold();
                });
            }
        });
    }

    static void CheckBox(IContainer c) => c.Width(9).Height(9).Border(0.75f);

    record ProductLine(string Name, int Quantity, List<int> OrderIds);

    static IEnumerable<ProductLine> ByProduct(IEnumerable<Order> orders) => orders
        .SelectMany(o => o.Items.Select(i => (Order: o, Item: i)))
        .GroupBy(x => x.Item.ProductName)
        .Select(g => new ProductLine(g.Key, g.Sum(x => x.Item.Quantity), g.Select(x => x.Order.Id).Distinct().ToList()))
        .OrderByDescending(p => p.Quantity);

    static void StyleTable(IXLWorksheet ws, int columns, int lastRow)
    {
        var header = ws.Range(1, 1, 1, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDE7F6");
        ws.SheetView.FreezeRows(1);
        if (lastRow > 1) ws.Range(1, 1, lastRow, columns).SetAutoFilter();
        ws.Columns().AdjustToContents(1, Math.Min(lastRow, 300), 8, 60);
    }

    static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
