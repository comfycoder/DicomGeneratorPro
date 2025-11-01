using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DicomGeneratorPro
{
    /// <summary>Standalone config loader so it’s always in scope.</summary>
    public static class ConfigUtil
    {
        public static AppConfig Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Config path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Config file not found.", path);

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

            if (cfg == null) throw new InvalidOperationException("Failed to parse configuration file.");

            // Ensure case-insensitive modality profile lookup
            cfg.Profiles = new Dictionary<string, ModalityProfile>(cfg.Profiles, StringComparer.OrdinalIgnoreCase);
            return cfg;
        }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2 || !string.Equals(args[0], "--config", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Usage: DicomGeneratorPro --config <path-to-config.json>");
                return 2;
            }

            // ✅ Explicit call so there’s never a “LoadConfig not found”.
            var cfg = ConfigUtil.Load(args[1]);

            var rnd = new RandomProvider(cfg.Seed).Rng;
            var outputRoot = Path.GetFullPath(cfg.OutputRoot);
            Directory.CreateDirectory(Path.Combine(outputRoot, "Dicom"));

            var orgGen = new OrgPrefixGenerator(rnd, cfg.OrgPrefix);
            var pidGen = new PatientIdGenerator(cfg.PatientId.Prefix, cfg.PatientId.Digits, cfg.PatientId.StartFrom);
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
                    patientCount++;

                    var (pid, human) = pidGen.Next();
                    var patientName = $"{org}^{human}";

                    // Print each patient name (requirement #1)
                    Console.WriteLine($">>> Patient: {patientName}  (ID: {pid})");

                    // Per-patient base date (years offset relative to now)
                    int yearOffset = cfg.DateRangeYears.Sample(rnd);
                    var baseDate = DateTime.UtcNow.AddYears(yearOffset);

                    int exams = cfg.ExamsPerPatient.Sample(rnd);
                    for (int ei = 0; ei < exams; ei++)
                    {
                        examCount++;

                        // Choose K unique modalities
                        int k = cfg.ModalitiesPerExam.Sample(rnd);
                        var pool = cfg.Modalities.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        k = Math.Min(k, pool.Count);

                        // Fisher–Yates shuffle
                        for (int i = pool.Count - 1; i > 0; i--)
                        {
                            int j = rnd.Next(i + 1);
                            (pool[i], pool[j]) = (pool[j], pool[i]);
                        }
                        var chosen = pool.Take(k).ToList();

                        foreach (var modality in chosen)
                        {
                            studyCount++;

                            var res = generator.GenerateStudy(
                                outputRoot, org, pid, patientName, modality, baseDate);

                            seriesCount += res.SeriesCount;
                            fileCount += res.FileCount;
                            filesByModality[modality] = filesByModality.GetValueOrDefault(modality) + res.FileCount;
                        }
                    }
                }
            }

            sw.Stop();

            // ---- Metrics summary (requirement #3)
            Console.WriteLine();
            Console.WriteLine("==== Generation Summary ====");
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
    }
}
