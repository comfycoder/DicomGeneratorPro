using System.Text.Json;

namespace DicomGeneratorPro;

public static class Program
{
    private static AppConfig LoadConfig(string path)
    {
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new AppConfig();
        cfg.Profiles = new Dictionary<string, ModalityProfile>(cfg.Profiles, StringComparer.OrdinalIgnoreCase);
        return cfg;
    }

    public static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] != "--config")
        {
            Console.WriteLine("Usage: DicomGeneratorPro --config <path-to-config.json>");
            return 2;
        }

        var cfg = LoadConfig(args[1]);
        var rnd = new RandomProvider(cfg.Seed).Rng;
        var outputRoot = Path.GetFullPath(cfg.OutputRoot);
        Directory.CreateDirectory(Path.Combine(outputRoot, "Dicom"));

        var orgGen = new OrgPrefixGenerator(rnd, cfg.OrgPrefix);
        var pidGen = new PatientIdGenerator(cfg.PatientId.Prefix, cfg.PatientId.Digits, cfg.PatientId.StartFrom);
        var generator = new DicomStudyGenerator(cfg, rnd);

        for (int oi=0; oi<cfg.NumOrganizations; oi++)
        {
            var org = orgGen.Next();
            int patients = cfg.PatientsPerOrg.Sample(rnd);
            for (int pi=0; pi<patients; pi++)
            {
                var (pid, human) = pidGen.Next();
                var patientName = $"{org}^{human}";

                // Per-patient base date (years offset relative to now)
                int yearOffset = cfg.DateRangeYears.Sample(rnd);
                var baseDate = DateTime.UtcNow.AddYears(yearOffset);

                int exams = cfg.ExamsPerPatient.Sample(rnd);
                for (int ei=0; ei<exams; ei++)
                {
                    // K unique modalities
                    int k = cfg.ModalitiesPerExam.Sample(rnd);
                    var pool = cfg.Modalities.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    k = Math.Min(k, pool.Count);
                    // Shuffle pool
                    for (int i = pool.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
                    var chosen = pool.Take(k).ToList();

                    foreach (var modality in chosen)
                    {
                        _ = generator.GenerateStudy(outputRoot, org, pid, patientName, modality, baseDate).ToList();
                    }
                }
            }
        }

        Console.WriteLine($"Done. Output at: {outputRoot}");
        return 0;
    }
}