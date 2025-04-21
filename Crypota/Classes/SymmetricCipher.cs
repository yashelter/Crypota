using System.Collections.Concurrent;
using System.Numerics;

namespace Crypota.Classes;

using static CryptoAlgorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public enum CipherMode { ECB, CBC, PCBC, CFB, OFB, CTR, RD }
public enum PaddingMode { Zeros, ANSIX923, PKCS7, ISO10126 }

public class SymmetricCipher : ISymmetricCipher
{
    public byte[]? Key { get; set; }
    public byte[] EncryptBlock(byte[] block)
    {
        throw new NotImplementedException();
    }

    public byte[] DecryptBlock(byte[] block)
    {
        throw new NotImplementedException();
    }
}