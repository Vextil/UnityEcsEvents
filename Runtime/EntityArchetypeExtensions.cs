﻿using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;

namespace Vella.Events
{
    public static class EntityArchetypeExtensions
    {
        /// <summary>
        /// Retrieves the current chunks from an <see cref="EntityArchetype"/>.
        /// </summary>
        public static unsafe void CopyChunksTo(this EntityArchetype archetype, NativeArray<ArchetypeChunk> destination)
        {
            var archetypeProxy = *(EntityArchetypeProxy*)&archetype;
            var chunkData = archetypeProxy.Archetype->Chunks;
            var destinationPtr = (ArchetypeChunkProxy*)destination.GetUnsafePtr();
            
            for (int i = 0; i < chunkData.Count; i++)
            {
                ArchetypeChunkProxy chunk;
                chunk.m_Chunk = chunkData.p[i];
                chunk.entityComponentStore = archetypeProxy._DebugComponentStore;
                destinationPtr[i] = chunk;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public unsafe struct ArchetypeChunkProxy
        {
            [FieldOffset(0)]
            public void* m_Chunk;

            [FieldOffset(8)]
            public void* entityComponentStore;
        }

        public unsafe struct EntityArchetypeProxy
        {
            [NativeDisableUnsafePtrRestriction]
            public ArchetypeProxy* Archetype;

            [NativeDisableUnsafePtrRestriction]
            public void* _DebugComponentStore;
        }

        public unsafe struct ArchetypeProxy
        {
            public ArchetypeChunkDataProxy Chunks;
        }

        public struct ArchetypeChunkDataProxy
        {
            public unsafe void** p;

            public unsafe int* data;

            public int Capacity;

            public int Count;
        }
    }

}