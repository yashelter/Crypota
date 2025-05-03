namespace Crypota.Interfaces;


public interface IEncryptionTransformation
{
    public void EncryptionTransformation(Span<byte> state, Span<byte> roundKey);
    public void DecryptionTransformation(Span<byte> state, Span<byte> roundKey);
}