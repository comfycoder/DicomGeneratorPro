using System.Text;

namespace DicomGeneratorPro;

public sealed class OrgPrefixGenerator
{
    private readonly Random _rnd; private readonly OrgPrefixConfig _cfg;
    public OrgPrefixGenerator(Random rnd, OrgPrefixConfig cfg) { _rnd = rnd; _cfg = cfg; }
    public string Next()
    {
        var sb = new StringBuilder(_cfg.PrefixLength);
        for (int i=0;i<_cfg.PrefixLength;i++) sb.Append(_cfg.Alphabet[_rnd.Next(_cfg.Alphabet.Length)]);
        return sb.ToString();
    }
}