namespace OpenCAD
{
    /// <summary>
    /// Represents a root CAD document/file with associated metadata.
    /// Child objects represent geometry, settings, and other drawing elements.
    /// </summary>
    public class OpenCADDocument : OpenCADObject
    {
        private string _filename = string.Empty;
        private string _description = string.Empty;

        public OpenCADDocument()
        {
            _isDrawable = false; // Document itself is not drawable
        }

        public OpenCADDocument(string filename, string description = "") : this()
        {
            _filename = filename;
            _description = description;
        }

        /// <summary>
        /// Gets or sets the filename of the CAD document.
        /// </summary>
        public string Filename
        {
            get => _filename;
            set => _filename = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the description of the CAD document.
        /// </summary>
        public string Description
        {
            get => _description;
            set => _description = value ?? string.Empty;
        }
    }
}