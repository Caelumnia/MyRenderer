using System;
using Unity.Collections;

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
                Buffer.CopyTo(_temp);
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

        public void Fill(T value)
        {
            if (_temp.Length == 0) return;

            int copyLength = 1;
            _temp[0] = value;
            while (copyLength <= _temp.Length / 2)
            {
                Array.Copy(_temp, 0, _temp, copyLength, copyLength);
                copyLength *= 2;
            }

            Array.Copy(_temp, 0, _temp, copyLength, _temp.Length - copyLength);

            Buffer.CopyFrom(_temp);
        }
    }
}