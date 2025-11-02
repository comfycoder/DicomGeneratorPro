using System.Text.Json.Serialization;

namespace DicomGeneratorPro;

public sealed class AppConfig
{
    // Core paths & randomization
    public string OutputRoot { get; set; } = "out";
    public int? Seed { get; set; } = 12345;

    // Population shape
    public int NumOrganizations { get; set; } = 10;
    public RangeInt PatientsPerOrg { get; set; } = new(10, 100);
    public RangeInt ExamsPerPatient { get; set; } = new(1, 4);

    // Modalities & exam sizing
    public RangeInt ModalitiesPerExam { get; set; } = new(2, 3);
    public List<string> Modalities { get; set; } = new() { "CT", "PT", "MR", "NM", "XA", "CR", "SR" };

    // NEW: Weighted exam mix
    public ExamMixConfig ExamMix { get; set; } = new();

    // ID patterns
    public OrgPrefixConfig OrgPrefix { get; set; } = new();
    public PatientIdConfig PatientId { get; set; } = new();

    // Date window (years back from now)
    public RangeInt DateRangeYears { get; set; } = new(0, 5);

    // DICOM defaults + per-modality overrides
    public DicomDefaults Defaults { get; set; } = new();
    public Dictionary<string, ModalityProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// ---- NEW ----
public sealed class ExamMixConfig
{
    /// <summary>Target share of exams that are exactly CT+PT pairs.</summary>
    public int CtPtPercent { get; set; } = 70;

    /// <summary>Target share of exams that are exactly CT+NM pairs.</summary>
    public int CtNmPercent { get; set; } = 10;

    /// <summary>Target share of exams that use the “mixed” bucket (>=2 modalities from ModalitiesPerExam & Modalities).</summary>
    public int MixedPercent { get; set; } = 20;
}

public sealed class OrgPrefixConfig
{
    public int PrefixLength { get; set; } = 3;
    public string Alphabet { get; set; } = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
}

public sealed class PatientIdConfig
{
    // New composite-ID controls
    public int InitialsLength { get; set; } = 2;   // e.g., "JD"
    public string InitialsAlphabet { get; set; } = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public int RandomCodeLength { get; set; } = 6;   // e.g., "X9T4A7"
    public string RandomCodeAlphabet { get; set; } = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public int RandomNumberDigits { get; set; } = 3;   // e.g., "042" (zero-padded)
    public string Separator { get; set; } = "-"; // glue between parts

    // (Optional) legacy fields kept for backward compatibility.
    // These will be IGNORED by the new generator but retained so
    // existing config files won't break model binding.
    public string Prefix { get; set; } = "P";
    public int Digits { get; set; } = 6;
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
    /// <summary>How many series per study for this modality.</summary>
    public RangeInt SeriesPerStudy { get; set; } = new(1, 2);

    /// <summary>Typical file counts per study (one value is sampled per study).</summary>
    public List<int> StandardStudyFileCounts { get; set; } = new() { 64 };

    /// <summary>Series descriptions cycled per series.</summary>
    public List<string> SeriesDescriptions { get; set; } = new() { "SeriesA", "SeriesB" };

    /// <summary>Study descriptions used for naming; fallback “Diagnostic”.</summary>
    public List<string> StudyDescriptions { get; set; } = new() { "Diagnostic" };

    /// <summary>If 0, fall back to Defaults.Rows.</summary>
    public int Rows { get; set; } = 0;

    /// <summary>If 0, fall back to Defaults.Cols.</summary>
    public int Cols { get; set; } = 0;
}
