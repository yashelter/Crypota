namespace Crypota.Symmetric;

public enum EncryptionStateTransform
{
    ApplyingPadding,
    RemovingPadding,
    Encrypting,
    Decrypting,
    Idle,
    Analyzing
}

// this class will make working stats
public class EncryptionState: ICloneable
{
    private readonly Lock _syncRoot = new Lock();

    private int _blocksToEncrypt;
    private int _encryptedBlocks;

    public int BlocksToEncrypt
    {
        get
        {
            lock (_syncRoot)
            {
                return _blocksToEncrypt;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _blocksToEncrypt = value;
            }
        }

    }

    public int EncryptedBlocks
    {
        get
        {
            lock (_syncRoot)
            {
                return _encryptedBlocks;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _encryptedBlocks = value;
            }
        }
    }

    private EncryptionStateTransform _transform = EncryptionStateTransform.Idle;

    public EncryptionStateTransform Transform
    {
        get
        {
            lock (_syncRoot)
            {
                return _transform;
            }
        }
        set
        {
            lock (_syncRoot)
            {
                _transform = value;
            }
        }
    }

    public object Clone()
    {
        return new EncryptionState();
    }
}