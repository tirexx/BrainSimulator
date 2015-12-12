﻿using GoodAI.Core;
using GoodAI.Core.Nodes;
using GoodAI.Core.Task;
using GoodAI.Core.Utils;
using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.CudaBlas;
using GoodAI.Modules.Matrix;
using GoodAI.Modules.NeuralNetwork.Layers;
using System;
using System.ComponentModel;
using System.Collections;
using System.Linq;
using YAXLib;
using System.Reflection;
using System.Collections.Generic;
using GoodAI.Platform.Core.Logging;

namespace GoodAI.Modules.NeuralNetwork.Group
{
    /// <author>GoodAI</author>
    /// <meta>ph</meta>
    /// <status>Working</status>
    /// <summary>Initialises Neural Network Group and sets internally used properties.</summary>
    /// <description></description>
    [Description("InitGroup"), MyTaskInfo(OneShot = true)]
    public class MyInitNNGroupTask : MyTask<MyNeuralNetworkGroup>
    {
        // parameterless constructor
        public MyInitNNGroupTask() { }

        // Kernel initialization
        public override void Init(int nGPU) { }

        // Task execution
        public override void Execute()
        {
            // timeStep is -1, because it is incremented at beginning of new timestep
            Owner.TimeStep = -1;

            // disable GradientCheck by default - TODO: fix this somehow
            Owner.GradientCheck.Enabled = false;

            // sort children in topological order
            Owner.SortedChildren = Owner.Children.OrderBy(o => o.TopologicalOrder).ToList();

            // set next and previous layer
            MyAbstractLayer layer;
            MyAbstractLayer lastTopologicalLayer = null;
            foreach (MyNode child in Owner.SortedChildren)
            {
                if (child is MyAbstractLayer)
                {
                    layer = child as MyAbstractLayer;

                    if (lastTopologicalLayer != null)
                    {
                        lastTopologicalLayer.NextTopologicalLayer = layer;
                    }

                    layer.NextTopologicalLayer = null;
                    layer.PreviousTopologicalLayer = lastTopologicalLayer;
                    lastTopologicalLayer = layer;

                    // collect all next and all previous conneted layers
                    layer.PreviousConnectedLayers = new List<MyAbstractLayer>();
                    layer.NextConnectedLayers = new List<MyAbstractLayer>();
                    foreach (MyConnection inputConnection in child.InputConnections)
                    {
                        if (inputConnection != null && inputConnection.From is MyAbstractLayer)
                        {
                            MyAbstractLayer lastConnectedLayer = inputConnection.From as MyAbstractLayer;
                            layer.PreviousConnectedLayers.Add(lastConnectedLayer);
                            lastConnectedLayer.NextConnectedLayers.Add(layer);
                        }
                    }
                }
            }

            // set first and last layer
            layer = lastTopologicalLayer;
            Owner.FirstTopologicalLayer = layer;
            Owner.LastTopologicalLayer = layer;
            while (layer != null)
            {
                Owner.FirstTopologicalLayer = layer;
                layer = layer.PreviousTopologicalLayer;
            }

            // count total number of weights
            Owner.TotalWeights = 0;
            layer = Owner.FirstTopologicalLayer;
            while (layer != null)
            {
                if (layer is MyAbstractWeightLayer)
                    Owner.TotalWeights += (layer as MyAbstractWeightLayer).Weights.Count;
                layer = layer.NextTopologicalLayer;
            }
        }
    }

    [Description("IncrementTimeStep"), MyTaskInfo(OneShot = false)]
    public class MyIncrementTimeStepTask : MyTask<MyNeuralNetworkGroup>
    {
        public MyIncrementTimeStepTask() { }

        public override void Init(int nGPU) { }

        public override void Execute()
        {
            Owner.TimeStep++;
        }
    }

    [Description("DecrementTimeStep"), MyTaskInfo(OneShot = false)]
    public class MyDecrementTimeStepTask : MyTask<MyNeuralNetworkGroup>
    {
        public MyDecrementTimeStepTask() { }

        public override void Init(int nGPU) { }

        public override void Execute()
        {
            Owner.TimeStep--;
        }
    }

    [Description("RunTemporalBlocksMode"), MyTaskInfo(OneShot = false)]
    public class MyRunTemporalBlocksModeTask : MyTask<MyNeuralNetworkGroup>
    {
        public override void Init(int nGPU)
        {
        }

        public override void Execute()
        {
            foreach (MyNode child in Owner.SortedChildren)
            {
                if (child is MyAbstractLayer)
                {
                    foreach (PropertyInfo memBlockInfo in MyNodeInfo.Get(child.GetType()).OwnedMemoryBlocks)
                    {
                        Object memBlock = memBlockInfo.GetValue(child);
                        if (memBlock is MyTemporalMemoryBlock<float>)
                        {
                            (memBlock as MyTemporalMemoryBlock<float>).RunMode();
                        }
                    }
                }
            }
        }
    }

    public abstract class MyAbstractBackpropTask : MyTask<MyNeuralNetworkGroup>
    {
        public virtual void Execute(MyAbstractLayer layer)
        {
            Log.Error(this.GetType(), "No method provided to backpropagate MyAbstractLayer " + layer + " in " + Owner);
        }

        public virtual void Execute(MyAbstractWeightLayer layer)
        {
            Log.Error(this.GetType(), "No method provided to backpropagate MyAbstractWeightLayer " + layer + " in " + Owner);
        }

