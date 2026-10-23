using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace ERP.Application.Common.Services;

public interface ICsvExportService
{
    byte[] ExportToCsv<T>(IEnumerable<T> records, string[]? columnHeaders = null);
    Stream ExportToCsvStream<T>(IEnumerable<T> records, string[]? columnHeaders = null);
}

public class CsvExportService : ICsvExportService
{
    public byte[] ExportToCsv<T>(IEnumerable<T> records, string[]? columnHeaders = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        }))
        {
            if (columnHeaders != null && columnHeaders.Length > 0)
            {
                foreach (var header in columnHeaders)
                {
                    csv.WriteField(header);
                }
                csv.NextRecord();

                foreach (var record in records)
                {
                    var properties = typeof(T).GetProperties();
                    foreach (var prop in properties.Take(columnHeaders.Length))
                    {
                        var value = prop.GetValue(record);
                        csv.WriteField(value);
                    }
                    csv.NextRecord();
                }
            }
            else
            {
                csv.WriteRecords(records);
            }
        }

        return stream.ToArray();
    }

    public Stream ExportToCsvStream<T>(IEnumerable<T> records, string[]? columnHeaders = null)
    {
        var bytes = ExportToCsv(records, columnHeaders);
        return new MemoryStream(bytes);
    }
}
