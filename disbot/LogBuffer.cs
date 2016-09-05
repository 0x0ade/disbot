using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisBot {
    public class LogBuffer {

        protected string[] _buffer;

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

        public virtual string this[int i] {
            get {
                return _buffer[(_position - CurrentSize + i) % _buffer.Length];
            }
            set {
                _buffer[_position % _buffer.Length] = value;
            }
        }

        public LogBuffer()
            : this(32) {
        }

        public LogBuffer(int size) {
            _buffer = new string[size];
        }

        public virtual void Add(string item) {
            _buffer[_position % _buffer.Length] = item;
            _position++;
        }

    }
}
