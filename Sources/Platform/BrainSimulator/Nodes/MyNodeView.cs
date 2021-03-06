﻿using GoodAI.BrainSimulator.Nodes;
using GoodAI.Core.Configuration;
using GoodAI.Core.Nodes;
using GoodAI.Core.Signals;
using GoodAI.Core.Utils;
using Graph;
using Graph.Items;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace GoodAI.BrainSimulator.NodeView
{
    internal class MyNodeView : Node
    {
        private MyNode m_node;
        public MyNode Node 
        {
            get { return m_node; }
            internal set
            {
                m_node = value;                
                InitBranches();
            }
        }

        protected NodeImageItem m_iconItem;
        protected NodeLabelItem m_descItem;

        public MyNodeConfig NodeInfo { get; private set; }
        protected Image m_icon;        

        protected List<NodeItem> m_inputBranches = new List<NodeItem>();
        protected List<NodeItem> m_outputBranches = new List<NodeItem>();

        protected List<MySignalItem> m_signals = new List<MySignalItem>();        

        public GraphControl Owner { get; set; }

        protected MyNodeView(MyNodeConfig nodeInfo, GraphControl owner) 
            : base("")
        {
            NodeInfo = nodeInfo;
            Owner = owner;

            m_icon = nodeInfo.BigImage;

            m_iconItem = new NodeImageItem(m_icon, 48, 48, false, false)
            {
                IsPassive = true,
                Tag = 0
            };
            m_descItem = new NodeLabelItem("")
            {
                IsPassive = true
            };

            AddItem(m_iconItem);       
            AddItem(m_descItem);            
        }        
        
        protected virtual void InitBranches()
        {
            MyNodeInfo nodeInfo = Node.GetInfo();

            foreach (PropertyInfo signalInfo in nodeInfo.RegisteredSignals)
            {
                MySignal signal = (MySignal)signalInfo.GetValue(Node);

                MySignalItem signalItem = new MySignalItem(signal);

                m_signals.Add(signalItem);
                AddItem(signalItem);
            }

            InitIOBranches(Node.InputBranches, m_iconItem.Input, m_inputBranches, Node.GetInfo().InputBlocks, isInput: true);
            InitIOBranches(Node.OutputBranches, m_iconItem.Output, m_outputBranches, Node.GetInfo().OutputBlocks, isInput: false);
        }

        private void InitIOBranches(int branchCount, NodeConnector connector, ICollection<NodeItem> branchList, IList<PropertyInfo> blocks, bool isInput)
        {
            if (branchCount == 1)
            {
                connector.Enabled = true;
                branchList.Add(m_iconItem);
            }
            else
            {
                for (int i = 0; i < branchCount; i++)
                {
                    string name = (i + 1) + "";

                    if (Node is MyWorkingNode)
                    {
                        name = blocks[i].Name;
                    }

                    NodeLabelItem branch = new NodeLabelItem(MyProject.ShortenMemoryBlockName(name), isInput, !isInput)
                    {
                        Tag = i,
                        IsPassive = true
                    };

                    branchList.Add(branch);
                    AddItem(branch);
                }
            }
        }

        public virtual void UpdateView()
        {
            if (Node != null)
            {
                Title = Node.Name;
                m_descItem.Text = Node.Description;

                if (Node.Location != null)
                {
                    Location = new PointF(Node.Location.X, Node.Location.Y);
                }
            }
        }           

        public override void OnEndDrag()
        {
            Node.Location = new MyLocation() { X = Location.X, Y = Location.Y };
        }       

        public NodeItem GetInputBranchItem(int index)
        {            
            return m_inputBranches[index];
        }

        public NodeItem GetOuputBranchItem(int index)
        {
            return m_outputBranches[index];
        }

        public bool InputBranchChangeNeeded 
        {
            get 
            {
                return Node.InputBranches != m_inputBranches.Count;
            } 
        }

        public bool OutputBranchChangeNeeded 
        {
            get 
            {
                return Node.OutputBranches != m_outputBranches.Count; 
            } 
        }

        public bool BranchChangeNeeded
        {
            get
            {
                return InputBranchChangeNeeded || OutputBranchChangeNeeded;
            }
        }      

        public static MyNodeView CreateNodeView(Type nodeType, GraphControl owner)
        {
            MyNodeConfig config = MyConfiguration.KnownNodes[nodeType];

            if (typeof(MyUserInput).IsAssignableFrom(nodeType))
            {
                return new MyUserInputView(config, owner);
            }
            if (typeof(MyGateInput).IsAssignableFrom(nodeType))
            {
                return new MyGateInputView(config, owner);
            }
            else if (typeof(MyNodeGroup).IsAssignableFrom(nodeType))                
            {
                return new MyNodeGroupView(config, owner);
            }
            else if (typeof(MyFork).IsAssignableFrom(nodeType) 
                || typeof(IMyVariableBranchViewNodeBase).IsAssignableFrom(nodeType))
            {
                return new MyVariableBranchView(config, owner);
            }
            else
            {
                return new MyNodeView(config, owner);
            }
        }

        public static MyNodeView CreateNodeView(MyNode node, GraphControl owner)
        {
            MyNodeView nodeView = CreateNodeView(node.GetType(), owner);
            nodeView.Node = node;

            return nodeView;
        }
        
    }
}
