/* Copyright (c) 2013, HotDocs Limited
   Use, modification and redistribution of this source is subject
   to the New BSD License as set out in LICENSE.TXT. */

using System;
using System.Collections.Generic;
using System.Configuration;
using HotDocs.Sdk.Server.Contracts;

namespace HotDocs.Sdk
{
    /// <summary>
    ///     <c>Tristate</c> is a way to represent boolean values and allow "default" values if defined elsewhere
    /// </summary>
    public enum Tristate
    {
        /// <summary>
        ///     A <c>Default</c> value means the current setting is defined somewhere else, such as on HotDocs server.
        /// </summary>
        Default,

        /// <summary>
        ///     a <c>True</c> value means the respective boolean setting evaluates to the "true" value.
        /// </summary>
        True,

        /// <summary>
        ///     a <c>False</c> value means the respective boolean setting evaluates to the "false" value.
        /// </summary>
        False
    }

    /// <summary>
    ///     InterviewSettings encapsulates all the settings that can be specified when requesting HotDocs browser-based
    ///     interviews.
    /// </summary>
    public class InterviewSettings : Settings
    {
        #region Constructors

        /// <summary>
        ///     Contruct an InterviewSettings object with the default settings.
        ///     Four parameters are required for any HotDocs Server interview request. The default (parameterless) constructor
        ///     REQUIRES that these four parameters have values that can be found in the host application's web.config file.
        ///     If any required value is not found there, the constructor will throw an exception.
        /// </summary>
        public InterviewSettings()
            : this(Default)
        {
            ValidateRequiredProperties();
        }

        /// <summary>
        ///     This constructor accepts values for the four "required" interview settings. HotDocs Server is unable to generate
        ///     a functional browser-based interview without meaningful values for these five parameters.
        /// </summary>
        /// <param name="postInterviewUrl">
        ///     The URL to which answers will be posted when the user finishes the interview. This page
        ///     is commonly known as the "disposition" page.
        /// </param>
        /// <param name="interviewRuntimeUrl">
        ///     The URL from which the browser interview will request the common runtime script files
        ///     used by the interview.
        /// </param>
        /// <param name="styleSheetUrl">The URL of the stylesheet to use with the interview.</param>
        /// <param name="interviewFileUrl">
        ///     The URL from which the browser interview will request template-specific files required by the interview at runtime.
        ///     These files include the interview definitions for the main and inserted templates, as well as images used on
        ///     dialogs.
        /// </param>
        public InterviewSettings(string postInterviewUrl, string interviewRuntimeUrl, string styleSheetUrl,
            string interviewFileUrl)
            : this(Default)
        {
            PostInterviewUrl = postInterviewUrl; // "disposition" page
            InterviewRuntimeUrl = interviewRuntimeUrl;
                // formerly known as "JavaScriptUrl"; base Urls in host app to provide required files to browser interview
            StyleSheetUrl = styleSheetUrl;
            InterviewFilesUrl = interviewFileUrl;

            ValidateRequiredProperties();
        }

        /// <summary>
        ///     Copy constructor -- this constructor is invoked to make a new copy of the (static) Default GetInterviewOptions
        ///     (as originally read from web.config).
        /// </summary>
        /// <param name="source">The source settings to copy.</param>
        private InterviewSettings(InterviewSettings source)
        {
            // Copy the four required settings.
            PostInterviewUrl = source.PostInterviewUrl;
            InterviewRuntimeUrl = source.InterviewRuntimeUrl;
            StyleSheetUrl = source.StyleSheetUrl;
            InterviewFilesUrl = source.InterviewFilesUrl;

            // Copy the rest of the (optional) interview settings.
            DocumentPreviewUrl = source.DocumentPreviewUrl;
            SaveAnswersUrl = source.SaveAnswersUrl;
            AnswerFileDataServiceUrl = source.AnswerFileDataServiceUrl;
            if (source.CustomDataSources != null)
                CustomDataSources = new Dictionary<string, string>(source.CustomDataSources,
                    StringComparer.OrdinalIgnoreCase);

            AddHdMainDiv = source.AddHdMainDiv;
            RoundTripUnusedAnswers = source.RoundTripUnusedAnswers;

            Format = source.Format;
            ThemeName = source.ThemeName;
            Title = source.Title;
            Locale = source.Locale;
            NextFollowsOutline = source.NextFollowsOutline;
            ShowAllResourceButtons = source.ShowAllResourceButtons;
            DisableDocumentPreview = source.DisableDocumentPreview;
            DisableSaveAnswers = source.DisableSaveAnswers;
            DisableAnswerSummary = source.DisableAnswerSummary;
            AnswerSummary = new AnswerSummaryOptions(source.AnswerSummary);

            DefaultUnansweredFormat = source.DefaultUnansweredFormat;
            HonorCmpUnansweredFormat = source.HonorCmpUnansweredFormat;
            DefaultDateFormat = source.DefaultDateFormat;
        }

