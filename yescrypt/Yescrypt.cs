﻿using Fasterlimit.Yescrypt;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Drawing;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Fasterlimit.Yescrypt
{
    public class Yescrypt
    {
        /**
         * yescrypt_kdf_body(shared, local, passwd, passwdlen, salt, saltlen,
         *     flags, N, r, p, t, NROM, buf, buflen):
         * Compute scrypt(passwd[0 .. passwdlen - 1], salt[0 .. saltlen - 1], N, r,
         * p, buflen), or a revision of scrypt as requested by flags and shared, and
         * write the result into buf.
         *
         * shared and flags may request special modes as described in yescrypt.h.
         *
         * local is the thread-local data structure, allowing optimized implementations
         * to preserve and reuse a memory allocation across calls, thereby reducing its
         * overhead (this reference implementation does not make that optimization).
         *
         * t controls computation time while not affecting peak memory usage.
         *
         * Return 0 on success; or -1 on error.
         */
        public byte[] DeriveKey(byte[] passwd, byte[] salt, uint flags, uint N, uint r, int length)
        {
            uint[] V = new uint[32 * r * N];
            uint[] B = new uint[32 * r ];
            byte[] sha256 = new byte[32];
            
            byte[] key = Encoding.ASCII.GetBytes("yescrypt");
            byte[] passwdHash; 
            using (var hmacsha256 = new HMACSHA256(key))
            {
                passwdHash = hmacsha256.ComputeHash(passwd);                
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(passwdHash, salt, 1, HashAlgorithmName.SHA256))
            {
                var bytes = pbkdf2.GetBytes(B.Length * 4);
                Helper.WordsFromBytes( bytes,0, B, 0, B.Length);
                Array.Copy(bytes, sha256, sha256.Length);
            }

            if ((flags & Flags.YESCRYPT_RW) !=0)
            {
                Smix.Mix(B,0, r, N, flags, V, ref sha256);
            }
            else
            {
                /* 3: B_i <-- MF(B_i, N) */
                Smix.Mix( B, 4 * r, r, N, flags, V, ref sha256);                
            }

            /* 5: DK <-- PBKDF2(P, B, 1, dkLen) */
            byte[] eSalt = new byte[B.Length * 4];
            Helper.WordsToBytes(B,0,eSalt, 0, B.Length);
            byte[] dk;
            using (var pbkdf2 = new Rfc2898DeriveBytes(sha256, eSalt, 1, HashAlgorithmName.SHA256))
            {
                dk = pbkdf2.GetBytes(length < 32 ? 32 : length);
            }

            /*
             * Except when computing classic scrypt, allow all computation so far
             * to be performed on the client.  The final steps below match those of
             * SCRAM (RFC 5802), so that an extension of SCRAM (with the steps so
             * far in place of SCRAM's use of PBKDF2 and with SHA-256 in place of
             * SCRAM's use of SHA-1) would be usable with yescrypt hashes.
             */

            /* Compute ClientKey */
            byte[] dk32 = new byte[32];
            Array.Copy(dk, dk32, dk32.Length);
            using (var hmacsha256 = new HMACSHA256(dk32))
            {
                sha256 = hmacsha256.ComputeHash(Encoding.ASCII.GetBytes("Client Key"));
                sha256 = SHA256.HashData(sha256);

                if (length > sha256.Length)
                {
                    Array.Copy(sha256, dk, sha256.Length);                                   
                }
                else
                {
                    dk = new byte[length];
                    Array.Copy(sha256, dk, dk.Length);
                }

                return dk;                
                
            }
        }
    }
}
