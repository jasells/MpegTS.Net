using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace MpegTS
{
    /// <summary>
    /// Represents a data stream (like video) spanning multiple Ts packets.
    /// 
    /// https://en.wikipedia.org/wiki/Packetized_elementary_stream
    /// </summary>
    public class PacketizedElementaryStream:IDisposable
    {
        /// <summary>
        /// look this value after the AdaptationField in the stream to 
        /// indicate the beginning of a video stream payload.
        /// </summary>
        public const uint VideoStartCode =  0xE0010000;//reversed byte order for little-endian systems

        //public static bool IsPES(TsPacket ts)
        //{
        //    bool val = false;

        //    val = (BitConverter.ToInt32(ts.data, ts.PayloadStart) == VideoStartCode);

        //    return val;
        //}

        internal Queue<TsPacket> packets;
        /// <summary>
        /// manages buffers for us
        /// </summary>
        private BufferExtractor myExtractor;

        private ushort ExtensionLen
        {
            get
            {
                int start = packets.Peek().PayloadStart + 4;
                return (ushort)(Header[start] << 8 | Header[start+1]);

                //var b = new byte[2];
                //Header.Read()
                //return
                //    BitConverter.ToUInt16(Header, packets.Peek().PayloadStart + 4);
            }
        }

        private TsChunk Header
        {
            get { return packets.Peek().data; }
        }
        private int payloadIndex;


        public bool IsComplete { get; private set; }
        /// <summary>
        /// Continuity counter is used to track if TS packets are missing.
        /// For TS packet 0...n, TSn+1.CC == (TSn.CC + 1)
        /// </summary>
        private int lastCC;

        public bool IsValid
        {
            get
            {
                int start = packets.Peek().PayloadStart;
                if (start < 4 || start + 4 > Header.Length)
                    return false;
                else
                {
                    long p = Header.Position;
                    Header.Position = packets.Peek().PayloadStart;

                    byte[] h = new byte[4];
                    Header.Read(h, 0, h.Length);

                    Header.Position = p;//reset cursor

                    return BitConverter.ToUInt32(h, 0) == VideoStartCode;
                }
            }
        }

        public PacketizedElementaryStream(BufferExtractor extractor, TsPacket first)
        {
            if (extractor == null || first == null)
                throw new ArgumentNullException("extractor or first", "no ctor parameters may be null");

            myExtractor = extractor;

            IsComplete = true;//we assume it is complete until we prove it is not

            packets = new Queue<TsPacket>(4);
            packets.Enqueue(first);

            lastCC = first.ContinuityCounter;

            payloadIndex = first.PayloadStart;
        }

        /// <summary>
        /// Add another Ts packet to this stream to be re-built later.
        /// </summary>
        /// <param name="next"></param>
        public void Add(TsPacket next)
        {
            packets.Enqueue(next);

            //check for missed packets
            if (lastCC < 15 &&
                next.ContinuityCounter != lastCC + 1)
            {
                IsComplete = false;
            }
            else if (lastCC == 15 && next.ContinuityCounter != 0)
                IsComplete = false;

            lastCC = next.ContinuityCounter;
        }
        //this is a safe size to use to estimate a buffer len to hold all child TsPackets
        const int packLen = TsPacket.PacketLength - 4;//each Ts packet has *at least* 4 bytes of TsHeader.
        internal int EstimateBufferSize()
        {
            //each Ts packet has *at least* 4 bytes of TsHeader.
            return packets.Count * packLen;
        }

        internal List<byte[]> GetBuffers()
        {
            var ret = new List<byte[]>();

            foreach (var packet in packets)
            {
                ret.Add(packet.data.data);
            }

            return ret;
        }

        /// <summary>
        /// byte count of this PES in the first TsPacket
        /// </summary>
        public int PesLen
        {
            get
            {
                var p = packets.Peek();

                return p.data.Length - payloadIndex;
            }
        }

        public int PesHeaderLen
        {
            get { return Header[payloadIndex + 8]; }
        }

        /// <summary>
        /// Presentation time stamp
        /// </summary>
        public long PTS
        {
            get
            {
                if (!HasPts || PesHeaderLen < 5)
                    return 0;

                int ptsi = payloadIndex+9;
                var data = Header;//hang onto a ref to the data buffer.
                //ByteBuffer hd = ByteBuffer.wrap(headerData);
                long pts = (((data[ptsi++] & 0x0e) << 29)
                            | ((data[ptsi++] & 0xff) << 22)
                            | ((data[ptsi++] & 0xfe) << 14)
                            | ((data[ptsi++] & 0xff) << 7)
                            | ((data[ptsi++] & 0xfe) >> 1));

                return pts;
            }
        }

        public bool HasPts
        {
            get { return (Header[payloadIndex + 7] & 0x80) > 0; }
        }

        public int PayloadStart
        {
            get { return StartCodeLen + PesExtLen + PesHeaderLen; }
        }

        public const int PesExtLen = 5;//bytes of ext PES header 
        public const int StartCodeLen = 4;

        public bool GetPayload(byte[] buffer)
        {
            //need to pull out NAL's now to pass to the decoder
            //http://stackoverflow.com/questions/1685494/what-does-this-h264-nal-header-mean
            //H.264 spec docs:  see Table 7-1 for NALu ID's
            //http://www.itu.int/rec/T-REC-H.264-201304-S

            //for now, let's just try to strip out all the header bytes, leaving only video stream bytes

            if (!IsValid)//we don't have a PES start code!
                return false;

            //let's do proper clean up... 
            using (var ms = new System.IO.MemoryStream(buffer))//try to get an estimate of the size needed to avoid re-sizing
                WriteToStream( ms);

            return true;
        }
        Queue<TsPacket> tmpQ;
        internal uint WriteToStream(System.IO.Stream outStream)
        {
            int startOfPayload = PayloadStart;//get this now, so we don't try to access the queue later.

            uint startPos = (uint)outStream.Position;

            //start with this packet's payload len...
            int firstLen = PesLen - startOfPayload;//-startcode/prefix(4) -header(5byte, usually)
            //int vidLen = firstLen;
            TsPacket p;

            //create a tmp que to stuff the packets back into so we don't lose them
            if(tmpQ == null)
                tmpQ = new Queue<TsPacket>(packets.Count);

            bool start = true;
            //get total byte count for reassembled PES
            while (packets.Count > 0)
            {
                tmpQ.Enqueue(p = packets.Dequeue());//hang onto the ref

                //vidLen += p.data.Length - p.PayloadStart;

                //get a memoryStream to the payload
                using (var s = p.GetPayload())
                {
                    if (!start)
                    {
                        //if (packets.Count > 0)
                        s.CopyTo(outStream);//no PES header stuff in following packets
                                     //else//need to trim trailing 0's
                                     //{
                                     //    using (var s2 = p.GetPayload(false))
                                     //        s2.CopyTo(ms);
                                     //}
                    }
                    else//first packet
                    {
                        s.Position = startOfPayload;//move past the header/start bytes

                        s.CopyTo(outStream);
                    }
                }

                start = false;
            }

            //swap queue's
            var q = packets;
            packets = tmpQ;
            tmpQ = q;

            return (uint)outStream.Position - startPos;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    //recycle all mem buffs here
                    myExtractor.RecyclePES(this);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~PacketizedElementaryStream() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        internal void Dispose(byte[] largeBuff)
        {
            if(largeBuff != null)
                myExtractor.ReturnLargeBuffer(largeBuff);

            Dispose(true);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}