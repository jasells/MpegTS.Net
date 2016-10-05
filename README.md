# MpegTS.Net
PCL that can decode MpegTS header info and extract embedded streams for decode by a codec.

Basic flow of calling code:

Open some stream (file, socket, whatever).

Read 188 bytes, then pass the chunk of data to the extractor.

some simplified pseudo code:

    public class Extractor
    {
       
        public void Run()
        {
            var finfo = new System.IO.FileInfo("c:\movie.mpeg");
            var fs = finfo.OpenRead();
            var buffEx = new BufferExtractor();

            while (fs.CanRead && buffEx.SampleCount == 0)
            {
                if (fs.Length - fs.Position < 188)
                {
                    eof = true;
                    break;//we're @ EOF
                }

                //we need a new buffer every loop!
                buff = new byte[188];
                bytes = await fs.ReadAsync(buff, 0, buff.Length)
                                .ConfigureAwait(false);

                //push the raw data to our custom extractor
                if (!buffEx.AddRaw(buff))
                {
                    Log.Debug("ExtractorActivity,   ", " ----------bad TS packet!");

                    //find next sync byte and try again
                    fs.Position -= buff.Length
                                  - buff.ToList().IndexOf(MpegTS.TsPacket.SyncByte);
                }
            }
            
            //buffex must have a stream sample ready
            //get the raw video stream, stripped of Mpeg TS headers
            var sample = buffEx.DequeueNextSample();
            
            //pass buffer to the codec
            codec.Decode(sample.Buffer);
            
        }        
    }
