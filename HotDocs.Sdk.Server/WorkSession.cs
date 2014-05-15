/* Copyright (c) 2013, HotDocs Limited
   Use, modification and redistribution of this source is subject
   to the New BSD License as set out in LICENSE.TXT. */

using System;
using System.Collections.Generic;
using System.IO;

namespace HotDocs.Sdk.Server
{
	/// <summary>
	/// This delegate type allows a host application to request notification immediately prior to the SDK assembling
	/// a specific document (during the execution of the WorkSession.AssembleDocuments method).
	/// </summary>
	/// <param name="template">The Template from which a new document is about to be assembled.</param>
	/// <param name="answers">The AnswerCollection which will be used for the assembly.  This represents the current
	/// state of the answer session for the entire WorkSession, and if modified, will modify the answers from here
	/// through to the end of the answer session.</param>
	/// <param name="settings">The AssembleDocumentSettings that will be used for this specific document assembly. This is
	/// a copy of the WorkSession's DefaultAssemblySettings; if modified it affects only the current assembly.</param>
	/// <param name="userState">Whatever state object is passed by the host application into
	/// WorkSession.AssembleDocuments will be passed back out to the host application in this userState parameter.</param>
	public delegate void PreAssembleDocumentDelegate(Template template, AnswerCollection answers, AssembleDocumentSettings settings, object userState);

	/// <summary>
	/// This delegate type allows a host application to request notification immediately following the SDK assembling
	/// a document during the execution of the WorkSession.AssembleDocuments method.
	/// </summary>
	/// <param name="template">The Template from which a new document was just assembled.</param>
	/// <param name="result">The AssemblyResult object associated with the assembled document.  The SDK has not yet
	/// processed this AssemblyResult.  The Document inside the result will be added to the Document array
	/// that will eventually be returned by AssembleDocuments.  The Answers will become the new answers for subsequent
	/// work in this WorkSession.</param>
	/// <param name="userState">Whatever state object is passed by the host application into
	/// WorkSession.AssembleDocuments will be passed back out to the host application in this userState parameter.</param>
	public delegate void PostAssembleDocumentDelegate(Template template, AssembleDocumentResult result, object userState);

	/// <summary>
	/// WorkSession is a state machine enabling a host application to easily navigate an assembly queue.
	/// It maintains an answer collection and a list of completed (and pending) interviews and documents.
	/// A host application uses this class not only to process through the assembly queue, but also to
	/// inspect the assembly queue for purposes of providing user feedback ("disposition pages").
	/// </summary>
	/// <remarks>
	/// Note: The implementation of WorkSession is the same whether interfacing with HDS or Core Services.
	/// </remarks>
	[Serializable]
	public class WorkSession
	{
		/// <summary>
		/// <c>WorkSession</c> constructor
		/// </summary>
		/// <param name="service">An object implementing the IServices interface, encapsulating the instance of
		/// HotDocs Server with which the host app is communicating.</param>
		/// <param name="template">The template upon which this WorkSession is based. The initial interview and/or
		/// document work items in the WorkSession will be based on this template (including its Switches property).</param>
		public WorkSession(IServices service, Template template) : this(service, template, null, null) { }