        public void ComputeWeightGradientSum(MyAbstractWeightLayer layer)
        {
            if (Owner.BatchSize == 1) // cuBLAS tends to be slower when BatchSize is 1, gradient is computed in update weights kernels
                return;

            // WeightGradient = Delta x Transpose(Input)
            MyCublasFactory.Instance.Gemm(Operation.NonTranspose, Operation.Transpose,
                layer.Neurons, layer.Input.Count / Owner.BatchSize, Owner.BatchSize, 1.0f,
                layer.Delta.GetDevice(layer), layer.Neurons,
                layer.Input.GetDevice(layer), layer.Input.Count / Owner.BatchSize,
                0.0f, layer.WeightGradient.GetDevice(layer), layer.Neurons
                );
            
            // BiasGradient = Delta x Transpose(BiasInput). BiasInput is vector of ones
            MyCublasFactory.Instance.Gemm(Operation.NonTranspose, Operation.Transpose,
                layer.Neurons, 1, Owner.BatchSize, 1.0f,
                layer.Delta.GetDevice(layer), layer.Neurons,
                layer.BiasInput.GetDevice(layer), 1,
                0.0f, layer.BiasGradient.GetDevice(layer), layer.Neurons
                );
        }
    }

    /// <author>GoodAI</author>
    /// <meta>ph</meta>
    /// <status>Working</status>
    /// <summary>Stochastic gradient descent is an online training algorithm, that updates each parameter in the direction of the gradient for the current training example.<br></br>
    /// The gradient used is the partial derivative of each parameter (weight or bias) with respect to the loss function.</summary>
    /// <description></description>
    [Description("StochasticGradientDescent"), MyTaskInfo(OneShot = false)]
    public class MySGDTask : MyAbstractBackpropTask
    {
        // properties
        [YAXSerializableField(DefaultValue = 0.25f)]
        [MyBrowsable, Category("\tHyperParameters")]
        public float TrainingRate { get; set; }

        [YAXSerializableField(DefaultValue = 0.0f)]
        [MyBrowsable, Category("\tHyperParameters")]
        public float Momentum { get; set; }

        //parameterless constructor
        public MySGDTask() { }

        // kernel
        private MyCudaKernel m_SGDupdateKernel, m_convSGDupdateKernel, m_partialSGDupdateKernel;
        public override void Init(int nGPU)
        {
            m_SGDupdateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Layer\UpdateWeightsKernels", "FullyConnectedSGDUpdateKernel");
            m_convSGDupdateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\ConvolutionKernel", "ConvolutionSGDUpdateWeightsKernel");
            m_partialSGDupdateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Layer\UpdateWeightsKernels", "PartialSGDUpdateKernel");
        }

        //Task execution - should be called with a parameter
        public override void Execute()
        {
            //Log.Error(this.GetType(), "Please Execute MySGDTask with a layer parameter in " + Owner);
        }

        public override void Execute(MyAbstractWeightLayer layer)
        {
            if (layer.Connection == ConnectionType.FULLY_CONNECTED)
            {
                ComputeWeightGradientSum(layer);
                
                m_SGDupdateKernel.SetupExecution(layer.Weights.Count);
                m_SGDupdateKernel.Run(
                    layer.Input,
                    layer.Delta,
                    layer.WeightGradient,
                    layer.BiasGradient,
                    layer.Weights,
                    layer.PreviousWeightDelta,
                    layer.Bias,
                    layer.PreviousBiasDelta,
                    Owner.SGD.TrainingRate,
                    Owner.SGD.Momentum,
                    Owner.L1,
                    Owner.L2,
                    layer.DropoutMask,
                    layer.Neurons,
                    Owner.BatchSize,
                    layer.Weights.Count
                    );
            }
            else if (layer.Connection == ConnectionType.GAUSSIAN)
            {
                // Gaussian hidden layer just propagates delta, no weight updates
            }
            else if (layer.Connection == ConnectionType.PARTIAL_UPDATE && layer is IPartialUpdateLayer)
            {
                // Update some but not all of the weights
                IPartialUpdateLayer partialUpdateLayer = layer as IPartialUpdateLayer;

                m_partialSGDupdateKernel.SetupExecution(layer.Weights.Count);
                m_partialSGDupdateKernel.Run(
                    layer.Input,
                    layer.Delta,
                    layer.Weights,
                    layer.PreviousWeightDelta,
                    layer.Bias,
                    layer.PreviousBiasDelta,
                    Owner.SGD.TrainingRate,
                    Owner.SGD.Momentum,
                    Owner.L1,
                    Owner.L2,
                    layer.DropoutMask,
                    layer.Neurons,
                    layer.Weights.Count,
                    partialUpdateLayer.SuppressUpdatesAt(),
                    partialUpdateLayer.SuppressUpdatesCount()
                );
            }
            else if (layer.Connection == ConnectionType.CONVOLUTION && layer is MyConvolutionLayer)
            {
                MyConvolutionLayer convLayer = (MyConvolutionLayer) layer;
                m_convSGDupdateKernel.SetupExecution(convLayer.Weights.Count);
                m_convSGDupdateKernel.Run(
                    Owner.SGD.TrainingRate, Owner.SGD.Momentum,
                    convLayer.Weights,
                    convLayer.Bias, convLayer.PreviousBiasDelta,
                    convLayer.Delta, convLayer.PreviousWeightDelta,
                    convLayer.PaddedImage,
                    convLayer.InputWidth + convLayer.ZeroPadding + convLayer.ZeroPadding,
                    (convLayer.InputWidth + convLayer.ZeroPadding + convLayer.ZeroPadding)*
                    (convLayer.InputHeight + convLayer.ZeroPadding + convLayer.ZeroPadding),
                    convLayer.FilterWidth,
                    convLayer.FilterWidth*convLayer.FilterHeight,
                    convLayer.FilterWidth*convLayer.FilterHeight*convLayer.InputDepth,
                    convLayer.OutputWidth, convLayer.OutputHeight, convLayer.OutputWidth*convLayer.OutputHeight,
                    convLayer.HorizontalStride, convLayer.VerticalStride,
                    convLayer.L1Term, convLayer.L2Term,
                    Owner.BatchSize,
                    convLayer.Weights.Count
                    // should be equal to FilterWidth * FilterHeight * FilterCount * InputDepth
                    );
            }
            else
            {
                Log.Error(this.GetType(), "No method provided to SGD propagate a " + layer.Connection +
                                        " connected MyAbstractWeightLayer in " + Owner);
            }
        }
    }

