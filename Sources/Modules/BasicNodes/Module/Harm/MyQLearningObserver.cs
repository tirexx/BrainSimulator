﻿using GoodAI.Core.Observers.Helper;
using GoodAI.Modules.Harm;
using ManagedCuda;
using System.Collections.Generic;

namespace GoodAI.Modules.Observers
{

    /// <author>GoodAI</author>
    /// <meta>jv</meta>
    /// <status>Working</status>
    /// <summary>
    /// Observes only flat QLearning node.
    /// </summary>
    public class MyQLearningObserver : MyAbstractQLearningObserver<MyDiscreteQLearningNode>
    {
        protected override void Execute()
        {

            if (m_qMatrix != null && MatrixSizeOK())
            {
                m_kernel.SetupExecution(TextureWidth * TextureHeight);
                m_kernel.Run(m_plotValues.DevicePointer, m_actionIndices.DevicePointer, m_actionLabels.DevicePointer, numOfActions, LABEL_PIXEL_WIDTH, LABEL_PIXEL_WIDTH, 0f, MaxQValue, m_qMatrix.GetLength(0), m_qMatrix.GetLength(1), VBODevicePointer);

                if (ViewMode == ViewMethod.Orbit_3D)
                {
                    m_vertexKernel.SetupExecution(m_qMatrix.Length);
                    m_vertexKernel.Run(m_plotValues.DevicePointer, 0.1f, m_qMatrix.GetLength(0), m_qMatrix.GetLength(1), MaxQValue, VertexVBODevicePointer);
                }
            }

            float[,] lastQMatrix = m_qMatrix;
            Target.Vis.ReadTwoDimensions(ref m_qMatrix, ref m_qMatrixActions, XAxisVariableIndex, YAxisVariableIndex, ShowCurrentMotivations);

            if (lastQMatrix != m_qMatrix)
            {
                TriggerReset();
            }
            else if (m_qMatrix != null && base.MatrixSizeOK())
            {
                m_plotValues.CopyToDevice(m_qMatrix);
                m_actionIndices.CopyToDevice(m_qMatrixActions);
            }
        }

        private CudaDeviceVariable<float> m_StringDeviceBuffer;

        protected override void Reset()
        {
            List<MyMotivatedAction> actions = Target.Rds.ActionManager.Actions;
            m_StringDeviceBuffer = new CudaDeviceVariable<float>(1000);
            m_StringDeviceBuffer.Memset(0);

            if (numOfActions < actions.Count)
            {
                if (m_actionLabels != null)
                {
                    m_actionLabels.Dispose();
                }

                m_actionLabels = new CudaDeviceVariable<uint>(actions.Count * LABEL_PIXEL_WIDTH * LABEL_PIXEL_WIDTH);
                m_actionLabels.Memset(0);

                for (int i = 0; i < actions.Count; i++)
                {
                    MyDrawStringHelper.String2Index(actions[i].GetLabel(), m_StringDeviceBuffer);
                    MyDrawStringHelper.DrawStringFromGPUMem(m_StringDeviceBuffer, i * LABEL_PIXEL_WIDTH + 5, 8, 0, 0xFFFFFFFF, m_actionLabels.DevicePointer, LABEL_PIXEL_WIDTH * actions.Count, LABEL_PIXEL_WIDTH, 0, actions[i].GetLabel().Length);
                }

                numOfActions = actions.Count;
            }
            Target.Vis.ReadTwoDimensions(ref m_qMatrix, ref m_qMatrixActions, XAxisVariableIndex, YAxisVariableIndex, ShowCurrentMotivations);

            if (MatrixSizeOK())
            {
                DrawDataToGpu();
            }
        }
    }
}