		/// <summary>
		/// Creates a WorkSession object that a host application can use to step through the process of presenting
		/// all the interviews and assembling all the documents that may result from the given template.
		/// </summary>
		/// <param name="service">An object implementing the IServices interface, encapsulating the instance of
		/// HotDocs Server with which the host app is communicating.</param>
		/// <param name="template">The template upon which this WorkSession is based. The initial interview and/or
		/// document work items in the WorkSession will be based on this template (including its Switches property).</param>
		/// <param name="answers">A collection of XML answers to use as a starting point for the work session.
		/// The initial interview (if any) will be pre-populated with these answers, and the subsequent generation
		/// of documents will have access to these answers as well.</param>
		public WorkSession(IServices service, Template template, TextReader answers) : this(service, template, answers, null) {}
        /// <summary>
        /// Creates a WorkSession object that a host application can use to step through the process of presenting
        /// all the interviews and assembling all the documents that may result from the given template.
        /// 
        /// Allows the default interview settings to be specified instead of being read from config file
        /// </summary>
        /// <param name="service">An object implementing the IServices interface, encapsulating the instance of
        /// HotDocs Server with which the host app is communicating.</param>
        /// <param name="template">The template upon which this WorkSession is based. The initial interview and/or
        /// document work items in the WorkSession will be based on this template (including its Switches property).</param>
        /// <param name="answers">A collection of XML answers to use as a starting point for the work session.
        /// The initial interview (if any) will be pre-populated with these answers, and the subsequent generation
        /// of documents will have access to these answers as well.</param>
        /// <param name="defaultInterviewSettings">The default interview settings to be used throughout the session</param>
        public WorkSession(IServices service, Template template, TextReader answers, InterviewSettings defaultInterviewSettings)
        {
            _service = service;
            AnswerCollection = new AnswerCollection();
            if (answers != null)
                AnswerCollection.ReadXml(answers);
            DefaultAssemblySettings = new AssembleDocumentSettings();
				if (defaultInterviewSettings != null)
					DefaultInterviewSettings = defaultInterviewSettings;
				else
					DefaultInterviewSettings = new InterviewSettings();
				// add the work items
            _workItems = new List<WorkItem>();
            if (template.HasInterview)
                _workItems.Add(new InterviewWorkItem(template));
            if (template.GeneratesDocument)
                _workItems.Add(new DocumentWorkItem(template));
        }

		/* properties/state */

		/// <summary>
		/// Returns the collection of answers pertaining to the current work item.
		/// </summary>
		public AnswerCollection AnswerCollection { get; private set; }

		/// <summary>
		/// When you create a WorkSession, a copy of the application-wide default assembly settings (as specified
		/// in web.config) is made and assigned to this property of the AnswerCollection.  If the host app customizes
		/// these DefaultAssemblyOptions, each subsequent document generated in the WorkSession will inherit those
		/// customized settings.
		/// </summary>
		public AssembleDocumentSettings DefaultAssemblySettings { get; private set; }

		/// <summary>
		/// The intent with DefaultInterviewOptions was to work much like DefaultAssemblyOptions. When you create
		/// a WorkSession, a copy of the application-wide default interview settings (as specified in web.config) is
		/// made and assigned to this property of the AnswerCollection.  If the host app customizes these
		/// DefaultInterviewOptions, each subsequent interview in the WorkSession will inherit those customized
		/// settings.  HOWEVER, this doesn't work currently because InterviewWorkItems do not carry a reference
		/// to the WorkSession, and therefore don't have access to this property. TODO: figure out how this should work.
		/// One possibility would be for the WorkSession class to expose the GetInterview method (and maybe also
		/// FinishInterview) rather than having those on the InterviewWorkItem class.  However, this would mean
		/// WorkSession would expose a GetInterview method all the time, but it is only callable some of the time
		/// (i.e. when the CurrentWorkItem is an interview).
		/// </summary>
		public InterviewSettings DefaultInterviewSettings { get; private set; }

		/// <summary>
		/// Returns the IServices object for the current work session.
		/// </summary>
		public IServices Service
		{
			get { return _service; }
		}

		private IServices _service;
		private List<WorkItem> _workItems;

		/// <summary>
		/// Exposees a list of interview and document work items, both already completed and pending, as suitable for
		/// presentation in a host application (for example, to show progress through the work session).
		/// </summary>
		public IEnumerable<WorkItem> WorkItems
		{
			get
			{
				return _workItems;
			}
		}

		/* convenience accessors */

		/// <summary>
		/// This is the one that's next in line to be completed.  In the case of interview work items,
		/// an interview is current both before and after it has been presented to the user, all the way
		/// up until FinishInterview is called, at which time whatever work item that follows becomes current.
		/// If the current work item is a document, AssembleDocuments() should be called, which will
		/// complete that document (and any that follow it), advancing CurrentWorkItem as it goes.
		/// </summary>
		public WorkItem CurrentWorkItem
		{
			get
			{
				foreach (var item in _workItems)
				{
					if (!item.IsCompleted)
						return item;
				}
				// else
				return null;
			}
		}

