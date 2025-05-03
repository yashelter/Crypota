namespace Crypota.Symmetric.Rc6;

using System;

public class Rc6KeyExpansion
{
    private const int W = 32;
    private const uint P = 0xB7E15163;
    private const uint Q = 0x9E3779B9;
    
    private readonly int _rounds;
    private readonly int _keySize;

    public Rc6KeyExpansion(int rounds = 20)
    {
        _rounds = rounds;
    }


    public uint[] GenerateRoundKeys(byte[] key)
    {
        uint[] L = BytesToWords(key);
        
        int c = Math.Max(1, key.Length / 4);
        uint[] S = new uint[2 * _rounds + 4];
        S[0] = P;
        
        for (int i = 1; i < S.Length; i++)
            S[i] = S[i - 1] + Q;


        uint A = 0, B = 0;
        int iIdx = 0, jIdx = 0;
        int n = 3 * Math.Max(S.Length, c);

        for (int k = 0; k < n; k++)
        {
            A = S[iIdx] = RotateLeft(S[iIdx] + A + B, 3);
            B = L[jIdx] = RotateLeft(L[jIdx] + A + B, (int)(A + B));
            
            iIdx = (iIdx + 1) % S.Length;
            jIdx = (jIdx + 1) % c;
        }
        return S;
    }
    
    
    private uint[] BytesToWords(byte[] key)
    {
        uint[] result = new uint[key.Length / 4];
        for (int i = 0; i < result.Length; i++)
            result[i] = BitConverter.ToUInt32(key, i * 4);
        return result;
    }
    
    
    private static uint RotateLeft(uint value, int shift) 
        => (value << shift) | (value >> (W - shift));
}