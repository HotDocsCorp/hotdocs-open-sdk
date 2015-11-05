/* Copyright (c) 2013, HotDocs Limited
   Use, modification and redistribution of this source is subject
   to the New BSD License as set out in LICENSE.TXT. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Xml.Serialization;
using HotDocs.Sdk.Server.Contracts;

namespace HotDocs.Sdk.Cloud
{
    /// <summary>
    ///     Extension methods
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        ///     Adds a key-value pair to a dictionary unless the value is null.
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddIfNotNull(this Dictionary<string, string> dict, string key, object value)
        {
            if (value != null)
            {
                dict.Add(key, value.ToString());
            }
        }
    }

    /// <summary>
    ///     The RESTful implementation of the Core Services client.
    /// </summary>
    public sealed partial class RestClient : ClientBase, IDisposable
    {
        #region Constructors

        /// <summary>
        ///     Constructs a Client object.
        /// </summary>
        public RestClient(
            string subscriberId,
            string signingKey,
            string outputDir = null,
            string hostAddress = null,
            string proxyServerAddress = null,
            RetrieveFromHub retrieveFromHub = RetrieveFromHub.No)
            : base(subscriberId, signingKey, hostAddress, "RestfulSvc.svc", proxyServerAddress)
        {
            OutputDir = outputDir ?? _defaultOutputDir;
            SetTcpKeepAlive();
            _retrieveFromHub = retrieveFromHub;

#if DEBUG
            // For debug builds, allow invalid server certificates.
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
#endif
        }

        #endregion

        #region Public properties

        /// <summary>
        ///     The name of the folder where output files will get created.
        /// </summary>
        public string OutputDir { get; set; }

        #endregion

        #region Dispose methods

        /// <summary>
        /// </summary>
        public void Dispose() // Since this class is sealed, we don't need to implement the full dispose pattern.
        {
            if (_parser != null)
            {
                _parser.Dispose();
                _parser = null;
            }
        }

        #endregion

        #region Private static methods

        private static Dictionary<string, string> GetOutputOptionsPairs(OutputOptions outputOptions)
        {
            var pairs = new Dictionary<string, string>();

            var basic = (BasicOutputOptions) outputOptions;
            pairs.AddIfNotNull("Author", basic.Author);
            pairs.AddIfNotNull("Comments", basic.Comments);
            pairs.AddIfNotNull("Company", basic.Company);
            pairs.AddIfNotNull("Keywords", basic.Keywords);
            pairs.AddIfNotNull("Subject", basic.Subject);
            pairs.AddIfNotNull("Title", basic.Title);

            if (outputOptions is PdfOutputOptions)
            {
                var pdf = (PdfOutputOptions) outputOptions;
                pairs.AddIfNotNull("EmbedFonts", pdf.EmbedFonts);
                pairs.AddIfNotNull("PdfA", pdf.PdfA);
                pairs.AddIfNotNull("TaggedPdf", pdf.TaggedPdf);
                pairs.AddIfNotNull("KeepFillablePdf", pdf.KeepFillablePdf);
                pairs.AddIfNotNull("TruncateFields", pdf.TruncateFields);
                pairs.AddIfNotNull("Permissions", pdf.Permissions);
                pairs.AddIfNotNull("OwnerPassword", pdf.OwnerPassword);
                pairs.AddIfNotNull("UserPassword", pdf.UserPassword);
            }
            else if (outputOptions is HtmlOutputOptions)
            {
                pairs.AddIfNotNull("Encoding", ((HtmlOutputOptions) outputOptions).Encoding);
            }
            else if (outputOptions is TextOutputOptions)
            {
                pairs.AddIfNotNull("Encoding", ((TextOutputOptions) outputOptions).Encoding);
            }

            return pairs;
        }

        #endregion

        #region Private methods

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

        #endregion

        #region Private fields

        private readonly string _defaultOutputDir = Path.GetTempPath();
        private MultipartMimeParser _parser = new MultipartMimeParser();
        private readonly RetrieveFromHub _retrieveFromHub;

        #endregion

        #region Public methods

        /// <summary>
        ///     Uploads a package to HotDocs Cloud Services from the specified file path.
        /// </summary>
        /// <include file="../../Shared/Help.xml" path="Help/string/param[@name='packageID']" />
        /// <include file="../../Shared/Help.xml" path="Help/string/param[@name='billingRef']" />
        /// <param name="packageFile">The file path of the package to be uploaded.</param>
        /// <remarks>
        ///     This call throws an exception if the package is already cached in Cloud Services.
        ///     The point of the exception is to discourage consumers from constantly re-uploading the
        ///     same package.  Consumers should upload a package only if:
        ///     1) They have already attempted their request and received a "package not found" error.
        ///     or
        ///     2) They happen to know that the package is not already cached, e.g. the package is new.
        /// </remarks>
        public void UploadPackage(
            string packageID,
            string billingRef,
            string packageFile)
        {
            if (!string.IsNullOrEmpty(packageFile))
            {
                using (var packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
                {
                    UploadPackage(packageID, billingRef, packageStream);
                }
            }
        }

        /// <summary>
        ///     Uploads a package to HotDocs Cloud Services from a stream.
        /// </summary>
        /// <include file="../../Shared/Help.xml" path="Help/string/param[@name='packageID']" />
        /// <include file="../../Shared/Help.xml" path="Help/string/param[@name='billingRef']" />
        /// <param name="packageStream">A stream containing the package to be uploaded.</param>
        /// <remarks>
        ///     This call throws an exception if the package is already cached in Cloud Services.
        ///     The point of the exception is to discourage consumers from constantly re-uploading the
        ///     same package.  Consumers should upload a package only if:
        ///     1) They have already attempted their request and received a "package not found" error.
        ///     or
        ///     2) They happen to know that the package is not already cached, e.g. the package is new.
        /// </remarks>
        public void UploadPackage(
            string packageID,
            string billingRef,
            Stream packageStream)
        {
            try
            {
                var timestamp = DateTime.UtcNow;

                var hmac = HMAC.CalculateHMAC(
                    SigningKey,
                    timestamp,
                    SubscriberId,
                    packageID,
                    null,
                    true,
                    billingRef);

                var urlBuilder = new StringBuilder(string.Format(
                    "{0}/{1}/{2}", EndpointAddress, SubscriberId, packageID));

                if (!string.IsNullOrEmpty(billingRef))
                {
                    urlBuilder.AppendFormat("?billingRef={0}", billingRef);
                }

                var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
                request.Method = "PUT";
                request.ContentType = "application/binary";
                request.Headers["x-hd-date"] = timestamp.ToString("r");
                request.Headers[HttpRequestHeader.Authorization] = hmac;

                if (packageStream.CanSeek)
                {
                    request.ContentLength = packageStream.Length;
                    request.AllowWriteStreamBuffering = false;
                }

                if (!string.IsNullOrEmpty(ProxyServerAddress))
                {
                    request.Proxy = new WebProxy(ProxyServerAddress);
                }

                packageStream.CopyTo(request.GetRequestStream());

                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    // Throw away the response, which will be empty.
                }
            }
            finally
            {
                packageStream.Close();
            }
        }

        /// <summary>
        ///     Returns a list of themes that belong to the current subscriber
        ///     and have the specified prefix.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="billingRef"></param>
        /// <returns></returns>
        public IEnumerable<string> GetThemeList(string prefix, string billingRef)
        {
            var timestamp = DateTime.UtcNow;

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                prefix,
                billingRef);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/theme/{1}", EndpointAddress, SubscriberId));

            if (!string.IsNullOrEmpty(prefix))
            {
                urlBuilder.AppendFormat("/" + prefix);
            }

            if (!string.IsNullOrEmpty(billingRef))
            {
                urlBuilder.AppendFormat("?billingref={0}", billingRef);
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "GET";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            var response = (HttpWebResponse) request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                while (!reader.EndOfStream)
                {
                    yield return reader.ReadLine();
                }
            }
        }

        /// <summary>
        ///     Returns a list of all themes that belong to the current subscriber.
        /// </summary>
        /// <param name="billingRef"></param>
        /// <returns></returns>
        public IEnumerable<string> GetThemeList(string billingRef)
        {
            return GetThemeList(null, billingRef);
        }

        /// <summary>
        ///     Returns the specified theme file as a stream.
        /// </summary>
        /// <param name="themeFileName"></param>
        /// <param name="billingRef"></param>
        /// <returns></returns>
        public Stream GetThemeFile(string themeFileName, string billingRef)
        {
            var timestamp = DateTime.UtcNow;

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                themeFileName,
                billingRef);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/themefile/{1}/{2}", EndpointAddress, SubscriberId, themeFileName));

            if (!string.IsNullOrEmpty(billingRef))
            {
                urlBuilder.AppendFormat("?billingref={0}", billingRef);
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "GET";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            var response = (HttpWebResponse) request.GetResponse();
            return response.GetResponseStream();
        }

        /// <summary>
        ///     Stores the specified theme file in the indicated file path.
        /// </summary>
        /// <param name="themeFileName"></param>
        /// <param name="localFilePath"></param>
        /// <param name="billingRef"></param>
        public void GetThemeFile(string themeFileName, string localFilePath, string billingRef)
        {
            using (var stream = GetThemeFile(themeFileName, billingRef))
            using (var fileStream = File.OpenWrite(localFilePath))
            {
                stream.CopyTo(fileStream);
            }
        }

        /// <summary>
        ///     Uploads a theme file from a stream.
        /// </summary>
        /// <param name="themeFileName"></param>
        /// <param name="stream"></param>
        /// <param name="billingRef"></param>
        public void PutThemeFile(string themeFileName, Stream stream, string billingRef)
        {
            var timestamp = DateTime.UtcNow;

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                themeFileName,
                billingRef);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/themefile/{1}/{2}", EndpointAddress, SubscriberId, themeFileName));

            if (!string.IsNullOrEmpty(billingRef))
            {
                urlBuilder.AppendFormat("?billingref={0}", billingRef);
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "PUT";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;
            request.ContentLength = stream.Length; // Stream must support this.

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            stream.CopyTo(request.GetRequestStream());

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                // Throw away the response, which will be empty.
            }
        }

        /// <summary>
        ///     Uploads a theme from from the specified file path.
        /// </summary>
        /// <param name="themeFileName"></param>
        /// <param name="localFilePath"></param>
        /// <param name="billingRef"></param>
        public void PutThemeFile(string themeFileName, string localFilePath, string billingRef)
        {
            using (var stream = File.Open(localFilePath, FileMode.Open))
            {
                PutThemeFile(themeFileName, stream, billingRef);
            }
        }

        /// <summary>
        ///     Deletes a theme file.
        /// </summary>
        /// <param name="themeName"></param>
        /// <param name="billingRef"></param>
        public void DeleteTheme(string themeName, string billingRef)
        {
            var timestamp = DateTime.UtcNow;

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                themeName,
                billingRef);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/theme/{1}/{2}", EndpointAddress, SubscriberId, themeName));

            if (!string.IsNullOrEmpty(billingRef))
            {
                urlBuilder.AppendFormat("?billingref={0}", billingRef);
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "DELETE";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                // Throw away the response, which will be empty.
            }
        }

        /// <summary>
        ///     Renames a theme file.
        /// </summary>
        /// <param name="themeName"></param>
        /// <param name="newThemeName"></param>
        /// <param name="billingRef"></param>
        public void RenameTheme(string themeName, string newThemeName, string billingRef)
        {
            var timestamp = DateTime.UtcNow;

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                themeName,
                newThemeName,
                billingRef);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/theme/{1}/{2}?rename={3}", EndpointAddress, SubscriberId, themeName, newThemeName));

            if (!string.IsNullOrEmpty(billingRef))
            {
                urlBuilder.AppendFormat("&billingref={0}", billingRef);
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "POST";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;
            request.ContentLength = 0;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                // Throw away the response, which will be empty.
            }
        }

        #endregion

        #region Protected internal implementations of abstract methods from the base class

        /// <summary>
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        protected internal override object TryWithoutAndWithPackage(Func<bool, object> func)
        {
            try
            {
                return func(false);
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                {
                    // No response, so rethrow.
                    throw;
                }

                using (var httpResponse = (HttpWebResponse) ex.Response)
                {
                    if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        return func(true);
                    }

                    using (var stream = httpResponse.GetResponseStream())
                    {
                        var message = new StreamReader(stream).ReadToEnd();
                        if (message == "")
                        {
                            // No error message, so rethrow.
                            throw;
                        }
                        // Create a new exception of the same type and include the error message.
                        // Also include the original exception as the inner exception.
                        var ex2 = (Exception) Activator.CreateInstance(ex.GetType(), message, ex);
                        throw ex2;
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="template"></param>
        /// <param name="answers"></param>
        /// <param name="settings"></param>
        /// <param name="billingRef">
        ///     This parameter lets you specify information that will be included in usage logs for this call.
        ///     For example, you can use a string to uniquely identify the end user that initiated the request and/or the context
        ///     in which the call was made. When you review usage logs, you can then see which end users initiated each request.
        ///     That information could then be used to pass costs on to those end users if desired.
        /// </param>
        /// <param name="uploadPackage">
        ///     Indicates if the package should be uploaded (forcefully) or not. This should only be true
        ///     if the package does not already exist in the Cloud Services cache.
        /// </param>
        /// <returns></returns>
        protected internal override AssemblyResult AssembleDocumentImpl(
            Template template,
            string answers,
            AssembleDocumentSettings settings,
            string billingRef,
            bool uploadPackage)
        {
            if (!(template.Location is PackageTemplateLocation))
                throw new Exception(
                    "HotDocs Cloud Services requires the use of template packages. Please use a PackageTemplateLocation derivative.");
            var packageTemplateLocation = (PackageTemplateLocation) template.Location;

            if (uploadPackage)
            {
                UploadPackage(packageTemplateLocation.PackageID, billingRef, packageTemplateLocation.GetPackageStream());
            }

            var timestamp = DateTime.UtcNow;

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                packageTemplateLocation.PackageID,
                template.FileName,
                false,
                billingRef,
                settings.Format,
                settings.Settings);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/assemble/{1}/{2}/{3}?format={4}&billingref={5}&encodeFileNames={6}&retrievefromhub={7}",
                EndpointAddress, SubscriberId, packageTemplateLocation.PackageID, template.FileName ?? "",
                settings.Format, billingRef, true, _retrieveFromHub));

            if (settings.Settings != null)
            {
                foreach (var kv in settings.Settings)
                {
                    urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
                }
            }

            // Note that the Comments and/or Keywords values, and therefore the resulting URL, could
            // be extremely long.  Consumers should be aware that overly-long URLs could be rejected
            // by Cloud Services.  If the Comments and/or Keywords values cannot be truncated, the
            // consumer should use the SOAP version of the client.
            var outputOptionsPairs = GetOutputOptionsPairs(settings.OutputOptions);

            foreach (var kv in outputOptionsPairs)
            {
                urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "POST";
            request.ContentType = "text/xml";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;
            request.Timeout = 10*60*1000; // Ten minute timeout
            request.ContentLength = answers != null ? answers.Length : 0L;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            if (answers != null)
            {
                var data = Encoding.UTF8.GetBytes(answers);
                request.GetRequestStream().Write(data, 0, data.Length);
            }
            var response = (HttpWebResponse) request.GetResponse();

            Directory.CreateDirectory(OutputDir);
            using (var resultsStream = new MemoryStream())
            {
                // Each part is written to a file whose name is specified in the content-disposition
                // header, except for the AssemblyResult part, which has a file name of "meta0.xml",
                // and is parsed into an AssemblyResult object.
                _parser.WritePartsToStreams(
                    response.GetResponseStream(),
                    h =>
                    {
                        var fileName = GetFileNameFromHeaders(h);
                        if (fileName != null)
                        {
                            if (fileName.Equals("meta0.xml", StringComparison.OrdinalIgnoreCase))
                            {
                                return resultsStream;
                            }

                            return new FileStream(Path.Combine(OutputDir, fileName), FileMode.Create);
                        }
                        return Stream.Null;
                    },
                    (new ContentType(response.ContentType)).Boundary);

                if (resultsStream.Position > 0)
                {
                    resultsStream.Position = 0;
                    var serializer = new XmlSerializer(typeof (AssemblyResult));
                    return (AssemblyResult) serializer.Deserialize(resultsStream);
                }
                return null;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="template"></param>
        /// <param name="answers"></param>
        /// <param name="settings"></param>
        /// <param name="billingRef">
        ///     This parameter lets you specify information that will be included in usage logs for this call.
        ///     For example, you can use a string to uniquely identify the end user that initiated the request and/or the context
        ///     in which the call was made. When you review usage logs, you can then see which end users initiated each request.
        ///     That information could then be used to pass costs on to those end users if desired.
        /// </param>
        /// <param name="uploadPackage">
        ///     Indicates if the package should be uploaded (forcefully) or not. This should only be true
        ///     if the package does not already exist in the Cloud Services cache.
        /// </param>
        /// <returns></returns>
        protected internal override BinaryObject[] GetInterviewImpl(
            Template template,
            string answers,
            InterviewSettings settings,
            string billingRef,
            bool uploadPackage)
        {
            if (!(template.Location is PackageTemplateLocation))
                throw new Exception(
                    "HotDocs Cloud Services requires the use of template packages. Please use a PackageTemplateLocation derivative.");
            var packageTemplateLocation = (PackageTemplateLocation) template.Location;

            if (uploadPackage)
            {
                UploadPackage(packageTemplateLocation.PackageID, billingRef, packageTemplateLocation.GetPackageStream());
            }

            var timestamp = DateTime.UtcNow;

            var interviewImageUrl = string.Empty;
            if (settings != null)
            {
                settings.Settings.TryGetValue("TempInterviewUrl", out interviewImageUrl);
            }
            else
            {
                settings = new InterviewSettings();
            }

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                packageTemplateLocation.PackageID,
                template.FileName,
                false,
                billingRef,
                settings.Format,
                interviewImageUrl,
                settings.Settings);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/interview/{1}/{2}/{3}?format={4}&markedvariables={5}&tempimageurl={6}&billingref={7}&encodeFileNames={8}&retrievefromhub={9}",
                EndpointAddress, SubscriberId, packageTemplateLocation.PackageID, template.FileName, settings.Format,
                settings.MarkedVariables != null ? string.Join(",", settings.MarkedVariables) : null, interviewImageUrl,
                billingRef, true, _retrieveFromHub));

            if (settings.Settings != null)
            {
                foreach (var kv in settings.Settings)
                {
                    urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
                }
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "POST";
            request.ContentType = "text/xml";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;
            request.ContentLength = answers != null ? answers.Length : 0L;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            using (var stream = request.GetRequestStream())
            {
                if (answers != null)
                {
                    var data = Encoding.UTF8.GetBytes(answers);
                    stream.Write(data, 0, data.Length);
                }
            }

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                Directory.CreateDirectory(OutputDir);
                using (var resultsStream = new MemoryStream())
                {
                    // Each part is written to a file whose name is specified in the content-disposition
                    // header, except for the BinaryObject[] part, which has a file name of "meta0.xml",
                    // and is parsed into an BinaryObject[] object.
                    _parser.WritePartsToStreams(
                        response.GetResponseStream(),
                        h =>
                        {
                            var id = GetFileNameFromHeaders(h);
                            if (id != null)
                            {
                                if (id.Equals("meta0.xml", StringComparison.OrdinalIgnoreCase))
                                {
                                    return resultsStream;
                                }

                                // The following stream will be closed by the parser
                                return new FileStream(Path.Combine(OutputDir, id), FileMode.Create);
                            }
                            return Stream.Null;
                        },
                        (new ContentType(response.ContentType)).Boundary);

                    if (resultsStream.Position > 0)
                    {
                        resultsStream.Position = 0;
                        var serializer = new XmlSerializer(typeof (BinaryObject[]));
                        return (BinaryObject[]) serializer.Deserialize(resultsStream);
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="template"></param>
        /// <param name="includeDialogs">Indicates whether or not information about dialogs should be included.</param>
        /// <param name="billingRef">
        ///     This parameter lets you specify information that will be included in usage logs for this call.
        ///     For example, you can use a string to uniquely identify the end user that initiated the request and/or the context
        ///     in which the call was made. When you review usage logs, you can then see which end users initiated each request.
        ///     That information could then be used to pass costs on to those end users if desired.
        /// </param>
        /// <param name="uploadPackage">
        ///     Indicates if the package should be uploaded (forcefully) or not. This should only be true
        ///     if the package does not already exist in the Cloud Services cache.
        /// </param>
        /// <returns></returns>
        protected internal override ComponentInfo GetComponentInfoImpl(
            Template template,
            bool includeDialogs,
            string billingRef,
            bool uploadPackage)
        {
            if (!(template.Location is PackageTemplateLocation))
                throw new Exception(
                    "HotDocs Cloud Services requires the use of template packages. Please use a PackageTemplateLocation derivative.");
            var packageTemplateLocation = (PackageTemplateLocation) template.Location;

            if (uploadPackage)
            {
                UploadPackage(packageTemplateLocation.PackageID, billingRef, packageTemplateLocation.GetPackageStream());
            }

            var timestamp = DateTime.UtcNow;
            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                packageTemplateLocation.PackageID,
                template.FileName,
                false,
                billingRef,
                includeDialogs);

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/componentinfo/{1}/{2}/{3}?includedialogs={4}&billingref={5}&retrievefromhub={6}",
                EndpointAddress, SubscriberId, packageTemplateLocation.PackageID, template.FileName, includeDialogs,
                billingRef, _retrieveFromHub));

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "GET";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }

            var response = (HttpWebResponse) request.GetResponse();

            var serializer = new XmlSerializer(typeof (ComponentInfo));
            var stream = new MemoryStream();
            return (ComponentInfo) serializer.Deserialize(response.GetResponseStream());
        }

        /// <summary>
        /// </summary>
        /// <param name="answers"></param>
        /// <param name="billingRef">
        ///     This parameter lets you specify information that will be included in usage logs for this call.
        ///     For example, you can use a string to uniquely identify the end user that initiated the request and/or the context
        ///     in which the call was made. When you review usage logs, you can then see which end users initiated each request.
        ///     That information could then be used to pass costs on to those end users if desired.
        /// </param>
        /// <returns></returns>
        protected internal override BinaryObject GetAnswersImpl(BinaryObject[] answers, string billingRef)
        {
            throw new NotImplementedException(); // The REST client does not support GetAnswers.
        }

        #endregion
    }
}