    /// <author>GoodAI</author>
    /// <meta>ph</meta>
    /// <status>Working</status>
    /// <summary>RMSProp is the online adaptation of the Resilient Backpropagation algorithm based on the mean squares of the parameters.<br></br>
    /// It solves the problem of saturated neurons and vanishing gradients, which can occur with other backpropagation methods.<br></br>
    /// The mean squares are moving averages based on the smoothing factor, so as to emulate batch learning.</summary>
    /// <description></description>
    [Description("RMSProp"), MyTaskInfo(OneShot = false)]
    public class MyRMSTask : MyAbstractBackpropTask
    {
        // properties
        [YAXSerializableField(DefaultValue = 0.0025f)]
        [MyBrowsable, Category("\tHyperParameters")]
        public float TrainingRate { get; set; }

        [YAXSerializableField(DefaultValue = 0.0f)]
        [MyBrowsable, Category("\tHyperParameters")]
        public float Momentum { get; set; }

        [YAXSerializableField(DefaultValue = 0.9f)]
        [MyBrowsable, Category("\tHyperParameters")]
        public float SmoothingFactor { get; set; }

        //parameterless constructor
        public MyRMSTask() { }

        //Kernel initialization
        private MyCudaKernel m_RMSPropUpdateKernel, m_convRMSPropUpdateKernel;
        public override void Init(int nGPU)
        {
            m_RMSPropUpdateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Layer\UpdateWeightsKernels", "FullyConnectedRMSPropUpdateKernel");
            m_convRMSPropUpdateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\ConvolutionKernel", "ConvolutionRMSPropUpdateWeightsKernel");
        }

        //Task execution
        public override void Execute()
        {
            //Log.Error(this.GetType(), "Please Execute MyRMSTask with a layer parameter in " + Owner);
        }

        public override void Execute(MyAbstractWeightLayer layer)
        {
            if (layer.Connection == ConnectionType.FULLY_CONNECTED)
            {
                ComputeWeightGradientSum(layer);

                m_RMSPropUpdateKernel.SetupExecution(layer.Weights.Count);
                m_RMSPropUpdateKernel.Run(
                    layer.Input,
                    layer.Delta,
                    layer.WeightGradient,
                    layer.BiasGradient,
                    layer.Weights,
                    layer.PreviousWeightDelta,
                    layer.Bias,
                    layer.PreviousBiasDelta,
                    Owner.RMS.TrainingRate,
                    Owner.RMS.Momentum,
                    Owner.L1,
                    Owner.L2,
                    layer.DropoutMask,
                    layer.Neurons,
                    Owner.BatchSize,
                    layer.Weights.Count,
                    layer.MeanSquareWeight,
                    layer.MeanSquareBias,
                    Owner.RMS.SmoothingFactor
                    );
            }
            else if (layer.Connection == ConnectionType.GAUSSIAN)
            {
                // Gaussian hidden layer just propagates delta, no weight updates
            }
            else if (layer.Connection == ConnectionType.CONVOLUTION && layer is MyConvolutionLayer)
            {
                MyConvolutionLayer convLayer = (MyConvolutionLayer)layer;
                m_convRMSPropUpdateKernel.SetupExecution(convLayer.Weights.Count);
                m_convRMSPropUpdateKernel.Run(
                    Owner.RMS.TrainingRate, Owner.RMS.Momentum,
                    convLayer.Weights,
                    convLayer.Bias, convLayer.PreviousBiasDelta,
                    convLayer.Delta, convLayer.PreviousWeightDelta,
                    convLayer.PaddedImage,
                    convLayer.InputWidth + convLayer.ZeroPadding + convLayer.ZeroPadding,
                    (convLayer.InputWidth + convLayer.ZeroPadding + convLayer.ZeroPadding) *
                    (convLayer.InputHeight + convLayer.ZeroPadding + convLayer.ZeroPadding),
                    convLayer.FilterWidth,
                    convLayer.FilterWidth * convLayer.FilterHeight,
                    convLayer.FilterWidth * convLayer.FilterHeight * convLayer.InputDepth,
                    convLayer.OutputWidth, convLayer.OutputHeight, convLayer.OutputWidth * convLayer.OutputHeight,
                    convLayer.HorizontalStride, convLayer.VerticalStride,
                    convLayer.L1Term, convLayer.L2Term,
                    convLayer.MeanSquareWeight, convLayer.MeanSquareBias, Owner.RMS.SmoothingFactor,
                    Owner.BatchSize,
                    convLayer.Weights.Count
                    // should be equal to FilterWidth * FilterHeight * FilterCount * InputDepth
                    );
            }
            else
            {
                Log.Error(this.GetType(), "No method provided to RMS propagate a " + layer.Connection +
                                      " connected MyAbstractWeightLayer in " + Owner);
            }
        }
    }

    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>Working</status>
    /// <summary>
    ///     Adadelta is an adaptive learning method that changes each weight (parameter) separately and automatically over time.<br></br>
    ///     No manual settings are needed and it is recommended to use the default values which should behave well in all cases.
    /// </summary>
    [Description("Adadelta"), MyTaskInfo(OneShot = false)]
    public class MyAdadeltaTask : MyAbstractBackpropTask
    {
        // properties
        [YAXSerializableField(DefaultValue = 0.000001f)]
        [MyBrowsable, Category("\tHyperParameters")]
        public float Epsilon { get; set; }

