﻿using GoodAI.Core;
using GoodAI.Core.Nodes;
using GoodAI.Core.Task;
using GoodAI.Core.Utils;
using GoodAI.Modules.NeuralNetwork.Group;
using GoodAI.Modules.NeuralNetwork.Layers;
using GoodAI.Modules.NeuralNetwork.Tasks;
using ManagedCuda.BasicTypes;
using System;
using System.ComponentModel;
using GoodAI.Platform.Core.Logging;

namespace CustomModels.NeuralNetwork.Tasks
{

    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>WIP</status>
    /// <summary>
    /// Performs MAX pooling forward pass. Chooses the max value from each receptive field and its each position (determined by FilterW/H and Stride parameters).
    /// </summary>
    /// <description></description>
    [Description("PoolingForward"), MyTaskInfo(OneShot = false)]
    public class MyPoolingForwardTask : MyAbstractForwardTask<MyPoolingLayer>
    {
        private MyCudaKernel m_kernel;

        public MyPoolingForwardTask() { } //parameterless constructor

        public override void Init(int nGPU) //Kernel initialization
        {
            m_kernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\PoolingKernel", "PoolingForwardKernel");
        }

        public override void Execute() //Task execution
        {
            m_kernel.SetupExecution(Owner.Neurons);
            m_kernel.Run(
                Owner.Input,
                Owner.Output,
                Owner.ActivatedNeurons,
                Owner.InputWidth, Owner.InputWidth * Owner.InputHeight,
                Owner.FilterWidth, Owner.FilterHeight,
                Owner.HorizontalStride, Owner.VerticalStride,
                Owner.OutputWidth, Owner.OutputWidth * Owner.OutputHeight,
                Owner.Neurons
            );
            Log.Debug(this.GetType(), "Pooling.");
        }
    }

    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>WIP</status>
    /// <summary>
    /// Propagates deltas back through the pooling layer.
    /// The chosen max value is saved in each forward pass and used in this backward pass to determine the neuron that will receive the delta.
    /// </summary>
    /// <description></description>
    [Description("PoolingBackward"), MyTaskInfo(OneShot = false)]
    public class MyPoolingBackwardTask : MyAbstractBackDeltaTask<MyPoolingLayer>
    {
        private MyCudaKernel m_kernel;

        public MyPoolingBackwardTask() { } //parameterless constructor

        public override void Init(int nGPU) //Kernel initialization
        {
            m_kernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\PoolingKernel", "PoolingBackwardKernel");
        }

        public override void Execute() //Task execution
        {
            Log.Debug(this.GetType(), "Pooling backward.");
            
            // pointer to previous layer
            MyNode node = Owner.Input.Owner;

            if (node is MyAbstractLayer)
            {
                MyAbstractLayer previousLayer = node as MyAbstractLayer;

                // reset delta
                previousLayer.Delta.Fill(0);

                // determine input to previous layer
                CUdeviceptr prevInputPtr = MyAbstractLayer.DetermineInput(previousLayer);

                m_kernel.SetupExecution(Owner.Neurons);
                m_kernel.Run(
                    (int)previousLayer.ActivationFunction,
                    Owner.Delta,
                    previousLayer.Delta,
                    prevInputPtr,
                    Owner.ActivatedNeurons,
                    Owner.Neurons
                );
            }

        }
    }

    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>WIP</status>
    /// <summary>
    /// Pads the input (image) with zeros to allow the result of convolution have the same dimension as the input.
    /// </summary>
    /// <description></description>
    [Description("PadImage"), MyTaskInfo(OneShot = false)]
    public class MyPadImageTask : MyTask<MyConvolutionLayer>
    {
        private MyCudaKernel m_kernel;

        public MyPadImageTask() { } //parameterless constructor

        public override void Init(int nGPU) //Kernel initialization
        {
            m_kernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\ConvolutionKernel", "PadImageKernel");
        }

