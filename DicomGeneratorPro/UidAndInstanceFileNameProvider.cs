namespace DicomGeneratorPro
{
    /// <summary>
    /// Produces filenames like:
    /// {SOPInstanceUID}_Instance_{InstanceNumber}.dcm
    /// Example:
    /// 1.2.826.0.1.3680043.2.1125.1730243994.123_Instance_00001.dcm
    /// </summary>
    public sealed class UidAndInstanceFileNameProvider : IFileNameProvider
    {
        private readonly string _separator;
        private readonly int _pad;

        /// <param name="separator">Separator between parts (default "_").</param>
        /// <param name="pad">Zero-padding for instance number (default 5; set 0 to disable).</param>
        public UidAndInstanceFileNameProvider(string separator = "_", int pad = 5)
        {
            _separator = separator;
            _pad = pad;
        }

        public string BuildFileName(string sopInstanceUid, int instanceNumber)
        {
            // SOP UIDs are dot-separated numeric strings; make sure no path chars sneak in.
            var safeUid = sopInstanceUid
                .Replace(Path.DirectorySeparatorChar, '.')
                .Replace(Path.AltDirectorySeparatorChar, '.');

            var num = _pad > 0 ? instanceNumber.ToString($"D{_pad}") : instanceNumber.ToString();
            return $"{safeUid}{_separator}Instance{_separator}{num}.dcm";
        }
    }
}
