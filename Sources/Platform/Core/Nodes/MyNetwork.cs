﻿using GoodAI.Core.Configuration;
using GoodAI.Core.Memory;
using GoodAI.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GoodAI.Platform.Core.Logging;
using YAXLib;

namespace GoodAI.Core.Nodes
{
    [YAXSerializeAs("Network")]    
    public class MyNetwork : MyNodeGroup
    {
        [YAXSerializeAs("Connection")]
        protected class MyConnectionProxy
        {
            [YAXSerializableField, YAXAttributeForClass]
            public int From { get; set; }
            [YAXSerializableField, YAXAttributeForClass]
            public int To { get; set; }
            [YAXSerializableField, YAXAttributeForClass]
            public int FromIndex { get; set; }
            [YAXSerializableField, YAXAttributeForClass]
            public int ToIndex { get; set; }
        };

        [YAXSerializableField, YAXSerializeAs("Connections")]
        protected List<MyConnectionProxy> m_connections = new List<MyConnectionProxy>();

        public void PrepareConnections()
        {
            m_connections.Clear();            
            PrepareConnections(this);
        }

        private void PrepareConnections(MyNodeGroup nodeGroup)
        {
            foreach(MyOutput outputNode in nodeGroup.GroupOutputNodes) 
            {
                PrepareConnections(outputNode);            
            }            

            foreach (MyNode node in nodeGroup.Children)
            {
                PrepareConnections(node);
            }
        }

        private void PrepareConnections(MyNode node)
        {
            foreach (MyConnection inputConnection in node.InputConnections)
            {
                if (inputConnection != null)
                {
                    MyConnectionProxy cp = new MyConnectionProxy()
                    {
                        From = inputConnection.From.Id,
                        To = inputConnection.To.Id,
                        FromIndex = inputConnection.FromIndex,
                        ToIndex = inputConnection.ToIndex
                    };
                    m_connections.Add(cp);
                }
            }

            if (node is MyNodeGroup)
            {
                PrepareConnections(node as MyNodeGroup);
            }
        }

        public void FilterPreparedCollection(HashSet<int> idSet)
        {
            List<MyConnectionProxy> filtered = new List<MyConnectionProxy>();

            foreach (MyConnectionProxy cp in m_connections)
            {
                if (idSet.Contains(cp.From))
                {
                    filtered.Add(cp);
                }
            }

            m_connections.Clear();
            m_connections.AddRange(filtered);
        }

        public int UpdateAfterDeserialization(int topId, MyProject parentProject)
        {
            if (topId < this.Id)
            {
                topId = this.Id;
            }

            this.Owner = parentProject;
            
            Dictionary<int, MyNode> nodes = new Dictionary<int,MyNode>();
            topId = CollectNodesAndUpdate(this, nodes, topId);

            parentProject.ReadOnly = false;
            
            MyNodeGroup.IteratorAction findUnknownAction = delegate(MyNode node)
            {
                if (!MyConfiguration.KnownNodes.ContainsKey(node.GetType()))
                {
                    Log.Warn(this.GetType(), "Unknown node type in loaded project: " + node.GetType());
                    parentProject.ReadOnly = true;

                    try
                    {
                        MyNodeConfig nodeConfig = new MyNodeConfig()
                        {
                            NodeType = node.GetType(),
                            NodeTypeName = node.GetType().FullName
                        };

                        nodeConfig.InitIcons(Assembly.GetExecutingAssembly());
                        nodeConfig.AddObservers(Assembly.GetExecutingAssembly());

                        MyConfiguration.KnownNodes[nodeConfig.NodeType] = nodeConfig;
                    }
                    catch (Exception e)
                    {
                        Log.Error(this.GetType(), "Node type loading failed: " + e.Message);
                    }
                }
            };

            Iterate(true, findUnknownAction);

            foreach (MyConnectionProxy cp in m_connections)
            {                
                try
                {
                    MyConnection connection = new MyConnection(nodes[cp.From], nodes[cp.To], cp.FromIndex, cp.ToIndex);
                    connection.Connect();
                }
                catch (Exception e)
                {
                    Log.Error(this.GetType(), "Error during connection deserialization: From id " + cp.From +" to id " + cp.To);
                }
            }            

            return topId;
        }        

