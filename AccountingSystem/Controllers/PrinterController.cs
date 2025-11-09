using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Newtonsoft.Json.Linq;
using Roadfn.Models;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Barcode;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Security;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Color = Syncfusion.Drawing.Color;
using Image = Syncfusion.Drawing.Image;

namespace Roadfn.Controllers
{
    public class PrinterController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private RoadFnDbContext _context;
        private readonly UserManager<AccountingSystem.Models.User> _userManager;
        private readonly ApplicationDbContext _accDB;

        public PrinterController(IWebHostEnvironment env, RoadFnDbContext context, UserManager<AccountingSystem.Models.User> userManager, ApplicationDbContext accDB)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
            _accDB = accDB;
        }

        public IActionResult Index()
        {
            return View();
        }


        [Route("PrintUserSlip")]
        [HttpGet]
        public async Task<ActionResult> PrintUserSlip(string Id)
        {
            var ids = Id.Split(",");

            using (PdfDocument finalDocument = new PdfDocument())
            {
                foreach (var id in ids)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        FileStream fileStream = new FileStream(System.IO.Path.Combine(_env.WebRootPath, "UserSlip.docx"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        WordDocument document = new WordDocument(fileStream, FormatType.Docx);
                        WTable table = await CreateTable(document, id);
                        // Set the width of the table to fit the page width.
                        table.TableFormat.Borders.BorderType = Syncfusion.DocIO.DLS.BorderStyle.Single;
                        table.TableFormat.Paddings.All = 1;
                        try
                        {
                            table.AutoFit(AutoFitType.FitToContent);
                        }
                        catch (Exception ex)
                        {

                        }

                        document.LastParagraph.OwnerTextBody.ChildEntities.Add(table);



                        foreach (WSection section in document.Sections)
                        {
                            IWParagraph paragraph = section.HeadersFooters.OddFooter.AddParagraph();
                            WPicture picture = (WPicture)paragraph.AppendPicture(GenerateBarcodeImage(id));
                            picture.Width = 150;
                            ApplyFormattingForCaption(document.LastParagraph);
                        }
                        MemoryStream stream = new MemoryStream();
                        document.Save(stream, Syncfusion.DocIO.FormatType.Docx);
                        stream.Position = 0;

                        //Instantiation of DocIORenderer for Word to PDF conversion
                        DocIORenderer render = new DocIORenderer();
                        //Sets Chart rendering Options.
                        render.Settings.ChartRenderingOptions.ImageFormat = Syncfusion.OfficeChart.ExportImageFormat.Jpeg;
                        //Converts Word document into PDF document  
                        PdfDocument pdf = render.ConvertToPDF(document);



                        MemoryStream memoryStream1 = new MemoryStream();

                        // Save the PDF document.
                        pdf.Save(memoryStream1);
                        memoryStream1.Position = 0;

                        PdfDocumentBase.Merge(finalDocument, memoryStream1);
                        render.Dispose();
                        pdf.Close();
                        document.Close();
                        fileStream.Close();
                    }


                }
                MemoryStream memoryStream = new MemoryStream();
                PdfSecurity security = finalDocument.Security;

                security.Algorithm = PdfEncryptionAlgorithm.AES;
                security.KeySize = PdfEncryptionKeySize.Key128Bit;

                security.OwnerPassword = "YourStrongOwnerPassword@@12##@!@#$#";
                security.Permissions = PdfPermissionsFlags.Print; // فقط الطباعة، كل شيء آخر ممنوع
                // Save the PDF document.
                finalDocument.Save(memoryStream);
                memoryStream.Position = 0;

                var filepath = SaveMemoryStream(memoryStream, @"Forms\" + Guid.NewGuid().ToString() + ".pdf");
                ViewBag.PrintPreview = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}/{filepath}";
                ViewBag.title = "User Slip";
                return View("PrintPreview");

            }
        }





        public async Task<WTable> CreateTable(WordDocument document, string BisnessUserPaymentHeaders)
        {
            var header = await _context.BisnessUserPaymentHeader.FindAsync(Convert.ToInt64(BisnessUserPaymentHeaders));

            var details = await _context.PayBusinessSlipView.Where(t => t.PaymentHeader == header.Id).ToListAsync();

            DataTable table = new DataTable();
            //DataColumn dataColumn1 = new DataColumn("ID");
            //table.Columns.Add(dataColumn1);
            DataColumn dataColumn1 = new DataColumn($"اسم العميل");
            table.Columns.Add(dataColumn1);

            DataColumn dataColumn2 = new DataColumn($"محتويات الطرد");
            table.Columns.Add(dataColumn2);

            DataColumn dataColumn3 = new DataColumn($"الاجمالي");
            table.Columns.Add(dataColumn3);

            DataColumn dataColumn4 = new DataColumn($"سعر الشحنة");
            table.Columns.Add(dataColumn4);

            DataColumn dataColumn5 = new DataColumn($"رسوم الشحن");
            table.Columns.Add(dataColumn5);

            DataColumn dataColumn6 = new DataColumn($"رقم الشحنة");
            table.Columns.Add(dataColumn6);

            DataColumn dataColumn7 = new DataColumn($"التاريخ");
            table.Columns.Add(dataColumn7);



            DataColumn dataColumn8 = new DataColumn($"منطقة العميل");
            table.Columns.Add(dataColumn8);


            DataColumn dataColumn9 = new DataColumn($"رسوم إضافية");
            table.Columns.Add(dataColumn9);

            foreach (var item in details)
            {
                DataRow dataRow = table.NewRow();
                //dataRow[0] = item?.Id;
                dataRow[5] = item?.ShipmentTrackingNo;
                if (item.ShipmentContains != null)
                {

                    item.ShipmentContains = item.ShipmentContains;

                }

                dataRow[1] = item?.ShipmentContains;

                if (item.ClientName != null)
                {
                    item.ClientName = item.ClientName;

                }
                dataRow[0] = item?.ClientName;
                //dataRow[3] = item?.CityName;
                dataRow[7] = item?.AreaName;
                dataRow[6] = Convert.ToDateTime(item.EntryDate).ToString("dd/MM/yyyy");
                dataRow[3] = item?.ShipmentPrice;
                dataRow[4] = item?.ShipmentFees;
                dataRow[8] = item?.ShipmentExtraFees;
                //  dataRow[7] = item?.ReturnFees;
                dataRow[2] = item?.ShipmentTotal;
                table.Rows.Add(dataRow);


            }
            var buss = await _context.Users.FindAsync(header?.UserId);
            if (buss.Address == null)
            {
                buss.Address = "";
            }

            var usera = await _userManager.GetUserAsync(User);
            var company = await _accDB.Branches.FirstOrDefaultAsync(t => t.Id == usera.PaymentBranchId);
            var t = await _context.CompanyBranches.FirstOrDefaultAsync(t => t.Id.ToString() == company.Code);

            document.Replace("{Date}", Convert.ToDateTime(header?.PaymentDate).ToString("dd/MM/yyyy"), true, true);
            document.Replace("{Total delivery shipments}", details.Count().ToString(), true, true);
            document.Replace("{Total Amount}", header?.PaymentValue.ToString(), true, true);
            document.Replace("{Ref}", header?.Id.ToString(), true, true);
            document.Replace("{Return Fee}", details?.Sum(t => t?.ReturnFees).ToString(), true, true);
            document.Replace("{Shipping expenses}", details?.Sum(t => t?.ShipmentFees).ToString(), true, true);
            document.Replace("{Extra charge}", details?.Sum(t => t?.ShipmentExtraFees).ToString(), true, true);
            document.Replace("{Customer dues}", header?.PaymentValue.ToString(), true, true);
            document.Replace("{AgentName}", buss?.FirstName + " " + buss?.LastName, true, true);
            document.Replace("{Act User Name}", usera?.FirstName + " " + usera?.LastName, true, true);
            document.Replace("{address}", buss?.Address, true, true);
            document.Replace("{mobile}", buss?.MobileNo1, true, true);
            document.Replace("{area}", t.BranchName?.ToString(), true, true);

            //Adds a new table into Word document
            WTable table1 = new WTable(document);

            //Specifies the total number of rows & columns
            table1.ResetCells(table.Rows.Count + 1, table.Columns.Count);

            IWTextRange textRange;
            int r = 0;

            float pageWidth = document.LastSection.PageSetup.PageSize.Width;
            foreach (DataColumn item in table.Columns)
            {
                //table1[0, r].Width = pageWidth / table1.Rows[r].Cells.Count;

                //Accesses the instance of the cell (first row, first cell) and adds the content into cell
                textRange = table1[0, r].AddParagraph().AppendText(item.ColumnName);
                //textRange.CharacterFormat.FontName = "Arial";
                textRange.CharacterFormat.FontSize = 8;
                textRange.CharacterFormat.Bold = true;
                textRange.CharacterFormat.TextBackgroundColor = Color.AliceBlue;
                textRange.CharacterFormat.TextColor = Color.Black;
                textRange.CharacterFormat.TextColor = Color.Black;
                int c = 1;
                foreach (DataRow itemc in table.Rows)
                {

                    //table1[c, r].Width = pageWidth / table1.Rows[r].Cells.Count;
                    //Accesses the instance of the cell (first row, first cell) and adds the content into cell
                    textRange = table1[c, r].AddParagraph().AppendText(itemc[item.ColumnName.ToString()].ToString());
                    //  textRange.CharacterFormat.FontName = "Arial";
                    textRange.CharacterFormat.FontSize = 6;
                    textRange.CharacterFormat.Bold = true;
                    c++;
                }
                r++;


            }


            return table1;
        }

        public Stream GenerateBarcodeImage(string barcodeText)
        {
            //Initialize a new PdfCode39Barcode instance
            PdfCode39Barcode barcode = new PdfCode39Barcode();
            //Set the height and text for barcode
            barcode.BarHeight = 45;
            barcode.Text = barcodeText;
            //Convert the barcode to image
            var barcodeImage = barcode.ToImage(new Syncfusion.Drawing.SizeF(145, 45));
            Image image = Image.FromStream(barcodeImage);



            return barcodeImage;
        }

        [NonAction]
        public string SaveMemoryStream(MemoryStream ms, string FileName)
        {
            FileStream outStream = System.IO.File.OpenWrite(_env.WebRootPath + @"/" + FileName);
            ms.WriteTo(outStream);
            outStream.Flush();
            outStream.Close();

            return FileName;
        }
        public void ApplyFormattingForCaption(WParagraph paragraph)
        {
            //Align the caption
            paragraph.ParagraphFormat.HorizontalAlignment = Syncfusion.DocIO.DLS.HorizontalAlignment.Center;
            //Sets after spacing
            paragraph.ParagraphFormat.AfterSpacing = 1.5f;
            //Sets before spacing
            paragraph.ParagraphFormat.BeforeSpacing = 1.5f;
        }


    }
}
