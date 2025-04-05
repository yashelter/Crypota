namespace Crypota;

public class RoundKey
{
    public List<byte> Key = new List<byte>();
}

public interface IKeyExtension
{
    public List<RoundKey> GetRoundKeys(List<byte> key);
}