using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

class Program
{
    static void Main()
    {
        string jsonPath = "input.json"; // Path to your Quill JSON
        string outputPdfPath = "output.pdf";

        try
        {
            // Read the JSON file
            string jsonContent = File.ReadAllText(jsonPath);
            QuillDocument quillDoc = JsonConvert.DeserializeObject<QuillDocument>(jsonContent);

            // Create a PDF
            using (PdfWriter writer = new PdfWriter(outputPdfPath))
            {
                using (PdfDocument pdf = new PdfDocument(writer))
                {
                    Document document = new Document(pdf);

                    // Iterate over Quill content
                    foreach (var block in quillDoc.Content)
                    {
                        if (block.Insert is string text)
                        {
                            Paragraph p = new Paragraph(text);
                            ApplyFormatting(p, block.Attributes);
                            document.Add(p);
                        }
                        else if (block.Insert is JObject obj)
                        {
                            string extractedText = ExtractTextFromNonTextElement(obj);
                            if (!string.IsNullOrEmpty(extractedText))
                            {
                                Paragraph p = new Paragraph(extractedText).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE));
                                document.Add(p);
                            }
                        }
                        else
                        {
                            Console.WriteLine("❌ Unsupported Block Type: " + block.Insert.GetType());
                        }
                    }

                    document.Close();
                }
            }

            Console.WriteLine("✅ PDF Generated Successfully: " + outputPdfPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error: " + ex.Message);
        }
    }

    static void ApplyFormatting(Paragraph p, Attributes attributes)
    {
        if (attributes == null) return;

        // Font Styles
        if (attributes.Bold) p.SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD));
        if (attributes.Italic) p.SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE));
        if (attributes.Underline) p.SetUnderline();
        if (attributes.Size > 0) p.SetFontSize(attributes.Size);
        if (!string.IsNullOrEmpty(attributes.Background)) p.SetBackgroundColor(ConvertColor(attributes.Background));

        // Alignment
        switch (attributes.Align)
        {
            case "center": p.SetTextAlignment(TextAlignment.CENTER); break;
            case "right": p.SetTextAlignment(TextAlignment.RIGHT); break;
            case "justify": p.SetTextAlignment(TextAlignment.JUSTIFIED); break;
            default: p.SetTextAlignment(TextAlignment.LEFT); break;
        }
    }

    static string ExtractTextFromNonTextElement(JObject obj)
    {
        if (obj.ContainsKey("notes"))
            return obj["notes"]["entity"]["text"]?.ToString() ?? "";

        if (obj.ContainsKey("bookmarks"))
            return obj["bookmarks"]["entity"]["text"]?.ToString() ?? "";

        if (obj.ContainsKey("highlights"))
            return obj["highlights"]["entity"]["selected"]?.ToString() ?? "";

        if (obj.ContainsKey("verse"))
            return "[Verse: " + obj["verse"]["id"]?.ToString() + "]";

        return "";
    }

    static DeviceRgb ConvertColor(string hexColor)
    {
        if (hexColor.StartsWith("#"))
            hexColor = hexColor.Substring(1);

        int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
        int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
        int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);

        return new DeviceRgb(r, g, b);
    }
}

// Classes for JSON Parsing
class QuillDocument
{
    public List<QuillBlock> Content { get; set; }
}

class QuillBlock
{
    public object Insert { get; set; }
    public Attributes Attributes { get; set; }
}

class Attributes
{
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public float Size { get; set; }
    public string Background { get; set; }
    public string Align { get; set; }
}
