using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Web;
using System.Xml.Serialization;
using HotDocs.Sdk.Cloud;
using HotDocs.Sdk.Server.Contracts;

namespace HotDocs.Sdk.Server.OnPremise
{
    [Obsolete("Please use HotDocsService instead")]
    public class OnPremiseServices : IServices
    {
        public OnPremiseServices(string hostAddress)
        {
            if (string.IsNullOrEmpty(hostAddress))
                throw new Exception("Host address cannot be null");

            HostAddress = hostAddress;
        }

        private string HostAddress { get; set; }

        public AssembleDocumentResult AssembleDocument(Template template, TextReader answers,
            AssembleDocumentSettings settings, string logRef)
        {
            using (var client = new HttpClient())
            {
                AssembleDocumentResult adr = null;
                var stringContent = new StringContent(answers.ReadToEnd());
                var packageTemplateLocation = (PackageTemplateLocation) template.Location;
                string packageId = packageTemplateLocation.PackageID;

                OutputFormat of = ConvertFormat(settings.Format);

                var urlBuilder =
                    new StringBuilder(string.Format(HostAddress + "/assemble/0/{0}/{1}?format={2}&encodefilenames={3}", packageId,
                        HttpUtility.UrlEncode(template.FileName), of,true));

                if (settings.Settings != null)
                {
                    foreach (var kv in settings.Settings)
                    {
                        urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
                    }
                }

                HttpResponseMessage result = client.PostAsync(urlBuilder.ToString(), stringContent).Result;

                var _parser = new MultipartMimeParser();

                HandleErrorStream(result, result.IsSuccessStatusCode);
                Stream streamResult = result.Content.ReadAsStreamAsync().Result;

                string outputDir = Path.GetTempPath();
                Directory.CreateDirectory(outputDir);
                using (var resultsStream = new MemoryStream())
                {
                    // Each part is written to a file whose name is specified in the content-disposition
                    // header, except for the AssemblyResult part, which has a file name of "meta0.xml",
                    // and is parsed into an AssemblyResult object.
                    _parser.WritePartsToStreams(
                        streamResult,
                        h =>
                        {
                            string fileName = GetFileNameFromHeaders(h);
                            if (fileName != null)
                            {
                                if (fileName.Equals("meta0.xml", StringComparison.OrdinalIgnoreCase))
                                {
                                    return resultsStream;
                                }

                                return new FileStream(Path.Combine(outputDir, fileName), FileMode.Create);
                            }
                            return Stream.Null;
                        },
                        (new ContentType(result.Content.Headers.ContentType.ToString())).Boundary);

                    if (resultsStream.Position > 0)
                    {
                        resultsStream.Position = 0;
                        var serializer = new XmlSerializer(typeof (AssemblyResult));
                        var asmResult = (AssemblyResult) serializer.Deserialize(resultsStream);
                        if (asmResult != null)
                            adr = Util.ConvertAssemblyResult(template, asmResult, settings.Format);
                    }
                }
                return adr;
            }
        }