        /// <summary>
        ///     Private constructor for reading defaults from web.config.
        ///     This constructor should only be invoked the first time the defaults are required.
        /// </summary>
        /// <param name="readDefaults">Indicates if defaults should be read or not.</param>
        private InterviewSettings(bool readDefaults)
        {
            // Read each of the four required settings.
            PostInterviewUrl = ConfigurationManager.AppSettings["PostInterviewUrl"]; // "disposition" page
            InterviewRuntimeUrl = ConfigurationManager.AppSettings["InterviewRuntimeUrl"];
                // formerly known as "JavaScriptUrl"; base Urls in host app to provide required files to browser interviews
            StyleSheetUrl = ConfigurationManager.AppSettings["StyleSheetUrl"];
            InterviewFilesUrl = ConfigurationManager.AppSettings["GetInterviewFileUrl"];

            ThemeName = ConfigurationManager.AppSettings["InterviewTheme"];

            // Urls in host app to support optional interview features
            DocumentPreviewUrl = ConfigurationManager.AppSettings["DocumentPreviewUrl"];
            SaveAnswersUrl = ConfigurationManager.AppSettings["SaveAnswersUrl"];
            AnswerFileDataServiceUrl = ConfigurationManager.AppSettings["AnswerFileDataService"];
            var customDataSourcesSection =
                ConfigurationManager.GetSection("customDataSources") as CustomDataSourcesSection;
            if (customDataSourcesSection != null)
            {
                CustomDataSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataSourceElement ds in customDataSourcesSection.DataSources)
                {
                    CustomDataSources[ds.Name] = ds.Address;
                }
            }

            // other settings having to do with the interview request or structure, rather than its user-observable behavior
            AddHdMainDiv = Util.ReadConfigurationTristate("AddHdMainDiv", Tristate.True);
            RoundTripUnusedAnswers = Util.ReadConfigurationBoolean("RoundTripUnusedAnswers", true);

            // other interview settings
            Format = Util.ReadConfigurationEnum("InterviewFormat", InterviewFormat.Unspecified);
            if (string.IsNullOrEmpty(ThemeName))
                ThemeName = Util.ReadConfigurationString("ThemeName") ?? "hdsuser";
            Title = Util.ReadConfigurationString("InterviewTitle");
            Locale = Util.ReadConfigurationString("InterviewLocale");
            NextFollowsOutline = Util.ReadConfigurationTristate("NextFollowsOutline", Tristate.Default);
            ShowAllResourceButtons = Util.ReadConfigurationTristate("ShowAllResourceButtons", Tristate.Default);
            DisableDocumentPreview = Util.ReadConfigurationBoolean("DisableDocumentPreview", false);
            DisableSaveAnswers = Util.ReadConfigurationBoolean("DisableSaveAnswers", false);
            DisableAnswerSummary = Util.ReadConfigurationTristate("DisableAnswerSummary", Tristate.Default);
            AnswerSummary = AnswerSummaryOptions.Default;

            // "shared" or "common" properties (these are also AssemblyOptions)
            DefaultUnansweredFormat = Util.ReadConfigurationEnum("DefaultUnansweredFormat", UnansweredFormat.Default);
            HonorCmpUnansweredFormat = Util.ReadConfigurationTristate("HonorCmpUnansweredFormat", Tristate.Default);
            DefaultDateFormat = ConfigurationManager.AppSettings["DefaultDateFormat"];
        }

        static InterviewSettings()
        {
            s_default = null;
        }

        /// <summary>
        ///     Ensures that the required interview settings have a value.
        /// </summary>
        protected void ValidateRequiredProperties()
        {
            if (PostInterviewUrl == null)
                throw new ArgumentException("PostInterviewUrl must be specified in GetInterviewOptions",
                    "PostInterviewUrl");
            if (InterviewRuntimeUrl == null)
                throw new ArgumentException("InterviewRuntimeUrl must be specified in GetInterviewOptions",
                    "InterviewRuntimeUrl");
            if (StyleSheetUrl == null)
                throw new ArgumentException("StyleSheetUrl must be specified in GetInterviewOptions", "StyleSheetUrl");
            if (InterviewFilesUrl == null)
                throw new ArgumentException("InterviewFilesUrl must be specified in GetInterviewOptions",
                    "InterviewFilesUrl");
        }

        private static InterviewSettings s_default;

        /// <summary>
        ///     Returns the default parameters defined
        /// </summary>
        public static InterviewSettings Default
        {
            get
            {
                // Avoid reading defaults out of the config file until/unless they are actually needed
                if (s_default == null)
                    s_default = new InterviewSettings(true);
                return s_default;
            }
            set { s_default = value; }
        }

        #endregion

        #region Required Settings

        // Urls to handlers in the host app that are REQUIRED in order to retrieve a functional interview

        /// <summary>
        ///     The url to which finished interview answers will be posted.
        ///     The page residing at this url is sometimes traditionally referred to as the "disposition page" -- since it is
        ///     most often used to inform the user about the disposition (outcome) of the data they submitted.
        ///     <para>A value for this property is required by the SDK.</para>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This property is equivalent to the FormActionUrl property in the HotDocs Server API and the
        ///         FormActionUrl setting in Core Services.
        ///     </para>
        ///     <para>
        ///         As with all properties of GetInterviewOptions and AssemblyOptions, a default value for this property
        ///         can be specified in your web app's web.config file.
        ///     </para>
        /// </remarks>
        public string PostInterviewUrl
        {
            get { return GetSettingString("FormActionUrl"); }
            set { SetSettingString("FormActionUrl", value); }
        }

