using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisBot {
    public class CircularBuffer<T> {

        protected T[] _buffer;

        private int _position;
        public virtual int Position {
            get {
                return _position;
            }
            set {
                _position = value;
            }
        }

        public virtual int Size {
            get {
                return _buffer.Length;
            }
            set {
                Array.Resize(ref _buffer, value);
            }
        }

        public int CurrentSize {
            get {
                return Math.Min(Position, Size);
            }
        }

        public virtual T this[int i] {
            get {
                return _buffer[(_position - CurrentSize + i) % _buffer.Length];
            }
            set {
                _buffer[_position % _buffer.Length] = value;
            }
        }

        public CircularBuffer()
            : this(32) {
        }

        public CircularBuffer(int size) {
            _buffer = new T[size];
        }

        public virtual void Add(T item) {
            _buffer[_position % _buffer.Length] = item;
            _position++;
        }

    }
}
