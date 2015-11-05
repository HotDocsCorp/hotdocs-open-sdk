using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Xml.Serialization;
using HotDocs.Sdk.Cloud;
using HotDocs.Sdk.Server.Contracts;

namespace HotDocs.Sdk.Server
{
    /// <summary>
    ///     A Service that allows connection and use of features on a HotDocs web services address or a HotDocs Cloud Services
    ///     address through REST calls.
    /// </summary>
    public class HotDocsService : IServices
    {
        private readonly RetrieveFromHub _retrieveFromHub;

        /// <summary>
        ///     Allows connection to a HotDocs Web API
        /// </summary>
        /// <param name="hostAddress">The address of the Web API</param>
        public HotDocsService(string hostAddress)
        {
            if (string.IsNullOrEmpty(hostAddress))
                throw new ArgumentException("Host address cannot be null");

            HostAddress = hostAddress;
            SigningKey = string.Empty;
            SubscriberId = "0";
        }

        /// <summary>
        ///     Allows connection to a HotDocs Cloud Services API
        /// </summary>
        /// <param name="retrieveFromHub"></param>
        /// <param name="hostAddress">The address of the Cloud Services API</param>
        /// <param name="subscriberId">Your Unique SubscriberId</param>
        /// <param name="signingKey">Your Uniuqe Signing Key</param>
        public HotDocsService(string subscriberId, string signingKey,
            RetrieveFromHub retrieveFromHub = RetrieveFromHub.No, string hostAddress = "https://cloud.hotdocs.ws/hdcs")
        {
            if (string.IsNullOrWhiteSpace(hostAddress))
                throw new ArgumentNullException(nameof(hostAddress));

            if (string.IsNullOrWhiteSpace(subscriberId))
                throw new ArgumentNullException(nameof(subscriberId));

            if (string.IsNullOrWhiteSpace(signingKey))
                throw new ArgumentNullException(nameof(signingKey));

            HostAddress = hostAddress;
            SigningKey = signingKey;
            _retrieveFromHub = retrieveFromHub;
            SubscriberId = subscriberId;
        }

        private string HostAddress { get; }
        private string SubscriberId { get; }
        private string SigningKey { get; }

        /// <summary>
        ///     Assemble a document from the given template, answers and settings.
        /// </summary>
        /// <param name="template">An instance of the Template class.</param>
        /// <param name="answers">
        ///     Either an XML answer string, or a string containing encoded
        ///     interview answers as posted from a HotDocs browser interview.
        /// </param>
        /// <param name="settings">An instance of the AssembleDocumentResult class.</param>
        /// <include file="../Shared/Help.xml" path="Help/string/param[@name='logRef']" />
        /// <returns>An AssemblyResult object containing all the files and data resulting from the request.</returns>
        public AssembleDocumentResult AssembleDocument(Template template, TextReader answers,
            AssembleDocumentSettings settings, string logRef = "")
        {
            if (template == null)
                throw new ArgumentNullException("template");

            using (var client = new HttpClient())
            {
                AssembleDocumentResult adr = null;
                var packageTemplateLocation = (PackageTemplateLocation) template.Location;
                var packageId = packageTemplateLocation.PackageID;
                var of = ConvertFormat(settings.Format);
                var timestamp = DateTime.UtcNow;

                var hmac = HMAC.CalculateHMAC(
                    SigningKey,
                    timestamp,
                    SubscriberId,
                    packageId,
                    template.FileName,
                    false,
                    logRef,
                    of,
                    settings.Settings);

                var urlBuilder =
                    new StringBuilder().AppendFormat("{0}/assemble/{1}/{2}/{3}?" +
                                                     "format={4}&" +
                                                     "encodefilenames={5}&" +
                                                     "billingref={6}&" +
                                                     "retrievefromhub={7}",
                        HostAddress, SubscriberId, packageId, Uri.EscapeDataString(template.FileName),
                        of,
                        true,
                        Uri.EscapeDataString(logRef),
                        _retrieveFromHub);

                if (settings.Settings != null)
                {
                    foreach (var kv in settings.Settings)
                    {
                        urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
                    }
                }

                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(urlBuilder.ToString()),
                    Method = HttpMethod.Post
                };

                request.Headers.Add("x-hd-date", timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                request.Headers.Authorization = new AuthenticationHeaderValue("basic", hmac);

                var stringContent = answers == null
                    ? new StringContent(string.Empty)
                    : new StringContent(answers.ReadToEnd());

                request.Content = stringContent;

                request.Content.Headers.TryAddWithoutValidation("Content-Type", "text/xml");

                var result = client.SendAsync(request).Result;

                var parser = new MultipartMimeParser();

                HandleStatusCode(result);

                var streamResult = result.Content.ReadAsStreamAsync().Result;

                var streams = new List<MemoryStream>();

                using (var resultsStream = new MemoryStream())
                {
                    // Each part is written to a file whose name is specified in the content-disposition
                    // header, except for the AssemblyResult part, which has a file name of "meta0.xml",
                    // and is parsed into an AssemblyResult object.
                    parser.WritePartsToStreams(
                        streamResult,
                        h =>
                        {
                            var fileName = GetFileNameFromHeaders(h);
                            if (fileName == null) return Stream.Null;

                            if (fileName.Equals("meta0.xml", StringComparison.OrdinalIgnoreCase))
                            {
                                return resultsStream;
                            }

                            var stream = new MemoryStream();
                            streams.Add(stream);
                            return stream;
                        },
                        (new ContentType(result.Content.Headers.ContentType.ToString())).Boundary);

                    if (resultsStream.Position <= 0) return null;

                    resultsStream.Position = 0;
                    var serializer = new XmlSerializer(typeof (AssemblyResult));
                    var asmResult = (AssemblyResult) serializer.Deserialize(resultsStream);

                    if (asmResult == null) return adr;

                    for (var i = 0; i < asmResult.Documents.Length; i++)
                    {
                        asmResult.Documents[i].Data = streams[i].ToArray();
                        streams[i].Dispose();
                    }

                    adr = Util.ConvertAssemblyResult(template, asmResult, settings.Format);
                }

                return adr;
            }
        }

