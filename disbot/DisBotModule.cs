using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisBot {
    public abstract class DisBotModule {

        public abstract string Name { get; }
        public abstract Version Version { get; }
        public abstract string Info { get; }

        public abstract void Init();
        public void Init(DisBotServerConfig server) { }

    }
}
