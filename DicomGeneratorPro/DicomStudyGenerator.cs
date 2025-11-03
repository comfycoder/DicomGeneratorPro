using System;
using System.Collections.Generic;
using System.IO;
using FellowOakDicom;

namespace DicomGeneratorPro
{
    /// <summary>
    /// Generates a single synthetic DICOM study (one or more series, each with N instances)
    /// and writes it to the standard folder layout:
    ///   <OutputRoot>/Dicom/<Organization>/<PatientId>/<YYYYMMDD_HHMMSS>/<Modality SeriesDescription_Snn_UID6>/*.dcm
    ///
    /// Reads AccessionNumber from written DICOM metadata (for realism),
    /// but does not include it in folder names.
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

        public sealed record StudyResult(string Modality, int SeriesCount, int FileCount, List<string> Files);

        public StudyResult GenerateStudy(
            string outputRoot,
            string organization,
            string patientId,
            string patientName,
            string modality,
            DateTime baseDateUtc,
            string accessionNumber)
        {
            if (string.IsNullOrWhiteSpace(outputRoot)) throw new ArgumentException("outputRoot is required");
            if (string.IsNullOrWhiteSpace(organization)) throw new ArgumentException("organization is required");
            if (string.IsNullOrWhiteSpace(patientId)) throw new ArgumentException("patientId is required");
            if (string.IsNullOrWhiteSpace(modality)) throw new ArgumentException("modality is required");
            if (string.IsNullOrWhiteSpace(accessionNumber)) throw new ArgumentException("accessionNumber is required");

            // Select modality profile
            var profile = _cfg.Profiles != null && _cfg.Profiles.TryGetValue(modality, out var p)
                ? p
                : new ModalityProfile();

            // Pick a study description
            var studyDescList = (profile.StudyDescriptions != null && profile.StudyDescriptions.Count > 0)
                ? profile.StudyDescriptions
                : new List<string> { "Diagnostic" };
            var studyDescription = studyDescList[_rnd.Next(studyDescList.Count)];

            // Geometry defaults
            int rows = profile.Rows > 0 ? profile.Rows : _cfg.Defaults.Rows;
            int cols = profile.Cols > 0 ? profile.Cols : _cfg.Defaults.Cols;

            // Series count and frames per series
            int seriesCount = profile.SeriesPerStudy != null ? profile.SeriesPerStudy.Sample(_rnd) : 1;
            if (seriesCount <= 0) seriesCount = 1;

            int standardCount = (profile.StandardStudyFileCounts != null && profile.StandardStudyFileCounts.Count > 0)
                ? profile.StandardStudyFileCounts[0]
                : 64;

            var perSeries = new List<int>(seriesCount);
            for (int i = 0; i < seriesCount; i++) perSeries.Add(standardCount);

            // Folder structure
            var orgFolder = Sanitizer.ForPath(organization);
            var patientFolder = Sanitizer.ForPath(patientId);

            // Exam folder = date + time only
            var examFolder = $"{baseDateUtc:yyyyMMdd}_{baseDateUtc:HHmmss}";
            examFolder = Sanitizer.ForPath(examFolder);

            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
            var fileNameProvider = new UidAndInstanceFileNameProvider("_", 5);

            var written = new List<string>();

            for (int s = 0; s < perSeries.Count; s++)
            {
                // Series description
                string seriesDesc = (profile.SeriesDescriptions != null && profile.SeriesDescriptions.Count > 0)
                    ? profile.SeriesDescriptions[s % profile.SeriesDescriptions.Count]
                    : $"Series{s + 1}";

                var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
                var uidSuffix = SafeTail(seriesUid, 6);
                var baseName = $"{modality} {seriesDesc}";
                var uniqueTail = $"S{(s + 1):D2}_{uidSuffix}";
                var combinedSeriesFolder = Sanitizer.ForPath($"{baseName}_{uniqueTail}");

                var seriesDir = Path.Combine(
                    outputRoot, "Dicom",
                    orgFolder,
                    patientFolder,
                    examFolder,
                    combinedSeriesFolder
                );
                Directory.CreateDirectory(seriesDir);

                int frames = perSeries[s];
                string firstFile = null;

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
                        cols,
                        accessionNumber
                    );
                    written.Add(fullPath);
                    if (firstFile == null) firstFile = fullPath;
                }

                // Just for realism: read AccessionNumber tag (don’t use it in naming)
                if (s == 0 && File.Exists(firstFile))
                {
                    try
                    {
                        var file = DicomFile.Open(firstFile);
                        var accFromTag = file.Dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, accessionNumber);
                        Console.WriteLine($"  [Study {modality}] AccessionNumber from metadata: {accFromTag}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [WARN] Could not read accession tag: {ex.Message}");
                    }
                }

                Console.WriteLine($"Generated {perSeries[s]} DICOM files at: {seriesDir}");
            }

            return new StudyResult(modality, perSeries.Count, written.Count, written);
        }

        /// <summary>
        /// Returns a safe uppercase tail from a UID for folder suffixing.
        /// Non-alphanumeric chars replaced with 'X'.
        /// </summary>
        private static string SafeTail(string uid, int length)
        {
            if (string.IsNullOrWhiteSpace(uid)) return new string('X', length);
            var s = uid.Length <= length ? uid : uid[^length..];
            Span<char> buf = stackalloc char[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                buf[i] = char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : 'X';
            }
            return new string(buf);
        }
    }
}