        /// <summary>
        ///     The base Url from which HotDocs interview runtime files (including hdloader.js and other JavaScript
        ///     files and/or Silverlight DLLs) will be fetched.
        ///     To avoid security errors or the need for cross-domain policy files, the host application should
        ///     serve these files itself (from its own domain name).
        ///     <para>A value for this property is required by the SDK.</para>
        /// </summary>
        /// <remarks>
        ///     <para>TODO: document the syntax of the query string, including explanation of the %VERSION% macro.</para>
        ///     <para>
        ///         This property is equivalent to the HotDocsJavaScriptUrl property in the HotDocs Server API
        ///         and the "HotDocsJsUrl" setting in Core Services.
        ///     </para>
        ///     <para>
        ///         As with all properties of GetInterviewOptions and AssemblyOptions, a default value for this property
        ///         can be specified in your web app's web.config file.
        ///     </para>
        /// </remarks>
        public string InterviewRuntimeUrl
        {
            get { return GetSettingString("HotDocsJsUrl"); }
            set { SetSettingString("HotDocsJsUrl", value); }
        }

        /// <summary>
        ///     The base URL from which the interview will request interview definitions and dialog element images.
        ///     For JavaScript interviews, interview definitions are JavaScript files; for Silverlight interviews, interview
        ///     definitions are compiled DLLs. HotDocs generates these interview definitions, but a host application must
        ///     deliver them from the host application's own domain name.
        ///     <para>A value for this property is required by the SDK.</para>
        /// </summary>
        public string InterviewFilesUrl
        {
            get { return _interviewFilesUrl; }
            set
            {
                _interviewFilesUrl = value;

                SetSettingString("InterviewDefUrl", value);
            }
        }

        private string _interviewFilesUrl;

        /// <summary>
        ///     The base Url from which interview style sheets and graphics will be requested by the web browser.
        ///     <para>A value for this property is required by the SDK.</para>
        /// </summary>
        /// <remarks>
        ///     <para>TODO: document the syntax of the query string, including explanation of the %VERSION% macro.</para>
        ///     <para>JavaScript-based interviews require two style sheets and an image:</para>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>The "User" CSS style sheet:  InterviewRuntimeUrl + ThemeName + ".css"</description>
        ///         </item>
        ///         <item>
        ///             <description>The interview sprite image:  InterviewRuntimeUrl + ThemeName + ".png"</description>
        ///         </item>
        ///         <item>
        ///             <description>The "System" CSS style sheet:  InterviewRuntimeUrl + "hdssystem.css"</description>
        ///         </item>
        ///     </list>
        ///     Silverlight-based interviews require the above items plus an additional third style sheet:
        ///     <list type="bullet">
        ///         <item>
        ///             <description>The XAML style sheet:  InterviewRuntimeUrl + ThemeName + ".xaml"</description>
        ///         </item>
        ///     </list>
        ///     <para>
        ///         You can switch interviews to different "themes" or appearances by changing the ThemeName
        ///         property, but of course you must choose a ThemeName for which appropriate files exist.
        ///     </para>
        ///     <para>
        ///         As with all properties of GetInterviewOptions and AssemblyOptions, a default value for this property
        ///         can be specified in your web app's web.config file.
        ///     </para>
        ///     <para>
        ///         This is related (but not equivalent) to HotDocsCSSUrl in the HotDocs Server API and the HotDocsCssUrl
        ///         setting in Core Services.  These other settings defines the *full* URL to the CSS "User" style sheet.
        ///         In the HotDocs Open SDK, that same full URL would be defined as InterviewRuntimeUrl + ThemeName + ".css".
        ///     </para>
        /// </remarks>
        public string StyleSheetUrl
        {
            get { return _stylesheetUrl; }
            set
            {
                _stylesheetUrl = value;

                // Update the HotDocsCssUrl setting in the dictionary since it is a combination of ThemeName and StyleSheetUrl.
                SetSettingString("HotDocsCssUrl", (value ?? "") + "/" + ThemeName + ".css");
            }
        }

        private string _stylesheetUrl;

        #endregion

        #region Optional Settings

        // Urls in host app to support optional interview features

        /// <summary>
        ///     The URL to which answers will be posted when a browser interview requests an HTML document preview.
        ///     The HTTP response returned by that POST will be displayed to user as the preview.
        ///     <para>If not specified, this property defaults to null (which omits the document preview feature).</para>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This property is equivalent to the DocumentPreviewUrl property in the HotDocs Server API
        ///         and the DocPreviewUrl setting in Core Services.
        ///     </para>
        ///     <para>The following are field values that may be posted to this URL:</para>
        ///     <list type="numbered">
        ///         <item>
        ///             <para>
        ///                 Answers are sent from the browser in a set of "HDInfo" field values. Usually, all answer content
        ///                 will be contained in a single HDInfo value, but if the answer content is large, it may be broken
        ///                 up across several HDInfo values which need to be concatenated back together before use. You can use the
        ///                 static InterviewResponse.GetAnswers method to read and join these values from the request
        ///                 automatically.
        ///                 These answers may be overlayed on any answers maintained on the server with a call to
        ///                 HotDocs.Sdk.AnswerCollection.OverlayXml, for example.
        ///                 The resulting combined answer collection may then used to assemble a document for a preview.
        ///             </para>
        ///         </item>
        ///         <item>
        ///             The "InlineImages" field's value is "true" if the requesting browser supports HTML with images
        ///             included as embedded URIs.
        ///         </item>
        ///     </list>
        /// </remarks>
        public string DocumentPreviewUrl
        {
            get { return GetSettingString("DocPreviewUrl"); }
            set { SetSettingString("DocPreviewUrl", value); }
        }

