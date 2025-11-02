using FellowOakDicom;

namespace DicomGeneratorPro
{
    /// <summary>
    /// Generates a single synthetic DICOM study (one or more series, each with N instances)
    /// and writes it to the standard folder layout:
    ///   <OutputRoot>/Dicom/<Organization>/<PatientId>/<YYYYMMDD_HHMMSS StudyDescription>/<Modality SeriesDescription>/*.dcm
    ///
    /// This version routes ALL instance filenames through an IFileNameProvider (UidAndInstanceFileNameProvider by default)
    /// to ensure the written file names match the dataset SOPInstanceUID/InstanceNumber.
    /// </summary>
    public sealed class DicomStudyGenerator
    {
        private readonly AppConfig _cfg;
        private readonly Random _rnd;
        private readonly DicomFactory _factory;

        public DicomStudyGenerator(AppConfig cfg, Random rnd)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _rnd = rnd ?? throw new ArgumentNullException(nameof(rnd));
            _factory = new DicomFactory(cfg);
        }

        public sealed record StudyResult(int SeriesCount, int FileCount, List<string> Files);

        /// <summary>
        /// Create a full study beneath the output root for the specified org/patient.
        /// </summary>
        public StudyResult GenerateStudy(
            string outputRoot,
            string organization,
            string patientId,
            string patientName,
            string modality,
            DateTime baseDateUtc)
        {
            if (string.IsNullOrWhiteSpace(outputRoot)) throw new ArgumentException("outputRoot is required");
            if (string.IsNullOrWhiteSpace(organization)) throw new ArgumentException("organization is required");
            if (string.IsNullOrWhiteSpace(patientId)) throw new ArgumentException("patientId is required");
            if (string.IsNullOrWhiteSpace(modality)) throw new ArgumentException("modality is required");

            // Profile by modality (fallback to empty profile)
            var profile = _cfg.Profiles != null && _cfg.Profiles.TryGetValue(modality, out var p)
                ? p
                : new ModalityProfile();

            // Study description
            var studyDescList = (profile.StudyDescriptions != null && profile.StudyDescriptions.Count > 0)
                ? profile.StudyDescriptions
                : new List<string> { "Diagnostic" };
            var studyDescription = studyDescList[_rnd.Next(studyDescList.Count)];

            // Geometry
            int rows = profile.Rows > 0 ? profile.Rows : _cfg.Defaults.Rows;
            int cols = profile.Cols > 0 ? profile.Cols : _cfg.Defaults.Cols;

            // How many series?
            int seriesCount = profile.SeriesPerStudy != null ? profile.SeriesPerStudy.Sample(_rnd) : 1;
            if (seriesCount <= 0) seriesCount = 1;

            // How many files per series?
            // If the profile lists explicit counts use the first as the standard count; else default to 64.
            int standardCount = (profile.StandardStudyFileCounts != null && profile.StandardStudyFileCounts.Count > 0)
                ? profile.StandardStudyFileCounts[0]
                : 64;

            var perSeries = new List<int>(seriesCount);
            for (int i = 0; i < seriesCount; i++) perSeries.Add(standardCount);

            // Folder components
            var examFolder = $"{baseDateUtc:yyyyMMdd}_{baseDateUtc:HHmmss} {studyDescription}";
            examFolder = Sanitizer.ForPath(examFolder);
            var orgFolder = Sanitizer.ForPath(organization);
            var patientFolder = Sanitizer.ForPath(patientId);

            // UIDs scoped to the study/series
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;

            // Use filename provider (SOPInstanceUID + instanceNumber)
            var fileNameProvider = new UidAndInstanceFileNameProvider("_", 5);

            var written = new List<string>();

            for (int s = 0; s < perSeries.Count; s++)
            {
                // Series description: cycle profile list or fallback
                string seriesDesc = (profile.SeriesDescriptions != null && profile.SeriesDescriptions.Count > 0)
                    ? profile.SeriesDescriptions[s % profile.SeriesDescriptions.Count]
                    : $"Series{s + 1}";

                // Folder: "<Modality> <SeriesDescription>"
                var combinedSeriesFolder = Sanitizer.ForPath($"{modality} {seriesDesc}");

                // Series UID
                var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;

                // Final series directory — note we do NOT use a separate studyDir variable
                var seriesDir = Path.Combine(
                    outputRoot, "Dicom",
                    orgFolder,
                    patientFolder,
                    examFolder,
                    combinedSeriesFolder
                );
                Directory.CreateDirectory(seriesDir);

                // Write instances
                int frames = perSeries[s];
                for (int i = 0; i < frames; i++)
                {
                    int instanceNumber = i + 1;

                    var fullPath = _factory.WriteInstanceToDirectory(
                        seriesDir,
                        fileNameProvider,
                        patientId,
                        patientName,
                        modality,
                        studyDescription,
                        baseDateUtc,
                        seriesDesc,
                        studyUid,
                        seriesUid,
                        instanceNumber,
                        rows,
                        cols);

                    written.Add(fullPath);

                    // Optional progress:
                    // Console.WriteLine(fullPath);
                }

                Console.WriteLine($"Generated {perSeries[s]} DICOM files at: {seriesDir}");
            }

            return new StudyResult(perSeries.Count, written.Count, written);
        }
    }
}