		/// <summary>
		/// returns true when all work items in the session have been completed, i.e. CurrentWorkItem == null.
		/// </summary>
		public bool IsCompleted
		{
			get { return CurrentWorkItem == null; }
		}

		/// <summary>
		/// AssembleDocuments causes all contiguous pending document work items (from CurrentWorkItem onwards)
		/// to be assembled, and returns the assembled documents.
		/// </summary>
		/// <include file="../Shared/Help.xml" path="Help/string/param[@name='logRef']"/>
		/// <returns></returns>
		/// <remarks>
		/// <para>If AssembleDocuments is called when the current work item is not a document (i.e. when there are
		/// currently no documents to assemble), it will return an empty array of results without performing any work.</para>
		/// <para>If you need to take any actions before or after each assembly, use the alternate constructor that
		/// accepts delegates.</para>
		/// TODO: include a table that shows the relationship between members of Document, AssemblyResult, WorkSession and DocumentWorkItem.
		/// </remarks>
		public Document[] AssembleDocuments(string logRef)
		{
			return AssembleDocuments(null, null, null, logRef);
		}

		/// <summary>
		/// AssembleDocuments causes all contiguous pending document work items (from CurrentWorkItem onwards)
		/// to be assembled, and returns the assembled documents.
		/// </summary>
		/// <param name="preAssembleDocument">This delegate will be called immediately before each document is assembled.</param>
		/// <param name="postAssembleDocument">This delegate will be called immediately following assembly of each document.</param>
		/// <param name="userState">This object will be passed to the above delegates.</param>
		/// <include file="../Shared/Help.xml" path="Help/string/param[@name='logRef']"/>
		/// <returns>An array of Document, one item for each document that was assembled.  Note that these items
		/// are of type Document, not AssemblyResult (see below).</returns>
		/// <remarks>
		/// <para>If AssembleDocuments is called when the current work item is not a document (i.e. when there are
		/// currently no documents to assemble), it will return an empty array of results without performing any work.</para>
		/// TODO: include a table that shows the relationship between members of Document, AssemblyResult, WorkSession and DocumentWorkItem.
		/// </remarks>
		public Document[] AssembleDocuments(PreAssembleDocumentDelegate preAssembleDocument,
			PostAssembleDocumentDelegate postAssembleDocument, object userState, string logRef)
		{
			var result = new List<Document>();
			// skip past completed work items to get the current workItem
			WorkItem workItem = null;
			int itemIndex = 0;
			for (; itemIndex < _workItems.Count; itemIndex++)
			{
				workItem = _workItems[itemIndex];
				if (!workItem.IsCompleted)
					break;
				workItem = null;
			}
			// while the current workItem != null && is a document (i.e. is not an interview)
			while (workItem != null && workItem is DocumentWorkItem)
			{
				var docWorkItem = workItem as DocumentWorkItem;
				// make a copy of the default assembly settings and pass it to the BeforeAssembleDocumentDelegate (if provided)
				AssembleDocumentSettings asmOpts = new AssembleDocumentSettings(DefaultAssemblySettings);
				asmOpts.Format = workItem.Template.NativeDocumentType;
				// if this is not the last work item in the queue, force retention of transient answers
				asmOpts.RetainTransientAnswers |= (workItem != _workItems[_workItems.Count - 1]);

				if (preAssembleDocument != null)
					preAssembleDocument(docWorkItem.Template, AnswerCollection, asmOpts, userState);

				// assemble the item
				using (var asmResult = _service.AssembleDocument(docWorkItem.Template, new StringReader(AnswerCollection.XmlAnswers), asmOpts, logRef))
				{
					if (postAssembleDocument != null)
						postAssembleDocument(docWorkItem.Template, asmResult, userState);

					// replace the session answers with the post-assembly answers
					AnswerCollection.ReadXml(asmResult.Answers);
					// add pendingAssemblies to the queue as necessary
					InsertNewWorkItems(asmResult.PendingAssemblies, itemIndex);
					// store UnansweredVariables in the DocumentWorkItem
					docWorkItem.UnansweredVariables = asmResult.UnansweredVariables;
					// add an appropriate Document to a list being compiled for the return value of this method
					result.Add(asmResult.ExtractDocument());
				}
				// mark the current workitem as complete
				docWorkItem.IsCompleted = true;
				// advance to the next workitem
				workItem = (++itemIndex >= _workItems.Count) ? null : _workItems[itemIndex];
			}
			return result.ToArray();
		}