        /// <summary>
        ///     The URL to which answers will be posted when user clicks the Save Answers button in the browser interview.
        ///     The HTTP response returned to the browser will be displayed to user.
        ///     <para>If no value is specified, this property defaults to null (which omits the Save Answers feature).</para>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This property is equivalent to the SaveAnswersPageUrl property in the HotDocs Server API
        ///         and the SaveAnswersPageUrl setting in Core Services.
        ///     </para>
        ///     <para>
        ///         Answers are sent from the browser in a set of "HDInfo" field values. Usually, all answer content
        ///         will be contained in a single HDInfo value, but if the answer content is large, it may be broken
        ///         up across several HDInfo values which need to be concatenated back together before use. You can use the
        ///         static InterviewResponse.GetAnswers method to read and join these values from the request automatically.
        ///     </para>
        /// </remarks>
        public string SaveAnswersUrl
        {
            get { return GetSettingString("SaveAnswersPageUrl"); }
            set { SetSettingString("SaveAnswersPageUrl", value); }
        }

        /// <summary>
        ///     The URL of an OData data service to which requests for HotDocs answer file-based answer sources will be sent.
        ///     Defaults to null (which disables the ability of browser interviews to request answer file-based answer sources).
        /// </summary>
        /// <remarks>
        ///     TODO: Document the query string used by the browser interview when performing an HTTP GET from this URL.
        /// </remarks>
        public string AnswerFileDataServiceUrl { get; set; }

        /// <summary>
        ///     A collection of custom answer sources. The dictionary Key is the name of a custom data source;
        ///     the Value is the Url of the OData data service that will return the desired data. Defaults to an empty dictionary.
        /// </summary>
        /// <remarks>
        ///     TODO: Make sure custom data sources are better documented.
        /// </remarks>
        public Dictionary<string, string> CustomDataSources { get; }

        #endregion

        #region Other Settings

        // other settings having to do with the interview request or structure, rather than its user-observable behavior

        /// <summary>
        ///     Whether HotDocs should include in the returned HTML fragment an HTML DIV with its id set to "hdMainDiv".
        ///     When a browser interview initializes, the interview is created dynamically inside this placeholder DIV.
        ///     Your host application can choose to include that DIV within the host page itself (in which case you
        ///     would set AddHdMainDiv to false), but by default AddHdMainDiv is set to true, meaning that HotDocs will
        ///     include this placeholder DIV in the interview HTML fragment.
        /// </summary>
        /// <remarks>
        ///     NOTE: This property is ignored if the SDK is communicating with HotDocs Server (either locally or
        ///     at a web service).  HotDocs Server always honors the setting specified in the HotDocs Server
        ///     Management Console, and does not allow individual interview requests to override that default value.
        /// </remarks>
        public Tristate AddHdMainDiv
        {
            get { return GetSettingTristate("AddHdMainDiv"); }
            set { SetSettingString("AddHdMainDiv", TristateToString(value)); }
        }

        /// <summary>
        ///     Whether the generated interview will include (and round-trip) the entire answer collection provided
        ///     to the GetInterview call, or only those answers needed by the interview.  Equivalent to the
        ///     "StatelessInterview" property on the HotDocs Server API.
        /// </summary>
        /// <remarks>
        ///     NOTE: If the SDK is communicating with HotDocs Server (either locally or at a web service),
        ///     choosing Default for this setting is equivalent to False. When used with HotDocs Core Services,
        ///     however, Default is equivalent to True. Beware.
        /// </remarks>
        public bool RoundTripUnusedAnswers { get; set; }

        #endregion

        #region Yet More Settings

        /// <summary>
        ///     List of marked variables
        /// </summary>
        public IEnumerable<string> MarkedVariables { get; set; }

