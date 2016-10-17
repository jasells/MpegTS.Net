using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MpegTS
{

    /// <summary>
    /// encapsulates info needed for decoding
    /// </summary>
    public class VideoSample
    {
        private byte[] b;
        public byte[] Buffer { get { return b; } internal set { Length = (value != null)? (b = value).Length : 0; } }

        public long PresentationTimeStamp { get; internal set; }

        public int Length { get; internal set; }

        private PacketizedElementaryStream myPes;
        internal PacketizedElementaryStream Pes
        {
            get { return myPes; }
            set { myPes = value; Length = myPes.EstimateBufferSize(); }
        }

        //public int SafeBufferLen { get { return Pes.EstimateBufferSize(); } }

        public void WriteToStream(System.IO.Stream outStream)
        {
            if (Pes != null)
                Pes.WriteToStream(outStream);
            else
                outStream.Write(b, 0, Length);
        }

        internal VideoSample() { }
    }
}