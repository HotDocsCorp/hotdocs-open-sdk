![Legacy Product — No Longer Supported](https://place-hold.it/540x50/FFFFFF/f03c15?text=Legacy%20Product%20-%20No%20Longer%20Supported&bold&fontsize=24)
---------------------------------------------

HotDocs Open SDK
================
The HotDocs Open SDK is a toolkit designed to ease integration between 3rd party applications and HotDocs.
It is implemented in C# and can be utilized by any managed (.NET) application to simplify communication
with HotDocs Cloud Services and/or HotDocs Server. The SDK is comprised of several components that can be
used together or (when it makes sense) individually:

*	**HotDocs.Sdk** - This managed DLL helps with parsing and/or creating various XML-based data files
	necessary for deep HotDocs integrations. For example, you can easily create and parse HotDocs XML
	answer collections. It also exposes methods for parsing and using template and package manifests,
	as well as code to extract files from template packages. *Compatible with .NET 2.0 and later.*

*	**HotDocs.Sdk.Cloud** - This managed DLL (also known as the Cloud Services Client Library)
	simplifies communication with the web services interfaces exposed by HotDocs Cloud Services.
	It exposes both the "Embedded" and "Direct" methods of integrating HotDocs browser interviews
	into your web application, and encapsulates such details as proper error handling. *Compatible with
	.NET 2.0 and later.*

*	**HotDocs.Sdk.Server** - This managed DLL allows applications to transparently integrate with either
	HotDocs Cloud Services (running in the cloud) or HotDocs Server (running on the same machine or
	remotely via web services). It provides an abstraction layer that hides many of the differences
	between those APIs. It also includes a WorkSession class that greatly simplifies implementation of
	basic HotDocs workflows (a.k.a. assembly queues). *Compatible with .NET 4.0 and later.*

*	**HotDocs.Sdk.Server.Contracts** - This small managed DLL contains shared data types and some
	protocols/algorithms necessary for proper communication between client code and HotDocs-provided web
	services. This includes e.g. Windows Communication Foundation (WCF) contracts for data types and
	services, and the standard HMAC algorithm necessary to call Cloud Services. Note: this project is
	included for informational and compatibility reasons. Changes to code in this module are not
	expected. *Compatible with .NET 2.0 and later.*

*	**HotDocs.Sdk.DataServices** - This managed DLL implements an OData data service facilitating
	the new Answer Source capabilities in HotDocs 11 browser interviews. On their own, browser interviews
	can display answer sources based on the "current" answer file only; however, host applications
	incorporating this DLL can also deliver data from server-side answer files. This DLL also provides
	a pattern to follow in implementing custom server-side answer sources. *Compatible with .NET 4.0
	and later.*

*	**SamplePortal** - This is a sample ASP.NET (Web Forms) application that illustrates how to use the
	HotDocs.Sdk.Server abstraction layer for communicating transparently with HotDocs Server and Cloud
	Services.

Documentation
-------------
Documentation still needs to be added to the project wiki, but in the meantime please refer to the following
sources of information:

*	The [auto-generated documentation](http://help.hotdocs.com/opensdk/) built from the XML comments in the code.
*	The **SamplePortal** project that is part of this code. This illustrates using HotDocs.Sdk.Server
	as an abstraction layer for talking to either HotDocs Server or Cloud Services.
*	The [HotDocs Embedded tutorials](http://help.hotdocs.com/cstutorial/), which illustrate how to
	easily embed HotDocs Cloud Services interviews & document assembly in a web page.

Requirements
------------
In order to take advantage of this project, you should either have a valid subscriber ID for HotDocs
Cloud Services or have access to a licensed installation of HotDocs Server. You must also have the
ability to write and compile ASP.NET web applications.

Other Platforms
---------------
The Cloud Services Client Library has been ported to other languages and platforms to facilitate HotDocs integration
from those platforms:

*	[Java](https://github.com/HotDocsCorp/hotdocs-cloud-java)
*	[PHP](https://github.com/HotDocsCorp/hotdocs-cloud-php) (HotDocs Embedded API only)
*	[Ruby](https://github.com/pifleo/hotdocs-cloud) (maintained by pifleo)

See the [Cloud Services Client Libraries wiki page](https://github.com/HotDocsCorp/hotdocs-open-sdk/wiki/HotDocs-Cloud-Services-Client-Libraries)
for more information on the implementations in these languages..

Contributing
------------
We welcome pull requests from potential contributors. If you think of a great way to improve HotDocs
integration with another application, we're all ears. Or if you are interested in helping port this
SDK (or any portion of it) onto any other platform or language, please let us know!
