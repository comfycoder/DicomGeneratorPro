namespace DicomGeneratorPro;

public sealed class PatientIdGenerator
{
    private readonly string _prefix;
    private readonly int _digits;
    private int _next;
    public PatientIdGenerator(string prefix, int digits, int startFrom) { _prefix = prefix; _digits = digits; _next = startFrom; }
    public (string id, string human) Next()
    {
        int n = _next++;
        return ($"{_prefix}{n.ToString("D"+_digits)}", n.ToString("D4"));
    }
}