        public override void Execute() //Task execution
        {
            if (Owner.ZeroPadding <= 0) return;

            if (Owner.Input == null)
            {
                Log.Error(this.GetType(), "MyPadImageTask error: Input to " + Owner + " is null.");
                return;
            }
            Owner.PaddedImage.Fill(0);

            m_kernel.SetupExecution(Owner.Input.Count);
            m_kernel.Run(
                Owner.Input,
                Owner.PaddedImage,
                Owner.InputWidth,
                Owner.ZeroPadding,
                Owner.InputWidth*Owner.InputHeight,
                (Owner.InputWidth + Owner.ZeroPadding + Owner.ZeroPadding) * (Owner.InputHeight + Owner.ZeroPadding + Owner.ZeroPadding),
                Owner.Input.Count
            );
        }
    }

    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>WIP</status>
    /// <summary>
    /// Standard forward pass of the convolution operation.
    /// </summary>
    /// <description></description>
    [Description("ConvolutionForward"), MyTaskInfo(OneShot = false)]
    public class MyConvolutionForwardTask : MyAbstractForwardTask<MyConvolutionLayer>
    {
        private MyCudaKernel m_kernel;

        public MyConvolutionForwardTask() { } //parameterless constructor

        public override void Init(int nGPU) //Kernel initialization
        {
            m_kernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\ConvolutionKernel", "ConvolutionForwardKernel");
        }

        public override void Execute() //Task execution
        {
            Log.Debug(this.GetType(), "Convolution forward.");
            m_kernel.SetupExecution(Owner.Output.Count);

            // use the input image as it is
            if (Owner.ZeroPadding <= 0)
                m_kernel.Run(
                    (int)Owner.ActivationFunction,
                    Owner.Input,
                    Owner.Weights,
                    Owner.Bias,
                    Owner.Output,
                    Owner.NeuronInput,
                    Owner.FilterWidth, Owner.FilterHeight,
                    Owner.InputDepth,
                    Owner.FilterWidth * Owner.FilterHeight,
                    Owner.FilterWidth * Owner.FilterHeight * Owner.InputDepth,
                    Owner.InputWidth * Owner.InputHeight,
                    Owner.InputWidth,
                    Owner.OutputWidth * Owner.OutputHeight,
                    1 + (Owner.InputWidth - Owner.FilterWidth) / Owner.HorizontalStride, //1 + (inputWidth - filterWidth) / horStride
                    Owner.HorizontalStride, Owner.VerticalStride,
                    Owner.Output.Count
                );
            // do and use zero padding
            else
                m_kernel.Run(
                    (int)Owner.ActivationFunction,
                    Owner.PaddedImage,
                    Owner.Weights,
                    Owner.Bias,
                    Owner.Output,
                    Owner.NeuronInput,
                    Owner.FilterWidth, Owner.FilterHeight, Owner.InputDepth,
                    Owner.FilterWidth * Owner.FilterHeight,
                    Owner.FilterWidth * Owner.FilterHeight * Owner.InputDepth,
                    (Owner.InputWidth + Owner.ZeroPadding + Owner.ZeroPadding) * (Owner.InputHeight + Owner.ZeroPadding + Owner.ZeroPadding),
                    (Owner.InputWidth + Owner.ZeroPadding + Owner.ZeroPadding),
                    Owner.OutputWidth * Owner.OutputHeight,
                    1 + ((Owner.InputWidth + Owner.ZeroPadding + Owner.ZeroPadding) - Owner.FilterWidth) / Owner.HorizontalStride, //1 + (inputWidth - filterWidth) / horStride
                    Owner.HorizontalStride, Owner.VerticalStride,
                    Owner.Output.Count
                );

        }
    }

    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>WIP</status>
    /// <summary>
    /// Computes deltas of the previous layer from deltas on this convolutional layer.
    /// </summary>
    /// <description></description>
    [Description("ConvolutionBackward"), MyTaskInfo(OneShot = false)]
    public class MyConvolutionBackwardTask : MyAbstractBackDeltaTask<MyConvolutionLayer>
    {
        private MyCudaKernel m_kernel;

        public override void Init(int nGPU)
        {
            m_kernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\ConvolutionKernel", "ConvolutionBackwardKernel");
        }

