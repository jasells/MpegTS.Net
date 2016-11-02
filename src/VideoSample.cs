using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MpegTS
{

    /// <summary>
    /// encapsulates info needed for decoding
    /// </summary>
    public class VideoSample:IDisposable
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

        /// <summary>
        /// writes the raw video data to the given stream, returns # bytes written
        /// </summary>
        /// <param name="outStream"></param>
        /// <returns></returns>
        public uint WriteToStream(System.IO.Stream outStream)
        {
            uint val = 0;

            if (Pes != null)
                val = Pes.WriteToStream(outStream);
            else
            {
                val = (uint)Length;
                outStream.Write(b, 0, Length);
            }
            return val;
        }

        public bool IsComplete
        {
            get
            {
                if (myPes != null)
                    return myPes.IsComplete;
                else
                    return false;
            }
        }

        internal VideoSample() { }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed objects.
                    if (myPes != null)
                        myPes.Dispose(b);

                    b = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~VideoSample() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        /// <summary>
        /// reclaims and recycles memory used to buffer data to the buffer pool
        /// </summary>
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