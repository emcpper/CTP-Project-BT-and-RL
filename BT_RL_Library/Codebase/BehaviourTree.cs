﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using CustomExtensions;

namespace BT_and_RL
{
    //namespace used for behaviour trees
    namespace Behaviour_Tree
    {
        //each node in the tree returns one of these to mark it's current status
        public enum StatusValue
        {
            NULL = 0,
            SUCCESS,
            FAILED,
            RUNNING,
        }

        //Blackboard class used by the tree to read and write information that this agent knows
        [Serializable]
        public class Blackboard
        {
            private Dictionary<string, object> memory;

            public Blackboard()
            {
                memory = new Dictionary<string, object>();
            }

            //Note: the returned value needs to be casted at the other end to the type we want
            public object GetValue(string valueName)
            {
                if (memory.ContainsKey(valueName))
                {
                    return memory[valueName];
                }
                return null;
            }

            public void SetValue(string valueName, object newValue)
            {
                if (memory.ContainsKey(valueName))
                {
                    memory[valueName] = newValue;
                }
                else
                {
                    memory.Add(valueName, newValue);
                }
            }
        }

        //Class for the main tree to inherit from. This should only have one child as the root node
        [Serializable]
        public class BTTree
        {
            protected StatusValue status;
            protected BTTask child;
            protected Blackboard blackboard;
            public Blackboard Blackboard
            {
                get { return blackboard; }
                set { blackboard = value; }
            }

            public BTTree(BTTask root)
            {
                blackboard = new Blackboard();
                child = root;
                root.SetTreeDepth(0);

                BeginTree();
            }

            public void BeginTree()
            {
                status = StatusValue.RUNNING;
                child.Begin();
            }

            //Each frame ticks the tree once
            public void Tick()
            {
                Blackboard.SetValue("QValueDebugString", "");
                status = child.Tick(blackboard);
            }

            public StatusValue GetStatus()
            {
                return status;
            }

            public string BuildDebugString()
            {
                string fullOutputString = "";
                child.DisplayValues(ref fullOutputString);
                return fullOutputString;
            }
        }

        //Base class for any task within a Behaviour Tree
        [Serializable]
        public class BTTask
        {
            protected StatusValue status;
            protected string taskName;
            protected int treeDepth = 0;
            protected bool poolable = false;    //set to true if this task can be pooled

            protected HashSet<int> compatibility;   //stores the situations that this task can be applied in (links to an enum defined in the game's project)

            public BTTask()
            {
                compatibility = new HashSet<int>();
            }

            //called when first ticked to set it as running
            virtual public void Begin()
            {
                status = StatusValue.RUNNING;
            }

            virtual public StatusValue Tick(Blackboard blackboard)
            {
                status = StatusValue.RUNNING;
                return status;
            }

            virtual public void Terminate()
            {
                status = StatusValue.FAILED;
            }

            public StatusValue GetStatus()
            {
                return status;
            }

            public string GetName()
            {
                return taskName;
            }

            public bool CheckIfPoolable()
            {
                return poolable;
            }

            //used to write out the behaviour tree's structure
            virtual public void DisplayValues(ref string fullOutputString)
            {
                fullOutputString += "\n";
                fullOutputString += new string('\t', treeDepth);
                fullOutputString += GetName();
                //fullOutputString += "\t " + GetStatus();
            }
            
            public int GetTreeDepth()
            {
                return treeDepth;
            }

            virtual public void SetTreeDepth(int depth)
            {
                treeDepth = depth;
            }            

            //this checks if the task is compatible with the node that is trying to add it (for RL purposes)
            public bool IsTaskCompatible(HashSet<int> compareWith)
            {
                foreach (int num in compareWith)
                {
                    if (compatibility.Contains(num))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        //Base class for a condition check
        [Serializable]
        public abstract class BTCondition : BTTask
        {
            abstract public bool CheckCondition();
        }

        //Base class for a single action to perform (leaf node)
        //This class is no different to a BTTask, however it is used by the system to differentiate between
        //a character action and a general BTTask class.
        //This is how the action pool is filled
        [Serializable]
        public abstract class BTAction : BTTask
        {
            public BTAction() { }
        }

        #region Composites

        //A composite task that stops at first successful action
        [Serializable]
        public class BTSelector : BTTask
        {
            protected int currentChildIndex = 0;
            protected List<BTTask> children = new List<BTTask>();

            public BTSelector() { taskName = "BTSelector"; }

            public BTSelector(List<BTTask> tasks)
            {
                taskName = "BTSelector";
                children = tasks;
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                currentChildIndex = 0;
                for (int i = 0; i < children.Count; i++)
                {
                    if (i == currentChildIndex)
                    {
                        status = children[i].Tick(blackboard);
                        if (status != StatusValue.FAILED)
                        {
                            return status;
                        }
                    }
                    currentChildIndex++;
                }
                status = StatusValue.FAILED;
                return status;
            }

            public override void DisplayValues(ref string fullOutputString)
            {
                base.DisplayValues(ref fullOutputString);
                for(int i = 0; i < children.Count; i++)
                {
                    children[i].DisplayValues(ref fullOutputString);
                }
            }

            public override void SetTreeDepth(int depth)
            {
                base.SetTreeDepth(depth);
                for(int i = 0; i < children.Count; i++)
                {
                    children[i].SetTreeDepth(depth + 1);
                }
            }
        }

        //A composite task that stops at first failed action
        [Serializable]
        public class BTSequence : BTTask
        {
            protected int currentChildIndex = 0;
            protected List<BTTask> children = new List<BTTask>();

            public BTSequence() { taskName = "BTSequence"; }

            public BTSequence(List<BTTask> tasks)
            {
                taskName = "BTSequence";
                children = tasks;
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].SetTreeDepth(treeDepth + 1);
                }
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                currentChildIndex = 0;
                for (int i = 0; i < children.Count(); i++)
                {
                    if (i == currentChildIndex)
                    {
                        status = children[i].Tick(blackboard);
                        if (status != StatusValue.SUCCESS)
                        {
                            return status;
                        }
                    }
                    currentChildIndex++;
                }
                status = StatusValue.SUCCESS;
                return status;
            }

            public override void DisplayValues(ref string fullOutputString)
            {
                base.DisplayValues(ref fullOutputString);
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].DisplayValues(ref fullOutputString);
                }
            }

