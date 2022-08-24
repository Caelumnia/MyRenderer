using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;

namespace MyRenderer
{
    public class NativeBuffer<T> where T : struct
    {
        public NativeArray<T> Buffer;
        private T[] _temp;

        public T[] Raw
        {
            get
            {
                Profiler.BeginSample($"NativeBuffer<{typeof(T).Name}>.Raw()");
                unsafe
                {
                    void* dest = UnsafeUtility.PinGCArrayAndGetDataAddress(_temp, out ulong handle);
                    UnsafeUtility.MemCpy(dest, Buffer.GetUnsafePtr(), Buffer.Length * UnsafeUtility.SizeOf<T>());
                    UnsafeUtility.ReleaseGCObject(handle);
                }
                Profiler.EndSample();

                return _temp;
            }
        }

        public NativeBuffer(int size)
        {
            Buffer = new NativeArray<T>(size, Allocator.Persistent);
            _temp = new T[size];
        }

        public void Release()
        {
            Buffer.Dispose();
            _temp = null;
        }

        public void Clear()
        {
            Profiler.BeginSample($"NativeBuffer<{typeof(T).Name}>.Clear()");
            unsafe
            {
                UnsafeUtility.MemSet(Buffer.GetUnsafePtr(), 0, Buffer.Length * UnsafeUtility.SizeOf<T>());
            }
            Profiler.EndSample();
        }

        public void Fill(T value)
        {
            Profiler.BeginSample($"NativeBuffer<{typeof(T).Name}>.Fill()");
            
            int copyLength = 1;
            _temp[0] = value;
            while (copyLength <= _temp.Length / 2)
            {
                Array.Copy(_temp, 0, _temp, copyLength, copyLength);
                copyLength *= 2;
            }

            Array.Copy(_temp, 0, _temp, copyLength, _temp.Length - copyLength);

            Buffer.CopyFrom(_temp);
            
            Profiler.EndSample();
        }
    }
}