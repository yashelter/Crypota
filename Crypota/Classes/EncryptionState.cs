namespace Crypota.Classes;

public enum EncryptionStateTransform
{
    ApplyingPadding,
    RemovingPadding,
    Encrypting,
    Decrypting
}

// this class will make working stats
public class EncryptionState
{
    public int BlocksToEncrypt{ get; set; }
    public int EncryptedBlocks{ get; set; }
    public EncryptionStateTransform Transform { get; set; }
}