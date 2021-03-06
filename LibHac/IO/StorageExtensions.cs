﻿using System;
using System.Buffers;
using System.IO;

namespace LibHac.IO
{
    public static class StorageExtensions
    {
        public static Storage Slice(this IStorage storage, long start)
        {
            if (storage.Length == -1)
            {
                return storage.Slice(start, storage.Length);
            }

            return storage.Slice(start, storage.Length - start);
        }

        public static Storage Slice(this IStorage storage, long start, long length)
        {
            return storage.Slice(start, length, true);
        }

        public static Storage Slice(this IStorage storage, long start, long length, bool leaveOpen)
        {
            if (storage is Storage s)
            {
                return s.Slice(start, length, leaveOpen);
            }

            return new SubStorage(storage, start, length, leaveOpen);
        }

        public static Storage WithAccess(this IStorage storage, FileAccess access)
        {
            return storage.WithAccess(access, true);
        }

        public static Storage WithAccess(this IStorage storage, FileAccess access, bool leaveOpen)
        {
            return new SubStorage(storage, 0, storage.Length, leaveOpen, access);
        }

        public static Stream AsStream(this IStorage storage) => new StorageStream(storage, true);

        public static void CopyTo(this IStorage input, IStorage output, IProgressReport progress = null)
        {
            const int bufferSize = 81920;
            long remaining = Math.Min(input.Length, output.Length);
            if (remaining < 0) throw new ArgumentException("Storage must have an explicit length");
            progress?.SetTotal(remaining);

            long pos = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (remaining > 0)
                {
                    int toCopy = (int)Math.Min(bufferSize, remaining);
                    Span<byte> buf = buffer.AsSpan(0, toCopy);
                    input.Read(buf, pos);
                    output.Write(buf, pos);

                    remaining -= toCopy;
                    pos += toCopy;

                    progress?.ReportAdd(toCopy);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            progress?.SetTotal(0);
        }

        public static void WriteAllBytes(this IStorage input, string filename, IProgressReport progress = null)
        {
            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                input.CopyToStream(outFile, input.Length, progress);
            }
        }

        public static void CopyToStream(this IStorage input, Stream output, long length, IProgressReport progress = null)
        {
            const int bufferSize = 0x8000;
            long remaining = length;
            long inOffset = 0;
            var buffer = new byte[bufferSize];
            progress?.SetTotal(length);

            while (remaining > 0)
            {
                int toWrite = (int) Math.Min(buffer.Length, remaining);
                input.Read(buffer, inOffset, toWrite, 0);

                output.Write(buffer, 0, toWrite);
                remaining -= toWrite;
                inOffset += toWrite;
                progress?.ReportAdd(toWrite);
            }
        }

        public static void CopyToStream(this IStorage input, Stream output) => CopyToStream(input, output, input.Length);

        public static Storage AsStorage(this Stream stream)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, true);
        }

        public static Storage AsStorage(this Stream stream, long start)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, true).Slice(start);
        }

        public static Storage AsStorage(this Stream stream, long start, int length)
        {
            if (stream == null) return null;
            return new StreamStorage(stream, true).Slice(start, length);
        }
    }
}
