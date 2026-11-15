using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace SpaceApps.Enlace
{
    /// <summary>
    /// Protocolo AEGIS de telemetría nave → Tierra (versión 1). Idéntico en los receptores de Python y Rust
    /// (ver ENLACE_ESPACIO_TIERRA.md):
    ///
    ///   JSON → deflate → AES-256-CBC (IV aleatorio) → HMAC-SHA256 → CRC-32 → Hamming(7,4) + entrelazado → canal
    ///
    /// Trama interna (big-endian):
    ///   "AEGS"(4) | versión(1)=1 | flags(1) bit0=deflate | sesión(4) | secuencia(4) | t_ms(8) | IV(16) | largo(4)
    ///   | cifrado(largo) | HMAC(32) sobre todo lo anterior | CRC-32(4) sobre todo lo anterior
    /// Claves: k_cif = HMAC-SHA256(maestra, "AEGIS-ENC-v1"), k_mac = HMAC-SHA256(maestra, "AEGIS-MAC-v1").
    /// Bloque FEC: largo(4) + trama → cada byte en 2 palabras Hamming(7,4) (1 byte c/u) → entrelazado de
    /// profundidad 16 (por columnas) para repartir las ráfagas de ruido entre muchas palabras.
    /// </summary>
    public static class EnlaceProtocolo
    {
        public const int Version = 1, HeaderLen = 42, MacLen = 32, Depth = 16;
        static readonly byte[] Magic = { (byte)'A', (byte)'E', (byte)'G', (byte)'S' };

        // ------------------------------------------------------------ claves

        /// <summary>Lee la clave maestra (64 caracteres hexadecimales; se ignoran líneas que empiezan con #).</summary>
        public static byte[] LoadMasterKey(string path)
        {
            var sb = new StringBuilder();
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                sb.Append(t);
            }
            string hex = sb.ToString();
            if (hex.Length != 64) throw new Exception("La clave debe tener 64 caracteres hexadecimales (256 bits)");
            var key = new byte[32];
            for (int i = 0; i < 32; i++) key[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return key;
        }

        public static string NewMasterKeyHex()
        {
            var k = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(k);
            var sb = new StringBuilder();
            foreach (var b in k) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static (byte[] enc, byte[] mac) DeriveKeys(byte[] master)
        {
            using (var h = new HMACSHA256(master))
                return (h.ComputeHash(Encoding.ASCII.GetBytes("AEGIS-ENC-v1")), h.ComputeHash(Encoding.ASCII.GetBytes("AEGIS-MAC-v1")));
        }

        // ------------------------------------------------------------ trama segura

        public static byte[] BuildFrame(string json, byte[] kEnc, byte[] kMac, uint session, uint seq, long tMs, bool compress)
        {
            byte[] plain = Encoding.UTF8.GetBytes(json);
            if (compress) plain = Deflate(plain);

            var iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(iv);
            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256; aes.Key = kEnc; aes.IV = iv;
                aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            }

            var f = new byte[HeaderLen + cipher.Length + MacLen + 4];
            Array.Copy(Magic, 0, f, 0, 4);
            f[4] = Version;
            f[5] = (byte)(compress ? 1 : 0);
            PutU32(f, 6, session);
            PutU32(f, 10, seq);
            PutU64(f, 14, (ulong)tMs);
            Array.Copy(iv, 0, f, 22, 16);
            PutU32(f, 38, (uint)cipher.Length);
            Array.Copy(cipher, 0, f, HeaderLen, cipher.Length);
            int macAt = HeaderLen + cipher.Length;
            using (var h = new HMACSHA256(kMac))
            {
                var mac = h.ComputeHash(f, 0, macAt);
                Array.Copy(mac, 0, f, macAt, MacLen);
            }
            PutU32(f, macAt + MacLen, Crc32(f, 0, macAt + MacLen));
            return f;
        }

        static byte[] Deflate(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        static void PutU32(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
        static void PutU64(byte[] b, int o, ulong v) { for (int i = 0; i < 8; i++) b[o + i] = (byte)(v >> (56 - 8 * i)); }

        // ------------------------------------------------------------ CRC-32 (IEEE 802.3, igual que zlib.crc32)

        static readonly uint[] CrcTable = MakeCrcTable();

        static uint[] MakeCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        public static uint Crc32(byte[] b, int offset, int count)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = offset; i < offset + count; i++) c = CrcTable[(c ^ b[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        // ------------------------------------------------------------ Hamming(7,4) + entrelazado

        /// <summary>Palabra Hamming(7,4) en los bits 0..6: p1 p2 d1 p3 d2 d3 d4 (posiciones 1..7).</summary>
        public static byte HammingEncode(int nibble)
        {
            int d1 = (nibble >> 3) & 1, d2 = (nibble >> 2) & 1, d3 = (nibble >> 1) & 1, d4 = nibble & 1;
            int p1 = d1 ^ d2 ^ d4, p2 = d1 ^ d3 ^ d4, p3 = d2 ^ d3 ^ d4;
            return (byte)(p1 | p2 << 1 | d1 << 2 | p3 << 3 | d2 << 4 | d3 << 5 | d4 << 6);
        }

        public static byte[] FecEncode(byte[] frame)
        {
            var data = new byte[frame.Length + 4];
            PutU32(data, 0, (uint)frame.Length);
            Array.Copy(frame, 0, data, 4, frame.Length);
            int n = data.Length * 2;
            int rows = (n + Depth - 1) / Depth;
            var cw = new byte[rows * Depth];                 // relleno = Hamming(0) = 0
            for (int i = 0; i < data.Length; i++)
            {
                cw[2 * i] = HammingEncode(data[i] >> 4);
                cw[2 * i + 1] = HammingEncode(data[i] & 0xF);
            }
            var outp = new byte[cw.Length];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < Depth; c++)
                    outp[c * rows + r] = cw[r * Depth + c];
            return outp;
        }
    }
}
