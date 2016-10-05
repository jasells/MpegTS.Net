# MpegTS.Net
PCL that can decode MpegTS header info and extract embedded streams for decode by a codec.

Basic flow of calling code:

Open some stream (file, socket, whatever).

Read 188 bytes, then pass the chunk of data to the extractor.

