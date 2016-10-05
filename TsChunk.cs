using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{
    /// <summary>
    /// A readonly (by default) chunk of Ts buffer
    /// </summary>
    public class TsChunk : System.IO.MemoryStream
    {
        internal protected byte[] data;

        public byte this[long index]
        {
            get
            {
                long p = Position;
                Position = index;
                byte val = (byte)ReadByte();
                Position = p;
                return val;
            }
            set
            {
                long p = Position;
                Position = index;
                WriteByte(value);
                Position = p;
            }
        }

        //public TsChunk() : base() { }

        public TsChunk(byte[] b, bool writable = false) : base(b, writable) { data = b; }

        /// <summary>
        /// index of the source buffer @ which this stream begins<para/>
        /// we need this to keep track of the original source data start index
        /// </summary>
        private int sourceIndex;

        public TsChunk(byte[] b, int index, int count, bool writable = false)
            : base(b, index, count, writable)
        {
            data = b;
            sourceIndex = index;
        }


        public new int Length
        {
            get
            {
                return (int)base.Length;//we won't be dealing with data that long.
            }
        }

        public TsChunk GetSubStream(int index, int count, bool writable = false)
        {
            if (data == null)
                return new TsChunk(data = this.ToArray(), index, count, writable);
            else
                return new TsChunk(data, sourceIndex + index, count, writable);
        }

        /// <summary>
        /// Return true if this chunk is multiple of 188 bytes... do other checks?
        /// </summary>
        public bool IsValidTs
        {
            get { return (data.Length % TsPacket.PacketLength == 0); }
        }

        public List<TsPacket> GetTsPackets()
        {
            int len = data.Length / TsPacket.PacketLength;
            var l = new List<TsPacket>(len);

            for(int i=0; i<len; ++i)
            {
                var chunk = GetSubStream(i * TsPacket.PacketLength, TsPacket.PacketLength);//new TsChunk(data, i*TsPacket.PacketLength,  TsPacket.PacketLength);
                l.Add(new TsPacket(chunk));
            }

            return l;
        }
    }
}