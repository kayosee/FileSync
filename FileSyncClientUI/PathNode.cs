using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileSyncClientUI
{
    public class PathNode
    {
        public string Name
        {
            get; set;
        }
        public string Path
        {
            get
            {
                var list = new List<string>();
                GetPath(this, ref list);
                return string.Join("/", list);
            }
        }
        public ObservableCollection<PathNode> Nodes { get; set; } = new ObservableCollection<PathNode>();
        public PathNode? Parent { get; set; }
        public PathNode(string name)
        {
            Name = name;
        }
        public PathNode Append(string name)
        {
            var temp = new PathNode(name);
            temp.Parent = this;
            Nodes.Append(temp);
            return temp;
        }
        public void GetPath(PathNode node, ref List<string> path)
        {
            path.Insert(0,node.Name);
            if (node.Parent != null)
            {
                GetPath(node.Parent, ref path);
            }
        }
    }
}