        public ComponentInfo GetComponentInfo(Template template, bool includeDialogs, string logRef)
        {
            using (var client = new HttpClient())
            {
                var packageTemplateLocation = (PackageTemplateLocation) template.Location;
                string packageId = packageTemplateLocation.PackageID;

                HttpResponseMessage result =
                    client.GetAsync(
                        string.Format(HostAddress + "/componentinfo/0/{0}/{1}?includedialogs={2}",
                            packageId, HttpUtility.UrlEncode(template.FileName), includeDialogs))
                        .Result;


                HandleErrorStream(result, result.IsSuccessStatusCode);
                Stream streamResult = result.Content.ReadAsStreamAsync().Result;


                using (var stream = (MemoryStream) streamResult)
                {
                    stream.Position = 0;
                    var serializer = new XmlSerializer(typeof (ComponentInfo));
                    return (ComponentInfo) serializer.Deserialize(stream);
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

        public Stream GetInterviewFile(Template template, string fileName, string fileType)
        {
            var packageTemplateLocation = (WebServiceTemplateLocation) template.Location;
            return
                packageTemplateLocation.GetFile(!Path.HasExtension(fileName)
                    ? HttpUtility.UrlEncode(fileName + fileType)
                    : HttpUtility.UrlEncode(fileName));
        }

        public InterviewResult GetInterview(Template template, TextReader answers, InterviewSettings settings,
            IEnumerable<string> markedVariables, string logRef)
        {
            using (var client = new HttpClient())
            {
                var packageTemplateLocation = (PackageTemplateLocation) template.Location;
                string packageId = packageTemplateLocation.PackageID;

                var urlBuilder = new StringBuilder(string.Format(
                    HostAddress +
                    "/interview/0/{0}/{1}?format={2}&markedvariables{3}&tempimageurl={4}&encodeFileNames={5}",
                    packageId, HttpUtility.UrlEncode(template.FileName), settings.Format,
                    markedVariables != null
                        ? "=" + HttpUtility.UrlEncode(string.Join(",", settings.MarkedVariables))
                        : null, HttpUtility.UrlEncode(settings.InterviewFilesUrl), true));

                foreach (var kv in settings.Settings)
                {
                    urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
                }

                StringContent stringContent = answers == null
                    ? new StringContent(String.Empty)
                    : new StringContent(answers.ReadToEnd());

                HttpResponseMessage result = client.PostAsync(urlBuilder.ToString(), stringContent).Result;

                HandleErrorStream(result, result.IsSuccessStatusCode);
                Stream streamResult = result.Content.ReadAsStreamAsync().Result;
                if (!result.IsSuccessStatusCode)
                {
                    using (var streamReader = new StreamReader(streamResult))
                    {
                        string error = streamReader.ReadToEnd();
                        throw new Exception(error);
                    }
                }

                var parser = new MultipartMimeParser();
                string outputDir = Path.GetTempPath();
                Directory.CreateDirectory(outputDir);

                using (var resultsStream = new MemoryStream())
                {
                    // Each part is written to a file whose name is specified in the content-disposition
                    // header, except for the BinaryObject[] part, which has a file name of "meta0.xml",
                    // and is parsed into an BinaryObject[] object.
                    parser.WritePartsToStreams(
                        streamResult,
                        h =>
                        {
                            string fileName = GetFileNameFromHeaders(h);

                            if (!string.IsNullOrEmpty(fileName))
                            {
                                if (fileName.Equals("meta0.xml", StringComparison.OrdinalIgnoreCase))
                                {
                                    return resultsStream;
                                }
                                // The following stream will be closed by the parser
                                return new FileStream(Path.Combine(outputDir, fileName), FileMode.Create);
                            }
                            return Stream.Null;
                        },
                        (new ContentType(result.Content.Headers.ContentType.ToString())).Boundary);

                    if (resultsStream.Position > 0)
                    {
                        resultsStream.Position = 0;
                        var serializer = new XmlSerializer(typeof (BinaryObject[]));
                        var binObjects = (BinaryObject[]) serializer.Deserialize(resultsStream);
                        string interviewContent = Util.ExtractString(binObjects[0]);
                        return new InterviewResult {HtmlFragment = interviewContent};
                    }
                    return null;
                }
            }
        }

        private static void HandleErrorStream(HttpResponseMessage response, bool isSuccessStatusCode)
        {
            if (!isSuccessStatusCode)
            {
                throw new Exception("An Error Has Occured: " + response.ReasonPhrase);
            }
        }

        private static string GetFileNameFromHeaders(Dictionary<string, string> headers)
        {
            string disp;

            if (!headers.TryGetValue("Content-Disposition", out disp)) return null;

            string[] pairs = disp.Split(';');

            foreach (string pair in pairs)
            {
                string trimmed = pair.Trim();

                if (!trimmed.StartsWith("filename*=")) continue;

                int endPos = trimmed.IndexOf("'", StringComparison.Ordinal);

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

    [Serializable]
    internal class ExceptionResponse : Exception
    {
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
    }
}