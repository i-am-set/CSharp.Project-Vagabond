using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    /// <summary>
    /// Holds the queue of actions for an entity to perform.
    /// </summary>
    public class ActionQueueComponent : IComponent, ICloneableComponent
    {
        public Queue<IAction> ActionQueue { get; set; } = new Queue<IAction>();

        public IComponent Clone()
        {
            var clone = (ActionQueueComponent)this.MemberwiseClone();
            clone.ActionQueue = new Queue<IAction>();
            return clone;
        }
    }
}