        [YAXSerializableField(DefaultValue = 0.95f)]
        [MyBrowsable, Category("\tHyperParameters")]
        public float Ro { get; set; }


        //Kernel initialization
        private MyCudaKernel m_adadeltaUpdateKernel, m_convAdadeltaUpdateKernel;
        public override void Init(int nGPU)
        {
            m_adadeltaUpdateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Layer\UpdateWeightsKernels", "FullyConnectedAdadeltaUpdateKernel");
            m_convAdadeltaUpdateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\Convolution\ConvolutionKernel", "ConvolutionAdadeltaUpdateWeightsKernel");
        }

        //Task execution
        public override void Execute()
        {
            //Log.Error(this.GetType(), "Please Execute Adadelta with a layer parameter in " + Owner);
        }

        public override void Execute(MyAbstractWeightLayer layer)
        {
            if (layer.Connection == ConnectionType.FULLY_CONNECTED)
            {
                ComputeWeightGradientSum(layer);

                m_adadeltaUpdateKernel.SetupExecution(layer.Weights.Count);
                m_adadeltaUpdateKernel.Run(
                    layer.Input,
                    layer.Delta,
                    layer.WeightGradient,
                    layer.BiasGradient,
                    layer.Weights,
                    layer.Bias,
                    Owner.L1,
                    Owner.L2,
                    layer.DropoutMask,
                    layer.Neurons,
                    Owner.BatchSize,
                    layer.Weights.Count,
                    layer.MeanSquareWeight, layer.PreviousWeightDelta, layer.MeanSquareBias, layer.PreviousBiasDelta,
                    Owner.Adadelta.Ro, Owner.Adadelta.Epsilon
                    );
            }
            else if (layer.Connection == ConnectionType.GAUSSIAN)
            {
                // Gaussian hidden layer just propagates delta, no weight updates
            }
            else if (layer.Connection == ConnectionType.CONVOLUTION && layer is MyConvolutionLayer)
            {
                MyConvolutionLayer convLayer = (MyConvolutionLayer)layer;
                m_convAdadeltaUpdateKernel.SetupExecution(convLayer.Weights.Count);
                m_convAdadeltaUpdateKernel.Run(
                    convLayer.Weights,
                    convLayer.Bias,
                    convLayer.Delta,
                    convLayer.PaddedImage,
                    convLayer.InputWidth + convLayer.ZeroPadding + convLayer.ZeroPadding,
                    (convLayer.InputWidth + convLayer.ZeroPadding + convLayer.ZeroPadding) *
                    (convLayer.InputHeight + convLayer.ZeroPadding + convLayer.ZeroPadding),
                    convLayer.FilterWidth,
                    convLayer.FilterWidth * convLayer.FilterHeight,
                    convLayer.FilterWidth * convLayer.FilterHeight * convLayer.InputDepth,
                    convLayer.OutputWidth, convLayer.OutputHeight, convLayer.OutputWidth * convLayer.OutputHeight,
                    convLayer.HorizontalStride, convLayer.VerticalStride,
                    convLayer.L1Term, convLayer.L2Term,
                    convLayer.MeanSquareWeight, convLayer.PreviousWeightDelta, convLayer.MeanSquareBias,
                    convLayer.PreviousBiasDelta,
                    Owner.Adadelta.Ro, Owner.Adadelta.Epsilon,
                    Owner.BatchSize,
                    convLayer.Weights.Count
                    // should be equal to FilterWidth * FilterHeight * FilterCount * InputDepth
                    );
            }
            else
            {
                Log.Error(this.GetType(), "No method provided to Adadelta propagate a " + layer.Connection +
                                      " connected MyAbstractWeightLayer in " + Owner);
            }
        }
    }

    //[Description("vSGD-fd"), MyTaskInfo(OneShot = false)]
    //public class MyvSGDfdTask : MyAbstractBackpropTask
    //{
    //    //parameterless constructor
    //    public MyvSGDfdTask() { }

    //    //Kernel initialization
    //    private MyCudaKernel m_ComputeGradientsKernel;
    //    private MyCudaKernel m_ShiftParametersKernel;
    //    private MyCudaKernel m_FDCurvatureKernel;
    //    private MyCudaKernel m_AdjustMemoryKernel;
    //    private MyCudaKernel m_UpdateMovingAveragesKernel;
    //    private MyCudaKernel m_EstimateLearningRateKernel;
    //    private MyCudaKernel m_UpdateMemoryKernel;
    //    private MyCudaKernel m_UpdateParametersKernel;
    //    public override void Init(int nGPU)
    //    {
    //        m_ComputeGradientsKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\GradientsKernels", "FullyConnectedGradientsKernel");
    //        m_ShiftParametersKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\ShiftKernels", "FullyConnectedShiftKernel");
    //        m_FDCurvatureKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\FDCurvatureKernels", "FullyConnectedCurvatureKernel");
    //        m_AdjustMemoryKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\AdjustMemoryKernels", "FullyConnectedAdjustMemoryKernel");
    //        m_UpdateMovingAveragesKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\UpdateMovingAveragesKernels", "FullyConnectedUpdateMovingAveragesKernel");
    //        m_EstimateLearningRateKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\EstimateLearningRateKernels", "FullyConnectedEstimateLearningRateKernel");
    //        m_UpdateMemoryKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\AdjustMemoryKernels", "FullyConnectedUpdateMemoryKernel");
    //        m_UpdateParametersKernel = MyKernelFactory.Instance.Kernel(nGPU, @"NeuralNetwork\vSGD\UpdateParametersKernels", "FullyConnectedUpdateParametersKernel");
    //    }

