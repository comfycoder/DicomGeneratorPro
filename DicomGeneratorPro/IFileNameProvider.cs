namespace DicomGeneratorPro
{
    /// <summary>
    /// Policy for composing the filename of a generated DICOM instance.
    /// Should return a simple filename (no directory separators) that ends with ".dcm".
    /// </summary>
    public interface IFileNameProvider
    {
        /// <summary>
        /// Build a filename for a single-frame DICOM instance.
        /// </summary>
        /// <param name="sopInstanceUid">SOP Instance UID (0008,0018) to embed in the filename.</param>
        /// <param name="instanceNumber">Instance Number (0020,0013); typically 1..N.</param>
        string BuildFileName(string sopInstanceUid, int instanceNumber);
    }
}
