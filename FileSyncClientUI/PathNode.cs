using DevExpress.Xpf.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using FileSyncClientUICommon;

namespace FileSyncClientUI
{
    public class PathNode : INotifyPropertyChanged
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
                return string.Join(System.IO.Path.DirectorySeparatorChar, list);
            }
        }
        public bool IsExpand
        {
            get => _isExpand;
            set
            {
                _isExpand = value;
            }
        }
        private bool _isExpand;

        public ObservableCollection<PathNode> Nodes { get; set; } = new ObservableCollection<PathNode>();
        public PathNode? Parent { get; set; }
        public PathNode(string name)
        {
            Name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PathNode Append(string name)
        {
            var temp = new PathNode(name);
            temp.Parent = this;
            Nodes.Add(temp);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nodes)));
            return temp;
        }
        private void GetPath(PathNode node, ref List<string> path)
        {
            path.Insert(0, node.Name);
            if (node.Parent != null)
            {
                GetPath(node.Parent, ref path);
            }
        }

        public PathNode? FindChild(string path, int level)
        {
            if (path == null)
                return null;

            var names = path.Split(System.IO.Path.DirectorySeparatorChar);
            if (level >= names.Length)
                return null;

            if (names[level] == Name)
            {
                if (level == names.Length - 1)
                    return this;

                foreach (var sub in Nodes)
                {
                    var findout = sub.FindChild(path, level + 1);
                    if (findout != null)
                        return findout;
                }
            }
            return null;
        }
    }
}