    //    //Task execution
    //    public override void Execute()
    //    {
    //        Log.Error(this.GetType(), "Please Execute MyvSGDfdTask with a layer parameter in " + Owner);
    //    }

    //    public override void Execute(MyAbstractWeightLayer layer)
    //    {
    //        if (layer.Connection == ConnectionType.FULLY_CONNECTED)
    //        {
    //            // compute gradients
    //            GetGradients(layer);

    //            // save gradients
    //            layer.OriginalWeightsGrad.CopyFromMemoryBlock(layer.WeightsGrad, 0, 0, layer.Weights.Count);
    //            layer.OriginalBiasGrad.CopyFromMemoryBlock(layer.BiasGrad, 0, 0, layer.Bias.Count);

    //            // save parameters
    //            layer.OriginalWeights.CopyFromMemoryBlock(layer.Weights, 0, 0, layer.Weights.Count);
    //            layer.OriginalBias.CopyFromMemoryBlock(layer.Bias, 0, 0, layer.Bias.Count);

    //            // save deltas
    //            layer.OriginalDelta.CopyFromMemoryBlock(layer.Delta, 0, 0, layer.Delta.Count);

    //            // shift parameters
    //            m_ShiftParametersKernel.SetupExecution(layer.Neurons);
    //            m_ShiftParametersKernel.Run(
    //                layer.Weights, // these are the shifted weights
    //                layer.Bias, // these are the shifted biases
    //                layer.OriginalWeights, // these are original weights
    //                layer.OriginalBias, // these are original biases
    //                layer.AvgWeightGrad,
    //                layer.AvgBiasGrad,
    //                layer.DropoutMask,
    //                layer.Input.Count,
    //                layer.Neurons
    //                );

    //            // let the first layer do the next forward and backward pass
    //            if (layer.Id == Owner.FirstLayer.Id)
    //            {
    //                // shifted forward pass                    
    //                Owner.FeedForward();

    //                // finite differente curvature
    //                vSGD_fdAlgorithm();                    
    //            }
    //        }
    //        else
    //        {
    //            Log.Error(this.GetType(), "No method provided to vSGD-fd propagate a " + layer.Connection + " connected MyAbstractWeightLayer in " + Owner);
    //        }
    //    }

    //    // vSGD_fdAlgorithm() is the main implementation of the vSGD-fd algorithm:
    //    // finds deltas
    //    // finds gradients for weights and biases
    //    // computes finite difference curvature
    //    // adjusts memory size for outliers
    //    // update moving averages
    //    private void vSGD_fdAlgorithm()
    //    {
    //        // this sets the output layer delta
    //        Owner.GetError();

    //        MyAbstractLayer layer = Owner.LastLayer;
    //        while (layer != null)
    //        {
    //            // send deltas to previous layer
    //            layer.DeltaBackTask.Execute();
    //            if (layer is MyAbstractWeightLayer)
    //            {
    //                MyAbstractWeightLayer weightLayer = layer as MyAbstractWeightLayer;

    //                // get gradients for this layer
    //                GetGradients(weightLayer);

    //                // get finite difference curvature for this layer
    //                m_FDCurvatureKernel.SetupExecution(weightLayer.Neurons);
    //                m_FDCurvatureKernel.Run(
    //                    weightLayer.OriginalWeightsGrad, // these are original weights gradients
    //                    weightLayer.OriginalBiasGrad, // these are original bias gradients
    //                    weightLayer.WeightsGrad, // these are shifted weight gradients
    //                    weightLayer.BiasGrad, // these are shifted bias gradients
    //                    weightLayer.AvgWeightGrad,
    //                    weightLayer.AvgBiasGrad,
    //                    weightLayer.WeightGradCurve,
    //                    weightLayer.BiasGradCurve,
    //                    weightLayer.DropoutMask,
    //                    weightLayer.Input.Count,
    //                    weightLayer.Neurons
    //                    );

    //                // adjust memory size for outliers
    //                m_AdjustMemoryKernel.SetupExecution(weightLayer.Neurons);
    //                m_AdjustMemoryKernel.Run(
    //                    weightLayer.OriginalWeightsGrad, // these are original weights gradients
    //                    weightLayer.OriginalBiasGrad, // these are original bias gradients
    //                    weightLayer.WeightGradCurve,
    //                    weightLayer.BiasGradCurve,
    //                    weightLayer.AvgWeightGrad,
    //                    weightLayer.AvgBiasGrad,
    //                    weightLayer.AvgWeightGradVar,
    //                    weightLayer.AvgBiasGradVar,
    //                    weightLayer.AvgWeightGradCurve,
    //                    weightLayer.AvgBiasGradCurve,
    //                    weightLayer.AvgWeightGradCurveVar,
    //                    weightLayer.AvgBiasGradCurveVar,
    //                    weightLayer.WeightMemorySize,
    //                    weightLayer.BiasMemorySize,
    //                    weightLayer.DropoutMask,
    //                    weightLayer.Input.Count,
    //                    weightLayer.Neurons
    //                    );

