using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using System;
using System.IO;

namespace DicomGeneratorPro
{
    /// <summary>
    /// Builds and writes synthetic DICOM instances.
    /// This version accepts an EXAM-level accession number so all studies
    /// generated for the same exam share the same AccessionNumber tag.
    /// </summary>
    public sealed class DicomFactory
    {
        private readonly AppConfig _cfg;
        public DicomFactory(AppConfig cfg) { _cfg = cfg; }

        /// <summary>
        /// Original behavior retained (explicit path), but now requires accessionNumber.
        /// </summary>
        public void WriteInstance(
            string path,
            string patientId,
            string patientName,
            string modality,
            string studyDescription,
            DateTime studyDateTimeUtc,
            string seriesDescription,
            string studyUid,
            string seriesUid,
            int instanceNumber,
            int rows,
            int cols,
            string accessionNumber)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is required", nameof(path));
            if (string.IsNullOrWhiteSpace(accessionNumber)) throw new ArgumentException("accessionNumber is required", nameof(accessionNumber));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var ds = CreateDataset(
                patientId, patientName, modality, studyDescription, studyDateTimeUtc,
                seriesDescription, studyUid, seriesUid, instanceNumber, rows, cols,
                out _,
                accessionNumber
            );

            new DicomFile(ds).Save(path);
        }

        /// <summary>
        /// Caller provides a target directory and a filename provider.
        /// The filename will be built from the dataset's SOPInstanceUID + InstanceNumber.
        /// Returns the full path actually written.
        /// </summary>
        public string WriteInstanceToDirectory(
            string directory,
            IFileNameProvider fileNameProvider,
            string patientId,
            string patientName,
            string modality,
            string studyDescription,
            DateTime studyDateTimeUtc,
            string seriesDescription,
            string studyUid,
            string seriesUid,
            int instanceNumber,
            int rows,
            int cols,
            string accessionNumber)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("directory is required", nameof(directory));
            if (fileNameProvider is null) throw new ArgumentNullException(nameof(fileNameProvider));
            if (string.IsNullOrWhiteSpace(accessionNumber)) throw new ArgumentException("accessionNumber is required", nameof(accessionNumber));

            Directory.CreateDirectory(directory);

            var ds = CreateDataset(
                patientId, patientName, modality, studyDescription, studyDateTimeUtc,
                seriesDescription, studyUid, seriesUid, instanceNumber, rows, cols,
                out string sopInstanceUid,
                accessionNumber
            );

            var fileName = fileNameProvider.BuildFileName(sopInstanceUid, instanceNumber);
            var fullPath = Path.Combine(directory, fileName);

            new DicomFile(ds).Save(fullPath);
            return fullPath;
        }

        /// <summary>
        /// Centralized dataset construction that preserves your requested configuration and
        /// keeps the geometry, SOP-class mapping, and simple-gradient pixel data exactly as requested,
        /// while injecting the EXAM-level accession number (SH, max 16 chars).
        /// </summary>
        private DicomDataset CreateDataset(
            string patientId,
            string patientName,
            string modality,
            string studyDescription,
            DateTime studyDateTimeUtc,
            string seriesDescription,
            string studyUid,
            string seriesUid,
            int instanceNumber,
            int rows,
            int cols,
            out string sopInstanceUid,
            string accessionNumber)
        {
            var ds = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian);

            ds.Add(DicomTag.PatientID, patientId);
            ds.Add(DicomTag.PatientName, patientName); // ORG^Initials
            ds.Add(DicomTag.StudyInstanceUID, studyUid);
            ds.Add(DicomTag.SeriesInstanceUID, seriesUid);

            // Generate once; also returned so filename can use the same value
            var sopUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            sopInstanceUid = sopUid.UID;
            ds.Add(DicomTag.SOPInstanceUID, sopInstanceUid);

            ds.Add(DicomTag.Modality, modality);
            ds.Add(DicomTag.StudyDescription, studyDescription);
            ds.Add(DicomTag.SeriesDescription, seriesDescription);
            ds.Add(DicomTag.InstanceNumber, instanceNumber);
            ds.Add(DicomTag.StudyDate, studyDateTimeUtc.ToString("yyyyMMdd"));
            ds.Add(DicomTag.StudyTime, studyDateTimeUtc.ToString("HHmmss"));

            // AccessionNumber is VR=SH (max 16 chars); enforce safety.
            ds.Add(DicomTag.AccessionNumber, TruncateForSH(accessionNumber));

            // Image module from config defaults
            ds.Add(DicomTag.SamplesPerPixel, (ushort)1);
            ds.Add(DicomTag.PhotometricInterpretation, _cfg.Defaults.PhotometricInterpretation);
            ds.Add(DicomTag.Rows, (ushort)rows);
            ds.Add(DicomTag.Columns, (ushort)cols);
            ds.Add(DicomTag.BitsAllocated, _cfg.Defaults.BitsAllocated);
            ds.Add(DicomTag.BitsStored, _cfg.Defaults.BitsStored);
            ds.Add(DicomTag.HighBit, (ushort)(_cfg.Defaults.BitsStored - 1));
            ds.Add(DicomTag.PixelRepresentation, (ushort)0);

            // --- Your exact requested block (kept verbatim) ---

            // Geometry
            ds.Add(DicomTag.PixelSpacing, "1\\1");
            ds.Add(DicomTag.ImageOrientationPatient, "1\\0\\0\\0\\1\\0");
            ds.Add(DicomTag.ImagePositionPatient, "0\\0\\0");

            // SOP Class mapping (includes SR)
            var sop = modality.ToUpperInvariant() switch
            {
                "CT" => DicomUID.CTImageStorage,
                "MR" => DicomUID.MRImageStorage,
                "PT" or "NM" => DicomUID.NuclearMedicineImageStorage,
                "XA" => DicomUID.XRayAngiographicImageStorage,
                "CR" => DicomUID.ComputedRadiographyImageStorage,
                "SR" => DicomUID.EnhancedSRStorage,
                _ => DicomUID.SecondaryCaptureImageStorage
            };
            ds.Add(DicomTag.SOPClassUID, sop.UID);

            // Pixels: simple gradient
            var frame = new byte[rows * cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    frame[r * cols + c] = (byte)((c + instanceNumber) % 256);
            var px = DicomPixelData.Create(ds, true);
            px.AddFrame(new MemoryByteBuffer(frame));

            // --- end of your kept block ---

            return ds;
        }

        /// <summary>
        /// Ensures a string conforms to DICOM SH (Short String) length limit (<=16).
        /// Trims whitespace and truncates if necessary.
        /// </summary>
        private static string TruncateForSH(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var v = value.Trim();
            return v.Length <= 16 ? v : v.Substring(0, 16);
        }
    }
}
