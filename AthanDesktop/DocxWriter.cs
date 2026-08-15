using System.IO;
using System.IO.Compression;
using System.Text;

namespace AthanDesktop;

/// <summary>
/// Writes a minimal Word document by hand. A .docx is a zip of OpenXML parts,
/// and the three parts below are all Word needs to open one - which is cheaper
/// than taking a dependency on DocumentFormat.OpenXml just to lay out a table,
/// in an app whose whole selling point is being one modest download.
/// </summary>
public static class DocxWriter
{
    /// <summary>Twips per inch, the unit every measurement below is in.</summary>
    private const int Twips = 1440;

    public sealed class Cell(string text)
    {
        public string Text { get; } = text;
        public bool Bold { get; init; }
        /// <summary>Six hex digits, or null for no shading.</summary>
        public string? Fill { get; init; }
        public string? Colour { get; init; }
        public bool Centre { get; init; }
    }

    /// <summary>
    /// [Content_Types].xml goes in first: the convention every consumer relies
    /// on to find the part map without scanning the archive.
    /// </summary>
    public static void Write(string path, string documentXml)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        Add(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """);

        Add(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
            </Relationships>
            """);

        Add(zip, "word/document.xml", documentXml);
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    // ---- building blocks ---------------------------------------------------

    public static string Document(string body, int pageWidth, int pageHeight, int margin) =>
        $"""
         <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
         <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
           <w:body>
         {body}
             <w:sectPr>
               <w:pgSz w:w="{pageWidth}" w:h="{pageHeight}"/>
               <w:pgMar w:top="{margin}" w:right="{margin}" w:bottom="{margin}" w:left="{margin}"
                        w:header="0" w:footer="0" w:gutter="0"/>
             </w:sectPr>
           </w:body>
         </w:document>
         """;

    /// <summary>Letter portrait, which is what a fridge-door timetable wants.</summary>
    public static (int Width, int Height) Letter => (Twips * 17 / 2, Twips * 11);

    public static string Paragraph(string text, int halfPointSize, bool bold = false,
        string? colour = null, int spaceAfter = 0, bool centre = false)
    {
        var justify = centre ? "<w:jc w:val=\"center\"/>" : "";
        return $"""
                <w:p>
                  <w:pPr><w:spacing w:before="0" w:after="{spaceAfter}" w:line="240" w:lineRule="auto"/>{justify}</w:pPr>
                  {Run(text, halfPointSize, bold, colour)}
                </w:p>
                """;
    }

    private static string Run(string text, int halfPointSize, bool bold, string? colour)
    {
        var b = bold ? "<w:b/>" : "";
        var c = colour is null ? "" : $"<w:color w:val=\"{colour}\"/>";
        return $"""
                <w:r>
                  <w:rPr><w:rFonts w:ascii="Segoe UI" w:hAnsi="Segoe UI" w:cs="Segoe UI"/>{b}{c}<w:sz w:val="{halfPointSize}"/><w:szCs w:val="{halfPointSize}"/></w:rPr>
                  <w:t xml:space="preserve">{Escape(text)}</w:t>
                </w:r>
                """;
    }

    public static string Table(IReadOnlyList<int> columnWidths, IEnumerable<IReadOnlyList<Cell>> rows,
        int halfPointSize, int rowHeight)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<w:tbl><w:tblPr>");
        sb.AppendLine("""
              <w:tblW w:w="0" w:type="auto"/>
              <w:tblLayout w:type="fixed"/>
              <w:tblBorders>
                <w:top w:val="single" w:sz="4" w:color="D6E2DB"/>
                <w:left w:val="single" w:sz="4" w:color="D6E2DB"/>
                <w:bottom w:val="single" w:sz="4" w:color="D6E2DB"/>
                <w:right w:val="single" w:sz="4" w:color="D6E2DB"/>
                <w:insideH w:val="single" w:sz="4" w:color="D6E2DB"/>
                <w:insideV w:val="single" w:sz="4" w:color="D6E2DB"/>
              </w:tblBorders>
              <w:tblCellMar>
                <w:top w:w="20" w:type="dxa"/><w:left w:w="70" w:type="dxa"/>
                <w:bottom w:w="20" w:type="dxa"/><w:right w:w="70" w:type="dxa"/>
              </w:tblCellMar>
            """);
        sb.AppendLine("</w:tblPr><w:tblGrid>");
        foreach (var w in columnWidths) sb.AppendLine($"<w:gridCol w:w=\"{w}\"/>");
        sb.AppendLine("</w:tblGrid>");

        foreach (var row in rows)
        {
            // "atLeast" rather than "exact": a row must never clip its text, and
            // the height only exists to keep 30 days on one page.
            sb.AppendLine($"<w:tr><w:trPr><w:trHeight w:val=\"{rowHeight}\" w:hRule=\"atLeast\"/><w:cantSplit/></w:trPr>");
            for (var i = 0; i < row.Count; i++)
            {
                var cell = row[i];
                var width = i < columnWidths.Count ? columnWidths[i] : columnWidths[^1];
                var shade = cell.Fill is null
                    ? ""
                    : $"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{cell.Fill}\"/>";
                var justify = cell.Centre ? "<w:jc w:val=\"center\"/>" : "";
                sb.AppendLine($"""
                    <w:tc>
                      <w:tcPr><w:tcW w:w="{width}" w:type="dxa"/>{shade}<w:vAlign w:val="center"/></w:tcPr>
                      <w:p>
                        <w:pPr><w:spacing w:before="0" w:after="0" w:line="240" w:lineRule="auto"/>{justify}</w:pPr>
                        {Run(cell.Text, halfPointSize, cell.Bold, cell.Colour)}
                      </w:p>
                    </w:tc>
                    """);
            }
            sb.AppendLine("</w:tr>");
        }

        sb.AppendLine("</w:tbl>");
        return sb.ToString();
    }

    private static string Escape(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
