using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{
    public class StreamExtractor:BufferExtractor
    {
        System.IO.Stream mySource;
        public StreamExtractor(System.IO.Stream source)
        {
            mySource = source;
        }

        //find the next Mpeg TS chunk
        public void Advance()
        {
            var b = new byte[188];


        }
    }
}