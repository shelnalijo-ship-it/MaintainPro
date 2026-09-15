using System.Globalization;
using ClosedXML.Excel;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Reporting;
using Microsoft.Extensions.Options;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace MaintainPro.Infrastructure.Reporting;

public sealed class ReportExportService : IReportExportService
{
    private readonly string applicationName;

    public ReportExportService(IOptions<ReportingOptions> options)
    {
        applicationName = string.IsNullOrWhiteSpace(options.Value.ApplicationName)
            ? "MaintainPro" : options.Value.ApplicationName.Trim();
        if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public ReportExportFile Export(ReportExportRequest request, ReportExportFormat format) =>
        format switch
        {
            ReportExportFormat.Pdf => Pdf(request),
            ReportExportFormat.Excel => Excel(request),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    private ReportExportFile Excel(ReportExportRequest request)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        sheet.Cell(1, 1).Value = applicationName;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(2, 1).Value = request.Table.Title;
        sheet.Cell(2, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Value = $"Generated {request.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC by {request.GeneratedBy}";
        sheet.Cell(4, 1).Value = request.Table.FilterSummary;
        var headerRow = 6;
        for (var column = 0; column < request.Table.Columns.Count; column++)
        {
            var cell = sheet.Cell(headerRow, column + 1);
            cell.Value = request.Table.Columns[column].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            cell.Style.Font.FontColor = XLColor.White;
        }
        for (var row = 0; row < request.Table.Rows.Count; row++)
        {
            for (var column = 0; column < request.Table.Columns.Count; column++)
            {
                var cell = sheet.Cell(headerRow + row + 1, column + 1);
                SetCell(cell, request.Table.Rows[row].ElementAtOrDefault(column));
                ApplyNumberFormat(cell, request.Table.Columns[column].Kind);
            }
        }
        if (request.Table.Columns.Count > 0)
        {
            var range = sheet.Range(headerRow, 1, headerRow + request.Table.Rows.Count,
                request.Table.Columns.Count);
            range.CreateTable("ReportData");
            sheet.SheetView.FreezeRows(headerRow);
            sheet.Columns(1, request.Table.Columns.Count).AdjustToContents();
            foreach (var column in sheet.Columns(1, request.Table.Columns.Count))
                if (column.Width > 50) column.Width = 50;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName(request.Table.FileStem, request.GeneratedAtUtc, "xlsx"), request.Table.Rows.Count);
    }

    private ReportExportFile Pdf(ReportExportRequest request)
    {
        using var document = new PdfDocument();
        document.Info.Title = request.Table.Title;
        document.Info.Author = applicationName;
        var titleFont = new XFont("Arial", 15, XFontStyleEx.Bold);
        var metadataFont = new XFont("Arial", 8, XFontStyleEx.Regular);
        var headerFont = new XFont("Arial", 7, XFontStyleEx.Bold);
        var rowFont = new XFont("Arial", 6.5, XFontStyleEx.Regular);
        const double margin = 24;
        const double rowHeight = 18;
        PdfPage? page = null;
        XGraphics? graphics = null;
        double y = 0;
        double columnWidth = 0;

        void NewPage()
        {
            graphics?.Dispose();
            page = document.AddPage();
            page.Orientation = PageOrientation.Landscape;
            page.Size = PageSize.A4;
            graphics = XGraphics.FromPdfPage(page);
            var usable = page.Width.Point - margin * 2;
            columnWidth = request.Table.Columns.Count == 0 ? usable : usable / request.Table.Columns.Count;
            y = margin;
            graphics.DrawString(applicationName, metadataFont, XBrushes.DarkSlateGray,
                new XRect(margin, y, usable, 12), XStringFormats.TopLeft);
            y += 14;
            graphics.DrawString(request.Table.Title, titleFont, XBrushes.Black,
                new XRect(margin, y, usable, 22), XStringFormats.TopLeft);
            y += 22;
            graphics.DrawString($"Generated {request.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC by {request.GeneratedBy}",
                metadataFont, XBrushes.Black, new XRect(margin, y, usable, 12), XStringFormats.TopLeft);
            y += 12;
            graphics.DrawString(Trim(request.Table.FilterSummary, 180), metadataFont, XBrushes.Black,
                new XRect(margin, y, usable, 20), XStringFormats.TopLeft);
            y += 22;
            DrawRow(graphics, request.Table.Columns.Select(x => x.Header).ToArray(), y,
                margin, columnWidth, rowHeight, headerFont, XBrushes.White, XBrushes.DarkBlue);
            y += rowHeight;
        }

        NewPage();
        foreach (var row in request.Table.Rows)
        {
            if (page is not null && y + rowHeight > page.Height.Point - margin - 18) NewPage();
            DrawRow(graphics!, row.Select(FormatValue).ToArray(), y, margin, columnWidth,
                rowHeight, rowFont, XBrushes.Black, XBrushes.White);
            y += rowHeight;
        }
        graphics?.Dispose();
        for (var index = 0; index < document.Pages.Count; index++)
        {
            using var footer = XGraphics.FromPdfPage(document.Pages[index], XGraphicsPdfPageOptions.Append);
            footer.DrawString($"Page {index + 1} of {document.Pages.Count}", metadataFont, XBrushes.Gray,
                new XRect(margin, document.Pages[index].Height.Point - margin, document.Pages[index].Width.Point - margin * 2, 12),
                XStringFormats.TopRight);
        }
        using var stream = new MemoryStream();
        document.Save(stream);
        return new(stream.ToArray(), "application/pdf",
            FileName(request.Table.FileStem, request.GeneratedAtUtc, "pdf"), request.Table.Rows.Count);
    }

    private static void DrawRow(XGraphics graphics, IReadOnlyList<string> values, double y,
        double margin, double columnWidth, double height, XFont font, XBrush text, XBrush fill)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var rectangle = new XRect(margin + columnWidth * index, y, columnWidth, height);
            graphics.DrawRectangle(XPens.LightGray, fill, rectangle);
            graphics.DrawString(Trim(values[index], 48), font, text,
                new XRect(rectangle.X + 2, rectangle.Y + 2, Math.Max(1, rectangle.Width - 4), rectangle.Height - 4),
                XStringFormats.TopLeft);
        }
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Clear(); break;
            case string text: cell.Value = text; break;
            case int number: cell.Value = number; break;
            case long number: cell.Value = number; break;
            case decimal number: cell.Value = number; break;
            case double number: cell.Value = number; break;
            case bool boolean: cell.Value = boolean; break;
            case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); break;
            case DateTime dateTime: cell.Value = dateTime; break;
            case Enum enumeration: cell.Value = enumeration.ToString(); break;
            default: cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
        }
    }

    private static void ApplyNumberFormat(IXLCell cell, ReportCellKind kind)
    {
        cell.Style.NumberFormat.Format = kind switch
        {
            ReportCellKind.Date => "yyyy-mm-dd",
            ReportCellKind.DateTime => "yyyy-mm-dd hh:mm:ss",
            ReportCellKind.Decimal => "0.00",
            ReportCellKind.Percentage => "0.00\"%\"",
            _ => "General"
        };
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    private static string FileName(string stem, DateTime generatedAt, string extension) =>
        $"{stem}-{generatedAt:yyyyMMdd-HHmmss}.{extension}";
}