        /// <summary>
        ///     Collection of settings that can be specified when requesting a HotDocs interview.
        ///     Values may be provided for the following settings:
        ///     <list type="table">
        ///         <listheader>
        ///             <term>Setting Name</term>
        ///             <description>Description</description>
        ///         </listheader>
        ///         <item>
        ///             <term>HotDocsJsUrl</term>
        ///             <description>
        ///                 The URL from which the web browser should request the HotDocs interview
        ///                 runtime files (including hdloader.js and other JavaScript files and/or Silverlight DLLs).
        ///                 To avoid security errors or complex cross-domain policy files, the host application must be able
        ///                 to serve these files itself (from its own domain name).
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>HotDocsCssUrl</term>
        ///             <description>
        ///                 The base URL from which the web browser should request the interview style sheet and related
        ///                 components.
        ///                 The setting itself designates the full URL to the "user" CSS style sheet (for styling JavaScript
        ///                 interviews).
        ///                 The default user style sheet is named "hdsuser.css", but customized style sheets can have any base file
        ///                 name.
        ///                 Browser interviews rely on this setting to derive URLs for several other style-related components:
        ///                 (1) For HotDocs 11 and later, a sprite-style image must be available at this same base URL (but with a
        ///                 .png file
        ///                 extension) containing all the interview images at expected offsets within the sprite;
        ///                 (2) For Silverlight interviews, a XAML style sheet will be requested from this same base URL (but with
        ///                 a .xaml file extension);
        ///                 (3) The "system" style sheet (hdssystem.css) must also be available at the same base URL (differing by
        ///                 file name).
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>HotDocsImageUrl</term>
        ///             <description>
        ///                 In HotDocs 11, this setting is ignored since interview images (used for all buttons, tree nodes, etc.)
        ///                 were replaced with a single sprite-style image at the same base URL as the user style sheet.
        ///                 For HotDocs 10.x interviews, this is the base URL from which each of the standard images will be
        ///                 requested.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>InterviewDefUrl</term>
        ///             <description>
        ///                 The base URL from which the interview runtime (running in the browser) will request interview
        ///                 definition files.
        ///                 For JavaScript interviews, interview definitions are JavaScript files; for Silverlight interviews,
        ///                 interview
        ///                 definitions are compiled DLLs. HotDocs generates these interview definitions, but a host application
        ///                 must deliver them
        ///                 from the host application's own domain name.
        ///                 At runtime HotDocs appends the file name and type of the requested interview definition to the supplied
        ///                 string and
        ///                 issues an HTTP GET to the resulting URL.
        ///                 Note that for HotDocs 10.x interviews, this URL was only used to deliver Silverlight interview
        ///                 definitions.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>DocPreviewUrl</term>
        ///             <description>
        ///                 If this URL is null or empty, users will not be able to request document previews from within browser
        ///                 interviews.
        ///                 Otherwise this URL should be the location of a handler from which a browser interview can request a
        ///                 document preview.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>SaveAnswersPageUrl</term>
        ///             <description>
        ///                 The URL of a page within your host application that allows saving answers during an interview.
        ///                 If this URL is null or empty, HotDocs will not show a Save Answers button on the interview toolbar.
        ///                 Otherwise the browser will POST interview answers to this page in response to the user clicking the
        ///                 Save Answers button.
        ///                 The page should return an HTML response that will be displayed within an IFRAME in the interview.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>FormActionUrl</term>
        ///             <description>
        ///                 The URL to which interview answers are posted when the user clicks the Finish button at the end of an
        ///                 interview.
        ///                 This is sometimes referred to as the "Disposition Page"; it typically proceeds to perform a document
        ///                 assembly based on
        ///                 those answers and may give the user options to decide whether to return to the interview, proceed to
        ///                 another interview,
        ///                 or go do something else.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>TempInterviewUrl</term>
        ///             <description>
        ///                 The base URL from which the browser interview can request graphics or other peripheral files necessary
        ///                 for an interview in progress. For example, when an interview is displayed that uses Image dialog
        ///                 elements, your host
        ///                 application must know about those image files, and be able to deliver them in response to requests to
        ///                 this URL.
        ///                 The interview will append the name of the image file it needs to the end of this URL when it makes the
        ///                 request.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>RequestTimeout</term>
        ///             <description>
        ///                 (number of seconds) By default, HotDocs Core Services will assume a request to produce an interview has
        ///                 hung if the
        ///                 response takes longer than (a certain time). Setting RequestTimeout to another value can raise or lower
        ///                 the
        ///                 threshhold before HotDocs gives up on a request.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AddHdMainDiv</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 In order to embed HotDocs interviews within a host application web page, HotDocs looks for a DIV
        ///                 with the id of "hdMain" somewhere on the page, and uses that location to embed the interview.<br />
        ///                 This setting indicates whether or not HotDocs should include the HDMain DIV element on its own
        ///                 (appended to the HTML content returned from GetInterview), or whether your host application
        ///                 has already placed a DIV with the appropiate ID for HotDocs to use.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>TemplateTitleOverride</term>
        ///             <description>
        ///                 By default, HotDocs interviews show a template's title in the interview toolbar.
        ///                 Use this setting to override the text shown in the toolbar.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>InterviewLocale</term>
        ///             <description>
        ///                 HotDocs interviews are assumed to use the English (US) language by default.
        ///                 If you wish to display interviews in other languages, you should set this property to the name of
        ///                 an appropriately configured HotDocs Browser Language Module.  See (link) for more information
        ///                 on Browser Language Modules.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>OutlineInOrder</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 Corresponds to the "Next Dialog Follows Outline" entry on the "Navigate" menu within desktop HotDocs.
        ///                 When this setting is "true" (the default), dialogs are navigated in a top-down manner, with the Next
        ///                 command
        ///                 always navigating the interview outline from top to bottom. When this setting is "false", behavior of
        ///                 the Next
        ///                 command depends on the current location: choosing Next from a top-level dialog always proceeds to the
        ///                 next
        ///                 top-level dialog (skipping child dialogs, if any), and choosing Next from a child dialog always
        ///                 navigates back
        ///                 to its parent.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>ShowAllResourceButtons</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 HotDocs interviews display a resource button next to input fields for which the template author has
        ///                 provided
        ///                 additional resource information. This setting determines whether resource buttons will be visible
        ///                 simultaneously for all such
        ///                 input fields, or whether resource buttons should appear only for the current-focused input field.
        ///                 Corrresponds to the HotDocs Options &gt; Interviews and Dialogs &gt; Show answer field resource button
        ///                 option in desktop HotDocs.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>DisableAnswerSummary</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 HotDocs interviews allow users to request answer summaries (from the interview toolbar) by default.
        ///                 This option can remove the answer summary toolbar button.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsMaximumMultipleChoice</term>
        ///             <description>
        ///                 No longer supported. This setting was used to set the maximum number of selected multiple choice
        ///                 options that
        ///                 could be displayed in an answer summary. HotDocs now shows ALL selected multiple choice options in
        ///                 answer summaries.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsNumberOfColumns</term>
        ///             <description>
        ///                 "1" or "2"<br />
        ///                 Answer summaries can be formatted in 1-column or 2-column format.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsIndentPrompts</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 Indents the left margin of each prompt with respect to dialog titles. Applicable only to 1-column
        ///                 answer summary layout.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsBulletPrompts</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 Precedes each prompt with a bullet character. Applicable only to 1-column answer summary layout.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsIndentAnswers</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 Indents the left margin of each answer with respect to its prompt. Applicable only to 1-column answer
        ///                 summary layout.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsBulletAnswers</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 Precedes each answer with a bullet character. Applicable only to 1-column answer summary layout.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsBorders</term>
        ///             <description>
        ///                 "N", "P" or "S"<br />
        ///                 Sets the border style for the answer summary table:  [N]one, [P]lain or [S]culpted. Applicable only to
        ///                 2-column answer summary layout.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>AsPercentOfWidthForPrompt</term>
        ///             <description>
        ///                 Recommended range of "30" to "70"<br />
        ///                 Specifies the percentage of the total interview width reserved for prompts.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>UnansweredFormat</term>
        ///             <description>
        ///                 "Nothing" / "Underscores" / "Asterisks" / "[Variable]" / "*** Variable ***"<br />
        ///                 Dictates what to merge (by default) when a value for which no answer is available is merged into text
        ///                 within the interview.
        ///                 Corresponds to the "Unanswered variable placeholder" HotDocs setting
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>HonorCmpUnansFormat</term>
        ///             <description>
        ///                 "true" or "false"<br />
        ///                 Determines whether template author preferences (as specified in a HotDocs component file) should
        ///                 override the
        ///                 default UnansweredFormat specified by the host application. ("true" corresponds to the behavior of
        ///                 desktop HotDocs.)
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>DefaultDateFormat</term>
        ///             <description>
        ///                 "June 3, 1990", etc.<br />
        ///                 Specifies the default format example to be used when merging date values into text.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>MaxRepeatCount</term>
        ///             <description>
        ///                 e.g. "100"<br />
        ///                 The number of iterations through a WHILE loop HotDocs will perform before generating an error.
        ///                 For performance reasons, HotDocs Core Services may ignore values over a certain threshhold.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>MaxRecursionDepth</term>
        ///             <description>
        ///                 e.g. "100"<br />
        ///                 The maximum depth that may be reached by a recursive script in HotDocs before generating an error.
        ///                 For performance reasons, HotDocs Core Services may ignore values over a certain threshhold.
        ///             </description>
        ///         </item>
        ///     </list>
        ///     <para>
        ///         When you do not explicitly provide values for any setting, the default values specified
        ///         in the HotDocs Core Services Administrative Portal will be used.
        ///     </para>
        ///     <para>TODO: document the %%VERSION% macro for the URL settings</para>
        ///     <para>TODO: document which URL settings above will be called with specific parameters on the query string.</para>
        /// </summary>
        public Dictionary<string, string> Settings
        {
            get { return _settings; }
        }

