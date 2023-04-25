using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderWatcherService.src
{
    public class Changes
    {
        public List<string> Created { get; } = new List<string>();
        public List<string> Changed { get; } = new List<string>();
        public List<string> Deleted { get; } = new List<string>();
    }
}
