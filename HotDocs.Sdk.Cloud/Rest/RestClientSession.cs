/* Copyright (c) 2013, HotDocs Limited
   Use, modification and redistribution of this source is subject
   to the New BSD License as set out in LICENSE.TXT. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using HotDocs.Sdk.Server.Contracts;

namespace HotDocs.Sdk.Cloud
{
    public sealed partial class RestClient : ClientBase
    {
        #region Private properties

        private string EmbeddedEndpointAddress
        {
            get { return new Uri(EndpointAddress).GetLeftPart(UriPartial.Authority) + "/embed"; }
        }

        #endregion

        #region Public methods

        /// <summary>
        ///     Creates a new session for assembling documents using the HotDocs Cloud Services Rest API.
        /// </summary>
        /// <param name="template">The template to use with the session.</param>
        /// <param name="billingRef">
        ///     This parameter lets you specify information that will be included in usage logs for this call.
        ///     For example, you can use a string to uniquely identify the end user that initiated the request and/or the context
        ///     in which the call was made. When you review usage logs, you can then see which end users initiated each request.
        ///     That information could then be used to pass costs on to those end users if desired.
        /// </param>
        /// <param name="answers">The answers to use.</param>
        /// <param name="markedVariables">
        ///     An array of variable names, whose prompts should be "marked" when displayed in an
        ///     interview.
        /// </param>
        /// <param name="interviewFormat">The format to use when displaying an interview.</param>
        /// <param name="outputFormat">The format to use when assembling a document.</param>
        /// <param name="settings">The settings to use with the session.</param>
        /// <param name="theme">The interview theme.</param>
        /// <param name="showDownloadLinks">
        ///     Indicates whether or not links for downloading the assembled document(s) should appear
        ///     at the end of the interview.
        /// </param>
        /// <returns></returns>
        public string CreateSession(
            Template template,
            string billingRef = null,
            string answers = null,
            string[] markedVariables = null,
            InterviewFormat interviewFormat = InterviewFormat.JavaScript,
            OutputFormat outputFormat = OutputFormat.Native,
            Dictionary<string, string> settings = null,
            string theme = null,
            bool showDownloadLinks = true
            )
        {
            return (string) TryWithoutAndWithPackage(
                uploadPackage => CreateSessionImpl(
                    template,
                    billingRef,
                    answers,
                    markedVariables,
                    interviewFormat,
                    outputFormat,
                    settings,
                    theme,
                    showDownloadLinks,
                    uploadPackage)
                );
        }

        /// <summary>
        ///     Resumes a saved session.
        /// </summary>
        /// <param name="state">The serialized state of the interrupted session, i.e. the "snapshot".</param>
        /// <param name="locationGetter">A delegate that takes a package ID and returns the template location.</param>
        /// <returns>A session ID to be passed into the JavaScript HD$.CreateInterviewFrame call.</returns>
        public string ResumeSession(string state, Func<string, PackageTemplateLocation> locationGetter = null)
        {
            if (locationGetter != null)
            {
                return (string) TryWithoutAndWithPackage(
                    uploadPackage => ResumeSessionImpl(state, locationGetter, uploadPackage));
            }
            return ResumeSessionImpl(state, locationGetter, false);
        }

        /// <summary>
        ///     Saves the specified session file to a location on the local disk.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="fileName">The name of the session document to return.</param>
        /// <param name="localPath">The local path where the session document will be saved.</param>
        public void GetSessionDoc(string sessionId, string fileName, string localPath)
        {
            var url = string.Format("{0}/session/{1}/docs/{2}", EmbeddedEndpointAddress, sessionId, fileName);
            using (var webClient = new WebClient())
            {
                webClient.DownloadFile(url, localPath);
            }
        }

        /// <summary>
        ///     Returns an array of assembled documents associated with the session.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <returns>An array of session documents.</returns>
        public string[] GetSessionDocList(string sessionId)
        {
            var url = string.Format("{0}/session/{1}/docs", EmbeddedEndpointAddress, sessionId);
            string list;
            using (var webClient = new WebClient())
            {
                list = webClient.DownloadString(url);
            }
            return list.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        ///     Returns the session state.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <returns>A string containing the session state.</returns>
        public string GetSessionState(string sessionId)
        {
            var url = string.Format("{0}/session/{1}/state", EmbeddedEndpointAddress, sessionId);
            using (var webClient = new WebClient())
            {
                return webClient.DownloadString(url);
            }
        }

        #endregion

        #region Private methods

        private string CreateSessionImpl(
            Template template,
            string billingRef,
            string answers,
            string[] markedVariables,
            InterviewFormat interviewFormat,
            OutputFormat outputFormat,
            Dictionary<string, string> settings,
            string theme,
            bool showDownloadLinks,
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
                billingRef,
                interviewFormat,
                outputFormat,
                settings); // Additional settings = null for this app

            var urlBuilder = new StringBuilder(string.Format(
                "{0}/newsession/{1}/{2}?interviewformat={3}&outputformat={4}&retrieveFromHub={5}",
                EmbeddedEndpointAddress, SubscriberId, packageTemplateLocation.PackageID,
                interviewFormat, outputFormat, _retrieveFromHub));

            if (markedVariables != null && markedVariables.Length > 0)
            {
                urlBuilder.AppendFormat("&markedvariables={0}", string.Join(",", markedVariables));
            }

            if (!string.IsNullOrEmpty(theme))
            {
                urlBuilder.AppendFormat("&theme={0}", theme);
            }

            if (!string.IsNullOrEmpty(billingRef))
            {
                urlBuilder.AppendFormat("&billingref={0}", billingRef);
            }

            if (showDownloadLinks)
            {
                urlBuilder.Append("&showdownloadlinks=true");
            }

            if (settings != null)
            {
                foreach (var kv in settings)
                {
                    urlBuilder.AppendFormat("&{0}={1}", kv.Key, kv.Value ?? "");
                }
            }

            var request = (HttpWebRequest) WebRequest.Create(urlBuilder.ToString());
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;
            byte[] data = null;
            if (answers != null)
            {
                data = Encoding.UTF8.GetBytes(answers);
            }
            request.ContentLength = data != null ? data.Length : 0;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }
            else
            {
                request.Proxy = null;
            }

            var stream = request.GetRequestStream();
            if (data != null)
            {
                stream.Write(data, 0, data.Length);
            }
            var response = (HttpWebResponse) request.GetResponse();
            var reader = new StreamReader(response.GetResponseStream());
            return reader.ReadLine();
        }

        private string ResumeSessionImpl(string state, Func<string, PackageTemplateLocation> locationGetter,
            bool uploadPackage)
        {
            if (string.IsNullOrEmpty(state))
            {
                throw new Exception("A session state string must be provided in order to resume a session.");
            }

            if (uploadPackage)
            {
                var base64 = state.Split('#')[0];
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var jss = new JavaScriptSerializer();
                var stateDict = jss.Deserialize<dynamic>(json);
                string packageID = stateDict["PackageID"];
                string billingRef = stateDict["BillingRef"];

                UploadPackage(packageID, billingRef, locationGetter(packageID).GetPackageStream());
            }

            var timestamp = DateTime.UtcNow;

            var hmac = HMAC.CalculateHMAC(
                SigningKey,
                timestamp,
                SubscriberId,
                state);

            var url = string.Format("{0}/resumesession/{1}", EmbeddedEndpointAddress, SubscriberId);

            var request = (HttpWebRequest) WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Headers["x-hd-date"] = timestamp.ToString("r");
            request.Headers[HttpRequestHeader.Authorization] = hmac;

            var data = Encoding.UTF8.GetBytes(state);
            request.ContentLength = data.Length;

            if (!string.IsNullOrEmpty(ProxyServerAddress))
            {
                request.Proxy = new WebProxy(ProxyServerAddress);
            }
            else
            {
                request.Proxy = null;
            }

            var stream = request.GetRequestStream();
            stream.Write(data, 0, data.Length);

            var response = (HttpWebResponse) request.GetResponse();
            var reader = new StreamReader(response.GetResponseStream());
            return reader.ReadLine();
        }

        #endregion
    }
}