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

        //**TODO: we **could make some sort of buffer recycling mech here to reduce GC
    }
}