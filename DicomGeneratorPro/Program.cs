using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DicomGeneratorPro
{
    /// <summary>
    /// Standalone config loader (JSON, comments allowed, case-insensitive).
    /// </summary>
    public static class ConfigUtil
    {
        public static AppConfig Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Config path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Config file not found: {path}", path);

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

            if (cfg == null)
                throw new InvalidOperationException("Failed to deserialize configuration.");
            return cfg;
        }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                // Expect: --config <path>
                if (args.Length < 2 || !string.Equals(args[0], "--config", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Usage: DicomGeneratorPro --config <path-to-config.json>");
                    return 2;
                }

                var cfg = ConfigUtil.Load(args[1]);

                // Random (deterministic when Seed is provided)
                var rnd = new RandomProvider(cfg.Seed).Rng;

                // Output root (create the <root>/Dicom container folder)
                var outputRoot = Path.GetFullPath(cfg.OutputRoot);
                Directory.CreateDirectory(Path.Combine(outputRoot, "Dicom"));

                // Generators
                var orgGen = new OrgPrefixGenerator(rnd, cfg.OrgPrefix);

                // NEW: composite Patient ID generator configuration (Organization + Initials + 6-char code + 3-digit number)
                var pidGen = new PatientIdGenerator(rnd, cfg.PatientId);

                var generator = new DicomStudyGenerator(cfg, rnd);

                // ---- Metrics
                var sw = Stopwatch.StartNew();
                int orgCount = 0, patientCount = 0, examCount = 0, studyCount = 0, seriesCount = 0, fileCount = 0;
                var filesByModality = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (int oi = 0; oi < cfg.NumOrganizations; oi++)
                {
                    orgCount++;
                    var org = orgGen.Next();

                    int patients = cfg.PatientsPerOrg.Sample(rnd);
                    for (int pi = 0; pi < patients; pi++)
                    {
                        // NEW: pass organization into patient-id builder
                        var (patientId, patientName) = pidGen.Next(org);
                        patientCount++;

                        Console.WriteLine($"[Patient] Org={org} PatientId={patientId} ({patientName})");

                        // Stable-ish time window per patient
                        var yearsBack = cfg.DateRangeYears.Sample(rnd);
                        var baseDateUtc = DateTime.UtcNow.AddYears(-yearsBack);

                        int exams = cfg.ExamsPerPatient.Sample(rnd);
                        for (int ei = 0; ei < exams; ei++)
                        {
                            examCount++;

                            // Choose modalities for this exam using weighted rules
                            int k = Math.Max(1, cfg.ModalitiesPerExam.Sample(rnd));
                            var modalities = ChooseModalitiesForExam(cfg, rnd, k);

                            foreach (var modality in modalities)
                            {
                                var result = generator.GenerateStudy(
                                    outputRoot,
                                    org,
                                    patientId,
                                    patientName,
                                    modality,
                                    baseDateUtc);

                                studyCount++;
                                seriesCount += result.SeriesCount;
                                fileCount += result.FileCount;

                                if (!filesByModality.ContainsKey(modality))
                                    filesByModality[modality] = 0;
                                filesByModality[modality] += result.FileCount;
                            }
                        }
                    }
                }

                sw.Stop();
                Console.WriteLine();
                Console.WriteLine("== Metrics ==");
                Console.WriteLine($"Organizations: {orgCount}");
                Console.WriteLine($"Patients:      {patientCount}");
                Console.WriteLine($"Exams:         {examCount}");
                Console.WriteLine($"Studies:       {studyCount}");
                Console.WriteLine($"Series:        {seriesCount}");
                Console.WriteLine($"Files:         {fileCount}");
                Console.WriteLine();

                Console.WriteLine("Files by Modality:");
                foreach (var kvp in filesByModality.OrderBy(k => k.Key))
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");

                Console.WriteLine();
                Console.WriteLine($"Done in {sw.Elapsed:mm\\:ss\\.fff}. Output at: {outputRoot}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        // ---- Weighted exam mix helper ----
        // Implements:
        // - CtPtPercent: target share of *exact* CT+PT pairs
        // - CtNmPercent: target share of *exact* CT+NM pairs
        // - MixedPercent: target share of "mixed" exams (>=2 modalities sampled from cfg.Modalities)
        //
        // k is the desired number of modalities for this exam (from ModalitiesPerExam).
        private static List<string> ChooseModalitiesForExam(AppConfig cfg, Random rnd, int k)
        {
            // Normalize modality list
            var all = (cfg.Modalities ?? new List<string> { "CT", "PT", "NM" })
                      .Where(m => !string.IsNullOrWhiteSpace(m))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList();

            if (all.Count == 0)
                return new List<string> { "CT" };

            // Weighted bucket selection
            int ctpt = Math.Max(0, cfg.ExamMix?.CtPtPercent ?? 0);
            int ctnm = Math.Max(0, cfg.ExamMix?.CtNmPercent ?? 0);
            int mixed = Math.Max(0, cfg.ExamMix?.MixedPercent ?? 0);

            int total = Math.Max(1, ctpt + ctnm + mixed);
            int roll = rnd.Next(total);
            string bucket = roll < ctpt ? "CTPT"
                           : roll < (ctpt + ctnm) ? "CTNM"
                           : "MIXED";

            // Exact pairs buckets only if k == 2 and both modalities exist
            if (bucket == "CTPT" && k == 2 && all.Any(x => x.Equals("CT", StringComparison.OrdinalIgnoreCase)) && all.Any(x => x.Equals("PT", StringComparison.OrdinalIgnoreCase)))
                return new List<string> { "CT", "PT" };
            if (bucket == "CTNM" && k == 2 && all.Any(x => x.Equals("CT", StringComparison.OrdinalIgnoreCase)) && all.Any(x => x.Equals("NM", StringComparison.OrdinalIgnoreCase)))
                return new List<string> { "CT", "NM" };

            // Mixed (or fallback when pair not possible): sample k distinct from all
            var pool = new List<string>(all);
            Shuffle(pool, rnd);
            return pool.Take(Math.Min(k, pool.Count)).ToList();
        }

        private static void Shuffle<T>(IList<T> list, Random rnd)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