        /// <summary>
        ///     GetComponentInfo returns metadata about the variables/types (and optionally dialogs and mapping info)
        ///     for the indicated template's interview.
        /// </summary>
        /// <param name="template">An instance of the Template class, for which you are requesting component information.</param>
        /// <param name="includeDialogs">Whether to include dialog &amp; mapping information in the returned results.</param>
        /// <include file="../Shared/Help.xml" path="Help/string/param[@name='logRef']" />
        /// <returns>The requested component information.</returns>
        public ComponentInfo GetComponentInfo(Template template, bool includeDialogs, string logRef = "")
        {
            if (template == null)
                throw new ArgumentNullException("template");

            using (var client = new HttpClient())
            {
                var packageTemplateLocation = (PackageTemplateLocation) template.Location;
                var packageId = packageTemplateLocation.PackageID;
                var timestamp = DateTime.UtcNow;

                var hmac = HMAC.CalculateHMAC(
                    SigningKey,
                    timestamp,
                    SubscriberId,
                    packageId,
                    template.FileName,
                    false,
                    logRef,
                    includeDialogs);

                var request = new HttpRequestMessage
                {
                    RequestUri =
                        new Uri(
                            string.Format(
                                "{0}/componentinfo/{1}/{2}/{3}?includedialogs={4}&billingref={5}&retrieveFromHub={6}",
                                HostAddress,
                                SubscriberId, packageId, Uri.EscapeDataString(template.FileName), includeDialogs,
                                Uri.EscapeDataString(logRef), _retrieveFromHub)),
                    Method = HttpMethod.Get
                };

                request.Headers.Add("x-hd-date", timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                request.Headers.Authorization = new AuthenticationHeaderValue("basic", hmac);


                var result =
                    client.SendAsync(request).Result;

                HandleStatusCode(result);
                var streamResult = result.Content.ReadAsStreamAsync().Result;

                using (var stream = (MemoryStream) streamResult)
                {
                    stream.Position = 0;
                    var serializer = new XmlSerializer(typeof (ComponentInfo));
                    return (ComponentInfo) serializer.Deserialize(stream);
                }
            }
        }

        /// <summary>
        ///     Retrieves a file required by the interview. This could be either an interview definition that contains the
        ///     variables and logic required to display an interview (questionaire) for the main template or one of its
        ///     inserted templates, or it could be an image file displayed on a dialog within the interview.
        /// </summary>
        /// <param name="template">The template related to the requested file.</param>
        /// <param name="fileName">
        ///     The file name of the image, or the file name of the template for which the interview
        ///     definition is being requested. In either case, this value is passed as "template" on the query string by the
        ///     browser interview.
        /// </param>
        /// <param name="fileType">The type of file being requested</param>
        /// <returns>A stream containing the requested interview file</returns>
        public Stream GetInterviewFile(Template template, string fileName, string fileType = "")
        {
            return GetInterviewFile(template, fileName, fileType, "");
        }

        /// <summary>
        ///     GetInterview returns an HTML fragment suitable for inclusion in any standards-mode web page, which embeds a HotDocs
        ///     interview
        ///     directly in that web page.
        /// </summary>
        /// <param name="template">The template for which to return an interview.</param>
        /// <param name="answers">The answers to use when building an interview.</param>
        /// <param name="settings">The <see cref="InterviewSettings" /> to use when building an interview.</param>
        /// <param name="markedVariables">
        ///     The variables to highlight to the user as needing special attention.
        ///     This is usually populated with <see cref="AssembleDocumentResult.UnansweredVariables" />
        ///     from <see cref="AssembleDocument" />.
        /// </param>
        /// <include file="../Shared/Help.xml" path="Help/string/param[@name='logRef']" />
        /// <returns>Returns the results of building the interview as an <see cref="InterviewResult" /> object.</returns>
        public InterviewResult GetInterview(Template template, TextReader answers, InterviewSettings settings,
            IEnumerable<string> markedVariables, string logRef = "")
        {
            if (template == null)
                throw new ArgumentNullException("template");

            if (settings == null)
                throw new ArgumentNullException("settings");

            using (var client = new HttpClient())
            {
                var packageTemplateLocation = (PackageTemplateLocation) template.Location;
                var packageId = packageTemplateLocation.PackageID;
                var timestamp = DateTime.UtcNow;

                var hmac = HMAC.CalculateHMAC(
                    SigningKey,
                    timestamp,
                    SubscriberId,
                    packageId,
                    template.FileName,
                    false,
                    logRef,
                    settings.Format,
                    settings.InterviewFilesUrl,
                    settings.Settings);

                var urlBuilder = new StringBuilder().AppendFormat("{0}/interview/{1}/{2}/{3}?" +
                                                                  "format={4}&" +
                                                                  "markedvariables{5}&" +
                                                                  "tempimageurl={6}&" +
                                                                  "encodeFileNames={7}&" +
                                                                  "billingref={8}&" +
                                                                  "retrievefromhub={9}",
                    HostAddress, SubscriberId, packageId, Uri.EscapeDataString(template.FileName),
                    settings.Format,
                    markedVariables != null && markedVariables.Any()
                        ? "=" + Uri.EscapeDataString(string.Join(",", settings.MarkedVariables))
                        : null,
                    Uri.EscapeDataString(settings.InterviewFilesUrl),
                    true,
                    Uri.EscapeDataString(logRef),
                    _retrieveFromHub);

                foreach (var kv in settings.Settings)
                {
                    urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
                }

                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(urlBuilder.ToString()),
                    Method = HttpMethod.Post
                };

                request.Headers.Add("x-hd-date", timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                request.Headers.Authorization = new AuthenticationHeaderValue("basic", hmac);

                var stringContent = answers == null
                    ? new StringContent(string.Empty)
                    : new StringContent(answers.ReadToEnd());

                request.Content = stringContent;

                var result = client.SendAsync(request).Result;

                HandleStatusCode(result);

                var streamResult = result.Content.ReadAsStreamAsync().Result;

                var parser = new MultipartMimeParser();
                var outputDir = Path.GetTempPath();
                Directory.CreateDirectory(outputDir);
                var streams = new List<MemoryStream>();

                using (var resultsStream = new MemoryStream())
                {
                    // Each part is written to a file whose name is specified in the content-disposition
                    // header, except for the BinaryObject[] part, which has a file name of "meta0.xml",
                    // and is parsed into an BinaryObject[] object.
                    parser.WritePartsToStreams(
                        streamResult,
                        h =>
                        {
                            var fileName = GetFileNameFromHeaders(h);

                            if (string.IsNullOrEmpty(fileName)) return Stream.Null;

                            if (fileName.Equals("meta0.xml", StringComparison.OrdinalIgnoreCase))
                            {
                                return resultsStream;
                            }

                            // The following stream will be closed by the parser
                            var stream = new MemoryStream();
                            streams.Add(stream);
                            return stream;
                        },
                        (new ContentType(result.Content.Headers.ContentType.ToString())).Boundary);

                    if (resultsStream.Position <= 0) return null;

                    resultsStream.Position = 0;
                    var serializer = new XmlSerializer(typeof (BinaryObject[]));
                    var binObjects = (BinaryObject[]) serializer.Deserialize(resultsStream);

                    for (var i = 0; i < binObjects.Length; i++)
                    {
                        binObjects[i].Data = streams[i].ToArray();
                        streams[i].Dispose();
                    }

                    var interviewContent = Util.ExtractString(binObjects[0]);
                    return new InterviewResult {HtmlFragment = interviewContent};
                }
            }
        }

        public string GetAnswers(IEnumerable<TextReader> answers, string logRef)
        {
            throw new NotImplementedException();
        }

        public void BuildSupportFiles(Template template, HDSupportFilesBuildFlags flags)
        {
            throw new NotImplementedException();
        }

        public void RemoveSupportFiles(Template template)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Retrieves a file required by the interview. This could be either an interview definition that contains the
        ///     variables and logic required to display an interview (questionaire) for the main template or one of its
        ///     inserted templates, or it could be an image file displayed on a dialog within the interview.
        /// </summary>
        /// <param name="template">The template related to the requested file.</param>
        /// <param name="fileName">
        ///     The file name of the image, or the file name of the template for which the interview
        ///     definition is being requested. In either case, this value is passed as "template" on the query string by the
        ///     browser interview.
        /// </param>
        /// <param name="fileType">The type of file being requested</param>
        /// <include file="../Shared/Help.xml" path="Help/string/param[@name='logRef']" />
        /// <returns>A stream containing the requested interview file</returns>
        public Stream GetInterviewFile(Template template, string fileName, string fileType, string logRef)
        {
            if (template == null)
                throw new ArgumentNullException("template");

            if (!Path.HasExtension(fileName) && fileType == string.Empty)
            {
                throw new ArgumentException("File extension must be included in the filename or the filetype parameter");
            }

            var packageTemplateLocation = (WebServiceTemplateLocation) template.Location;
            return
                packageTemplateLocation.GetFile(!Path.HasExtension(fileName)
                    ? Uri.EscapeDataString(fileName +
                                           (fileType.StartsWith(".") ? fileType : string.Format(".{0}", fileType)))
                    : Uri.EscapeDataString(fileName), logRef);
        }

        private static void HandleStatusCode(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            var errorMessage = response.Content.ReadAsStringAsync().Result;

            if (string.IsNullOrEmpty(errorMessage))
                throw new Exception("An Error Has Occured: " + response.ReasonPhrase);

            throw new Exception(errorMessage);
        }

        private static string GetFileNameFromHeaders(Dictionary<string, string> headers)
        {
            string disp;

            if (!headers.TryGetValue("Content-Disposition", out disp)) return null;

            var pairs = disp.Split(';');

            foreach (var pair in pairs)
            {
                var trimmed = pair.Trim();

                if (!trimmed.StartsWith("filename*=")) continue;

                var endPos = trimmed.IndexOf("'", StringComparison.Ordinal);

                return trimmed.Substring(endPos + 2);
            }
            return null;
        }

        private OutputFormat ConvertFormat(DocumentType docType)
        {
            var format = OutputFormat.None;
            switch (docType)
            {
                case DocumentType.HFD:
                    format = OutputFormat.HFD;
                    break;
                case DocumentType.HPD:
                    format = OutputFormat.HPD;
                    break;
                case DocumentType.HTML:
                    format = OutputFormat.HTML;
                    break;
                case DocumentType.HTMLwDataURIs:
                    format = OutputFormat.HTMLwDataURIs;
                    break;
                case DocumentType.MHTML:
                    format = OutputFormat.MHTML;
                    break;
                case DocumentType.Native:
                    format = OutputFormat.Native;
                    break;
                case DocumentType.PDF:
                    format = OutputFormat.PDF;
                    break;
                case DocumentType.PlainText:
                    format = OutputFormat.PlainText;
                    break;
                case DocumentType.WordDOC:
                    format = OutputFormat.DOCX;
                    break;
                case DocumentType.WordDOCX:
                    format = OutputFormat.DOCX;
                    break;
                case DocumentType.WordPerfect:
                    format = OutputFormat.WPD;
                    break;
                case DocumentType.WordRTF:
                    format = OutputFormat.RTF;
                    break;
                case DocumentType.XML:
                    // Note: Contracts.OutputFormat does not have an XML document type.
                    format = OutputFormat.None;
                    break;
                default:
                    format = OutputFormat.None;
                    break;
            }
            // Always include the Answers output
            format |= OutputFormat.Answers;
            return format;
        }
    }
}