    //                // update moving averages
    //                m_UpdateMovingAveragesKernel.SetupExecution(weightLayer.Neurons);
    //                m_UpdateMovingAveragesKernel.Run(
    //                    weightLayer.OriginalWeightsGrad, // these are original weights gradients
    //                    weightLayer.OriginalBiasGrad, // these are original bias gradients
    //                    weightLayer.WeightGradCurve,
    //                    weightLayer.BiasGradCurve,
    //                    weightLayer.AvgWeightGrad,
    //                    weightLayer.AvgBiasGrad,
    //                    weightLayer.AvgWeightGradVar,
    //                    weightLayer.AvgBiasGradVar,
    //                    weightLayer.AvgWeightGradCurve,
    //                    weightLayer.AvgBiasGradCurve,
    //                    weightLayer.AvgWeightGradCurveVar,
    //                    weightLayer.AvgBiasGradCurveVar,
    //                    weightLayer.WeightMemorySize,
    //                    weightLayer.BiasMemorySize,
    //                    weightLayer.DropoutMask,
    //                    weightLayer.Input.Count,
    //                    weightLayer.Neurons
    //                    );

    //                // estimate learning rate
    //                m_EstimateLearningRateKernel.SetupExecution(weightLayer.Neurons);
    //                m_EstimateLearningRateKernel.Run(
    //                    weightLayer.WeightLearningRate,
    //                    weightLayer.BiasLearningRate,
    //                    weightLayer.AvgWeightGrad,
    //                    weightLayer.AvgBiasGrad,
    //                    weightLayer.AvgWeightGradVar,
    //                    weightLayer.AvgBiasGradVar,
    //                    weightLayer.AvgWeightGradCurve,
    //                    weightLayer.AvgBiasGradCurve,
    //                    weightLayer.AvgWeightGradCurveVar,
    //                    weightLayer.AvgBiasGradCurveVar,
    //                    weightLayer.DropoutMask,
    //                    weightLayer.Input.Count,
    //                    weightLayer.Neurons
    //                    );

    //                // update memory size
    //                m_UpdateMemoryKernel.SetupExecution(weightLayer.Neurons);
    //                m_UpdateMemoryKernel.Run(
    //                    weightLayer.AvgWeightGrad,
    //                    weightLayer.AvgBiasGrad,
    //                    weightLayer.AvgWeightGradVar,
    //                    weightLayer.AvgBiasGradVar,
    //                    weightLayer.WeightMemorySize,
    //                    weightLayer.BiasMemorySize,
    //                    weightLayer.DropoutMask,
    //                    weightLayer.Input.Count,
    //                    weightLayer.Neurons
    //                    );

    //                // restore gradients
    //                weightLayer.WeightsGrad.CopyFromMemoryBlock(weightLayer.OriginalWeightsGrad, 0, 0, weightLayer.Weights.Count);
    //                weightLayer.BiasGrad.CopyFromMemoryBlock(weightLayer.OriginalBiasGrad, 0, 0, weightLayer.Bias.Count);

    //                // restore parameters
    //                weightLayer.Weights.CopyFromMemoryBlock(weightLayer.OriginalWeights, 0, 0, weightLayer.Weights.Count);
    //                weightLayer.Bias.CopyFromMemoryBlock(weightLayer.OriginalBias, 0, 0, weightLayer.Bias.Count);

    //                // restore deltas
    //                weightLayer.Delta.CopyFromMemoryBlock(weightLayer.OriginalDelta, 0, 0, weightLayer.Delta.Count);

    //                // update parameters
    //                if (SimulationStep > 1000)
    //                {
    //                    m_UpdateParametersKernel.SetupExecution(weightLayer.Neurons);
    //                    m_UpdateMemoryKernel.Run(
    //                        weightLayer.Weights,
    //                        weightLayer.Bias,
    //                        weightLayer.WeightLearningRate,
    //                        weightLayer.BiasLearningRate,
    //                        weightLayer.WeightsGrad,
    //                        weightLayer.BiasGrad,
    //                        weightLayer.DropoutMask,
    //                        weightLayer.Input.Count,
    //                        weightLayer.Neurons
    //                        );
    //                }
    //            }
    //            layer = layer.PreviousLayer;
    //        }
    //    }

    //    private void GetGradients(MyAbstractWeightLayer layer)
    //    {
    //        m_ComputeGradientsKernel.SetupExecution(layer.Neurons);
    //        m_ComputeGradientsKernel.Run(
    //            layer.Input,
    //            layer.Delta,
    //            layer.Weights,
    //            Owner.L1,
    //            Owner.L2,
    //            layer.DropoutMask,
    //            layer.WeightsGrad,
    //            layer.BiasGrad,
    //            layer.Input.Count,
    //            layer.Neurons
    //            );
    //    }
    //}

    /// <author>GoodAI</author>
    /// <meta>ph</meta>
    /// <status>Working</status>
    /// <summary>Gradient checking mainly for developers to make sure the calculated gradients are correct.
    /// <br></br>
    /// This should generally be disabled during training, since it will negatively affect performance</summary>
    /// <description></description>
    [Description("GradientCheck"), MyTaskInfo(OneShot = false)]
    public class MyGradientCheckTask : MyTask<MyNeuralNetworkGroup>
    {
        //Properties
        [YAXSerializableField(DefaultValue = 0.1f)]
        [MyBrowsable, Category("\tParams")]
        public float RelativeStepSize { get; set; }

        [YAXSerializableField(DefaultValue = 1)]
        [MyBrowsable, Category("\tParams")]
        public int SamplesPerTimestep { get; set; }

        [YAXSerializableField(DefaultValue = 0.001f)]
        [MyBrowsable, Category("\tParams")]
        public float ThresholdRelative { get; set; }