        /// <summary>
        ///     Dictates whether the returned interview will use the JavaScript runtime or the Silverlight runtime.
        ///     The default behavior is "Default", which leaves the choice up to the HotDocs Server from which you
        ///     request the interview.
        /// </summary>
        public InterviewFormat Format { get; set; }

        private string _themeName;

        /// <summary>
        ///     The base file name of requested style sheet and sprite image; defaults to "hdsuser"
        /// </summary>
        public string ThemeName
        {
            get { return _themeName; }
            set
            {
                _themeName = value;

                // Update the HotDocsCssUrl setting in the dictionary since it is a combination of ThemeName and StyleSheetUrl.
                SetSettingString("HotDocsCssUrl", (StyleSheetUrl ?? "") + "/" + (value ?? "") + ".css");
            }
        }

        /// <summary>
        ///     The title to be displayed at the left side of the browser interview's title/toolbar.
        ///     If this property is null, HotDocs will use the built-in title of the template.
        /// </summary>
        /// <remarks>
        ///     This property is equivalent to the TemplateTitle property on the HotDocs Server API and the
        ///     TemplateTitleOverride setting in Core Services.
        /// </remarks>
        public string Title
        {
            get { return GetSettingString("TemplateTitleOverride"); }
            set { SetSettingString("TemplateTitleOverride", value); }
        }