        public override void Execute()
        {
            Log.Debug(this.GetType(), "Convolution backward.");

            // pointer to previous layer
            MyNode node = Owner.Input.Owner;

            if (node is MyAbstractLayer)
            {
                MyAbstractLayer previousLayer = node as MyAbstractLayer;

                // reset delta
                previousLayer.Delta.Fill(0);

                // determine input to previous layer
                CUdeviceptr prevInputPtr = MyAbstractLayer.DetermineInput(previousLayer);

                m_kernel.SetupExecution(previousLayer.Neurons);
                m_kernel.Run(
                    (int)previousLayer.ActivationFunction,
                    Owner.Weights,
                    Owner.Delta,
                    previousLayer.Delta,
                    prevInputPtr,
                    Owner.FilterCount,
                    Owner.InputWidth * Owner.InputHeight, // input slice size without padding
                    (Owner.InputWidth + Owner.ZeroPadding + Owner.ZeroPadding) * (Owner.InputHeight + Owner.ZeroPadding + Owner.ZeroPadding), // input slice size
                    Owner.ZeroPadding,
                    Owner.InputWidth, Owner.InputHeight,
                    Owner.FilterWidth, Owner.FilterHeight,
                    Owner.FilterWidth * Owner.FilterHeight,
                    Owner.FilterWidth * Owner.FilterHeight * Owner.InputDepth, 
                    Owner.OutputWidth, Owner.OutputHeight, Owner.OutputWidth * Owner.OutputHeight,
                    Owner.HorizontalStride, Owner.VerticalStride,
                    previousLayer.Neurons
                );
            }
        }
    }

    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>WIP</status>
    /// <summary>
    /// Randomly initialises weights and biases of the convolution layer.
    /// Uses normal distribution with standard deviation of 1 / (sqrt(input.Count))
    /// </summary>
    /// <description></description>
    [Description("InitLayer"), MyTaskInfo(OneShot = true)]
    public class MyConvolutionInitLayerTask : MyTask<MyConvolutionLayer>
    {
        public MyConvolutionInitLayerTask() { } //parameterless constructor
        public override void Init(int nGPU) { } //Kernel initialization

        public override void Execute() //Task execution
        {
            // init vars to 0
            Owner.PreviousBiasDelta.Fill(0f);
            Owner.PreviousWeightDelta.Fill(0f);

            float stdDev = 0.01f;

            // init random weights
            if (Owner.Input != null && Owner.Input.Count > 0)
                stdDev = 1.0f / (float)Math.Sqrt(Owner.Input.Count + 1);
                
            MyKernelFactory.Instance.GetRandDevice(Owner).GenerateNormal(Owner.Weights.GetDevice(Owner), 0, stdDev);
            MyKernelFactory.Instance.GetRandDevice(Owner).GenerateNormal(Owner.Bias.GetDevice(Owner), 0, stdDev);
        }
    }


    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>WIP</status>
    /// <summary>
    /// Updates the weights (filters) of this convolutional layer. The exact algorithm is determined by the parent network's settings.
    /// </summary>
    /// <description></description>
    [Description("UpdateWeights"), MyTaskInfo(OneShot = false)]
    public class MyConvolutionUpdateWeights : MyAbstractUpdateWeightsTask<MyConvolutionLayer>
    {
        public override void Init(int nGPU) { }

        public override void Execute() //Task execution
        {
            // get enabled loss function
            MyTask task = Owner.ParentNetwork.GetEnabledTask("BackPropagation");
            MyAbstractBackpropTask backpropTask = null;
            if (task is MyAbstractBackpropTask)
                backpropTask = task as MyAbstractBackpropTask;
            else
                Log.Error(this.GetType(), "Backprop task does not derive from MyAbstractBackpropTask in " + Owner.ParentNetwork);

            if (backpropTask == null)
                Log.Error(this.GetType(), "Undetermined backprop task in " + Owner.ParentNetwork);
            else
            {
                backpropTask.Execute(Owner); // call the group task to do the backpropagation
            }
        }
    }
}
