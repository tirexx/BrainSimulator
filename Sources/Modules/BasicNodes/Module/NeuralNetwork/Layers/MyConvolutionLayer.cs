﻿using System;
using CustomModels.NeuralNetwork.Tasks;
using GoodAI.Core.Memory;
using GoodAI.Core.Nodes;
using GoodAI.Core.Utils;
using System.ComponentModel;
using GoodAI.Platform.Core.Logging;
using YAXLib;

namespace GoodAI.Modules.NeuralNetwork.Layers
{



    /// <author>GoodAI</author>
    /// <meta>mz</meta>
    /// <status>Working</status>
    /// <summary>Convolutional layer.</summary>
    /// <description>
    /// Classic convolutional layer that performs convolution on image windows using its filters.\n
    /// Great tutorial on filter and input dimensions is available here: http://cs231n.github.io/convolutional-networks/ \n \n
    /// 
    /// You can use AutomaticInput to determine parameters of the convolution. By default, it will try to preserve input dimensions on the output.
    /// 
    /// 
    ///  </description>
    public class MyConvolutionLayer : MyAbstractWeightLayer, IMyCustomTaskFactory
    {

        public MyPadImageTask PadImageTask { get; protected set; }
        public MyConvolutionInitLayerTask InitLayerTask { get; protected set; }
        public MyConvolutionUpdateWeights UpdateWeights { get; protected set; }

        #region Parameters

        public override ConnectionType Connection
        {
            get { return ConnectionType.CONVOLUTION; }
        }


        [YAXSerializableField(DefaultValue = 128)]
        [MyBrowsable, Category("\tLayer"), ReadOnly(true)]
        public override int Neurons { get; set; }



        [YAXSerializableField(DefaultValue = 16)]
        [MyBrowsable, Category("Filter")]
        public int FilterCount { get; set; }


        [YAXSerializableField(DefaultValue = false)]
        private bool m_autoInput = false;
        [MyBrowsable, Category("Input image dimensions"), DisplayName("Automatic parameters")]
        public bool AutomaticInput
        {
            get { return m_autoInput; }
            set
            {
                m_autoInput = false;
                if (value)
                {
                    if (PreviousConnectedLayers.Count <= 0)
                    {
                        Log.Error(this.GetType(), "No connected layers to layer " + this.Name + ". Please update memory blocks.");
                        return;
                    }

                    if (Input.Owner is MyAbstractLayer)
                    {
                        MyAbstractLayer prevLayer = Input.Owner as MyAbstractLayer;

                        if (prevLayer is MyConvolutionLayer)
                        {
                            InputWidth = ((MyConvolutionLayer) prevLayer).OutputWidth;
                            InputHeight = ((MyConvolutionLayer) prevLayer).OutputHeight;
                            InputDepth = ((MyConvolutionLayer) prevLayer).FilterCount;
                            Log.Info(this.GetType(), "Input parameters of layer " + this.Name + " were set succesfully.");
                            return;
                        }

                        else if (prevLayer is MyPoolingLayer)
                        {
                            InputWidth = ((MyPoolingLayer) prevLayer).OutputWidth;
                            InputHeight = ((MyPoolingLayer) prevLayer).OutputHeight;
                            InputDepth = ((MyPoolingLayer) prevLayer).Depth;
                            Log.Info(this.GetType(), "Input parameters of layer " + this.Name + " were set succesfully.");
                            return;
                        }

                        // assume input image
                        if ((prevLayer is MyHiddenLayer) || (Input != null && Input.Count > 0))
                        {
                            Log.Info(this.GetType(), "Layer " + this.Name +
                                                 " cannot interpret input neuron count as image width, height and depth automatically.");
                            return;
                        }
                    }

                    Log.Warn(this.GetType(), "Input parameters for layer " + this.Name +
                                            " could not be set automatically.");
                }
            }
        }


