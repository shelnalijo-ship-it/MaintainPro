using MaintainPro.Application.Reporting;

namespace MaintainPro.Application.Abstractions;

public interface IReportExportService
{
    ReportExportFile Export(ReportExportRequest request, ReportExportFormat format);
}
