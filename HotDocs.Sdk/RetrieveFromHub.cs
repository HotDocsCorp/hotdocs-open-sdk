namespace HotDocs.Sdk
{
    /// <summary>
    ///     For use with HotDocs Cloud Services specifying if you want to use a package located in the HotDocs Template Hub
    /// </summary>
    public enum RetrieveFromHub
    {
        /// <summary>
        ///     Don't use a package from the Template Hub
        /// </summary>
        No,

        /// <summary>
        ///     Use a package from the Template Hub where the Template Id is equal to the packageId you are requesting (The live
        ///     version of that template will be requested).
        /// </summary>
        ByTemplateId,

        /// <summary>
        ///     Use a package from the Template Hub where the Template Version Id is equal to the packageId you are requesting.
        /// </summary>
        ByVersionId
    }
}