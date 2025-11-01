using System.Text.Json.Serialization;

namespace DicomGeneratorPro;

public sealed class AppConfig
{
    public string OutputRoot { get; set; } = "out";
    public int? Seed { get; set; } = 12345;

    public int NumOrganizations { get; set; } = 10;
    public RangeInt PatientsPerOrg { get; set; } = new(10, 100);
    public RangeInt ExamsPerPatient { get; set; } = new(1, 4);

    public RangeInt ModalitiesPerExam { get; set; } = new(1, 6);
    public List<string> Modalities { get; set; } = new() { "CT","PT","MR","NM","XA","CR","SR" };

    public RangeInt DateRangeYears { get; set; } = new(-3, 0);

    public OrgPrefixConfig OrgPrefix { get; set; } = new();
    public PatientIdConfig PatientId { get; set; } = new();

    public DicomDefaults Defaults { get; set; } = new();

    /// <summary>Per-modality profiles (case-insensitive keys).</summary>
    public Dictionary<string, ModalityProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class OrgPrefixConfig
{
    public int PrefixLength { get; set; } = 3;
    public string Alphabet { get; set; } = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
}

public sealed class PatientIdConfig
{
    public string Prefix { get; set; } = "PT";
    public int Digits { get; set; } = 8;
    public int StartFrom { get; set; } = 1;
}

public sealed class DicomDefaults
{
    public int Rows { get; set; } = 128;
    public int Cols { get; set; } = 128;
    public ushort BitsAllocated { get; set; } = 8;
    public ushort BitsStored { get; set; } = 8;
    public string PhotometricInterpretation { get; set; } = "MONOCHROME2";
}

public sealed class ModalityProfile
{
    public RangeInt SeriesPerStudy { get; set; } = new(1,2);
    public List<int> StandardStudyFileCounts { get; set; } = new() { 64 };
    public List<string> SeriesDescriptions { get; set; } = new() { "SeriesA", "SeriesB" };
    public List<string> StudyDescriptions { get; set; } = new() { "Diagnostic" };
    public int Rows { get; set; } = 0; // 0 -> fallback to Defaults.Rows
    public int Cols { get; set; } = 0; // 0 -> fallback to Defaults.Cols
}