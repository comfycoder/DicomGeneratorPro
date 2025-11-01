using System;
using System.Collections.Generic;
using System.IO;
using FellowOakDicom;

namespace DicomGeneratorPro
{
    public sealed class DicomStudyGenerator
    {
        private readonly AppConfig _cfg;
        private readonly Random _rnd;
        private readonly DicomFactory _factory;

        public DicomStudyGenerator(AppConfig cfg, Random rnd)
        {
            _cfg = cfg;
            _rnd = rnd;
            _factory = new DicomFactory(cfg);
        }

        private static List<int> SplitPositive(int total, int parts, Random rnd)
        {
            parts = Math.Max(1, Math.Min(parts, total));
            var cuts = new SortedSet<int>();
            while (cuts.Count < parts - 1) cuts.Add(rnd.Next(1, total));
            var points = new List<int> { 0 };
            points.AddRange(cuts);
            points.Add(total);

            var outp = new List<int>();
            for (int i = 1; i < points.Count; i++)
                outp.Add(points[i] - points[i - 1]);

            return outp;
        }

        public sealed record StudyResult(int SeriesCount, int FileCount, List<string> Files);

        public StudyResult GenerateStudy(
            string outputRoot,
            string organization,
            string patientId,
            string patientName,
            string modality,
            DateTime baseDateUtc)
        {
            var profile = _cfg.Profiles.TryGetValue(modality, out var p) ? p : new ModalityProfile();

            // Choose a study description (profile override first, else generic)
            var studyDescList = (profile.StudyDescriptions.Count > 0 ? profile.StudyDescriptions : new List<string> { "Diagnostic" });
            var studyDescription = studyDescList[_rnd.Next(studyDescList.Count)];

            int rows = profile.Rows > 0 ? profile.Rows : _cfg.Defaults.Rows;
            int cols = profile.Cols > 0 ? profile.Cols : _cfg.Defaults.Cols;

            int seriesCount = profile.SeriesPerStudy.Sample(_rnd);
            int totalFiles = profile.StandardStudyFileCounts.Count > 0
                ? profile.StandardStudyFileCounts[_rnd.Next(profile.StandardStudyFileCounts.Count)]
                : Math.Max(1, seriesCount * 16);

            var perSeries = SplitPositive(totalFiles, seriesCount, _rnd);

            var examFolder = $"{baseDateUtc:yyyyMMdd}_{baseDateUtc:HHmmss} {studyDescription}";
            examFolder = Sanitizer.ForPath(examFolder);
            var orgFolder = Sanitizer.ForPath(organization);
            var patientFolder = Sanitizer.ForPath(patientId);

            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
            var written = new List<string>();

            for (int s = 0; s < perSeries.Count; s++)
            {
                var seriesDesc = profile.SeriesDescriptions.Count > 0
                    ? profile.SeriesDescriptions[s % profile.SeriesDescriptions.Count]
                    : $"Series{s + 1}";

                // Combined Modality + Series description folder (kept)
                var combinedSeriesFolder = Sanitizer.ForPath($"{modality} {seriesDesc}");
                var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;

                var seriesDir = Path.Combine(
                    outputRoot, "Dicom",
                    orgFolder,
                    patientFolder,
                    examFolder,
                    combinedSeriesFolder
                );
                Directory.CreateDirectory(seriesDir);

                for (int i = 0; i < perSeries[s]; i++)
                {
                    int instanceNumber = i + 1;
                    var fileName = $"IM{instanceNumber:000000}.dcm";
                    var path = Path.Combine(seriesDir, fileName);

                    _factory.WriteInstance(
                        path, patientId, patientName, modality, studyDescription, baseDateUtc,
                        seriesDesc, studyUid, seriesUid, instanceNumber, rows, cols);

                    // Report each file as it is created
                    //Console.WriteLine(path);

                    written.Add(path);
                }

                // Per-series progress
                Console.WriteLine($"Generated {perSeries[s]} DICOM files at: {seriesDir}");
            }

            return new StudyResult(perSeries.Count, written.Count, written);
        }
    }
}
