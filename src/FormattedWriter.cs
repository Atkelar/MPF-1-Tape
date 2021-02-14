using System;
using System.IO;

namespace uPF1Tape
{
    internal class FormattedWriter
    {
        public FormattedWriter(System.IO.Stream wrap)
        {
            this._Wrapped = wrap;
        }
        byte[] buffer = new byte[4]; // NOT thread save at all, but prevent re-allocation for this simple program!
        private readonly Stream _Wrapped;

        public long Position { get => _Wrapped.Position; set => _Wrapped.Position = value; }

        public void WriteBuffer(byte[] buffer, int offset, int length)
        {
            _Wrapped.Write(buffer, offset, length);
        }
        public void WriteWord(ushort word)
        {
            buffer[0] = (byte)(word & 0xFF);
            buffer[1] = (byte)((word >> 8) & 0xFF);
            _Wrapped.Write(buffer, 0, 2);
        }
        public void WriteWord(uint word)
        {
            buffer[0] = (byte)(word & 0xFF);
            buffer[1] = (byte)((word >> 8) & 0xFF);
            buffer[2] = (byte)((word >> 16) & 0xFF);
            buffer[3] = (byte)((word >> 24) & 0xFF);
            _Wrapped.Write(buffer, 0, 4);
        }
    }
}