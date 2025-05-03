namespace Crypota;


public interface IKeyExtension
{ 
    public Memory<byte>[] GetRoundKeys(byte[] key);
}