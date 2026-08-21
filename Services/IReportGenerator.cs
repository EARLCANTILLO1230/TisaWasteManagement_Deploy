// Services/IReportGenerator.cs
//
// An "interface" just lists WHAT a service can do, without saying HOW.
// This lets ReportsController ask for "something that can generate
// reports" without needing to know it's specifically PDF/Excel code.
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Services
{
    public interface IReportGenerator
    {
        // Returns the finished file as raw bytes, ready to send to the browser.
        // format is either "PDF" or "Excel".
        byte[] GenerateCollectionReport(CollectionReportData data, string format);
        byte[] GenerateComplaintReport(ComplaintReportData data, string format);
    }
}