        private int CollectNodesAndUpdate(MyNodeGroup nodeGroup, Dictionary<int, MyNode> nodes, int topId) 
        {
            foreach (MyParentInput inputNode in nodeGroup.GroupInputNodes)
            {
                nodes[inputNode.Id] = inputNode;
                inputNode.Parent = nodeGroup;
                inputNode.Owner = Owner;

                if (topId < inputNode.Id)
                {
                    topId = inputNode.Id;
                }
            }

            foreach (MyOutput outputNode in nodeGroup.GroupOutputNodes)
            {
                nodes[outputNode.Id] = outputNode;
                outputNode.Parent = nodeGroup;
                outputNode.Owner = Owner;

                if (topId < outputNode.Id)
                {
                    topId = outputNode.Id;
                }
            }           

            foreach (MyNode node in nodeGroup.Children)
            {
                nodes[node.Id] = node;                

                //parent link 
                node.Parent = nodeGroup;
                node.Owner = Owner;          
                //topId collect
                if (topId < node.Id)
                {
                    topId = node.Id;
                }
                
                //task owner update
                if (node is MyWorkingNode)
                {
                    (node as MyWorkingNode).FinalizeTasksDeserialization();
                }
             
                if (node is MyNodeGroup)
                {
                    topId = CollectNodesAndUpdate(node as MyNodeGroup, nodes, topId);
                }

                //obsolete check 
                MyObsoleteAttribute obsolete = node.GetType().GetCustomAttribute<MyObsoleteAttribute>(true);
                if (obsolete != null) 
                {
                    string message = "You are using obsolete node type (" + node.GetType().Name + ") ";
                    if (obsolete.ReplacedBy != null)
                    {
                        message += "Use " + obsolete.ReplacedBy.Name + " instead.";
                    }
                    Log.Warn(this.GetType(), message);
                }
            }
            return topId;
        }

        public void SaveState(string fileName, uint simulationStep)
        {
            try
            {
                string dataFolder = Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + ".statedata";

                MyNetworkState networkState = new MyNetworkState()
                {
                    ProjectName = Owner.Name,
                    MemoryBlocksLocation = dataFolder,
                    SimulationStep = simulationStep
                };

                YAXSerializer serializer = new YAXSerializer(typeof(MyNetworkState),
                    YAXExceptionHandlingPolicies.ThrowErrorsOnly,
                    YAXExceptionTypes.Warning);

                serializer.SerializeToFile(networkState, fileName);

                if (Directory.Exists(dataFolder))
                {
                    Directory.Delete(dataFolder, true);
                }

                Directory.CreateDirectory(dataFolder);
                MyMemoryManager.Instance.SaveBlocks(this, true, dataFolder);
            }
            catch (Exception e)
            {
                Log.Error(this.GetType(), "Saving state failed: " + e.Message);
            }       
        }

        public MyNetworkState LoadState(string fileName)
        {
            try
            {
                YAXSerializer serializer = new YAXSerializer(typeof(MyNetworkState),
                    YAXExceptionHandlingPolicies.ThrowErrorsOnly,
                    YAXExceptionTypes.Warning);

                MyNetworkState networkState = (MyNetworkState)serializer.DeserializeFromFile(fileName);

                if (Owner.Name != networkState.ProjectName)
                {
                    throw new InvalidDataException("Different network state file: " + networkState.ProjectName);
                }
                MyMemoryManager.Instance.LoadBlocks(this, true, networkState.MemoryBlocksLocation);

                return networkState;
            }
            catch (Exception e)
            {
                Log.Error(this.GetType(), "Restoring state failed: " + e.Message);
                return new MyNetworkState();                
            }
        }
    }
}
