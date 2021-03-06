﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BT_and_RL.Behaviour_Tree;

namespace BT_and_RL.QLearning
{
    //class to instantiate actions when dynamically added to the behaviour tree
    public class ActionPool
    {
        private static ActionPool instance;
        public static ActionPool Instance
        {
            get
            {
                if(instance == null)
                {
                    instance = new ActionPool();
                }
                return instance;
            }
        }

        //this stores all of the actions that can be added
        protected Dictionary<string, Type> actionDictionary;

        public ActionPool()
        {
            actionDictionary = new Dictionary<string, Type>();

            //initialise the action pool with the types of actions that will be used
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {                
                //create an array for all of the actions that the user has implemented
                //currently only uses BTAction but could be implemented to work with all task types in future
                Type[] actions = assemblies[i].GetTypes().Where(t => t.IsSubclassOf(typeof(BTAction))).ToArray();
                for (int j = 0; j < actions.Length; j++)
                {
                    //create a temporary instance of the action class so that its name can be retrieved
                    BTTask task = (BTTask)assemblies[i].CreateInstance(actions[j].Name);
                    //only let this action be added if it is flagged as 'poolable'
                    //custom flag to prevent system breaking additions
                    if (task.CheckIfPoolable())
                    {
                        if (!actionDictionary.ContainsKey(task.GetName()))
                        {
                            actionDictionary.Add(task.GetName(), actions[j]);
                        }
                    }
                }                
            }
        }         

        //returns the specified action from the pool
        public object GetAction(string name)
        {
            if (actionDictionary.ContainsKey(name))
            {
                return System.Reflection.Assembly.GetAssembly(actionDictionary[name]).CreateInstance(actionDictionary[name].Name);
            }

            return "action not found";
        }

        //returns a random action from the pool
        public object GetRandomAction()
        {
            Type get = actionDictionary.Values.ElementAt(CustomExtensions.ThreadSafeRandom.ThisThreadsRandom.Next(0, actionDictionary.Count));
            return System.Reflection.Assembly.GetAssembly(get).CreateInstance(get.Name);
        }
    }
}
