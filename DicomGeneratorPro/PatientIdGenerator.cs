using System.Text;

namespace DicomGeneratorPro;

public sealed class PatientIdGenerator
{
    private readonly Random _rnd;
    private readonly PatientIdConfig _cfg;

    public PatientIdGenerator(Random rnd, PatientIdConfig cfg)
    {
        _rnd = rnd;
        _cfg = cfg;
    }

    private static string SampleString(Random rnd, string alphabet, int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(alphabet[rnd.Next(alphabet.Length)]);
        return sb.ToString();
    }

    /// <summary>
    /// Builds: {Organization}{Sep}{Initials}{Sep}{RandCode}{Sep}{RandDigits}
    /// Returns (patientId, patientNameForDicom).
    /// </summary>
    public (string id, string human) Next(string organization)
    {
        // Defaults if user didn’t add new fields in config
        int initialsLen = _cfg.InitialsLength > 0 ? _cfg.InitialsLength : 2;
        string initialsAlphabet = string.IsNullOrWhiteSpace(_cfg.InitialsAlphabet) ? "ABCDEFGHIJKLMNOPQRSTUVWXYZ" : _cfg.InitialsAlphabet;

        int codeLen = _cfg.RandomCodeLength > 0 ? _cfg.RandomCodeLength : 6;
        string codeAlphabet = string.IsNullOrWhiteSpace(_cfg.RandomCodeAlphabet) ? "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789" : _cfg.RandomCodeAlphabet;

        int numberDigits = _cfg.RandomNumberDigits > 0 ? _cfg.RandomNumberDigits : 3;
        string sep = _cfg.Separator ?? "-";

        var initials = SampleString(_rnd, initialsAlphabet, initialsLen);
        var code = SampleString(_rnd, codeAlphabet, codeLen);
        var number = _rnd.Next((int)Math.Pow(10, numberDigits)).ToString("D" + numberDigits);

        var id = $"{organization}{sep}{initials}{sep}{code}{sep}{number}";

        // DICOM PN (PatientName) is "Family^Given^Middle^..."
        // Keep your old style of embedding org + something human-readable:
        var human = $"{organization}^{initials}";

        return (id, human);
    }
}