            public override void SetTreeDepth(int depth)
            {
                base.SetTreeDepth(depth);
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].SetTreeDepth(depth + 1);
                }
            }
        }
        #endregion

        #region Decorators
        //A decorator task that only has one child
        public class BTDecorator : BTTask
        {
            protected BTTask child = null;

            public BTDecorator(BTTask task)
            {
                taskName = "BTDecorator";
                child = task;
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                status = StatusValue.RUNNING;
                if (child != null)
                {
                    status = child.Tick(blackboard);
                    if (status != StatusValue.FAILED)
                    {
                        return status;
                    }
                }
                return StatusValue.FAILED;
            }

            public override void DisplayValues(ref string fullOutputString)
            {
                base.DisplayValues(ref fullOutputString);
                if(child != null)
                {
                    child.DisplayValues(ref fullOutputString);
                }
            }

            public override void SetTreeDepth(int depth)
            {
                base.SetTreeDepth(depth);
                if(child != null)
                {
                    child.SetTreeDepth(depth + 1);
                }
            }
        }
        #endregion

        #region Unused
        /*
        //Selector that randomises list before checking
        [Serializable]
        public class BTShuffleSelector : BTTask
        {
            [SerializeField]
            protected List<BTTask> children = new List<BTTask>();

            public BTShuffleSelector() { }

            public BTShuffleSelector(List<BTTask> tasks)
            {
                children = tasks;
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                children.Shuffle();
                foreach (BTTask c in children)
                {
                    status = c.Tick(blackboard);
                    if(status != StatusValue.FAILED)
                    {
                        return status;
                    }
                }
                status = StatusValue.FAILED;
                return status;
            }
        }
       
        //Sequence that randomises list before checking
        [Serializable]
        public class BTShuffleSequence : BTTask
        {
            [SerializeField]
            protected List<BTTask> children = new List<BTTask>();

            public BTShuffleSequence() { }

            public BTShuffleSequence(List<BTTask> tasks)
            {
                children = tasks;
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                children.Shuffle();
                foreach(BTTask c in children)
                {
                    status = c.Tick(blackboard);
                    if (status != StatusValue.SUCCESS)
                    {
                        return status;
                    }
                }
                status = StatusValue.SUCCESS;
                return status;
            }
        }

        //A parallel task that runs its children concurrently
        [Serializable]
        public class BTParallel : BTTask
        {
            //List of children currently running
            protected List<BTTask> running_children;

            StatusValue result;
            [SerializeField]
            protected List<BTTask> children = new List<BTTask>();

            public BTParallel() { }

            public BTParallel(List<BTTask> tasks)
            {
                children = tasks;
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                result = StatusValue.NULL;

                foreach(BTTask c in children)
                {
                    Thread thread = new Thread(() => RunChild(c, blackboard));
                    thread.Start();
                }
                //Sleep this thread between checks for completion
                while(result == StatusValue.NULL)
                {
                    Thread.Sleep(100);
                }
                return result;
            }

            //Runs the current child in its own thread
            protected void RunChild(BTTask child, Blackboard blackboard)
            {
                running_children.Add(child);
                StatusValue returned = child.Tick(blackboard);
                running_children.Remove(child);

                //If the child fails, terminate
                if(returned == StatusValue.FAILED)
                {
                    Terminate();
                    result = StatusValue.FAILED;
                }
                //If all children succeed, this has succeeded
                else if(running_children.Count == 0)
                {
                    result = StatusValue.SUCCESS;
                }
            }

            //Parallel tasks fail when any child fails. It must then tell all other children to terminate
            public override void Terminate()
            {
                foreach(BTTask c in running_children)
                {
                    c.Terminate();
                }
            }
        }

        //Decorator that inverts the its child's return value
        [Serializable]
        public class BTInverter : BTDecorator
        {
            public BTInverter(BTTask task) : base(task)
            {
                child = task;
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                status = StatusValue.RUNNING;
                status = base.Tick(blackboard);
                //Invert the result of the child node
                if(status == StatusValue.SUCCESS)
                {
                    status = StatusValue.FAILED;
                    return status;
                }
                else if(status == StatusValue.FAILED)
                {
                    status = StatusValue.SUCCESS;
                    return status;
                }
                return status;
            }
        }

        //Decorator node that guards a thread
        [Serializable]
        public class BTSemaphoreGuard : BTDecorator
        {
            protected Semaphore semaphore;

            BTSemaphoreGuard(BTTask task, Semaphore semaphore) : base(task)
            {
                child = task;
                this.semaphore = semaphore;
            }

            public override StatusValue Tick(Blackboard blackboard)
            {
                if(semaphore.WaitOne())
                {
                    status = child.Tick(blackboard);
                    semaphore.Release();
                    return status;
                }
                return StatusValue.FAILED;
            }
        }
        */
        #endregion
    }
}
