using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Configuration;
using System.Text;
using Text = DocumentFormat.OpenXml.Spreadsheet.Text;

namespace FIleScannerTool.Documents
{
    internal class OpenOfficeContentExtractor
    {
        private bool _showTrace;

        public OpenOfficeContentExtractor(IConfiguration configuration)
        {
            bool.TryParse(configuration["ShowTrace"], out _showTrace);
        }

        private void WriteTrace(string message)
        {
            if (_showTrace)
            {
                Console.WriteLine($"{DateTime.Now} OpenOfficeContentExtractor: {message}");
            }
        }

        public byte[] ExtractTextFromDocxBytes(byte[] fileBytes)
        {
            StringBuilder text = new StringBuilder();

            try
            {
                using (MemoryStream stream = new MemoryStream(fileBytes))
                using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
                {
                    if (doc.MainDocumentPart != null && doc.MainDocumentPart.Document != null)
                    {
                        var innerText = doc.MainDocumentPart.Document.Body.InnerText;
                        text.Append(innerText);
                    }
                }

            }
            catch (Exception ex)
            {
                WriteTrace($"Docx extraction exception: {ex.Message}");
            }

            return ConvertStringToByteArray(text.ToString());
        }

        public byte[] ExtractTextFromXlsxBytes(byte[] fileBytes)
        {
            StringBuilder text = new StringBuilder();

            try
            {
                using (MemoryStream stream = new MemoryStream(fileBytes))
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(stream, false))
                {
                    WorkbookPart workbookPart = document.WorkbookPart;
                    Sheet sheet = workbookPart.Workbook.Sheets.GetFirstChild<Sheet>();
                    WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                    SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

                    foreach (Row row in sheetData.Elements<Row>())
                    {
                        foreach (Cell cell in row.Elements<Cell>())
                        {
                            if (cell.CellValue != null)
                            {
                                string cellValue = cell.CellValue.InnerText;
                                if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
                                {
                                    SharedStringTablePart stringTablePart = workbookPart.SharedStringTablePart;
                                    cellValue = stringTablePart.SharedStringTable.Elements<SharedStringItem>().ElementAt(int.Parse(cellValue)).InnerText;
                                }
                                text.Append(cellValue + " ");
                            }
                        }
                        text.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteTrace($"Xlsx extraction exception: {ex.Message}");
            }

            return ConvertStringToByteArray(text.ToString());
        }

        public static byte[] ConvertStringToByteArray(string text)
        {
            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(text);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