        /// <summary>
        ///     The name of a HotDocs "Browser Language Module" that dictates locale-specific behavior and language
        ///     for a HotDocs interview.
        ///     If this property is null, US English ("en-US") behavior is assumed by default.
        /// </summary>
        /// <remarks>
        ///     This property is equivalent to the InterviewLocale setting in Core Services, and the
        ///     HDInterviewLocale global JavaScript variable in browser interviews.
        /// </remarks>
        public string Locale
        {
            get { return GetSettingString("InterviewLocale"); }
            set { SetSettingString("InterviewLocale", value); }
        }

        /// <summary>
        ///     Determines the order in which end users will progress through a browser interview when they click
        ///     the Next and Previous navigation buttons.
        ///     By default it defers to the behavior as configured on the server.
        /// </summary>
        /// <remarks>
        ///     Corresponds to the "Next Dialog Follows Outline" entry on the "Navigate" menu within desktop HotDocs.
        ///     When this setting is True, dialogs are navigated in a top-down manner, with the Next command
        ///     always navigating the interview outline from top to bottom. When this setting is "false", behavior of the Next
        ///     command depends on the current location: choosing Next from a top-level dialog always proceeds to the next
        ///     top-level dialog (skipping child dialogs, if any), and choosing Next from a child dialog always navigates back
        ///     to its parent.
        ///     <para>
        ///         This property is also equivalent to the "Next button follows interview outline" setting
        ///         in the HotDocs Server management console and the OutlineInOrder setting in Core Services.
        ///     </para>
        /// </remarks>
        public Tristate NextFollowsOutline
        {
            get { return GetSettingTristate("OutlineInOrder"); }
            set { SetSettingString("OutlineInOrder", TristateToString(value)); }
        }

        /// <summary>
        ///     HotDocs interviews display a resource button next to input fields for which the template author has provided
        ///     additional resource information. This setting determines whether resource buttons will be visible simultaneously
        ///     for all such
        ///     input fields, or whether resource buttons should appear only for the current-focused input field.
        ///     By default it honors the behavior configured on the server.
        /// </summary>
        /// <remarks>
        ///     This property is equivalent to the "Show all resource butons" setting in the HotDocs Server management
        ///     console and the ShowAllResourceButtons setting in Core Services.  It also corrresponds to the
        ///     HotDocs Options &gt; Interviews and Dialogs &gt; Show answer field resource button
        ///     option in desktop HotDocs.
        /// </remarks>
        public Tristate ShowAllResourceButtons
        {
            get { return GetSettingTristate("ShowAllResourceButtons"); }
            set { SetSettingString("ShowAllResourceButtons", TristateToString(value)); }
        }

        /// <summary>
        ///     Specifies that the Document Preview button should not appear on the interview toolbar, independent of whether
        ///     a DocumentPreviewUrl has been specified or not.  By default it respects the default behavior as configured
        ///     on the server.
        /// </summary>
        /// <remarks>
        ///     This property corresponds to (but is reversed from) the "Allow document preview" setting in the HotDocs Server
        ///     management console and the DocumentPreview property in the HotDocs Server API.
        /// </remarks>
        public bool DisableDocumentPreview { get; set; } // defaults to Default

        /// <summary>
        ///     Specifies that the Save Answers button should not appear on the interview toolbar, independent of whether
        ///     a SaveAnswersUrl has been specified or not.  Defaults to False.
        /// </summary>
        /// <remarks>
        ///     There is no server default behavior to fall back on. Property is Tristate instead of bool only for consistency with
        ///     the other "Disable..." properties.
        /// </remarks>
        public bool DisableSaveAnswers { get; set; }

        /// <summary>
        ///     HotDocs interviews normally allow users to request answer summaries from the interview toolbar.
        ///     This property can remove the answer summary button from the toolbar.  By default the property
        ///     inherits the default value configured on the server.
        /// </summary>
        /// <remarks>
        ///     This property is corresponds to (but is reversed from) the "Allow answer summary" setting in the HotDocs Server
        ///     management console; it is also equivalent to the DisableAnswerSummary setting in Core Services.
        /// </remarks>
        public Tristate DisableAnswerSummary
        {
            get { return GetSettingTristate("DisableAnswerSummary"); }
            set { SetSettingString("DisableAnswerSummary", TristateToString(value)); }
        }

        /// <summary>
        ///     Settings that determine the formatting of Answer Summaries generated in the browser interview.
        /// </summary>
        /// <remarks>
        ///     NOTE: All answer summary properties are ignored if the SDK is communicating with HotDocs Server (either locally or
        ///     at a web service).  HotDocs Server always honors the settings specified in the HotDocs Server
        ///     Management Console, and does not allow individual interview requests to override those default values.
        /// </remarks>
        public AnswerSummaryOptions AnswerSummary { get; set; } // initialized to a new AnswerSummaryOptions()

        #endregion

        #region Shared or Common Properties

        // "shared" or "common" properties (also AssemblyOptions)

