using System;
using System.IO;

/*

    Copyright 2021 by Atkelar

    This source code is part of a retro experiment;
    It creates the required encoding for the µPF 

*/

namespace uPF1Tape
{
    static class Program
    {

        /// <summary>
        /// Writes a complete wave file into the stream
        /// </summary>
        /// <param name="target">The target stream. Must be write- and seekable.</param>
        /// <param name="data">The bytes to write into the target. Must be between 1 and 0xFFFF bytes inclusive.</param>
        /// <param name="baseAddress">The target address for the loaded content. Must be between 0 and allow the data do fit into the 64k range.</param>
        /// <param name="fileName">The "filename" to apply. Arbitrary number that is used for loading the "correct" program.</param>
        static void WriteWaveForBytes(Stream target, byte[] data, ushort baseAddress, ushort fileName)
        {
            // sanity checks for call
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!(target.CanSeek && target.CanWrite))
                throw new InvalidOperationException("The target stream must be seek- and writable!");

            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length == 0 || data.Length > 0x10000)
                throw new ArgumentOutOfRangeException(nameof(data), "The data to write must be at least one byte long and at most 64kb (practical limit would be 62 for the MPF-I)");

            if (baseAddress + data.Length > 0x10000)
                throw new ArgumentOutOfRangeException(nameof(baseAddress), "The base address plus the length of the data don't fit into the 64k limit!");

            var waveFile = new FormattedWriter(target);
            Console.WriteLine("Creating wave file for {0} bytes, based at {1:x4}, using file #{2:x4}...", data.Length, baseAddress, fileName);

            // write the WAVE file header for a mono, 8bit, 8kHz wave.
            waveFile.WriteWord(0x46464952); // RIFF
            byte[] buffer = new byte[4];
            long chunkSizeOffset = waveFile.Position;
            waveFile.WriteWord((uint)0); // temp
            waveFile.WriteWord(0x45564157); // WAVE
            // fmt header
            waveFile.WriteWord(0x20746d66); // fmt
            waveFile.WriteWord((uint)0x10); // fmt size
            waveFile.WriteWord((ushort)1);  // PCM
            waveFile.WriteWord((ushort)1);  // channels
            waveFile.WriteWord((uint)8000); // f
            waveFile.WriteWord((uint)8000); // byte rate. At mono/8bit this is the same as sample rate.
            waveFile.WriteWord((ushort)1);  // bit alingn
            waveFile.WriteWord((ushort)8);  // bits per sample
            // data block
            waveFile.WriteWord(0x61746164); // data
            long dataLengthOffset = waveFile.Position;
            waveFile.WriteWord((uint)0); // data

            // Now we are at the raw wave sample data. Each byte will be 1/8000th of a second worth of audio.
            // header format:
            // filename[2]/baseaddress[2]/endaddress[2]/checksum[1] [LSB/MSB]
            // HSYNC/HEAD/MSYNC/DATA/TSYNC
            // HSYNC = 1kHz, MSYNC+TSYNC = 2kHz, all 4000 cycles
            // Byte format: START/0/1/2/3/4/5/6/7/STOP bits
            // Bit format: 1 = 4+4 cycles of 2 and 1kHz, 0 = 8+2 cycles of 2 and 1 kHz
            // at 8kHz sample rate, 1 and 2kHz should be cycles of 4 and 8 respective...