        [YAXSerializableField(DefaultValue = 0.0001f)]
        [MyBrowsable, Category("\tParams")]
        public float ThresholdAbsolute { get; set; }

        private Random Rand = new Random();

        public MyGradientCheckTask() { } //parameterless constructor

        public override void Init(int nGPU) { } //Kernel initialization

        public override void Execute()
        {
            float maxRelDiff = 0.0f;
            float maxAbsDiff = 0.0f;
            int maxDiffLayer = 0;
            int maxDiffWeight = 0;
            float maxDiffWeightValue = 0.0f;
            float maxDiffStepSize = 0.0f;
            float maxDiffAnalyticalGrad = 0.0f;
            float maxDiffNumericalGrad = 0.0f;

            float sampleProbability = 1.0f / Owner.TotalWeights;
            for (int s = 0; s < SamplesPerTimestep; s++)
            {
                // dice roll
                float diceRoll = (float)Rand.NextDouble();

                // convert diceroll to parameter to sample
                int w = (int)Math.Floor(diceRoll / sampleProbability);
                if (w >= Owner.TotalWeights)
                {
                    if (w > Owner.TotalWeights)
                        Log.Error(this.GetType(), "w > Owner.TotalWeights"); // just for testing, this should never hit
                    w = Owner.TotalWeights - 1; // this is just to make if safe, but it should never hit
                }

                // loop through the layers
                MyAbstractLayer layer = Owner.FirstTopologicalLayer;
                while (layer != null)
                {
                    // check for weights
                    if (layer is MyAbstractWeightLayer)
                    {
                        MyAbstractWeightLayer weightLayer = (layer as MyAbstractWeightLayer);
                        if (weightLayer.Weights.Count <= w)
                            w -= weightLayer.Weights.Count;
                        else
                        {
                            weightLayer.Weights.SafeCopyToHost(w, 1); // copy this weight to host
                            float originalWeight = weightLayer.Weights.Host[w]; // save weight
                            float stepSize = Math.Abs(originalWeight) * RelativeStepSize; // set stepSize

                            // get errorPlus
                            weightLayer.Weights.Host[w] = originalWeight + stepSize; // increase weight
                            weightLayer.Weights.SafeCopyToDevice(w, 1); // back to device
                            Owner.FeedForward(); // forward the network
                            float errorPlus = Owner.GetError();

                            // get errorMinus
                            weightLayer.Weights.Host[w] = originalWeight - stepSize; // decrease weight
                            weightLayer.Weights.SafeCopyToDevice(w, 1); // back to device
                            Owner.FeedForward(); // forward the network
                            float errorMinus = Owner.GetError();

                            // reset to original
                            weightLayer.Weights.Host[w] = originalWeight; // back to where we started
                            weightLayer.Weights.SafeCopyToDevice(w, 1); // back to device
                            Owner.FeedForward(); // forward the network
                            Owner.GetError(); // this sets the original error

                            // numerical gradient
                            float numericalGradient = (errorPlus - errorMinus) / (2 * stepSize);

                            if (numericalGradient == 0)
                            {
                                Log.Debug(this.GetType(), "t: " + SimulationStep + " id: " + weightLayer.Id + " w" + w + ": " + weightLayer.Weights.Host[w] + " step: " + stepSize + " numerical gradient is 0.");
                                break; // continue to next sample
                            }

                            // analytical gradient
                            int n = w % weightLayer.Neurons;
                            int i = (w - n) / weightLayer.Neurons;
                            weightLayer.Delta.SafeCopyToHost(n, 1); // copy delta to host
                            weightLayer.Input.SafeCopyToHost(i, 1); // copy input to host
                            weightLayer.DropoutMask.SafeCopyToHost(n, 1); // copy dropoutmask to host
                            //weightLayer.Weights.SafeCopyToHost(w, 1); // already present at host due to resetting to original
                            if (weightLayer.DropoutMask.Host[n] > 0)
                                break;
                            float analyticalGradient = weightLayer.Delta.Host[n] * weightLayer.Input.Host[i] + Owner.L1 * (weightLayer.Weights.Host[w] < 0.0f ? -1.0f : 1.0f) + Owner.L2 * weightLayer.Weights.Host[w];
                            float relativeDiff = 0.0f;
                            float absoluteDiff = 0.0f;
                            if (analyticalGradient == 0)
                            {
                                Log.Debug(this.GetType(), "t: " + SimulationStep + " id: " + weightLayer.Id + " w" + w + ": " + weightLayer.Weights.Host[w] + " step: " + stepSize + " analytical gradient is 0.");
                                break; // continue to next sample
                            }
                            absoluteDiff = Math.Abs(numericalGradient - analyticalGradient);
                            relativeDiff = absoluteDiff / (Math.Abs(numericalGradient) + Math.Abs(analyticalGradient));
                            if (relativeDiff > maxRelDiff && absoluteDiff > ThresholdAbsolute)
                            {
                                maxAbsDiff = absoluteDiff;
                                maxRelDiff = relativeDiff;
                                maxDiffLayer = weightLayer.Id;
                                maxDiffWeight = w;
                                maxDiffWeightValue = weightLayer.Weights.Host[w];
                                maxDiffStepSize = stepSize;
                                maxDiffAnalyticalGrad = analyticalGradient;
                                maxDiffNumericalGrad = numericalGradient;
                            }
                            Log.Debug(this.GetType(), "t: " + SimulationStep + " id: " + weightLayer.Id + " w" + w + ": " + weightLayer.Weights.Host[w] + " step: " + stepSize + " AG: " + analyticalGradient + " NG: " + numericalGradient + " diff: " + relativeDiff);
                            break; // continue to next sample
                        }
                    }
                    layer = layer.NextTopologicalLayer;

                    // catch unmatched dice-rolls
                    if (layer == null)
                        Log.Error(this.GetType(), "GradientCheck task: Weight w " + w + " not found within " + Owner.TotalWeights + " total weights"); // just for testing, this should never hit
                }
            }
            // handle the relativeDiff we just found
            if (maxRelDiff > ThresholdRelative && maxRelDiff > ThresholdAbsolute)
            {
                Log.Info(this.GetType(), "Gradient threshold exceeded on SimulationStep: " + SimulationStep);
                Log.Info(this.GetType(), "Max analytical vs numerical relative gradient difference found in layer id " + maxDiffLayer + " for weight " + maxDiffWeight + ": " + maxDiffWeightValue + " with Step size: " + maxDiffStepSize);
                Log.Info(this.GetType(), "Analytical gradient: " + maxDiffAnalyticalGrad + " Numerical gradient: " + maxDiffNumericalGrad + " Relative difference: " + maxRelDiff);
                Log.Info(this.GetType(), string.Empty);
            }
        }
        //        // copy delta to host
        //        selectedLayer.Delta.SafeCopyToHost();

