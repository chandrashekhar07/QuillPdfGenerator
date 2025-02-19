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
using iText.IO.Image;
using iText.Kernel.Pdf.Action;

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
            QuillDocument quillDoc = JsonConvert.DeserializeObject<QuillDocument>(jsonContent) ?? new QuillDocument { Content = [] };

            // Create a PDF
            using (PdfWriter writer = new PdfWriter(outputPdfPath))
            {
                using PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf);


                // Add Title
                document.Add(new Paragraph("Title " + quillDoc.Title)
                    .SetFontSize(18)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                // Add Presenter
                document.Add(new Paragraph("Presenter: " + quillDoc.Presenter)
                    .SetFontSize(14)
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                // Add Summary
                document.Add(new Paragraph("Summary: " + quillDoc.Summary)
                    .SetFontSize(12));

                // Add a new line
                document.Add(new Paragraph());

                Paragraph currentParagraph = new Paragraph();

                // Iterate over Quill content
                foreach (var block in quillDoc.Content)
                {
                    if (block.Insert is string text)
                    {
                        string[] parts = text.Split('\n');

                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(parts[i]))
                            {
                                currentParagraph.Add(parts[i]);
                                ApplyFormatting(currentParagraph, block.Attributes);
                            }

                            // If there was a newline
                            if (i < parts.Length - 1 || parts.Length == 1)
                            {

                                // Apply alignment to the current paragraph before adding it to the document
                                if (block.Attributes != null && !string.IsNullOrEmpty(block.Attributes.Align))
                                {
                                    ApplyAlignment(currentParagraph, block.Attributes.Align);
                                }

                                document.Add(currentParagraph);
                                currentParagraph = new Paragraph();
                            }

                        }
                    }


                    else if (block.Insert is JObject obj)
                    {
                        var (extractedText, paragraphCode, icon) = ExtractTextFromNonTextElement(obj);
                        if (!string.IsNullOrEmpty(extractedText))
                        {
                            Paragraph p = new Paragraph();

                            // Add the icon first, if available
                            if (!string.IsNullOrEmpty(icon))
                            {
                                Image img = new Image(ImageDataFactory.Create(icon));
                                img.ScaleToFit(20f, 20f); // Adjust image size as needed
                                p.Add(img);
                            }

                            // Add the extracted text
                            p.Add(new Text(extractedText).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE)));

                            // Display the paragraphCode and make it a clickable link (only the paragraphCode is clickable not the whole line/paragraphs)
                            // paragraphCode should be at the last of the paragraph, underlined and clickable to external URL, with blue color
                            if (!string.IsNullOrEmpty(paragraphCode))
                            {
                                p.Add(new Text(paragraphCode)
                                    .SetUnderline()
                                    .SetAction(PdfAction.CreateURI("https://example.com/" + paragraphCode))
                                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE))
                                    .SetFontColor(ColorConstants.BLUE));
                            }


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

            Console.WriteLine("✅ PDF Generated Successfully: " + outputPdfPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error: " + ex.Message);
            Console.WriteLine("❌ Stack Trace: " + ex.StackTrace);
        }
    }

    static void ApplyFormatting(Paragraph p, Attributes attributes)
    {

        if (attributes == null) return;

        // Font Style Handling
        PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        if (attributes.Bold && attributes.Italic)
            font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLDOBLIQUE);
        else if (attributes.Bold)
            font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        else if (attributes.Italic)
            font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE);

        p.SetFont(font);

        // Underline
        if (attributes.Underline)
            p.SetUnderline();

        // Font Size
        if (attributes.Size > 0)
            p.SetFontSize(attributes.Size);

        // Background Color
        if (!string.IsNullOrEmpty(attributes.Background))
            p.SetBackgroundColor(ConvertColor(attributes.Background));

    }

    static (string Text, string paragraphReference, string Icon) ExtractTextFromNonTextElement(JObject obj)
    {
        if (obj.ContainsKey("notes") && obj["notes"] is JObject notes)
        {
            string bookCode = notes["publication"]?["code"]?.ToString() ?? "";
            string paragraphId = notes["entity"]?["paraId"]?.ToString() ?? "";
            string selectedText = notes["entity"]?["text"]?.ToString() ?? "";

            return (selectedText + " ", bookCode + " " + paragraphId, ""); // Example icon for notes
        }

        if (obj.ContainsKey("bookmarks") && obj["bookmarks"] is JObject bookmarks)
        {
            string bookCode = bookmarks["publication"]?["code"]?.ToString() ?? "";
            string paragraphId = bookmarks["entity"]?["paraId"]?.ToString() ?? "";
            string selectedText = bookmarks["entity"]?["text"]?.ToString() ?? "";

            return (selectedText + " ", bookCode + " " + paragraphId, ""); // Example icon for bookmarks
        }

        if (obj.ContainsKey("highlights") && obj["highlights"] is JObject highlights)
        {
            string bookCode = highlights["publication"]?["code"]?.ToString() ?? "";
            string paragraphId = highlights["entity"]?["range"]?["range"]?.ToString().Split("-")[0] ?? "";
            string selectedText = highlights["entity"]?["selected"]?.ToString() ?? "";

            return (selectedText + " ", bookCode + " " + paragraphId, ""); // Example icon for highlights
        }

        if (obj.ContainsKey("verse"))
            return ("[Verse: " + obj["verse"]?["id"]?.ToString() + "]", " ", "https://static-00.iconduck.com/assets.00/404-page-not-found-illustration-512x249-ju1c9yxg.png"); // Example icon for verse

        return ("", "", ""); // Default return if no match
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


    // Method to apply alignment to a paragraph
    static void ApplyAlignment(Paragraph p, string align)
    {
        switch (align.ToLower())
        {
            case "center": p.SetTextAlignment(TextAlignment.CENTER); break;
            case "right": p.SetTextAlignment(TextAlignment.RIGHT); break;
            case "justify": p.SetTextAlignment(TextAlignment.JUSTIFIED); break;
            default: p.SetTextAlignment(TextAlignment.LEFT); break;
        }
    }
}

// Classes for JSON Parsing
class QuillDocument
{
    public List<QuillBlock> Content { get; set; }

    public string Title { get; set; }

    public string Presenter { get; set; }

    public string Summary { get; set; }
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

    public string List { get; set; }
}


