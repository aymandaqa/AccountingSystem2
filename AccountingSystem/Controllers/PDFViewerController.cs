using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Syncfusion.EJ2.PdfViewer;
using System.Net;

namespace AccountingSystem.Controllers
{
    public class PdfViewerController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        public IMemoryCache _cache;
        public PdfViewerController(IWebHostEnvironment hostingEnvironment, IMemoryCache cache)
        {
            _hostingEnvironment = hostingEnvironment;
            _cache = cache;
        }
        // GET: /<controller>/
        public IActionResult Index()
        {
            return View();
        }
        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/Load")]
        public IActionResult Load([FromBody] Dictionary<string, string> jsonData)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            MemoryStream stream = new MemoryStream();

            object jsonResult = new object();
            if (jsonData != null && jsonData.ContainsKey("document"))
            {
                if (bool.Parse(jsonData["isFileName"]))
                {
                    string documentPath = GetDocumentPath(jsonData["document"]);

                    if (!string.IsNullOrEmpty(documentPath))
                    {
                        byte[] bytes = System.IO.File.ReadAllBytes(documentPath);
                        stream = new MemoryStream(bytes);
                    }
                    else
                    {
                        string fileName = jsonData["document"].Split(new string[] { "://" }, StringSplitOptions.None)[0];
                        if (fileName == "http" || fileName == "https")
                        {
                            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                            if (TryReadLocalFileFromUrl(jsonData["document"], out byte[] localBytes))
                            {
                                stream = new MemoryStream(localBytes);
                            }
                            else
                            {
                                try
                                {
                                    using (WebClient webClient = new WebClient())
                                    {
                                        byte[] pdfDoc = webClient.DownloadData(jsonData["document"]);
                                        stream = new MemoryStream(pdfDoc);
                                    }
                                }
                                catch (WebException ex)
                                {
                                    if (ex.Response is HttpWebResponse httpResponse)
                                    {
                                        if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                                        {
                                            return StatusCode((int)HttpStatusCode.NotFound, $"Document '{jsonData["document"]}' was not found.");
                                        }

                                        string errorDetails = string.Empty;
                                        using (var responseStream = httpResponse.GetResponseStream())
                                        {
                                            if (responseStream != null)
                                            {
                                                using (var reader = new StreamReader(responseStream))
                                                {
                                                    errorDetails = reader.ReadToEnd();
                                                }
                                            }
                                        }

                                        if (string.IsNullOrWhiteSpace(errorDetails))
                                        {
                                            errorDetails = $"Unable to download '{jsonData["document"]}'. Remote server returned status {(int)httpResponse.StatusCode} ({httpResponse.StatusDescription}).";
                                        }

                                        return StatusCode((int)httpResponse.StatusCode, errorDetails);
                                    }

                                    return StatusCode((int)HttpStatusCode.BadGateway, $"Unable to download '{jsonData["document"]}'. {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            return this.Content(jsonData["document"] + " is not found");
                        }

                    }
                }
                else
                {
                    byte[] bytes = Convert.FromBase64String(jsonData["document"]);
                    stream = new MemoryStream(bytes);
                }
            }
            jsonResult = pdfviewer.Load(stream, jsonData);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/RenderPdfPages")]
        public IActionResult RenderPdfPages([FromBody] Dictionary<string, string> jsonObject)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object jsonResult = pdfviewer.GetPage(jsonObject);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/RenderAnnotationComments")]
        public IActionResult RenderAnnotationComments([FromBody] Dictionary<string, string> jsonObject)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object jsonResult = pdfviewer.GetAnnotationComments(jsonObject);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/Unload")]
        public IActionResult Unload([FromBody] Dictionary<string, string> jsonObject)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            pdfviewer.ClearCache(jsonObject);
            return this.Content("Document cache is cleared");
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/RenderThumbnailImages")]
        public IActionResult RenderThumbnailImages([FromBody] Dictionary<string, string> jsonObject)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object result = pdfviewer.GetThumbnailImages(jsonObject);
            return Content(JsonConvert.SerializeObject(result));
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/Bookmarks")]
        public IActionResult Bookmarks([FromBody] Dictionary<string, string> jsonObject)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object jsonResult = pdfviewer.GetBookmarks(jsonObject);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/Download")]
        public IActionResult Download([FromBody] Dictionary<string, string> jsonObject)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            string documentBase = pdfviewer.GetDocumentAsBase64(jsonObject);
            return Content(documentBase);
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [Route("api/[controller]/PrintImages")]
        public IActionResult PrintImages([FromBody] Dictionary<string, string> jsonObject)
        {
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object pageImage = pdfviewer.GetPrintImage(jsonObject);
            return Content(JsonConvert.SerializeObject(pageImage));
        }

        private string GetDocumentPath(string document)
        {
            string documentPath = string.Empty;
            if (!System.IO.File.Exists(document))
            {
                string basePath = _hostingEnvironment.WebRootPath;
                string dataPath = string.Empty;
                dataPath = basePath + "\\";// + @"/fileuploads/";
                if (System.IO.File.Exists(dataPath + document))
                    documentPath = dataPath + document;
            }
            else
            {
                documentPath = document;
            }
            return documentPath;
        }

        private bool TryReadLocalFileFromUrl(string url, out byte[] bytes)
        {
            bytes = null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var requestHost = Request?.Host;
            if (!requestHost.HasValue)
            {
                return false;
            }

            string currentHost = requestHost.Host;
            if (!string.Equals(uri.Host, currentHost, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relativePath = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string webRootPath = _hostingEnvironment.WebRootPath;
            if (string.IsNullOrEmpty(webRootPath))
            {
                return false;
            }

            string candidatePath = Path.Combine(webRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string fullCandidatePath = Path.GetFullPath(candidatePath);
            string fullWebRoot = Path.GetFullPath(webRootPath);

            if (!fullCandidatePath.StartsWith(fullWebRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!System.IO.File.Exists(fullCandidatePath))
            {
                return false;
            }

            bytes = System.IO.File.ReadAllBytes(fullCandidatePath);
            return true;
        }

    }

}
