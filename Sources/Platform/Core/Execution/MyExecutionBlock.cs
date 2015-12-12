﻿using GoodAI.Core.Nodes;
using GoodAI.Core.Task;
using GoodAI.Core.Utils;
using System;
using GoodAI.Platform.Core.Logging;

namespace GoodAI.Core.Execution
{   
    /// Container for multiple IMyExecutable objects
    public class MyExecutionBlock : IMyExecutable
    {
        public bool Enabled { get { return true; } }        
        public uint SimulationStep { get; set; }
        public virtual string Name { get; set; }

        public MyExecutionBlock Parent { get; private set; }

        protected IMyExecutable[] m_children;
        protected int m_childIterator = 0;

        /// Element which is to be run next
        public IMyExecutable CurrentChild
        {
            get
            {
                if (m_childIterator < m_children.Length)
                {
                    return m_children[m_childIterator];
                }
                else
                {
                    return null;
                }
            }
        }

        /// All children elements
        public IMyExecutable[] Children
        {
            get { return m_children; }
        }

        /// <summary>
        /// Creates MyExecutionBlock from given IMyExecutable elements
        /// </summary>
        /// <param name="children">List of elements</param>
        public MyExecutionBlock(params IMyExecutable[] children)
        {
            m_children = children;

            for (int i = 0; i < m_children.Length; i++)
            {
                if (m_children[i] is MyExecutionBlock)
                {
                    (m_children[i] as MyExecutionBlock).Parent = this;
                }
            }
        }
        
        /// Executes current IMyExecutable children and moves to next one
        public virtual MyExecutionBlock ExecuteStep()
        {
            if (m_childIterator < m_children.Length)
            {
                IMyExecutable currentChild = m_children[m_childIterator];
                m_childIterator++;

                if (currentChild is MyExecutionBlock)
                {
                    MyExecutionBlock childList = currentChild as MyExecutionBlock;
                    childList.Reset();

                    return childList;
                }
                else
                {
                    if (currentChild.Enabled)
                    {
                        Log.Debug(this.GetType(), "Executing: " + currentChild.Name);
                        currentChild.SimulationStep = SimulationStep;
                        currentChild.Execute();
                    }
                    return this;
                }
            }
            else
            {
                return Parent;
            }
        }

        /// Go back to first element of MyExecutionBlock
        public virtual void Reset()
        {
            m_childIterator = 0;
        }

        /// Executes all children elements
        public virtual void Execute()
        {
            for (int i = 0; i < m_children.Length; i++)
            {
                IMyExecutable child = m_children[i];

                if (child.Enabled)
                {
                    child.SimulationStep = SimulationStep;
                    child.Execute();
                }
            }
        }

        public delegate void IteratorAction(IMyExecutable executable);

        /// <summary>
        /// Iterates over all children and performs action on all of them
        /// </summary>
        /// <param name="recursive">Iterate recursively if set to true</param>
        /// <param name="action">Action to perform on iterated elements</param>
        public void Iterate(bool recursive, IteratorAction action)
        {
            Iterate(this, recursive, action);
        }

        private void Iterate(MyExecutionBlock block, bool recursive, IteratorAction action)
        {
            foreach (IMyExecutable executable in block.Children)
            {
                action(executable);

                if (recursive && executable is MyExecutionBlock)
                {
                    Iterate(executable as MyExecutionBlock, recursive, action);
                }
            }
        }     
    }

    /// <summary>
    /// Execution block with IF condition. Block is run, only when condition is met
    /// </summary>
    public class MyIfBlock : MyExecutionBlock
    {
        public Func<bool> Condition { get; private set; }   // Function evaluating the condition. Block is run, when function returns true
        private bool m_testPassed;
        public override string Name { get { return Condition() ? "If (passed)" : "If (not passed)"; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="condition">Condition to be met</param>
        /// <param name="children">Children of MyExecutionBlock</param>
        public MyIfBlock(Func<bool> condition, params IMyExecutable[] children) : base(children)
        {
            Condition = condition;
        }

        public override void Reset()
        {
            base.Reset();
            m_testPassed = false;
        }

        public override MyExecutionBlock ExecuteStep()
        {
            if (m_testPassed)
            {
                return base.ExecuteStep();
            }
            else 
            {               
                if (Condition())
                {             
                    m_testPassed = true;
                    return this;
                }
                else 
                {                 
                    return Parent;
                }
            }
        }

        public override void Execute()
        {
            if (Condition())
            {
                base.Execute();
            }
        }
    }

    /// <summary>
    /// Execution block which can be run multiple times
    /// </summary>
    public class MyLoopBlock : MyExecutionBlock
    {
        public Func<int, bool> Condition { get; private set; }  // Function controlling the execution. Iteration counter is passed to it and execution block is run if function returns true.
        public override string Name { get { return "Loop (i = " + m_loopIterator + ")"; } }
        private int m_loopIterator;
        private bool m_testPassed;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="condition">Function controlling the run</param>
        /// <param name="children">Children of MyExecutionBlock</param>
        public MyLoopBlock(Func<int, bool> condition, params IMyExecutable[] children) : base(children)
        {
            Condition = condition;
        }

        public override void Reset()
        {
            base.Reset();
            m_loopIterator = 0; 
            m_testPassed = false;
        }

        public override MyExecutionBlock ExecuteStep()
        {
            if (m_testPassed && m_childIterator < m_children.Length)
            {
                return base.ExecuteStep();                               
            }
            else
            {                
                if (Condition(m_loopIterator))                
                {
                    m_testPassed = true;
                    m_loopIterator++;
                    base.Reset();
                    
                    return this;
                }
                else
                {
                    return Parent;
                }
            }
        }

        public override void Execute()
        {
            m_loopIterator = 0;

            while (Condition(m_loopIterator))
            {
                base.Execute();
                m_loopIterator++;
            }
        }
    }

    public abstract class MySignalTask : IMyExecutable
    {
        public uint SimulationStep { get; set; }

        public abstract string Name { get; }

        protected MyNode m_node;        

        public MySignalTask(MyNode node)
        {
            m_node = node;
        }

        public bool Enabled
        {
            get { return true; }
        }

        public abstract void Execute();        
    }

    public class MyIncomingSignalTask : MySignalTask
    {
        public MyIncomingSignalTask(MyNode node) : base(node) { }

        public override string Name { get { return "Receive Signals (" + m_node.Name + ")"; } }

        public override void Execute()
        {            
            m_node.ProcessIncomingSignals();
        }
    }

    public class MyOutgoingSignalTask : MySignalTask
    {
        public MyOutgoingSignalTask(MyNode node) : base(node) { }

        public override string Name { get { return "Send Signals (" + m_node.Name + ")"; } }

        public override void Execute()
        {         
            m_node.ProcessOutgoingSignals();
        }
    }
}