		/// <summary>
		/// This constructor accepts a value for the interview format in case the host application wants to have more
		/// control over which format to use other than the one format specified in web.config. For example, the host
		/// application can detect whether or not the user's browser has Silverlight installed, and if not, it can choose
		/// to fall back to JavaScript interviews even if its normal preference is Silverlight.
		/// </summary>
		/// <param name="format">The format (Silverlight or JavaScript) of interview being requested.</param>
		/// <returns>An <c>InterviewResult</c>, containing the HTML fragment and any other supporting files required by the interview.</returns>
		public InterviewResult GetCurrentInterview(Contracts.InterviewFormat format)
		{
			InterviewSettings s = DefaultInterviewSettings;

			// If a format was specified (e.g., it is not "Unspecified") then use the format provided.
			if (format != Contracts.InterviewFormat.Unspecified)
				s.Format = format;

			return GetCurrentInterview(s, null);
		}

		/// <summary>
		/// Returns the current interview using default interview settings
		/// </summary>
		/// <returns></returns>
		public InterviewResult GetCurrentInterview()
		{
			return GetCurrentInterview(DefaultInterviewSettings, null);
		}

		/// <summary>
		/// Returns the current interview with the given settings
		/// </summary>
		/// <param name="settings">Settings to use with the interview.</param>
		/// <param name="markedVariables">A list of variable names whose prompts should be "marked" in the interview.</param>
		/// <include file="../Shared/Help.xml" path="Help/string/param[@name='logRef']"/>
		/// <returns>An <c>InterviewResult</c> containing the HTML fragment and other supporting files for the interview.</returns>
		public InterviewResult GetCurrentInterview(InterviewSettings settings, IEnumerable<string> markedVariables, string logRef = "")
		{
			WorkItem currentWorkItem = CurrentWorkItem;
			TextReader answers = new StringReader(AnswerCollection.XmlAnswers);


			settings.Title = settings.Title ?? CurrentWorkItem.Template.Title;

			return _service.GetInterview(currentWorkItem.Template, answers, settings, markedVariables, logRef);
		}

		/// <summary>
		/// Called by the host application when answers have been posted back from a browser interview.
		/// </summary>
		/// <param name="interviewAnswers">The answers that were posted back from the interview.</param>
		public void FinishInterview(TextReader interviewAnswers)
		{
			// pseudocode:
			// overlay interviewAnswers over the session answer set,
			// if the current template is an interview template
			//     "assemble" it
			//     add pending assemblies to the queue as necessary
			// mark this interview workitem as complete.  (This will cause the WorkSession to advance to the next workItem.)

			AnswerCollection.OverlayXml(interviewAnswers);
			if (CurrentWorkItem is InterviewWorkItem)
			{
				InterviewResult interviewResult = _service.GetInterview(CurrentWorkItem.Template, new StringReader(AnswerCollection.XmlAnswers), InterviewSettings.Default, null, "");

				CurrentWorkItem.IsCompleted = true;
			}
		}

		private void InsertNewWorkItems(IEnumerable<Template> templates, int parentPosition)
		{
			int insertPosition = parentPosition + 1;
			foreach (var template in templates)
			{
				if (template.HasInterview)
					_workItems.Insert(insertPosition++, new InterviewWorkItem(template));
				if (template.GeneratesDocument)
					_workItems.Insert(insertPosition++, new DocumentWorkItem(template));
			}
		}

	}
}
