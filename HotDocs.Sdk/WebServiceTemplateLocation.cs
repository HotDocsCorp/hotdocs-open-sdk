using System;
using System.IO;
using System.Net;
using System.Net.Http;
using HotDocs.Sdk.Server.Contracts;

namespace HotDocs.Sdk
{
    /// <summary>
    ///     Allows interaction with a template package located in a HotDocs Web API Cache, a HotDocs Cloud Services Cache or a
    ///     HotDocs
    ///     Template Hub through Cloud Services
    /// </summary>
    public class WebServiceTemplateLocation : PackageTemplateLocation
    {
        private string _templateDir;

        /// <summary>
        ///     Allows interaction with a template located in a HotDocs Web API cache
        /// </summary>
        /// <param name="packageID">The Package ID of the template located in the cache of the Web API</param>
        /// <param name="hostAddress">The URI for the Web API</param>
        public WebServiceTemplateLocation(string packageID, string hostAddress)
            : base(packageID)
        {
            if (string.IsNullOrEmpty(HostAddress))
                throw new ArgumentNullException("hostAddress");

            HostAddress = hostAddress;

            //Not used in Web API
            SubscriberId = "0";
            SigningKey = string.Empty;
        }

        /// <summary>
        /// </summary>
        /// <param name="packageID">The Package ID of the template located in the cache of the Cloud Services or the Template Hub</param>
        /// <param name="hostAddress">The URI for Cloud Services</param>
        /// <param name="subscriberId">The subscriber ID for a Cloud Services account</param>
        /// <param name="signingKey">The Signing Key for a Cloud Services account</param>
        /// <param name="retrieveFromHub">
        ///     Indicates if the template is stored in a Template Hub and if the package ID used is a
        ///     Version ID or Template ID
        /// </param>
        public WebServiceTemplateLocation(string packageID, string subscriberId, string signingKey,
            RetrieveFromHub retrieveFromHub = RetrieveFromHub.No, string hostAddress = "https://cloud.hotdocs.ws")
            : base(packageID)
        {
            if (string.IsNullOrEmpty(HostAddress))
                throw new ArgumentNullException("hostAddress");

            if (string.IsNullOrEmpty(subscriberId))
                throw new ArgumentNullException("subscriberId");

            if (string.IsNullOrEmpty(signingKey))
                throw new ArgumentNullException(subscriberId);

            HostAddress = hostAddress;
            SubscriberId = subscriberId;
            SigningKey = signingKey;
            RetrieveFromHub = retrieveFromHub;
        }

        public string HostAddress { get; protected set; }

        private string SubscriberId { get; }

        private string SigningKey { get; }

        public RetrieveFromHub RetrieveFromHub { get; }


        public override TemplateLocation Duplicate()
        {
            var location = new WebServiceTemplateLocation(PackageID, HostAddress) {_templateDir = _templateDir};
            return location;
        }

        public override int GetHashCode()
        {
            const int prime = 397;
            var result = HostAddress.ToLower().GetHashCode(); // package path must be case-insensitive
            result = (result*prime) ^ PackageID.GetHashCode(); // combine the hashes
            return result;
        }

        public override bool Equals(TemplateLocation other)
        {
            var otherPackagePathLoc = other as WebServiceTemplateLocation;
            return (otherPackagePathLoc != null)
                   && string.Equals(PackageID, otherPackagePathLoc.PackageID, StringComparison.Ordinal)
                   && string.Equals(HostAddress, otherPackagePathLoc.HostAddress, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(_templateDir, otherPackagePathLoc._templateDir, StringComparison.Ordinal);
        }

        public override Stream GetFile(string fileName)
        {
            return GetFile(fileName, "");
        }

        public override string GetTemplateDirectory()
        {
            throw new NotImplementedException();
        }

        public Stream GetFile(string fileName, string logRef)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri =
                        new Uri(
                            string.Format("{0}/interviewfile/{1}/{2}?filename={3}&billingRef={4}&retrievefromhub={5}",
                                HostAddress, SubscriberId, PackageID, fileName, logRef, RetrieveFromHub))
                };

                var timestamp = DateTime.UtcNow;

                var hmac = HMAC.CalculateHMAC(
                    SigningKey,
                    timestamp,
                    SubscriberId,
                    PackageID,
                    fileName,
                    logRef);

                request.Headers.TryAddWithoutValidation("Authorization", hmac);
                request.Headers.Add("x-hd-date", timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                var result = client.SendAsync(request).Result;

                if (result.IsSuccessStatusCode)
                {
                    return result.Content.ReadAsStreamAsync().Result;
                }

                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception(result.ReasonPhrase);
                }

                throw new Exception(string.Format(
                    "The server returned a '{0}' when searching for the file '{1}'", result.ReasonPhrase, fileName));
            }
        }

        protected override string SerializeContent()
        {
            return PackageID + "|" + HostAddress;
        }

        protected override void DeserializeContent(string content)
        {
            if (content == null)
                throw new ArgumentNullException();

            var tokens = content.Split('|');
            if (tokens.Length != 2)
                throw new Exception("Invalid template location.");

            PackageID = tokens[0];
            HostAddress = tokens[1];
        }

        public override Stream GetPackageStream()
        {
            throw new Exception("The package file stream is not available from the cache");
        }

        public override TemplatePackageManifest GetPackageManifest()
        {
            using (var stream = GetFile("manifest.xml"))
            {
                return TemplatePackageManifest.FromStream(stream);
            }
        }
    }
}