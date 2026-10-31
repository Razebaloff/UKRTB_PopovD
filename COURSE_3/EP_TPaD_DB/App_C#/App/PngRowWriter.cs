using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    // RGB PNG записывается сверху вниз. Полный Bitmap на 530 млн пикселей не нужен.
    // Формат: W3C PNG, IHDR + sRGB + pHYs + последовательные IDAT + IEND.
    internal sealed class PngRowWriter : IDisposable
    {
        private readonly string target, temporary;
        private readonly FileStream file;
        private readonly IdatStream chunks;
        private readonly ZLibStream compressor;
        private readonly int width, height;
        private byte[] previous, current;
        private readonly byte[] bgr, up, paeth;
        private int rowsWritten;
        private bool complete, disposed;

        internal PngRowWriter(string path, Size size)
        {
            width = size.Width; height = size.Height;
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(size));
            target = Path.GetFullPath(path);
            temporary = target + ".partial-" + Guid.NewGuid().ToString("N");
            int rowBytes = checked(width * 3);
            previous = new byte[rowBytes]; current = new byte[rowBytes];
            bgr = new byte[(rowBytes + 3) & ~3];
            up = new byte[rowBytes]; paeth = new byte[rowBytes];
            file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536);
            try
            {
                file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                Span<byte> header = stackalloc byte[13];
                header.Clear();
                BinaryPrimitives.WriteInt32BigEndian(header, width);
                BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
                header[8] = 8; header[9] = 2; // RGB, 8 бит на канал.
                WriteChunk(file, "IHDR"u8, header);
                WriteChunk(file, "sRGB"u8, new byte[] { 0 });
                Span<byte> density = stackalloc byte[9];
                BinaryPrimitives.WriteInt32BigEndian(density, 3780);
                BinaryPrimitives.WriteInt32BigEndian(density[4..], 3780);
                density[8] = 1;
                WriteChunk(file, "pHYs"u8, density);
                chunks = new IdatStream(file);
                compressor = new ZLibStream(chunks, CompressionLevel.SmallestSize, leaveOpen: true);
            }
            catch
            {
                file.Dispose();
                File.Delete(temporary);
                throw;
            }
        }

        internal void Append(Bitmap band)
        {
            if (complete || disposed) throw new InvalidOperationException("PNG уже закрыт.");
            if (band.Width != width || rowsWritten + band.Height > height)
                throw new ArgumentException("Неверные размеры полосы.", nameof(band));
            BitmapData data = band.LockBits(new Rectangle(Point.Empty, band.Size),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                for (int y = 0; y < band.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bgr, 0, width * 3);
                    for (int x = 0; x < width * 3; x += 3)
                    {
                        current[x] = bgr[x + 2]; current[x + 1] = bgr[x + 1]; current[x + 2] = bgr[x];
                    }
                    int upCost = 0, paethCost = 0;
                    for (int x = 0; x < current.Length; x++)
                    {
                        int left = x >= 3 ? current[x - 3] : 0;
                        int above = previous[x], diagonal = x >= 3 ? previous[x - 3] : 0;
                        int p = left + above - diagonal;
                        int dl = Math.Abs(p - left), da = Math.Abs(p - above), dd = Math.Abs(p - diagonal);
                        int predictor = dl <= da && dl <= dd ? left : da <= dd ? above : diagonal;
                        byte u = unchecked((byte)(current[x] - above));
                        byte a = unchecked((byte)(current[x] - predictor));
                        up[x] = u; paeth[x] = a;
                        upCost += u < 128 ? u : 256 - u;
                        paethCost += a < 128 ? a : 256 - a;
                    }
                    bool useUp = upCost < paethCost;
                    compressor.WriteByte(useUp ? (byte)2 : (byte)4);
                    compressor.Write(useUp ? up : paeth);
                    (current, previous) = (previous, current);
                    rowsWritten++;
                }
            }
            finally { band.UnlockBits(data); }
        }

        internal void Complete()
        {
            if (complete || disposed) throw new InvalidOperationException("PNG уже закрыт.");
            if (rowsWritten != height) throw new InvalidOperationException("Записаны не все строки изображения.");
            compressor.Dispose();
            chunks.Flush();
            WriteChunk(file, "IEND"u8, ReadOnlySpan<byte>.Empty);
            file.Flush(flushToDisk: true);
            long expectedLength = file.Position;
            if (new FileInfo(temporary).Length != expectedLength)
                throw new IOException("Изменился временный файл PNG. Готовый файл не заменён.");
            file.Dispose();
            using (FileStream verify = File.OpenRead(temporary))
            {
                if (verify.Length != expectedLength) throw new IOException("PNG записан не полностью.");
                verify.Seek(-12, SeekOrigin.End);
                Span<byte> tail = stackalloc byte[12];
                verify.ReadExactly(tail);
                ReadOnlySpan<byte> end = new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
                if (!tail.SequenceEqual(end)) throw new IOException("PNG не содержит завершающего блока.");
            }
            File.Move(temporary, target, overwrite: true);
            complete = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (!complete) compressor.Dispose(); }
            finally
            {
                file.Dispose();
                if (!complete) File.Delete(temporary);
            }
        }

        private static readonly uint[] CrcTable = BuildCrcTable();
        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint c = i;
                for (int bit = 0; bit < 8; bit++) c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> header = stackalloc byte[8];
            BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
            type.CopyTo(header[4..]);
            output.Write(header); output.Write(data);
            uint crc = 0xffffffff;
            foreach (byte value in type) crc = CrcTable[(int)((crc ^ value) & 255)] ^ (crc >> 8);
            foreach (byte value in data) crc = CrcTable[(int)((crc ^ value) & 255)] ^ (crc >> 8);
            Span<byte> checksum = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(checksum, crc ^ 0xffffffff);
            output.Write(checksum);
        }

        private sealed class IdatStream(Stream output) : Stream
        {
            private readonly byte[] buffer = new byte[65536];
            private int count;
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Write(byte[] bytes, int offset, int length) => Write(bytes.AsSpan(offset, length));
            public override void Write(ReadOnlySpan<byte> bytes)
            {
                while (!bytes.IsEmpty)
                {
                    int length = Math.Min(buffer.Length - count, bytes.Length);
                    bytes[..length].CopyTo(buffer.AsSpan(count));
                    count += length; bytes = bytes[length..];
                    if (count == buffer.Length) Flush();
                }
            }
            public override void Flush()
            {
                if (count == 0) return;
                WriteChunk(output, "IDAT"u8, buffer.AsSpan(0, count));
                count = 0;
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