        [YAXSerializableField(DefaultValue = 1)]
        private int m_inputDepth = 1;
        [MyBrowsable, Category("Input image dimensions")]
        public int InputDepth
        {
            get { return m_inputDepth; }
            set
            {
                if (value < 1)
                    return;
                m_inputDepth = value;
            }
        }


        [YAXSerializableField(DefaultValue = 2)]
        private int m_inputWidth = 2;
        [MyBrowsable, Category("Input image dimensions")]
        public int InputWidth
        {
            get { return m_inputWidth; }
            set
            {
                if (value < 1)
                    return;
                m_inputWidth = value;
                OutputWidth = SetOutputDimension(InputWidth, FilterWidth, HorizontalStride, ZeroPadding);
            }
        }


        [YAXSerializableField(DefaultValue = 2)]
        private int m_inputHeight = 2;
        [MyBrowsable, Category("Input image dimensions")]
        public int InputHeight
        {
            get { return m_inputHeight; }
            set
            {
                if (value < 1)
                    return;
                m_inputHeight = value;
                OutputHeight = SetOutputDimension(InputHeight, FilterHeight, VerticalStride, ZeroPadding);
            }
        }



        [YAXSerializableField(DefaultValue = false)]
        private bool m_autoOutput = false;
        [MyBrowsable, Category("Output image dimensions"), DisplayName("Automatic zero padding")]
        public bool AutomaticOutput
        {
            get { return m_autoOutput; }
            set
            {
                m_autoOutput = false;
                if (value)
                {
                    int currentOutSize = 1 + (int)Math.Ceiling( (InputWidth - FilterWidth) / (double)HorizontalStride);
                    if (((InputWidth - currentOutSize) % 2) != 0)
                        Log.Warn(this.GetType(), "Zero padding of " + this.Name + " set automatically but filter will not cover the whole padded image when convolving.");
                    else
                    {
                        Log.Info(this.GetType(), "Output parameters of layer " + this.Name + " were set succesfully.");                        
                    }
                    ZeroPadding = (int)Math.Ceiling((InputWidth - currentOutSize) / (double)2);

                }
            }
        }



        [YAXSerializableField(DefaultValue = 0)]
        private int m_outputWidth = 0;
        [MyBrowsable, Category("Output image dimensions"), ReadOnly(true)]
        public int OutputWidth
        {
            get { return m_outputWidth; }
            set
            {
                if (value < 1)
                    return;
                m_outputWidth = value;
            }
        }

        [YAXSerializableField(DefaultValue = 0)]
        private int m_outputHeight = 0;
        [MyBrowsable, Category("Output image dimensions"), ReadOnly(true)]
        public int OutputHeight
        {
            get { return m_outputHeight; }
            set
            {
                if (value < 1)
                    return;
                m_outputHeight = value;
            }
        }

        [YAXSerializableField(DefaultValue = 1)]
        private int m_zeroPadding = 1;
        [MyBrowsable, Category("Output image dimensions")]
        public int ZeroPadding
        {
            get { return m_zeroPadding; }
            set
            {
                if (value < 0)
                    return;
                m_zeroPadding = value;
                OutputWidth = SetOutputDimension(InputWidth, FilterWidth, HorizontalStride, ZeroPadding);
                OutputHeight = SetOutputDimension(InputHeight, FilterHeight, VerticalStride, ZeroPadding);
            }
        }



        [YAXSerializableField(DefaultValue = 3)]
        private int m_width = 3;
        [MyBrowsable, Category("Filter")]
        public int FilterWidth
        {
            get { return m_width; }
            set
            {
                if (value < 1)
                    return;
                m_width = value;
                OutputWidth = SetOutputDimension(InputWidth, FilterWidth, HorizontalStride, ZeroPadding);
            }
        }

        [YAXSerializableField(DefaultValue = 3)]
        private int m_height = 3;
        [MyBrowsable, Category("Filter")]
        public int FilterHeight
        {
            get { return m_height; }
            set
            {
                if (value < 1)
                    return;
                m_height = value;
                OutputHeight = SetOutputDimension(InputHeight, FilterHeight, VerticalStride, ZeroPadding);
            }
        }

