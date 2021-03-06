﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace MpegTS
{

    //    public interface ISampleReadyCallback
    //    {
    //        void SampleReadyCallbackHandler(int count);
    //    }

    /// <summary>
    /// This class replaces the MediaExtractor class from the Android.Media library to 
    /// *try* to extract elemental streams from a Mpeg TS.
    /// </summary>
    public class BufferExtractor
    {
        public delegate void SampleReadyCallback(int count);

        private volatile int good, bad;

        //this interface didn't seem to help the stuttering either.
        //public ISampleReadyCallback Callback{get; set;}

        /// <summary>
        /// running count of # good PES samples found
        /// </summary>
        public int Good { get { return good; } }

        //
        public int Bad { get { return bad; } }

        //concurrent collections do seem to improve performance
        private ConcurrentStack<TsPacket> bufferPool = new ConcurrentStack<TsPacket>();
        private List<byte[]> largeBufferPool = new List<byte[]>();

        //concurrent collections do seem to improve performance
        protected ConcurrentQueue<PacketizedElementaryStream> outBuffers = new ConcurrentQueue<PacketizedElementaryStream>();

        public MpegTS.PacketizedElementaryStream pes;

        public BufferExtractor() : base() { }

        public int SampleCount
        {
            get {  {return outBuffers.Count; } }
        }

        /// <summary>
        /// this event is raised when the extractor has found a complete sample <para/>
        /// <see cref="PacketizedElementaryStream"/>(re-assembled PES).
        /// </summary>
        public event SampleReadyCallback SampleReady;

        protected async void OnSampleReady(int count, long pts)
        {
            var del = SampleReady;//get the CB delegate

            if (del != null)
                try
                {
                    //if (lastSampleRendered != null)
                    //    await lastSampleRendered.ConfigureAwait(false) ;

                    if (count > 0)
                    {
                        //var opt = TaskCreationOptions.PreferFairness;

                        //lastSampleRendered = new Task(() => del(count), opt);

                        //lastSampleRendered.Start();

                        del(count);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("error in SampleReady callback: "
                                                        +ex.StackTrace);
                }
        }

        /// <summary>
        /// Provides a byte buffer of the next sample in the internal FIFO (thread-safe)
        /// </summary>
        /// <returns>byte[].Lenth may =0 if called when internal queue is empty</returns>
        public VideoSample DequeueNextSample()
        {
            PacketizedElementaryStream pes = DequeueNextPacket();

            var sample = new VideoSample();
            byte[] buff = null;

            bool gotPayload = false;

            if (pes != null)
            {
                buff = GetLargeBuffer(pes.EstimateBufferSize());
                gotPayload = pes.GetPayload(buff);

                if (!gotPayload)
                {
                    ReturnLargeBuffer(buff);
                }

                if (pes.HasPts)
                    sample.PresentationTimeStamp = pes.PTS;

                //this is now handled in pes.Dispose() !
                //// reclaim buffers
                //if (usePool)
                //{
                //    lock (bufferPool)
                //    {
                //        var returnedBuffers = pes.GetBuffers();

                //        foreach (var buffer in returnedBuffers)
                //            bufferPool.Push(buffer);
                //    }
                //}
            }

            sample.Buffer = gotPayload ? buff : null;

            return sample;
        }

        public VideoSample DequeueNextSample(bool autoCreateBuffer = true)
        {
            if (autoCreateBuffer)
                return DequeueNextSample();
            else
                return DequeueNextSample(null);
        }

        /// <summary>
        /// Write the next sample to an output stream.<para/>
        /// the returned <see cref="VideoSample.Length"/> = # of bytes writen to outStream
        /// </summary>
        /// <param name="outStream"></param>
        /// <returns><see cref="VideoSample.Length"/> = # of bytes writen to outStream</returns>
        private VideoSample DequeueNextSample(System.IO.Stream outStream)
        {
            VideoSample sample = null;
            PacketizedElementaryStream pes = DequeueNextPacket();

            if (pes != null)
            {
                sample = new VideoSample();
                //long cursor = outStream.Position;

                //pes.WriteToStream(outStream);

                if (pes.HasPts)
                    sample.PresentationTimeStamp = pes.PTS;


                sample.Pes = pes;// (int)(outStream.Position - cursor);
            }

            return sample;
        }

        private byte[] GetLargeBuffer(int v)
        {
            lock (largeBufferPool)
            {
                var ret = largeBufferPool.FirstOrDefault(b => b.Length >= v);

                if (ret == null)
                {
                    ret = new byte[v];
                }
                else
                {
                    largeBufferPool.Remove(ret);
                }

                return ret;
            }
        }

        internal void ReturnLargeBuffer(byte[] buffer)
        {
            lock (largeBufferPool)
            {
                largeBufferPool.Add(buffer);
            }
        }

        private PacketizedElementaryStream DequeueNextPacket()
        {
            PacketizedElementaryStream pes = null;

            outBuffers.TryDequeue(out pes);

            return pes;
        }

        /// <summary>
        /// returns a pooled buffer of length 188 bytes
        /// </summary>
        /// <returns></returns>
        public TsPacket GetBuffer()
        {
            TsPacket ret = null;

            if (bufferPool.Count > 0)
                bufferPool.TryPop(out ret);

            if (ret == null)
                ret = new TsPacket(new byte[TsPacket.PacketLength]);

            return ret;
        }


        /// <summary>
        /// to push new raw data from any source, pass the data in here
        /// </summary>
        /// <param name="data"></param>
        public bool AddRaw(byte[] data)
        {
            if (data == null) return false;

            return AddPacket(new TsPacket(data));
        }

        public bool AddPacket(TsPacket ts)
        {
            //assume it's Mpeg TS for now...
            if (!ts.IsValid)
            {
                // reclaim buffer
                if (ts.data.Length == TsPacket.PacketLength) bufferPool.Push(ts);

                return false;//not valid TS packet!
            }

            return AddTsPacket(ts);
        }

        private void RecycleSmallBuffer(IEnumerable<TsPacket> data)
        {
            //TODO: could we check here for some max size of the bufferPool to not get too big?
            foreach (var b in data)
                bufferPool.Push(b);
        }

        internal void RecyclePES(PacketizedElementaryStream pes)
        {
            RecycleSmallBuffer(pes.packets);
            pes.packets.Clear();
        }

        private bool AddTsPacket(TsPacket ts)
        {
            if (ts.PID != PID.H264Video)
            {
                CheckCustomPIDs(ts);
                // reclaim buffer
                bufferPool.Push(ts);

                return true;//not video, so ignore it for now, it is a valid packet.
            }

            //if (pes == null && ts.IsPayloadUnitStart)
            //    pes = new MpegTS.PacketizedElementaryStream(ts);

            if (ts.IsPayloadUnitStart && pes != null)
            {
                //let's take care of the old (complete) one now: push out buffers
                //TODO: provide the time stamp/PES with this buffer, or, just provide the 
                //PES?
                if (pes.IsValid && pes.IsComplete)
                {
                    //lock (outBuffers)
                    {
                        outBuffers.Enqueue(pes);
                        //SampleCount = outBuffers.Count;
                    }

                    long pts = 0;
                    if (pes.HasPts) pts = pes.PTS;

                    OnSampleReady(SampleCount, pts);

                    ++good;
                }
                else
                    ++bad;

                pes = new MpegTS.PacketizedElementaryStream(this, ts);//we have the new pes

            }
            else if (pes != null)//we have already found the beginning of the stream and are building a pes
            {
                pes.Add(ts);
            }
            else//looking for a start packet
                pes = new PacketizedElementaryStream(this, ts);//           

            return true;
        }

        private void CheckCustomPIDs(TsPacket p)
        {
            //**TODO: provide a way for users to provide custom/private PIDs
            //so that the extractor can notify (event or callback) when it sees
            //one.

            //throw new NotImplementedException();
        }

        public Task AddRawAsync(byte[] data)
        {
            return Task.Run(() => AddRaw(data));
        }
    }
}
