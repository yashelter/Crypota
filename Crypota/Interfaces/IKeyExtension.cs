namespace Crypota;

public class RoundKey
{
    public byte[]? Key = null;
}

public interface IKeyExtension
{ 
    public RoundKey[] GetRoundKeys(byte[] key);
}