        [YAXSerializableField(DefaultValue = 1)]
        private int m_horizontalStride = 1;
        [MyBrowsable, Category("Filter")]
        public int HorizontalStride
        {
            get { return m_horizontalStride; }
            set
            {
                if (value < 1)
                    return;
                m_horizontalStride = value;
                OutputWidth = SetOutputDimension(InputWidth, FilterWidth, HorizontalStride, ZeroPadding);
            }
        }

        [YAXSerializableField(DefaultValue = 1)]
        private int m_verticalStride = 1;
        [MyBrowsable, Category("Filter")]
        public int VerticalStride
        {
            get { return m_verticalStride; }
            set
            {
                if (value < 1)
                    return;
                m_verticalStride = value;
                OutputHeight = SetOutputDimension(InputHeight, FilterHeight, VerticalStride, ZeroPadding);
            }
        }



        #endregion

        #region Memory blocks
        public MyMemoryBlock<float> PaddedImage { get; protected set; }


        #endregion

        //Memory blocks size rules
        public override void UpdateMemoryBlocks()
        {
            Neurons = OutputWidth*OutputHeight*FilterCount;
            base.UpdateMemoryBlocks();
            if (Neurons > 0)
            {
                // allocate memory scaling with number of neurons in layer
                Delta.Count = Neurons;
                Delta.ColumnHint = OutputWidth;
                Bias.Count = FilterCount;
                PreviousBiasDelta.Count = Neurons; // momentum method

                // RMSProp allocations
                MeanSquareWeight.Count = Weights.Count;
                MeanSquareBias.Count = Bias.Count;

                // Adadelta allocation
                //AdadeltaWeight.Count = Weights.Count;
                //AdadeltaBias.Count = Bias.Count;

                if (ZeroPadding > 0)
                {
                    PaddedImage.Count = InputDepth*(InputWidth + 2*ZeroPadding)*(InputHeight + 2*ZeroPadding);
                    PaddedImage.ColumnHint = InputWidth + 2*ZeroPadding;
                }
                else
                    PaddedImage.Count = 0;

                // allocate memory scaling with input
                if (Input != null)
                {
                    Weights.Count = FilterWidth * FilterHeight * InputDepth * FilterCount;
                    Weights.ColumnHint = FilterWidth;

                    PreviousWeightDelta.Count = Weights.Count;
                    PreviousWeightDelta.ColumnHint = Weights.ColumnHint;

                    //AdadeltaWeight.ColumnHint = Weights.ColumnHint;
                    MeanSquareWeight.ColumnHint = Weights.ColumnHint;
                }

                if (Weights.Count % 2 != 0)
                    Weights.Count++;
                if (Bias.Count % 2 != 0)
                    Bias.Count++;


            }
        }

        //Validation rules
        public override void Validate(MyValidator validator)
        {
            validator.AssertError((InputHeight - FilterHeight + 2 * ZeroPadding) % VerticalStride == 0, this, "Filter doesn't fit vertically when striding.");

            validator.AssertError((InputWidth - FilterWidth + 2 * ZeroPadding) % HorizontalStride == 0, this, "Filter doesn't fit horizontally when striding.");

            validator.AssertInfo(ZeroPadding == (FilterWidth - 1) / 2 && ZeroPadding == (FilterHeight - 1) / 2, this, "Input and output might not have the same dimension. Set stride to 1 and zero padding to ((FilterSize - 1) / 2) to fix this.");
        }

        // description
        public override string Description
        {
            get
            {
                return "Convolutional layer";
            }
        }

        public static int SetOutputDimension(int inputSize, int filterSize, int stride, int zeroPadding)
        {
            return 1 + (zeroPadding * 2 + inputSize - filterSize) / stride;
        }

        public void CreateTasks()
        {
            ForwardTask = new MyConvolutionForwardTask();
            DeltaBackTask = new MyConvolutionBackwardTask();
        }
    }
}
