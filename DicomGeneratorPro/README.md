# DicomGeneratorPro

Generates synthetic DICOM with a strict, human-friendly folder structure:

```
<OutputRoot>/Dicom/<Organization>/<PatientId>/<YYYYMMDD_HHMMSS StudyDescription>/<Modality>/<SeriesDescription>/IM000001.dcm
```

## Build & Run

```bash
dotnet build DicomGeneratorPro.sln
dotnet run --project DicomGeneratorPro -- --config DicomGeneratorPro/Configs/config.json
```

## Highlights
- Config schema matches your original (OrgPrefix.PrefixLength, PatientId { Prefix, Digits, StartFrom }, ModalitiesPerExam, Modalities including SR, DateRangeYears).
- Deterministic RNG via `Seed` (applies to all choices).
- K unique modality selection per exam.
- Per-patient base date used for all that patient's studies.
- Global DICOM defaults with per-modality overrides and fallbacks.
- Correct SOP Class mapping for SR.