using System;
using System.Collections.Generic;

namespace DicomGeneratorPro
{
    /// <summary>
    /// Coordinates an exam (clinical encounter) that can contain multiple DICOM studies,
    /// typically one study per modality selected for the exam.
    /// A single exam has a shared AccessionNumber that is threaded into all studies.
    /// </summary>
    public sealed class DicomExamGenerator
    {
        private readonly AppConfig _cfg;
        private readonly Random _rnd;
        private readonly DicomStudyGenerator _studyGen;

        public DicomExamGenerator(AppConfig cfg, Random rnd)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _rnd = rnd ?? throw new ArgumentNullException(nameof(rnd));
            _studyGen = new DicomStudyGenerator(cfg, rnd);
        }

        public sealed record ExamResult(
            string AccessionNumber,
            DateTime ExamDateTimeUtc,
            List<DicomStudyGenerator.StudyResult> Studies);

        /// <summary>
        /// Generates a single exam with one study per modality.
        /// All generated instances share the same AccessionNumber.
        /// </summary>
        public ExamResult GenerateExam(
            string outputRoot,
            string organization,
            string patientId,
            string patientName,
            DateTime examDateTimeUtc,
            List<string> modalities)
        {
            if (string.IsNullOrWhiteSpace(outputRoot)) throw new ArgumentException("outputRoot is required");
            if (string.IsNullOrWhiteSpace(organization)) throw new ArgumentException("organization is required");
            if (string.IsNullOrWhiteSpace(patientId)) throw new ArgumentException("patientId is required");
            if (modalities is null || modalities.Count == 0) throw new ArgumentException("At least one modality required", nameof(modalities));

            // One accession per exam (shared across all studies), 16 chars max for VR SH.
            var accession = BuildAccession(examDateTimeUtc);

            var results = new List<DicomStudyGenerator.StudyResult>(modalities.Count);
            foreach (var modality in modalities)
            {
                var study = _studyGen.GenerateStudy(
                    outputRoot,
                    organization,
                    patientId,
                    patientName,
                    modality,
                    examDateTimeUtc,
                    accession);

                results.Add(study);
            }

            return new ExamResult(accession, examDateTimeUtc, results);
        }

        private string BuildAccession(DateTime dtUtc)
        {
            // Exactly 16 characters: "yyyyMMddHHmm" (12) + 4 random digits (4) = 16
            // Example: 202511021602 + 7075 => "2025110216027075"
            var core = dtUtc.ToString("yyyyMMddHHmm");
            var suffix = _rnd.Next(0, 10000).ToString("D4");
            return core + suffix;
        }
    }
}