        /// <summary>
        ///     This specifies what text will be merged when no answer has been provided for the requested variable.
        ///     The choices are
        ///     <list type="table">
        ///         <listheader>
        ///             <term>Value</term><description>Merged into Text</description>
        ///         </listheader>
        ///         <item>
        ///             <term>Default</term><description>(whichever of the below is specified on the server)</description>
        ///         </item>
        ///         <item>
        ///             <term>Nothing</term><description>(nothing)</description>
        ///         </item>
        ///         <item>
        ///             <term>Underscores</term><description>____________</description>
        ///         </item>
        ///         <item>
        ///             <term>Asterisks</term><description>***</description>
        ///         </item>
        ///         <item>
        ///             <term>VarNameWithBrakets</term><description>[Variable Name]</description>
        ///         </item>
        ///         <item>
        ///             <term>VarNameWithAsterisks</term><description>*** Variable Name ***</description>
        ///         </item>
        ///     </list>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The property corresponds to the "Unanswered variable placeholder" setting in the HotDocs Server
        ///         management console and the UnansweredFormat setting in Core Services.
        ///     </para>
        ///     <para>
        ///         NOTE: This property is ignored if the SDK is communicating with HotDocs Server (either locally or
        ///         at a web service).  HotDocs Server always honors the setting specified in the HotDocs Server
        ///         Management Console, and does not allow individual interview requests to override that default value.
        ///     </para>
        /// </remarks>
        public UnansweredFormat DefaultUnansweredFormat
        {
            // defaults to Default (only changeable for CS)
            get
            {
                switch (GetSettingString("UnansweredFormat"))
                {
                    case "Nothing":
                        return UnansweredFormat.Nothing;
                    case "Underscores":
                        return UnansweredFormat.Underscores;
                    case "Asterisks":
                        return UnansweredFormat.Asterisks;
                    case "[Variable]":
                        return UnansweredFormat.VarNameWithBrackets;
                    case "*** Variable ***":
                        return UnansweredFormat.VarNameWithAsterisks;
                    default:
                        return UnansweredFormat.Default;
                }
            }
            set
            {
                string val = null;
                switch (value)
                {
                    case UnansweredFormat.Nothing:
                        val = "Nothing";
                        break;
                    case UnansweredFormat.Underscores:
                        val = "Underscores";
                        break;
                    case UnansweredFormat.Asterisks:
                        val = "Asterisks";
                        break;
                    case UnansweredFormat.VarNameWithBrackets:
                        val = "";
                        break;
                    case UnansweredFormat.VarNameWithAsterisks:
                        val = "";
                        break;
                }
                SetSettingString("UnansweredFormat", val);
            }
        }

        /// <summary>
        ///     Whether or not the Unanswered Variable Placeholder specified by a template author (in Component
        ///     File Properties) is allowed to override the DefaultUnansweredFormat as configured on the server
        ///     or set in the SDK.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The property corresponds to the "Allow individual component files to override this placeholder"
        ///         setting in the HotDocs Server management console and the HonorCmpUnansFormat setting in Core Services.
        ///     </para>
        ///     <para>
        ///         NOTE: This property is ignored if the SDK is communicating with HotDocs Server (either locally or
        ///         at a web service).  HotDocs Server always honors the setting specified in the HotDocs Server
        ///         Management Console, and does not allow individual interview requests to override that default value.
        ///     </para>
        /// </remarks>
        public Tristate HonorCmpUnansweredFormat
        {
            // defaults to Default (only changeable for CS)
            get { return GetSettingTristate("HonorCmpUnansFormat"); }
            set { SetSettingString("HonorCmpUnansFormat", TristateToString(value)); }
        }

        /// <summary>
        ///     The default format example used when merging date values into text where no explicit or default format
        ///     example has been specified.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The property corresponds to the "Default date format" setting in the HotDocs Server
        ///         management console and the DefaultDateFormat setting in Core Services.
        ///     </para>
        ///     <para>
        ///         NOTE: This property is ignored if the SDK is communicating with HotDocs Server (either locally or
        ///         at a web service).  HotDocs Server always honors the setting specified in the HotDocs Server
        ///         Management Console, and does not allow individual interview requests to override that default value.
        ///     </para>
        /// </remarks>
        public string DefaultDateFormat
        {
            // defaults to Default (only changeable for CS)
            get { return GetSettingString("DefaultDateFormat"); }
            set { SetSettingString("DefaultDateFormat", value); }
        }

        #endregion

        #region Omitted and Future Properties

        // These are some previously-included or discussed properties that have been intentionally omitted

        //public string TempInterviewPath { get; set; } // this is where interview images get copied & where doc previews are created -- redundant with InterviewResult.SupportingFiles
        //public int MaxWhileCount { get; set; } // defaults to Default (only changeable for CS)
        //public int MaxRecursionDepth { get; set; } // defaults to Default (only changeable for CS)

        /* ------------------------------------------------------------------------------------------------------------
		 * Other global variables in the JavaScript that could be added to GetInterviewOptions in the future:
		 * - HDRequiredAsterisk
		 * - HDShowProgressBar
		 * - HDInterviewWidth
		 * - HDInterviewHeight
		 * - HDBottomMargin
		 * - HDInterviewOutlineWidth
		 * - HDResourcePaneHeight
		 * - HDDisableBrowserWarning
		 * - HDDisableFinishWarning
		 * - HDLeaveWarning
		 * - HDDisableOnSubmit
		 * - HDFinalSubmitFrame
		 */

        #endregion
    }
}