﻿using GoodAI.Core;
using GoodAI.Core.Memory;
using GoodAI.Core.Nodes;
using GoodAI.Core.Task;
using GoodAI.Core.Utils;
using System;
using System.ComponentModel;
using YAXLib;

namespace GoodAI.Modules.NeuralNetwork
{
    /// <author>GoodAI</author>
    /// <meta>kk</meta>
    /// <status>Working</status>
    /// <summary>Replay buffer</summary>
    /// <description>Replay buffer for creating batches of random samples suitable for batch learning of neural networks.<br />
    ///     I/O:
    ///         <ul>
    ///             <li>Sample: Sample to be stored in replay buffer</li>
    ///             <li>Target: Target associated with the sample to be stored in replay buffer</li>
    ///             <li>SamplesBatch: Batch of samples randomly selected from replay buffer</li>
    ///             <li>TargetsBatch: Batch of targets associated with the samples batch</li>
    ///         </ul>
    /// 
    /// </description>
    [YAXSerializeAs("ReplayBuffer")]
    public class MyReplayBuffer : MyWorkingNode
    {
        [MyBrowsable, Category("Replay buffer")]
        [YAXSerializableField(DefaultValue = 10000)]
        public int ReplayBufferSize { get; set; }

        [MyBrowsable, Category("Replay buffer")]
        [YAXSerializableField(DefaultValue = 1)]
        public int BatchSize { get; set; }

        [YAXSerializableField(DefaultValue = MyReplayBufferStorage.Host)]
        [MyBrowsable, Category("Replay buffer")]
        public MyReplayBufferStorage BufferStorage { get; set; }

        public enum MyReplayBufferStorage
        {
            Host,
            Device
        }

        [MyInputBlock(0)]
        public MyMemoryBlock<float> Sample { get { return GetInput(0); } }

        [MyInputBlock(1)]
        public MyMemoryBlock<float> Target { get { return GetInput(1); } }

        [MyOutputBlock(0)]
        public MyMemoryBlock<float> SamplesBatch
        {
            get { return GetOutput(0); }
            set { SetOutput(0, value); }
        }

        [MyOutputBlock(1)]
        public MyMemoryBlock<float> TargetsBatch
        {
            get { return GetOutput(1); }
            set { SetOutput(1, value); }
        }

        public MyMemoryBlock<float> SampleReplayBuffer { get; private set; }
        public MyMemoryBlock<float> TargetReplayBuffer { get; private set; }

        public MyCreateBatchTask CreateBatch { get; protected set; }

        /// <summary>Creates a batch containing random samples from replay buffer.<br /></summary>
        [Description("CreateBatch"), MyTaskInfo(OneShot = false)]
        public class MyCreateBatchTask : MyTask<MyReplayBuffer>
        {
            [MyBrowsable, Category("Randomization")]
            [YAXSerializableField(DefaultValue = 0)]
            public int RandomSeed { get; set; }

            private int m_samplesCount;
            private Random m_rand;

            public override void Init(int nGPU)
            {
                m_samplesCount = 0;

                if (RandomSeed == 0)
                    m_rand = new Random();
                else
                    m_rand = new Random(RandomSeed);
            }

            private void ReplayBufferCopy(MyMemoryBlock<float> source, MyMemoryBlock<float> destination, int sourceOffset, int destOffset, int count)
            {
                if (Owner.BufferStorage == MyReplayBufferStorage.Host)
                {
                    Buffer.BlockCopy(source.Host, sourceOffset * sizeof(float), destination.Host, destOffset * sizeof(float), count * sizeof(float));
                }
                else if (Owner.BufferStorage == MyReplayBufferStorage.Device)
                {
                    source.CopyToMemoryBlock(destination, sourceOffset, destOffset, count);
                }
            }

            public override void Execute()
            {
                int bufferIdx = m_samplesCount % Owner.ReplayBufferSize; // once the buffer is full, start rewriting old data from beginning

                if (Owner.BufferStorage == MyReplayBufferStorage.Host)
                {
                    Owner.Sample.SafeCopyToHost();
                    Owner.Target.SafeCopyToHost();
                }

                ReplayBufferCopy(Owner.Sample, Owner.SampleReplayBuffer, 0, bufferIdx * Owner.Sample.Count, Owner.Sample.Count);
                ReplayBufferCopy(Owner.Target, Owner.TargetReplayBuffer, 0, bufferIdx * Owner.Target.Count, Owner.Target.Count);
                
                m_samplesCount++;
                
                for (int batchIdx = 0; batchIdx < Owner.BatchSize; batchIdx++)
                {
                    int randomBufferIdx = m_rand.Next(0, Math.Min(m_samplesCount, Owner.ReplayBufferSize)); // select a random sample from already filled part of replay buffer

                    ReplayBufferCopy(Owner.SampleReplayBuffer, Owner.SamplesBatch, randomBufferIdx * Owner.Sample.Count, batchIdx * Owner.Sample.Count, Owner.Sample.Count);
                    ReplayBufferCopy(Owner.TargetReplayBuffer, Owner.TargetsBatch, randomBufferIdx * Owner.Target.Count, batchIdx * Owner.Target.Count, Owner.Target.Count);
                }

                if (Owner.BufferStorage == MyReplayBufferStorage.Host)
                {
                    Owner.SamplesBatch.SafeCopyToDevice();
                    Owner.TargetsBatch.SafeCopyToDevice();
                }
            }
        }

        public override void UpdateMemoryBlocks()
        {
            if (Sample != null && Target != null)
            {
                SampleReplayBuffer.Count = Sample.Count * ReplayBufferSize;
                TargetReplayBuffer.Count = Target.Count * ReplayBufferSize;

                SamplesBatch.Count = Sample.Count * BatchSize;
                TargetsBatch.Count = Target.Count * BatchSize;
            }
        }
    }
}
