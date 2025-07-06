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
    public class ActionQueueComponent : IComponent
    {
        public Queue<IAction> ActionQueue { get; } = new Queue<IAction>();
    }
}