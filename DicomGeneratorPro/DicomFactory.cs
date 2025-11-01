using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace DicomGeneratorPro;

public sealed class DicomFactory
{
    private readonly AppConfig _cfg;
    public DicomFactory(AppConfig cfg) { _cfg = cfg; }

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
        int cols)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var ds = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian);
        ds.Add(DicomTag.PatientID, patientId);
        ds.Add(DicomTag.PatientName, patientName); // ORG^NNNN
        ds.Add(DicomTag.StudyInstanceUID, studyUid);
        ds.Add(DicomTag.SeriesInstanceUID, seriesUid);
        ds.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID);
        ds.Add(DicomTag.Modality, modality);
        ds.Add(DicomTag.StudyDescription, studyDescription);
        ds.Add(DicomTag.SeriesDescription, seriesDescription);
        ds.Add(DicomTag.InstanceNumber, instanceNumber);
        ds.Add(DicomTag.StudyDate, studyDateTimeUtc.ToString("yyyyMMdd"));
        ds.Add(DicomTag.StudyTime, studyDateTimeUtc.ToString("HHmmss"));
        ds.Add(DicomTag.AccessionNumber, Math.Abs(patientId.GetHashCode()).ToString());

        // Image module from config defaults
        ds.Add(DicomTag.SamplesPerPixel, (ushort)1);
        ds.Add(DicomTag.PhotometricInterpretation, _cfg.Defaults.PhotometricInterpretation);
        ds.Add(DicomTag.Rows, (ushort)rows);
        ds.Add(DicomTag.Columns, (ushort)cols);
        ds.Add(DicomTag.BitsAllocated, _cfg.Defaults.BitsAllocated);
        ds.Add(DicomTag.BitsStored, _cfg.Defaults.BitsStored);
        ds.Add(DicomTag.HighBit, (ushort)(_cfg.Defaults.BitsStored - 1));
        ds.Add(DicomTag.PixelRepresentation, (ushort)0);

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
        for (int r=0;r<rows;r++)
            for (int c=0;c<cols;c++)
                frame[r*cols+c] = (byte)((c + instanceNumber) % 256);
        var px = DicomPixelData.Create(ds, true);
        px.AddFrame(new MemoryByteBuffer(frame));

        new DicomFile(ds).Save(path);
    }
}