            // "checksum" is just all the bytes added without overflow checks.
            byte sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                unchecked
                {
                    sum += data[i];
                }
            }

            WriteCycles(waveFile, 8, 4000); // HSYNC

            // Header block
            WriteWaveByte(waveFile, (byte)(fileName & 0xFF));
            WriteWaveByte(waveFile, (byte)((fileName >> 8) & 0xFF));
            WriteWaveByte(waveFile, (byte)(baseAddress & 0xFF));
            WriteWaveByte(waveFile, (byte)((baseAddress >> 8) & 0xFF));
            int endAddess = baseAddress + data.Length - 1;
            WriteWaveByte(waveFile, (byte)(endAddess & 0xFF));
            WriteWaveByte(waveFile, (byte)((endAddess >> 8) & 0xFF));
            WriteWaveByte(waveFile, sum);

            WriteCycles(waveFile, 4, 4000); // MSYNC

            // DATA block
            for (int i = 0; i < data.Length; i++)
            {
                WriteWaveByte(waveFile, data[i]);
            }

            WriteCycles(waveFile, 4, 4000); // TSYNC

            // fixup the length values of the WAVE file
            long len = waveFile.Position - dataLengthOffset - 4;
            long len2 = waveFile.Position - chunkSizeOffset - 4;
            waveFile.Position = dataLengthOffset;
            waveFile.WriteWord((uint)len);
            waveFile.Position = chunkSizeOffset;
            waveFile.WriteWord((uint)len2);
            // and done.
        }

        private static void WriteWaveByte(FormattedWriter waveFile, byte value)
        {
            WriteBit(waveFile, false);  // start
            for (int i = 0; i < 8; i++)
            {
                WriteBit(waveFile, (value & 1) != 0);
                value >>= 1;
            }
            WriteBit(waveFile, true); // stop
        }

        private static void WriteBit(FormattedWriter waveFile, bool bitValue)
        {
            if (bitValue)
            {
                WriteCycles(waveFile, 4, 4);
                WriteCycles(waveFile, 8, 4);
            }
            else
            {
                WriteCycles(waveFile, 4, 8);
                WriteCycles(waveFile, 8, 2);
            }
        }

        private static void WriteCycles(FormattedWriter waveFile, int cycleSamples, int numberOfCycles)
        {
            int halfWave = cycleSamples / 2;
            byte[] buf = new byte[1];
            for (int i = 0; i < numberOfCycles; i++)
            {
                buf[0] = 0xFF;
                for (int j = 0; j < cycleSamples; j++)
                {
                    if (j == halfWave)
                        buf[0] = 0;
                    waveFile.WriteBuffer(buf, 0, 1);
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
            {
                Console.WriteLine("Call: mpf1tape INPUTFILENAME BASEADDRESS FILENAME [OUTPUTFILENAME]");
                Console.WriteLine("   BASEADDRESS = hex 0-ffff");
                Console.WriteLine("   FILENAME = hex 0-ffff");
                return;
            }
            string inputFile = args[0];
            FileInfo fi = new FileInfo(inputFile);
            if (!fi.Exists)
            {
                Console.WriteLine("Input filename {0} not found!", inputFile);
                return;
            }
            string outputFile = Path.ChangeExtension(inputFile, ".wav");
            if (args.Length > 3)
                outputFile = args[3];
            int baseAddress = int.Parse(args[1], System.Globalization.NumberStyles.HexNumber);
            int fileNumber = int.Parse(args[2], System.Globalization.NumberStyles.HexNumber);
            if (baseAddress < 0 || baseAddress > 0xFFFF)
            {
                Console.WriteLine("Base address {0:x4} out of range!", baseAddress);
                return;
            }
            if (fileNumber < 0 || fileNumber > 0xFFFF)
            {
                Console.WriteLine("File number {0:x4} out of range!", fileNumber);
                return;
            }
            if (fi.Length <= 0 || fi.Length > 0xFFFF)
            {
                Console.WriteLine("File  length {0} out of range!", fi.Length);
                return;
            }
            if (baseAddress + fi.Length > 0x10000)
            {
                Console.WriteLine("File and base address out of range by {0} bytes!", 0x10000 - (fi.Length + baseAddress));
                return;
            }
            byte[] data = new byte[fi.Length];
            using (var f = File.OpenRead(inputFile))
            {
                int r = f.Read(data, 0, (int)fi.Length);
                if (r != fi.Length)
                {
                    Console.WriteLine("File read error: only {0} bytes read!", r);
                    return;
                }
            }
            using (var f = File.Create(outputFile))
            {
                WriteWaveForBytes(f, data, (ushort)baseAddress, (ushort)fileNumber);
            }
        }
    }
}
