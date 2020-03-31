﻿using Unity.Entities;

namespace Vella.Events
{
    /// <summary>
    /// An component for easy identification of events with EntityQuery
    /// </summary>
    public struct EntityEvent : IComponentData
    {
        public int Id;
    }

}