# MpegTS.Net
PCL that can decode MpegTS header info and extract embedded streams for decode by a codec.

Basic flow of calling code:

Open some stream (file, socket, whatever).

Read 188 bytes, then pass the chunk of data to the extractor.

pseudo code:

var buffEx = new BufferExtractor();
var input = new Stream("some file");
byte[] buff = new byte[188];

input.FillBuffer(buff);

buffEx.AddRaw(buff);

//check for complete chunk of raw embedded stream data

if(buffEx.SampleCount > 0)//there is at least one complete sample of stream data
  codec.DecodeBuffer(buffEx.DequeueNextSample().Buffer);//pass the raw media stream buffer to the codec
