using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderWatcherService.src
{
    public class State
    {
        public Dictionary<string, DateTimeOffset> Files { get; set; } = new Dictionary<string, DateTimeOffset>();
    }
}