        //        // loop the neurons
        //        for (int n = 0; n < selectedLayer.Neurons; n++)
        //        {
        //            // nudge each neurons weightedInput with MiniStepSize forward
        //            selectedLayer.WeightedInput.SafeCopyToHost(n, 1); // copy to host
        //            selectedLayer.WeightedInput.Host[n] += MiniStepSize; // increment with MiniStep
        //            selectedLayer.WeightedInput.SafeCopyToDevice(n, 1); // back to device

        //            // find lossPlus
        //            FeedForward(selectedLayer);
        //            Owner.OutputLayer.CalcDelta.Execute(); // save loss contribution
        //            Owner.Loss.Host[0] = 0;
        //            Owner.OutputLayer.LossContribution.SafeCopyToHost();
        //            for (int i = 0; i < Owner.OutputLayer.Neurons; i++)
        //                Owner.Loss.Host[0] += Owner.OutputLayer.LossContribution.Host[i];
        //            Owner.Loss.SafeCopyToDevice();
        //            float lossPlus = Owner.Loss.Host[0];

        //            // nudge each neurons weightedInput with MiniStepSize backward
        //            selectedLayer.WeightedInput.Host[n] -= 2 * MiniStepSize; // decrement 2 MiniSteps
        //            selectedLayer.WeightedInput.SafeCopyToDevice(n, 1); // copy to device

        //            // find lossMinus
        //            FeedForward(selectedLayer);
        //            Owner.OutputLayer.CalcDelta.Execute(); // save loss contribution
        //            Owner.Loss.Host[0] = 0;
        //            Owner.OutputLayer.LossContribution.SafeCopyToHost();
        //            for (int i = 0; i < Owner.OutputLayer.Neurons; i++)
        //                Owner.Loss.Host[0] += Owner.OutputLayer.LossContribution.Host[i];
        //            Owner.Loss.SafeCopyToDevice();
        //            Owner.Loss.SafeCopyToHost();
        //            float lossMinus = Owner.Loss.Host[0];

        //            // calculate numerical gradient
        //            selectedLayer.NumericalDelta.Host[n] = (lossPlus - lossMinus) / (2 * MiniStepSize);
        //            selectedLayer.NumericalDelta.SafeCopyToDevice(n, 1); // copy to device

        //            if (selectedLayer.NumericalDelta.Host[n] == 0 || selectedLayer.Delta.Host[n] == 0)
        //                Owner.ZeroValuedDeltas.Host[0]++; // count neurons with zero delta on host
        //            else
        //            {
        //                float deltaDiff = Math.Abs(selectedLayer.Delta.Host[n] - selectedLayer.NumericalDelta.Host[n]) / (Math.Abs(selectedLayer.NumericalDelta.Host[n]) + Math.Abs(selectedLayer.Delta.Host[n]));// set max delta diff
        //                Owner.AvgRelativeDeltaDiff.Host[0] += deltaDiff;
        //                if (deltaDiff > Owner.MaxRelativeDeltaDiff.Host[0])
        //                    Owner.MaxRelativeDeltaDiff.Host[0] = deltaDiff;
        //                neurons++;
        //            }

        //            // reset input
        //            selectedLayer.WeightedInput.Host[n] += MiniStepSize; // increment with MiniStep
        //            selectedLayer.WeightedInput.SafeCopyToDevice(n, 1); // copy to device
        //        }

        //        // select next layer
        //        selectedLayer = selectedLayer.NextLayer as MyLayerfloat; // this could potentially break, if there is an abstract layer present (which there should not be)
        //    }

        //    // copy saturated neurons to device
        //    Owner.ZeroValuedDeltas.SafeCopyToDevice();

        //    // calc and copy avg delta diff to device
        //    Owner.AvgRelativeDeltaDiff.Host[0] /= neurons;
        //    Owner.AvgRelativeDeltaDiff.SafeCopyToDevice();

        //    Owner.MaxRelativeDeltaDiff.SafeCopyToDevice();

        //    // cast to float value
        //    Owner.AvgRelativeDeltaDiffFloat.Host[0] = (float)Owner.AvgRelativeDeltaDiff.Host[0];
        //    Owner.AvgRelativeDeltaDiffFloat.SafeCopyToDevice();

        //    Owner.MaxRelativeDeltaDiffFloat.Host[0] = (float)Owner.MaxRelativeDeltaDiff.Host[0];
        //    Owner.MaxRelativeDeltaDiffFloat.SafeCopyToDevice();
        //}
    }

}