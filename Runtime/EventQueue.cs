﻿using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Runtime.CompilerServices;
using System;

namespace Vella.Events
{
    /// <summary>
    /// A collection that allows systems/jobs to schedule event components to be created later in the frame.
    /// <para>
    /// Event components are attached to new entities by the <see cref="EntityEventSystem'"/>
    /// and exist for one frame only (until <see cref="EntityEventSystem"/>.OnUpdate() runs again).
    /// </para>
    /// <para>
    /// This is intended to be passed into jobs, where it will be injected with thread index 
    /// and utilize the corresponding queue dedicated to that thread.
    /// </para>Helpers
    /// 
    /// EventQueues do not need to be disposed manually by the systems creating them. 
    /// They are owned and disposed automatically by <see cref="EntityEventSystem"/>
    /// </summary>
    /// <typeparam name="T">type of event</typeparam>
    public unsafe struct EventQueue<T> where T : struct, IComponentData
    {
        [NativeSetThreadIndex]
        internal int _threadIndex;

        internal MultiAppendBuffer _data;

        public void Enqueue(T item) => _data.GetBuffer(_threadIndex).Add(item);

        public void Enqueue(NativeArray<T> items)
            => _data.GetBuffer(_threadIndex).Add(items.GetUnsafePtr(), UnsafeUtility.SizeOf<T>() * items.Length);
    }

    /// <summary>
    /// A collection that allows systems/jobs to schedule event components to be created later in the frame.
    /// <para>
    /// Event components are attached to new entities by the <see cref="EntityEventSystem'"/>
    /// and exist for one frame only (until <see cref="EntityEventSystem"/>.OnUpdate() runs again).
    /// </para>
    /// <para>
    /// This is intended to be passed into jobs, where it will be injected with thread index 
    /// and utilize the corresponding queue dedicated to that thread.
    /// </para>
    /// EventQueues do not need to be disposed manually by the systems creating them. 
    /// They are owned and disposed automatically by <see cref="EntityEventSystem"/>
    /// </summary>
    /// <typeparam name="TComponent">type of event component</typeparam>
    public unsafe struct EventQueue<TComponent,TBufferData> 
        where TComponent : struct, IComponentData
        where TBufferData : unmanaged, IBufferElementData
    {
        [NativeSetThreadIndex]
        private int _threadIndex;

        private MultiAppendBuffer _data;
        private MultiAppendBuffer _bufferMap;
        private MultiAppendBuffer _bufferData;

        public void Enqueue(TComponent item, void* items, int length)
        {
            _data.GetBuffer(_threadIndex).Add(item);

            var buffer = _bufferData.GetBuffer(_threadIndex);
            var offset = buffer.Size;

            _bufferMap.GetBuffer(_threadIndex).Add(new BufferLink
            {
                ThreadIndex = _threadIndex,
                Offset = offset,
                Length = length,
            });

            _bufferData.GetBuffer(_threadIndex).Add(items, UnsafeUtility.SizeOf<TBufferData>() * length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(TComponent component, NativeArray<TBufferData> array)
            => Enqueue(component, array.GetUnsafePtr(), array.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(TComponent item, DynamicBuffer<TBufferData> buffer)
        {
            var header = *(BufferHeaderProxy**)UnsafeUtility.AddressOf(ref buffer);
            var ptr = BufferHeaderProxy.GetElementPointer(header);
            Enqueue(item, ptr, buffer.Length);
        }
    }

    /// <summary>
    /// Wraps management of a queue for storing up components from multiple sources (c# events/systems/jobs/threads).
    /// This untyped version is a union with <see cref="EventQueue{T}"/>
    /// </summary>
    public unsafe struct EventQueue
    {
        [NativeSetThreadIndex]
        private int _threadIndex;

        private MultiAppendBuffer _componentData;
        public MultiAppendBuffer _bufferLinks;
        public MultiAppendBuffer _bufferData;

        private int _componentSize;
        private int _bufferElementSize;
        private int _cachedCount;

        public EventQueue(int componentSize, Allocator allocator) : this(componentSize, 0, allocator) { }

        public EventQueue(int componentSize, int bufferElementSize, Allocator allocator) : this()
        {
            _componentData = new MultiAppendBuffer(allocator);
            _bufferLinks = new MultiAppendBuffer(allocator);
            _bufferData = new MultiAppendBuffer(allocator);
            _threadIndex = MultiAppendBuffer.DefaultThreadIndex;
            _componentSize = componentSize;
            _bufferElementSize = bufferElementSize;
        }


        public static EventQueue<T> Create<T>(Allocator allocator) where T : struct, IComponentData
            => new EventQueue(UnsafeUtility.SizeOf<T>(), Allocator.Temp).Cast<EventQueue<T>>();

        public static EventQueue<TComponent, TBufferData> Create<TComponent, TBufferData>(Allocator allocator)
            where TComponent : struct, IComponentData
            where TBufferData : unmanaged, IBufferElementData
        {
            return new EventQueue(UnsafeUtility.SizeOf<TComponent>(), UnsafeUtility.SizeOf<TComponent>(), allocator)
                .Cast<EventQueue<TComponent, TBufferData>>();
        }

        public int CachedCount => _cachedCount;

        public int ComponentCount() => _componentSize != 0 ? _cachedCount = _componentData.Size() / _componentSize : 0;

        public int LinksCount() => _cachedCount = _bufferLinks.Size() / UnsafeUtility.SizeOf<BufferLink>();

        public int BufferElementCount() => _bufferElementSize != 0 ? _bufferData.Size() / _bufferElementSize : 0;

        public MultiAppendBuffer.Reader GetComponentReader() => _componentData.AsReader();

        public MultiAppendBuffer.Reader GetLinksReader() => _bufferLinks.AsReader();

        public MultiAppendBuffer.Reader GetBuffersReader() => _bufferLinks.AsReader();

        public ref UnsafeAppendBuffer GetComponentsForThread(int threadIndex = MultiAppendBuffer.DefaultThreadIndex)
        {
            if (_componentData.IsInvalidThreadIndex(threadIndex))
                throw new ArgumentException(nameof(threadIndex));

            return ref _componentData.GetBuffer(threadIndex);
        }

        public ref UnsafeAppendBuffer GetLinksForThread(int threadIndex = MultiAppendBuffer.DefaultThreadIndex)
        {
            if (_componentData.IsInvalidThreadIndex(threadIndex))
                throw new ArgumentException(nameof(threadIndex));

            return ref _bufferLinks.GetBuffer(threadIndex);
        }

        public ref UnsafeAppendBuffer GetBuffersForThread(int threadIndex = MultiAppendBuffer.DefaultThreadIndex)
        {
            if (_componentData.IsInvalidThreadIndex(threadIndex))
                throw new ArgumentException(nameof(threadIndex));

            return ref _bufferData.GetBuffer(threadIndex);
        }

        public T Cast<T>() where T : struct => UnsafeUtilityEx.AsRef<T>(UnsafeUtility.AddressOf(ref this));

        public void Clear()
        {
            _componentData.Clear();
            _bufferLinks.Clear();
            _bufferData.Clear();
        }

        public void Dispose()
        {
            _componentData.Dispose();
            _bufferLinks.Dispose();
            _bufferData.Dispose();
        }


    }

}